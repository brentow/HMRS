using MySqlConnector;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record SulopBudgetAllocationDto(
        long AllocationId,
        string Program,
        decimal AllocatedAmount,
        decimal UsedAmount,
        decimal RemainingAmount,
        string Status);

    public sealed record SulopDisbursementResult(
        long TransactionId,
        long ConsolidatedId,
        decimal RemainingAfter);

    public sealed class SulopFundAllocationService
    {
        private readonly string _connectionString;
        private readonly long _officeId;

        public SulopFundAllocationService(string connectionString, long officeId)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? throw new ArgumentException("Sulop connection string is required.", nameof(connectionString))
                : connectionString;
            _officeId = officeId > 0
                ? officeId
                : throw new ArgumentOutOfRangeException(nameof(officeId), "Office ID must be greater than zero.");
        }

        public async Task<SulopBudgetAllocationDto?> GetActiveAllocationAsync()
        {
            const string sql = @"
SELECT
    id,
    COALESCE(program, 'General Payroll') AS program,
    COALESCE(amount, 0) AS allocated_amount,
    COALESCE(used_amount, 0) AS used_amount,
    COALESCE(remaining_amount, 0) AS remaining_amount,
    COALESCE(status, 'active') AS status
FROM budget_allocations
WHERE office_id = @office_id
  AND COALESCE(office_type, 'service') = 'service'
  AND COALESCE(status, 'active') = 'active'
ORDER BY updated_at DESC, id DESC
LIMIT 1;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@office_id", _officeId);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new SulopBudgetAllocationDto(
                AllocationId: ToLong(reader["id"]),
                Program: reader["program"]?.ToString() ?? "General Payroll",
                AllocatedAmount: ToDecimal(reader["allocated_amount"]),
                UsedAmount: ToDecimal(reader["used_amount"]),
                RemainingAmount: ToDecimal(reader["remaining_amount"]),
                Status: reader["status"]?.ToString() ?? "active");
        }

        public async Task<SulopDisbursementResult> RecordPayrollDisbursementAsync(
            long allocationId,
            decimal amount,
            string recipientName,
            string purpose,
            string? description)
        {
            if (allocationId <= 0)
            {
                throw new InvalidOperationException("Invalid Sulop allocation ID.");
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
    COALESCE(amount, 0) AS allocated_amount,
    COALESCE(used_amount, 0) AS used_amount,
    COALESCE(remaining_amount, 0) AS remaining_amount
FROM budget_allocations
WHERE id = @allocation_id
  AND office_id = @office_id
  AND COALESCE(office_type, 'service') = 'service'
  AND COALESCE(status, 'active') = 'active'
LIMIT 1
FOR UPDATE;";

            const string updateAllocationSql = @"
UPDATE budget_allocations
SET used_amount = used_amount + @amount,
    remaining_amount = remaining_amount - @amount,
    updated_at = NOW()
WHERE id = @allocation_id
  AND office_id = @office_id;";

            const string insertTransactionSql = @"
INSERT INTO tbl_transaction
(
    office_id,
    budget_allocation_id,
    status,
    amount,
    purpose,
    recipient_type,
    recipient_name,
    description,
    priority,
    date_applied_,
    distributed_by,
    created_at,
    updated_at
)
VALUES
(
    @office_id,
    @allocation_id,
    'pending',
    @amount,
    @purpose,
    'individual',
    @recipient_name,
    @description,
    'medium',
    CURDATE(),
    1,
    NOW(),
    NOW()
);";

            const string insertConsolidatedSql = @"
INSERT INTO consolidated_transaction
(
    office_id,
    total_budget,
    budget,
    beneficiary_id,
    budget_received,
    status,
    remarks,
    created_at,
    updated_at
)
VALUES
(
    @office_id_text,
    @total_budget_text,
    @amount_text,
    @beneficiary_text,
    @amount_text,
    'Pending',
    @remarks,
    NOW(),
    NOW()
);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                decimal totalBudget;
                decimal remainingBefore;

                await using (var lockCommand = new MySqlCommand(lockAllocationSql, connection, transaction))
                {
                    lockCommand.Parameters.AddWithValue("@allocation_id", allocationId);
                    lockCommand.Parameters.AddWithValue("@office_id", _officeId);

                    await using var reader = await lockCommand.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        throw new InvalidOperationException("No active Sulop allocation found for this office.");
                    }

                    totalBudget = ToDecimal(reader["allocated_amount"]);
                    remainingBefore = ToDecimal(reader["remaining_amount"]);
                }

                if (amount > remainingBefore)
                {
                    throw new InvalidOperationException(
                        $"Amount exceeds remaining Sulop allocation. Remaining: PHP {remainingBefore:N2}, Requested: PHP {amount:N2}.");
                }

                await using (var updateCommand = new MySqlCommand(updateAllocationSql, connection, transaction))
                {
                    updateCommand.Parameters.AddWithValue("@amount", amount);
                    updateCommand.Parameters.AddWithValue("@allocation_id", allocationId);
                    updateCommand.Parameters.AddWithValue("@office_id", _officeId);
                    await updateCommand.ExecuteNonQueryAsync();
                }

                long transactionId;
                await using (var txCommand = new MySqlCommand(insertTransactionSql, connection, transaction))
                {
                    txCommand.Parameters.AddWithValue("@office_id", _officeId);
                    txCommand.Parameters.AddWithValue("@allocation_id", allocationId);
                    txCommand.Parameters.AddWithValue("@amount", amount);
                    txCommand.Parameters.AddWithValue("@purpose", purpose.Trim());
                    txCommand.Parameters.AddWithValue("@recipient_name", recipientName.Trim());
                    txCommand.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(description) ? DBNull.Value : description.Trim());
                    await txCommand.ExecuteNonQueryAsync();
                    transactionId = txCommand.LastInsertedId;
                }

                long consolidatedId;
                await using (var consolidatedCommand = new MySqlCommand(insertConsolidatedSql, connection, transaction))
                {
                    var amountText = amount.ToString("0.00", CultureInfo.InvariantCulture);
                    consolidatedCommand.Parameters.AddWithValue("@office_id_text", _officeId.ToString(CultureInfo.InvariantCulture));
                    consolidatedCommand.Parameters.AddWithValue("@total_budget_text", totalBudget.ToString("0.00", CultureInfo.InvariantCulture));
                    consolidatedCommand.Parameters.AddWithValue("@amount_text", amountText);
                    consolidatedCommand.Parameters.AddWithValue("@beneficiary_text", recipientName.Trim());
                    consolidatedCommand.Parameters.AddWithValue("@remarks", purpose.Trim());
                    await consolidatedCommand.ExecuteNonQueryAsync();
                    consolidatedId = consolidatedCommand.LastInsertedId;
                }

                await transaction.CommitAsync();
                return new SulopDisbursementResult(
                    TransactionId: transactionId,
                    ConsolidatedId: consolidatedId,
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