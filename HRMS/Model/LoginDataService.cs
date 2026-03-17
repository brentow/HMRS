using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MySqlConnector;

namespace HRMS.Model
{
    public enum LoginStatus
    {
        Success,
        InvalidCredentials,
        Inactive,
        Locked
    }

    public record AuthenticatedUser(
        int UserId,
        int? EmployeeId,
        string Username,
        string FullName,
        string RoleName,
        string Status,
        bool MustChangePassword = false);

    public record LoginValidationResult(LoginStatus Status, AuthenticatedUser? User = null);

    public class LoginDataService
    {
        private readonly string _connectionString;

        public LoginDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<LoginValidationResult> ValidateCredentialsAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return new LoginValidationResult(LoginStatus.InvalidCredentials);
            }

            var normalizedUsername = NormalizeUsername(username);

            var employeeDisplayNameSql = UserAccountIdentitySync.EmployeeDisplayNameSql("e");
            var sql = $@"
SELECT
    ua.user_id,
    ua.employee_id,
    ua.username,
    COALESCE(ua.password_hash, '') AS password_hash,
    COALESCE(ua.must_change_password, 0) AS must_change_password,
    ua.status,
    COALESCE(
        {employeeDisplayNameSql},
        NULLIF(TRIM(ua.full_name), ''),
        ua.username
    ) AS display_name,
    COALESCE(r.role_name, 'User') AS role_name
FROM user_accounts ua
LEFT JOIN employees e ON e.employee_id = ua.employee_id
LEFT JOIN roles r ON r.role_id = ua.role_id
WHERE LOWER(TRIM(ua.username)) = LOWER(@username)
   OR LOWER(TRIM(COALESCE(e.employee_no, ''))) = LOWER(@username)
LIMIT 1;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureEmployeeAccountsAsync(connection);

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@username", normalizedUsername);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new LoginValidationResult(LoginStatus.InvalidCredentials);
            }

            var userId = Convert.ToInt32(reader["user_id"]);
            var employeeId = reader["employee_id"] == DBNull.Value
                ? (int?)null
                : Convert.ToInt32(reader["employee_id"], CultureInfo.InvariantCulture);
            var dbUsername = reader["username"]?.ToString()?.Trim() ?? normalizedUsername;
            var fullName = reader["display_name"]?.ToString()?.Trim();
            var roleName = reader["role_name"]?.ToString()?.Trim();
            var status = reader["status"]?.ToString()?.Trim().ToUpperInvariant() ?? "INACTIVE";
            if (status == "LOCKED")
            {
                return new LoginValidationResult(LoginStatus.Locked);
            }

            if (status == "INACTIVE")
            {
                return new LoginValidationResult(LoginStatus.Inactive);
            }

            var storedPasswordHash = reader["password_hash"]?.ToString();
            var passwordIsValid = !string.IsNullOrWhiteSpace(storedPasswordHash) &&
                                  PasswordSecurity.VerifyPassword(password, storedPasswordHash);

            if (!passwordIsValid)
            {
                return new LoginValidationResult(LoginStatus.InvalidCredentials);
            }

            var mustChangePassword = Convert.ToInt32(reader["must_change_password"], CultureInfo.InvariantCulture) == 1;
            await reader.CloseAsync();

            const string updateLoginSql = @"
UPDATE user_accounts
SET last_login_at = NOW()
WHERE user_id = @user_id;";

            await using (var updateCommand = new MySqlCommand(updateLoginSql, connection))
            {
                updateCommand.Parameters.AddWithValue("@user_id", userId);
                await updateCommand.ExecuteNonQueryAsync();
            }

            var user = new AuthenticatedUser(
                UserId: userId,
                EmployeeId: employeeId,
                Username: dbUsername,
                FullName: string.IsNullOrWhiteSpace(fullName) ? dbUsername : fullName,
                RoleName: string.IsNullOrWhiteSpace(roleName) ? "User" : roleName,
                Status: status,
                MustChangePassword: mustChangePassword);

            return new LoginValidationResult(LoginStatus.Success, user);
        }

        private static async Task EnsureEmployeeAccountsAsync(MySqlConnection connection)
        {
            var employeeRoleId = await ResolveRoleIdAsync(connection, "Employee");
            if (!employeeRoleId.HasValue)
            {
                return;
            }

            const string missingEmployeesSql = @"
SELECT
    e.employee_id,
    e.employee_no,
    COALESCE(e.first_name, '') AS first_name,
    COALESCE(e.middle_name, '') AS middle_name,
    COALESCE(e.last_name, '') AS last_name,
    COALESCE(e.email, '') AS email
FROM employees e
LEFT JOIN user_accounts ua ON ua.employee_id = e.employee_id
WHERE ua.employee_id IS NULL
ORDER BY e.employee_id;";

            var missingEmployees = new List<(int EmployeeId, string EmployeeNo, string FirstName, string MiddleName, string LastName, string Email)>();

            await using (var missingCommand = new MySqlCommand(missingEmployeesSql, connection))
            await using (var reader = await missingCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    missingEmployees.Add((
                        EmployeeId: Convert.ToInt32(reader["employee_id"], CultureInfo.InvariantCulture),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? string.Empty,
                        FirstName: reader["first_name"]?.ToString() ?? string.Empty,
                        MiddleName: reader["middle_name"]?.ToString() ?? string.Empty,
                        LastName: reader["last_name"]?.ToString() ?? string.Empty,
                        Email: reader["email"]?.ToString() ?? string.Empty));
                }
            }

            if (missingEmployees.Count == 0)
            {
                return;
            }

            const string insertSql = @"
INSERT INTO user_accounts (
    role_id,
    employee_id,
    username,
    password_hash,
    must_change_password,
    password_changed_at,
    full_name,
    email,
    status
)
VALUES (
    @role_id,
    @employee_id,
    @username,
    @password_hash,
    @must_change_password,
    NULL,
    @full_name,
    @email,
    'ACTIVE'
);";

            foreach (var employee in missingEmployees)
            {
                var username = await BuildUniqueEmployeeUsernameAsync(connection, employee.EmployeeNo, employee.EmployeeId);
                var fullName = UserAccountIdentitySync.BuildEmployeeDisplayName(employee.FirstName, employee.LastName, employee.MiddleName);
                var tempPassword = string.IsNullOrWhiteSpace(employee.EmployeeNo)
                    ? PasswordSecurity.GenerateTemporaryPassword()
                    : employee.EmployeeNo.Trim();

                await using var insert = new MySqlCommand(insertSql, connection);
                insert.Parameters.AddWithValue("@role_id", employeeRoleId.Value);
                insert.Parameters.AddWithValue("@employee_id", employee.EmployeeId);
                insert.Parameters.AddWithValue("@username", username);
                insert.Parameters.AddWithValue("@password_hash", PasswordSecurity.HashPassword(tempPassword));
                insert.Parameters.AddWithValue("@must_change_password", 1);
                insert.Parameters.AddWithValue("@full_name", string.IsNullOrWhiteSpace(fullName) ? DBNull.Value : fullName);
                insert.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(employee.Email) ? DBNull.Value : employee.Email.Trim());
                await insert.ExecuteNonQueryAsync();
            }
        }

        private static async Task<int?> ResolveRoleIdAsync(MySqlConnection connection, string roleName)
        {
            const string sql = @"
SELECT role_id
FROM roles
WHERE role_name = @role_name
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@role_name", roleName);
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static async Task<string> BuildUniqueEmployeeUsernameAsync(MySqlConnection connection, string employeeNo, int employeeId)
        {
            var digits = string.Concat((employeeNo ?? string.Empty).Where(char.IsDigit));
            var baseUsername = string.IsNullOrWhiteSpace(digits)
                ? $"emp{employeeId}"
                : $"emp{digits}";

            var candidate = baseUsername.ToLowerInvariant();
            var suffix = 1;

            while (await UsernameExistsAsync(connection, candidate))
            {
                suffix++;
                candidate = $"{baseUsername.ToLowerInvariant()}_{suffix}";
            }

            return candidate;
        }

        private static async Task<bool> UsernameExistsAsync(MySqlConnection connection, string username)
        {
            const string sql = @"
SELECT 1
FROM user_accounts
WHERE LOWER(TRIM(username)) = LOWER(@username)
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@username", username);
            var value = await command.ExecuteScalarAsync();
            return value != null && value != DBNull.Value;
        }

        private static string NormalizeUsername(string username) => username.Trim();

        /// <summary>
        /// Changes the password for a user account (typically after first login).
        /// </summary>
        /// <param name="userId">The user account ID.</param>
        /// <param name="newPassword">The new password (must be at least 8 characters).</param>
        /// <returns>True if successful; throws exception if validation fails.</returns>
        public async Task<bool> ChangePasswordAsync(int userId, string newPassword)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("Invalid user ID.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                throw new ArgumentException("Password cannot be empty.", nameof(newPassword));
            }

            var trimmedPassword = newPassword.Trim();
            if (trimmedPassword.Length < 8)
            {
                throw new InvalidOperationException("Password must be at least 8 characters long.");
            }

            const string sql = @"
UPDATE user_accounts
SET password_hash = @password_hash,
    must_change_password = 0,
    password_changed_at = CURRENT_TIMESTAMP,
    updated_at = CURRENT_TIMESTAMP
WHERE user_id = @user_id;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@password_hash", PasswordSecurity.HashPassword(trimmedPassword));
                command.Parameters.AddWithValue("@user_id", userId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException("User account not found.");
                }

                return true;
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to update password: {ex.Message}",
                    ex);
            }
        }

    }
}
