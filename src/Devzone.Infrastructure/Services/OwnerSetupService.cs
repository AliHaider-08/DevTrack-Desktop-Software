using System.Text.Json;
using Devzone.Application.Authorization;
using Devzone.Application.Security;
using Devzone.Infrastructure.Persistence;
using Devzone.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Devzone.Infrastructure.Services;

public sealed class OwnerSetupService(DevzoneDbContext dbContext, IPasswordHasher passwordHasher) : IOwnerSetupService
{
    private static readonly string[] OwnerPermissions =
    [
        Permissions.DashboardRead, Permissions.CompanyManage, Permissions.UsersManage,
        Permissions.ProductsRead, Permissions.ProductsManage, Permissions.InventoryAdjust,
        Permissions.SalesRead, Permissions.SalesCreate, Permissions.SalesPost, Permissions.SalesReturn,
        Permissions.PurchasesRead, Permissions.PurchasesCreate, Permissions.PurchasesPost,
        Permissions.AccountingRead, Permissions.AccountingPost, Permissions.AuditRead,
        Permissions.BackupManage, Permissions.ReportsExport
    ];

    public async Task CreateFirstOwnerAsync(OwnerSetupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var businessName = request.BusinessName.Trim();
        var ownerName = request.OwnerName.Trim();
        var username = request.Username.Trim();
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (businessName.Length is < 2 or > 200 || ownerName.Length is < 2 or > 160 || username.Length is < 3 or > 80)
            throw new ArgumentException("Enter a valid business name, owner name, and username.", nameof(request));
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
            throw new ArgumentException("Currency must be a three-letter code.", nameof(request));

        var passwordHash = passwordHasher.Hash(request.Password);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await dbContext.Users.AnyAsync(cancellationToken))
            throw new InvalidOperationException("Initial setup is already complete.");
        if (await dbContext.Users.AnyAsync(user => user.NormalizedUsername == UserAccountService.Normalize(username), cancellationToken))
            throw new InvalidOperationException("That username is already in use.");

        var companyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.Companies.Add(new CompanyEntity
        {
            Id = companyId,
            BusinessName = businessName,
            CurrencyCode = currency,
            CurrencyDecimalPlaces = 2,
            FinancialYearStart = request.FinancialYearStart,
            InvoiceNumberPrefix = "INV-",
            NextInvoiceNumber = 1
        });
        dbContext.Users.Add(new UserEntity
        {
            Id = ownerId,
            CompanyId = companyId,
            Username = username,
            NormalizedUsername = UserAccountService.Normalize(username),
            DisplayName = ownerName,
            PasswordAlgorithm = passwordHash.Algorithm,
            PasswordIterations = passwordHash.Iterations,
            PasswordVersion = passwordHash.Version,
            PasswordSalt = passwordHash.Salt,
            PasswordHash = passwordHash.Hash,
            PermissionsJson = JsonSerializer.Serialize(OwnerPermissions),
            IsActive = true,
            CreatedAt = now
        });
        dbContext.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(), UserId = ownerId, Username = username, OccurredAt = now,
            Action = "OwnerSetupCompleted", Module = "Security", RecordId = ownerId.ToString("D"),
            RelatedReference = companyId.ToString("D")
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
