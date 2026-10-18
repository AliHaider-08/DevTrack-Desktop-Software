namespace Devzone.Infrastructure.Persistence.Entities;

public sealed class StockMovementEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public ProductEntity Product { get; set; } = null!;
    public decimal QuantityChange { get; set; }
    public decimal BalanceAfter { get; set; }
    public decimal UnitCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}
