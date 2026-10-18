namespace Devzone.Application.Purchasing;

public interface IPurchasingService
{
    Task<IReadOnlyList<PurchaseItem>> GetPurchasesAsync(Guid companyId, int limit = 200, CancellationToken cancellationToken = default);
    Task ReceiveAsync(Guid companyId, PurchaseDraft purchase, string username, CancellationToken cancellationToken = default);
}
