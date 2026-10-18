namespace Devzone.Application.Auditing;

public sealed record AuditEvent(
    Guid Id,
    Guid? UserId,
    string Username,
    DateTimeOffset OccurredAt,
    string Action,
    string Module,
    string RecordId,
    string? Reason,
    string? RelatedReference,
    string? SessionId);
