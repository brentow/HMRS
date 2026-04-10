using MySqlConnector;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record GgmsBudgetAllocationDto(
        long AllocationId,
        string Program,
        decimal AllocatedAmount,
        decimal UsedAmount,
        decimal RemainingAmount,
        string Status);

    public sealed record GgmsDisbursementResult(
        long TransactionId,
        decimal RemainingAfter);

    public sealed class GgmsFundAllocationService
    {
        private readonly string _connectionString;
        private readonly long _officeId;
        private readonly string _officeCode;

        public GgmsFundAllocationService(string connectionString, long officeId, string officeCode)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? throw new ArgumentException("GGMS connection string is required.", nameof(connectionString))
                : connectionString;
            _officeId = officeId > 0
                ? officeId
                : throw new ArgumentOutOfRangeException(nameof(officeId), "Office ID must be greater than zero.");
            _officeCode = string.IsNullOrWhiteSpace(officeCode)
                ? throw new ArgumentException("Office code is required.", nameof(officeCode))
                : officeCode.Trim();
        }

        public async Task<GgmsBudgetAllocationDto?> GetActiveAllocationAsync()
        {
            const string sql = @"
SELECT
    id,
    @office_code AS program,
    COALESCE(AllocatedAmount, 0) AS allocated_amount,
    COALESCE(SpentAmount, 0) AS used_amount,
    (COALESCE(AllocatedAmount, 0) - COALESCE(SpentAmount, 0)) AS remaining_amount,
    'active' AS status
FROM officeallocations
WHERE office_code = @office_code
ORDER BY YearlyBudgetId DESC, id DESC
LIMIT 1;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@office_code", _officeCode);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new GgmsBudgetAllocationDto(
                AllocationId: ToLong(reader["id"]),
                Program: reader["program"]?.ToString() ?? "General Payroll",
                AllocatedAmount: ToDecimal(reader["allocated_amount"]),
                UsedAmount: ToDecimal(reader["used_amount"]),
                RemainingAmount: ToDecimal(reader["remaining_amount"]),
                Status: reader["status"]?.ToString() ?? "active");
        }

        public async Task<GgmsDisbursementResult> RecordPayrollDisbursementAsync(
            long allocationId,
            decimal amount,
            string recipientName,
            string purpose,
            string? description)
        {
            if (allocationId <= 0)
            {
                throw new InvalidOperationException("Invalid GGMS allocation ID.");
            }

            if (amount <= 0)
            {
                throw new InvalidOperationException("Disbursement amount must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(recipientName))
            {
                throw new InvalidOperationException("Recipient name is required.");
            }

            if (string.IsNullOrWhiteSpace(purpose))
            {
                throw new InvalidOperationException("Purpose is required.");
            }

            const string lockAllocationSql = @"
SELECT
        COALESCE(AllocatedAmount, 0) AS allocated_amount,
        COALESCE(SpentAmount, 0) AS used_amount,
        (COALESCE(AllocatedAmount, 0) - COALESCE(SpentAmount, 0)) AS remaining_amount
FROM officeallocations
WHERE id = @allocation_id
    AND office_code = @office_code
LIMIT 1
FOR UPDATE;";

            const string updateAllocationSql = @"
UPDATE officeallocations
SET SpentAmount = COALESCE(SpentAmount, 0) + @amount
WHERE id = @allocation_id
    AND office_code = @office_code;";

            const string insertTransactionSql = @"
INSERT INTO tbl_transaction
(
    office_id,
    status,
    amount,
    purpose,
    recipient_type,
    recipient_name,
    description,
    priority,
    date_applied_,
    created_at,
    updated_at
)
VALUES
(
    @office_id,
    'pending',
    @amount,
    @purpose,
    'individual',
    @recipient_name,
    @description,
    'Normal',
    NOW(),
    NOW(),
    NOW()
);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                decimal remainingBefore;

                await using (var lockCommand = new MySqlCommand(lockAllocationSql, connection, transaction))
                {
                    lockCommand.Parameters.AddWithValue("@allocation_id", allocationId);
                    lockCommand.Parameters.AddWithValue("@office_code", _officeCode);

                    await using var reader = await lockCommand.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        throw new InvalidOperationException("No active GGMS allocation found for this office.");
                    }

                    remainingBefore = ToDecimal(reader["remaining_amount"]);
                }

                if (amount > remainingBefore)
                {
                    throw new InvalidOperationException(
                        $"Amount exceeds remaining GGMS allocation. Remaining: PHP {remainingBefore:N2}, Requested: PHP {amount:N2}.");
                }

                await using (var updateCommand = new MySqlCommand(updateAllocationSql, connection, transaction))
                {
                    updateCommand.Parameters.AddWithValue("@amount", amount);
                    updateCommand.Parameters.AddWithValue("@allocation_id", allocationId);
                    updateCommand.Parameters.AddWithValue("@office_code", _officeCode);
                    await updateCommand.ExecuteNonQueryAsync();
                }

                long transactionId;
                await using (var txCommand = new MySqlCommand(insertTransactionSql, connection, transaction))
                {
                    txCommand.Parameters.AddWithValue("@office_id", _officeId);
                    txCommand.Parameters.AddWithValue("@amount", amount);
                    txCommand.Parameters.AddWithValue("@purpose", purpose.Trim());
                    txCommand.Parameters.AddWithValue("@recipient_name", recipientName.Trim());
                    txCommand.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(description) ? DBNull.Value : description.Trim());
                    await txCommand.ExecuteNonQueryAsync();
                    transactionId = txCommand.LastInsertedId;
                }

                await transaction.CommitAsync();
                return new GgmsDisbursementResult(
                    TransactionId: transactionId,
                    RemainingAfter: remainingBefore - amount);
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                }

                throw;
            }
        }

        private static long ToLong(object value) =>
            value == DBNull.Value || value == null ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);

        private static decimal ToDecimal(object value) =>
            value == DBNull.Value || value == null ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }
}
