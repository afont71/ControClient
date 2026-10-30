namespace ControClient.Domain.Forms;

/// <summary>
/// Excepción de secreto para un campo de una ficha concreta. Si no existe, se
/// aplica lo que diga el tipo. Permite marcar como secreto un campo que por
/// defecto no lo es, o desclasificar uno que sí lo es.
/// </summary>
public sealed class RecordSecretFlag
{
    private RecordSecretFlag() { }

    public RecordSecretFlag(Guid recordId, string fieldKey, bool isSecret)
    {
        RecordId = recordId;
        FieldKey = fieldKey;
        IsSecret = isSecret;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RecordId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public bool IsSecret { get; private set; }
}