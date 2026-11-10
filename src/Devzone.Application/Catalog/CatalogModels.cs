namespace Devzone.Application.Catalog;

public sealed record CategoryItem(Guid Id, string Name, string? Description, int ProductCount);

public sealed record ProductItem(
    Guid Id, string Sku, string? Barcode, string Name, string? CategoryName,
    decimal CostPrice, decimal SellingPrice, decimal MinimumStock, decimal OnHand, bool IsActive,
    string? BatchNumber = null, DateOnly? ExpiryDate = null, string? Strength = null, string? DosageForm = null);

public sealed record ProductDraft(
    Guid? Id, string Sku, string? Barcode, string Name, Guid? CategoryId,
    decimal CostPrice, decimal SellingPrice, decimal MinimumStock,
    string? BatchNumber = null, DateOnly? ExpiryDate = null, string? Strength = null, string? DosageForm = null);

public sealed record StockMovementItem(
    DateTimeOffset OccurredAt, string ProductSku, string ProductName, decimal QuantityChange,
    decimal BalanceAfter, string Reason, string Username);

public sealed record DashboardSummary(int ProductCount, int LowStockCount, int OutOfStockCount, decimal InventoryCostValue);
