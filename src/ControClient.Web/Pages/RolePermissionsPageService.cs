using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Security;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Pages;

/// <summary>Permisos de bóveda asignados a cada rol, editables por administración.</summary>
public sealed class RolePermissionsPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record RoleRow(AppRole Role, string Label, HashSet<string> Permissions);

    public async Task<IReadOnlyList<RoleRow>?> ListAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return null;

        var rows = await db.RoleVaultPermissions.AsNoTracking().ToListAsync();
        return Ui.AssignableRoles
            .Select(name => Enum.Parse<AppRole>(name))
            .Select(role => new RoleRow(
                role,
                Ui.RoleLabel(role.ToString()),
                rows.Where(r => r.Role == role).Select(r => r.Permission).ToHashSet()))
            .ToList();
    }

    public async Task<(bool Ok, string? Error)> SetAsync(AppRole role, string permission, bool enabled)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, db) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede cambiar permisos de bóveda.");

        if (!VaultPermissions.All.Any(p => p.Key == permission))
            return (false, "Permiso desconocido.");

        // Protección: la administración no puede quedarse sin gestionar la bóveda.
        if (role == AppRole.Administrator && permission == VaultPermissions.Manage && !enabled)
        {
            var remaining = await db.RoleVaultPermissions
                .CountAsync(p => p.Permission == VaultPermissions.Manage && p.Role != AppRole.Administrator);
            if (remaining == 0)
                return (false, "Debe haber al menos un rol que pueda gestionar la bóveda.");
        }

        var existing = await db.RoleVaultPermissions
            .FirstOrDefaultAsync(p => p.Role == role && p.Permission == permission);

        if (enabled && existing is null)
            db.RoleVaultPermissions.Add(new RoleVaultPermission(role, permission));
        else if (!enabled && existing is not null)
            db.RoleVaultPermissions.Remove(existing);

        await db.SaveChangesAsync();

        var audit = sp.GetRequiredService<IAuditWriter>();
        await audit.WriteAsync(new AuditEvent(
            entity: "roles", action: "roles.vault_permission", actorName: actor.DisplayName,
            actorObjectId: actor.EntraObjectId, correlationId: Guid.NewGuid().ToString("N"),
            reason: $"rol={role} permiso={permission} activo={enabled}"));

        return (true, null);
    }

    private async Task<(AppUser? Actor, AppDbContext Db)> ResolveAsync(IServiceProvider sp)
    {
        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        var db = sp.GetRequiredService<AppDbContext>();
        if (current is null)
            return (null, db);

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, current.Email);
        return (user.IsActive ? user : null, db);
    }
}
