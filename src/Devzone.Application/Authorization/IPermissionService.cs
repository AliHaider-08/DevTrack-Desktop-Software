namespace Devzone.Application.Authorization;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, Guid companyId, string permission, CancellationToken cancellationToken = default);
}
