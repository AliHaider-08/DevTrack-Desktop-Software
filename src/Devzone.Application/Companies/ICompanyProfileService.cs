namespace Devzone.Application.Companies;

public interface ICompanyProfileService
{
    Task<CompanyProfile?> GetAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task SaveAsync(CompanyProfile profile, CancellationToken cancellationToken = default);
}
