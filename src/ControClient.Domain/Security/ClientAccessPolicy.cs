using ControClient.Domain.Users;

namespace ControClient.Domain.Security;

/// <summary>
/// Autorización por cliente (PRM-01…PRM-09). Denegación por defecto y alcance
/// explícito: un rol global no concede revelado de secretos (SEC-06).
/// </summary>
public interface IClientAccessPolicy
{
    bool CanViewClient(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds);

    bool CanEditClient(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds);

    bool CanRevealSecret(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds);
}

public sealed class ClientAccessPolicy : IClientAccessPolicy
{
    private static readonly HashSet<AppRole> GlobalView = [AppRole.Administrator, AppRole.Auditor];
    private static readonly HashSet<AppRole> ScopedView =
    [
        AppRole.ClientManager, AppRole.Technician, AppRole.ReadOnly,
        AppRole.VaultCustodian, AppRole.MigrationOperator, AppRole.Acceptance
    ];
    private static readonly HashSet<AppRole> CanEdit = [AppRole.Administrator, AppRole.ClientManager, AppRole.Technician];
    private static readonly HashSet<AppRole> CanReveal = [AppRole.Technician, AppRole.VaultCustodian];

    public bool CanViewClient(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds)
    {
        if (user is null || !user.IsActive)
            return false;

        if (GlobalView.Contains(user.Role))
            return true;

        return ScopedView.Contains(user.Role) && grantedClientIds.Contains(clientId);
    }

    public bool CanEditClient(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds)
    {
        if (user is null || !user.IsActive || !CanEdit.Contains(user.Role))
            return false;

        return user.Role == AppRole.Administrator || grantedClientIds.Contains(clientId);
    }

    /// <summary>
    /// Revelado de secretos: un administrador NO puede revelar por su rol (matriz §3.3).
    /// Requiere rol técnico o custodio y alcance sobre el cliente.
    /// </summary>
    public bool CanRevealSecret(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds)
    {
        if (user is null || !user.IsActive || !CanReveal.Contains(user.Role))
            return false;

        return grantedClientIds.Contains(clientId);
    }
}

