using MySqlConnector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record DbMigrationResult(
        int TotalMigrationFiles,
        int AppliedCount,
        IReadOnlyList<string> AppliedMigrations);

    /// <summary>
    /// Lightweight SQL-file migration runner.
    /// SQL files are applied in filename order and tracked in schema_migrations.
    /// </summary>
    public sealed class DbMigrationService
    {
        private readonly string _connectionString;
        private readonly string _migrationsPath;

        public DbMigrationService(string connectionString, string? migrationsPath = null)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? throw new ArgumentException("Connection string is required.", nameof(connectionString))
                : connectionString;

            _migrationsPath = string.IsNullOrWhiteSpace(migrationsPath)
                ? Path.Combine(AppContext.BaseDirectory, "Database", "Migrations")
                : migrationsPath;
        }

        public async Task<DbMigrationResult> ApplyPendingMigrationsAsync()
        {
            if (!Directory.Exists(_migrationsPath))
            {
                return new DbMigrationResult(0, 0, Array.Empty<string>());
            }

            var migrationFiles = Directory
                .GetFiles(_migrationsPath, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (migrationFiles.Length == 0)
            {
                return new DbMigrationResult(0, 0, Array.Empty<string>());
            }

            // Migration scripts use MySQL user variables (e.g. @sql, @EMP_ID_TYPE).
            var builder = new MySqlConnectionStringBuilder(_connectionString)
            {
                AllowUserVariables = true
            };

            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            await EnsureMigrationsTableAsync(connection);
            var appliedKeys = await GetAppliedMigrationKeysAsync(connection);

            var appliedNow = new List<string>();

            foreach (var migrationFile in migrationFiles)
            {
                var migrationKey = Path.GetFileName(migrationFile);
                if (string.IsNullOrWhiteSpace(migrationKey) || appliedKeys.Contains(migrationKey))
                {
                    continue;
                }

                var sql = await File.ReadAllTextAsync(migrationFile);

                if (!string.IsNullOrWhiteSpace(sql))
                {
                    try
                    {
                        await using var migrationCommand = new MySqlCommand(sql, connection);
                        await migrationCommand.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Database migration failed on '{migrationKey}'. {ex.Message}",
                            ex);
                    }
                }

                await MarkMigrationAppliedAsync(connection, migrationKey);
                appliedNow.Add(migrationKey);
            }

            return new DbMigrationResult(
                TotalMigrationFiles: migrationFiles.Length,
                AppliedCount: appliedNow.Count,
                AppliedMigrations: appliedNow);
        }

        private static async Task EnsureMigrationsTableAsync(MySqlConnection connection)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS schema_migrations (
    migration_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    migration_key VARCHAR(190) NOT NULL UNIQUE,
    applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;";

            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<HashSet<string>> GetAppliedMigrationKeysAsync(MySqlConnection connection)
        {
            const string sql = "SELECT migration_key FROM schema_migrations;";
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var key = reader["migration_key"]?.ToString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    keys.Add(key);
                }
            }

            return keys;
        }

        private static async Task MarkMigrationAppliedAsync(MySqlConnection connection, string migrationKey)
        {
            const string sql = @"
INSERT INTO schema_migrations (migration_key)
VALUES (@migration_key);";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@migration_key", migrationKey);
            await command.ExecuteNonQueryAsync();
        }
    }
}
