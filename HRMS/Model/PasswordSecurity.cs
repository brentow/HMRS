using System;
using System.Security.Cryptography;
using System.Text;

namespace HRMS.Model
{
    public static class PasswordSecurity
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int DefaultIterations = 120000;
        private const string Scheme = "pbkdf2_sha256";

        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password is required.", nameof(password));
            }

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                DefaultIterations,
                HashAlgorithmName.SHA256,
                KeySize);

            return $"{Scheme}${DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public static bool VerifyPassword(string password, string? hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }

            var parts = hash.Split('$');
            if (parts.Length != 4 ||
                !string.Equals(parts[0], Scheme, StringComparison.Ordinal) ||
                !int.TryParse(parts[1], out var iterations) ||
                iterations <= 0)
            {
                return false;
            }

            byte[] salt;
            byte[] expectedKey;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expectedKey = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actualKey = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedKey.Length);

            return CryptographicOperations.FixedTimeEquals(expectedKey, actualKey);
        }

        public static string GenerateTemporaryPassword(int length = 12)
        {
            if (length < 10)
            {
                length = 10;
            }

            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@#$%*-_";
            var all = upper + lower + digits + symbols;

            Span<char> result = stackalloc char[length];
            result[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
            result[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
            result[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
            result[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

            for (var i = 4; i < length; i++)
            {
                result[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
            }

            for (var i = result.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }

            return new string(result);
        }
    }
}
