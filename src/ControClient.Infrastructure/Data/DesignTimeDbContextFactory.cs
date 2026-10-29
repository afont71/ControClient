using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ControClient.Infrastructure.Data;

/// <summary>
/// Fábrica para las herramientas de EF Core (migraciones). No conecta a la base:
/// solo necesita un proveedor para generar el DDL.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string Dummy = "Host=localhost;Port=5432;Database=contoclient;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Dummy)
            .Options;

        return new AppDbContext(options);
    }
}
