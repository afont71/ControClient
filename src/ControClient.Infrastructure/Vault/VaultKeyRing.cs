using System.Security.Cryptography;
using System.Text;

namespace ControClient.Infrastructure.Vault;

/// <summary>
/// Protección de la clave maestra en reposo (ADR-009). En Windows se usa DPAPI
/// de equipo; el fichero resultante no es utilizable en otra máquina.
/// </summary>
public interface IVaultKeyProtector
{
    string Description { get; }
    byte[] Protect(byte[] key);
    byte[] Unprotect(byte[] protectedBytes);
}

public sealed class WindowsDpapiProtector : IVaultKeyProtector
{
    // Entropía adicional fija: dificulta descifrar el blob fuera de la aplicación.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ControClient.Vault.v1");

    public string Description => "Windows DPAPI (equipo)";

    public byte[] Protect(byte[] key) =>
        ProtectedData.Protect(key, Entropy, DataProtectionScope.LocalMachine);

    public byte[] Unprotect(byte[] protectedBytes) =>
        ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
}

/// <summary>
/// Clave maestra viva solo en memoria. Se vacía al bloquear y al reiniciar el
/// proceso; nunca se escribe en claro ni se registra (SEC-07).
/// </summary>
public sealed class VaultKeyRing
{
    private readonly object _sync = new();
    private byte[]? _kek;

    public bool IsUnsealed
    {
        get { lock (_sync) return _kek is not null; }
    }

    public void Set(byte[] kek)
    {
        if (kek.Length != VaultCrypto.KeySize)
            throw new ArgumentException("La clave maestra debe tener 32 bytes.", nameof(kek));

        lock (_sync)
        {
            if (_kek is not null) CryptographicOperations.ZeroMemory(_kek);
            _kek = (byte[])kek.Clone();
        }
    }

    public byte[] Require()
    {
        lock (_sync)
        {
            if (_kek is null)
                throw new VaultSealedException();

            return (byte[])_kek.Clone();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            if (_kek is not null) CryptographicOperations.ZeroMemory(_kek);
            _kek = null;
        }
    }
}

public sealed class VaultSealedException() : Exception("La bóveda está sellada: desellala antes de operar.");

public sealed class VaultNotInitializedException() : Exception("La bóveda no está constituida.");

public sealed class VaultAlreadyInitializedException() : Exception("La bóveda ya está constituida.");
