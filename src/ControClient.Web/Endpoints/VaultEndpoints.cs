using System.Security.Claims;
using System.Security.Cryptography;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Users;
using ControClient.Domain.Vault;
using ControClient.Infrastructure.Vault;
using ControClient.Web.Security;

namespace ControClient.Web.Endpoints;

public static class VaultEndpoints
{
    public static IEndpointRouteBuilder MapVaultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();

        // ---------------------------------------------------------------- estado
        group.MapGet("/vault/status", async (
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            VaultService vault,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var status = await vault.GetStatusAsync(ct);
            return Results.Ok(new
            {
                initialized = status.Initialized,
                unsealed = status.Unsealed,
                items = status.Items,
                protection = vault.KeyProtectionDescription
            });
        });

        // ---------------------------------------------------------------- constitución
        group.MapPost("/vault/initialize", async (
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            VaultService vault,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            try
            {
                var recoveryCode = await vault.InitializeAsync(ct);

                await audit.WriteAsync(new AuditEvent(
                    entity: "vault",
                    action: "vault.initialize",
                    actorName: user.DisplayName,
                    actorObjectId: user.EntraObjectId,
                    correlationId: Trace(http)), ct);

                // Se devuelve UNA vez; no se registra y no se puede recuperar después.
                return Results.Ok(new
                {
                    initialized = true,
                    keyProtection = vault.KeyProtectionDescription,
                    recoveryCode,
                    warning = "Guarda este valor en un sobre sellado y en un lugar distinto del servidor. No se volverá a mostrar."
                });
            }
            catch (VaultAlreadyInitializedException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization("RequireMfa");

        // ---------------------------------------------------------------- desellado
        group.MapPost("/vault/unseal", async (
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            VaultService vault,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            try
            {
                await vault.UnsealAsync(ct);

                await audit.WriteAsync(new AuditEvent(
                    entity: "vault",
                    action: "vault.unseal",
                    actorName: user.DisplayName,
                    actorObjectId: user.EntraObjectId,
                    correlationId: Trace(http),
                    ipAddress: http.HttpContext?.Connection.RemoteIpAddress?.ToString()), ct);

                return Results.Ok(new { unsealed = true });
            }
            catch (VaultNotInitializedException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (CryptographicException)
            {
                return Results.BadRequest(new { error = "No se pudo abrir la clave protegida en este equipo." });
            }
        }).RequireAuthorization("RequireMfa");

        group.MapPost("/vault/lock", async (
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            VaultService vault,
            IAuditWriter audit,
            IHttpContextAccessor http) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner);
            if (user is null)
                return Results.Unauthorized();

            if (!user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            vault.Lock();

            await audit.WriteAsync(new AuditEvent(
                entity: "vault",
                action: "vault.lock",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: Trace(http)));

            return Results.Ok(new { unsealed = false });
        }).RequireAuthorization("RequireMfa");

        // ---------------------------------------------------------------- recuperación
        group.MapPost("/vault/recover", async (
            RecoverRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            VaultService vault,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!user.IsIn(AppRole.Administrator))
                return Results.Forbid();

            try
            {
                var newRecovery = await vault.RecoverAsync(request.RecoveryCode, ct);

                await audit.WriteAsync(new AuditEvent(
                    entity: "vault",
                    action: "vault.recover",
                    actorName: user.DisplayName,
                    actorObjectId: user.EntraObjectId,
                    correlationId: Trace(http),
                    reason: "reconstitución con sobre de recuperación"), ct);

                return Results.Ok(new
                {
                    recovered = true,
                    recoveryCode = newRecovery,
                    warning = "Se ha generado una clave nueva. Sustituye el sobre anterior y destrúyelo con acta."
                });
            }
            catch (VaultNotInitializedException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (CryptographicException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization("RequireMfa");

        // ---------------------------------------------------------------- credenciales
        group.MapGet("/clients/{clientId:guid}/vault-items", async (
            Guid clientId,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            VaultService vault,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!await access.CanViewAsync(user, clientId, ct))
                return Results.NotFound();

            return Results.Ok(await vault.ListAsync(clientId, ct));
        });

        group.MapPost("/clients/{clientId:guid}/vault-items", async (
            Guid clientId,
            CreateVaultItemRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            VaultService vault,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!await access.CanViewAsync(user, clientId, ct))
                return Results.NotFound();

            if (!await access.CanEditAsync(user, clientId, ct))
                return Results.Forbid();

            if (!Enum.TryParse<VaultItemKind>(request.Kind, ignoreCase: true, out var kind))
                kind = VaultItemKind.Other;

            var (item, version, error) = await vault.CreateItemAsync(
                clientId, request.Name, kind, request.Username, request.Url, request.Notes, request.Secret, user.DisplayName, ct);

            if (item is null)
                return SealedOrBadRequest(error);

            await audit.WriteAsync(new AuditEvent(
                entity: "vault_items",
                action: "vault.create",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: Trace(http),
                clientId: clientId,
                reason: $"item={item.Id} version={version}"), ct);

            return Results.Created($"/api/v1/vault-items/{item.Id}",
                new { item.Id, item.Name, Kind = item.Kind.ToString(), Version = version });
        });

        group.MapPost("/vault-items/{id:guid}/versions", async (
            Guid id,
            NewSecretRequest request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            VaultService vault,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var item = await vault.GetAsync(id, ct);
            if (item is null || !await access.CanViewAsync(user, item.ClientId, ct))
                return Results.NotFound();

            if (!await access.CanEditAsync(user, item.ClientId, ct))
                return Results.Forbid();

            var (version, error) = await vault.AddVersionAsync(id, request.Secret, user.DisplayName, request.Comment, ct);
            if (version == 0)
                return SealedOrBadRequest(error);

            await audit.WriteAsync(new AuditEvent(
                entity: "vault_items",
                action: "vault.new_version",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: Trace(http),
                clientId: item.ClientId,
                reason: $"item={id} version={version}"), ct);

            return Results.Ok(new { version });
        });

        group.MapPost("/vault-items/{id:guid}/reveal", async (
            Guid id,
            ShareReasonRequest? request,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            VaultService vault,
            IAuditWriter audit,
            IHttpContextAccessor http,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var item = await vault.GetAsync(id, ct);
            if (item is null || !await access.CanViewAsync(user, item.ClientId, ct))
                return Results.NotFound();

            // El revelado exige rol técnico o custodio Y concesión explícita (SEC-06).
            if (!await access.CanRevealAsync(user, item.ClientId, ct))
                return Results.Forbid();

            var (secret, version, error) = await vault.RevealAsync(id, ct);

            await audit.WriteAsync(new AuditEvent(
                entity: "vault_items",
                action: "vault.reveal",
                actorName: user.DisplayName,
                actorObjectId: user.EntraObjectId,
                correlationId: Trace(http),
                clientId: item.ClientId,
                result: secret is null ? "denied" : "ok",
                reason: request?.Reason is null ? $"item={id} version={version}" : $"item={id} motivo={request.Reason}",
                ipAddress: http.HttpContext?.Connection.RemoteIpAddress?.ToString()), ct);

            if (secret is null)
                return SealedOrBadRequest(error);

            var response = Results.Json(new
            {
                id,
                version,
                secret,
                revealedAt = DateTimeOffset.UtcNow,
                hideAfterSeconds = 30
            });

            http.HttpContext?.Response.Headers.Append("Cache-Control", "no-store");
            return response;
        });

        return app;
    }

    private static IResult SealedOrBadRequest(string? error)
    {
        if (error is not null && error.Contains("sellada", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error }, statusCode: StatusCodes.Status423Locked);

        return Results.BadRequest(new { error });
    }

    private static string Trace(IHttpContextAccessor http) =>
        http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    public sealed record RecoverRequest(string RecoveryCode);
    public sealed record CreateVaultItemRequest(string Name, string Kind, string? Username, string? Url, string? Notes, string Secret);
    public sealed record NewSecretRequest(string Secret, string? Comment);
    public sealed record ShareReasonRequest(string? Reason);
}
