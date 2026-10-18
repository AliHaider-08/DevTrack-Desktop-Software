using Devzone.Domain.BusinessRules;
using Devzone.Domain.Exceptions;

namespace Devzone.Domain.Accounting;

public enum JournalStatus { Draft, Posted, Reversed }

public sealed class JournalLine
{
    public Guid AccountId { get; }
    public decimal Debit { get; }
    public decimal Credit { get; }
    public string? Memo { get; }
    internal JournalLine(Guid accountId, decimal debit, decimal credit, string? memo)
    { AccountId = accountId; Debit = debit; Credit = credit; Memo = memo; }
}

/// <summary>
/// Double-entry journal. A posted entry is immutable; corrections are made ONLY through
/// CreateReversal, which preserves the original and links both records.
/// IdempotencyKey (e.g. "SALE:{id}") gets a UNIQUE index in the database so a retry can never post twice.
/// </summary>
public sealed class JournalEntry
{
    private readonly List<JournalLine> _lines = new();
    private readonly RoundingPolicy _policy;

    public Guid Id { get; } = Guid.NewGuid();
    public string EntryNumber { get; }
    public DateOnly EntryDate { get; }
    public string Description { get; }
    public string SourceType { get; }
    public string? SourceId { get; }
    public string IdempotencyKey { get; }
    public JournalStatus Status { get; private set; } = JournalStatus.Draft;
    public Guid? ReversalOfId { get; private set; }
    public Guid? ReversedByEntryId { get; private set; }
    public string? ReversalReason { get; private set; }
    public IReadOnlyList<JournalLine> Lines => _lines;

    public JournalEntry(string entryNumber, DateOnly entryDate, string description,
        string idempotencyKey, string sourceType, string? sourceId = null, RoundingPolicy? policy = null)
    {
        if (string.IsNullOrWhiteSpace(entryNumber)) throw new DomainException("Entry number is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new DomainException("Idempotency key is required.");
        EntryNumber = entryNumber; EntryDate = entryDate; Description = description;
        IdempotencyKey = idempotencyKey; SourceType = sourceType; SourceId = sourceId;
        _policy = policy ?? RoundingPolicy.Default;
    }

    public decimal TotalDebit => _lines.Sum(l => l.Debit);
    public decimal TotalCredit => _lines.Sum(l => l.Credit);

    public JournalEntry Debit(Guid accountId, decimal amount, string? memo = null) => Add(accountId, amount, 0m, memo);
    public JournalEntry Credit(Guid accountId, decimal amount, string? memo = null) => Add(accountId, 0m, amount, memo);

    private JournalEntry Add(Guid accountId, decimal debit, decimal credit, string? memo)
    {
        if (Status != JournalStatus.Draft) throw new DomainException("A posted journal entry cannot be modified.");
        var amount = debit + credit;
        if (accountId == Guid.Empty) throw new DomainException("Account is required.");
        if (amount <= 0) throw new DomainException("Journal line amount must be greater than zero.");
        if (!_policy.IsMoneyPrecise(amount)) throw new DomainException("Journal amount exceeds currency precision.");
        _lines.Add(new JournalLine(accountId, debit, credit, memo));
        return this;
    }

    public void Post()
    {
        if (Status != JournalStatus.Draft) throw new DomainException("Journal entry is already posted.");
        if (_lines.Count < 2) throw new DomainException("A journal entry needs at least two lines.");
        if (TotalDebit != TotalCredit) throw new UnbalancedJournalException(TotalDebit, TotalCredit);
        Status = JournalStatus.Posted;
    }

    /// <summary>Creates and posts the mirror entry; marks this one Reversed. Original lines are untouched.</summary>
    public JournalEntry CreateReversal(string reversalNumber, DateOnly date, string reason)
    {
        if (Status != JournalStatus.Posted) throw new DomainException("Only a posted, non-reversed entry can be reversed.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("A reversal reason is required.");

        var rev = new JournalEntry(reversalNumber, date, $"Reversal of {EntryNumber}: {reason}",
            $"REVERSAL:{IdempotencyKey}", "Reversal", Id.ToString(), _policy)
        { ReversalOfId = Id, ReversalReason = reason };
        foreach (var l in _lines)
            rev.Add(l.AccountId, l.Credit, l.Debit, l.Memo);   // swap sides
        rev.Post();

        Status = JournalStatus.Reversed;
        ReversedByEntryId = rev.Id;
        return rev;
    }
}
