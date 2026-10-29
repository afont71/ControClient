using System.Reflection;
using System.Security.Claims;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
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

        // Información de sistema. El rol que se muestra es el de la BASE DE DATOS,
        // que es el que aplica la autorización; el token de Entra no trae roles.
        app.MapGet("/api/v1/system/info", async (
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var claimUser = CurrentUser.FromPrincipal(principal);

            return Results.Ok(new
            {
                application = "Gestión de clientes IT",
                version = AppVersion(),
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                vault = "sealed", // Etapa 4: estado real de la bóveda (SEC-11…SEC-14).
                specification = "v1.0.0",
                authentication = config["Authentication:Mode"] ?? "Entra",
                user = new
                {
                    ObjectId = user.EntraObjectId,
                    Name = user.DisplayName,
                    Role = user.Role.ToString(),
                    MfaSatisfied = claimUser?.MfaSatisfied ?? false,
                    IsActive = user.IsActive
                }
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

        // Usuarios: solo administración, para poder conceder permisos por cliente.
        app.MapGet("/api/v1/users", async (
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            var users = await db.Users.AsNoTracking()
                .OrderBy(u => u.DisplayName)
                .Select(u => new { u.Id, u.EntraObjectId, u.DisplayName, Role = u.Role.ToString(), u.IsActive })
                .ToListAsync(ct);

            return Results.Ok(users);
        }).RequireAuthorization();

        // Cambio de rol: solo administración y queda auditado (PRM-13, PRM-15: no autoaprobación).
        app.MapPatch("/api/v1/users/{id:guid}/role", async (
            Guid id,
            ChangeRoleRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            AppDbContext db,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var actor = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (actor is null)
                return Results.Unauthorized();

            if (!actor.IsIn(AppRole.Administrator))
                return Results.Forbid();

            if (!Enum.TryParse<AppRole>(request.Role, ignoreCase: true, out var role))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = ["Rol inválido."]
                });

            var target = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (target is null)
                return Results.NotFound();

            // No autoaprobación: nadie cambia su propio rol (PRM-15).
            if (target.Id == actor.Id)
                return Results.Forbid();

            target.ChangeRole(role);
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "users",
                action: "users.change_role",
                actorName: actor.DisplayName,
                actorObjectId: actor.EntraObjectId,
                correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                reason: $"usuario={target.EntraObjectId} rol={role}",
                ipAddress: http.HttpContext?.Connection.RemoteIpAddress?.ToString()), ct);

            return Results.Ok(new { target.Id, Role = target.Role.ToString() });
        }).RequireAuthorization();

        // Activar o desactivar un usuario. Desactivar revoca de inmediato el acceso
        // (el servicio de acceso comprueba IsActive en cada petición).
        app.MapPatch("/api/v1/users/{id:guid}/active", async (
            Guid id,
            ChangeActiveRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            AppDbContext db,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var actor = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (actor is null)
                return Results.Unauthorized();

            if (!actor.IsIn(AppRole.Administrator))
                return Results.Forbid();

            if (id == actor.Id)
                return Results.Conflict(new { error = "No puedes desactivar tu propia cuenta." });

            var target = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (target is null)
                return Results.NotFound();

            if (!request.Active)
                target.Deactivate();

            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "users",
                action: request.Active ? "users.activate" : "users.deactivate",
                actorName: actor.DisplayName,
                actorObjectId: actor.EntraObjectId,
                correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                reason: $"usuario={target.EntraObjectId}"), ct);

            return Results.Ok(new { target.Id, target.IsActive });
        }).RequireAuthorization();

        return app;
    }

    public sealed record ChangeRoleRequest(string Role);
    public sealed record ChangeActiveRequest(bool Active);

    private static string AppVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
}

