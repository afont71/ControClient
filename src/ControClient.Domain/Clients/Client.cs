namespace ControClient.Domain.Clients;

/// <summary>
/// Cliente canónico. La carpeta de origen es otra entidad: varias carpetas pueden
/// asociarse a un mismo cliente (v1.0 §4.4 y BASE-11).
/// </summary>
public sealed class Client
{
    private Client() { }

    public Client(string name, string? alias = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(name));

        Name = name.Trim();
        Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Alias { get; private set; }

    /// <summary>
    /// Estado comercial validado. Nunca se deriva de la actividad documental (BASE-12).
    /// </summary>
    public ClientStatus Status { get; private set; } = ClientStatus.PendingValidation;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Datos de empresa, dirección y contacto.
    public string? TaxId { get; private set; }
    public string? Address { get; private set; }
    public string? PostalCode { get; private set; }
    public string? City { get; private set; }
    public string? Province { get; private set; }
    public string? Country { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactRole { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? ContactMobile { get; private set; }
    public string? Website { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Aplica los datos de empresa, dirección y contacto.</summary>
    public void UpdateDetails(ClientDetails details)
    {
        Alias = Clean(details.Alias);
        TaxId = Clean(details.TaxId);
        Address = Clean(details.Address);
        PostalCode = Clean(details.PostalCode);
        City = Clean(details.City);
        Province = Clean(details.Province);
        Country = Clean(details.Country);
        ContactName = Clean(details.ContactName);
        ContactRole = Clean(details.ContactRole);
        ContactEmail = Clean(details.ContactEmail);
        ContactPhone = Clean(details.ContactPhone);
        ContactMobile = Clean(details.ContactMobile);
        Website = Clean(details.Website);
        Notes = Clean(details.Notes);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(name));

        Name = name.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(ClientStatus status)
    {
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum ClientStatus
{
    PendingValidation = 0,
    Active = 1,
    Inactive = 2,
    Archived = 3
}

