using ControClient.Domain.Users;

namespace ControClient.Domain.Security;

/// <summary>
/// Autorización por cliente (PRM-01…PRM-09). Denegación por defecto y alcance
/// explícito: un rol global no concede revelado de secretos (SEC-06).
/// </summary>
public interface IClientAccessPolicy
{
    /// <summary>El rol ve todos los clientes sin necesidad de concesión (administrador, auditor).</summary>
    bool CanViewAnyClient(AppUser user);

    /// <summary>El rol puede editar cualquier cliente sin concesión (solo administrador).</summary>
    bool CanEditAnyClient(AppUser user);

    /// <summary>El rol puede revelar secretos si además tiene concesión sobre el cliente.</summary>
    bool CanRevealSecretsByRole(AppUser user);

    /// <summary>
    /// Cambiar la condición de secreto de un dato (marcar o desmarcar) es una
    /// capacidad de bóveda, no un permiso de cliente.
    /// </summary>
    bool CanManageSecrecy(AppUser user);

    bool CanViewClient(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds);

    bool CanEditClient(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds);

    bool CanRevealSecret(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds);
}

public sealed class ClientAccessPolicy : IClientAccessPolicy
{
    private static readonly HashSet<AppRole> GlobalView = [AppRole.Administrator, AppRole.Auditor];
    private static readonly HashSet<AppRole> ScopedView =
    [
        AppRole.Technician, AppRole.ReadOnly,
        AppRole.MigrationOperator, AppRole.Acceptance
    ];
    private static readonly HashSet<AppRole> CanEdit = [AppRole.Administrator, AppRole.Technician];
    private static readonly HashSet<AppRole> CanReveal = [AppRole.Technician, AppRole.VaultCustodian];

    public bool CanViewAnyClient(AppUser user) =>
        user is not null && user.IsActive && GlobalView.Contains(user.Role);

    public bool CanEditAnyClient(AppUser user) =>
        user is not null && user.IsActive && user.Role == AppRole.Administrator;

    public bool CanManageSecrecy(AppUser user) =>
        user is not null && user.IsActive && user.Role == AppRole.Administrator;

    public bool CanRevealSecretsByRole(AppUser user) =>
        user is not null && user.IsActive && CanReveal.Contains(user.Role);

    public bool CanViewClient(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds)
    {
        if (user is null || !user.IsActive)
            return false;

        if (CanViewAnyClient(user))
            return true;

        return ScopedView.Contains(user.Role) && grantedClientIds.Contains(clientId);
    }

    public bool CanEditClient(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds)
    {
        if (user is null || !user.IsActive || !CanEdit.Contains(user.Role))
            return false;

        return CanEditAnyClient(user) || grantedClientIds.Contains(clientId);
    }

    /// <summary>
    /// Revelado de secretos: un administrador NO puede revelar por su rol (matriz §3.3).
    /// Requiere rol técnico o custodio y alcance sobre el cliente.
    /// </summary>
    public bool CanRevealSecret(AppUser user, Guid clientId, IReadOnlyCollection<Guid> grantedClientIds)
    {
        if (!CanRevealSecretsByRole(user))
            return false;

        return grantedClientIds.Contains(clientId);
    }
}
