using ControClient.Domain.Abstractions;
using ControClient.Domain.Clients;
using ControClient.Domain.Security;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Security;

/// <summary>
/// Implementa el alcance real: rol + concesiones directas + concesiones de grupo.
/// Todo se resuelve en servidor; nunca se confía en el cliente (AC-05).
/// El acceso efectivo es la unión (OR) de todas las concesiones vigentes.
/// </summary>
public sealed class ClientAccessService(AppDbContext db, IClientAccessPolicy policy) : IClientAccessService
{
    public async Task<IReadOnlyCollection<Guid>?> GetVisibleClientIdsAsync(AppUser user, CancellationToken ct = default)
    {
        if (policy.CanViewAnyClient(user))
            return null; // todos

        var now = DateTimeOffset.UtcNow;

        var direct = db.Grants.AsNoTracking()
            .Where(g => g.UserId == user.Id
                        && (g.ExpiresAt == null || g.ExpiresAt > now)
                        && (g.Actions & GrantActions.View) == GrantActions.View)
            .Select(g => g.ClientId);

        var viaGroups = from m in db.GroupMembers.AsNoTracking()
                        join g in db.Groups.AsNoTracking() on m.GroupId equals g.Id
                        join gg in db.GroupGrants.AsNoTracking() on m.GroupId equals gg.GroupId
                        where m.UserId == user.Id
                              && g.IsActive
                              && (gg.ExpiresAt == null || gg.ExpiresAt > now)
                              && (gg.Actions & GrantActions.View) == GrantActions.View
                        select gg.ClientId;

        return await direct.Concat(viaGroups).Distinct().ToListAsync(ct);
    }

    public async Task<bool> CanViewAsync(AppUser user, Guid clientId, CancellationToken ct = default)
    {
        if (!user.IsActive)
            return false;

        if (policy.CanViewAnyClient(user))
            return true;

        var actions = await GetEffectiveActionsAsync(user.Id, clientId, ct);
        return (actions & GrantActions.View) == GrantActions.View;
    }

    public async Task<bool> CanEditAsync(AppUser user, Guid clientId, CancellationToken ct = default)
    {
        if (!user.IsActive)
            return false;

        if (policy.CanEditAnyClient(user))
            return true;

        var actions = await GetEffectiveActionsAsync(user.Id, clientId, ct);
        return (actions & GrantActions.Edit) == GrantActions.Edit;
    }

    public async Task<bool> CanRevealAsync(AppUser user, Guid clientId, CancellationToken ct = default)
    {
        if (!policy.CanRevealSecretsByRole(user))
            return false;

        var actions = await GetEffectiveActionsAsync(user.Id, clientId, ct);
        return (actions & GrantActions.RevealSecret) == GrantActions.RevealSecret;
    }

    public async Task<bool> CanEditRecordAsync(AppUser user, Guid clientId, Guid recordId, CancellationToken ct = default)
    {
        if (!user.IsActive)
            return false;

        if (await CanEditAsync(user, clientId, ct))
            return true;

        // Permiso puntual sobre una ficha, concedido a alguno de sus grupos.
        return await (from rg in db.RecordGrants.AsNoTracking()
                      join m in db.GroupMembers.AsNoTracking() on rg.GroupId equals m.GroupId
                      join g in db.Groups.AsNoTracking() on rg.GroupId equals g.Id
                      where m.UserId == user.Id && rg.RecordId == recordId && g.IsActive
                            && (rg.Actions & GrantActions.Edit) == GrantActions.Edit
                      select rg.Id).AnyAsync(ct);
    }

    /// <summary>Unión de concesiones directas vigentes y de grupo (grupo activo).</summary>
    private async Task<GrantActions> GetEffectiveActionsAsync(Guid userId, Guid clientId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var direct = await db.Grants.AsNoTracking()
            .Where(g => g.UserId == userId && g.ClientId == clientId
                        && (g.ExpiresAt == null || g.ExpiresAt > now))
            .Select(g => (int)g.Actions)
            .ToListAsync(ct);

        var viaGroups = await (
            from m in db.GroupMembers.AsNoTracking()
            join g in db.Groups.AsNoTracking() on m.GroupId equals g.Id
            join gg in db.GroupGrants.AsNoTracking() on m.GroupId equals gg.GroupId
            where m.UserId == userId && gg.ClientId == clientId && g.IsActive
                  && (gg.ExpiresAt == null || gg.ExpiresAt > now)
            select (int)gg.Actions).ToListAsync(ct);

        var actions = GrantActions.None;
        foreach (var value in direct.Concat(viaGroups))
            actions |= (GrantActions)value;

        return actions;
    }
}
