using ControClient.Domain.Abstractions;
using ControClient.Domain.Users;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControClient.Web;

/// <summary>Estado de navegación: qué enlaces puede ver el usuario según su rol.</summary>
public sealed class NavService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public async Task<bool> IsAdminAsync()
    {
        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        if (current is null)
            return false;

        using var scope = scopes.CreateScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<IUserProvisioner>();
        var user = await provisioner.GetOrCreateAsync(current.ObjectId, current.Name, current.Email);
        return user.IsActive && user.IsIn(AppRole.Administrator);
    }
}
