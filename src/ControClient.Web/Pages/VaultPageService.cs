using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Domain.Users;
using ControClient.Domain.Vault;
using ControClient.Infrastructure.Vault;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControClient.Web.Pages;

/// <summary>Bóveda para las pantallas: estado, alta de credenciales y revelado.</summary>
public sealed class VaultPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record Status(bool Initialized, bool Unsealed, int Items, string Protection);
    public sealed record ItemRow(Guid Id, string Name, string Kind, string? Username, string? Url, int CurrentVersion, DateTimeOffset UpdatedAt);

    public async Task<Status> StatusAsync()
    {
        using var scope = scopes.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<VaultService>();
        var (initialized, unsealed, items) = await vault.GetStatusAsync();
        return new Status(initialized, unsealed, items, vault.KeyProtectionDescription);
    }

    public async Task<(string? RecoveryCode, string? Error)> InitializeAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, vault) = await ResolveAsync(sp);
        if (user is null || !user.IsIn(AppRole.Administrator))
            return (null, "Solo un administrador puede constituir la bóveda.");

        try
        {
            var code = await vault.InitializeAsync();
            await AuditAsync(sp, user, "vault.initialize", null);
            return (code, null);
        }
        catch (Exception ex) when (ex is VaultAlreadyInitializedException or VaultNotInitializedException)
        {
            return (null, ex.Message);
        }
    }

    public async Task<(bool Ok, string? Error)> UnsealAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, vault) = await ResolveAsync(sp);
        if (user is null || !user.IsIn(AppRole.Administrator))
            return (false, "Solo un administrador puede desellar la bóveda.");

        try
        {
            await vault.UnsealAsync();
            await AuditAsync(sp, user, "vault.unseal", null);
            return (true, null);
        }
        catch (Exception ex) when (ex is VaultNotInitializedException or System.Security.Cryptography.CryptographicException)
        {
            return (false, ex.Message);
        }
    }

    public async Task LockAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, vault) = await ResolveAsync(sp);
        if (user is null || !user.IsIn(AppRole.Administrator))
            return;

        vault.Lock();
        await AuditAsync(sp, user, "vault.lock", null);
    }

    public async Task<(IReadOnlyList<ItemRow> Items, bool CanReveal)> ListAsync(Guid clientId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, vault) = await ResolveAccessAsync(sp);
        if (user is null || !await access.CanViewAsync(user, clientId))
            return ([], false);

        var items = await vault.ListAsync(clientId);
        var canReveal = await access.CanRevealAsync(user, clientId);

        return (items.Select(i => new ItemRow(
            i.Id, i.Name, i.Kind, i.Username, i.Url, i.CurrentVersion, i.UpdatedAt)).ToList(), canReveal);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(
        Guid clientId, string name, string kind, string? username, string? url, string? notes, string secret)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, vault) = await ResolveAccessAsync(sp);
        if (user is null)
            return (false, "Sesión no válida.");

        if (!await access.CanEditAsync(user, clientId))
            return (false, "No tienes permiso para añadir credenciales a este cliente.");

        if (!Enum.TryParse<VaultItemKind>(kind, ignoreCase: true, out var parsed))
            parsed = VaultItemKind.Other;

        var (item, version, error) = await vault.CreateItemAsync(
            clientId, name, parsed, username, url, notes, secret, user.DisplayName);

        if (item is null)
            return (false, error ?? "No se pudo crear la credencial.");

        await AuditAsync(sp, user, "vault.create", clientId, $"item={item.Id} version={version}");
        return (true, null);
    }

    public async Task<(string? Secret, string? Error)> RevealAsync(Guid itemId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var (user, access, vault) = await ResolveAccessAsync(sp);
        if (user is null)
            return (null, "Sesión no válida.");

        var item = await vault.GetAsync(itemId);
        if (item is null || !await access.CanViewAsync(user, item.ClientId))
            return (null, "La credencial no existe.");

        if (!await access.CanRevealAsync(user, item.ClientId))
            return (null, "No tienes permiso para revelar secretos de este cliente.");

        var (secret, version, error) = await vault.RevealAsync(itemId);
        await AuditAsync(sp, user, "vault.reveal", item.ClientId,
            secret is null ? $"item={itemId} resultado=denegado" : $"item={itemId} version={version}");

        return (secret, error);
    }

    private async Task<(AppUser? User, VaultService Vault)> ResolveAsync(IServiceProvider sp)
    {
        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        var vault = sp.GetRequiredService<VaultService>();
        if (current is null)
            return (null, vault);

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, null);
        return (user.IsActive ? user : null, vault);
    }

    private async Task<(AppUser? User, IClientAccessService Access, VaultService Vault)> ResolveAccessAsync(IServiceProvider sp)
    {
        var (user, vault) = await ResolveAsync(sp);
        return (user, sp.GetRequiredService<IClientAccessService>(), vault);
    }

    private static async Task AuditAsync(IServiceProvider sp, AppUser user, string action, Guid? clientId, string? reason = null)
    {
        var audit = sp.GetRequiredService<IAuditWriter>();
        await audit.WriteAsync(new AuditEvent(
            entity: "vault",
            action: action,
            actorName: user.DisplayName,
            actorObjectId: user.EntraObjectId,
            correlationId: Guid.NewGuid().ToString("N"),
            clientId: clientId,
            reason: reason));
    }
}
