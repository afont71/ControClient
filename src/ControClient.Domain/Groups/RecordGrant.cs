using ControClient.Domain.Clients;

namespace ControClient.Domain.Groups;

/// <summary>
/// Permiso de un grupo sobre una ficha concreta. Permite que un cliente sea de
/// solo lectura pero un dato concreto (una licencia, un servidor) sea editable.
/// Se suma a la concesión de cliente: nunca la reduce.
/// </summary>
public sealed class RecordGrant
{
    private RecordGrant() { }

    public RecordGrant(Guid groupId, Guid recordId, GrantActions actions, string grantedBy)
    {
        if (actions == GrantActions.None)
            throw new ArgumentException("La concesión debe incluir al menos una acción.", nameof(actions));

        GroupId = groupId;
        RecordId = recordId;
        Actions = actions;
        GrantedBy = grantedBy;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GroupId { get; private set; }
    public Guid RecordId { get; private set; }
    public GrantActions Actions { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string GrantedBy { get; private set; } = string.Empty;

    public void ChangeActions(GrantActions actions)
    {
        if (actions == GrantActions.None)
            throw new ArgumentException("La concesión debe incluir al menos una acción.", nameof(actions));

        Actions = actions;
    }
}
