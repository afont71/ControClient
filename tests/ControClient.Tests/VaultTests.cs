using System.Security.Cryptography;
using System.Text;
using ControClient.Domain.Clients;
using ControClient.Domain.Vault;
using ControClient.Infrastructure.Data;
using ControClient.Infrastructure.Vault;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ControClient.Tests;

public class VaultCryptoTests
{
    private static readonly Guid ItemId = Guid.NewGuid();

    [Fact]
    public void Cifra_y_descifra_el_secreto()
    {
        var kek = VaultCrypto.NewKey();
        var sealedValue = VaultCrypto.Seal(kek, "S3cr3t0!", ItemId);

        Assert.DoesNotContain("S3cr3t0!", Encoding.UTF8.GetString(sealedValue.Ciphertext));

        var opened = VaultCrypto.Open(kek, ItemId, sealedValue.Nonce, sealedValue.Ciphertext, sealedValue.DekNonce, sealedValue.WrappedDek);
        Assert.Equal("S3cr3t0!", opened);
    }

    [Fact]
    public void Detects_manipulacion_del_texto_cifrado()
    {
        var kek = VaultCrypto.NewKey();
        var sealedValue = VaultCrypto.Seal(kek, "valor", ItemId);
        sealedValue.Ciphertext[0] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() =>
            VaultCrypto.Open(kek, ItemId, sealedValue.Nonce, sealedValue.Ciphertext, sealedValue.DekNonce, sealedValue.WrappedDek));
    }

    [Fact]
    public void Otra_kek_no_abre_la_dek()
    {
        var sealedValue = VaultCrypto.Seal(VaultCrypto.NewKey(), "valor", ItemId);
        var other = VaultCrypto.NewKey();

        Assert.ThrowsAny<CryptographicException>(() =>
            VaultCrypto.Open(other, ItemId, sealedValue.Nonce, sealedValue.Ciphertext, sealedValue.DekNonce, sealedValue.WrappedDek));
    }

    [Fact]
    public void Una_dek_no_sirve_para_otro_elemento()
    {
        var kek = VaultCrypto.NewKey();
        var sealedValue = VaultCrypto.Seal(kek, "valor", ItemId);

        Assert.ThrowsAny<CryptographicException>(() =>
            VaultCrypto.Open(kek, Guid.NewGuid(), sealedValue.Nonce, sealedValue.Ciphertext, sealedValue.DekNonce, sealedValue.WrappedDek));
    }
}

/// <summary>Protector de pruebas: no usa DPAPI (no depende del equipo).</summary>
internal sealed class FakeProtector : IVaultKeyProtector
{
    public string Description => "pruebas";
    public byte[] Protect(byte[] key) => (byte[])key.Clone();
    public byte[] Unprotect(byte[] protectedBytes) => (byte[])protectedBytes.Clone();
}

public class VaultServiceTests : IDisposable
{
    private readonly string _keyFile = Path.Combine(Path.GetTempPath(), $"cc-vault-{Guid.NewGuid():N}.bin");
    private readonly AppDbContext _db;
    private readonly VaultKeyRing _ring = new();
    private readonly VaultService _vault;
    private readonly Guid _clientId = Guid.NewGuid();

    public VaultServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vault-{Guid.NewGuid():N}").Options);

        _db.Clients.Add(new Client("Cliente de pruebas", null));
        var client = _db.Clients.Local.First();
        _clientId = client.Id;
        _db.SaveChanges();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vault:KeyFile"] = _keyFile
        }).Build();

        _vault = new VaultService(_db, _ring, new FakeProtector(), config);
    }

    [Fact]
    public async Task Constitucion_desellado_y_revelado()
    {
        var recovery = await _vault.InitializeAsync();
        Assert.Equal(32, Convert.FromBase64String(recovery).Length);

        var (item, version, error) = await _vault.CreateItemAsync(
            _clientId, "FortiGate", VaultItemKind.Password, "admin", "https://10.0.0.1", "acceso principal", "ClaveSegura123", "tester");

        Assert.NotNull(item);
        Assert.Equal(1, version);
        Assert.Null(error);

        // El secreto no debe estar en claro en la base.
        var stored = await _db.VaultVersions.SingleAsync();
        Assert.DoesNotContain("ClaveSegura123", Encoding.UTF8.GetString(stored.Ciphertext));

        var (secret, revealedVersion, revealError) = await _vault.RevealAsync(item!.Id);
        Assert.Null(revealError);
        Assert.Equal("ClaveSegura123", secret);
        Assert.Equal(1, revealedVersion);

        _vault.Lock();
        var sealedResult = await _vault.RevealAsync(item.Id);
        Assert.Null(sealedResult.Secret);
        Assert.Contains("sellada", sealedResult.Error!, StringComparison.OrdinalIgnoreCase);

        await _vault.UnsealAsync();
        var afterUnseal = await _vault.RevealAsync(item.Id);
        Assert.Equal("ClaveSegura123", afterUnseal.Secret);
    }

    [Fact]
    public async Task Recuperacion_rota_la_clave_y_reenvuelve_las_dek()
    {
        var recovery = await _vault.InitializeAsync();
        var (item, _, _) = await _vault.CreateItemAsync(
            _clientId, "VPN", VaultItemKind.Password, null, null, null, "SecretoVPN", "tester");

        var newRecovery = await _vault.RecoverAsync(recovery);

        Assert.NotEqual(recovery, newRecovery);

        // El valor sigue siendo legible con la clave nueva.
        var (secret, _, _) = await _vault.RevealAsync(item!.Id);
        Assert.Equal("SecretoVPN", secret);

        // El sobre antiguo ya no sirve.
        await Assert.ThrowsAnyAsync<CryptographicException>(() => _vault.RecoverAsync(recovery));
    }

    [Fact]
    public async Task No_se_puede_inicializar_dos_veces()
    {
        await _vault.InitializeAsync();
        await Assert.ThrowsAsync<VaultAlreadyInitializedException>(() => _vault.InitializeAsync());
    }

    [Fact]
    public async Task Sin_desellar_no_se_crean_credenciales()
    {
        await _vault.InitializeAsync();
        _vault.Lock();

        var (item, _, error) = await _vault.CreateItemAsync(
            _clientId, "X", VaultItemKind.Other, null, null, null, "secreto", "tester");

        Assert.Null(item);
        Assert.Contains("sellada", error!, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_keyFile)) File.Delete(_keyFile);
    }
}
