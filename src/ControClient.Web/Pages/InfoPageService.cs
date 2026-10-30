using ControClient.Domain.Abstractions;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Pages;

/// <summary>Vista global de información: fichas de todos los clientes visibles.</summary>
public sealed class InfoPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record InfoRow(Guid Id, Guid ClientId, string ClientName, Guid TemplateId, string TemplateName, string Title, int Version, DateTimeOffset? UpdatedAt);
    public sealed record TemplateOption(Guid Id, string Name);
    public sealed record ClientOption(Guid Id, string Name);

    public async Task<IReadOnlyList<InfoRow>?> SearchAsync(string? text, Guid? templateId, Guid? clientId)
    {
        using var scope = scopes.CreateScope();
        if (scope.ServiceProvider.GetService(typeof(ControClient.Web.Security.VaultLockService)) is ControClient.Web.Security.VaultLockService gate && await gate.IsLockedAsync())
            return null;

        var sp = scope.ServiceProvider;

        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        if (current is null)
            return null;

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, current.Email);
        if (!user.IsActive)
            return null;

        var access = sp.GetRequiredService<IClientAccessService>();
        var visible = await access.GetVisibleClientIdsAsync(user);

        var db = sp.GetRequiredService<AppDbContext>();
        var query = from r in db.Records.AsNoTracking()
                    join t in db.FormTemplates.AsNoTracking() on r.TemplateId equals t.Id
                    join c in db.Clients.AsNoTracking() on r.ClientId equals c.Id
                    select new { r, TemplateName = t.Name, ClientName = c.Name };

        if (visible is not null)
            query = query.Where(x => visible.Contains(x.r.ClientId));

        if (templateId is Guid tid)
            query = query.Where(x => x.r.TemplateId == tid);

        if (clientId is Guid cid)
            query = query.Where(x => x.r.ClientId == cid);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var term = text.Trim().ToLower();
            query = query.Where(x =>
                x.r.Title.ToLower().Contains(term)
                || x.TemplateName.ToLower().Contains(term)
                || x.ClientName.ToLower().Contains(term));
        }

        var rows = await query
            .OrderBy(x => x.ClientName).ThenBy(x => x.TemplateName).ThenBy(x => x.r.Title)
            .Take(300)
            .Select(x => new InfoRow(x.r.Id, x.r.ClientId, x.ClientName, x.r.TemplateId, x.TemplateName,
                x.r.Title, x.r.Version, x.r.UpdatedAt))
            .ToListAsync();

        // Una ficha puede tener el ver retirado expresamente.
        var visibleRows = new List<InfoRow>();
        foreach (var row in rows)
        {
            if (await access.CanViewRecordAsync(user, row.ClientId, row.Id))
                visibleRows.Add(row);
        }

        return visibleRows;
    }

    public async Task<IReadOnlyList<TemplateOption>> TemplatesAsync()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var list = await db.FormTemplates.AsNoTracking().Where(t => t.IsActive).ToListAsync();
        return list
            .GroupBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(t => t.Version).First())
            .OrderBy(t => t.Name)
            .Select(t => new TemplateOption(t.Id, t.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<ClientOption>> ClientsAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;

        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        if (current is null)
            return [];

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, current.Email);
        var access = sp.GetRequiredService<IClientAccessService>();
        var visible = await access.GetVisibleClientIdsAsync(user);

        var db = sp.GetRequiredService<AppDbContext>();
        var clients = db.Clients.AsNoTracking().AsQueryable();
        if (visible is not null)
            clients = clients.Where(c => visible.Contains(c.Id));

        return await clients.OrderBy(c => c.Name).Select(c => new ClientOption(c.Id, c.Name)).ToListAsync();
    }
}
