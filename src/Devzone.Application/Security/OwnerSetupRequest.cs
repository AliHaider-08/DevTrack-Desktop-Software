namespace Devzone.Application.Security;

public sealed record OwnerSetupRequest(
    string BusinessName,
    string CurrencyCode,
    string OwnerName,
    string Username,
    string Password,
    DateOnly FinancialYearStart);

public interface IOwnerSetupService
{
    Task CreateFirstOwnerAsync(OwnerSetupRequest request, CancellationToken cancellationToken = default);
}
