using Devzone.Application.Auditing;
using Devzone.Application.Companies;
using Devzone.Application.Authorization;
using Devzone.Application.Security;
using Devzone.Application.Users;
using Devzone.Application.Catalog;
using Devzone.Application.Customers;
using Devzone.Application.Purchasing;
using Devzone.Infrastructure.Persistence;
using Devzone.Infrastructure.Services;
using Devzone.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Devzone.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDevzoneLocalServices(this IServiceCollection services, string? databasePath = null)
    {
        services.AddDbContext<DevzoneDbContext>(options => options.UseSqlite(
            SqliteDatabase.CreateConnectionString(databasePath)));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ICompanyProfileService, CompanyProfileService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IUserAuthenticator, UserAuthenticator>();
        services.AddScoped<IOwnerSetupService, OwnerSetupService>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IProductCatalogService, ProductCatalogService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IPurchasingService, PurchasingService>();
        return services;
    }
}
