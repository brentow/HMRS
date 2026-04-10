using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public readonly record struct TransactionsSummaryDto(
        int Total,
        int Success,
        int Denied,
        int Failed,
        int Today);

    public sealed record TransactionEntryDto(
        long AuditLogId,
        DateTime CreatedAt,
        string ActionCode,
        string TargetType,
        string TargetId,
        string ResultStatus,
        string Username,
        string Details);

    public sealed class TransactionsDataService
    {
        private readonly string _connectionString;

        public TransactionsDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<TransactionsSummaryDto> GetSummaryAsync(int? actedByUserId = null)
        {
            const string sql = @"
SELECT
    COUNT(*) AS total_count,
    SUM(CASE WHEN result_status = 'SUCCESS' THEN 1 ELSE 0 END) AS success_count,
    SUM(CASE WHEN result_status = 'DENIED' THEN 1 ELSE 0 END) AS denied_count,
    SUM(CASE WHEN result_status = 'FAILED' THEN 1 ELSE 0 END) AS failed_count,
    SUM(CASE WHEN DATE(created_at) = CURDATE() THEN 1 ELSE 0 END) AS today_count
FROM audit_logs
WHERE (@acted_by_user_id IS NULL OR acted_by_user_id = @acted_by_user_id);";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@acted_by_user_id", actedByUserId.HasValue ? actedByUserId.Value : DBNull.Value);

                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new TransactionsSummaryDto();
                }

                return new TransactionsSummaryDto(
                    Total: ToInt(reader["total_count"]),
                    Success: ToInt(reader["success_count"]),
                    Denied: ToInt(reader["denied_count"]),
                    Failed: ToInt(reader["failed_count"]),
                    Today: ToInt(reader["today_count"]));
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return new TransactionsSummaryDto();
            }
        }

        public async Task<IReadOnlyList<TransactionEntryDto>> GetTransactionsAsync(
            string? resultStatus = null,
            string? search = null,
            int limit = 300,
            int? actedByUserId = null)
        {
            const string sql = @"
SELECT
    al.audit_log_id,
    al.created_at,
    al.action_code,
    al.target_type,
    COALESCE(al.target_id, '') AS target_id,
    al.result_status,
    COALESCE(NULLIF(ua.username, ''), 'System') AS username,
    COALESCE(al.details, '') AS details
FROM audit_logs al
LEFT JOIN user_accounts ua
    ON ua.user_id = al.acted_by_user_id
WHERE (@acted_by_user_id IS NULL OR al.acted_by_user_id = @acted_by_user_id)
  AND (@status = 'ALL' OR al.result_status = @status)
  AND (
      @search = ''
      OR CAST(al.audit_log_id AS CHAR) LIKE @search_like
      OR al.action_code LIKE @search_like
      OR al.target_type LIKE @search_like
      OR COALESCE(al.target_id, '') LIKE @search_like
      OR al.result_status LIKE @search_like
      OR COALESCE(ua.username, '') LIKE @search_like
      OR COALESCE(al.details, '') LIKE @search_like
  )
ORDER BY al.created_at DESC, al.audit_log_id DESC
LIMIT @limit;";

            var normalizedLimit = Math.Clamp(limit, 1, 1000);
            var normalizedStatus = NormalizeStatus(resultStatus);
            var normalizedSearch = search?.Trim() ?? string.Empty;
            var searchLike = $"%{normalizedSearch}%";

            var rows = new List<TransactionEntryDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@acted_by_user_id", actedByUserId.HasValue ? actedByUserId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@status", normalizedStatus);
                command.Parameters.AddWithValue("@search", normalizedSearch);
                command.Parameters.AddWithValue("@search_like", searchLike);
                command.Parameters.AddWithValue("@limit", normalizedLimit);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new TransactionEntryDto(
                        AuditLogId: ToLong(reader["audit_log_id"]),
                        CreatedAt: reader["created_at"] == DBNull.Value
                            ? DateTime.MinValue
                            : Convert.ToDateTime(reader["created_at"], CultureInfo.InvariantCulture),
                        ActionCode: reader["action_code"]?.ToString()?.Trim() ?? "-",
                        TargetType: reader["target_type"]?.ToString()?.Trim() ?? "-",
                        TargetId: reader["target_id"]?.ToString()?.Trim() ?? string.Empty,
                        ResultStatus: reader["result_status"]?.ToString()?.Trim() ?? "-",
                        Username: reader["username"]?.ToString()?.Trim() ?? "System",
                        Details: reader["details"]?.ToString()?.Trim() ?? string.Empty));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<TransactionEntryDto>();
            }

            return rows;
        }

        private static string NormalizeStatus(string? status)
        {
            var normalized = status?.Trim().ToUpperInvariant();
            return normalized switch
            {
                "SUCCESS" => "SUCCESS",
                "DENIED" => "DENIED",
                "FAILED" => "FAILED",
                _ => "ALL"
            };
        }

        private static int ToInt(object value)
        {
            if (value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static long ToLong(object value)
        {
            if (value == DBNull.Value)
            {
                return 0L;
            }

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static bool IsMissingObjectError(MySqlException ex) =>
            ex.Number is 1146 or 1054;
    }
}
