using ControClient.Infrastructure.Vault;

namespace ControClient.Web.Security;

/// <summary>
/// Estado de bloqueo por bóveda sellada. Con la bóveda constituida y sellada, la
/// aplicación queda cerrada: solo el administrador puede entrar a la bóveda para
/// desellarla. Si la bóveda no está constituida, no se bloquea nada.
/// </summary>
public sealed class VaultLockService(VaultService vault)
{
    public async Task<bool> IsLockedAsync(CancellationToken ct = default)
    {
        var (initialized, unsealed, _) = await vault.GetStatusAsync(ct);
        return initialized && !unsealed;
    }
}
