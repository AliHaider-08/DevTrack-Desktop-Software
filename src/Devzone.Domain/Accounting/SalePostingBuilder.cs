using Devzone.Domain.BusinessRules;
using Devzone.Domain.Exceptions;

namespace Devzone.Domain.Accounting;

public sealed record SaleAccountMap(
    Guid Cash, Guid Receivable, Guid SalesRevenue, Guid TaxPayable, Guid CostOfGoodsSold, Guid Inventory);

/// <summary>
/// Dr Cash (paid) / Dr Receivable (unpaid) / Cr Sales Revenue (total - tax, net of discounts) / Cr Tax Payable
/// Dr COGS / Cr Inventory (cost from the costing engine).
/// Debits = total + cogs; Credits = (total - tax) + tax + cogs, so it always balances.
/// </summary>
public static class SalePostingBuilder
{
    public static JournalEntry Build(string entryNumber, DateOnly date, Guid saleId, string invoiceNumber,
        InvoiceResult invoice, decimal paidAmount, decimal costOfGoodsSold, SaleAccountMap map)
    {
        if (paidAmount < 0 || paidAmount > invoice.GrandTotal)
            throw new DomainException("Paid amount must be between zero and the invoice total.");
        if (costOfGoodsSold < 0) throw new DomainException("Cost of goods sold cannot be negative.");

        var e = new JournalEntry(entryNumber, date, $"Sales invoice {invoiceNumber}",
            $"SALE:{saleId}", "SalesInvoice", saleId.ToString());

        if (paidAmount > 0) e.Debit(map.Cash, paidAmount, invoiceNumber);
        var unpaid = invoice.GrandTotal - paidAmount;
        if (unpaid > 0) e.Debit(map.Receivable, unpaid, invoiceNumber);
        e.Credit(map.SalesRevenue, invoice.GrandTotal - invoice.TaxTotal, invoiceNumber);
        if (invoice.TaxTotal > 0) e.Credit(map.TaxPayable, invoice.TaxTotal, invoiceNumber);
        if (costOfGoodsSold > 0)
        {
            e.Debit(map.CostOfGoodsSold, costOfGoodsSold, invoiceNumber);
            e.Credit(map.Inventory, costOfGoodsSold, invoiceNumber);
        }
        e.Post();
        return e;
    }
}
