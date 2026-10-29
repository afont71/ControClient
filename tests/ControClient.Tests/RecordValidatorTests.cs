using System.Text.Json;
using ControClient.Domain.Forms;

namespace ControClient.Tests;

public class RecordValidatorTests
{
    private static FormTemplate ServerTemplate() => new("servidor", "Servidor", null,
    [
        new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1),
        new FieldDefinition("ip", "IP", FieldType.Ip, order: 2),
        new FieldDefinition("sistema", "Sistema", FieldType.Select, order: 3,
            options: JsonSerializer.Serialize(new[] { "Windows Server", "Linux" })),
        new FieldDefinition("rol", "Roles", FieldType.MultiSelect, order: 4,
            options: JsonSerializer.Serialize(new[] { "Web", "BD" })),
        new FieldDefinition("buzones", "Buzones", FieldType.Number, order: 5),
        new FieldDefinition("activo", "Activo", FieldType.Boolean, order: 6),
        new FieldDefinition("alta", "Alta", FieldType.Date, order: 7),
        new FieldDefinition("web", "Web", FieldType.Url, order: 8),
        new FieldDefinition("contacto", "Contacto", FieldType.Email, order: 9),
        new FieldDefinition("notas", "Notas", FieldType.LongText, order: 10)
    ]);

    private static ValidationOutcome Run(string json) =>
        RecordValidator.Validate(ServerTemplate(), JsonDocument.Parse(json).RootElement);

    [Fact]
    public void Acepta_una_ficha_correcta_y_normaliza()
    {
        var result = Run("""{"nombre":"SRV01","ip":"10.0.0.5","sistema":"Linux","rol":["Web"],"buzones":12,"activo":true,"alta":"2026-01-31","web":"https://ejemplo.com","contacto":"a@b.com"}""");

        Assert.True(result.IsValid);
        Assert.Contains("\"nombre\":\"SRV01\"", result.NormalizedJson);
        Assert.Contains("\"buzones\":12", result.NormalizedJson);
    }

    [Fact]
    public void Rechaza_campo_obligatorio_ausente()
    {
        var result = Run("""{"ip":"10.0.0.5"}""");

        Assert.False(result.IsValid);
        Assert.Contains("nombre", result.Errors.Keys);
    }

    [Fact]
    public void Rechaza_campos_desconocidos()
    {
        var result = Run("""{"nombre":"SRV01","inventado":"x"}""");

        Assert.False(result.IsValid);
        Assert.Contains("inventado", result.Errors.Keys);
    }

    [Fact]
    public void Rechaza_numero_como_texto()
    {
        var result = Run("""{"nombre":"SRV01","buzones":"doce"}""");

        Assert.False(result.IsValid);
        Assert.Contains("buzones", result.Errors.Keys);
    }

    [Fact]
    public void Rechaza_opcion_no_permitida()
    {
        var result = Run("""{"nombre":"SRV01","sistema":"MacOS"}""");

        Assert.False(result.IsValid);
        Assert.Contains("sistema", result.Errors.Keys);
    }

    [Fact]
    public void Rechaza_multiseleccion_con_opcion_invalida()
    {
        var result = Run("""{"nombre":"SRV01","rol":["Web","Correo"]}""");

        Assert.False(result.IsValid);
        Assert.Contains("rol", result.Errors.Keys);
    }

    [Fact]
    public void Rechaza_ip_correo_url_y_fecha_invalidos()
    {
        var result = Run("""{"nombre":"SRV01","ip":"999.1.1.1","contacto":"no-es-correo","web":"ftp://x","alta":"31/01/2026"}""");

        Assert.False(result.IsValid);
        Assert.Contains("ip", result.Errors.Keys);
        Assert.Contains("contacto", result.Errors.Keys);
        Assert.Contains("web", result.Errors.Keys);
        Assert.Contains("alta", result.Errors.Keys);
    }

    [Fact]
    public void Rechaza_referencia_a_boveda_hasta_la_etapa_4()
    {
        var template = new FormTemplate("acceso", "Acceso", null,
        [
            new FieldDefinition("usuario", "Usuario", FieldType.Text, required: true, order: 1),
            new FieldDefinition("secreto", "Contraseña", FieldType.SecretReference, order: 2)
        ]);

        var result = RecordValidator.Validate(template,
            JsonDocument.Parse("""{"usuario":"admin","secreto":"ref-1"}""").RootElement);

        Assert.False(result.IsValid);
        Assert.Contains("secreto", result.Errors.Keys);
    }

    [Fact]
    public void Los_booleanos_falsos_cuentan_como_valor()
    {
        var template = new FormTemplate("test", "Test", null,
        [
            new FieldDefinition("activo", "Activo", FieldType.Boolean, required: true, order: 1)
        ]);

        var result = RecordValidator.Validate(template, JsonDocument.Parse("""{"activo":false}""").RootElement);

        Assert.True(result.IsValid); // 0 y false son valores válidos (BASE-07)
    }
}
