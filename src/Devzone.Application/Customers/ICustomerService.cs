namespace Devzone.Application.Customers;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerItem>> SearchAsync(Guid companyId, string? search = null, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid companyId, CustomerDraft customer, string username, CancellationToken cancellationToken = default);
    Task RecordReceiptAsync(Guid companyId, Guid customerId, decimal amount, string description, string username, CancellationToken cancellationToken = default);
    Task RecordChargeAsync(Guid companyId, Guid customerId, decimal amount, string description, string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerLedgerItem>> GetLedgerAsync(Guid companyId, Guid customerId, CancellationToken cancellationToken = default);
}
