using ControClient.Domain.Abstractions;
using ControClient.Domain.Users;
using ControClient.Infrastructure.Expirations;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControClient.Web.Pages;

/// <summary>Vencimientos visibles para el usuario: licencias, garantías, renovaciones.</summary>
public sealed class ExpirationsPageService(
    IServiceScopeFactory scopes,
    AuthenticationStateProvider auth,
    ExpirationsService expirations)
{
    public sealed record Row(Guid RecordId, string ClientName, string TemplateName, string RecordTitle, string FieldLabel, DateOnly Date, int DaysLeft);

    public async Task<IReadOnlyList<Row>?> ListAsync(int horizonDays)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;

        var state = await auth.GetAuthenticationStateAsync();
        var current = CurrentUser.FromPrincipal(state.User);
        if (current is null)
            return null;

        var prov = sp.GetRequiredService<IUserProvisioner>();
        var user = await prov.GetOrCreateAsync(current.ObjectId, current.Name, current.Email);
        if (!user.IsActive)
            return null;

        var access = sp.GetRequiredService<IClientAccessService>();
        var visible = await access.GetVisibleClientIdsAsync(user);

        var items = await expirations.GetAsync(visible, horizonDays);

        return items.Select(i => new Row(
            i.RecordId, i.ClientName, i.TemplateName, i.RecordTitle,
            i.FieldLabel, i.Date, i.DaysLeft)).ToList();
    }
}
