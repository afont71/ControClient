using System.Reflection;
using System.Security.Claims;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        // Salud: anónima y sin datos de negocio (NFR-01, contrato §13.3).
        app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
        {
            var databaseOk = await db.Database.CanConnectAsync(ct);

            return Results.Json(new
            {
                status = databaseOk ? "ok" : "degraded",
                database = databaseOk ? "up" : "down",
                version = AppVersion(),
                timestamp = DateTimeOffset.UtcNow
            }, statusCode: databaseOk ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        }).AllowAnonymous();

        // Información de sistema: exige sesión con MFA.
        app.MapGet("/api/v1/system/info", (ClaimsPrincipal principal, IConfiguration config) =>
        {
            var user = CurrentUser.FromPrincipal(principal);
            if (user is null)
                return Results.Unauthorized();

            return Results.Ok(new
            {
                application = "Gestión de clientes IT",
                version = AppVersion(),
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                vault = "sealed", // Etapa 4: estado real de la bóveda (SEC-11…SEC-14).
                specification = "v1.0.0",
                authentication = config["Authentication:Mode"] ?? "Entra",                user = new { user.ObjectId, user.Name, Role = user.Role.ToString(), user.MfaSatisfied }
            });
        }).RequireAuthorization();

        // Auditoría: solo roles con permiso explícito (PRM §3.3).
        app.MapGet("/api/v1/audit/events", async (
            ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var user = CurrentUser.FromPrincipal(principal);
            if (user is null)
                return Results.Unauthorized();

            if (!user.IsIn(AppRole.Administrator, AppRole.Auditor, AppRole.VaultCustodian))
                return Results.Forbid();

            var events = await db.AuditEvents
                .AsNoTracking()
                .OrderByDescending(e => e.OccurredAt)
                .Take(50)
                .Select(e => new
                {
                    e.Id,
                    e.OccurredAt,
                    e.ActorName,
                    e.Entity,
                    e.Action,
                    e.Result,
                    e.ClientId,
                    e.CorrelationId
                })
                .ToListAsync(ct);

            return Results.Ok(events);
        }).RequireAuthorization();

        return app;
    }

    private static string AppVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
}

