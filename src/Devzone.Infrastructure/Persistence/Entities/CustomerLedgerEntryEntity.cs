namespace Devzone.Infrastructure.Persistence.Entities;

public sealed class CustomerLedgerEntryEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
    public DateTimeOffset OccurredAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal BalanceChange { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
