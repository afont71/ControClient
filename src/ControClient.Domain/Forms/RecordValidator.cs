using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControClient.Domain.Forms;

public sealed record ValidationOutcome(bool IsValid, string? NormalizedJson, IReadOnlyDictionary<string, string[]> Errors)
{
    public static ValidationOutcome Invalid(Dictionary<string, string[]> errors) => new(false, null, errors);
    public static ValidationOutcome Valid(string json) => new(true, json, new Dictionary<string, string[]>());
}

/// <summary>
/// Validación y normalización de los valores de una ficha contra su plantilla.
/// Todo se comprueba en servidor; el JSON normalizado solo incluye campos conocidos.
/// </summary>
public static class RecordValidator
{
    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    public static ValidationOutcome Validate(FormTemplate template, JsonElement values)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (values.ValueKind != JsonValueKind.Object)
            return ValidationOutcome.Invalid(new() { ["_"] = ["Los valores deben ser un objeto."] });

        var known = template.Fields.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var property in values.EnumerateObject())
        {
            if (!known.Contains(property.Name))
                errors[property.Name] = ["El campo no existe en la plantilla."];
        }

        var normalized = new JsonObject();

        foreach (var field in template.Fields.OrderBy(f => f.Order))
        {
            var has = values.TryGetProperty(field.Key, out var raw)
                      && raw.ValueKind != JsonValueKind.Null
                      && !(raw.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(raw.GetString()))
                      && !(raw.ValueKind == JsonValueKind.Array && raw.GetArrayLength() == 0);

            if (!has)
            {
                if (field.IsRequired)
                    errors[field.Key] = ["Campo obligatorio."];
                continue;
            }

            if (!TryNormalize(field, raw, out var node, out var error))
            {
                errors[field.Key] = [error!];
                continue;
            }

            normalized[field.Key] = node;
        }

        return errors.Count > 0
            ? ValidationOutcome.Invalid(errors)
            : ValidationOutcome.Valid(normalized.ToJsonString(Compact));
    }

    private static bool TryNormalize(FieldDefinition field, JsonElement raw, out JsonNode? node, out string? error)
    {
        node = null;
        error = null;

        switch (field.Type)
        {
            case FieldType.Text:
            case FieldType.LongText:
                if (raw.ValueKind != JsonValueKind.String)
                    return Fail("Debe ser texto.", out error);
                var text = raw.GetString()!;
                var max = field.Type == FieldType.Text ? 500 : 20_000;
                if (text.Length > max)
                    return Fail($"Máximo {max} caracteres.", out error);
                node = JsonValue.Create(text);
                return true;

            case FieldType.Number:
                if (raw.ValueKind != JsonValueKind.Number)
                    return Fail("Debe ser un número.", out error);
                node = JsonNode.Parse(raw.GetRawText());
                return true;

            case FieldType.Boolean:
                if (raw.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return Fail("Debe ser verdadero o falso.", out error);
                node = JsonValue.Create(raw.GetBoolean());
                return true;

            case FieldType.Date:
                // Formato ISO estricto e independiente de la cultura del servidor.
                if (raw.ValueKind != JsonValueKind.String
                    || !DateOnly.TryParseExact(raw.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    return Fail("Fecha inválida (formato aaaa-mm-dd).", out error);
                node = JsonValue.Create(date.ToString("yyyy-MM-dd"));
                return true;

            case FieldType.Select:
                if (raw.ValueKind != JsonValueKind.String)
                    return Fail("Debe ser una opción.", out error);
                var option = raw.GetString()!;
                var options = Options(field);
                if (!options.Contains(option))
                    return Fail("Opción no permitida.", out error);
                node = JsonValue.Create(option);
                return true;

            case FieldType.MultiSelect:
                if (raw.ValueKind != JsonValueKind.Array)
                    return Fail("Debe ser una lista de opciones.", out error);
                var allowed = Options(field);
                var selected = new List<string>();
                foreach (var item in raw.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                        return Fail("Cada opción debe ser texto.", out error);
                    var value = item.GetString()!;
                    if (!allowed.Contains(value))
                        return Fail($"Opción no permitida: {value}.", out error);
                    if (!selected.Contains(value))
                        selected.Add(value);
                }
                var array = new JsonArray();
                foreach (var value in selected) array.Add(value);
                node = array;
                return true;

            case FieldType.Ip:
                if (raw.ValueKind != JsonValueKind.String || !IPAddress.TryParse(raw.GetString(), out _))
                    return Fail("Dirección IP inválida.", out error);
                node = JsonValue.Create(raw.GetString());
                return true;

            case FieldType.Url:
                if (raw.ValueKind != JsonValueKind.String
                    || !Uri.TryCreate(raw.GetString(), UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    return Fail("URL inválida (http o https).", out error);
                node = JsonValue.Create(uri.ToString());
                return true;

            case FieldType.Email:
                if (raw.ValueKind != JsonValueKind.String
                    || !MailAddress.TryCreate(raw.GetString(), out _))
                    return Fail("Correo electrónico inválido.", out error);
                node = JsonValue.Create(raw.GetString());
                return true;

            case FieldType.SecretReference:
                return Fail("Las referencias a la bóveda llegan en la etapa 4; no se admiten en fichas todavía.", out error);

            default:
                return Fail("Tipo de campo no soportado.", out error);
        }
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }

    private static HashSet<string> Options(FieldDefinition field)
    {
        try
        {
            var parsed = JsonNode.Parse(field.OptionsJson ?? "[]") as JsonArray;
            return parsed?
                .Select(n => n?.GetValue<string>() ?? string.Empty)
                .Where(s => s.Length > 0)
                .ToHashSet(StringComparer.Ordinal) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
