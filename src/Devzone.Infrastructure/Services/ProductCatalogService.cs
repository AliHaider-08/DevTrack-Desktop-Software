using Devzone.Application.Auditing;
using Devzone.Application.Catalog;
using Devzone.Infrastructure.Persistence;
using Devzone.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Devzone.Infrastructure.Services;

public sealed class ProductCatalogService(DevzoneDbContext dbContext) : IProductCatalogService
{
    public async Task<IReadOnlyList<ProductItem>> SearchProductsAsync(Guid companyId, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Products.AsNoTracking().Where(product => product.CompanyId == companyId && product.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(product => product.Name.Contains(term) || product.Sku.Contains(term) ||
                (product.Barcode != null && product.Barcode.Contains(term)));
        }

        var products = await query.Include(product => product.Category).OrderBy(product => product.Name).ToListAsync(cancellationToken);
        var productIds = products.Select(product => product.Id).ToArray();
        var movements = await dbContext.StockMovements.AsNoTracking()
            .Where(movement => movement.CompanyId == companyId && productIds.Contains(movement.ProductId))
            .Select(movement => new { movement.ProductId, movement.QuantityChange })
            .ToListAsync(cancellationToken);
        var balances = movements.GroupBy(movement => movement.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(movement => movement.QuantityChange));

        return products.Select(product => new ProductItem(
            product.Id, product.Sku, product.Barcode, product.Name, product.Category?.Name,
            product.CostPrice, product.SellingPrice, product.MinimumStock,
            balances.GetValueOrDefault(product.Id), product.IsActive)).ToArray();
    }

    public async Task<IReadOnlyList<CategoryItem>> GetCategoriesAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Categories.AsNoTracking().Where(category => category.CompanyId == companyId)
            .OrderBy(category => category.Name)
            .Select(category => new CategoryItem(category.Id, category.Name, category.Description, category.Products.Count))
            .ToListAsync(cancellationToken);

    public async Task SaveCategoryAsync(Guid companyId, string name, string? description, string username, CancellationToken cancellationToken = default)
    {
        var cleanedName = name.Trim();
        if (cleanedName.Length is < 2 or > 100)
            throw new ArgumentException("Category name must be between 2 and 100 characters.", nameof(name));
        if (description?.Trim().Length > 500)
            throw new ArgumentException("Category description cannot exceed 500 characters.", nameof(description));
        var normalizedName = cleanedName.ToUpperInvariant();
        if (await dbContext.Categories.AnyAsync(category => category.CompanyId == companyId && category.NormalizedName == normalizedName, cancellationToken))
            throw new InvalidOperationException("A category with that name already exists.");

        var category = new CategoryEntity
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Name = cleanedName,
            NormalizedName = normalizedName, Description = Clean(description), CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Categories.Add(category);
        dbContext.AuditEvents.Add(CreateAudit(username, "CategoryCreated", "Categories", category.Id, "Category created"));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveProductAsync(Guid companyId, ProductDraft draft, string username, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var name = draft.Name.Trim();
        var sku = draft.Sku.Trim();
        if (name.Length is < 2 or > 200 || sku.Length is < 1 or > 64)
            throw new ArgumentException("Product name and SKU are required and must fit their field limits.");
        if (draft.CostPrice < 0 || draft.SellingPrice < 0 || draft.MinimumStock < 0)
            throw new ArgumentException("Prices and minimum stock cannot be negative.");
        var normalizedSku = sku.ToUpperInvariant();
        var barcode = Clean(draft.Barcode);
        if (await dbContext.Products.AnyAsync(product => product.CompanyId == companyId && product.NormalizedSku == normalizedSku && product.Id != draft.Id, cancellationToken))
            throw new InvalidOperationException("That SKU is already in use.");
        if (barcode is not null && await dbContext.Products.AnyAsync(product => product.CompanyId == companyId && product.Barcode == barcode && product.Id != draft.Id, cancellationToken))
            throw new InvalidOperationException("That barcode is already in use.");
        if (draft.CategoryId.HasValue && !await dbContext.Categories.AnyAsync(category => category.Id == draft.CategoryId && category.CompanyId == companyId, cancellationToken))
            throw new InvalidOperationException("Select a category from this company.");

        var now = DateTimeOffset.UtcNow;
        ProductEntity product;
        var action = draft.Id.HasValue ? "ProductUpdated" : "ProductCreated";
        if (draft.Id.HasValue)
        {
            product = await dbContext.Products.SingleOrDefaultAsync(item => item.Id == draft.Id && item.CompanyId == companyId, cancellationToken)
                ?? throw new InvalidOperationException("The product could not be found.");
        }
        else
        {
            product = new ProductEntity { Id = Guid.NewGuid(), CompanyId = companyId, CreatedAt = now, IsActive = true };
            dbContext.Products.Add(product);
        }

        product.Name = name;
        product.Sku = sku;
        product.NormalizedSku = normalizedSku;
        product.Barcode = barcode;
        product.CategoryId = draft.CategoryId;
        product.CostPrice = decimal.Round(draft.CostPrice, 4, MidpointRounding.AwayFromZero);
        product.SellingPrice = decimal.Round(draft.SellingPrice, 4, MidpointRounding.AwayFromZero);
        product.MinimumStock = decimal.Round(draft.MinimumStock, 3, MidpointRounding.AwayFromZero);
        product.UpdatedAt = now;
        dbContext.AuditEvents.Add(CreateAudit(username, action, "Products", product.Id, action));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveProductAsync(Guid companyId, Guid productId, string username, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(item => item.Id == productId && item.CompanyId == companyId, cancellationToken)
            ?? throw new InvalidOperationException("The product could not be found.");
        product.IsActive = false;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.AuditEvents.Add(CreateAudit(username, "ProductArchived", "Products", product.Id, "Product archived"));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AdjustStockAsync(Guid companyId, Guid productId, decimal quantityChange, string reason, string username, CancellationToken cancellationToken = default)
    {
        if (quantityChange == 0 || decimal.Round(quantityChange, 3) != quantityChange)
            throw new ArgumentException("Enter a non-zero quantity with up to three decimal places.", nameof(quantityChange));
        var cleanedReason = reason.Trim();
        if (cleanedReason.Length is < 3 or > 300)
            throw new ArgumentException("Enter a reason between 3 and 300 characters.", nameof(reason));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var product = await dbContext.Products.SingleOrDefaultAsync(item => item.Id == productId && item.CompanyId == companyId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("The active product could not be found.");
        var priorMovements = await dbContext.StockMovements.AsNoTracking()
            .Where(movement => movement.CompanyId == companyId && movement.ProductId == productId)
            .Select(movement => movement.QuantityChange).ToListAsync(cancellationToken);
        var currentBalance = priorMovements.Sum();
        var newBalance = currentBalance + quantityChange;
        if (newBalance < 0)
            throw new InvalidOperationException($"This adjustment would make stock negative. Available: {currentBalance:0.###}.");

        var now = DateTimeOffset.UtcNow;
        dbContext.StockMovements.Add(new StockMovementEntity
        {
            Id = Guid.NewGuid(), CompanyId = companyId, ProductId = productId,
            QuantityChange = quantityChange, BalanceAfter = newBalance, UnitCost = product.CostPrice,
            Reason = cleanedReason, Username = username, OccurredAt = now
        });
        dbContext.AuditEvents.Add(CreateAudit(username, "StockAdjusted", "Inventory", productId, cleanedReason));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockMovementItem>> GetStockMovementsAsync(Guid companyId, int limit = 200, CancellationToken cancellationToken = default)
    {
        var movements = await dbContext.StockMovements.AsNoTracking().Where(movement => movement.CompanyId == companyId)
            .OrderByDescending(movement => movement.OccurredAt).Take(Math.Clamp(limit, 1, 1000))
            .Select(movement => new StockMovementItem(movement.OccurredAt, movement.Product.Sku, movement.Product.Name,
                movement.QuantityChange, movement.BalanceAfter, movement.Reason, movement.Username))
            .ToListAsync(cancellationToken);
        return movements;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var products = await dbContext.Products.AsNoTracking().Where(product => product.CompanyId == companyId && product.IsActive)
            .Select(product => new { product.Id, product.CostPrice, product.MinimumStock }).ToListAsync(cancellationToken);
        var productIds = products.Select(product => product.Id).ToArray();
        var movements = await dbContext.StockMovements.AsNoTracking()
            .Where(movement => movement.CompanyId == companyId && productIds.Contains(movement.ProductId))
            .Select(movement => new { movement.ProductId, movement.QuantityChange }).ToListAsync(cancellationToken);
        var balances = movements.GroupBy(movement => movement.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(movement => movement.QuantityChange));
        var lowStock = products.Count(product => balances.GetValueOrDefault(product.Id) <= product.MinimumStock);
        var outOfStock = products.Count(product => balances.GetValueOrDefault(product.Id) <= 0);
        var inventoryValue = products.Sum(product => balances.GetValueOrDefault(product.Id) * product.CostPrice);
        return new DashboardSummary(products.Count, lowStock, outOfStock, inventoryValue);
    }

    private static AuditEventEntity CreateAudit(string username, string action, string module, Guid recordId, string reason) => new()
    {
        Id = Guid.NewGuid(), Username = username, OccurredAt = DateTimeOffset.UtcNow,
        Action = action, Module = module, RecordId = recordId.ToString("D"), Reason = reason
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
