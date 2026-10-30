using ControClient.Domain.Security;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Security;

/// <summary>
/// Permisos de bóveda por defecto. Idempotente: solo siembra si la tabla está
/// vacía, para no pisar lo que configure un administrador.
/// </summary>
public static class VaultPermissionSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.RoleVaultPermissions.AnyAsync(ct))
            return;

        db.RoleVaultPermissions.AddRange(
            new RoleVaultPermission(AppRole.Administrator, VaultPermissions.Manage),
            new RoleVaultPermission(AppRole.Administrator, VaultPermissions.ManageSecrets),
            new RoleVaultPermission(AppRole.VaultCustodian, VaultPermissions.Reveal),
            new RoleVaultPermission(AppRole.VaultCustodian, VaultPermissions.ManageSecrets),
            new RoleVaultPermission(AppRole.Technician, VaultPermissions.Reveal));

        await db.SaveChangesAsync(ct);
    }
}