using System.Security.Cryptography;

namespace RogueLikeNet.Server;

/// <summary>
/// Cryptographically secure password hashing using PBKDF2-HMAC-SHA256.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16; // 128-bit
    private const int HashSize = 32; // 256-bit
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    private const string VERSION_1 = "v1"; // Version prefix for stored hashes to allow future algorithm changes

    /// <summary>
    /// Hashes a password with a random salt. Returns empty strings for empty passwords (no password set).
    /// </summary>
    public static (string Hash, string Salt) HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return ("", "");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return (VERSION_1 + Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    /// <summary>
    /// Verifies a password against a stored hash and salt.
    /// Empty stored hash means no password was set — always returns true.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(storedHash))
            return true; // No password set — accept any

        if (string.IsNullOrEmpty(password))
            return false; // Password required but none provided

        if (!storedHash.StartsWith(VERSION_1))
            throw new NotSupportedException("Unsupported password hash version");

        var hashPart = storedHash.Substring(VERSION_1.Length);
        if (string.IsNullOrEmpty(hashPart) || string.IsNullOrEmpty(storedSalt))
            return false; // Invalid stored hash/salt

        var salt = Convert.FromBase64String(storedSalt);
        var expectedHash = Convert.FromBase64String(hashPart);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
