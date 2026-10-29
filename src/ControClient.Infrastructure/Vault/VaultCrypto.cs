using System.Security.Cryptography;
using System.Text;

namespace ControClient.Infrastructure.Vault;

/// <summary>
/// Cifrado de la bóveda: cada secreto se cifra con su propia DEK; la DEK se
/// envuelve con la KEK, que solo existe en memoria tras el desellado.
/// Algoritmo: AES-256-GCM (autenticado). .NET no expone XChaCha20; se documenta
/// la sustitución en ADR-001 y ambas son AEAD de 256 bits.
/// </summary>
public static class VaultCrypto
{
    public const int KeySize = 32;   // 256 bits
    public const int NonceSize = 12; // 96 bits, recomendado para GCM
    public const int TagSize = 16;

    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeySize);

    public static byte[] NewNonce() => RandomNumberGenerator.GetBytes(NonceSize);

    /// <summary>Cifra el secreto con una DEK nueva y la envuelve con la KEK.</summary>
    public static (byte[] Nonce, byte[] Ciphertext, byte[] DekNonce, byte[] WrappedDek) Seal(byte[] kek, string secret, Guid itemId)
    {
        var dek = NewKey();
        try
        {
            var plaintext = Encoding.UTF8.GetBytes(secret);
            var nonce = NewNonce();
            var ciphertext = Encrypt(dek, nonce, plaintext, AdditionalData(itemId));

            var dekNonce = NewNonce();
            var wrappedDek = Encrypt(kek, dekNonce, dek, AdditionalData(itemId));

            return (nonce, ciphertext, dekNonce, wrappedDek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>Abre la DEK con la KEK y devuelve el secreto en claro.</summary>
    public static string Open(byte[] kek, Guid itemId, byte[] nonce, byte[] ciphertext, byte[] dekNonce, byte[] wrappedDek)
    {
        var dek = Decrypt(kek, dekNonce, wrappedDek, AdditionalData(itemId));
        try
        {
            var plaintext = Decrypt(dek, nonce, ciphertext, AdditionalData(itemId));
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>Desenvuelve la DEK para poder reenvolverla con una KEK nueva (rotación).</summary>
    public static byte[] UnwrapDek(byte[] kek, Guid itemId, byte[] dekNonce, byte[] wrappedDek) =>
        Decrypt(kek, dekNonce, wrappedDek, AdditionalData(itemId));

    /// <summary>Reenvuelve una DEK existente con otra KEK, sin tocar el secreto.</summary>
    public static (byte[] DekNonce, byte[] WrappedDek) WrapDek(byte[] kek, Guid itemId, byte[] dek)
    {
        var dekNonce = NewNonce();
        return (dekNonce, Encrypt(kek, dekNonce, dek, AdditionalData(itemId)));
    }

    private static byte[] AdditionalData(Guid itemId) => itemId.ToByteArray();

    private static byte[] Encrypt(byte[] key, byte[] nonce, byte[] plaintext, byte[] aad)
    {
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        return [.. ciphertext, .. tag];
    }

    private static byte[] Decrypt(byte[] key, byte[] nonce, byte[] ciphertextWithTag, byte[] aad)
    {
        if (ciphertextWithTag.Length < TagSize)
            throw new CryptographicException("Texto cifrado inválido.");

        var ciphertext = ciphertextWithTag.AsSpan(0, ciphertextWithTag.Length - TagSize);
        var tag = ciphertextWithTag.AsSpan(ciphertextWithTag.Length - TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        return plaintext;
    }
}
