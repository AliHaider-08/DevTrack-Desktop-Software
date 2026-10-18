using Devzone.Domain.BusinessRules;
using Devzone.Domain.Exceptions;

namespace Devzone.Domain.Inventory;

/// <summary>
/// Weighted-average costing for one product in one warehouse.
/// Stores TotalValue (not just average) so the books never drift:
/// issuing the full remaining quantity relieves exactly the remaining value.
/// Negative stock is rejected unless allowNegative is explicitly enabled by company setting.
/// </summary>
public sealed class WeightedAverageStock
{
    private readonly RoundingPolicy _p;
    private readonly bool _allowNegative;
    private decimal _lastUnitCost;

    public decimal Quantity { get; private set; }
    public decimal TotalValue { get; private set; }

    public WeightedAverageStock(decimal openingQty = 0m, decimal openingUnitCost = 0m,
        bool allowNegative = false, RoundingPolicy? policy = null)
    {
        _p = policy ?? RoundingPolicy.Default;
        _allowNegative = allowNegative;
        if (openingQty < 0 || openingUnitCost < 0) throw new DomainException("Opening stock cannot be negative.");
        Quantity = openingQty;
        _lastUnitCost = openingUnitCost;
        TotalValue = _p.Money(openingQty * openingUnitCost);
    }

    public decimal AverageUnitCost => Quantity > 0 ? _p.UnitCost(TotalValue / Quantity) : _lastUnitCost;

    /// <summary>Purchase receipt. Returns the value added to inventory.</summary>
    public decimal Receive(decimal quantity, decimal unitCost)
    {
        if (quantity <= 0) throw new DomainException("Received quantity must be greater than zero.");
        if (unitCost < 0) throw new DomainException("Unit cost cannot be negative.");
        var value = _p.Money(quantity * unitCost);
        Quantity += quantity;
        TotalValue += value;
        _lastUnitCost = unitCost;
        return value;
    }

    /// <summary>Customer return: stock re-enters at the ORIGINAL cost of the sale, not today's average.</summary>
    public void ReturnToStock(decimal quantity, decimal originalCost)
    {
        if (quantity <= 0) throw new DomainException("Returned quantity must be greater than zero.");
        if (originalCost < 0) throw new DomainException("Cost cannot be negative.");
        Quantity += quantity;
        TotalValue += originalCost;
    }

    /// <summary>Sale / write-off / transfer-out. Returns the cost relieved (COGS).</summary>
    public decimal Issue(decimal quantity)
    {
        if (quantity <= 0) throw new DomainException("Issued quantity must be greater than zero.");
        if (quantity > Quantity && !_allowNegative) throw new InsufficientStockException(quantity, Quantity);

        decimal cost;
        if (quantity == Quantity) cost = TotalValue;
        else if (Quantity > 0) cost = _p.Money(quantity * TotalValue / Quantity);
        else cost = _p.Money(quantity * _lastUnitCost);

        Quantity -= quantity;
        TotalValue -= cost;
        return cost;
    }
}
