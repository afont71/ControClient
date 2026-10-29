using ControClient.Domain.Auditing;

namespace ControClient.Domain.Abstractions;

/// <summary>Escritura de auditoría funcional, independiente del log técnico (OBS-02).</summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

