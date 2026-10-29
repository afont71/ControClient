using ControClient.Domain.Clients;
using ControClient.Domain.Forms;
using ControClient.Infrastructure.Data;
using ControClient.Infrastructure.Expirations;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Tests;

public class ExpirationsTests
{
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"exp-{Guid.NewGuid():N}")
            .Options);

    private static FormTemplate Template() => new("licencia", "Licencia", null,
    [
        new FieldDefinition("producto", "Producto", FieldType.Text, required: true, order: 1),
        new FieldDefinition("renovacion", "Renovación", FieldType.Date, order: 2)
    ], version: 1);

    [Fact]
    public async Task Incluye_vencidos_y_proximos_y_excluye_lejanos()
    {
        await using var db = Db();
        var template = Template();
        var client = new Client("Bodegas", null);
        db.FormTemplates.Add(template);
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string Json(DateOnly d) => $"{{\"producto\":\"Office\",\"renovacion\":\"{d:yyyy-MM-dd}\"}}";

        db.Records.Add(new RecordInstance(client.Id, template, "Office", Json(today.AddDays(10)), "test"));
        db.Records.Add(new RecordInstance(client.Id, template, "Antivirus", Json(today.AddDays(-3)), "test"));
        db.Records.Add(new RecordInstance(client.Id, template, "Backup", Json(today.AddDays(200)), "test"));
        await db.SaveChangesAsync();

        var rows = await new ExpirationsService(db).GetAsync(null, 90);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Antivirus", rows[0].RecordTitle);
        Assert.True(rows[0].DaysLeft < 0);
        Assert.Equal("Office", rows[1].RecordTitle);
        Assert.Equal(10, rows[1].DaysLeft);
        Assert.DoesNotContain(rows, r => r.RecordTitle == "Backup");
    }

    [Fact]
    public async Task Respeta_el_alcance_del_usuario()
    {
        await using var db = Db();
        var template = Template();
        var mine = new Client("Bodegas", null);
        var other = new Client("Institut", null);
        db.FormTemplates.Add(template);
        db.Clients.AddRange(mine, other);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);
        string Json = $"{{\"producto\":\"Office\",\"renovacion\":\"{today:yyyy-MM-dd}\"}}";

        db.Records.Add(new RecordInstance(mine.Id, template, "Mío", Json, "test"));
        db.Records.Add(new RecordInstance(other.Id, template, "Ajeno", Json, "test"));
        await db.SaveChangesAsync();

        var rows = await new ExpirationsService(db).GetAsync([mine.Id], 90);

        Assert.Single(rows);
        Assert.Equal("Mío", rows[0].RecordTitle);
    }

    [Fact]
    public async Task Ignora_campos_de_fecha_vacios_o_mal_formados()
    {
        await using var db = Db();
        var template = Template();
        var client = new Client("Bodegas", null);
        db.FormTemplates.Add(template);
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        db.Records.Add(new RecordInstance(client.Id, template, "Sin fecha", "{\"producto\":\"Office\"}", "test"));
        db.Records.Add(new RecordInstance(client.Id, template, "Mala", "{\"producto\":\"Office\",\"renovacion\":\"31/12/2026\"}", "test"));
        await db.SaveChangesAsync();

        var rows = await new ExpirationsService(db).GetAsync(null, 365);

        Assert.Empty(rows);
    }
}
