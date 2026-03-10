using MySqlConnector;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public static class UserAccountIdentitySync
    {
        public static string EmployeeDisplayNameSql(string employeeAlias) =>
            $@"NULLIF(TRIM(CONCAT_WS(', ',
    NULLIF(TRIM({employeeAlias}.last_name), ''),
    NULLIF(TRIM(CONCAT_WS(' ',
        NULLIF(TRIM({employeeAlias}.first_name), ''),
        NULLIF(TRIM({employeeAlias}.middle_name), '')
    )), '')
)), '')";

        public static string BuildEmployeeDisplayName(string? firstName, string? lastName, string? middleName)
        {
            var first = Safe(firstName);
            var last = Safe(lastName);
            var middle = Safe(middleName);
            var firstMiddle = string.Join(" ", new[] { first, middle }.Where(static x => !string.IsNullOrWhiteSpace(x)));

            if (string.IsNullOrWhiteSpace(last))
            {
                return string.IsNullOrWhiteSpace(firstMiddle) ? string.Empty : firstMiddle;
            }

            return string.IsNullOrWhiteSpace(firstMiddle)
                ? last
                : $"{last}, {firstMiddle}";
        }

        public static (string LastName, string FirstName, string? MiddleName) ParseDisplayName(string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return (string.Empty, string.Empty, null);
            }

            var text = displayName.Trim();
            if (text.Contains(','))
            {
                var parts = text.Split(new[] { ',' }, 2, StringSplitOptions.RemoveEmptyEntries);
                var lastName = Safe(parts.ElementAtOrDefault(0));
                var firstMiddle = Safe(parts.ElementAtOrDefault(1));
                var tokens = firstMiddle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var firstName = tokens.ElementAtOrDefault(0) ?? string.Empty;
                var middleName = tokens.Length > 1 ? string.Join(" ", tokens.Skip(1)) : null;
                return (lastName, firstName, string.IsNullOrWhiteSpace(middleName) ? null : middleName);
            }

            var nameTokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return nameTokens.Length switch
            {
                0 => (string.Empty, string.Empty, null),
                1 => (nameTokens[0], string.Empty, null),
                2 => (nameTokens[1], nameTokens[0], null),
                _ => (nameTokens[^1], nameTokens[0], string.Join(" ", nameTokens.Skip(1).Take(nameTokens.Length - 2)))
            };
        }

        public static async Task<int?> ResolveLinkedEmployeeIdAsync(MySqlConnection connection, int userId, MySqlTransaction? transaction = null)
        {
            const string sql = @"
SELECT employee_id
FROM user_accounts
WHERE user_id = @user_id
LIMIT 1;";

            await using var command = transaction is null
                ? new MySqlCommand(sql, connection)
                : new MySqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("@user_id", userId);
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static async Task SyncLinkedAccountAsync(MySqlConnection connection, int employeeId, MySqlTransaction? transaction = null)
        {
            if (employeeId <= 0)
            {
                return;
            }

            var displayNameSql = EmployeeDisplayNameSql("e");
            var sql = $@"
UPDATE user_accounts ua
INNER JOIN employees e ON e.employee_id = ua.employee_id
SET ua.full_name = COALESCE({displayNameSql}, ua.full_name),
    ua.email = CASE
        WHEN e.email IS NULL OR TRIM(e.email) = '' THEN NULL
        ELSE TRIM(e.email)
    END,
    ua.updated_at = CURRENT_TIMESTAMP
WHERE ua.employee_id = @employee_id;";

            await using var command = transaction is null
                ? new MySqlCommand(sql, connection)
                : new MySqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("@employee_id", employeeId);
            await command.ExecuteNonQueryAsync();
        }

        private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
