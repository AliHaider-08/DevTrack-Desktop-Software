using System.Text.Json;
using Devzone.Application.Security;
using Devzone.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devzone.Infrastructure.Services;

public sealed class UserAuthenticator(DevzoneDbContext dbContext, IPasswordHasher passwordHasher) : IUserAuthenticator
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;
        var normalized = UserAccountService.Normalize(username);
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.NormalizedUsername == normalized && item.IsActive, cancellationToken);
        if (user is null)
            return null;

        var hash = new PasswordHash(user.PasswordAlgorithm, user.PasswordIterations, user.PasswordSalt, user.PasswordHash, user.PasswordVersion);
        if (!passwordHasher.Verify(password, hash))
            return null;

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        var permissions = JsonSerializer.Deserialize<HashSet<string>>(user.PermissionsJson) ?? [];
        return new AuthenticatedUser(user.Id, user.CompanyId, user.Username, permissions);
    }
}
