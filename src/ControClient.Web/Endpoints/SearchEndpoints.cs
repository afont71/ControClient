using System.Security.Claims;
using ControClient.Domain.Abstractions;
using ControClient.Infrastructure.Search;
using ControClient.Web.Security;

namespace ControClient.Web.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/search", async (
            string? q,
            int limit,
            ClaimsPrincipal principal,
            IUserProvisioner provisioner,
            IClientAccessService access,
            SearchService search,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(principal, provisioner, ct);
            if (user is null)
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Results.Ok(Array.Empty<SearchService.Hit>());

            // El alcance se calcula primero: nunca se busca fuera de lo autorizado.
            var visible = await access.GetVisibleClientIdsAsync(user, ct);
            var results = await search.SearchAsync(visible, q, limit <= 0 ? 40 : limit, ct);

            return Results.Ok(results);
        }).RequireAuthorization().WithTags("Búsqueda");

        return app;
    }
}
