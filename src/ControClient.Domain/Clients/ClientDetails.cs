namespace ControClient.Domain.Clients;

/// <summary>Datos de empresa, dirección y contacto de un cliente.</summary>
public sealed record ClientDetails(
    string? Alias = null,
    string? TaxId = null,
    string? Address = null,
    string? PostalCode = null,
    string? City = null,
    string? Province = null,
    string? Country = null,
    string? ContactName = null,
    string? ContactRole = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? ContactMobile = null,
    string? Website = null,
    string? Notes = null)
{
    public static ClientDetails Empty { get; } = new();
}
