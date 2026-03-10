using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public static class AuthorizationGuard
    {
        public static async Task<string?> GetRoleNameAsync(MySqlConnection connection, int actingUserId, MySqlTransaction? transaction = null)
        {
            ArgumentNullException.ThrowIfNull(connection);

            if (actingUserId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT r.role_name
FROM user_accounts ua
LEFT JOIN roles r ON r.role_id = ua.role_id
WHERE ua.user_id = @user_id
LIMIT 1;";

            await using var command = transaction is null
                ? new MySqlCommand(sql, connection)
                : new MySqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("@user_id", actingUserId);

            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            var role = result.ToString();
            return string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        }

        public static bool IsAdmin(string? roleName) =>
            string.Equals(roleName?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase);

        public static bool IsAdminOrHr(string? roleName) =>
            IsAdmin(roleName) || string.Equals(roleName?.Trim(), "HR Manager", StringComparison.OrdinalIgnoreCase);

        public static void DemandAdmin(string? roleName, string actionLabel)
        {
            if (!IsAdmin(roleName))
            {
                throw new InvalidOperationException($"Only Admin can {actionLabel}.");
            }
        }

        public static void DemandAdminOrHr(string? roleName, string actionLabel)
        {
            if (!IsAdminOrHr(roleName))
            {
                throw new InvalidOperationException($"Only Admin or HR Manager can {actionLabel}.");
            }
        }
    }
}
