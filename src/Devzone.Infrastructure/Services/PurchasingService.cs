using Devzone.Application.Purchasing;
using Devzone.Infrastructure.Persistence;
using Devzone.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Devzone.Infrastructure.Services;

public sealed class PurchasingService(DevzoneDbContext dbContext) : IPurchasingService
{
    public async Task<IReadOnlyList<PurchaseItem>> GetPurchasesAsync(
        Guid companyId, int limit = 200, CancellationToken cancellationToken = default) =>
        await dbContext.Purchases.AsNoTracking()
            .Where(purchase => purchase.CompanyId == companyId)
            .OrderByDescending(purchase => purchase.ReceivedAt)
            .Take(Math.Clamp(limit, 1, 1000))
            .Select(purchase => new PurchaseItem(purchase.ReceivedAt, purchase.Reference, purchase.SupplierName,
                purchase.Product.Sku, purchase.Product.Name, purchase.Quantity, purchase.UnitCost,
                purchase.TotalCost, purchase.Username))
            .ToListAsync(cancellationToken);

    public async Task ReceiveAsync(Guid companyId, PurchaseDraft purchase, string username,
        CancellationToken cancellationToken = default)
    {
        var supplier = purchase.SupplierName.Trim();
        var reference = purchase.Reference.Trim();
        if (supplier.Length is < 2 or > 200)
            throw new ArgumentException("Supplier name must be between 2 and 200 characters.", nameof(purchase));
        if (reference.Length is < 1 or > 100)
            throw new ArgumentException("Enter a purchase or invoice reference (up to 100 characters).", nameof(purchase));
        if (purchase.Quantity <= 0 || decimal.Round(purchase.Quantity, 3) != purchase.Quantity)
            throw new ArgumentException("Quantity must be positive, with up to three decimal places.", nameof(purchase));
        if (purchase.UnitCost < 0 || decimal.Round(purchase.UnitCost, 4) != purchase.UnitCost)
            throw new ArgumentException("Unit cost must be zero or greater, with up to four decimal places.", nameof(purchase));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var product = await dbContext.Products.SingleOrDefaultAsync(item =>
            item.Id == purchase.ProductId && item.CompanyId == companyId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("The active product could not be found.");
        var movementChanges = await dbContext.StockMovements.AsNoTracking()
            .Where(movement => movement.CompanyId == companyId && movement.ProductId == product.Id)
            .Select(movement => movement.QuantityChange)
            .ToListAsync(cancellationToken);
        var onHand = movementChanges.Sum();
        var currentValue = onHand * product.CostPrice;
        var newBalance = onHand + purchase.Quantity;
        var unitCost = decimal.Round(purchase.UnitCost, 4, MidpointRounding.AwayFromZero);
        var averageCost = newBalance > 0
            ? decimal.Round((currentValue + purchase.Quantity * unitCost) / newBalance, 4, MidpointRounding.AwayFromZero)
            : unitCost;
        var totalCost = decimal.Round(purchase.Quantity * unitCost, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow;
        var purchaseId = Guid.NewGuid();
        dbContext.Purchases.Add(new PurchaseEntity
        {
            Id = purchaseId, CompanyId = companyId, ProductId = product.Id, ReceivedAt = now,
            Reference = reference, SupplierName = supplier, Quantity = purchase.Quantity,
            UnitCost = unitCost, TotalCost = totalCost, Username = username
        });
        dbContext.StockMovements.Add(new StockMovementEntity
        {
            Id = Guid.NewGuid(), CompanyId = companyId, ProductId = product.Id,
            QuantityChange = purchase.Quantity, BalanceAfter = newBalance, UnitCost = unitCost,
            Reason = $"Purchase {reference} from {supplier}", Username = username, OccurredAt = now
        });
        product.CostPrice = averageCost;
        product.UpdatedAt = now;
        dbContext.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(), Username = username, OccurredAt = now,
            Action = "PurchaseReceived", Module = "Purchases", RecordId = purchaseId.ToString("D"),
            Reason = $"{reference}: {purchase.Quantity:0.###} × {unitCost:0.####} {product.Sku} from {supplier}"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
