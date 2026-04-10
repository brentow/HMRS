using MySqlConnector;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HRMS.Model
{
    internal static class BeneficiaryStagingSchemaCompatibility
    {
        private static readonly SemaphoreSlim SchemaLock = new(1, 1);
        private static bool _schemaEnsured;

        public static async Task EnsureAsync(MySqlConnection connection)
        {
            ArgumentNullException.ThrowIfNull(connection);

            if (_schemaEnsured)
            {
                return;
            }

            await SchemaLock.WaitAsync();
            try
            {
                if (_schemaEnsured)
                {
                    return;
                }

                if (!await TableExistsAsync(connection, "BeneficiaryStaging"))
                {
                    return;
                }

                await EnsureColumnAsync(connection, "Remarks", "VARCHAR(500) NULL");
                await EnsureColumnAsync(connection, "ApprovedRejectedAt", "DATETIME NULL");
                await EnsureColumnAsync(connection, "ResidentsId", "BIGINT NULL");
                await EnsureColumnAsync(connection, "BeneficiaryId", "VARCHAR(255) NULL");
                await EnsureColumnAsync(connection, "FullName", "VARCHAR(255) NULL");
                await EnsureColumnAsync(connection, "Sex", "VARCHAR(50) NULL");
                await EnsureColumnAsync(connection, "DateOfBirth", "VARCHAR(100) NULL");
                await EnsureColumnAsync(connection, "Age", "VARCHAR(20) NULL");
                await EnsureColumnAsync(connection, "MaritalStatus", "VARCHAR(100) NULL");
                await EnsureColumnAsync(connection, "IsPwd", "TINYINT(1) NOT NULL DEFAULT 0");
                await EnsureColumnAsync(connection, "PwdIdNo", "VARCHAR(255) NULL");
                await EnsureColumnAsync(connection, "DisabilityType", "VARCHAR(255) NULL");
                await EnsureColumnAsync(connection, "CauseOfDisability", "VARCHAR(255) NULL");
                await EnsureColumnAsync(connection, "IsSenior", "TINYINT(1) NOT NULL DEFAULT 0");
                await EnsureColumnAsync(connection, "SeniorIdNo", "VARCHAR(255) NULL");

                var masterIdType = await GetUserAccountIdColumnTypeAsync(connection);
                await EnsureColumnAsync(connection, "MasterID", $"{masterIdType} NULL");

                await EnsureIndexAsync(connection, "idx_beneficiary_staging_verification", "VerificationStatus");
                await EnsureIndexAsync(connection, "idx_beneficiary_staging_beneficiary_id", "BeneficiaryId");
                await EnsureIndexAsync(connection, "idx_beneficiary_masterid", "MasterID");

                _schemaEnsured = true;
            }
            finally
            {
                SchemaLock.Release();
            }
        }

        private static async Task<bool> TableExistsAsync(MySqlConnection connection, string tableName)
        {
            const string sql = @"
SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @table_name
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@table_name", tableName);
            var result = await command.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task EnsureColumnAsync(MySqlConnection connection, string columnName, string definition)
        {
            if (await ColumnExistsAsync(connection, columnName))
            {
                return;
            }

            var sql = $"ALTER TABLE BeneficiaryStaging ADD COLUMN {columnName} {definition};";
            await ExecuteAlterIgnoringDuplicatesAsync(connection, sql);
        }

        private static async Task<bool> ColumnExistsAsync(MySqlConnection connection, string columnName)
        {
            const string sql = @"
SELECT 1
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BeneficiaryStaging'
  AND COLUMN_NAME = @column_name
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@column_name", columnName);
            var result = await command.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task EnsureIndexAsync(MySqlConnection connection, string indexName, string columnName)
        {
            if (!await ColumnExistsAsync(connection, columnName) || await IndexExistsAsync(connection, indexName))
            {
                return;
            }

            var sql = $"ALTER TABLE BeneficiaryStaging ADD INDEX {indexName} ({columnName});";
            await ExecuteAlterIgnoringDuplicatesAsync(connection, sql);
        }

        private static async Task<bool> IndexExistsAsync(MySqlConnection connection, string indexName)
        {
            const string sql = @"
SELECT 1
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BeneficiaryStaging'
  AND INDEX_NAME = @index_name
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@index_name", indexName);
            var result = await command.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task<string> GetUserAccountIdColumnTypeAsync(MySqlConnection connection)
        {
            const string sql = @"
SELECT COLUMN_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'user_accounts'
  AND COLUMN_NAME = 'user_id'
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            var result = (await command.ExecuteScalarAsync())?.ToString();
            return string.IsNullOrWhiteSpace(result) ? "INT" : result.Trim();
        }

        private static async Task ExecuteAlterIgnoringDuplicatesAsync(MySqlConnection connection, string sql)
        {
            try
            {
                await using var command = new MySqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex) when (ex.Number == 1060 || ex.Number == 1061)
            {
                // Another app instance may have added the same column or index after the existence check.
            }
        }
    }
}
