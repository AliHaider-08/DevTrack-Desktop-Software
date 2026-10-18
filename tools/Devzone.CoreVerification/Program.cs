using Devzone.Domain.Accounting;
using Devzone.Domain.BusinessRules;
using Devzone.Domain.Exceptions;
using Devzone.Domain.Inventory;

int passed = 0, failed = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) { passed++; Console.WriteLine($"  PASS  {name}"); }
    else { failed++; Console.WriteLine($"  FAIL  {name} {detail}"); }
}
void Throws<T>(string name, Action a) where T : Exception
{
    try { a(); Check(name, false, "(no exception)"); }
    catch (T) { Check(name, true); }
    catch (Exception ex) { Check(name, false, $"(wrong exception {ex.GetType().Name})"); }
}
InvoiceLineInput L(decimal q, decimal p, decimal d = 0, decimal t = 0) => new(q, p, d, t);
InvoiceResult Calc(TaxMode m, decimal disc, decimal charges, params InvoiceLineInput[] lines)
    => InvoiceCalculator.Calculate(new InvoiceInput(lines, disc, charges, m));

Console.WriteLine("== Invoice calculation ==");
var r1 = Calc(TaxMode.Exclusive, 0, 0, L(3, 19.99m, 0, 10));
Check("3 x 19.99 + 10% tax = 65.97", r1.GrandTotal == 65.97m && r1.TaxTotal == 6.00m && r1.NetSales == 59.97m, r1.GrandTotal.ToString());

var r2 = Calc(TaxMode.Exclusive, 1.00m, 0, L(1, 10), L(1, 10), L(1, 10));
Check("invoice discount 1.00 over 3 equal lines sums exactly", r2.Lines.Sum(x => x.AllocatedInvoiceDiscount) == 1.00m);
Check("allocation is 0.34 / 0.33 / 0.33", r2.Lines[0].AllocatedInvoiceDiscount == 0.34m && r2.Lines[1].AllocatedInvoiceDiscount == 0.33m);
Check("grand total after discount = 29.00", r2.GrandTotal == 29.00m);

var r3 = Calc(TaxMode.Inclusive, 0, 0, L(1, 110, 0, 10));
Check("tax-inclusive 110 @10% => net 100 tax 10 total 110", r3.NetSales == 100m && r3.TaxTotal == 10m && r3.GrandTotal == 110m);

Check("midpoint 0.125 rounds away from zero to 0.13", Calc(TaxMode.Exclusive, 0, 0, L(1, 0.125m)).GrandTotal == 0.13m);
var r4 = Calc(TaxMode.Exclusive, 0, 25.00m, L(2, 50, 10, 5));
Check("line discount + tax + freight: (100-10)=90 +4.50 +25 = 119.50", r4.GrandTotal == 119.50m, r4.GrandTotal.ToString());

Throws<DomainException>("rejects line discount above line amount", () => Calc(TaxMode.Exclusive, 0, 0, L(1, 10, 11)));
Throws<DomainException>("rejects zero quantity", () => Calc(TaxMode.Exclusive, 0, 0, L(0, 10)));
Throws<DomainException>("rejects invoice discount above value", () => Calc(TaxMode.Exclusive, 99, 0, L(1, 10)));
Throws<DomainException>("rejects empty invoice", () => Calc(TaxMode.Exclusive, 0, 0));
Throws<DomainException>("rejects sub-cent charges", () => Calc(TaxMode.Exclusive, 0, 0.001m, L(1, 10)));

Check("outstanding = 100 - 40 - 10 = 50", InvoiceCalculator.Outstanding(100, 40, 10) == 50);
Throws<DomainException>("rejects overpayment", () => InvoiceCalculator.Outstanding(100, 90, 20));

Console.WriteLine("== Journal / double entry ==");
Guid A() => Guid.NewGuid();
var d = new DateOnly(2026, 10, 17);
var cash = A(); var rev = A();
var je = new JournalEntry("JE-1", d, "test", "KEY:1", "Manual").Debit(cash, 100).Credit(rev, 100);
je.Post();
Check("balanced entry posts", je.Status == JournalStatus.Posted);
Throws<UnbalancedJournalException>("unbalanced entry is rejected",
    () => new JournalEntry("JE-2", d, "bad", "KEY:2", "Manual").Debit(cash, 100).Credit(rev, 99.99m).Post());
Throws<DomainException>("posted entry cannot be edited", () => je.Debit(cash, 1));
Throws<DomainException>("single-line entry rejected",
    () => new JournalEntry("JE-3", d, "one", "KEY:3", "Manual").Debit(cash, 5).Post());
Throws<DomainException>("zero-amount line rejected", () => new JournalEntry("JE-4", d, "z", "KEY:4", "Manual").Debit(cash, 0));
var reversal = je.CreateReversal("JE-1R", d, "customer cancelled");
Check("reversal swaps sides", reversal.Lines[0].Credit == 100 && reversal.Lines[1].Debit == 100);
Check("reversal references original and is posted", reversal.ReversalOfId == je.Id && reversal.Status == JournalStatus.Posted);
Check("original preserved and marked reversed", je.Status == JournalStatus.Reversed && je.Lines[0].Debit == 100 && je.ReversedByEntryId == reversal.Id);
Throws<DomainException>("cannot reverse twice", () => je.CreateReversal("JE-1R2", d, "again"));
Throws<DomainException>("reversal needs a reason", () => new JournalEntry("JE-5", d, "x", "KEY:5", "Manual").Debit(cash, 1).Credit(rev, 1).CreateReversal("R", d, " "));

Console.WriteLine("== Weighted-average costing ==");
var s = new WeightedAverageStock();
s.Receive(10, 5.00m); s.Receive(10, 7.00m);
Check("avg after 10@5 + 10@7 = 6.0000, value 120", s.AverageUnitCost == 6m && s.TotalValue == 120m && s.Quantity == 20);
Check("issue 5 relieves 30.00", s.Issue(5) == 30m && s.TotalValue == 90m);
Check("issuing remaining 15 relieves exactly 90.00", s.Issue(15) == 90m && s.TotalValue == 0m && s.Quantity == 0);
Throws<InsufficientStockException>("negative stock blocked by default", () => s.Issue(1));

var s2 = new WeightedAverageStock();
var valueIn = s2.Receive(3, 3.3333m);
var cogs = s2.Issue(1) + s2.Issue(1) + s2.Issue(1);
Check("no drift: total COGS of 3 issues == value received", cogs == valueIn && s2.TotalValue == 0m, $"{cogs} vs {valueIn}");

var s3 = new WeightedAverageStock(allowNegative: true);
s3.Receive(2, 4m);
Check("negative stock allowed only when enabled", s3.Issue(3) >= 0 && s3.Quantity == -1);

var s4 = new WeightedAverageStock(10, 5m);
var c4 = s4.Issue(4);
s4.ReturnToStock(4, c4);
Check("return restores quantity and original cost", s4.Quantity == 10 && s4.TotalValue == 50m);

Console.WriteLine("== Sale posting reconciles to invoice ==");
var map = new SaleAccountMap(A(), A(), A(), A(), A(), A());
var inv = Calc(TaxMode.Exclusive, 5.00m, 12.00m, L(4, 24.50m, 3, 15), L(2, 9.99m, 0, 5));
var saleId = Guid.NewGuid();
var entry = SalePostingBuilder.Build("JE-S1", d, saleId, "INV-0001", inv, 100.00m, 61.37m, map);
Check("sale entry is balanced", entry.TotalDebit == entry.TotalCredit);
decimal Sum(Guid acc, bool debit) => entry.Lines.Where(x => x.AccountId == acc).Sum(x => debit ? x.Debit : x.Credit);
Check("cash + receivable debits == invoice total", Sum(map.Cash, true) + Sum(map.Receivable, true) == inv.GrandTotal);
Check("revenue + tax credits == invoice total", Sum(map.SalesRevenue, false) + Sum(map.TaxPayable, false) == inv.GrandTotal);
Check("COGS debit == inventory credit", Sum(map.CostOfGoodsSold, true) == Sum(map.Inventory, false));
Check("idempotency key is deterministic per sale", entry.IdempotencyKey == $"SALE:{saleId}");
Throws<DomainException>("overpaid sale posting rejected", () => SalePostingBuilder.Build("X", d, saleId, "I", inv, inv.GrandTotal + 1, 0, map));

Console.WriteLine("== Randomized invariants (5,000 invoices, fixed seed) ==");
var rnd = new Random(20261017);
int bad = 0; string firstBad = "";
decimal[] rates = { 0m, 5m, 10m, 15.5m, 17m };
for (var n = 0; n < 5000; n++)
{
    var count = rnd.Next(1, 8);
    var lines = new List<InvoiceLineInput>();
    for (var i = 0; i < count; i++)
    {
        var qty = Math.Round((decimal)(rnd.NextDouble() * 49.999 + 0.001), 3);
        var price = Math.Round((decimal)(rnd.NextDouble() * 999.98 + 0.01), 2);
        var gross = Math.Round(qty * price, 2, MidpointRounding.AwayFromZero);
        var disc = rnd.Next(0, 4) == 0 ? Math.Round(gross * (decimal)rnd.NextDouble(), 2) : 0m;
        lines.Add(new InvoiceLineInput(qty, price, disc, rates[rnd.Next(rates.Length)]));
    }
    var mode = rnd.Next(2) == 0 ? TaxMode.Exclusive : TaxMode.Inclusive;
    var baseTotal = lines.Sum(x => Math.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero) - x.LineDiscount);
    var invDisc = rnd.Next(0, 3) == 0 ? Math.Round(baseTotal * (decimal)rnd.NextDouble(), 2) : 0m;
    var charges = rnd.Next(0, 3) == 0 ? Math.Round((decimal)(rnd.NextDouble() * 50), 2) : 0m;
    try
    {
        var res = InvoiceCalculator.Calculate(new InvoiceInput(lines, invDisc, charges, mode));
        var ok = res.Lines.Sum(x => x.AllocatedInvoiceDiscount) == invDisc
              && res.Lines.Sum(x => x.Total) + charges == res.GrandTotal
              && res.NetSales + res.TaxTotal + charges == res.GrandTotal
              && res.Lines.All(x => x.NetBeforeTax >= 0 && x.Tax >= 0);
        var paid = Math.Round(res.GrandTotal * (decimal)rnd.NextDouble(), 2);
        var e = SalePostingBuilder.Build($"JE-{n}", d, Guid.NewGuid(), $"INV-{n}", res, paid,
            Math.Round(res.NetSales * 0.6m, 2), map);
        ok &= e.TotalDebit == e.TotalCredit;
        if (!ok) { bad++; if (firstBad == "") firstBad = $"iteration {n}"; }
    }
    catch (Exception ex) { bad++; if (firstBad == "") firstBad = $"iteration {n}: {ex.Message}"; }
}
Check("all 5,000 invoices: allocations exact, totals consistent, postings balanced", bad == 0, firstBad);

Console.WriteLine($"\nRESULT: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
