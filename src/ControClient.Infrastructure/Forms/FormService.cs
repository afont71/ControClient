using System.Text.Json;
using System.Text.Json.Nodes;
using ControClient.Infrastructure.Vault;
using ControClient.Domain.Forms;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Forms;

/// <summary>
/// Plantillas y fichas. La validación vive en el dominio (RecordValidator); aquí
/// solo se persiste y se controla la versión de la plantilla y la concurrencia.
/// </summary>
public sealed class FormService(AppDbContext db, VaultService vault)
{
    public sealed record TemplateSummary(Guid Id, string Key, string Name, string? Description, int Version, bool IsActive, int FieldCount);
    public sealed record FieldSpec(string Key, string Label, FieldType Type, bool Required, string? OptionsJson, string? HelpText, int Order, bool IsSecret = false);
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
            f.Key, f.Label, f.Type, f.Required, f.Order, f.OptionsJson, f.HelpText, f.IsSecret));

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
        IReadOnlyDictionary<string, string>? secretValues = null,
        IReadOnlyDictionary<string, bool>? secretFlags = null,
        bool allowSecrecyChange = false,
        CancellationToken ct = default)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return (null, null, "El cliente no existe.");

        var template = await GetActiveTemplateAsync(templateId, ct);
        if (template is null)
            return (null, null, "La plantilla no existe o está inactiva.");

        if (!allowSecrecyChange && SecrecyChanged(template, secretFlags, null))
            return (null, null, "Solo la administración de la bóveda puede cambiar la condición de secreto.");

        var effective = EffectiveSecrecy(template, secretFlags, null);
        var valueObject = ToObject(values);
        var secrets = MergeSecrets(valueObject, effective, secretValues);

        if (secrets.Count > 0 && !vault.IsUnsealed)
            return (null, null, "La bóveda está sellada: desellala para guardar secretos.");

        var validationValues = PrepareForValidation(JsonSerializer.SerializeToElement(valueObject), effective, secrets, null);
        var outcome = RecordValidator.Validate(template, validationValues);
        if (!outcome.IsValid)
            return (null, outcome.Errors, null);

        var record = new RecordInstance(clientId, template, title, StripSecretKeys(outcome.NormalizedJson!, effective), createdBy);
        db.Records.Add(record);
        await db.SaveChangesAsync(ct);

        await SaveSecretsAsync(record, effective, secrets, createdBy, ct);
        await SaveFlagsAsync(record, template, effective, ct);
        return (record, null, null);
    }

    public async Task<(RecordInstance? Record, IReadOnlyDictionary<string, string[]>? Errors, string? Error, bool NotFound, bool Conflict)> UpdateRecordAsync(
        Guid recordId,
        string? title,
        JsonElement values,
        int expectedVersion,
        IReadOnlyDictionary<string, string>? secretValues = null,
        string? updatedBy = null,
        IReadOnlyDictionary<string, bool>? secretFlags = null,
        bool allowSecrecyChange = false,
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

        var existingFlags = await db.RecordSecretFlags.AsNoTracking()
            .Where(f => f.RecordId == recordId)
            .ToDictionaryAsync(f => f.FieldKey, f => f.IsSecret, ct);

        var existingSecrets = await db.RecordSecrets.AsNoTracking()
            .Where(s => s.RecordId == recordId)
            .ToListAsync(ct);

        if (!allowSecrecyChange && SecrecyChanged(template, secretFlags, existingFlags))
            return (null, null, "Solo la administración de la bóveda puede cambiar la condición de secreto.", false, false);

        var effective = EffectiveSecrecy(template, secretFlags, existingFlags);
        var valueObject = ToObject(values);

        // Desclasificar: un campo que deja de ser secreto se descifra y se guarda
        // en claro, salvo que el usuario haya escrito un valor nuevo.
        foreach (var field in template.Fields.Where(f => !effective[f.Key]))
        {
            if (valueObject.ContainsKey(field.Key))
                continue;

            var stored = existingSecrets.FirstOrDefault(s => s.FieldKey == field.Key);
            if (stored is null)
                continue;

            if (!vault.IsUnsealed)
                return (null, null, "La bóveda está sellada: desellala para desclasificar el secreto.", false, false);

            valueObject[field.Key] = vault.OpenValue(recordId, stored.Nonce, stored.Ciphertext, stored.DekNonce, stored.WrappedDek);
        }

        var secrets = MergeSecrets(valueObject, effective, secretValues);
        if (secrets.Count > 0 && !vault.IsUnsealed)
            return (null, null, "La bóveda está sellada: desellala para guardar secretos.", false, false);

        var validationValues = PrepareForValidation(JsonSerializer.SerializeToElement(valueObject), effective, secrets,
            existingSecrets.Select(s => s.FieldKey).ToList());

        var outcome = RecordValidator.Validate(template, validationValues);
        if (!outcome.IsValid)
            return (null, outcome.Errors, null, false, false);

        if (!record.TryUpdate(title ?? record.Title, StripSecretKeys(outcome.NormalizedJson!, effective), expectedVersion))
            return (null, null, "Conflicto de versión.", false, true);

        await db.SaveChangesAsync(ct);
        await SaveSecretsAsync(record, effective, secrets, updatedBy ?? record.CreatedBy, ct);
        await SaveFlagsAsync(record, template, effective, ct);
        return (record, null, null, false, false);
    }

    /// <summary>Excepciones de secreto guardadas para una ficha.</summary>
    public async Task<IReadOnlyDictionary<string, bool>> SecretFlagsAsync(Guid recordId, CancellationToken ct = default) =>
        await db.RecordSecretFlags.AsNoTracking()
            .Where(f => f.RecordId == recordId)
            .ToDictionaryAsync(f => f.FieldKey, f => f.IsSecret, ct);

    /// <summary>Claves de los campos secretos que ya tienen valor cifrado.</summary>
    public async Task<IReadOnlyList<string>> SecretKeysAsync(Guid recordId, CancellationToken ct = default) =>
        await db.RecordSecrets.AsNoTracking()
            .Where(s => s.RecordId == recordId)
            .Select(s => s.FieldKey)
            .ToListAsync(ct);

    /// <summary>Descifra un campo secreto. El permiso se comprueba fuera, en el servicio de acceso.</summary>
    public async Task<string?> RevealSecretAsync(Guid recordId, string fieldKey, CancellationToken ct = default)
    {
        var secret = await db.RecordSecrets.AsNoTracking()
            .FirstOrDefaultAsync(s => s.RecordId == recordId && s.FieldKey == fieldKey, ct);

        return secret is null
            ? null
            : vault.OpenValue(recordId, secret.Nonce, secret.Ciphertext, secret.DekNonce, secret.WrappedDek);
    }

    /// <summary>Secreto efectivo por campo: pedido en la ficha, o excepción guardada, o el del tipo.</summary>
    private static Dictionary<string, bool> EffectiveSecrecy(
        FormTemplate template,
        IReadOnlyDictionary<string, bool>? requested,
        IReadOnlyDictionary<string, bool>? existing)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in template.Fields)
        {
            if (requested is not null && requested.TryGetValue(field.Key, out var wanted))
                map[field.Key] = wanted;
            else if (existing is not null && existing.TryGetValue(field.Key, out var saved))
                map[field.Key] = saved;
            else
                map[field.Key] = field.IsSecret;
        }

        return map;
    }

    /// <summary>¿La petición cambia la condición de secreto de algún campo?</summary>
    private static bool SecrecyChanged(
        FormTemplate template,
        IReadOnlyDictionary<string, bool>? requested,
        IReadOnlyDictionary<string, bool>? existing)
    {
        if (requested is null)
            return false;

        foreach (var field in template.Fields)
        {
            if (!requested.TryGetValue(field.Key, out var wanted))
                continue;

            var current = existing is not null && existing.TryGetValue(field.Key, out var saved)
                ? saved
                : field.IsSecret;

            if (wanted != current)
                return true;
        }

        return false;
    }

    private static JsonObject ToObject(JsonElement values) =>
        JsonNode.Parse(values.GetRawText()) as JsonObject ?? new JsonObject();

    /// <summary>Pasa a secretos los valores que vienen en claro para campos secretos.</summary>
    private static Dictionary<string, string> MergeSecrets(
        JsonObject node,
        IReadOnlyDictionary<string, bool> effective,
        IReadOnlyDictionary<string, string>? provided)
    {
        var secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (provided is not null)
            foreach (var pair in provided)
                secrets[pair.Key] = pair.Value;

        foreach (var key in effective.Where(p => p.Value).Select(p => p.Key).ToList())
        {
            if (node.TryGetPropertyValue(key, out var value)
                && value is JsonValue jsonValue
                && jsonValue.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                secrets[key] = text;
                node.Remove(key);
            }
        }

        foreach (var empty in secrets.Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Key).ToList())
            secrets.Remove(empty);

        return secrets;
    }

    /// <summary>Mete un marcador en los campos secretos solo para validar; no se persiste.</summary>
    private static JsonElement PrepareForValidation(
        JsonElement values,
        IReadOnlyDictionary<string, bool> effective,
        IReadOnlyDictionary<string, string>? provided,
        IReadOnlyCollection<string>? existingKeys)
    {
        var node = JsonNode.Parse(values.GetRawText()) as JsonObject ?? new JsonObject();

        foreach (var pair in effective.Where(p => p.Value))
        {
            var incoming = provided is not null
                           && provided.TryGetValue(pair.Key, out var value)
                           && !string.IsNullOrWhiteSpace(value);
            var existing = existingKeys?.Contains(pair.Key) == true;

            if (incoming || existing)
                node[pair.Key] = "••••";
        }

        return JsonSerializer.SerializeToElement(node);
    }

    private static string StripSecretKeys(string json, IReadOnlyDictionary<string, bool> effective)
    {
        if (JsonNode.Parse(json) is not JsonObject node)
            return json;

        foreach (var key in effective.Where(p => p.Value).Select(p => p.Key))
            node.Remove(key);

        return node.ToJsonString();
    }

    private async Task SaveSecretsAsync(
        RecordInstance record,
        IReadOnlyDictionary<string, bool> effective,
        IReadOnlyDictionary<string, string>? provided,
        string actor,
        CancellationToken ct)
    {
        foreach (var pair in effective)
        {
            var existing = await db.RecordSecrets
                .FirstOrDefaultAsync(s => s.RecordId == record.Id && s.FieldKey == pair.Key, ct);

            if (!pair.Value)
            {
                // Ya no es secreto: el valor se descifró y quedó en el JSON.
                if (existing is not null)
                    db.RecordSecrets.Remove(existing);

                continue;
            }

            if (provided is null || !provided.TryGetValue(pair.Key, out var plain) || string.IsNullOrWhiteSpace(plain))
                continue;

            var sealedValue = vault.SealValue(record.Id, plain);
            if (existing is null)
                db.RecordSecrets.Add(new RecordSecret(record.Id, pair.Key,
                    sealedValue.Nonce, sealedValue.Ciphertext, sealedValue.DekNonce, sealedValue.WrappedDek, actor));
            else
                existing.Replace(sealedValue.Nonce, sealedValue.Ciphertext, sealedValue.DekNonce, sealedValue.WrappedDek, actor);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SaveFlagsAsync(
        RecordInstance record,
        FormTemplate template,
        IReadOnlyDictionary<string, bool> effective,
        CancellationToken ct)
    {
        foreach (var field in template.Fields)
        {
            var wanted = effective[field.Key];
            var existing = await db.RecordSecretFlags
                .FirstOrDefaultAsync(f => f.RecordId == record.Id && f.FieldKey == field.Key, ct);

            if (wanted == field.IsSecret)
            {
                if (existing is not null)
                    db.RecordSecretFlags.Remove(existing);

                continue;
            }

            if (existing is null)
                db.RecordSecretFlags.Add(new RecordSecretFlag(record.Id, field.Key, wanted));
            else if (existing.IsSecret != wanted)
                db.RecordSecretFlags.Remove(existing);
        }

        await db.SaveChangesAsync(ct);
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

    public async Task<bool> DeleteRecordAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.Records.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (record is null)
            return false;

        db.Records.Remove(record);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<RecordInstance?> GetRecordAsync(Guid id, CancellationToken ct = default) =>
        db.Records.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
}
