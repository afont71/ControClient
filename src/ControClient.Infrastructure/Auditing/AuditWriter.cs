using ControClient.Domain.Abstractions;
using ControClient.Domain.Auditing;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControClient.Infrastructure.Auditing;

/// <summary>
/// Persiste auditoría funcional. Un fallo de auditoría no debe tumbar la operación
/// de negocio ya confirmada, pero sí quedar registrado en el log técnico.
/// </summary>
public sealed class AuditWriter(AppDbContext db, ILogger<AuditWriter> logger) : IAuditWriter
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            db.AuditEvents.Add(auditEvent);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // SEC-09: la auditoría es obligatoria; si falla, se degrada de forma visible.
            logger.LogError(ex, "No se pudo persistir el evento de auditoría {Entity}/{Action}",
                auditEvent.Entity, auditEvent.Action);
            throw;
        }
    }
}

