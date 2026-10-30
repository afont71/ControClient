using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Groups;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Pages;

/// <summary>Gestión de usuarios para administración: rol, estado y grupos.</summary>
public sealed class UsersPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record UserRow(Guid Id, string ObjectId, string Name, string? Email, string Role, bool IsActive, bool CanMarkSecrets, DateTimeOffset? LastLogin, string Groups, bool Pending);

    public async Task<IReadOnlyList<(Guid Id, string Name)>> GroupsAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return [];

        return await db.Groups.AsNoTracking().Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new ValueTuple<Guid, string>(g.Id, g.Name))
            .ToListAsync();
    }

    public async Task<(Guid? Id, string? Error)> PreRegisterAsync(
        string email, string? displayName, string role, IReadOnlyCollection<Guid>? groupIds = null)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (null, "Solo un administrador puede pre-registrar usuarios.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return (null, "Correo válido obligatorio.");

        var normalized = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == normalized))
            return (null, "Ya existe un usuario con ese correo.");

        if (!Enum.TryParse<AppRole>(role, ignoreCase: true, out var parsed))
            parsed = AppRole.ReadOnly;

        var name = string.IsNullOrWhiteSpace(displayName) ? normalized : displayName.Trim();
        var entity = new AppUser("pending:" + normalized, name, normalized, parsed);
        db.Users.Add(entity);
        await db.SaveChangesAsync();

        // Grupos: los permisos se heredan de sus clientes en cuanto entre.
        if (groupIds is { Count: > 0 })
        {
            var valid = await db.Groups.Where(g => groupIds.Contains(g.Id) && g.IsActive).Select(g => g.Id).ToListAsync();
            foreach (var groupId in valid)
                db.GroupMembers.Add(new GroupMember(groupId, entity.Id, actor.DisplayName));

            await db.SaveChangesAsync();
        }

        await AuditAsync(sp, actor, "users.pre_register", $"usuario={normalized} rol={parsed} grupos={groupIds?.Count ?? 0}");
        return (entity.Id, null);
    }

    public async Task<IReadOnlyList<UserRow>?> ListAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return null;

        var users = await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync();

        var memberships = await (
            from m in db.GroupMembers.AsNoTracking()
            join g in db.Groups.AsNoTracking() on m.GroupId equals g.Id
            select new { m.UserId, GroupName = g.Name }).ToListAsync();

        return users.Select(u => new UserRow(
            u.Id,
            u.EntraObjectId,
            u.DisplayName,
            u.Email,
            u.Role.ToString(),
            u.IsActive,
            u.CanMarkSecrets,
            u.LastLoginAt,
            string.Join(", ", memberships.Where(m => m.UserId == u.Id).Select(m => m.GroupName)),
            u.IsPending
        )).ToList();
    }

    public async Task<(bool Ok, string? Error)> DeletePendingAsync(Guid userId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede eliminar fichas.");

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (target is null)
            return (true, null);

        if (!target.IsPending)
            return (false, "Solo se pueden eliminar fichas pendientes. Para un usuario real, desactívalo.");

        var email = target.Email;
        db.Users.Remove(target);
        await db.SaveChangesAsync();
        await AuditAsync(sp, actor, "users.delete_pending", $"usuario={email}");
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetCanMarkSecretsAsync(Guid userId, bool allowed)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede cambiar este permiso.");

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (target is null)
            return (false, "Usuario inexistente.");

        if (target.Role == AppRole.Administrator && !allowed)
            return (false, "Un administrador siempre puede marcar secretos.");

        target.SetCanMarkSecrets(allowed);
        await db.SaveChangesAsync();
        await AuditAsync(sp, actor, "users.mark_secrets", $"usuario={target.EntraObjectId} permitido={allowed}");
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetRoleAsync(Guid userId, string role)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede cambiar roles.");

        if (!Enum.TryParse<AppRole>(role, ignoreCase: true, out var parsed))
            return (false, "Rol no válido.");

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (target is null)
            return (false, "Usuario inexistente.");

        if (target.Id == actor.Id)
            return (false, "No puedes cambiar tu propio rol.");

        target.ChangeRole(parsed);
        await db.SaveChangesAsync();
        await SyncSystemMembershipAsync(db, target);
        await AuditAsync(sp, actor, "users.change_role", $"usuario={target.EntraObjectId} rol={parsed}");
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetActiveAsync(Guid userId, bool active)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede activar o desactivar usuarios.");

        if (userId == actor.Id)
            return (false, "No puedes desactivar tu propia cuenta.");

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (target is null)
            return (false, "Usuario inexistente.");

        if (active) target.Activate(); else target.Deactivate();
        await db.SaveChangesAsync();
        await AuditAsync(sp, actor, active ? "users.activate" : "users.deactivate", $"usuario={target.EntraObjectId}");
        return (true, null);
    }

    private static async Task SyncSystemMembershipAsync(AppDbContext db, AppUser user)
    {
        var systemGroup = await db.Groups.FirstOrDefaultAsync(g => g.IsSystem);
        if (systemGroup is null)
            return;

        var membership = await db.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == systemGroup.Id && m.UserId == user.Id);

        if (user.Role == AppRole.Administrator && user.IsActive)
        {
            if (membership is null)
            {
                db.GroupMembers.Add(new GroupMember(systemGroup.Id, user.Id, "sistema"));
                await db.SaveChangesAsync();
            }
        }
        else if (membership is not null)
        {
            db.GroupMembers.Remove(membership);
            await db.SaveChangesAsync();
        }
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

    private static async Task AuditAsync(IServiceProvider sp, AppUser actor, string action, string reason)
    {
        var audit = sp.GetRequiredService<IAuditWriter>();
        await audit.WriteAsync(new AuditEvent(
            entity: "users", action: action, actorName: actor.DisplayName,
            actorObjectId: actor.EntraObjectId, correlationId: Guid.NewGuid().ToString("N"), reason: reason));
    }
}
