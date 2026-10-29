using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ControClient.Web.Security;

/// <summary>
/// Autenticación de desarrollo: simula un usuario ya autenticado con MFA para poder
/// trabajar sin tenant. Solo se registra si Authentication:Mode = Development y el
/// entorno es Development; cualquier otro caso usa Entra ID.
/// </summary>
public sealed class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dev";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var objectId = configuration["Authentication:Dev:ObjectId"] ?? "dev-admin-0001";
        var name = configuration["Authentication:Dev:Name"] ?? "Administrador de desarrollo";
        var role = configuration["Authentication:Dev:Role"] ?? "Administrator";

        var claims = new List<Claim>
        {
            new("oid", objectId),
            new(ClaimTypes.NameIdentifier, objectId),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new("amr", "mfa"),
            new("http://schemas.microsoft.com/identity/claims/objectidentifier", objectId)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

