using ControClient.Domain.Users;

namespace ControClient.Domain.Security;

/// <summary>Permisos de bóveda asignables a cada rol.</summary>
public static class VaultPermissions
{
    public const string Manage = "vault.manage";        // constituir, desellar, sellar, recuperar
    public const string Reveal = "vault.reveal";        // revelar secretos (con permiso de cliente/ficha)
    public const string ManageSecrets = "vault.secrets"; // marcar/desmarcar campos secretos

    public static readonly (string Key, string Label, string Help)[] All =
    [
        (Manage, "Gestionar la bóveda", "Constituir, desellar, sellar y recuperar"),
        (Reveal, "Revelar secretos", "Ver el valor de un campo secreto (además del permiso de cliente o ficha)"),
        (ManageSecrets, "Marcar secretos", "Marcar o desmarcar campos como secretos")
    ];
}

/// <summary>Permiso de bóveda concedido a un rol.</summary>
public sealed class RoleVaultPermission
{
    private RoleVaultPermission() { }

    public RoleVaultPermission(AppRole role, string permission)
    {
        Role = role;
        Permission = permission;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public AppRole Role { get; private set; }
    public string Permission { get; private set; } = string.Empty;
}