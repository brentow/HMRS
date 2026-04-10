using System;
using System.Collections.Generic;
using System.IO;

namespace HRMS.Model
{
    public sealed class GgmsConnectionSettings
    {
        public string Host { get; set; } = "194.59.164.58";
        public string Port { get; set; } = "3306";
        public string Database { get; set; } = "u621755393_ggms";
        public string Username { get; set; } = "u621755393_ggms_user";
        public string Password { get; set; } = "Ggms@2026";
    }

    public static class GgmsConfig
    {
        private const string DefaultHost = "194.59.164.58";
        private const string DefaultPort = "3306";
        private const string DefaultDatabase = "u621755393_ggms";
        private const string DefaultUser = "u621755393_ggms_user";
        private const string DefaultPassword = "Ggms@2026";

        private static readonly string TextSettingsFilePath = Path.Combine(AppContext.BaseDirectory, "GgmsConfig.txt");

        public static string ConnectionString => BuildConnectionString(GetSettings());

        public static string GetTextSettingsFilePath() => TextSettingsFilePath;

        public static GgmsConnectionSettings GetSettings()
        {
            var fileSettings = LoadFromTextFile();
            if (fileSettings != null)
            {
                return Normalize(fileSettings);
            }

            return Normalize(new GgmsConnectionSettings
            {
                Host = Get("GGMS_DB_HOST", DefaultHost),
                Port = Get("GGMS_DB_PORT", DefaultPort),
                Database = Get("GGMS_DB_NAME", DefaultDatabase),
                Username = Get("GGMS_DB_USER", DefaultUser),
                Password = Get("GGMS_DB_PASSWORD", DefaultPassword)
            });
        }

        public static void SaveSettings(GgmsConnectionSettings settings)
        {
            var normalized = Normalize(settings);
            var lines = new[]
            {
                "# GGMS Database Connection Settings",
                $"Server={normalized.Host}",
                $"Port={normalized.Port}",
                $"Database={normalized.Database}",
                $"User={normalized.Username}",
                $"Password={normalized.Password}"
            };
            File.WriteAllLines(TextSettingsFilePath, lines);
            ApplyToProcessEnvironment(normalized);
        }

        public static string BuildConnectionString(GgmsConnectionSettings settings)
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

        public static void ApplyToProcessEnvironment(GgmsConnectionSettings settings)
        {
            var normalized = Normalize(settings);
            Environment.SetEnvironmentVariable("GGMS_DB_HOST", normalized.Host, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GGMS_DB_PORT", normalized.Port, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GGMS_DB_NAME", normalized.Database, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GGMS_DB_USER", normalized.Username, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GGMS_DB_PASSWORD", normalized.Password, EnvironmentVariableTarget.Process);
        }

        private static GgmsConnectionSettings? LoadFromTextFile()
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

                return Normalize(new GgmsConnectionSettings
                {
                    Host = ReadValue(map, "Server", "Host") ?? string.Empty,
                    Port = ReadValue(map, "Port") ?? string.Empty,
                    Database = ReadValue(map, "Database", "Db", "Name") ?? string.Empty,
                    Username = ReadValue(map, "User", "Username", "Uid") ?? string.Empty,
                    Password = ReadValue(map, "Password", "Pwd") ?? string.Empty
                });
            }
            catch
            {
                return null;
            }
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

        private static GgmsConnectionSettings Normalize(GgmsConnectionSettings? settings)
        {
            settings ??= new GgmsConnectionSettings();

            return new GgmsConnectionSettings
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
