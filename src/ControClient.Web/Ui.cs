namespace ControClient.Web;

/// <summary>Etiquetas y estilos de presentación compartidos por las pantallas.</summary>
public static class Ui
{
    /// <summary>Roles que se pueden asignar desde la interfaz (los técnicos de migración quedan fuera).</summary>
    public static IReadOnlyList<string> AssignableRoles { get; } =
    [
        nameof(ControClient.Domain.Users.AppRole.Administrator),
        nameof(ControClient.Domain.Users.AppRole.ClientManager),
        nameof(ControClient.Domain.Users.AppRole.Technician),
        nameof(ControClient.Domain.Users.AppRole.Auditor),
        nameof(ControClient.Domain.Users.AppRole.ReadOnly),
        nameof(ControClient.Domain.Users.AppRole.VaultCustodian)
    ];

    public static string StatusLabel(string? status) => status switch
    {
        "Active" => "Activo",
        "PendingValidation" => "Pendiente de validar",
        "Suspended" => "Suspendido",
        "Archived" => "Archivado",
        null or "" => "—",
        _ => status
    };

    public static string StatusBadge(string? status) => status switch
    {
        "Active" => "ok",
        "Suspended" or "Archived" => "danger",
        "PendingValidation" => "warn",
        _ => ""
    };

    public static string RoleLabel(string? role) => role switch
    {
        "Administrator" => "Administrador",
        "ClientManager" => "Responsable de clientes",
        "Technician" => "Técnico",
        "Auditor" => "Auditor",
        "ReadOnly" => "Solo lectura",
        "VaultCustodian" => "Custodio de bóveda",
        "MigrationOperator" => "Operador de migración",
        "Acceptance" => "Aceptación",
        _ => role ?? "—"
    };
}
