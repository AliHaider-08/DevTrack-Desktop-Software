namespace Devzone.Application.Purchasing;

public sealed record PurchaseItem(
    DateTimeOffset ReceivedAt, string Reference, string SupplierName, string ProductSku,
    string ProductName, decimal Quantity, decimal UnitCost, decimal TotalCost, string Username);

public sealed record PurchaseDraft(
    Guid ProductId, string Reference, string SupplierName, decimal Quantity, decimal UnitCost);
