using ControClient.Domain.Clients;
using ControClient.Domain.Documents;
using ControClient.Domain.Forms;
using ControClient.Domain.Vault;
using ControClient.Infrastructure.Data;
using ControClient.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Tests;

public class SearchServiceTests
{
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"search-{Guid.NewGuid():N}")
            .Options);

    private static async Task<(AppDbContext Db, Guid ClientA, Guid ClientB)> SeedAsync()
    {
        var db = Db();

        var clientA = new Client("Bodegas Habla", "Bodegas");
        var clientB = new Client("Institut FP", null);
        db.Clients.AddRange(clientA, clientB);

        var template = new FormTemplate("servidor", "Servidor", null,
        [
            new FieldDefinition("nombre", "Nombre", FieldType.Text, required: true, order: 1)
        ]);
        db.FormTemplates.Add(template);

        var recordA = new RecordInstance(clientA.Id, template, "SRV-DC01", """{"nombre":"SRV-DC01"}""", "tester");
        var recordB = new RecordInstance(clientB.Id, template, "SRV-INSTITUT", """{"nombre":"SRV-INSTITUT"}""", "tester");
        db.Records.AddRange(recordA, recordB);

        var docA = new Document(clientA.Id, "Contrato mantenimiento", DocumentClassification.Confidential, "tester");
        docA.AddVersion("contrato.txt", "text/plain", 10, "abc", "aa/bb/abc", "tester");
        var docB = new Document(clientB.Id, "Factura Institut", DocumentClassification.Internal, "tester");
        docB.AddVersion("factura.pdf", "application/pdf", 10, "def", "cc/dd/def", "tester");
        db.Documents.AddRange(docA, docB);

        var vaultA = new VaultItem(clientA.Id, "FortiGate", VaultItemKind.Password, "tester", "admin", "https://10.0.0.1", null);
        vaultA.AddVersion([1], [2], [3], [4], "tester");
        db.VaultItems.Add(vaultA);

        await db.SaveChangesAsync();
        return (db, clientA.Id, clientB.Id);
    }

    [Fact]
    public async Task Sin_alcance_global_solo_devuelve_lo_concedido()
    {
        var (db, clientA, _) = await SeedAsync();
        var search = new SearchService(db);

        var results = await search.SearchAsync([clientA], "srv", 50);

        Assert.All(results, r => Assert.Equal(clientA, r.ClientId));
        Assert.Contains(results, r => r.Type == "Ficha" && r.Title == "SRV-DC01");
        Assert.DoesNotContain(results, r => r.Title == "SRV-INSTITUT");
    }

    [Fact]
    public async Task Busca_por_nombre_de_cliente_documento_y_credencial()
    {
        var (db, _, _) = await SeedAsync();
        var search = new SearchService(db);

        Assert.Contains(await search.SearchAsync(null, "bodegas", 50), r => r.Type == "Cliente");
        Assert.Contains(await search.SearchAsync(null, "contrato", 50), r => r.Type == "Documento");
        Assert.Contains(await search.SearchAsync(null, "fortigate", 50), r => r.Type == "Credencial");
    }

    [Fact]
    public async Task No_encuentra_por_el_valor_del_secreto()
    {
        var (db, _, _) = await SeedAsync();
        var search = new SearchService(db);

        // El secreto "ClaveForti2026" no se guarda como metadato ni se indexa.
        var results = await search.SearchAsync(null, "ClaveForti2026", 50);
        Assert.Empty(results);
    }

    [Fact]
    public async Task El_alcance_tambien_filtra_documentos_y_credenciales()
    {
        var (db, _, clientB) = await SeedAsync();
        var search = new SearchService(db);

        var results = await search.SearchAsync([clientB], "contrato", 50);
        Assert.Empty(results); // el contrato es del cliente A

        var vaultResults = await search.SearchAsync([clientB], "fortigate", 50);
        Assert.Empty(vaultResults);
    }
}
