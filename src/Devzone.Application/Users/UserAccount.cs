namespace Devzone.Application.Users;

public sealed record UserAccount(
    Guid Id,
    Guid CompanyId,
    string Username,
    string DisplayName,
    string PasswordAlgorithm,
    int PasswordIterations,
    int PasswordVersion,
    string PasswordSalt,
    string PasswordHash,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);
