using System.Security.Cryptography;
using ControClient.Domain.Vault;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ControClient.Infrastructure.Vault;

/// <summary>
/// Constitución, desellado, revelado y rotación de la bóveda (SEC-10…SEC-14).
/// La clave maestra se guarda protegida por DPAPI; el valor de recuperación se
/// entrega una sola vez para custodiarlo sellado.
/// </summary>
public sealed class VaultService(
    AppDbContext db,
    VaultKeyRing ring,
    IVaultKeyProtector protector,
    IConfiguration configuration)
{
    public sealed record ItemSummary(Guid Id, Guid ClientId, string Name, string Kind, string? Username, string? Url, string? Notes, int CurrentVersion, DateTimeOffset UpdatedAt, bool IsArchived);
    public sealed record ItemDetail(Guid Id, Guid ClientId, string Name, string Kind, string? Username, string? Url, string? Notes, bool IsArchived, DateTimeOffset CreatedAt, string CreatedBy, IReadOnlyList<VersionSummary> Versions);
    public sealed record VersionSummary(int Version, DateTimeOffset CreatedAt, string CreatedBy, string? Comment);

    public bool IsInitialized => File.Exists(KeyFilePath);

    public bool IsUnsealed => ring.IsUnsealed;

    private string KeyFilePath
    {
        get
        {
            var path = configuration["Vault:KeyFile"];
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ControClient", "vault", "kek.bin");

            return Path.GetFullPath(path);
        }
    }

    public string KeyProtectionDescription => protector.Description;

    /// <summary>Cifra un valor asociado a una ficha (campo secreto) con la clave viva.</summary>
    public (byte[] Nonce, byte[] Ciphertext, byte[] DekNonce, byte[] WrappedDek) SealValue(Guid aad, string plaintext)
    {
        var kek = ring.Require();
        try
        {
            return VaultCrypto.Seal(kek, plaintext, aad);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <summary>Descifra un valor de ficha. Requiere la bóveda desellada.</summary>
    public string OpenValue(Guid aad, byte[] nonce, byte[] ciphertext, byte[] dekNonce, byte[] wrappedDek)
    {
        var kek = ring.Require();
        try
        {
            return VaultCrypto.Open(kek, aad, nonce, ciphertext, dekNonce, wrappedDek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public async Task<(bool Initialized, bool Unsealed, int Items)> GetStatusAsync(CancellationToken ct = default) =>
        (IsInitialized, ring.IsUnsealed, await db.VaultItems.CountAsync(ct));

    /// <summary>Constitución: genera KEK, la protege con DPAPI y devuelve el valor de recuperación UNA vez.</summary>
    public async Task<string> InitializeAsync(CancellationToken ct = default)
    {
        if (IsInitialized)
            throw new VaultAlreadyInitializedException();

        var kek = VaultCrypto.NewKey();
        try
        {
            var protectedBytes = protector.Protect(kek);
            var path = KeyFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, protectedBytes, ct);
            File.Move(temp, path, overwrite: true);

            ring.Set(kek);

            // Valor de recuperación: el sobre que se custodia fuera del servidor.
            return Convert.ToBase64String(kek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public async Task UnsealAsync(CancellationToken ct = default)
    {
        if (!IsInitialized)
            throw new VaultNotInitializedException();

        var protectedBytes = await File.ReadAllBytesAsync(KeyFilePath, ct);
        var kek = protector.Unprotect(protectedBytes);
        ring.Set(kek);
        CryptographicOperations.ZeroMemory(kek);
    }

    public void Lock() => ring.Clear();

    /// <summary>
    /// Recuperación con el sobre: valida el valor, reenvuelve todas las DEK con una
    /// KEK nueva y devuelve el nuevo valor de recuperación.
    /// </summary>
    public async Task<string> RecoverAsync(string recoveryCode, CancellationToken ct = default)
    {
        if (!IsInitialized)
            throw new VaultNotInitializedException();

        byte[] oldKey;
        try
        {
            oldKey = Convert.FromBase64String(recoveryCode.Trim());
        }
        catch (FormatException)
        {
            throw new CryptographicException("El valor de recuperación no es válido.");
        }

        if (oldKey.Length != VaultCrypto.KeySize)
            throw new CryptographicException("El valor de recuperación no es válido.");

        var versions = await db.VaultVersions.ToListAsync(ct);
        var rewrap = new List<(VaultVersion Version, byte[] DekNonce, byte[] WrappedDek)>();

        foreach (var version in versions)
        {
            var dek = VaultCrypto.UnwrapDek(oldKey, version.ItemId, version.DekNonce, version.WrappedDek);
            rewrap.Add((version, default!, dek));
        }

        CryptographicOperations.ZeroMemory(oldKey);

        var newKey = VaultCrypto.NewKey();
        try
        {
            foreach (var (version, _, dek) in rewrap)
            {
                var (dekNonce, wrapped) = VaultCrypto.WrapDek(newKey, version.ItemId, dek);
                version.Rewrap(dekNonce, wrapped);
                CryptographicOperations.ZeroMemory(dek);
            }

            var protectedBytes = protector.Protect(newKey);
            var path = KeyFilePath;
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, protectedBytes, ct);
            File.Move(temp, path, overwrite: true);

            await db.SaveChangesAsync(ct);
            ring.Set(newKey);

            return Convert.ToBase64String(newKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newKey);
        }
    }

    public async Task<(VaultItem? Item, int Version, string? Error)> CreateItemAsync(
        Guid clientId, string name, VaultItemKind kind, string? username, string? url, string? notes,
        string secret, string createdBy, CancellationToken ct = default)
    {
        if (!IsInitialized) return (null, 0, "La bóveda no está constituida.");
        if (!ring.IsUnsealed) return (null, 0, "La bóveda está sellada.");
        if (string.IsNullOrEmpty(secret)) return (null, 0, "El secreto es obligatorio.");

        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return (null, 0, "El cliente no existe.");

        var kek = ring.Require();
        try
        {
            var item = new VaultItem(clientId, name, kind, createdBy, username, url, notes);
            var sealedValue = VaultCrypto.Seal(kek, secret, item.Id);
            var version = item.AddVersion(sealedValue.Nonce, sealedValue.Ciphertext, sealedValue.DekNonce, sealedValue.WrappedDek, createdBy);

            db.VaultItems.Add(item);
            await db.SaveChangesAsync(ct);
            return (item, version.Version, null);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public async Task<(int Version, string? Error)> AddVersionAsync(
        Guid itemId, string secret, string createdBy, string? comment, CancellationToken ct = default)
    {
        if (!ring.IsUnsealed) return (0, "La bóveda está sellada.");
        if (string.IsNullOrEmpty(secret)) return (0, "El secreto es obligatorio.");

        var item = await db.VaultItems.Include(i => i.Versions).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return (0, "La credencial no existe.");

        var kek = ring.Require();
        try
        {
            var sealedValue = VaultCrypto.Seal(kek, secret, item.Id);
            var version = item.AddVersion(sealedValue.Nonce, sealedValue.Ciphertext, sealedValue.DekNonce, sealedValue.WrappedDek, createdBy, comment);
            await db.SaveChangesAsync(ct);
            return (version.Version, null);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public async Task<(string? Secret, int Version, string? Error)> RevealAsync(Guid itemId, CancellationToken ct = default)
    {
        if (!ring.IsUnsealed) return (null, 0, "La bóveda está sellada.");

        var item = await db.VaultItems.AsNoTracking().Include(i => i.Versions).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return (null, 0, "La credencial no existe.");

        var current = item.Versions.OrderByDescending(v => v.Version).FirstOrDefault();
        if (current is null) return (null, 0, "La credencial no tiene versiones.");

        var kek = ring.Require();
        try
        {
            var secret = VaultCrypto.Open(kek, item.Id, current.Nonce, current.Ciphertext, current.DekNonce, current.WrappedDek);
            return (secret, current.Version, null);
        }
        catch (CryptographicException)
        {
            return (null, 0, "No se pudo descifrar: la clave no corresponde o el dato está alterado.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public async Task<IReadOnlyList<ItemSummary>> ListAsync(Guid clientId, CancellationToken ct = default)
    {
        var rows = await db.VaultItems.AsNoTracking()
            .Where(i => i.ClientId == clientId)
            .Select(i => new
            {
                i.Id, i.ClientId, i.Name, i.Kind, i.Username, i.Url, i.Notes, i.UpdatedAt, i.CreatedAt, i.IsArchived,
                CurrentVersion = i.Versions.Max(v => (int?)v.Version) ?? 0
            })
            .ToListAsync(ct);

        return rows
            .OrderBy(r => r.Name)
            .Select(r => new ItemSummary(r.Id, r.ClientId, r.Name, r.Kind.ToString(), r.Username, r.Url, r.Notes, r.CurrentVersion, r.UpdatedAt ?? r.CreatedAt, r.IsArchived))
            .ToList();
    }

    public async Task<ItemDetail?> GetAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await db.VaultItems.AsNoTracking().Include(i => i.Versions).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return null;

        return new ItemDetail(
            item.Id, item.ClientId, item.Name, item.Kind.ToString(), item.Username, item.Url, item.Notes,
            item.IsArchived, item.CreatedAt, item.CreatedBy,
            item.Versions.OrderByDescending(v => v.Version)
                .Select(v => new VersionSummary(v.Version, v.CreatedAt, v.CreatedBy, v.Comment)).ToList());
    }
}
