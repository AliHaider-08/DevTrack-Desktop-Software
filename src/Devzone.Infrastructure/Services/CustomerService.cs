using Devzone.Infrastructure.Persistence;
using Devzone.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Devzone.Application.Auditing;
using Devzone.Application.Customers;

namespace Devzone.Infrastructure.Services;

public sealed class CustomerService(DevzoneDbContext dbContext) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerItem>> SearchAsync(Guid companyId, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Customers.AsNoTracking().Where(customer => customer.CompanyId == companyId && customer.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(customer => customer.Name.Contains(term) || customer.Code.Contains(term) ||
                (customer.Phone != null && customer.Phone.Contains(term)) || (customer.Email != null && customer.Email.Contains(term)));
        }

        var customers = await query.OrderBy(customer => customer.Name).ToListAsync(cancellationToken);
        var customerIds = customers.Select(customer => customer.Id).ToArray();
        var entries = await dbContext.CustomerLedgerEntries.AsNoTracking()
            .Where(entry => entry.CompanyId == companyId && customerIds.Contains(entry.CustomerId))
            .Select(entry => new { entry.CustomerId, entry.BalanceChange }).ToListAsync(cancellationToken);
        var balances = entries.GroupBy(entry => entry.CustomerId)
            .ToDictionary(group => group.Key, group => decimal.Round(group.Sum(entry => entry.BalanceChange), 2, MidpointRounding.AwayFromZero));
        return customers.Select(customer => new CustomerItem(customer.Id, customer.Code, customer.Name,
            customer.Phone, customer.Email, customer.Address, customer.CreditLimit,
            balances.GetValueOrDefault(customer.Id), customer.IsActive)).ToArray();
    }

    public async Task SaveAsync(Guid companyId, CustomerDraft draft, string username, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var code = draft.Code.Trim();
        var name = draft.Name.Trim();
        if (code.Length is < 1 or > 40 || name.Length is < 2 or > 200)
            throw new ArgumentException("Customer code and name are required and must fit their field limits.");
        if (draft.CreditLimit < 0)
            throw new ArgumentException("Credit limit cannot be negative.", nameof(draft));
        var normalizedCode = code.ToUpperInvariant();
        if (await dbContext.Customers.AnyAsync(customer => customer.CompanyId == companyId && customer.NormalizedCode == normalizedCode && customer.Id != draft.Id, cancellationToken))
            throw new InvalidOperationException("That customer code is already in use.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var isNew = !draft.Id.HasValue;
        var customer = isNew
            ? new CustomerEntity { Id = Guid.NewGuid(), CompanyId = companyId, CreatedAt = DateTimeOffset.UtcNow }
            : await dbContext.Customers.SingleOrDefaultAsync(item => item.Id == draft.Id && item.CompanyId == companyId, cancellationToken)
                ?? throw new InvalidOperationException("The customer could not be found.");
        if (isNew)
            dbContext.Customers.Add(customer);

        customer.Code = code;
        customer.NormalizedCode = normalizedCode;
        customer.Name = name;
        customer.NormalizedName = name.ToUpperInvariant();
        customer.Phone = Clean(draft.Phone);
        customer.Email = Clean(draft.Email)?.ToLowerInvariant();
        customer.Address = Clean(draft.Address);
        customer.CreditLimit = decimal.Round(draft.CreditLimit, 2, MidpointRounding.AwayFromZero);
        customer.IsActive = true;

        var now = DateTimeOffset.UtcNow;
        if (isNew && draft.OpeningBalance != 0)
        {
            var opening = decimal.Round(draft.OpeningBalance, 2, MidpointRounding.AwayFromZero);
            dbContext.CustomerLedgerEntries.Add(CreateEntry(companyId, customer.Id, now, "OpeningBalance", opening, opening, "Opening balance", username));
        }
        dbContext.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(), Username = username, OccurredAt = now,
            Action = isNew ? "CustomerCreated" : "CustomerUpdated", Module = "Customers",
            RecordId = customer.Id.ToString("D"), Reason = isNew ? "Customer created" : "Customer details updated"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task RecordReceiptAsync(Guid companyId, Guid customerId, decimal amount, string description, string username, CancellationToken cancellationToken = default) =>
        RecordBalanceEntryAsync(companyId, customerId, amount, description, username, "Receipt", -1, cancellationToken);

    public Task RecordChargeAsync(Guid companyId, Guid customerId, decimal amount, string description, string username, CancellationToken cancellationToken = default) =>
        RecordBalanceEntryAsync(companyId, customerId, amount, description, username, "Charge", 1, cancellationToken);

    public async Task<IReadOnlyList<CustomerLedgerItem>> GetLedgerAsync(Guid companyId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var entries = await dbContext.CustomerLedgerEntries.AsNoTracking()
            .Where(entry => entry.CompanyId == companyId && entry.CustomerId == customerId)
            .OrderBy(entry => entry.OccurredAt).ThenBy(entry => entry.Id)
            .Select(entry => new CustomerLedgerItem(entry.OccurredAt, entry.Type, entry.BalanceChange,
                entry.BalanceAfter, entry.Description, entry.Username)).ToListAsync(cancellationToken);
        return entries;
    }

    private async Task RecordBalanceEntryAsync(Guid companyId, Guid customerId, decimal amount, string description,
        string username, string type, int sign, CancellationToken cancellationToken)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Enter a positive amount with up to two decimal places.", nameof(amount));
        var cleanedDescription = description.Trim();
        if (cleanedDescription.Length is < 3 or > 300)
            throw new ArgumentException("Description must be between 3 and 300 characters.", nameof(description));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var customer = await dbContext.Customers.SingleOrDefaultAsync(item => item.Id == customerId && item.CompanyId == companyId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("The customer could not be found.");
        var changes = await dbContext.CustomerLedgerEntries.AsNoTracking()
            .Where(entry => entry.CompanyId == companyId && entry.CustomerId == customerId)
            .Select(entry => entry.BalanceChange).ToListAsync(cancellationToken);
        var change = amount * sign;
        var balance = decimal.Round(changes.Sum() + change, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow;
        dbContext.CustomerLedgerEntries.Add(CreateEntry(companyId, customer.Id, now, type, change, balance, cleanedDescription, username));
        dbContext.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(), Username = username, OccurredAt = now,
            Action = type == "Receipt" ? "CustomerReceiptRecorded" : "CustomerChargeRecorded",
            Module = "Customers", RecordId = customer.Id.ToString("D"), Reason = cleanedDescription
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static CustomerLedgerEntryEntity CreateEntry(Guid companyId, Guid customerId, DateTimeOffset occurredAt,
        string type, decimal change, decimal balanceAfter, string description, string username) => new()
    {
        Id = Guid.NewGuid(), CompanyId = companyId, CustomerId = customerId, OccurredAt = occurredAt,
        Type = type, BalanceChange = change, BalanceAfter = balanceAfter,
        Description = description, Username = username
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
