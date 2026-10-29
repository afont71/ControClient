using ControClient.Domain.Users;

namespace ControClient.Domain.Abstractions;

/// <summary>
/// Alta automática del usuario en el primer acceso: la identidad vive en Entra,
/// aquí solo se materializa el rol y el estado (SEC-01).
/// </summary>
public interface IUserProvisioner
{
    Task<AppUser> GetOrCreateAsync(string objectId, string displayName, string? email, CancellationToken ct = default);
}
