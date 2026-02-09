

using System.Security.Cryptography;

namespace FinancialApp.Backend.Security;

public class PasswordHasher
{   
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static (string Hash, string Salt) Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySize
        );

        return (Convert.ToBase64String(hash),
            Convert.ToBase64String(salt));
    }

    public static bool Verify(
        string password,
        string storedHash,
        string storedSalt)
    {
        byte[] salt = Convert.FromBase64String(storedSalt);
        byte[] expectedHash = Convert.FromBase64String(storedHash);

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySize);

        return CryptographicOperations.FixedTimeEquals(
            actualHash,
            expectedHash);
    }
}
