using ControClient.Domain.Clients;
using ControClient.Domain.Users;

namespace ControClient.Domain.Abstractions;

/// <summary>
/// Resuelve qué clientes ve y puede tocar un usuario, combinando su rol y sus
/// concesiones vigentes. Se evalúa en servidor antes de responder (AC-05).
/// </summary>
public interface IClientAccessService
{
    /// <summary>Devuelve todos los identificadores visibles; <c>null</c> significa "todos" (rol global).</summary>
    Task<IReadOnlyCollection<Guid>?> GetVisibleClientIdsAsync(AppUser user, CancellationToken ct = default);

    Task<bool> CanViewAsync(AppUser user, Guid clientId, CancellationToken ct = default);

    Task<bool> CanEditAsync(AppUser user, Guid clientId, CancellationToken ct = default);

    Task<bool> CanRevealAsync(AppUser user, Guid clientId, CancellationToken ct = default);

    /// <summary>
    /// Edición de una ficha concreta: la del cliente o, si el cliente es de solo
    /// lectura, la concedida al grupo sobre esa ficha.
    /// </summary>
    Task<bool> CanEditRecordAsync(AppUser user, Guid clientId, Guid recordId, CancellationToken ct = default);

    /// <summary>Ver una ficha concreta: la concesión de la ficha sustituye a la del cliente.</summary>
    Task<bool> CanViewRecordAsync(AppUser user, Guid clientId, Guid recordId, CancellationToken ct = default);

    /// <summary>Revelar un campo secreto de una ficha: la excepción de la ficha manda.</summary>
    Task<bool> CanRevealRecordAsync(AppUser user, Guid clientId, Guid recordId, CancellationToken ct = default);
}
