using ControClient.Domain.Abstractions;
using ControClient.Domain.Users;

namespace ControClient.Web.Security;

/// <summary>
/// Con la bóveda sellada no se sirve ninguna página ni API salvo el propio desellado.
/// El administrador va a /boveda; el resto recibe un aviso. Las rutas de infraestructura
/// (Blazor, estáticos, login) se dejan pasar para que la aplicación pueda funcionar.
/// </summary>
public sealed class VaultLockMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedPrefixes =
    [
        "/boveda",
        "/logged-out",
        "/logout",
        "/health",
        "/signin-oidc",
        "/signout-callback-oidc",
        "/_blazor",
        "/_framework",
        "/_content",
        "/app.css",
        "/favicon",
        "/api/v1/vault/status",
        "/api/v1/vault/unseal",
        "/api/v1/vault/initialize",
        "/api/v1/vault/recover"
    ];

    public async Task InvokeAsync(
        HttpContext context,
        VaultLockService lockService,
        IUserProvisioner provisioner,
        ILogger<VaultLockMiddleware> logger)
    {
        if (!await lockService.IsLockedAsync(context.RequestAborted))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "/";
        if (AllowedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        // Los estáticos llevan huella en el nombre (app.ka4oxk4uzr.css): se dejan
        // pasar por extensión para que la página de desellado se vea bien.
        var extension = Path.GetExtension(path);
        if (extension is ".css" or ".js" or ".map" or ".png" or ".svg" or ".ico" or ".woff" or ".woff2")
        {
            await next(context);
            return;
        }

        var current = CurrentUser.FromPrincipal(context.User);
        var isAdmin = false;

        if (current is not null)
        {
            try
            {
                var user = await provisioner.GetOrCreateAsync(current.ObjectId, current.Name, current.Email, context.RequestAborted);
                isAdmin = user.IsActive && user.IsIn(AppRole.Administrator);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo resolver el usuario en el bloqueo de bóveda.");
            }
        }

        // Sin sesión no se puede saber si es administrador: se le manda a la
        // pantalla de desellado, que a su vez lanza el inicio de sesión.
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect("/boveda");
            return;
        }

        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status423Locked;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "La bóveda está sellada. Un administrador debe desellarla para continuar.",
                locked = true
            });
            return;
        }

        if (isAdmin)
        {
            context.Response.Redirect("/boveda");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync("""
            <!DOCTYPE html>
            <html lang="es"><head><meta charset="utf-8" /><title>Bóveda sellada</title>
            <style>body{font-family:Segoe UI,system-ui,sans-serif;background:#F4F6FA;color:#0F172A;display:grid;place-items:center;height:100vh;margin:0}
            .box{background:#fff;border:1px solid #E2E8F0;border-radius:12px;padding:2rem;max-width:32rem;text-align:center}
            h1{font-size:1.3rem;margin:0 0 .5rem}p{color:#64748B}</style></head>
            <body><div class="box"><h1>Bóveda sellada</h1>
            <p>La aplicación está cerrada hasta que un administrador deselle la bóveda.</p></div></body></html>
            """);
    }
}
