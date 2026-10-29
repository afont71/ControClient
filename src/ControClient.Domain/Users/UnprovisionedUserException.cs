namespace ControClient.Domain.Users;

/// <summary>
/// La cuenta autenticada no está autorizada a usar la aplicación: no existe ficha
/// y el modo estricto exige pre-registro por un administrador.
/// </summary>
public sealed class UnprovisionedUserException(string email)
    : Exception($"La cuenta {email} no está autorizada. Pide a un administrador que te pre-registre.");
