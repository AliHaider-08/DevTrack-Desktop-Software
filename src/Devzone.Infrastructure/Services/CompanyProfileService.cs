using Devzone.Application.Companies;
using Devzone.Infrastructure.Persistence;
using Devzone.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Devzone.Infrastructure.Services;

public sealed class CompanyProfileService(DevzoneDbContext dbContext) : ICompanyProfileService
{
    public async Task<CompanyProfile?> GetAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        return company is null ? null : ToProfile(company);
    }

    public async Task SaveAsync(CompanyProfile profile, CancellationToken cancellationToken = default)
    {
        Validate(profile);
        var entity = await dbContext.Companies.SingleOrDefaultAsync(item => item.Id == profile.Id, cancellationToken);
        if (entity is null)
        {
            entity = new CompanyEntity { Id = profile.Id };
            dbContext.Companies.Add(entity);
        }

        entity.BusinessName = profile.BusinessName.Trim();
        entity.Address = Clean(profile.Address);
        entity.Phone = Clean(profile.Phone);
        entity.Email = Clean(profile.Email)?.ToLowerInvariant();
        entity.CurrencyCode = profile.CurrencyCode.Trim().ToUpperInvariant();
        entity.CurrencyDecimalPlaces = profile.CurrencyDecimalPlaces;
        entity.FinancialYearStart = profile.FinancialYearStart;
        entity.InvoiceNumberPrefix = profile.InvoiceNumberPrefix.Trim();
        entity.NextInvoiceNumber = profile.NextInvoiceNumber;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CompanyProfile ToProfile(CompanyEntity entity) => new(
        entity.Id, entity.BusinessName, entity.Address, entity.Phone, entity.Email, entity.CurrencyCode,
        entity.CurrencyDecimalPlaces, entity.FinancialYearStart, entity.InvoiceNumberPrefix, entity.NextInvoiceNumber);

    private static void Validate(CompanyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.BusinessName) || profile.BusinessName.Length > 200)
            throw new ArgumentException("A company ID and business name of at most 200 characters are required.", nameof(profile));
        if (profile.CurrencyCode.Trim().Length != 3 || profile.CurrencyDecimalPlaces is < 0 or > 4)
            throw new ArgumentException("Currency must be a three-letter code with 0 to 4 decimal places.", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.InvoiceNumberPrefix) || profile.InvoiceNumberPrefix.Length > 20 || profile.NextInvoiceNumber < 1)
            throw new ArgumentException("Invoice numbering settings are invalid.", nameof(profile));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
