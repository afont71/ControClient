namespace ControClient.Domain.Clients;

/// <summary>
/// Concesión de acceso de un usuario a un cliente concreto (PRM-01…PRM-09).
/// Denegación por defecto: sin concesión vigente no hay acceso.
/// </summary>
public sealed class ClientGrant
{
    private ClientGrant() { }

    public ClientGrant(Guid userId, Guid clientId, GrantActions actions, string? grantedBy = null, DateTimeOffset? expiresAt = null)
    {
        if (actions == GrantActions.None)
            throw new ArgumentException("La concesión debe incluir al menos una acción.", nameof(actions));

        UserId = userId;
        ClientId = clientId;
        Actions = actions;
        GrantedBy = grantedBy;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid ClientId { get; private set; }
    public GrantActions Actions { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string? GrantedBy { get; private set; }

    public bool IsActive(DateTimeOffset now) => ExpiresAt is null || ExpiresAt > now;

    public bool Allows(GrantActions action, DateTimeOffset now) =>
        IsActive(now) && (Actions & action) == action;

    public void ChangeActions(GrantActions actions)
    {
        if (actions == GrantActions.None)
            throw new ArgumentException("La concesión debe incluir al menos una acción.", nameof(actions));

        Actions = actions;
    }

    public void SetExpiry(DateTimeOffset? expiresAt) => ExpiresAt = expiresAt;
}

/// <summary>Acciones concedibles sobre un cliente (subconjunto de §3.3).</summary>
[Flags]
public enum GrantActions
{
    None = 0,
    View = 1,
    Edit = 2,
    RevealSecret = 4
}
