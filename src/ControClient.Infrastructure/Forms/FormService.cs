using System.Text.Json;
using ControClient.Domain.Forms;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Forms;

/// <summary>
/// Plantillas y fichas. La validación vive en el dominio (RecordValidator); aquí
/// solo se persiste y se controla la versión de la plantilla y la concurrencia.
/// </summary>
public sealed class FormService(AppDbContext db)
{
    public sealed record TemplateSummary(Guid Id, string Key, string Name, string? Description, int Version, bool IsActive, int FieldCount);
    public sealed record FieldSpec(string Key, string Label, FieldType Type, bool Required, string? OptionsJson, string? HelpText, int Order);
    public sealed record RecordSummary(Guid Id, Guid ClientId, Guid TemplateId, string TemplateName, int TemplateVersion, string Title, int Version, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, string CreatedBy);

    public async Task<IReadOnlyList<TemplateSummary>> ListTemplatesAsync(bool includeInactive, CancellationToken ct = default) =>
        await db.FormTemplates.AsNoTracking()
            .Where(t => includeInactive || t.IsActive)
            .OrderBy(t => t.Name).ThenByDescending(t => t.Version)
            .Select(t => new TemplateSummary(t.Id, t.Key, t.Name, t.Description, t.Version, t.IsActive, t.Fields.Count))
            .ToListAsync(ct);

    public Task<FormTemplate?> GetTemplateAsync(Guid id, CancellationToken ct = default) =>
        db.FormTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<FormTemplate> CreateTemplateAsync(
        string key,
        string name,
        string? description,
        IReadOnlyList<FieldSpec> fields,
        CancellationToken ct = default)
    {
        var lastVersion = await db.FormTemplates
            .Where(t => t.Key == key)
            .MaxAsync(t => (int?)t.Version, ct) ?? 0;

        var definitions = fields.Select(f => new FieldDefinition(
            f.Key, f.Label, f.Type, f.Required, f.Order, f.OptionsJson, f.HelpText));

        var template = new FormTemplate(key, name, description, definitions, lastVersion + 1);
        db.FormTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return template;
    }

    public async Task<FormTemplate?> GetActiveTemplateAsync(Guid id, CancellationToken ct = default) =>
        await db.FormTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.IsActive, ct);

    public async Task<(RecordInstance? Record, IReadOnlyDictionary<string, string[]>? Errors, string? Error)> CreateRecordAsync(
        Guid clientId,
        Guid templateId,
        string title,
        JsonElement values,
        string createdBy,
        CancellationToken ct = default)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return (null, null, "El cliente no existe.");

        var template = await GetActiveTemplateAsync(templateId, ct);
        if (template is null)
            return (null, null, "La plantilla no existe o está inactiva.");

        var outcome = RecordValidator.Validate(template, values);
        if (!outcome.IsValid)
            return (null, outcome.Errors, null);

        var record = new RecordInstance(clientId, template, title, outcome.NormalizedJson!, createdBy);
        db.Records.Add(record);
        await db.SaveChangesAsync(ct);
        return (record, null, null);
    }

    public async Task<(RecordInstance? Record, IReadOnlyDictionary<string, string[]>? Errors, string? Error, bool NotFound, bool Conflict)> UpdateRecordAsync(
        Guid recordId,
        string? title,
        JsonElement values,
        int expectedVersion,
        CancellationToken ct = default)
    {
        var record = await db.Records.FirstOrDefaultAsync(r => r.Id == recordId, ct);
        if (record is null)
            return (null, null, null, true, false);

        if (record.Version != expectedVersion)
            return (null, null, "La ficha ha cambiado desde que la abriste. Recarga antes de guardar.", false, true);

        var template = await db.FormTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == record.TemplateId, ct);
        if (template is null)
            return (null, null, "La plantilla de la ficha ya no existe.", true, false);

        var outcome = RecordValidator.Validate(template, values);
        if (!outcome.IsValid)
            return (null, outcome.Errors, null, false, false);

        if (!record.TryUpdate(title ?? record.Title, outcome.NormalizedJson!, expectedVersion))
            return (null, null, "Conflicto de versión.", false, true);

        await db.SaveChangesAsync(ct);
        return (record, null, null, false, false);
    }

    public async Task<IReadOnlyList<RecordSummary>> ListRecordsAsync(Guid clientId, Guid? templateId = null, CancellationToken ct = default)
    {
        var query = from r in db.Records.AsNoTracking()
                    join t in db.FormTemplates.AsNoTracking() on r.TemplateId equals t.Id
                    where r.ClientId == clientId
                    select new { r, t.Name };

        if (templateId is not null)
            query = query.Where(x => x.r.TemplateId == templateId);

        return await query
            .OrderBy(x => x.Name).ThenBy(x => x.r.Title)
            .Select(x => new RecordSummary(
                x.r.Id, x.r.ClientId, x.r.TemplateId, x.Name, x.r.TemplateVersion,
                x.r.Title, x.r.Version, x.r.CreatedAt, x.r.UpdatedAt, x.r.CreatedBy))
            .ToListAsync(ct);
    }

    public Task<RecordInstance?> GetRecordAsync(Guid id, CancellationToken ct = default) =>
        db.Records.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
}
