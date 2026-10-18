namespace Devzone.Application.Security;

public sealed record PasswordHash(string Algorithm, int Iterations, string Salt, string Hash, int Version = 1);
