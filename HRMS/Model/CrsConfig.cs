using System;
using System.Collections.Generic;
using System.IO;

namespace HRMS.Model
{
    public sealed class CrsConnectionSettings
    {
        public string Host { get; set; } = "127.0.0.1";
        public string Port { get; set; } = "3306";
        public string Database { get; set; } = "crs_db";
        public string Username { get; set; } = "hrms_app";
        public string Password { get; set; } = string.Empty;
    }

    public static class CrsConfig
    {
        private const string DefaultHost = "127.0.0.1";
        private const string DefaultPort = "3306";
        private const string DefaultDatabase = "crs_db";
        private const string DefaultUser = "hrms_app";
        private const string DefaultPassword = "";

        private static readonly string TextSettingsFilePath = Path.Combine(AppContext.BaseDirectory, "CrsConfig.txt");

        public static string ConnectionString => BuildConnectionString(GetSettings());

        public static string GetTextSettingsFilePath() => TextSettingsFilePath;

        public static CrsConnectionSettings GetSettings()
        {
            var fileSettings = LoadFromTextFile();
            return Normalize(new CrsConnectionSettings
            {
                Host = Get("CRS_DB_HOST", fileSettings?.Host ?? DefaultHost),
                Port = Get("CRS_DB_PORT", fileSettings?.Port ?? DefaultPort),
                Database = Get("CRS_DB_NAME", fileSettings?.Database ?? DefaultDatabase),
                Username = Get("CRS_DB_USER", fileSettings?.Username ?? DefaultUser),
                Password = GetPassword(fileSettings?.Password)
            });
        }

        public static void SaveSettings(CrsConnectionSettings settings)
        {
            var normalized = Normalize(settings);
            var lines = new[]
            {
                "# CRS Database Connection Settings",
                $"Server={normalized.Host}",
                $"Port={normalized.Port}",
                $"Database={normalized.Database}",
                $"User={normalized.Username}",
                $"ProtectedPassword={SensitiveIdProtector.ProtectForStorage(normalized.Password) ?? string.Empty}"
            };
            File.WriteAllLines(TextSettingsFilePath, lines);
            ApplyToProcessEnvironment(normalized);
        }

        public static string BuildConnectionString(CrsConnectionSettings settings)
        {
            var normalized = Normalize(settings);
            var isLocalHost = IsLocalHost(normalized.Host);
            var sslMode = isLocalHost ? "Preferred" : "Required";
            return
                $"Server={normalized.Host};" +
                $"Port={normalized.Port};" +
                $"Database={normalized.Database};" +
                $"Uid={normalized.Username};" +
                $"Pwd={normalized.Password};" +
                $"SslMode={sslMode};" +
                (isLocalHost ? "AllowPublicKeyRetrieval=True;" : string.Empty) +
                "AllowZeroDateTime=True;" +
                "ConvertZeroDateTime=True;" +
                "AllowUserVariables=True;" +
                "Pooling=True;" +
                "MinimumPoolSize=1;" +
                "MaximumPoolSize=12;" +
                "ConnectionIdleTimeout=3600;" +
                "ConnectionReset=True;" +
                "DefaultCommandTimeout=180;" +
                "Keepalive=30;";
        }

        public static void ApplyToProcessEnvironment(CrsConnectionSettings settings)
        {
            var normalized = Normalize(settings);
            Environment.SetEnvironmentVariable("CRS_DB_HOST", normalized.Host, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("CRS_DB_PORT", normalized.Port, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("CRS_DB_NAME", normalized.Database, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("CRS_DB_USER", normalized.Username, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("CRS_DB_PASSWORD", normalized.Password, EnvironmentVariableTarget.Process);
        }

        private static CrsConnectionSettings? LoadFromTextFile()
        {
            try
            {
                if (!File.Exists(TextSettingsFilePath))
                {
                    return null;
                }

                var lines = File.ReadAllLines(TextSettingsFilePath);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    var line = raw.Trim();
                    if (line.StartsWith("#") || line.StartsWith(";"))
                    {
                        continue;
                    }

                    var idx = line.IndexOf('=');
                    if (idx <= 0 || idx == line.Length - 1)
                    {
                        continue;
                    }

                    var key = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        map[key] = value;
                    }
                }

                if (map.Count == 0)
                {
                    return null;
                }

                return Normalize(new CrsConnectionSettings
                {
                    Host = ReadValue(map, "Server", "Host") ?? string.Empty,
                    Port = ReadValue(map, "Port") ?? string.Empty,
                    Database = ReadValue(map, "Database", "Db", "Name") ?? string.Empty,
                    Username = ReadValue(map, "User", "Username", "Uid") ?? string.Empty,
                    Password = ReadStoredPassword(map)
                });
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadValue(IReadOnlyDictionary<string, string> map, params string[] keys)
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

        private static CrsConnectionSettings Normalize(CrsConnectionSettings? settings)
        {
            settings ??= new CrsConnectionSettings();

            return new CrsConnectionSettings
            {
                Host = string.IsNullOrWhiteSpace(settings.Host) ? DefaultHost : settings.Host.Trim(),
                Port = string.IsNullOrWhiteSpace(settings.Port) ? DefaultPort : settings.Port.Trim(),
                Database = string.IsNullOrWhiteSpace(settings.Database) ? DefaultDatabase : settings.Database.Trim(),
                Username = string.IsNullOrWhiteSpace(settings.Username) ? DefaultUser : settings.Username.Trim(),
                Password = settings.Password?.Trim() ?? DefaultPassword
            };
        }

        private static string Get(string key, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string GetPassword(string? filePassword)
        {
            var plainEnvironmentValue = Environment.GetEnvironmentVariable("CRS_DB_PASSWORD");
            if (!string.IsNullOrWhiteSpace(plainEnvironmentValue))
            {
                return plainEnvironmentValue.Trim();
            }

            var protectedEnvironmentValue = Environment.GetEnvironmentVariable("CRS_DB_PASSWORD_PROTECTED");
            if (!string.IsNullOrWhiteSpace(protectedEnvironmentValue))
            {
                return SensitiveIdProtector.UnprotectToPlaintext(protectedEnvironmentValue.Trim()) ?? string.Empty;
            }

            return filePassword ?? DefaultPassword;
        }

        private static string ReadStoredPassword(IReadOnlyDictionary<string, string> map)
        {
            var protectedValue = ReadValue(map, "ProtectedPassword", "PasswordProtected");
            if (!string.IsNullOrWhiteSpace(protectedValue))
            {
                return SensitiveIdProtector.UnprotectToPlaintext(protectedValue.Trim()) ?? string.Empty;
            }

            var legacyPlaintextValue = ReadValue(map, "Password", "Pwd");
            if (string.IsNullOrWhiteSpace(legacyPlaintextValue))
            {
                return string.Empty;
            }

            return SensitiveIdProtector.IsProtected(legacyPlaintextValue)
                ? SensitiveIdProtector.UnprotectToPlaintext(legacyPlaintextValue.Trim()) ?? string.Empty
                : legacyPlaintextValue.Trim();
        }

        private static bool IsLocalHost(string? host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return true;
            }

            return host.Trim().ToLowerInvariant() switch
            {
                "localhost" => true,
                "127.0.0.1" => true,
                "::1" => true,
                _ => false
            };
        }
    }
}
