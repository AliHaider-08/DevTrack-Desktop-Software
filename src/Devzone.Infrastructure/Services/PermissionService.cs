using System.Text.Json;
using Devzone.Application.Authorization;
using Devzone.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devzone.Infrastructure.Services;

public sealed class PermissionService(DevzoneDbContext dbContext) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(Guid userId, Guid companyId, string permission, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return false;
        var serializedPermissions = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.CompanyId == companyId && user.IsActive)
            .Select(user => user.PermissionsJson)
            .SingleOrDefaultAsync(cancellationToken);
        if (serializedPermissions is null)
            return false;
        var permissions = JsonSerializer.Deserialize<HashSet<string>>(serializedPermissions);
        return permissions?.Contains(permission) == true;
    }
}
