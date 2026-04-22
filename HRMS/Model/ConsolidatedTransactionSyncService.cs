using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record ConsolidatedTransactionPayload(
        string BeneficiaryId,
        string? CivilRegistryId,
        string ProjectCode,
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

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@beneficiary_id", ToDbValue(payload.BeneficiaryId));
            command.Parameters.AddWithValue("@civil_registry_id", ToDbValue(payload.CivilRegistryId));
            command.Parameters.AddWithValue("@project_code", ToDbValue(payload.ProjectCode));
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

        private static object ToDbValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }
}
