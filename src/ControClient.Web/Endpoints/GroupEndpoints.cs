using System.Security.Claims;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Clients;
using ControClient.Domain.Groups;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Data;
using ControClient.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Web.Endpoints;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/groups").RequireAuthorization();

        group.MapGet("/", async (
            ClaimsPrincipal principal, IUserProvisioner provisioner, AppDbContext db, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null || !user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            var items = await db.Groups.AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Description,
                    g.IsActive,
                    Members = g.Members.Count,
                    Clients = g.Grants.Count
                })
                .ToListAsync(ct);

            return Results.Ok(items);
        });

        group.MapPost("/", async (
            CreateGroupRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            AppDbContext db,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null || !user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["El nombre es obligatorio."]
                });

            if (await db.Groups.AnyAsync(g => g.Name == request.Name.Trim(), ct))
                return Results.Conflict(new { error = "Ya existe un grupo con ese nombre." });

            var entity = new Group(request.Name, request.Description, user.DisplayName);
            db.Groups.Add(entity);
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "groups", action: "groups.create", actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId, correlationId: Trace(http),
                reason: $"grupo={entity.Id}"), ct);

            return Results.Created($"/api/v1/groups/{entity.Id}", new { entity.Id, entity.Name });
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal, IUserProvisioner provisioner, AppDbContext db, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null || !user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            var entity = await db.Groups.AsNoTracking()
                .Where(g => g.Id == id)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Description,
                    g.IsActive,
                    Members = (from m in db.GroupMembers.AsNoTracking()
                               join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                               where m.GroupId == g.Id
                               select new { u.Id, u.DisplayName, u.Email }).ToList(),
                    Grants = (from gg in db.GroupGrants.AsNoTracking()
                              join c in db.Clients.AsNoTracking() on gg.ClientId equals c.Id
                              where gg.GroupId == g.Id
                              select new { ClientId = c.Id, ClientName = c.Name, Actions = (int)gg.Actions, gg.ExpiresAt }).ToList()
                })
                .FirstOrDefaultAsync(ct);

            return entity is null ? Results.NotFound() : Results.Ok(entity);
        });

        group.MapPost("/{id:guid}/members", async (
            Guid id, AddMemberRequest request,
            ClaimsPrincipal principal, IUserProvisioner provisioner, AppDbContext db,
            IAuditWriter audit, IHttpContextAccessor http, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null || !user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            if (!await db.Groups.AnyAsync(g => g.Id == id, ct))
                return Results.NotFound();

            if (!await db.Users.AnyAsync(u => u.Id == request.UserId, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["Usuario inexistente."] });

            if (await db.GroupMembers.AnyAsync(m => m.GroupId == id && m.UserId == request.UserId, ct))
                return Results.Ok(new { alreadyMember = true });

            db.GroupMembers.Add(new GroupMember(id, request.UserId, user.DisplayName));
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "groups", action: "groups.add_member", actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId, correlationId: Trace(http),
                reason: $"grupo={id} usuario={request.UserId}"), ct);

            return Results.Ok(new { added = true });
        });

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (
            Guid id, Guid userId,
            ClaimsPrincipal principal, IUserProvisioner provisioner, AppDbContext db,
            IAuditWriter audit, IHttpContextAccessor http, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null || !user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            var member = await db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == userId, ct);
            if (member is null)
                return Results.NotFound();

            db.GroupMembers.Remove(member);
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "groups", action: "groups.remove_member", actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId, correlationId: Trace(http),
                reason: $"grupo={id} usuario={userId}"), ct);

            return Results.NoContent();
        });

        // Asignación masiva: un grupo recibe varios clientes de una vez.
        group.MapPost("/{id:guid}/clients", async (
            Guid id, AddClientsRequest request,
            ClaimsPrincipal principal, IUserProvisioner provisioner, AppDbContext db,
            IAuditWriter audit, IHttpContextAccessor http, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null || !user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            if (!await db.Groups.AnyAsync(g => g.Id == id, ct))
                return Results.NotFound();

            var actions = (GrantActions)request.Actions;
            if (actions == GrantActions.None || request.ClientIds.Count == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clients"] = ["Indica al menos un cliente y una acción (View=1, Edit=2, RevealSecret=4)."]
                });

            var validIds = await db.Clients.Where(c => request.ClientIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct);
            var existing = await db.GroupGrants.Where(g => g.GroupId == id && validIds.Contains(g.ClientId)).ToListAsync(ct);

            foreach (var clientId in validIds)
            {
                var grant = existing.FirstOrDefault(g => g.ClientId == clientId);
                if (grant is null)
                    db.GroupGrants.Add(new GroupGrant(id, clientId, actions, user.DisplayName, request.ExpiresAt));
                else
                    grant.ChangeActions(actions);
            }

            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "groups", action: "groups.grant_clients", actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId, correlationId: Trace(http),
                reason: $"grupo={id} clientes={validIds.Count} acciones={actions}"), ct);

            return Results.Ok(new { updated = validIds.Count, actions = actions.ToString() });
        });

        group.MapDelete("/{id:guid}/clients/{clientId:guid}", async (
            Guid id, Guid clientId,
            ClaimsPrincipal principal, IUserProvisioner provisioner, AppDbContext db,
            IAuditWriter audit, IHttpContextAccessor http, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null || !user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            var grant = await db.GroupGrants.FirstOrDefaultAsync(g => g.GroupId == id && g.ClientId == clientId, ct);
            if (grant is null)
                return Results.NotFound();

            db.GroupGrants.Remove(grant);
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "groups", action: "groups.revoke_client", actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId, correlationId: Trace(http),
                reason: $"grupo={id} cliente={clientId}"), ct);

            return Results.NoContent();
        });

        return app;
    }

    private static string Trace(IHttpContextAccessor http) =>
        http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    public sealed record CreateGroupRequest(string Name, string? Description);
    public sealed record AddMemberRequest(Guid UserId);
    public sealed record AddClientsRequest(List<Guid> ClientIds, int Actions, DateTimeOffset? ExpiresAt);
}
