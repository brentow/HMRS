using System;
using System.Collections.Generic;
using System.IO;

namespace HRMS.Model
{
    public sealed class BrevoOtpSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string SenderName { get; set; } = "HRMS Security";
        public string SenderEmail { get; set; } = string.Empty;
        public string RecipientName { get; set; } = "HRMS Administrator";
        public string RecipientEmail { get; set; } = string.Empty;
        public int OtpLength { get; set; } = 6;
        public int OtpTtlMinutes { get; set; } = 5;
        public int AccessWindowMinutes { get; set; } = 10;
        public int ResendCooldownSeconds { get; set; } = 45;
        public bool SandboxMode { get; set; }
    }

    public static class BrevoOtpConfig
    {
        private static readonly string TextSettingsFilePath = Path.Combine(AppContext.BaseDirectory, "BrevoOtpConfig.txt");

        public static string GetTextSettingsFilePath() => TextSettingsFilePath;

        public static BrevoOtpSettings GetSettings()
        {
            var map = LoadFromTextFile();

            return Normalize(new BrevoOtpSettings
            {
                ApiKey = ReadApiKey(map),
                SenderName = ReadString(map, "HRMS_BREVO_SENDER_NAME", "HRMS Security", "SenderName"),
                SenderEmail = ReadString(map, "HRMS_BREVO_SENDER_EMAIL", string.Empty, "SenderEmail"),
                RecipientName = ReadString(map, "HRMS_BREVO_RECIPIENT_NAME", "HRMS Administrator", "RecipientName"),
                RecipientEmail = ReadString(map, "HRMS_BREVO_RECIPIENT_EMAIL", string.Empty, "RecipientEmail"),
                OtpLength = ReadInt(map, "HRMS_BREVO_OTP_LENGTH", 6, "OtpLength"),
                OtpTtlMinutes = ReadInt(map, "HRMS_BREVO_OTP_TTL_MINUTES", 5, "OtpTtlMinutes"),
                AccessWindowMinutes = ReadInt(map, "HRMS_BREVO_ACCESS_WINDOW_MINUTES", 10, "AccessWindowMinutes"),
                ResendCooldownSeconds = ReadInt(map, "HRMS_BREVO_RESEND_COOLDOWN_SECONDS", 45, "ResendCooldownSeconds"),
                SandboxMode = ReadBool(map, "HRMS_BREVO_SANDBOX_MODE", false, "SandboxMode")
            });
        }

        public static bool TryValidate(BrevoOtpSettings settings, out string message)
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                message = $"Missing Brevo API key. Fill HRMS_BREVO_API_KEY or ApiKey in {Path.GetFileName(TextSettingsFilePath)}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.SenderEmail))
            {
                message = $"Missing Brevo sender email. Fill HRMS_BREVO_SENDER_EMAIL or SenderEmail in {Path.GetFileName(TextSettingsFilePath)}.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static Dictionary<string, string> LoadFromTextFile()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(TextSettingsFilePath))
                {
                    return map;
                }

                foreach (var raw in File.ReadAllLines(TextSettingsFilePath))
                {
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    var line = raw.Trim();
                    if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
                    {
                        continue;
                    }

                    var key = line[..separatorIndex].Trim();
                    var value = line[(separatorIndex + 1)..].Trim();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        map[key] = value;
                    }
                }
            }
            catch
            {
                // Keep empty map when the file cannot be read.
            }

            return map;
        }

        private static string ReadApiKey(Dictionary<string, string> map)
        {
            var envPlain = Environment.GetEnvironmentVariable("HRMS_BREVO_API_KEY");
            if (!string.IsNullOrWhiteSpace(envPlain))
            {
                return envPlain.Trim();
            }

            var envProtected = Environment.GetEnvironmentVariable("HRMS_BREVO_API_KEY_PROTECTED");
            if (!string.IsNullOrWhiteSpace(envProtected))
            {
                return SensitiveIdProtector.UnprotectToPlaintext(envProtected.Trim()) ?? string.Empty;
            }

            var protectedFileValue = ReadValue(map, "ProtectedApiKey", "ApiKeyProtected");
            if (!string.IsNullOrWhiteSpace(protectedFileValue))
            {
                return SensitiveIdProtector.UnprotectToPlaintext(protectedFileValue.Trim()) ?? string.Empty;
            }

            var fileValue = ReadValue(map, "ApiKey");
            if (!string.IsNullOrWhiteSpace(fileValue))
            {
                return SensitiveIdProtector.IsProtected(fileValue)
                    ? SensitiveIdProtector.UnprotectToPlaintext(fileValue.Trim()) ?? string.Empty
                    : fileValue.Trim();
            }

            return string.Empty;
        }

        private static string ReadString(Dictionary<string, string> map, string envKey, string fallback, params string[] fileKeys)
        {
            var env = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env.Trim();
            }

            var fileValue = ReadValue(map, fileKeys);
            return string.IsNullOrWhiteSpace(fileValue) ? fallback : fileValue.Trim();
        }

        private static int ReadInt(Dictionary<string, string> map, string envKey, int fallback, params string[] fileKeys)
        {
            var env = Environment.GetEnvironmentVariable(envKey);
            if (int.TryParse(env, out var envValue))
            {
                return envValue;
            }

            var fileValue = ReadValue(map, fileKeys);
            return int.TryParse(fileValue, out var parsed) ? parsed : fallback;
        }

        private static bool ReadBool(Dictionary<string, string> map, string envKey, bool fallback, params string[] fileKeys)
        {
            var env = Environment.GetEnvironmentVariable(envKey);
            if (bool.TryParse(env, out var envValue))
            {
                return envValue;
            }

            var fileValue = ReadValue(map, fileKeys);
            return bool.TryParse(fileValue, out var parsed) ? parsed : fallback;
        }

        private static string? ReadValue(Dictionary<string, string> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(key, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static BrevoOtpSettings Normalize(BrevoOtpSettings settings)
        {
            return new BrevoOtpSettings
            {
                ApiKey = settings.ApiKey?.Trim() ?? string.Empty,
                SenderName = string.IsNullOrWhiteSpace(settings.SenderName) ? "HRMS Security" : settings.SenderName.Trim(),
                SenderEmail = settings.SenderEmail?.Trim() ?? string.Empty,
                RecipientName = string.IsNullOrWhiteSpace(settings.RecipientName) ? "HRMS Administrator" : settings.RecipientName.Trim(),
                RecipientEmail = settings.RecipientEmail?.Trim() ?? string.Empty,
                OtpLength = Math.Clamp(settings.OtpLength, 4, 8),
                OtpTtlMinutes = Math.Clamp(settings.OtpTtlMinutes, 2, 15),
                AccessWindowMinutes = Math.Clamp(settings.AccessWindowMinutes, 1, 30),
                ResendCooldownSeconds = Math.Clamp(settings.ResendCooldownSeconds, 15, 180),
                SandboxMode = settings.SandboxMode
            };
        }
    }
}
