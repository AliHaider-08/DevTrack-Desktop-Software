namespace Devzone.Application.Auditing;

public interface IAuditTrail
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
