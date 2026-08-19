using System.Security.Cryptography;

namespace Cmie.MotorTest.Wpf.Services;

internal static class PasswordHasher
{
    public const int DefaultIterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static (string Hash, string Salt, int Iterations) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt), DefaultIterations);
    }

    public static bool Verify(string password, string expectedHash, string salt, int iterations)
    {
        try
        {
            var expectedBytes = Convert.FromBase64String(expectedHash);
            var saltBytes = Convert.FromBase64String(salt);
            var actualBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                iterations,
                HashAlgorithmName.SHA256,
                expectedBytes.Length);
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
