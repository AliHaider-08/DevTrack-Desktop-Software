namespace Devzone.Application.Security;

public sealed record AuthenticatedUser(Guid Id, Guid CompanyId, string Username, IReadOnlySet<string> Permissions);

public interface IUserAuthenticator
{
    Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
}
