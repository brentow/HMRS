using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HRMS.Model
{
    public sealed class DbConnectionSettings
    {
        public string Host { get; set; } = "srv1237.hstgr.io";
        public string Port { get; set; } = "3306";
        public string Database { get; set; } = "u621755393_hrms3b";
        public string Username { get; set; } = "u621755393_hrms3b_user";
        public string Password { get; set; } = "Hrms3b@2026";
    }

    /// <summary>
    /// Central place for connection-related settings so ViewModels share the same string.
    /// Supports local text/json config and environment variables.
    /// </summary>
    public static class DbConfig
    {
        private const string DefaultHost = "srv1237.hstgr.io";
        private const string DefaultPort = "3306";
        private const string DefaultDatabase = "u621755393_hrms3b";
        private const string DefaultUser = "u621755393_hrms3b_user";
        private const string DefaultPassword = "Hrms3b@2026";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HRMS");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "database.config.json");
        private static readonly string TextSettingsFilePath = Path.Combine(AppContext.BaseDirectory, "DatabaseConfig.txt");

        public static string ConnectionString => BuildConnectionString(GetSettings());

        public static DbConnectionSettings GetSettings()
        {
            var textSettings = LoadFromTextFile();
            if (textSettings != null)
            {
                return Normalize(textSettings);
            }

            var fileSettings = LoadFromFile();
            if (fileSettings != null)
            {
                return Normalize(fileSettings);
            }

            return Normalize(new DbConnectionSettings
            {
                Host = Get("HRMS_DB_HOST", DefaultHost),
                Port = Get("HRMS_DB_PORT", DefaultPort),
                Database = Get("HRMS_DB_NAME", DefaultDatabase),
                Username = Get("HRMS_DB_USER", DefaultUser),
                Password = Get("HRMS_DB_PASSWORD", DefaultPassword)
            });
        }

        public static string GetSettingsFilePath() => SettingsFilePath;
        public static string GetTextSettingsFilePath() => TextSettingsFilePath;

        public static void SaveSettings(DbConnectionSettings settings)
        {
            var normalized = Normalize(settings);
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
            TrySaveToTextFile(normalized);
            ApplyToProcessEnvironment(normalized);
        }

        public static string BuildConnectionString(DbConnectionSettings settings)
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

        public static void ApplyToProcessEnvironment(DbConnectionSettings settings)
        {
            var normalized = Normalize(settings);
            Environment.SetEnvironmentVariable("HRMS_DB_HOST", normalized.Host, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("HRMS_DB_PORT", normalized.Port, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("HRMS_DB_NAME", normalized.Database, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("HRMS_DB_USER", normalized.Username, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("HRMS_DB_PASSWORD", normalized.Password, EnvironmentVariableTarget.Process);
        }

        private static DbConnectionSettings? LoadFromFile()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return null;
                }

                var json = File.ReadAllText(SettingsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<DbConnectionSettings>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static DbConnectionSettings? LoadFromTextFile()
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

                return Normalize(new DbConnectionSettings
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

        private static void TrySaveToTextFile(DbConnectionSettings settings)
        {
            try
            {
                if (!File.Exists(TextSettingsFilePath))
                {
                    return;
                }

                var lines = new[]
                {
                    "# HRMS Database Connection Settings",
                    $"Server={settings.Host}",
                    $"Port={settings.Port}",
                    $"Database={settings.Database}",
                    $"User={settings.Username}",
                    $"Password={settings.Password}"
                };
                File.WriteAllLines(TextSettingsFilePath, lines);
            }
            catch
            {
                // Ignore write issues for read-only folders.
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

        private static DbConnectionSettings Normalize(DbConnectionSettings? settings)
        {
            settings ??= new DbConnectionSettings();

            return new DbConnectionSettings
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
