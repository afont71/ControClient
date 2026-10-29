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

