using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Documents;
using ControClient.Infrastructure.Documents;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControClient.Web.Pages;

/// <summary>Documentos para las pantallas: listado, subida y nueva versión.</summary>
public sealed class DocumentPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record DocumentRow(Guid Id, string Name, string Classification, int CurrentVersion, long SizeBytes, string? FileName, DateTimeOffset UpdatedAt);

    public async Task<IReadOnlyList<DocumentRow>> ListAsync(Guid clientId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, documents) = await ResolveAsync(sp);
        if (user is null || !await access.CanViewAsync(user, clientId))
            return [];

        var list = await documents.ListAsync(clientId);
        return list.Select(d => new DocumentRow(
            d.Id, d.Name, d.Classification.ToString(), d.CurrentVersion,
            d.SizeBytes, d.LastFileName, d.UpdatedAt)).ToList();
    }

    public async Task<(bool Ok, string? Error)> UploadAsync(
        Guid clientId, string? name, DocumentClassification classification, string fileName, Stream content)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, documents) = await ResolveAsync(sp);
        if (user is null)
            return (false, "Sesión no válida.");

        if (!await access.CanEditAsync(user, clientId))
            return (false, "No tienes permiso para subir documentos a este cliente.");

        var displayName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(fileName) : name!;
        var (document, version, error) = await documents.CreateAsync(
            clientId, displayName, classification, fileName, null, content, 0, user.DisplayName);

        if (document is null || version is null)
            return (false, error ?? "No se pudo guardar el documento.");

        await AuditAsync(sp, user, "documents.upload", clientId, $"documento={document.Id} version={version.Version}");
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> NewVersionAsync(Guid documentId, string fileName, Stream content)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, documents) = await ResolveAsync(sp);
        if (user is null)
            return (false, "Sesión no válida.");

        var existing = await documents.GetAsync(documentId);
        if (existing is null || !await access.CanEditAsync(user, existing.ClientId))
            return (false, "No tienes permiso para modificar este documento.");

        var (version, error) = await documents.AddVersionAsync(
            documentId, fileName, null, content, 0, user.DisplayName);

        if (version is null)
            return (false, error ?? "No se pudo guardar la versión.");

        await AuditAsync(sp, user, "documents.new_version", existing.ClientId, $"documento={documentId} version={version.Version}");
        return (true, null);
    }

    private async Task<(Domain.Users.AppUser? User, IClientAccessService Access, DocumentService Documents)> ResolveAsync(IServiceProvider sp)
    {
        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        var access = sp.GetRequiredService<IClientAccessService>();
        var documents = sp.GetRequiredService<DocumentService>();

        if (current is null)
            return (null, access, documents);

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, null);
        return (user.IsActive ? user : null, access, documents);
    }

    private static async Task AuditAsync(IServiceProvider sp, Domain.Users.AppUser user, string action, Guid clientId, string reason)
    {
        var audit = sp.GetRequiredService<IAuditWriter>();
        await audit.WriteAsync(new AuditEvent(
            entity: "documents",
            action: action,
            actorName: user.DisplayName,
            actorObjectId: user.EntraObjectId,
            correlationId: Guid.NewGuid().ToString("N"),
            clientId: clientId,
            reason: reason));
    }
}
