using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HRMS.Model
{
    public static class SensitiveIdProtector
    {
        private const string Prefix = "enc:v1:";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HRMS");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "security.config.json");
        private static readonly Lazy<byte[]> EncryptionKey = new(LoadOrCreateKey, true);

        public static string SettingsFile => SettingsFilePath;

        public static bool IsProtected(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Trim().StartsWith(Prefix, StringComparison.Ordinal);

        public static string? ProtectForStorage(string? value)
        {
            var normalized = Normalize(value);
            if (normalized == null)
            {
                return null;
            }

            if (IsProtected(normalized))
            {
                return normalized;
            }

            var plaintext = Encoding.UTF8.GetBytes(normalized);
            var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
            var cipher = new byte[plaintext.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];

            using var aes = new AesGcm(EncryptionKey.Value, AesGcm.TagByteSizes.MaxSize);
            aes.Encrypt(nonce, plaintext, cipher, tag);

            var payload = new byte[nonce.Length + tag.Length + cipher.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);
            return Prefix + Convert.ToBase64String(payload);
        }

        public static string? UnprotectToPlaintext(string? value)
        {
            var normalized = Normalize(value);
            if (normalized == null)
            {
                return null;
            }

            if (!IsProtected(normalized))
            {
                return normalized;
            }

            var payload = Convert.FromBase64String(normalized[Prefix.Length..]);
            var nonceLength = AesGcm.NonceByteSizes.MaxSize;
            var tagLength = AesGcm.TagByteSizes.MaxSize;

            if (payload.Length <= nonceLength + tagLength)
            {
                throw new InvalidOperationException("Protected value payload is invalid.");
            }

            var nonce = payload[..nonceLength];
            var tag = payload[nonceLength..(nonceLength + tagLength)];
            var cipher = payload[(nonceLength + tagLength)..];
            var plaintext = new byte[cipher.Length];

            using var aes = new AesGcm(EncryptionKey.Value, AesGcm.TagByteSizes.MaxSize);
            aes.Decrypt(nonce, cipher, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }

        public static string Mask(string? value)
        {
            var plaintext = Normalize(UnprotectToPlaintextSafe(value));
            if (plaintext == null)
            {
                return "-";
            }

            var visibleChars = Math.Min(4, plaintext.Length);
            if (plaintext.Length <= visibleChars)
            {
                return new string('•', plaintext.Length);
            }

            return new string('•', plaintext.Length - visibleChars) + plaintext[^visibleChars..];
        }

        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed == "-" ? null : trimmed;
        }

        private static string? UnprotectToPlaintextSafe(string? value)
        {
            try
            {
                return UnprotectToPlaintext(value);
            }
            catch
            {
                return Normalize(value);
            }
        }

        private static byte[] LoadOrCreateKey()
        {
            var env = Environment.GetEnvironmentVariable("HRMS_DATA_PROTECTION_KEY");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return ParseKey(env.Trim());
            }

            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<SecuritySettings>(json, JsonOptions);
                    if (!string.IsNullOrWhiteSpace(settings?.DataProtectionKey))
                    {
                        return ParseKey(settings.DataProtectionKey.Trim());
                    }
                }
            }
            catch
            {
                // Fall through to creating a new local key.
            }

            var generatedKey = RandomNumberGenerator.GetBytes(32);
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var settings = new SecuritySettings
                {
                    DataProtectionKey = Convert.ToBase64String(generatedKey)
                };
                File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
            }
            catch
            {
                // Keep the generated in-memory key even if the settings file cannot be written.
            }

            return generatedKey;
        }

        private static byte[] ParseKey(string value)
        {
            try
            {
                var parsed = Convert.FromBase64String(value);
                if (parsed.Length == 32)
                {
                    return parsed;
                }
            }
            catch
            {
                // Fall back to SHA256 key derivation below.
            }

            return SHA256.HashData(Encoding.UTF8.GetBytes(value));
        }

        private sealed class SecuritySettings
        {
            public string DataProtectionKey { get; set; } = string.Empty;
        }
    }
}
