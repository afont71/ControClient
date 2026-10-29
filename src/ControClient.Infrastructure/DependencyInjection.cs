using ControClient.Domain.Abstractions;
using ControClient.Domain.Security;
using ControClient.Infrastructure.Auditing;
using ControClient.Infrastructure.Data;
using ControClient.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControClient.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra persistencia, auditoría, autorización y comprobación de salud.
    /// Proveedor "InMemory" solo para desarrollo y pruebas locales: nunca producción.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "PostgreSQL";

        if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("ControClient-dev"));
        }
        else
        {
            var connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException(
                    "Falta ConnectionStrings:Default. Configure la cadena con user-secrets o variables de entorno.");
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        }

        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddSingleton<IClientAccessPolicy, ClientAccessPolicy>();
        services.AddScoped<IClientAccessService, ClientAccessService>();
        services.AddScoped<IUserProvisioner, UserProvisioner>();
        services.AddScoped<ControClient.Infrastructure.Forms.FormService>();
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}

