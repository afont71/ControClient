using System.Text.Json;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Forms;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Forms;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControClient.Web.Pages;

/// <summary>Gestión de tipos de ficha y campos desde la interfaz (AC-04).</summary>
public sealed class TemplatesPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record TemplateRow(Guid Id, string Key, string Name, string? Description, int Version, bool IsActive, int Fields);
    public sealed record FieldDraft(string Key, string Label, string Type, bool Required, string? OptionsCsv);

    public async Task<IReadOnlyList<TemplateRow>?> ListAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, forms) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return null;

        var templates = await forms.ListTemplatesAsync(includeInactive: true);

        // Solo la última versión de cada clave: es la que se usa al crear fichas.
        return templates
            .GroupBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(t => t.Version).First())
            .OrderBy(t => t.Name)
            .Select(t => new TemplateRow(t.Id, t.Key, t.Name, t.Description, t.Version, t.IsActive, t.FieldCount))
            .ToList();
    }

    public async Task<IReadOnlyList<FieldDraft>> FieldsAsync(Guid templateId)
    {
        using var scope = scopes.CreateScope();
        var forms = scope.ServiceProvider.GetRequiredService<FormService>();
        var template = await forms.GetTemplateAsync(templateId);
        if (template is null)
            return [];

        return template.Fields.OrderBy(f => f.Order).Select(f => new FieldDraft(
            f.Key,
            f.Label,
            f.Type.ToString(),
            f.IsRequired,
            f.OptionsJson is null ? null : string.Join(", ", JsonSerializer.Deserialize<string[]>(f.OptionsJson) ?? [])
        )).ToList();
    }

    public async Task<(Guid? Id, string? Error)> CreateAsync(string key, string name, string? description, IReadOnlyList<FieldDraft> fields)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, forms) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (null, "Solo un administrador puede crear tipos de ficha.");

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name))
            return (null, "Clave y nombre son obligatorios.");

        if (fields.Count == 0)
            return (null, "Añade al menos un campo.");

        var duplicate = fields.GroupBy(f => f.Key, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1);
        if (duplicate)
            return (null, "Hay claves de campo repetidas.");

        if (fields.Any(f => string.IsNullOrWhiteSpace(f.Key) || string.IsNullOrWhiteSpace(f.Label)))
            return (null, "Cada campo necesita clave y etiqueta.");

        try
        {
            var specs = fields.Select((f, index) => new FormService.FieldSpec(
                f.Key.Trim(),
                f.Label.Trim(),
                Enum.TryParse<FieldType>(f.Type, ignoreCase: true, out var type) ? type : FieldType.Text,
                f.Required,
                ParseOptions(f.OptionsCsv),
                null,
                index + 1)).ToList();

            var template = await forms.CreateTemplateAsync(key, name, description, specs);
            return (template.Id, null);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// Guardar cambios de un tipo: como los tipos están versionados, crear una
    /// nueva versión con los campos resultantes. Las fichas antiguas conservan la
    /// versión con la que se crearon.
    /// </summary>
    public async Task<(Guid? Id, string? Error)> UpdateAsync(
        Guid templateId, string name, string? description, IReadOnlyList<FieldDraft> fields)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (actor, forms) = await ResolveAsync(sp);
        if (actor is null || !actor.IsIn(AppRole.Administrator))
            return (null, "Solo un administrador puede modificar tipos de ficha.");

        var template = await forms.GetTemplateAsync(templateId);
        if (template is null)
            return (null, "El tipo no existe.");

        if (string.IsNullOrWhiteSpace(name))
            return (null, "El nombre es obligatorio.");

        if (fields.Count == 0)
            return (null, "El tipo debe tener al menos un campo.");

        if (fields.Any(f => string.IsNullOrWhiteSpace(f.Key) || string.IsNullOrWhiteSpace(f.Label)))
            return (null, "Cada campo necesita clave y etiqueta.");

        if (fields.GroupBy(f => f.Key, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return (null, "Hay claves de campo repetidas.");

        try
        {
            var specs = fields.Select((f, index) => new FormService.FieldSpec(
                f.Key.Trim(),
                f.Label.Trim(),
                Enum.TryParse<FieldType>(f.Type, ignoreCase: true, out var type) ? type : FieldType.Text,
                f.Required,
                ParseOptions(f.OptionsCsv),
                null,
                index + 1)).ToList();

            var updated = await forms.CreateTemplateAsync(template.Key, name, description, specs);
            return (updated.Id, null);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }
    }

    private static string? ParseOptions(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return null;

        var values = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return values.Length == 0 ? null : JsonSerializer.Serialize(values);
    }

    private async Task<(AppUser? Actor, FormService Forms)> ResolveAsync(IServiceProvider sp)
    {
        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        var forms = sp.GetRequiredService<FormService>();
        if (current is null)
            return (null, forms);

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, current.Email);
        return (user.IsActive ? user : null, forms);
    }
}
