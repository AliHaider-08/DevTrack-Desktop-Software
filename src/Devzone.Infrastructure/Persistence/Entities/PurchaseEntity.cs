namespace Devzone.Infrastructure.Persistence.Entities;

public sealed class PurchaseEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public ProductEntity Product { get; set; } = null!;
    public DateTimeOffset ReceivedAt { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string Username { get; set; } = string.Empty;
}
