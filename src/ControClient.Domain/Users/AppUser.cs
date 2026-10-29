namespace ControClient.Domain.Users;

/// <summary>
/// Usuario de la aplicación. La identidad real vive en Entra ID: aquí solo se
/// guarda el identificador estable y el rol concedido (SEC-01, PRM-01…PRM-16).
/// </summary>
public sealed class AppUser
{
    private AppUser() { }

    public AppUser(string entraObjectId, string displayName, string? email, AppRole role)
    {
        if (string.IsNullOrWhiteSpace(entraObjectId))
            throw new ArgumentException("El identificador de Entra es obligatorio.", nameof(entraObjectId));

        EntraObjectId = entraObjectId.Trim();
        DisplayName = displayName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Role = role;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string EntraObjectId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public AppRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; private set; }

    public void RegisterLogin() => LastLoginAt = DateTimeOffset.UtcNow;

    public bool IsIn(params AppRole[] roles) => IsActive && Array.IndexOf(roles, Role) >= 0;

    public void UpdateProfile(string displayName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            DisplayName = displayName.Trim();

        Email = string.IsNullOrWhiteSpace(email) ? Email : email.Trim();
    }

    public void ChangeRole(AppRole role) => Role = role;

    public void Deactivate() => IsActive = false;
}

/// <summary>Roles de §3.2. Un usuario puede necesitar varios; en v1 se usa el principal.</summary>
public enum AppRole
{
    Administrator = 0,
    ClientManager = 1,
    Technician = 2,
    Auditor = 3,
    ReadOnly = 4,
    VaultCustodian = 5,
    MigrationOperator = 6,
    Acceptance = 7
}

