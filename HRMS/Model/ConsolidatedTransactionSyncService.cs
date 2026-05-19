using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record ConsolidatedTransactionPayload(
        string BeneficiaryId,
        string? CivilRegistryId,
        string ProjectCode,
        string? ProjectName,
        string OfficeId,
        string FullName,
        string FirstName,
        string? MiddleName,
        string LastName,
        string OfficeName,
        string TransactionType,
        decimal Amount,
        DateTime TransactionDate,
        string Status);

    public sealed class ConsolidatedTransactionSyncService
    {
        private const string TableName = "consolidated_transactions";
        private const string ProjectNameColumn = "project_name";
        private readonly string _ggmsConnectionString;

        public ConsolidatedTransactionSyncService(string ggmsConnectionString)
        {
            _ggmsConnectionString = string.IsNullOrWhiteSpace(ggmsConnectionString)
                ? throw new ArgumentException("GGMS connection string is required.", nameof(ggmsConnectionString))
                : ggmsConnectionString;
        }

        public async Task InsertAsync(ConsolidatedTransactionPayload payload)
        {
            const string sql = @"
INSERT INTO consolidated_transactions
(
    beneficiary_id,
    civil_registry_id,
    project_code,
    project_name,
    office_id,
    full_name,
    first_name,
    middle_name,
    last_name,
    office_name,
    transaction_type,
    amount,
    transaction_date,
    status
)
VALUES
(
    @beneficiary_id,
    @civil_registry_id,
    @project_code,
    @project_name,
    @office_id,
    @full_name,
    @first_name,
    @middle_name,
    @last_name,
    @office_name,
    @transaction_type,
    @amount,
    @transaction_date,
    @status
);";

            await using var connection = new MySqlConnection(_ggmsConnectionString);
            await connection.OpenAsync();
            await EnsureTableExistsAsync(connection);
            await EnsureProjectNameColumnAsync(connection);

            try
            {
                await ExecuteInsertAsync(connection, sql, payload);
            }
            catch (MySqlException ex) when (IsMissingTableError(ex))
            {
                await EnsureTableExistsAsync(connection);
                await EnsureProjectNameColumnAsync(connection);
                await ExecuteInsertAsync(connection, sql, payload);
            }
            catch (MySqlException ex) when (IsMissingProjectNameColumnError(ex))
            {
                await EnsureProjectNameColumnAsync(connection);
                await ExecuteInsertAsync(connection, sql, payload);
            }
        }

        private static async Task ExecuteInsertAsync(
            MySqlConnection connection,
            string sql,
            ConsolidatedTransactionPayload payload)
        {
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@beneficiary_id", ToDbValue(payload.BeneficiaryId));
            command.Parameters.AddWithValue("@civil_registry_id", ToDbValue(payload.CivilRegistryId));
            command.Parameters.AddWithValue("@project_code", ToDbValue(payload.ProjectCode));
            command.Parameters.AddWithValue("@project_name", ToDbValue(payload.ProjectName));
            command.Parameters.AddWithValue("@office_id", ToDbValue(payload.OfficeId));
            command.Parameters.AddWithValue("@full_name", ToDbValue(payload.FullName));
            command.Parameters.AddWithValue("@first_name", ToDbValue(payload.FirstName));
            command.Parameters.AddWithValue("@middle_name", ToDbValue(payload.MiddleName));
            command.Parameters.AddWithValue("@last_name", ToDbValue(payload.LastName));
            command.Parameters.AddWithValue("@office_name", ToDbValue(payload.OfficeName));
            command.Parameters.AddWithValue("@transaction_type", ToDbValue(payload.TransactionType));
            command.Parameters.AddWithValue("@amount", payload.Amount);
            command.Parameters.AddWithValue("@transaction_date", payload.TransactionDate.Date);
            command.Parameters.AddWithValue("@status", ToDbValue(payload.Status));
            await command.ExecuteNonQueryAsync();
        }

        private static async Task EnsureTableExistsAsync(MySqlConnection connection)
        {
            var sql = @"
CREATE TABLE IF NOT EXISTS consolidated_transactions (
    id                INT AUTO_INCREMENT PRIMARY KEY,
    beneficiary_id    VARCHAR(45) NULL,
    civil_registry_id VARCHAR(45) NULL,
    project_code      VARCHAR(45) NULL,
    project_name      VARCHAR(45) NULL,
    office_id         VARCHAR(45) NULL,
    full_name         VARCHAR(45) NULL,
    first_name        VARCHAR(45) NULL,
    middle_name       VARCHAR(45) NULL,
    last_name         VARCHAR(45) NULL,
    office_name       VARCHAR(45) NULL,
    transaction_type  VARCHAR(45) NULL,
    amount            DECIMAL(20,4) NULL,
    transaction_date  DATE NULL,
    status            VARCHAR(45) NULL,
    created_at        DATETIME DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task EnsureProjectNameColumnAsync(MySqlConnection connection)
        {
            if (await ColumnExistsAsync(connection, TableName, ProjectNameColumn))
            {
                return;
            }

            var sql = "ALTER TABLE consolidated_transactions ADD COLUMN project_name VARCHAR(45) NULL AFTER project_code;";
            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<bool> ColumnExistsAsync(
            MySqlConnection connection,
            string tableName,
            string columnName)
        {
            const string sql = @"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @table_name
  AND COLUMN_NAME = @column_name;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@table_name", tableName);
            command.Parameters.AddWithValue("@column_name", columnName);
            var result = await command.ExecuteScalarAsync();
            return result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
        }

        private static bool IsMissingTableError(MySqlException ex) =>
            ex.Number == 1146 &&
            ex.Message.Contains(TableName, StringComparison.OrdinalIgnoreCase);

        private static bool IsMissingProjectNameColumnError(MySqlException ex) =>
            ex.Number == 1054 &&
            ex.Message.Contains(ProjectNameColumn, StringComparison.OrdinalIgnoreCase);

        private static object ToDbValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }
}
