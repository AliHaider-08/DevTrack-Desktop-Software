namespace Devzone.Infrastructure.Persistence.Entities;

public sealed class AuditEventEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? RelatedReference { get; set; }
    public string? SessionId { get; set; }
}
