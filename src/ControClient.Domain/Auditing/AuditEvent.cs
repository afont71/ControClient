namespace ControClient.Domain.Auditing;

/// <summary>
/// Evento de auditoría funcional (SEC-09). No contiene valores secretos: solo
/// metadatos, actor, resultado y correlación (OBS-02).
/// </summary>
public sealed class AuditEvent
{
    public long Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;
    public string? ActorObjectId { get; private set; }
    public string ActorName { get; private set; } = "sistema";
    public Guid? ClientId { get; private set; }
    public string Entity { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Result { get; private set; } = "ok";
    public string? Reason { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }

    private AuditEvent() { }

    public AuditEvent(
        string entity,
        string action,
        string actorName,
        string? actorObjectId,
        string correlationId,
        string result = "ok",
        Guid? clientId = null,
        string? reason = null,
        string? ipAddress = null)
    {
        Entity = entity;
        Action = action;
        ActorName = actorName;
        ActorObjectId = actorObjectId;
        CorrelationId = correlationId;
        Result = result;
        ClientId = clientId;
        Reason = reason is null ? null : Redactor.Redact(reason);
        IpAddress = ipAddress;
    }
}

