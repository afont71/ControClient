using ControClient.Domain.Abstractions;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Pages;

/// <summary>Consulta de auditoría para administración y auditoría.</summary>
public sealed class AuditPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record EventRow(long Id, DateTimeOffset At, string Actor, string Entity, string Action, string Result, string? Reason, Guid? ClientId);

    public async Task<IReadOnlyList<EventRow>?> ListAsync(string? action, string? actor, int limit = 200)
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
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, null);
        if (!user.IsIn(AppRole.Administrator, AppRole.Auditor))
            return null;

        var db = sp.GetRequiredService<AppDbContext>();
        var query = db.AuditEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.Action.ToLower().Contains(action.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(e => e.ActorName.ToLower().Contains(actor.Trim().ToLower()));

        return await query
            .OrderByDescending(e => e.OccurredAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(e => new EventRow(e.Id, e.OccurredAt, e.ActorName, e.Entity, e.Action, e.Result, e.Reason, e.ClientId))
            .ToListAsync();
    }
}
