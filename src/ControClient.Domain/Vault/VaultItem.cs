namespace ControClient.Domain.Vault;

public enum VaultItemKind
{
    Password = 0,
    ApiToken = 1,
    Certificate = 2,
    ConnectionString = 3,
    Other = 4
}

/// <summary>
/// Credencial de un cliente. La metadata (nombre, usuario, URL, notas) no es
/// secreta; el valor vive cifrado en <see cref="VaultVersion"/>.
/// </summary>
public sealed class VaultItem
{
    private VaultItem() { }

    public VaultItem(Guid clientId, string name, VaultItemKind kind, string createdBy, string? username = null, string? url = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la credencial es obligatorio.", nameof(name));

        ClientId = clientId;
        Name = name.Trim();
        Kind = kind;
        CreatedBy = createdBy;
        Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public VaultItemKind Kind { get; private set; }
    public string? Username { get; private set; }
    public string? Url { get; private set; }

    /// <summary>Notas no secretas; nunca debe contener la contraseña.</summary>
    public string? Notes { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public bool IsArchived { get; private set; }

    public List<VaultVersion> Versions { get; private set; } = [];

    public VaultVersion AddVersion(byte[] nonce, byte[] ciphertext, byte[] dekNonce, byte[] wrappedDek, string createdBy, string? comment = null)
    {
        var version = new VaultVersion(
            Id,
            Versions.Count == 0 ? 1 : Versions.Max(v => v.Version) + 1,
            nonce, ciphertext, dekNonce, wrappedDek, createdBy, comment);

        Versions.Add(version);
        UpdatedAt = DateTimeOffset.UtcNow;
        return version;
    }

    public void Archive()
    {
        IsArchived = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>Valor cifrado de una versión: AES-GCM sobre el secreto y DEK envuelta con la KEK.</summary>
public sealed class VaultVersion
{
    private VaultVersion() { }

    internal VaultVersion(
        Guid itemId, int version,
        byte[] nonce, byte[] ciphertext,
        byte[] dekNonce, byte[] wrappedDek,
        string createdBy, string? comment)
    {
        ItemId = itemId;
        Version = version;
        Nonce = nonce;
        Ciphertext = ciphertext;
        DekNonce = dekNonce;
        WrappedDek = wrappedDek;
        CreatedBy = createdBy;
        Comment = comment;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ItemId { get; private set; }
    public int Version { get; private set; }

    public byte[] Nonce { get; private set; } = [];
    public byte[] Ciphertext { get; private set; } = [];

    public byte[] DekNonce { get; private set; } = [];
    public byte[] WrappedDek { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; private set; } = string.Empty;
    public string? Comment { get; private set; }

    /// <summary>Reenvuelve la DEK con una KEK nueva (rotación) sin tocar el secreto.</summary>
    public void Rewrap(byte[] dekNonce, byte[] wrappedDek)
    {
        DekNonce = dekNonce;
        WrappedDek = wrappedDek;
    }
}
