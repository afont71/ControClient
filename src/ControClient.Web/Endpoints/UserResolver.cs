using System.Security.Claims;
using ControClient.Domain.Abstractions;
using ControClient.Domain.Users;
using ControClient.Web.Security;

namespace ControClient.Web.Endpoints;

/// <summary>Resuelve la identidad de la petición al usuario de la aplicación (alta en el primer acceso).</summary>
public static class UserResolver
{
    public static async Task<AppUser?> ResolveAsync(
        ClaimsPrincipal principal,
        IUserProvisioner provisioner,
        CancellationToken ct = default)
    {
        var current = CurrentUser.FromPrincipal(principal);
        if (current is null)
            return null;

        var user = await provisioner.GetOrCreateAsync(current.ObjectId, current.Name, current.Email, ct);
        return user.IsActive ? user : null;
    }
}
