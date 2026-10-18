namespace Devzone.Application.Customers;

public sealed record CustomerItem(
    Guid Id, string Code, string Name, string? Phone, string? Email, string? Address,
    decimal CreditLimit, decimal Balance, bool IsActive);

public sealed record CustomerDraft(
    Guid? Id, string Code, string Name, string? Phone, string? Email, string? Address,
    decimal CreditLimit, decimal OpeningBalance);

public sealed record CustomerLedgerItem(
    DateTimeOffset OccurredAt, string Type, decimal BalanceChange, decimal BalanceAfter,
    string Description, string Username);
