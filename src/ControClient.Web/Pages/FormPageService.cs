using System.Text.Json;
using System.Text.Json.Nodes;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Forms;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Infrastructure.Forms;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Pages;

/// <summary>Acceso a plantillas y fichas para las pantallas, con un ámbito por operación.</summary>
public sealed class FormPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record TemplateRow(Guid Id, string Key, string Name, string? Description, int Version);
    public sealed record FieldInfo(string Key, string Label, string Type, bool Required, string[] Options, string? HelpText, bool IsSecret = false);
    public sealed record RecordRow(Guid Id, Guid ClientId, Guid TemplateId, string TemplateName, string Title, int Version, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
    public sealed record RecordView(Guid Id, Guid ClientId, string Title, int Version, bool CanEdit, bool CanReveal, bool CanManageSecrecy, IReadOnlyList<FieldInfo> Fields, Dictionary<string, string?> Text, Dictionary<string, bool> Bools, Dictionary<string, List<string>> Multi, Dictionary<string, string[]> Errors, IReadOnlyCollection<string> SecretKeys, IReadOnlyDictionary<string, bool> SecretFlags);

    public async Task<IReadOnlyList<TemplateRow>> TemplatesAsync()
    {
        using var scope = scopes.CreateScope();
        var forms = scope.ServiceProvider.GetRequiredService<FormService>();
        var list = await forms.ListTemplatesAsync(includeInactive: false);

        // Solo la última versión de cada clave: las anteriores son histórico.
        return list
            .GroupBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(t => t.Version).First())
            .OrderBy(t => t.Name)
            .Select(t => new TemplateRow(t.Id, t.Key, t.Name, t.Description, t.Version))
            .ToList();
    }

    public async Task<IReadOnlyList<RecordRow>> RecordsAsync(Guid clientId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, forms) = await ResolveAsync(sp);
        if (user is null)
            return [];

        var list = await forms.ListRecordsAsync(clientId);
        var visible = new List<RecordRow>();
        foreach (var r in list)
        {
            if (!await access.CanViewRecordAsync(user, clientId, r.Id))
                continue;

            visible.Add(new RecordRow(r.Id, r.ClientId, r.TemplateId, r.TemplateName, r.Title, r.Version, r.CreatedAt, r.UpdatedAt));
        }

        return visible;
    }

    public async Task<IReadOnlyList<FieldInfo>> FieldsAsync(Guid templateId)
    {
        using var scope = scopes.CreateScope();
        var forms = scope.ServiceProvider.GetRequiredService<FormService>();
        var template = await forms.GetTemplateAsync(templateId);
        if (template is null)
            return [];

        return template.Fields.OrderBy(f => f.Order)
            .Select(f => new FieldInfo(f.Key, f.Label, f.Type.ToString(), f.IsRequired, ParseOptions(f.OptionsJson), f.HelpText, f.IsSecret))
            .ToList();
    }

    public async Task<bool> CanManageSecrecyAsync(Guid clientId)
    {
        using var scope = scopes.CreateScope();
        var (user, access, _) = await ResolveAsync(scope.ServiceProvider);
        return user is not null && await access.CanManageSecrecyAsync(user, clientId);
    }

    public async Task<(string? Value, string? Error)> RevealAsync(Guid recordId, string fieldKey)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, forms) = await ResolveAsync(sp);
        if (user is null)
            return (null, "Sesión no válida.");

        var record = await forms.GetRecordAsync(recordId);
        if (record is null)
            return (null, "La ficha no existe.");

        if (!await access.CanRevealRecordAsync(user, record.ClientId, record.Id))
            return (null, "No tienes permiso para revelar secretos de esta ficha.");

        var value = await forms.RevealSecretAsync(recordId, fieldKey);
        if (value is null)
            return (null, "Ese campo no tiene valor guardado.");

        var audit = sp.GetRequiredService<ControClient.Domain.Abstractions.IAuditWriter>();
        await audit.WriteAsync(new ControClient.Domain.Auditing.AuditEvent(
            entity: "records", action: "records.reveal_field", actorName: user.DisplayName,
            actorObjectId: user.EntraObjectId, correlationId: Guid.NewGuid().ToString("N"),
            clientId: record.ClientId, reason: $"ficha={recordId} campo={fieldKey}"));

        return (value, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid recordId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, forms) = await ResolveAsync(sp);
        if (user is null)
            return (false, "Sin sesión.");

        var record = await forms.GetRecordAsync(recordId);
        if (record is null)
            return (false, "La ficha no existe.");

        if (!await access.CanEditRecordAsync(user, record.ClientId, record.Id))
            return (false, "No tienes permiso para modificar este cliente.");

        if (!await forms.DeleteRecordAsync(recordId))
            return (false, "No se pudo eliminar.");

        var audit = sp.GetRequiredService<ControClient.Domain.Abstractions.IAuditWriter>();
        await audit.WriteAsync(new ControClient.Domain.Auditing.AuditEvent(
            entity: "records", action: "records.delete", actorName: user.DisplayName,
            actorObjectId: user.EntraObjectId, correlationId: Guid.NewGuid().ToString("N"),
            clientId: record.ClientId, reason: $"ficha={record.Title}"));

        return (true, null);
    }

    public async Task<RecordView?> GetAsync(Guid recordId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, forms) = await ResolveAsync(sp);
        if (user is null)
            return null;

        var record = await forms.GetRecordAsync(recordId);
        if (record is null || !await access.CanViewRecordAsync(user, record.ClientId, record.Id))
            return null;

        var template = await forms.GetTemplateAsync(record.TemplateId);
        if (template is null)
            return null;

        var canEdit = await access.CanEditRecordAsync(user, record.ClientId, record.Id);
        var text = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var bools = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var multi = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(record.ValuesJson);
        var canReveal = await access.CanRevealRecordAsync(user, record.ClientId, record.Id);
        var canManageSecrecy = await access.CanManageSecrecyAsync(user, record.ClientId);
        var secretKeys = await forms.SecretKeysAsync(record.Id);
        var secretFlags = await forms.SecretFlagsAsync(record.Id);

        var fields = template.Fields.OrderBy(f => f.Order).Select(f => new FieldInfo(
            f.Key, f.Label, f.Type.ToString(), f.IsRequired, ParseOptions(f.OptionsJson), f.HelpText, f.IsSecret)).ToList();

        foreach (var field in fields)
        {
            if (!doc.RootElement.TryGetProperty(field.Key, out var value))
                continue;

            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    bools[field.Key] = value.GetBoolean();
                    break;
                case JsonValueKind.Array:
                    multi[field.Key] = value.EnumerateArray().Select(v => v.GetString() ?? string.Empty).ToList();
                    break;
                case JsonValueKind.Number:
                    text[field.Key] = value.GetRawText();
                    break;
                default:
                    text[field.Key] = value.GetString();
                    break;
            }
        }

        return new RecordView(record.Id, record.ClientId, record.Title, record.Version, canEdit, canReveal, canManageSecrecy, fields, text, bools, multi, [], secretKeys, secretFlags);
    }

    public async Task<(Guid? RecordId, Dictionary<string, string[]>? Errors, string? Error)> CreateAsync(
        Guid clientId, Guid templateId, string title,
        IReadOnlyList<FieldInfo> fields,
        Dictionary<string, string?> text, Dictionary<string, bool> bools, Dictionary<string, List<string>> multi,
        IReadOnlyDictionary<string, string>? secrets = null,
        IReadOnlyDictionary<string, bool>? secretFlags = null)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, forms) = await ResolveAsync(sp);
        if (user is null)
            return (null, null, "Sesión no válida.");

        if (!await access.CanEditAsync(user, clientId))
            return (null, null, "No tienes permiso para añadir fichas a este cliente.");

        var json = BuildJson(fields, text, bools, multi);
        var canManage = await access.CanManageSecrecyAsync(user, clientId);
        var (record, errors, error) = await forms.CreateRecordAsync(clientId, templateId, title, JsonDocument.Parse(json).RootElement, user.DisplayName, secrets, secretFlags, canManage);

        if (errors is not null)
            return (null, errors.ToDictionary(e => e.Key, e => e.Value), null);

        if (record is null)
            return (null, null, error);

        await AuditAsync(sp, user, "records.create", clientId, $"ficha={record.Id}");
        return (record.Id, null, null);
    }

    public async Task<(bool Ok, Dictionary<string, string[]>? Errors, string? Error, bool Conflict)> UpdateAsync(
        Guid recordId, string title, int version,
        IReadOnlyList<FieldInfo> fields,
        Dictionary<string, string?> text, Dictionary<string, bool> bools, Dictionary<string, List<string>> multi,
        IReadOnlyDictionary<string, string>? secrets = null,
        IReadOnlyDictionary<string, bool>? secretFlags = null)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, forms) = await ResolveAsync(sp);
        if (user is null)
            return (false, null, "Sesión no válida.", false);

        var existing = await forms.GetRecordAsync(recordId);
        if (existing is null || !await access.CanEditRecordAsync(user, existing.ClientId, existing.Id))
            return (false, null, "No tienes permiso para modificar esta ficha.", false);

        var json = BuildJson(fields, text, bools, multi);
        var (record, errors, error, notFound, conflict) = await forms.UpdateRecordAsync(
            recordId, title, JsonDocument.Parse(json).RootElement, version);

        if (conflict)
            return (false, null, error, true);

        if (errors is not null)
            return (false, errors.ToDictionary(e => e.Key, e => e.Value), null, false);

        if (record is null)
            return (false, null, notFound ? "La ficha no existe." : error, false);

        await AuditAsync(sp, user, "records.update", record.ClientId, $"ficha={record.Id} version={record.Version}");
        return (true, null, null, false);
    }

    /// <summary>Convierte los campos del formulario al JSON que valida el dominio.</summary>
    public static string BuildJson(
        IReadOnlyList<FieldInfo> fields,
        Dictionary<string, string?> text,
        Dictionary<string, bool> bools,
        Dictionary<string, List<string>> multi)
    {
        var obj = new JsonObject();

        foreach (var field in fields)
        {
            switch (field.Type)
            {
                case nameof(FieldType.Boolean):
                    if (bools.TryGetValue(field.Key, out var flag))
                        obj[field.Key] = flag;
                    break;

                case nameof(FieldType.MultiSelect):
                    if (multi.TryGetValue(field.Key, out var values) && values.Count > 0)
                    {
                        var array = new JsonArray();
                        foreach (var item in values) array.Add(item);
                        obj[field.Key] = array;
                    }
                    break;

                case nameof(FieldType.Number):
                    if (text.TryGetValue(field.Key, out var raw) && !string.IsNullOrWhiteSpace(raw))
                    {
                        obj[field.Key] = double.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var number)
                            ? JsonValue.Create(number)
                            : JsonValue.Create(raw); // deja que el validador devuelva el error
                    }
                    break;

                default:
                    if (text.TryGetValue(field.Key, out var value) && !string.IsNullOrWhiteSpace(value))
                        obj[field.Key] = value.Trim();
                    break;
            }
        }

        return obj.ToJsonString();
    }

    public static string[] ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private async Task<(AppUser? User, IClientAccessService Access, FormService Forms)> ResolveAsync(IServiceProvider sp)
    {
        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        var access = sp.GetRequiredService<IClientAccessService>();
        var forms = sp.GetRequiredService<FormService>();

        if (await sp.GetRequiredService<ControClient.Web.Security.VaultLockService>().IsLockedAsync())
            return (null, access, forms);

        if (current is null)
            return (null, access, forms);

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, null);
        return (user.IsActive ? user : null, access, forms);
    }

    private static async Task AuditAsync(IServiceProvider sp, AppUser user, string action, Guid clientId, string reason)
    {
        var audit = sp.GetRequiredService<IAuditWriter>();
        await audit.WriteAsync(new AuditEvent(
            entity: "record_instances",
            action: action,
            actorName: user.DisplayName,
            actorObjectId: user.EntraObjectId,
            correlationId: Guid.NewGuid().ToString("N"),
            clientId: clientId,
            reason: reason));
    }
}
