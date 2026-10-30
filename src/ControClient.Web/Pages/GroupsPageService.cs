using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Clients;
using ControClient.Domain.Groups;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Pages;

/// <summary>Gestión de grupos: miembros y clientes asignados con permisos.</summary>
public sealed class GroupsPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record GroupRow(Guid Id, string Name, string? Description, bool IsActive, bool IsSystem, int Members, int Clients);
    public sealed record ClientPermissionRow(Guid ClientId, string ClientName, int Actions);
    public sealed record RecordPermissionRow(Guid RecordId, string Title, string TemplateName, bool HasOverride, int EffectiveActions);
    public sealed record MemberRow(Guid UserId, string Name, string? Email);
    public sealed record GrantRow(Guid ClientId, string ClientName, int Actions, DateTimeOffset? ExpiresAt);
    public sealed record GroupDetail(Guid Id, string Name, string? Description, IReadOnlyList<MemberRow> Members, IReadOnlyList<GrantRow> Grants);

    public async Task<IReadOnlyList<GroupRow>?> ListAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return null;

        return await db.Groups.AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GroupRow(g.Id, g.Name, g.Description, g.IsActive, g.IsSystem, g.Members.Count, g.Grants.Count))
            .ToListAsync();
    }

    public async Task<GroupDetail?> DetailAsync(Guid id)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return null;

        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
        if (group is null)
            return null;

        var members = await (
            from m in db.GroupMembers.AsNoTracking()
            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.GroupId == id
            orderby u.DisplayName
            select new MemberRow(u.Id, u.DisplayName, u.Email)).ToListAsync();

        var grants = await (
            from g in db.GroupGrants.AsNoTracking()
            join c in db.Clients.AsNoTracking() on g.ClientId equals c.Id
            where g.GroupId == id
            orderby c.Name
            select new GrantRow(c.Id, c.Name, (int)g.Actions, g.ExpiresAt)).ToListAsync();

        return new GroupDetail(group.Id, group.Name, group.Description, members, grants);
    }

    public async Task<(Guid? Id, string? Error)> CreateAsync(string name, string? description)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (null, "Solo un administrador puede crear grupos.");

        if (string.IsNullOrWhiteSpace(name))
            return (null, "El nombre es obligatorio.");

        if (await db.Groups.AnyAsync(g => g.Name == name.Trim()))
            return (null, "Ya existe un grupo con ese nombre.");

        var group = new Group(name, description, actor.DisplayName);
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        await AuditAsync(sp, actor, "groups.create", null, $"grupo={group.Id}");
        return (group.Id, null);
    }

    public async Task<IReadOnlyList<ClientPermissionRow>> ClientsMatrixAsync(Guid groupId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return [];

        var grants = await db.GroupGrants.AsNoTracking()
            .Where(g => g.GroupId == groupId)
            .ToDictionaryAsync(g => g.ClientId, g => (int)g.Actions);

        var clients = await db.Clients.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name }).ToListAsync();

        return clients.Select(c => new ClientPermissionRow(c.Id, c.Name, grants.GetValueOrDefault(c.Id, 0))).ToList();
    }

    public async Task<(bool Ok, string? Error)> SetClientActionsAsync(Guid groupId, Guid clientId, int actions)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede gestionar grupos.");

        if (await db.Groups.AnyAsync(g => g.Id == groupId && g.IsSystem))
            return (false, "El grupo Administradores es de sistema: se gestiona solo con el rol.");

        if (!await db.Clients.AnyAsync(c => c.Id == clientId))
            return (false, "El cliente no existe.");

        var grant = await db.GroupGrants.FirstOrDefaultAsync(g => g.GroupId == groupId && g.ClientId == clientId);

        if (actions <= 0)
        {
            if (grant is not null) db.GroupGrants.Remove(grant);
        }
        else if (grant is null)
        {
            db.GroupGrants.Add(new GroupGrant(groupId, clientId, (GrantActions)actions, actor.DisplayName));
        }
        else
        {
            grant.ChangeActions((GrantActions)actions);
        }

        await db.SaveChangesAsync();
        await AuditAsync(sp, actor, "groups.grant_clients", null, $"grupo={groupId} cliente={clientId} acciones={(GrantActions)actions}");
        return (true, null);
    }

    public async Task<IReadOnlyList<RecordPermissionRow>> RecordsAsync(Guid groupId, Guid clientId, int clientActions)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return [];

        var records = await (from r in db.Records.AsNoTracking()
                             join t in db.FormTemplates.AsNoTracking() on r.TemplateId equals t.Id
                             where r.ClientId == clientId
                             orderby t.Name, r.Title
                             select new { r.Id, r.Title, Template = t.Name }).ToListAsync();

        var ids = records.Select(r => r.Id).ToList();
        var grants = await db.RecordGrants.AsNoTracking()
            .Where(g => g.GroupId == groupId && ids.Contains(g.RecordId))
            .ToDictionaryAsync(g => g.RecordId, g => (int)g.Actions);

        return records.Select(r => new RecordPermissionRow(
            r.Id, r.Title, r.Template,
            grants.ContainsKey(r.Id),
            grants.TryGetValue(r.Id, out var value) ? value : clientActions)).ToList();
    }

    public async Task<(bool Ok, string? Error)> SetRecordPermissionsAsync(Guid groupId, Guid recordId, int actions)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede gestionar grupos.");

        if (await db.Groups.AnyAsync(g => g.Id == groupId && g.IsSystem))
            return (false, "El grupo Administradores es de sistema y ya tiene acceso total.");

        if (!await db.Records.AnyAsync(r => r.Id == recordId))
            return (false, "La ficha no existe.");

        var grant = await db.RecordGrants.FirstOrDefaultAsync(g => g.GroupId == groupId && g.RecordId == recordId);
        if (grant is null)
            db.RecordGrants.Add(new RecordGrant(groupId, recordId, (GrantActions)actions, actor.DisplayName));
        else
            grant.ChangeActions((GrantActions)actions);

        await db.SaveChangesAsync();
        await AuditAsync(sp, actor, "groups.record_permission", null,
            $"grupo={groupId} ficha={recordId} acciones={(GrantActions)actions}");
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RemoveRecordOverrideAsync(Guid groupId, Guid recordId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede gestionar grupos.");

        var grant = await db.RecordGrants.FirstOrDefaultAsync(g => g.GroupId == groupId && g.RecordId == recordId);
        if (grant is not null)
        {
            db.RecordGrants.Remove(grant);
            await db.SaveChangesAsync();
            await AuditAsync(sp, actor, "groups.record_permission", null, $"grupo={groupId} ficha={recordId} heredar=true");
        }

        return (true, null);
    }
    public async Task<(bool Ok, string? Error)> AddMemberAsync(Guid groupId, Guid userId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede gestionar grupos.");

        if (await db.Groups.AnyAsync(g => g.Id == groupId && g.IsSystem))
            return (false, "El grupo Administradores es de sistema: sus miembros dependen del rol.");

        if (await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId))
            return (true, null);

        db.GroupMembers.Add(new GroupMember(groupId, userId, actor.DisplayName));
        await db.SaveChangesAsync();
        await AuditAsync(sp, actor, "groups.add_member", null, $"grupo={groupId} usuario={userId}");
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RemoveMemberAsync(Guid groupId, Guid userId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede gestionar grupos.");

        if (await db.Groups.AnyAsync(g => g.Id == groupId && g.IsSystem))
            return (false, "El grupo Administradores es de sistema: sus miembros dependen del rol.");

        var member = await db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (member is null)
            return (true, null);

        db.GroupMembers.Remove(member);
        await db.SaveChangesAsync();
        await AuditAsync(sp, actor, "groups.remove_member", null, $"grupo={groupId} usuario={userId}");
        return (true, null);
    }

    public async Task<(int Updated, string? Error)> GrantClientsAsync(Guid groupId, IReadOnlyCollection<Guid> clientIds, GrantActions actions)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (0, "Solo un administrador puede gestionar grupos.");

        if (clientIds.Count == 0 || actions == GrantActions.None)
            return (0, "Selecciona al menos un cliente y una acción.");

        var valid = await db.Clients.Where(c => clientIds.Contains(c.Id)).Select(c => c.Id).ToListAsync();
        var existing = await db.GroupGrants.Where(g => g.GroupId == groupId && valid.Contains(g.ClientId)).ToListAsync();

        foreach (var clientId in valid)
        {
            var grant = existing.FirstOrDefault(g => g.ClientId == clientId);
            if (grant is null)
                db.GroupGrants.Add(new GroupGrant(groupId, clientId, actions, actor.DisplayName));
            else
                grant.ChangeActions(actions);
        }

        await db.SaveChangesAsync();
        await AuditAsync(sp, actor, "groups.grant_clients", null, $"grupo={groupId} clientes={valid.Count} acciones={actions}");
        return (valid.Count, null);
    }

    public async Task<(bool Ok, string? Error)> RevokeClientAsync(Guid groupId, Guid clientId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede gestionar grupos.");

        var grant = await db.GroupGrants.FirstOrDefaultAsync(g => g.GroupId == groupId && g.ClientId == clientId);
        if (grant is not null)
        {
            db.GroupGrants.Remove(grant);
            await db.SaveChangesAsync();
            await AuditAsync(sp, actor, "groups.revoke_client", null, $"grupo={groupId} cliente={clientId}");
        }

        return (true, null);
    }

    public async Task<IReadOnlyList<(Guid Id, string Name)>> UsersAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return [];

        return await db.Users.AsNoTracking().Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new ValueTuple<Guid, string>(u.Id, u.DisplayName))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<(Guid Id, string Name)>> ClientsAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return [];

        return await db.Clients.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new ValueTuple<Guid, string>(c.Id, c.Name))
            .ToListAsync();
    }

    private async Task<(AppUser? Actor, AppDbContext Db)> ResolveAsync(IServiceProvider sp)
    {
        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        var db = sp.GetRequiredService<AppDbContext>();

        if (await sp.GetRequiredService<ControClient.Web.Security.VaultLockService>().IsLockedAsync())
            return (null, db);

        if (current is null)
            return (null, db);

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, null);
        return (user.IsActive ? user : null, db);
    }

    private static async Task AuditAsync(IServiceProvider sp, AppUser actor, string action, Guid? clientId, string reason)
    {
        var audit = sp.GetRequiredService<IAuditWriter>();
        await audit.WriteAsync(new AuditEvent(
            entity: "groups", action: action, actorName: actor.DisplayName,
            actorObjectId: actor.EntraObjectId, correlationId: Guid.NewGuid().ToString("N"),
            clientId: clientId, reason: reason));
    }
}
