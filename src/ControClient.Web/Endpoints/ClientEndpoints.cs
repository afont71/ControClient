using System.Security.Claims;
using System.Text.Json;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Clients;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Endpoints;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/clients").RequireAuthorization();

        // GET: el filtro de alcance se aplica antes de serializar (AC-05).
        group.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var user = CurrentUser.FromPrincipal(principal);
            if (user is null)
                return Results.Unauthorized();

            // Etapa 1 añadirá la tabla de concesiones; mientras tanto solo los roles
            // globales ven datos y el resto obtiene una lista vacía, nunca un 403 con pistas.
            var canSeeAll = user.IsIn(AppRole.Administrator, AppRole.Auditor);
            if (!canSeeAll)
                return Results.Ok(Array.Empty<object>());

            var items = await db.Clients
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.Alias, Status = c.Status.ToString() })
                .ToListAsync(ct);

            return Results.Ok(items);
        });

        // POST: alta con permiso de edición y auditoría de la acción (AC-04, AC-26).
        group.MapPost("/", async (
            CreateClientRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = CurrentUser.FromPrincipal(principal);
            if (user is null)
                return Results.Unauthorized();

            if (!user.IsIn(AppRole.Administrator, AppRole.ClientManager))
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["El nombre es obligatorio."]
                });

            var exists = await db.Clients.AnyAsync(c => c.Name == request.Name.Trim(), ct);
            if (exists)
                return Results.Conflict(new { error = "Ya existe un cliente con ese nombre." });

            var client = new Client(request.Name, request.Alias);
            db.Clients.Add(client);
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "clients",
                action: "clients.create",
                actorName: user.Name,
                actorObjectId: user.ObjectId,
                correlationId: http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"),
                clientId: client.Id,
                ipAddress: http.HttpContext?.Connection.RemoteIpAddress?.ToString()), ct);

            return Results.Created($"/api/v1/clients/{client.Id}",
                new { client.Id, client.Name, client.Alias, Status = client.Status.ToString() });
        });

        return app;
    }

    public sealed record CreateClientRequest(string Name, string? Alias);
}

