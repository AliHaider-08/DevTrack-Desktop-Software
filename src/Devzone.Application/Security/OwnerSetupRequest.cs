namespace Devzone.Application.Security;

public sealed record OwnerSetupRequest(
    string BusinessName,
    string BusinessType,
    string CurrencyCode,
    string OwnerName,
    string Username,
    string Password,
    DateOnly FinancialYearStart);

public interface IOwnerSetupService
{
    Task CreateOwnerAsync(OwnerSetupRequest request, CancellationToken cancellationToken = default);
}
