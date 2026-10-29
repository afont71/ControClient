using ControClient.Domain.Abstractions;
using ControClient.Infrastructure.Search;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControClient.Web.Pages;

/// <summary>Búsqueda global para la interfaz, ya filtrada por el alcance del usuario.</summary>
public sealed class SearchPageService(IServiceScopeFactory scopes, AuthenticationStateProvider auth)
{
    public sealed record HitRow(string Type, Guid Id, Guid ClientId, string ClientName, string Title, string? Subtitle);

    public async Task<IReadOnlyList<HitRow>> SearchAsync(string term)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;

        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        if (current is null)
            return [];

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, null);
        if (!user.IsActive)
            return [];

        var access = sp.GetRequiredService<IClientAccessService>();
        var visible = await access.GetVisibleClientIdsAsync(user);

        var search = sp.GetRequiredService<SearchService>();
        var hits = await search.SearchAsync(visible, term, 40);

        return hits.Select(h => new HitRow(h.Type, h.Id, h.ClientId, h.ClientName, h.Title, h.Subtitle)).ToList();
    }
}
