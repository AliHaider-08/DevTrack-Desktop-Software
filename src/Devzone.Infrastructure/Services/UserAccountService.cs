using System.Text.Json;
using Devzone.Application.Users;
using Devzone.Infrastructure.Persistence;
using Devzone.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Devzone.Infrastructure.Services;

public sealed class UserAccountService(DevzoneDbContext dbContext) : IUserAccountService
{
    public async Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(username);
        var user = await dbContext.Users.AsNoTracking()
            .Where(item => item.NormalizedUsername == normalized && item.IsActive)
            .OrderBy(item => item.CompanyId)
            .FirstOrDefaultAsync(cancellationToken);
        return user is null ? null : ToAccount(user);
    }

    public async Task CreateAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Id == Guid.Empty || account.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(account.Username) ||
            account.Username.Length > 80 || string.IsNullOrWhiteSpace(account.DisplayName) || account.DisplayName.Length > 160)
            throw new ArgumentException("User ID, company, username, and display name are required.", nameof(account));
        var normalized = Normalize(account.Username);
        dbContext.Users.Add(new UserEntity
        {
            Id = account.Id,
            CompanyId = account.CompanyId,
            Username = account.Username.Trim(),
            NormalizedUsername = normalized,
            DisplayName = account.DisplayName.Trim(),
            PasswordAlgorithm = account.PasswordAlgorithm,
            PasswordIterations = account.PasswordIterations,
            PasswordVersion = account.PasswordVersion,
            PasswordSalt = account.PasswordSalt,
            PasswordHash = account.PasswordHash,
            PermissionsJson = JsonSerializer.Serialize(Array.Empty<string>()),
            IsActive = account.IsActive,
            CreatedAt = account.CreatedAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordLoginAsync(Guid userId, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.SingleAsync(item => item.Id == userId, cancellationToken);
        user.LastLoginAt = occurredAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static string Normalize(string username) => username.Trim().ToUpperInvariant();

    private static UserAccount ToAccount(UserEntity user) => new(
        user.Id, user.CompanyId, user.Username, user.DisplayName, user.PasswordAlgorithm, user.PasswordIterations,
        user.PasswordVersion, user.PasswordSalt, user.PasswordHash, user.IsActive, user.CreatedAt, user.LastLoginAt);
}
