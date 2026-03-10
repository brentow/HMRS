using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed class AuditLogWriter
    {
        private readonly string _connectionString;
        private static readonly string SessionId = Guid.NewGuid().ToString("N");
        private static readonly string ClientName = Environment.MachineName;

        public AuditLogWriter(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task TryWriteAsync(
            int? actedByUserId,
            string actionCode,
            string targetType,
            string? targetId,
            string resultStatus,
            string? details)
        {
            await using var connection = new MySqlConnection(_connectionString);

            try
            {
                await connection.OpenAsync();
                await TryWriteAsync(connection, null, actedByUserId, actionCode, targetType, targetId, resultStatus, details);
            }
            catch
            {
                // Intentionally ignored. Business action should not fail because audit logging failed.
            }
        }

        public static async Task TryWriteAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            int? actedByUserId,
            string actionCode,
            string targetType,
            string? targetId,
            string resultStatus,
            string? details)
        {
            ArgumentNullException.ThrowIfNull(connection);

            if (string.IsNullOrWhiteSpace(actionCode) || string.IsNullOrWhiteSpace(targetType))
            {
                return;
            }

            const string sql = @"
INSERT INTO audit_logs
    (acted_by_user_id, ip_address, client_name, session_id, action_code, target_type, target_id, result_status, details, old_values_json, new_values_json)
VALUES
    (@acted_by_user_id, @ip_address, @client_name, @session_id, @action_code, @target_type, @target_id, @result_status, @details, @old_values_json, @new_values_json);";

            var normalizedStatus = NormalizeResultStatus(resultStatus);

            try
            {
                await using var command = transaction is null
                    ? new MySqlCommand(sql, connection)
                    : new MySqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@acted_by_user_id", actedByUserId.HasValue && actedByUserId.Value > 0
                    ? actedByUserId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@ip_address", DBNull.Value);
                command.Parameters.AddWithValue("@client_name", string.IsNullOrWhiteSpace(ClientName) ? DBNull.Value : ClientName);
                command.Parameters.AddWithValue("@session_id", SessionId);
                command.Parameters.AddWithValue("@action_code", actionCode.Trim());
                command.Parameters.AddWithValue("@target_type", targetType.Trim());
                command.Parameters.AddWithValue("@target_id", string.IsNullOrWhiteSpace(targetId) ? DBNull.Value : targetId.Trim());
                command.Parameters.AddWithValue("@result_status", normalizedStatus);
                command.Parameters.AddWithValue("@details", string.IsNullOrWhiteSpace(details) ? DBNull.Value : details.Trim());
                command.Parameters.AddWithValue("@old_values_json", DBNull.Value);
                command.Parameters.AddWithValue("@new_values_json", DBNull.Value);

                await command.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex) when (IsAuditTableUnavailable(ex))
            {
                // Intentionally ignored for environments where migration is not yet applied.
            }
            catch
            {
                // Intentionally ignored. Audit logging must not block the main operation.
            }
        }

        private static string NormalizeResultStatus(string? resultStatus)
        {
            var normalized = resultStatus?.Trim().ToUpperInvariant();
            return normalized switch
            {
                "SUCCESS" => "SUCCESS",
                "DENIED" => "DENIED",
                "FAILED" => "FAILED",
                _ => "FAILED"
            };
        }

        private static bool IsAuditTableUnavailable(MySqlException ex) =>
            ex.Number is 1146 or 1054 ||
            ex.Message.Contains("audit_logs", StringComparison.OrdinalIgnoreCase);
    }
}
