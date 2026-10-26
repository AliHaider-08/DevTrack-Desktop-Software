namespace Devzone.Infrastructure.Persistence.Entities;

public sealed class CompanyEntity
{
    public Guid Id { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = "Other";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public int CurrencyDecimalPlaces { get; set; } = 2;
    public DateOnly FinancialYearStart { get; set; }
    public string InvoiceNumberPrefix { get; set; } = "INV-";
    public long NextInvoiceNumber { get; set; } = 1;
    public ICollection<UserEntity> Users { get; set; } = new List<UserEntity>();
}
