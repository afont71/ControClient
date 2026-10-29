using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ControClient.Infrastructure.Data;

/// <summary>
/// Fábrica para las herramientas de EF Core (migraciones). Usa la cadena del
/// usuario dueño del esquema si existe la variable CONTOCLIENT_DB_OWNER; si no,
/// un valor ficticio que solo sirve para generar el DDL.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string Dummy = "Host=localhost;Port=5432;Database=contoclient;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTOCLIENT_DB_OWNER");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = Dummy;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
