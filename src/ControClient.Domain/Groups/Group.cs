using ControClient.Domain.Clients;

namespace ControClient.Domain.Groups;

/// <summary>
/// Equipo de trabajo (por ejemplo «Soporte» o «Redes»). Agrupa personas y les
/// concede acceso a clientes completos, para no asignar uno a uno.
/// El acceso efectivo es la unión de concesiones directas y de grupo.
/// </summary>
public sealed class Group
{
    private Group() { }

    public Group(string name, string? description, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del grupo es obligatorio.", nameof(name));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CreatedBy = createdBy;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; private set; } = true;

    public List<GroupMember> Members { get; private set; } = [];
    public List<GroupGrant> Grants { get; private set; } = [];

    public void Rename(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del grupo es obligatorio.", nameof(name));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void Deactivate() => IsActive = false;
}

public sealed class GroupMember
{
    private GroupMember() { }

    public GroupMember(Guid groupId, Guid userId, string addedBy)
    {
        GroupId = groupId;
        UserId = userId;
        AddedBy = addedBy;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string AddedBy { get; private set; } = string.Empty;
}

/// <summary>Concesión de un cliente a un grupo, con las mismas acciones que una individual.</summary>
public sealed class GroupGrant
{
    private GroupGrant() { }

    public GroupGrant(Guid groupId, Guid clientId, GrantActions actions, string grantedBy, DateTimeOffset? expiresAt = null)
    {
        if (actions == GrantActions.None)
            throw new ArgumentException("La concesión debe incluir al menos una acción.", nameof(actions));

        GroupId = groupId;
        ClientId = clientId;
        Actions = actions;
        GrantedBy = grantedBy;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GroupId { get; private set; }
    public Guid ClientId { get; private set; }
    public GrantActions Actions { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string GrantedBy { get; private set; } = string.Empty;

    public bool IsActive(DateTimeOffset now) => ExpiresAt is null || ExpiresAt > now;

    public void ChangeActions(GrantActions actions)
    {
        if (actions == GrantActions.None)
            throw new ArgumentException("La concesión debe incluir al menos una acción.", nameof(actions));

        Actions = actions;
    }
}
