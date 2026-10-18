using Devzone.Application.Auditing;
using Devzone.Infrastructure.Persistence;
using Devzone.Infrastructure.Persistence.Entities;

namespace Devzone.Infrastructure.Services;

public sealed class AuditTrail(DevzoneDbContext dbContext) : IAuditTrail
{
    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        if (auditEvent.Id == Guid.Empty || string.IsNullOrWhiteSpace(auditEvent.Username) ||
            string.IsNullOrWhiteSpace(auditEvent.Action) || string.IsNullOrWhiteSpace(auditEvent.Module) ||
            string.IsNullOrWhiteSpace(auditEvent.RecordId))
            throw new ArgumentException("Audit ID, actor, action, module, and record are required.", nameof(auditEvent));
        dbContext.AuditEvents.Add(new AuditEventEntity
        {
            Id = auditEvent.Id,
            UserId = auditEvent.UserId,
            Username = auditEvent.Username,
            OccurredAt = auditEvent.OccurredAt,
            Action = auditEvent.Action,
            Module = auditEvent.Module,
            RecordId = auditEvent.RecordId,
            Reason = auditEvent.Reason,
            RelatedReference = auditEvent.RelatedReference,
            SessionId = auditEvent.SessionId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
