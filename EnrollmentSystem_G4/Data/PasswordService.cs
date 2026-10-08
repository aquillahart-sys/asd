using System.Security.Cryptography;
using EnrollmentSystem_G4.Models;

namespace EnrollmentSystem_G4.Data
{
    public class PasswordService
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 210_000;

        public void HashPassword(User user, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            user.PasswordSalt = Convert.ToBase64String(salt);
            user.PasswordHash = Convert.ToBase64String(hash);
        }

        public bool VerifyPassword(User user, string password)
        {
            try
            {
                byte[] salt = Convert.FromBase64String(user.PasswordSalt);
                byte[] expectedHash = Convert.FromBase64String(user.PasswordHash);
                if (salt.Length != SaltSize || expectedHash.Length != HashSize)
                {
                    return false;
                }

                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    Iterations,
                    HashAlgorithmName.SHA256,
                    HashSize);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
