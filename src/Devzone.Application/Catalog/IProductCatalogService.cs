namespace Devzone.Application.Catalog;

public interface IProductCatalogService
{
    Task<IReadOnlyList<ProductItem>> SearchProductsAsync(Guid companyId, string? search = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryItem>> GetCategoriesAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task SaveCategoryAsync(Guid companyId, string name, string? description, string username, CancellationToken cancellationToken = default);
    Task SaveProductAsync(Guid companyId, ProductDraft product, string username, CancellationToken cancellationToken = default);
    Task ArchiveProductAsync(Guid companyId, Guid productId, string username, CancellationToken cancellationToken = default);
    Task AdjustStockAsync(Guid companyId, Guid productId, decimal quantityChange, string reason, string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockMovementItem>> GetStockMovementsAsync(Guid companyId, int limit = 200, CancellationToken cancellationToken = default);
    Task<DashboardSummary> GetDashboardSummaryAsync(Guid companyId, CancellationToken cancellationToken = default);
}
