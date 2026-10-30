namespace ControClient.Domain.Forms;

/// <summary>
/// Valor cifrado de un campo marcado como secreto en una ficha. Se guarda aparte
/// del JSON normal: en claro nunca toca la base de datos ni las copias.
/// </summary>
public sealed class RecordSecret
{
    private RecordSecret() { }

    public RecordSecret(Guid recordId, string fieldKey, byte[] nonce, byte[] ciphertext, byte[] dekNonce, byte[] wrappedDek, string createdBy)
    {
        RecordId = recordId;
        FieldKey = fieldKey;
        Nonce = nonce;
        Ciphertext = ciphertext;
        DekNonce = dekNonce;
        WrappedDek = wrappedDek;
        CreatedBy = createdBy;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RecordId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public byte[] Nonce { get; private set; } = [];
    public byte[] Ciphertext { get; private set; } = [];
    public byte[] DekNonce { get; private set; } = [];
    public byte[] WrappedDek { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; private set; } = string.Empty;

    public void Replace(byte[] nonce, byte[] ciphertext, byte[] dekNonce, byte[] wrappedDek, string createdBy)
    {
        Nonce = nonce;
        Ciphertext = ciphertext;
        DekNonce = dekNonce;
        WrappedDek = wrappedDek;
        CreatedBy = createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}