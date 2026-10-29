using ControClient.Domain.Auditing;

namespace ControClient.Tests;

public class RedactorTests
{
    [Theory]
    [InlineData("password=Secreto1", "password=***")]
    [InlineData("PWD: abc123", "PWD: ***")]
    [InlineData("clave: hunter2", "clave: ***")]
    [InlineData("token = eyJhbGciOi", "token = ***")]
    public void Redacta_pares_clave_valor(string input, string expected)
    {
        Assert.Equal(expected, Redactor.Redact(input));
    }

    [Fact]
    public void Redacta_credenciales_en_url()
    {
        var result = Redactor.Redact("conectando a https://admin:secreto@10.0.0.5/admin");
        Assert.Equal("conectando a https://***:***@10.0.0.5/admin", result);
    }

    [Fact]
    public void Conserva_texto_sin_secretos()
    {
        const string texto = "Reinicio del servidor tras actualizar FortiOS";
        Assert.Equal(texto, Redactor.Redact(texto));
    }

    [Fact]
    public void Texto_nulo_devuelve_vacio()
    {
        Assert.Equal(string.Empty, Redactor.Redact(null));
    }

    [Fact]
    public void Un_evento_de_auditoria_redacta_el_motivo()
    {
        var evt = new AuditEvent(
            entity: "vault",
            action: "vault.reveal",
            actorName: "Técnico",
            actorObjectId: "oid-1",
            correlationId: "corr-1",
            reason: "pedido con password=abc123");

        Assert.DoesNotContain("abc123", evt.Reason);
        Assert.Contains("***", evt.Reason);
    }
}
