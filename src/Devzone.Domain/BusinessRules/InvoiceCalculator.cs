using Devzone.Domain.Exceptions;

namespace Devzone.Domain.BusinessRules;

public enum TaxMode { Exclusive, Inclusive }

public sealed record InvoiceLineInput(decimal Quantity, decimal UnitPrice, decimal LineDiscount, decimal TaxRatePercent);

public sealed record InvoiceInput(
    IReadOnlyList<InvoiceLineInput> Lines,
    decimal InvoiceDiscount,
    decimal Charges,
    TaxMode TaxMode);

public sealed record InvoiceLineResult(
    decimal Gross, decimal LineDiscount, decimal AllocatedInvoiceDiscount,
    decimal NetBeforeTax, decimal Tax, decimal Total);

public sealed record InvoiceResult(
    IReadOnlyList<InvoiceLineResult> Lines,
    decimal Subtotal,          // sum of line gross (tax-inclusive gross when TaxMode.Inclusive)
    decimal LineDiscounts,
    decimal InvoiceDiscount,
    decimal NetSales,          // revenue excluding tax, after all discounts, excluding charges
    decimal TaxTotal,
    decimal Charges,
    decimal GrandTotal);

/// <summary>
/// Rules (documented, deterministic):
/// 1. Line gross = round(Qty x UnitPrice).
/// 2. Line discount is an amount (UI may convert % to amount via PercentOf) and cannot exceed line gross.
/// 3. Invoice discount is allocated across lines proportionally to line value after line discount,
///    using largest-remainder so allocations sum EXACTLY to the discount (no lost cents).
/// 4. Tax is computed on the discounted line base.
///    Exclusive: tax = round(base x rate); total = base + tax.
///    Inclusive: base already contains tax; tax = round(base - base/(1+rate)); total = base.
/// 5. Charges (freight etc.) are added after tax and are not taxed here.
/// 6. Grand total = sum of line totals + charges.
/// </summary>
public static class InvoiceCalculator
{
    public static decimal PercentOf(decimal amount, decimal percent, RoundingPolicy? policy = null)
        => (policy ?? RoundingPolicy.Default).Money(amount * percent / 100m);

    public static InvoiceResult Calculate(InvoiceInput input, RoundingPolicy? policy = null)
    {
        var p = policy ?? RoundingPolicy.Default;
        if (input.Lines.Count == 0) throw new DomainException("Invoice must contain at least one line.");
        if (input.InvoiceDiscount < 0) throw new DomainException("Invoice discount cannot be negative.");
        if (input.Charges < 0) throw new DomainException("Charges cannot be negative.");
        if (!p.IsMoneyPrecise(input.InvoiceDiscount) || !p.IsMoneyPrecise(input.Charges))
            throw new DomainException("Discount and charges must use the configured currency precision.");

        var gross = new decimal[input.Lines.Count];
        var lineDisc = new decimal[input.Lines.Count];
        var base1 = new decimal[input.Lines.Count];

        for (var i = 0; i < input.Lines.Count; i++)
        {
            var l = input.Lines[i];
            if (l.Quantity <= 0) throw new DomainException($"Line {i + 1}: quantity must be greater than zero.");
            if (l.UnitPrice < 0) throw new DomainException($"Line {i + 1}: unit price cannot be negative.");
            if (l.LineDiscount < 0) throw new DomainException($"Line {i + 1}: discount cannot be negative.");
            if (l.TaxRatePercent < 0) throw new DomainException($"Line {i + 1}: tax rate cannot be negative.");
            if (!p.IsMoneyPrecise(l.LineDiscount)) throw new DomainException($"Line {i + 1}: discount exceeds currency precision.");

            gross[i] = p.Money(l.Quantity * l.UnitPrice);
            lineDisc[i] = l.LineDiscount;
            if (lineDisc[i] > gross[i]) throw new DomainException($"Line {i + 1}: discount exceeds line amount.");
            base1[i] = gross[i] - lineDisc[i];
        }

        var sumBase1 = base1.Sum();
        if (input.InvoiceDiscount > sumBase1) throw new DomainException("Invoice discount exceeds invoice value.");
        var alloc = Allocate(input.InvoiceDiscount, base1, p);

        var lines = new List<InvoiceLineResult>(input.Lines.Count);
        for (var i = 0; i < input.Lines.Count; i++)
        {
            var rate = input.Lines[i].TaxRatePercent / 100m;
            var b = base1[i] - alloc[i];
            decimal tax, net, total;
            if (input.TaxMode == TaxMode.Exclusive)
            {
                tax = p.Money(b * rate);
                net = b;
                total = b + tax;
            }
            else
            {
                tax = p.Money(b - b / (1m + rate));
                net = b - tax;
                total = b;
            }
            lines.Add(new InvoiceLineResult(gross[i], lineDisc[i], alloc[i], net, tax, total));
        }

        var taxTotal = lines.Sum(x => x.Tax);
        var netSales = lines.Sum(x => x.NetBeforeTax);
        var grand = lines.Sum(x => x.Total) + input.Charges;
        return new InvoiceResult(lines, gross.Sum(), lineDisc.Sum(), input.InvoiceDiscount,
            netSales, taxTotal, input.Charges, grand);
    }

    /// <summary>Outstanding = Total - Allocated Payments - Valid Credits. Overpayment is rejected.</summary>
    public static decimal Outstanding(decimal invoiceTotal, decimal allocatedPayments, decimal validCredits)
    {
        if (allocatedPayments < 0 || validCredits < 0) throw new DomainException("Payments and credits cannot be negative.");
        var outstanding = invoiceTotal - allocatedPayments - validCredits;
        if (outstanding < 0) throw new DomainException("Payments and credits exceed the invoice total.");
        return outstanding;
    }

    /// <summary>Largest-remainder allocation: result sums exactly to total.</summary>
    public static decimal[] Allocate(decimal total, decimal[] weights, RoundingPolicy p)
    {
        var n = weights.Length;
        var result = new decimal[n];
        var sumW = weights.Sum();
        if (total == 0 || sumW == 0) return result;

        var unit = p.SmallestMoneyUnit;
        var exact = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            exact[i] = total * weights[i] / sumW;
            result[i] = Math.Floor(exact[i] / unit) * unit;
        }
        var leftoverUnits = (int)Math.Round((total - result.Sum()) / unit);
        var order = Enumerable.Range(0, n)
            .OrderByDescending(i => exact[i] - result[i]).ThenBy(i => i).ToArray();
        for (var k = 0; k < leftoverUnits; k++) result[order[k]] += unit;
        return result;
    }
}
