using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Clients;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Pages;

/// <summary>
/// Acceso a datos para las pantallas Blazor. Crea un ámbito por operación: en
/// Blazor Server el circuito es de larga vida y un DbContext compartido se
/// quedaría con datos obsoletos y crecería sin control.
/// </summary>
public sealed class ClientPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record ClientRow(Guid Id, string Name, string? Alias, string Status, DateTimeOffset CreatedAt);
    public sealed record ClientDetail(
        Guid Id, string Name, string? Alias, string Status,
        string? TaxId, string? Address, string? PostalCode, string? City, string? Province, string? Country,
        string? ContactName, string? ContactRole, string? ContactEmail, string? ContactPhone, string? ContactMobile,
        string? Website, string? Notes,
        DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, bool CanEdit)
    {
        public ClientDetails Details => new(Alias, TaxId, Address, PostalCode, City, Province, Country,
            ContactName, ContactRole, ContactEmail, ContactPhone, ContactMobile, Website, Notes);
    }
    public sealed record GrantRow(Guid Id, Guid UserId, string UserName, string Actions, DateTimeOffset GrantedAt, DateTimeOffset? ExpiresAt);
    public sealed record UserRow(Guid Id, string Name, string Role, bool IsActive);

    public async Task<(IReadOnlyList<ClientRow> Items, int Total)> ListAsync(string? query, int page, int pageSize)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, db) = await ResolveAsync(sp);
        if (user is null)
            return ([], 0);

        var visible = await access.GetVisibleClientIdsAsync(user);
        var clients = db.Clients.AsNoTracking().AsQueryable();

        if (visible is not null)
            clients = clients.Where(c => visible.Contains(c.Id));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            clients = clients.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Alias != null && c.Alias.ToLower().Contains(term)));
        }

        var total = await clients.CountAsync();
        var items = await clients
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClientRow(c.Id, c.Name, c.Alias, c.Status.ToString(), c.CreatedAt))
            .ToListAsync();

        return (items, total);
    }

    public async Task<ClientDetail?> GetAsync(Guid id)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, db) = await ResolveAsync(sp);
        if (user is null || !await access.CanViewAsync(user, id))
            return null;

        var canEdit = await access.CanEditAsync(user, id);
        return await db.Clients.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ClientDetail(
                c.Id, c.Name, c.Alias, c.Status.ToString(),
                c.TaxId, c.Address, c.PostalCode, c.City, c.Province, c.Country,
                c.ContactName, c.ContactRole, c.ContactEmail, c.ContactPhone, c.ContactMobile,
                c.Website, c.Notes,
                c.CreatedAt, c.UpdatedAt, canEdit))
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(string name, ClientDetails details)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, _, db) = await ResolveAsync(sp);
        if (user is null)
            return (false, "Sesión no válida.");

        if (!user.IsIn(AppRole.Administrator, AppRole.ClientManager))
            return (false, "No tienes permiso para crear clientes.");

        if (string.IsNullOrWhiteSpace(name))
            return (false, "El nombre es obligatorio.");

        var clean = name.Trim();
        if (await db.Clients.AnyAsync(c => c.Name == clean))
            return (false, "Ya existe un cliente con ese nombre.");

        var client = new Client(clean, details.Alias);
        client.UpdateDetails(details);
        db.Clients.Add(client);

        if (!user.IsIn(AppRole.Administrator))
            db.Grants.Add(new ClientGrant(user.Id, client.Id, GrantActions.View | GrantActions.Edit, user.DisplayName));

        await db.SaveChangesAsync();
        await AuditAsync(sp, user, "clients.create", client.Id, null);

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(Guid id, string? name, string? status, ClientDetails details)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, db) = await ResolveAsync(sp);
        if (user is null)
            return (false, "Sesión no válida.");

        if (!await access.CanEditAsync(user, id))
            return (false, "No tienes permiso para modificar este cliente.");

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null)
            return (false, "El cliente no existe.");

        if (!string.IsNullOrWhiteSpace(name) && name.Trim() != client.Name)
        {
            var clean = name.Trim();
            if (await db.Clients.AnyAsync(c => c.Name == clean && c.Id != id))
                return (false, "Ya existe otro cliente con ese nombre.");
            client.Rename(clean);
        }

        client.UpdateDetails(details);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<ClientStatus>(status, ignoreCase: true, out var parsed))
        {
            client.SetStatus(parsed);
        }

        await db.SaveChangesAsync();
        await AuditAsync(sp, user, "clients.update", id, null);

        return (true, null);
    }

    public async Task<IReadOnlyList<GrantRow>> GrantsAsync(Guid id)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, db) = await ResolveAsync(sp);
        if (user is null || !await access.CanEditAsync(user, id))
            return [];

        var rows = await (
            from g in db.Grants.AsNoTracking()
            join u in db.Users.AsNoTracking() on g.UserId equals u.Id
            where g.ClientId == id
            orderby u.DisplayName
            select new { g.Id, UserId = u.Id, UserName = u.DisplayName, g.Actions, g.GrantedAt, g.ExpiresAt }
        ).ToListAsync();

        return rows.Select(g => new GrantRow(g.Id, g.UserId, g.UserName, g.Actions.ToString(), g.GrantedAt, g.ExpiresAt)).ToList();
    }

    public async Task<IReadOnlyList<UserRow>> UsersAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, _, db) = await ResolveAsync(sp);
        if (user is null || !user.IsIn(AppRole.Administrator))
            return [];

        return await db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserRow(u.Id, u.DisplayName, u.Role.ToString(), u.IsActive))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Error)> UpsertGrantAsync(Guid clientId, Guid userId, GrantActions actions, DateTimeOffset? expiresAt)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, _, db) = await ResolveAsync(sp);
        if (user is null)
            return (false, "Sesión no válida.");

        if (!user.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede gestionar permisos.");

        if (actions == GrantActions.None)
            return (false, "Selecciona al menos una acción.");

        if (!await db.Clients.AnyAsync(c => c.Id == clientId))
            return (false, "El cliente no existe.");

        if (!await db.Users.AnyAsync(u => u.Id == userId))
            return (false, "El usuario no existe.");

        var existing = await db.Grants.FirstOrDefaultAsync(g => g.UserId == userId && g.ClientId == clientId);
        if (existing is null)
            db.Grants.Add(new ClientGrant(userId, clientId, actions, user.DisplayName, expiresAt));
        else
        {
            existing.ChangeActions(actions);
            existing.SetExpiry(expiresAt);
        }

        await db.SaveChangesAsync();
        await AuditAsync(sp, user, "grants.upsert", clientId, $"usuario={userId} acciones={actions}");

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RevokeAsync(Guid clientId, Guid grantId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, _, db) = await ResolveAsync(sp);
        if (user is null)
            return (false, "Sesión no válida.");

        if (!user.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede gestionar permisos.");

        var grant = await db.Grants.FirstOrDefaultAsync(g => g.Id == grantId && g.ClientId == clientId);
        if (grant is null)
            return (false, "La concesión no existe.");

        db.Grants.Remove(grant);
        await db.SaveChangesAsync();
        await AuditAsync(sp, user, "grants.revoke", clientId, $"userId={grant.UserId}");

        return (true, null);
    }

    private async Task<(AppUser? User, IClientAccessService Access, AppDbContext Db)> ResolveAsync(IServiceProvider sp)
    {
        if (await sp.GetRequiredService<ControClient.Web.Security.VaultLockService>().IsLockedAsync())
            return (null, sp.GetRequiredService<IClientAccessService>(), sp.GetRequiredService<AppDbContext>());

        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        if (current is null)
            return (null, sp.GetRequiredService<IClientAccessService>(), sp.GetRequiredService<AppDbContext>());

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, null);
        return (user.IsActive ? user : null, sp.GetRequiredService<IClientAccessService>(), sp.GetRequiredService<AppDbContext>());
    }

    private static async Task AuditAsync(IServiceProvider sp, AppUser user, string action, Guid clientId, string? reason)
    {
        var audit = sp.GetRequiredService<IAuditWriter>();
        await audit.WriteAsync(new AuditEvent(
            entity: "clients",
            action: action,
            actorName: user.DisplayName,
            actorObjectId: user.EntraObjectId,
            correlationId: Guid.NewGuid().ToString("N"),
            clientId: clientId,
            reason: reason));
    }
}
