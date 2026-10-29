namespace ControClient.Web.Security;

/// <summary>
/// Defensa en profundidad: aunque la validación del token ya comprueba el emisor,
/// se rechaza explícitamente cualquier identidad de otro tenant. La aplicación es
/// de uso interno: solo empleados de la organización.
/// </summary>
public sealed class TenantGuardMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var mode = configuration["Authentication:Mode"];
            var expected = configuration["AzureAd:TenantId"];

            var entraMode = !string.Equals(mode, "Development", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(expected);

            if (entraMode)
            {
                var tenantId =
                    httpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
                    ?? httpContext.User.FindFirst("tid")?.Value;

                if (!string.Equals(tenantId, expected, StringComparison.OrdinalIgnoreCase))
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await httpContext.Response.WriteAsync("Identidad de otro tenant: acceso denegado.");
                    return;
                }
            }
        }

        await next(httpContext);
    }
}
