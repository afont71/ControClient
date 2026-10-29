namespace ControClient.Domain.Forms;

/// <summary>
/// Plantilla versionada de ficha. Publicar una versión nueva no altera las fichas
/// existentes: cada registro conserva la versión con la que se creó (§2.1).
/// </summary>
public sealed class FormTemplate
{
    private FormTemplate() { }

    public FormTemplate(string key, string name, string? description, IEnumerable<FieldDefinition> fields, int version = 1)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("La clave de la plantilla es obligatoria.", nameof(key));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la plantilla es obligatorio.", nameof(name));

        var list = fields?.ToList() ?? [];
        if (list.Count == 0)
            throw new ArgumentException("La plantilla debe tener al menos un campo.", nameof(fields));

        var duplicated = list.GroupBy(f => f.Key, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1);
        if (duplicated)
            throw new ArgumentException("Hay claves de campo repetidas.", nameof(fields));

        Key = key.Trim();
        Name = name.Trim();
        Description = description;
        Version = version;
        Fields = list.OrderBy(f => f.Order).ToList();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Version { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public List<FieldDefinition> Fields { get; private set; } = [];

    public void Deactivate() => IsActive = false;
}
