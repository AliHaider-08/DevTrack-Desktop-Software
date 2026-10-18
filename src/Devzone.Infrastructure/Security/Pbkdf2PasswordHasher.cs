using System.Security.Cryptography;
using Devzone.Application.Security;

namespace Devzone.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int CurrentIterations = 600_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string CurrentAlgorithm = "PBKDF2-SHA256";

    public PasswordHash Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length < 12)
        {
            throw new ArgumentException("Passwords must contain at least 12 characters.", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, HashAlgorithmName.SHA256, HashBytes);
        return new PasswordHash(CurrentAlgorithm, CurrentIterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, PasswordHash passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(passwordHash);
        if (passwordHash.Version != 1 || passwordHash.Algorithm != CurrentAlgorithm ||
            passwordHash.Iterations is < 100_000 or > 2_000_000)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(passwordHash.Salt);
            var expected = Convert.FromBase64String(passwordHash.Hash);
            if (salt.Length != SaltBytes || expected.Length != HashBytes)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, passwordHash.Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
