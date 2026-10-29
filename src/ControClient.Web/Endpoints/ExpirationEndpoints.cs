using System.Security.Claims;
using ControClient.Domain.Abstractions;
using ControClient.Infrastructure.Expirations;
using ControClient.Web.Security;

namespace ControClient.Web.Endpoints;

public static class ExpirationEndpoints
{
    public static IEndpointRouteBuilder MapExpirationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/expirations", async (
            int days,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            ExpirationsService expirations,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            var visible = await access.GetVisibleClientIdsAsync(user, ct);
            var rows = await expirations.GetAsync(visible, days <= 0 ? 90 : days, ct);

            return Results.Ok(rows.Select(r => new
            {
                r.RecordId,
                r.ClientId,
                r.ClientName,
                r.TemplateName,
                r.RecordTitle,
                r.FieldLabel,
                Date = r.Date.ToString("yyyy-MM-dd"),
                r.DaysLeft,
                r.WarningDays,
                status = r.DaysLeft < 0 ? "vencido" : r.DaysLeft <= r.WarningDays ? "avisar" : "futuro"
            }));
        }).RequireAuthorization().WithTags("Vencimientos");

        return app;
    }
}
