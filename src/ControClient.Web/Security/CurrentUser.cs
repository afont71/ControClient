using System.Security.Claims;
using ControClient.Domain.Users;

namespace ControClient.Web.Security;

/// <summary>Vista mínima del usuario autenticado para endpoints y componentes.</summary>
public sealed record CurrentUser(string ObjectId, string Name, AppRole Role, bool MfaSatisfied)
{
    public static CurrentUser? FromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        // El identificador estable es el claim objectidentifier (oid, GUID). Con
        // MapInboundClaims=false el nombre largo es el que llega; "sub" (nameidentifier)
        // es un identificador por aplicación y no debe usarse si hay oid.
        var objectId =
            principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? principal.FindFirst("oid")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(objectId))
            return null;

        var name = principal.FindFirst(ClaimTypes.Name)?.Value
                   ?? principal.FindFirst("name")?.Value
                   ?? objectId;

        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value
                        ?? principal.FindFirst("roles")?.Value;

        var role = Enum.TryParse<AppRole>(roleClaim, ignoreCase: true, out var parsed)
            ? parsed
            : AppRole.ReadOnly;

        var mfa = principal.FindAll("amr").Any(c => c.Value.Contains("mfa", StringComparison.OrdinalIgnoreCase))
                  || principal.FindAll("acrs").Any(c => c.Value.Contains("mfa", StringComparison.OrdinalIgnoreCase));

        return new CurrentUser(objectId, name, role, mfa);
    }

    public bool IsIn(params AppRole[] roles) => Array.IndexOf(roles, Role) >= 0;
}

