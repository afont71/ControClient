using System.Security.Claims;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Clients;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Endpoints;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/clients").RequireAuthorization();

        // ---------------------------------------------------------------- listado
        group.MapGet("/", async (
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            AppDbContext db,
            string? query,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            // El alcance se aplica antes de contar y antes de serializar (AC-05).
            var visible = await access.GetVisibleClientIdsAsync(user, ct);
            var clients = db.Clients.AsNoTracking().AsQueryable();

            if (visible is not null)
                clients = clients.Where(c => visible.Contains(c.Id));

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLower();
                clients = clients.Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    (c.Alias != null && c.Alias.ToLower().Contains(term)));
            }

            var total = await clients.CountAsync(ct);
            var items = await clients
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Alias,
                    Status = c.Status.ToString(),
                    c.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(new { items, total, page, pageSize });
        });

        // ---------------------------------------------------------------- detalle
        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            // Fuera de alcance responde 404: no se revela su existencia (SEC-09, AC-05).
            if (!await access.CanViewAsync(user, id, ct))
                return Results.NotFound();

            var client = await db.Clients.AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new { c.Id, c.Name, c.Alias, Status = c.Status.ToString(), c.CreatedAt, c.UpdatedAt })
                .FirstOrDefaultAsync(ct);

            return client is null ? Results.NotFound() : Results.Ok(client);
        });

        // ---------------------------------------------------------------- alta
        group.MapPost("/", async (
            CreateClientRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            AppDbContext db,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!user.IsIn(AppRole.Administrator, AppRole.ClientManager))
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["El nombre es obligatorio."]
                });

            var name = request.Name.Trim();
            if (await db.Clients.AnyAsync(c => c.Name == name, ct))
                return Results.Conflict(new { error = "Ya existe un cliente con ese nombre." });

            var client = new Client(name, request.Alias);
            db.Clients.Add(client);

            // Quien crea el cliente conserva acceso si no es administrador global.
            if (!user.IsIn(AppRole.Administrator))
                db.Grants.Add(new ClientGrant(user.Id, client.Id, GrantActions.View | GrantActions.Edit, user.DisplayName));

            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "clients",
                action: "clients.create",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: Correlation(http),
                clientId: client.Id,
                ipAddress: http.HttpContext?.Connection.RemoteIpAddress?.ToString()), ct);

            return Results.Created($"/api/v1/clients/{client.Id}",
                new { client.Id, client.Name, client.Alias, Status = client.Status.ToString() });
        });

        // ---------------------------------------------------------------- modificación
        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateClientRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            AppDbContext db,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!await access.CanViewAsync(user, id, ct))
                return Results.NotFound();

            if (!await access.CanEditAsync(user, id, ct))
                return Results.Forbid();

            var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (client is null)
                return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim() != client.Name)
            {
                var name = request.Name.Trim();
                if (await db.Clients.AnyAsync(c => c.Name == name && c.Id != id, ct))
                    return Results.Conflict(new { error = "Ya existe un cliente con ese nombre." });

                client.Rename(name);
            }

            if (!string.IsNullOrWhiteSpace(request.Status)
                && Enum.TryParse<ClientStatus>(request.Status, ignoreCase: true, out var status))
            {
                client.SetStatus(status);
            }

            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "clients",
                action: "clients.update",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: Correlation(http),
                clientId: client.Id,
                ipAddress: http.HttpContext?.Connection.RemoteIpAddress?.ToString()), ct);

            return Results.Ok(new { client.Id, client.Name, client.Alias, Status = client.Status.ToString() });
        });

        // ---------------------------------------------------------------- concesiones
        group.MapGet("/{id:guid}/grants", async (
            Guid id,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!await access.CanEditAsync(user, id, ct))
                return Results.NotFound();

            var grants = await (
                from g in db.Grants.AsNoTracking()
                join u in db.Users.AsNoTracking() on g.UserId equals u.Id
                where g.ClientId == id
                orderby u.DisplayName
                select new
                {
                    g.Id,
                    UserId = u.Id,
                    UserObjectId = u.EntraObjectId,
                    UserName = u.DisplayName,
                    g.Actions,
                    g.GrantedAt,
                    g.ExpiresAt,
                    g.GrantedBy
                }).ToListAsync(ct);

            return Results.Ok(grants.Select(g => new
            {
                g.Id,
                g.UserId,
                g.UserObjectId,
                g.UserName,
                Actions = g.Actions.ToString(),
                g.GrantedAt,
                g.ExpiresAt,
                g.GrantedBy
            }));
        });

        group.MapPost("/{id:guid}/grants", async (
            Guid id,
            CreateGrantRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            AppDbContext db,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            // Gestionar permisos es exclusivo del administrador (§3.3).
            if (!user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            if (!await db.Clients.AnyAsync(c => c.Id == id, ct))
                return Results.NotFound();

            var target = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
            if (target is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["userId"] = ["El usuario no existe."]
                });

            var actions = (GrantActions)request.Actions;
            if (actions == GrantActions.None)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["actions"] = ["Indica al menos una acción: View=1, Edit=2, RevealSecret=4."]
                });

            var existing = await db.Grants.FirstOrDefaultAsync(g => g.UserId == target.Id && g.ClientId == id, ct);
            if (existing is not null)
            {
                existing.ChangeActions(actions);
                existing.SetExpiry(request.ExpiresAt);
            }
            else
            {
                db.Grants.Add(new ClientGrant(target.Id, id, actions, user.DisplayName, request.ExpiresAt));
            }

            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "client_grants",
                action: "grants.upsert",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: Correlation(http),
                clientId: id,
                reason: $"usuario={target.EntraObjectId} acciones={actions}",
                ipAddress: http.HttpContext?.Connection.RemoteIpAddress?.ToString()), ct);

            return Results.Ok(new { clientId = id, userId = target.Id, actions = actions.ToString() });
        });

        group.MapDelete("/{id:guid}/grants/{grantId:guid}", async (
            Guid id,
            Guid grantId,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            AppDbContext db,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            var grant = await db.Grants.FirstOrDefaultAsync(g => g.Id == grantId && g.ClientId == id, ct);
            if (grant is null)
                return Results.NotFound();

            db.Grants.Remove(grant);
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "client_grants",
                action: "grants.revoke",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: Correlation(http),
                clientId: id,
                reason: $"userId={grant.UserId}",
                ipAddress: http.HttpContext?.Connection.RemoteIpAddress?.ToString()), ct);

            return Results.NoContent();
        });

        return app;
    }

    private static string Correlation(IHttpContextAccessor http) =>
        http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    public sealed record CreateClientRequest(string Name, string? Alias);

    public sealed record UpdateClientRequest(string? Name, string? Status);

    public sealed record CreateGrantRequest(Guid UserId, int Actions, DateTimeOffset? ExpiresAt);
}
