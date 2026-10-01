using System.Security.Cryptography;
using Application.Abstractions;

namespace Infrastructure.Security;

// Format: PBKDF2-SHA512$<iterations>$<base64 salt>$<base64 hash>.
// Storing the iteration count with each hash lets us raise it later without breaking existing users.
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2-SHA512";
    private const int Iterations = 210_000; // OWASP 2023 recommendation for PBKDF2-HMAC-SHA512
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);
            // Constant-time comparison so response timing doesn't leak how many bytes matched.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
