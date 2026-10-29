using System.Text.Json;

namespace ControClient.Domain.Forms;

/// <summary>
/// Ficha de un cliente: valores tipados de una plantilla concreta, en la versión
/// con la que se creó. Los valores se guardan como JSON normalizado (no secretos).
/// </summary>
public sealed class RecordInstance
{
    private RecordInstance() { }

    public RecordInstance(Guid clientId, FormTemplate template, string title, string valuesJson, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título de la ficha es obligatorio.", nameof(title));

        ClientId = clientId;
        TemplateId = template.Id;
        TemplateVersion = template.Version;
        Title = title.Trim();
        ValuesJson = valuesJson;
        CreatedBy = createdBy;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClientId { get; private set; }
    public Guid TemplateId { get; private set; }
    public int TemplateVersion { get; private set; }
    public string Title { get; private set; } = string.Empty;

    /// <summary>JSON normalizado; nunca contiene secretos (SEC-07).</summary>
    public string ValuesJson { get; private set; } = "{}";

    /// <summary>Control de concurrencia optimista (PATCH con ETag/versión).</summary>
    public int Version { get; private set; } = 1;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;

    public bool TryUpdate(string title, string valuesJson, int expectedVersion)
    {
        if (expectedVersion != Version)
            return false;

        if (!string.IsNullOrWhiteSpace(title))
            Title = title.Trim();

        ValuesJson = valuesJson;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public JsonDocument ParseValues() => JsonDocument.Parse(ValuesJson);
}
