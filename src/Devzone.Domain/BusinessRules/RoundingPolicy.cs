namespace Devzone.Domain.BusinessRules;

/// <summary>
/// Single source of truth for rounding. All monetary values are System.Decimal.
/// Rule: round at LINE level, then sum the rounded lines. Totals are never re-rounded,
/// so UI, printed invoices, ledger entries and reports always agree to the cent.
/// </summary>
public sealed record RoundingPolicy(
    int MoneyDecimals = 2,
    int UnitCostDecimals = 4,
    MidpointRounding Mode = MidpointRounding.AwayFromZero)
{
    public static RoundingPolicy Default { get; } = new();

    public decimal Money(decimal value) => Math.Round(value, MoneyDecimals, Mode);
    public decimal UnitCost(decimal value) => Math.Round(value, UnitCostDecimals, Mode);

    public bool IsMoneyPrecise(decimal value) => value == Math.Round(value, MoneyDecimals, Mode);

    public decimal SmallestMoneyUnit
    {
        get { var u = 1m; for (var i = 0; i < MoneyDecimals; i++) u /= 10m; return u; }
    }
}
