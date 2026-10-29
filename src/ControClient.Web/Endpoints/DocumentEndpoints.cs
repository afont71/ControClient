using System.Security.Claims;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Documents;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Documents;
using ControClient.Web.Security;

namespace ControClient.Web.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();

        // ---------------------------------------------------------------- listado
        group.MapGet("/clients/{clientId:guid}/documents", async (
            Guid clientId,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!await access.CanViewAsync(user, clientId, ct))
                return Results.NotFound();

            var items = await documents.ListAsync(clientId, ct);
            return Results.Ok(items.Select(d => new
            {
                d.Id,
                d.ClientId,
                d.Name,
                Classification = d.Classification.ToString(),
                d.CurrentVersion,
                d.VersionCount,
                d.SizeBytes,
                d.LastFileName,
                d.UpdatedAt,
                d.CreatedBy
            }));
        });

        // ---------------------------------------------------------------- alta (multipart)
        group.MapPost("/clients/{clientId:guid}/documents", async (
            Guid clientId,
            HttpRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            DocumentService documents,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!await access.CanViewAsync(user, clientId, ct))
                return Results.NotFound();

            if (!await access.CanEditAsync(user, clientId, ct))
                return Results.Forbid();

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Se espera multipart/form-data." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Falta el fichero (campo 'file')." });

            var name = form["name"].ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileNameWithoutExtension(file.FileName);

            Enum.TryParse<DocumentClassification>(form["classification"].ToString(), ignoreCase: true, out var classification);

            await using var stream = file.OpenReadStream();
            var (document, version, error) = await documents.CreateAsync(
                clientId, name, classification, file.FileName, file.ContentType, stream, file.Length, user.DisplayName, ct);

            if (document is null || version is null)
                return Results.BadRequest(new { error });

            await audit.WriteAsync(new AuditEvent(
                entity: "documents",
                action: "documents.upload",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                clientId: clientId,
                reason: $"documento={document.Id} version={version.Version} sha256={version.Sha256[..12]}…"), ct);

            return Results.Created($"/api/v1/documents/{document.Id}",
                new { document.Id, document.Name, Classification = document.Classification.ToString(), Version = version.Version, version.Sha256, version.SizeBytes });
        }).DisableAntiforgery();

        // ---------------------------------------------------------------- detalle
        group.MapGet("/documents/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            DocumentService documents,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var document = await documents.GetAsync(id, ct);
            if (document is null || !await access.CanViewAsync(user, document.ClientId, ct))
                return Results.NotFound();

            return Results.Ok(new
            {
                document.Id,
                document.ClientId,
                document.Name,
                document.Description,
                Classification = document.Classification.ToString(),
                document.CreatedAt,
                document.CreatedBy,
                document.Versions
            });
        });

        // ---------------------------------------------------------------- nueva versión
        group.MapPost("/documents/{id:guid}/versions", async (
            Guid id,
            HttpRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            DocumentService documents,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var existing = await documents.GetAsync(id, ct);
            if (existing is null || !await access.CanViewAsync(user, existing.ClientId, ct))
                return Results.NotFound();

            if (!await access.CanEditAsync(user, existing.ClientId, ct))
                return Results.Forbid();

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Se espera multipart/form-data." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Falta el fichero (campo 'file')." });

            await using var stream = file.OpenReadStream();
            var (version, error) = await documents.AddVersionAsync(
                id, file.FileName, file.ContentType, stream, file.Length, user.DisplayName, ct);

            if (version is null)
                return Results.BadRequest(new { error });

            await audit.WriteAsync(new AuditEvent(
                entity: "documents",
                action: "documents.new_version",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                clientId: existing.ClientId,
                reason: $"documento={id} version={version.Version}"), ct);

            return Results.Ok(version);
        }).DisableAntiforgery();

        // ---------------------------------------------------------------- descarga
        group.MapGet("/documents/{id:guid}/content", async (
            Guid id,
            int? version,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            DocumentService documents,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var document = await documents.GetAsync(id, ct);
            if (document is null || !await access.CanViewAsync(user, document.ClientId, ct))
                return Results.NotFound();

            var (stream, fileName, contentType, error) = await documents.OpenAsync(id, version, ct);
            if (stream is null)
                return Results.NotFound(new { error });

            // La descarga queda auditada (SEC-08/SEC-09).
            await audit.WriteAsync(new AuditEvent(
                entity: "documents",
                action: "documents.download",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                clientId: document.ClientId,
                reason: $"documento={id} version={version?.ToString() ?? "latest"}"), ct);

            return Results.File(stream, contentType ?? "application/octet-stream", fileName, enableRangeProcessing: true);
        });

        return app;
    }
}
