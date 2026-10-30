using System.Security.Claims;
using System.Text.Json;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Forms;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Forms;
using ControClient.Web.Security;

namespace ControClient.Web.Endpoints;

public static class FormEndpoints
{
    public static IEndpointRouteBuilder MapFormEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();

        // ---------------------------------------------------------------- plantillas
        group.MapGet("/templates", async (
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            FormService forms,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var includeInactive = user.IsIn(AppRole.Administrator);
            return Results.Ok(await forms.ListTemplatesAsync(includeInactive, ct));
        });

        group.MapPost("/templates", async (
            CreateTemplateRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            FormService forms,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            // Crear tipos de ficha es administración de plantillas (PRM-14).
            if (!user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Name) || request.Fields.Count == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["template"] = ["Clave, nombre y al menos un campo son obligatorios."]
                });

            try
            {
                var specs = request.Fields.Select((f, index) => new FormService.FieldSpec(
                    f.Key,
                    f.Label,
                    Enum.TryParse<FieldType>(f.Type, ignoreCase: true, out var type) ? type : FieldType.Text,
                    f.Required,
                    f.Options is { Length: > 0 } ? JsonSerializer.Serialize(f.Options) : null,
                    f.HelpText,
                    f.Order == 0 ? index + 1 : f.Order,
                    f.IsSecret)).ToList();

                var template = await forms.CreateTemplateAsync(request.Key, request.Name, request.Description, specs, ct);

                await audit.WriteAsync(new AuditEvent(
                    entity: "form_templates",
                    action: "templates.create",
                    actorName: user.DisplayName,
                    actorObjectId: user.EntraObjectId,
                    correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                    reason: $"key={template.Key} version={template.Version}"), ct);

                return Results.Created($"/api/v1/templates/{template.Id}",
                    new { template.Id, template.Key, template.Name, template.Version });
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["template"] = [ex.Message] });
            }
        });

        // ---------------------------------------------------------------- fichas por cliente
        group.MapGet("/clients/{clientId:guid}/records", async (
            Guid clientId,
            Guid? templateId,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            FormService forms,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!await access.CanViewAsync(user, clientId, ct))
                return Results.NotFound();

            var all = await forms.ListRecordsAsync(clientId, templateId, ct);
            var visible = new List<ControClient.Infrastructure.Forms.FormService.RecordSummary>();
            foreach (var r in all)
            {
                if (await access.CanViewRecordAsync(user, clientId, r.Id, ct))
                    visible.Add(r);
            }

            return Results.Ok(visible);
        });

        group.MapPost("/clients/{clientId:guid}/records", async (
            Guid clientId,
            CreateRecordRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            FormService forms,
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

            var canManageSecrecy = await access.CanManageSecrecyAsync(user, clientId, ct);
            var (record, errors, error) = await forms.CreateRecordAsync(
                clientId, request.TemplateId, request.Title, request.Values, user.DisplayName, request.Secrets, request.SecretFlags, canManageSecrecy, ct: ct);

            if (errors is not null)
                return Results.ValidationProblem(errors.ToDictionary(e => e.Key, e => e.Value));

            if (record is null)
                return Results.BadRequest(new { error });

            await audit.WriteAsync(new AuditEvent(
                entity: "record_instances",
                action: "records.create",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                clientId: clientId,
                reason: $"ficha={record.Id} plantilla={record.TemplateId}"), ct);

            return Results.Created($"/api/v1/records/{record.Id}",
                new { record.Id, record.ClientId, record.TemplateId, record.TemplateVersion, record.Title, record.Version });
        });

        // ---------------------------------------------------------------- ficha individual
        group.MapGet("/records/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            FormService forms,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var record = await forms.GetRecordAsync(id, ct);
            if (record is null)
                return Results.NotFound();

            if (!await access.CanViewRecordAsync(user, record.ClientId, record.Id, ct))
                return Results.NotFound();

            var template = await forms.GetTemplateAsync(record.TemplateId, ct);
            var canEdit = await access.CanEditRecordAsync(user, record.ClientId, record.Id, ct);

            return Results.Ok(new
            {
                record.Id,
                record.ClientId,
                record.TemplateId,
                record.TemplateVersion,
                record.Title,
                Values = JsonDocument.Parse(record.ValuesJson).RootElement,
                record.Version,
                record.CreatedAt,
                record.UpdatedAt,
                record.CreatedBy,
                CanEdit = canEdit,
                Template = template is null ? null : new
                {
                    template.Id,
                    template.Name,
                    Fields = template.Fields.OrderBy(f => f.Order).Select(f => new
                    {
                        f.Key,
                        f.Label,
                        Type = f.Type.ToString(),
                        f.IsRequired,
                        f.HelpText,
                        Options = f.OptionsJson is null ? null : (object)JsonDocument.Parse(f.OptionsJson).RootElement
                    })
                }
            });
        });

        group.MapPatch("/records/{id:guid}", async (
            Guid id,
            UpdateRecordRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            FormService forms,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var existing = await forms.GetRecordAsync(id, ct);
            if (existing is null || !await access.CanViewRecordAsync(user, existing.ClientId, existing.Id, ct))
                return Results.NotFound();

            if (!await access.CanEditRecordAsync(user, existing.ClientId, existing.Id, ct))
                return Results.Forbid();

            var canManageSecrecy = await access.CanManageSecrecyAsync(user, existing.ClientId, ct);
            var (record, errors, error, notFound, conflict) = await forms.UpdateRecordAsync(
                id, request.Title, request.Values, request.Version, request.Secrets, user.DisplayName, request.SecretFlags, canManageSecrecy, ct: ct);

            if (conflict)
                return Results.Conflict(new { error });

            if (notFound)
                return Results.NotFound();

            if (errors is not null)
                return Results.ValidationProblem(errors.ToDictionary(e => e.Key, e => e.Value));

            if (record is null)
                return Results.BadRequest(new { error });

            await audit.WriteAsync(new AuditEvent(
                entity: "record_instances",
                action: "records.update",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                clientId: record.ClientId,
                reason: $"ficha={record.Id} version={record.Version}"), ct);

            return Results.Ok(new { record.Id, record.Title, record.Version, record.UpdatedAt });
        });

        group.MapDelete("/records/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            FormService forms,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var record = await forms.GetRecordAsync(id, ct);
            if (record is null)
                return Results.NotFound();

            if (!await access.CanEditRecordAsync(user, record.ClientId, record.Id, ct))
                return Results.Forbid();

            var title = record.Title;
            if (!await forms.DeleteRecordAsync(id, ct))
                return Results.NotFound();

            await audit.WriteAsync(new AuditEvent(
                entity: "records",
                action: "records.delete",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                clientId: record.ClientId,
                reason: $"ficha={title}"), ct);

            return Results.NoContent();
        });

        // Revelar un campo secreto: exige ver la ficha, permiso de revelar
        // (el de la ficha manda sobre el del cliente) y la bóveda desellada.
        group.MapPost("/records/{id:guid}/fields/{fieldKey}/reveal", async (
            Guid id,
            string fieldKey,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            FormService forms,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var record = await forms.GetRecordAsync(id, ct);
            if (record is null)
                return Results.NotFound();

            if (!await access.CanRevealRecordAsync(user, record.ClientId, record.Id, ct))
                return Results.Forbid();

            try
            {
                var value = await forms.RevealSecretAsync(id, fieldKey, ct);
                if (value is null)
                    return Results.NotFound(new { error = "Ese campo no tiene valor guardado." });

                await audit.WriteAsync(new AuditEvent(
                    entity: "records",
                    action: "records.reveal_field",
                    actorName: user.DisplayName,
                    actorObjectId: user.EntraObjectId,
                    correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                    clientId: record.ClientId,
                    reason: $"ficha={id} campo={fieldKey}"), ct);

                return Results.Ok(new { value });
            }
            catch (ControClient.Infrastructure.Vault.VaultSealedException)
            {
                return Results.Json(new { error = "La bóveda está sellada." },
                    statusCode: StatusCodes.Status423Locked);
            }
        });

        return app;
    }

    public sealed record CreateTemplateRequest(string Key, string Name, string? Description, List<FieldRequest> Fields);
    public sealed record FieldRequest(string Key, string Label, string Type, bool Required = false, string[]? Options = null, string? HelpText = null, int Order = 0, bool IsSecret = false);
    public sealed record CreateRecordRequest(Guid TemplateId, string Title, JsonElement Values, Dictionary<string, string>? Secrets = null, Dictionary<string, bool>? SecretFlags = null);
    public sealed record UpdateRecordRequest(string? Title, JsonElement Values, int Version, Dictionary<string, string>? Secrets = null, Dictionary<string, bool>? SecretFlags = null);
}
