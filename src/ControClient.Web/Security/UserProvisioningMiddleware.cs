using ControClient.Domain.Abstractions;
using ControClient.Domain.Users;
using ControClient.Web.Security;

namespace ControClient.Web.Security;

/// <summary>
/// Da de alta al usuario en el primer acceso autenticado, antes de que ninguna
/// página o endpoint lo necesite. En modo cerrado, una cuenta sin pre-registro
/// recibe 403 con un mensaje claro en lugar de crear una ficha vacía.
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
                catch (UnprovisionedUserException ex)
                {
                    logger.LogWarning("Acceso rechazado: {Mensaje}", ex.Message);
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    httpContext.Response.ContentType = "text/plain; charset=utf-8";
                    await httpContext.Response.WriteAsync(ex.Message);
                    return;
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
