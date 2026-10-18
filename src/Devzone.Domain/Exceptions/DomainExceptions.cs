namespace Devzone.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public sealed class UnbalancedJournalException : DomainException
{
    public UnbalancedJournalException(decimal debit, decimal credit)
        : base($"Journal entry is not balanced: debits {debit} <> credits {credit}.") { }
}

public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException(decimal requested, decimal available)
        : base($"Insufficient stock: requested {requested}, available {available}.") { }
}
