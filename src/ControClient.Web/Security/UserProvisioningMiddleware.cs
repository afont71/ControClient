using ControClient.Domain.Abstractions;
using ControClient.Web.Security;

namespace ControClient.Web.Security;

/// <summary>
/// Da de alta al usuario en el primer acceso autenticado, antes de que ninguna
/// página o endpoint lo necesite. Si el alta falla, la petición sigue: el error
/// se registrará al usar la aplicación y no se convierte en un 500 de login.
/// </summary>
public sealed class UserProvisioningMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IUserProvisioner provisioner, ILogger<UserProvisioningMiddleware> logger)
    {
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var current = CurrentUser.FromPrincipal(httpContext.User);
            if (current is not null)
            {
                try
                {
                    await provisioner.GetOrCreateAsync(current.ObjectId, current.Name, current.Email);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "No se pudo provisionar el usuario {ObjectId}", current.ObjectId);
                }
            }
        }

        await next(httpContext);
    }
}
