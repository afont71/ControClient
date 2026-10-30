namespace ControClient.Domain.Forms;

/// <summary>Tipos de campo soportados por las plantillas (§2.1 de la especificación).</summary>
public enum FieldType
{
    Text = 0,
    LongText = 1,
    Number = 2,
    Boolean = 3,
    Date = 4,
    Select = 5,
    MultiSelect = 6,
    Ip = 7,
    Url = 8,
    Email = 9,

    /// <summary>Reservado a la bóveda (etapa 4). En v1 se rechaza su uso en fichas.</summary>
    SecretReference = 10
}

/// <summary>Campo de una plantilla. Los tipos se validan en servidor, nunca en cliente.</summary>
public sealed class FieldDefinition
{
    private FieldDefinition() { }

    public FieldDefinition(string key, string label, FieldType type, bool required = false, int order = 0, string? options = null, string? helpText = null, bool isSecret = false)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
            throw new ArgumentException("La clave del campo solo admite letras, números, guion y guion bajo.", nameof(key));

        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("La etiqueta del campo es obligatoria.", nameof(label));

        if (type is FieldType.Select or FieldType.MultiSelect && string.IsNullOrWhiteSpace(options))
            throw new ArgumentException("Los campos de selección necesitan opciones.", nameof(options));

        Key = key.Trim();
        Label = label.Trim();
        Type = type;
        IsRequired = required;
        Order = order;
        OptionsJson = options;
        HelpText = helpText;
        IsSecret = isSecret;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Key { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public FieldType Type { get; private set; }
    public bool IsRequired { get; private set; }
    public int Order { get; private set; }

    /// <summary>Array JSON de opciones para Select/MultiSelect.</summary>
    public string? OptionsJson { get; private set; }

    public string? HelpText { get; private set; }

    /// <summary>Valor cifrado con la clave de la bóveda; solo se revela con permiso.</summary>
    public bool IsSecret { get; private set; }
}
