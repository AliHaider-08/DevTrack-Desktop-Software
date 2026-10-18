namespace Devzone.Application.Users;

public interface IUserAccountService
{
    Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task CreateAsync(UserAccount account, CancellationToken cancellationToken = default);

    Task RecordLoginAsync(Guid userId, DateTimeOffset occurredAt, CancellationToken cancellationToken = default);
}
