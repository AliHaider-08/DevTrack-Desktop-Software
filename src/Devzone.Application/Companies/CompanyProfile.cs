namespace Devzone.Application.Companies;

public sealed record CompanyProfile(
    Guid Id,
    string BusinessName,
    string? Address,
    string? Phone,
    string? Email,
    string CurrencyCode,
    int CurrencyDecimalPlaces,
    DateOnly FinancialYearStart,
    string InvoiceNumberPrefix,
    long NextInvoiceNumber);
