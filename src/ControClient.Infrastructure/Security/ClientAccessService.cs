using ControClient.Domain.Abstractions;
using ControClient.Domain.Clients;
using ControClient.Domain.Security;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Security;

/// <summary>
/// Implementa el alcance real: rol + concesiones vigentes en base de datos.
/// Todo se resuelve en servidor; nunca se confía en el cliente (AC-05).
/// </summary>
public sealed class ClientAccessService(AppDbContext db, IClientAccessPolicy policy) : IClientAccessService
{
    public async Task<IReadOnlyCollection<Guid>?> GetVisibleClientIdsAsync(AppUser user, CancellationToken ct = default)
    {
        if (policy.CanViewAnyClient(user))
            return null; // todos

        var now = DateTimeOffset.UtcNow;
        var ids = await db.Grants
            .AsNoTracking()
            .Where(g => g.UserId == user.Id
                        && (g.ExpiresAt == null || g.ExpiresAt > now)
                        && (g.Actions & GrantActions.View) == GrantActions.View)
            .Select(g => g.ClientId)
            .Distinct()
            .ToListAsync(ct);

        return ids;
    }

    public async Task<bool> CanViewAsync(AppUser user, Guid clientId, CancellationToken ct = default)
    {
        if (!user.IsActive)
            return false;

        if (policy.CanViewAnyClient(user))
            return true;

        var actions = await GetActionsAsync(user.Id, clientId, ct);
        return actions.HasValue && (actions.Value & GrantActions.View) == GrantActions.View;
    }

    public async Task<bool> CanEditAsync(AppUser user, Guid clientId, CancellationToken ct = default)
    {
        if (!user.IsActive)
            return false;

        if (policy.CanEditAnyClient(user))
            return true;

        var actions = await GetActionsAsync(user.Id, clientId, ct);
        return actions.HasValue && (actions.Value & GrantActions.Edit) == GrantActions.Edit;
    }

    public async Task<bool> CanRevealAsync(AppUser user, Guid clientId, CancellationToken ct = default)
    {
        if (!policy.CanRevealSecretsByRole(user))
            return false;

        var actions = await GetActionsAsync(user.Id, clientId, ct);
        return actions.HasValue && (actions.Value & GrantActions.RevealSecret) == GrantActions.RevealSecret;
    }

    private async Task<GrantActions?> GetActionsAsync(Guid userId, Guid clientId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var grant = await db.Grants
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == userId && g.ClientId == clientId, ct);

        if (grant is null || !grant.IsActive(now))
            return null;

        return grant.Actions;
    }
}
