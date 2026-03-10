using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record UserAdminDto(
        int UserId,
        string Username,
        string FullName,
        string Email,
        string RoleName,
        string Status,
        DateTime? LastLoginAt);

    public record RoleSummaryDto(
        int RoleId,
        string RoleName,
        int UserCount,
        int PermissionCount);

    public record PermissionSummaryDto(
        int PermissionId,
        string PermissionCode,
        string Description,
        int AssignedRoleCount);

    public record UsersRolesStatsDto(
        int TotalUsers,
        int ActiveUsers,
        int LockedUsers,
        int AdminUsers,
        int TotalRoles,
        int TotalPermissions);

    public record CurrentUserProfileDto(
        int UserId,
        string Username,
        string FullName,
        string Email,
        string RoleName,
        string Status,
        DateTime? LastLoginAt);

    public record UpdateCurrentUserProfileDto(
        int UserId,
        string Username,
        string FullName,
        string Email,
        string? NewPassword);

    public record CreateUserAccountDto(
        string Username,
        string Password,
        string FullName,
        string Email,
        string RoleName);

    public class UsersRolesDataService
    {
        private readonly string _connectionString;
        private readonly AuditLogWriter _auditLogWriter;

        public UsersRolesDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _auditLogWriter = new AuditLogWriter(_connectionString);
        }

        public async Task<IReadOnlyList<UserAdminDto>> GetUsersAsync()
        {
            var employeeDisplayNameSql = UserAccountIdentitySync.EmployeeDisplayNameSql("e");
            var sql = $@"
SELECT
    ua.user_id,
    ua.username,
    COALESCE(
        {employeeDisplayNameSql},
        NULLIF(TRIM(ua.full_name), ''),
        ''
    ) AS full_name,
    COALESCE(
        NULLIF(TRIM(e.email), ''),
        NULLIF(TRIM(ua.email), ''),
        ''
    ) AS email,
    COALESCE(r.role_name, 'Employee') AS role_name,
    COALESCE(ua.status, 'ACTIVE') AS status,
    ua.last_login_at
FROM user_accounts ua
LEFT JOIN roles r ON r.role_id = ua.role_id
LEFT JOIN employees e ON e.employee_id = ua.employee_id
ORDER BY ua.username;";

            var users = new List<UserAdminDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await EnsureEmployeeAccountsAsync(connection);

                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    users.Add(new UserAdminDto(
                        UserId: Convert.ToInt32(reader["user_id"], CultureInfo.InvariantCulture),
                        Username: reader["username"]?.ToString() ?? string.Empty,
                        FullName: reader["full_name"]?.ToString() ?? string.Empty,
                        Email: reader["email"]?.ToString() ?? string.Empty,
                        RoleName: reader["role_name"]?.ToString() ?? "Employee",
                        Status: reader["status"]?.ToString() ?? "ACTIVE",
                        LastLoginAt: reader["last_login_at"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(reader["last_login_at"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException)
            {
                return Array.Empty<UserAdminDto>();
            }

            return users;
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

        public async Task<IReadOnlyList<string>> GetRolesAsync()
        {
            const string sql = @"
SELECT role_name
FROM roles
ORDER BY
    CASE role_name
        WHEN 'Admin' THEN 0
        WHEN 'HR Manager' THEN 1
        WHEN 'Dept Head' THEN 2
        WHEN 'Employee' THEN 3
        ELSE 99
    END,
    role_name;";

            var roles = new List<string>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var role = reader["role_name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        roles.Add(role.Trim());
                    }
                }
            }
            catch (MySqlException)
            {
                return Array.Empty<string>();
            }

            return roles;
        }

        public async Task<int> CreateUserAsync(CreateUserAccountDto dto, int actingUserId)
        {
            ArgumentNullException.ThrowIfNull(dto);

            if (string.IsNullOrWhiteSpace(dto.Username))
            {
                throw new InvalidOperationException("Username is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Password))
            {
                throw new InvalidOperationException("Password is required.");
            }
            if (dto.Password.Trim().Length < 8)
            {
                throw new InvalidOperationException("Password must be at least 8 characters.");
            }

            if (string.IsNullOrWhiteSpace(dto.FullName))
            {
                throw new InvalidOperationException("Full name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.RoleName))
            {
                throw new InvalidOperationException("Role is required.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var normalizedUsername = NormalizeUsername(dto.Username);
            await EnsureAdminAccessAsync(connection, actingUserId, "create user accounts", "USER_CREATE", "user_accounts", normalizedUsername);

            var roleId = await ResolveRoleIdAsync(connection, dto.RoleName.Trim());
            if (!roleId.HasValue)
            {
                throw new InvalidOperationException("Selected role does not exist.");
            }

            if (await UsernameExistsAsync(connection, normalizedUsername))
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "USER_CREATE",
                    "user_accounts",
                    normalizedUsername,
                    "FAILED",
                    $"Duplicate username '{normalizedUsername}'.");
                throw new InvalidOperationException("Username already exists.");
            }

            await using var transaction = await connection.BeginTransactionAsync();

            const string sql = @"
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
    NULL,
    @username,
    @password_hash,
    1,
    NULL,
    @full_name,
    @email,
    'ACTIVE'
);
SELECT LAST_INSERT_ID();";

            try
            {
                await using var command = new MySqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@role_id", roleId.Value);
                command.Parameters.AddWithValue("@username", normalizedUsername);
                command.Parameters.AddWithValue("@password_hash", PasswordSecurity.HashPassword(dto.Password.Trim()));
                command.Parameters.AddWithValue("@full_name", dto.FullName.Trim());
                command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(dto.Email) ? DBNull.Value : dto.Email.Trim());

                var value = await command.ExecuteScalarAsync();
                var insertedId = value == null || value == DBNull.Value
                    ? 0
                    : Convert.ToInt32(value, CultureInfo.InvariantCulture);

                if (insertedId <= 0)
                {
                    await transaction.RollbackAsync();
                    await _auditLogWriter.TryWriteAsync(actingUserId, "USER_CREATE", "user_accounts", dto.Username.Trim(), "FAILED", "Insert returned no user id.");
                    throw new InvalidOperationException("Unable to create user account.");
                }

                await transaction.CommitAsync();
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "USER_CREATE",
                    "user_accounts",
                    insertedId.ToString(CultureInfo.InvariantCulture),
                    "SUCCESS",
                    $"Created username '{normalizedUsername}' with role '{dto.RoleName.Trim()}'.");
                return insertedId;
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                await transaction.RollbackAsync();
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "USER_CREATE",
                    "user_accounts",
                    normalizedUsername,
                    "FAILED",
                    $"Duplicate username '{normalizedUsername}'.");
                throw new InvalidOperationException("Username already exists.");
            }
            catch
            {
                await transaction.RollbackAsync();
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "USER_CREATE",
                    "user_accounts",
                    normalizedUsername,
                    "FAILED",
                    "Unexpected error while creating user.");
                throw;
            }
        }

        public async Task<IReadOnlyList<RoleSummaryDto>> GetRoleSummariesAsync()
        {
            const string sql = @"
SELECT
    r.role_id,
    r.role_name,
    COUNT(DISTINCT ua.user_id) AS user_count,
    COUNT(DISTINCT rp.permission_id) AS permission_count
FROM roles r
LEFT JOIN user_accounts ua ON ua.role_id = r.role_id
LEFT JOIN role_permissions rp ON rp.role_id = r.role_id
GROUP BY r.role_id, r.role_name
ORDER BY r.role_name;";

            var rows = new List<RoleSummaryDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RoleSummaryDto(
                        RoleId: Convert.ToInt32(reader["role_id"], CultureInfo.InvariantCulture),
                        RoleName: reader["role_name"]?.ToString() ?? string.Empty,
                        UserCount: Convert.ToInt32(reader["user_count"], CultureInfo.InvariantCulture),
                        PermissionCount: Convert.ToInt32(reader["permission_count"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException)
            {
                return Array.Empty<RoleSummaryDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<PermissionSummaryDto>> GetPermissionSummariesAsync()
        {
            const string sql = @"
SELECT
    p.permission_id,
    p.perm_code,
    COALESCE(p.description, '') AS description,
    COUNT(DISTINCT rp.role_id) AS role_count
FROM permissions p
LEFT JOIN role_permissions rp ON rp.permission_id = p.permission_id
GROUP BY p.permission_id, p.perm_code, p.description
ORDER BY p.perm_code;";

            var rows = new List<PermissionSummaryDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new PermissionSummaryDto(
                        PermissionId: Convert.ToInt32(reader["permission_id"], CultureInfo.InvariantCulture),
                        PermissionCode: reader["perm_code"]?.ToString() ?? string.Empty,
                        Description: reader["description"]?.ToString() ?? string.Empty,
                        AssignedRoleCount: Convert.ToInt32(reader["role_count"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException)
            {
                return Array.Empty<PermissionSummaryDto>();
            }

            return rows;
        }

        public async Task<UsersRolesStatsDto> GetStatsAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var totalUsers = await CountAsync(connection, "SELECT COUNT(*) FROM user_accounts;");
                var activeUsers = await CountAsync(connection, "SELECT COUNT(*) FROM user_accounts WHERE status = 'ACTIVE';");
                var lockedUsers = await CountAsync(connection, "SELECT COUNT(*) FROM user_accounts WHERE status = 'LOCKED';");
                var adminUsers = await CountAsync(connection, @"
SELECT COUNT(*)
FROM user_accounts ua
JOIN roles r ON r.role_id = ua.role_id
WHERE r.role_name = 'Admin';");
                var totalRoles = await CountAsync(connection, "SELECT COUNT(*) FROM roles;");
                var totalPermissions = await CountAsync(connection, "SELECT COUNT(*) FROM permissions;");

                return new UsersRolesStatsDto(totalUsers, activeUsers, lockedUsers, adminUsers, totalRoles, totalPermissions);
            }
            catch (MySqlException)
            {
                return new UsersRolesStatsDto(0, 0, 0, 0, 0, 0);
            }
        }

        public async Task UpdateUserAccessAsync(int userId, string roleName, string status, int actingUserId)
        {
            if (userId <= 0)
            {
                throw new InvalidOperationException("Invalid user.");
            }

            if (string.IsNullOrWhiteSpace(roleName))
            {
                throw new InvalidOperationException("Role is required.");
            }

            var normalizedStatus = NormalizeStatus(status);

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureAdminAccessAsync(connection, actingUserId, "update user access", "USER_ACCESS_UPDATE", "user_accounts", userId.ToString(CultureInfo.InvariantCulture));

            var roleId = await ResolveRoleIdAsync(connection, roleName.Trim());
            if (!roleId.HasValue)
            {
                throw new InvalidOperationException("Selected role does not exist.");
            }

            const string sql = @"
UPDATE user_accounts
SET role_id = @role_id,
    status = @status,
    updated_at = CURRENT_TIMESTAMP
WHERE user_id = @user_id;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@role_id", roleId.Value);
            command.Parameters.AddWithValue("@status", normalizedStatus);
            command.Parameters.AddWithValue("@user_id", userId);

            var affected = await command.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                await _auditLogWriter.TryWriteAsync(actingUserId, "USER_ACCESS_UPDATE", "user_accounts", userId.ToString(CultureInfo.InvariantCulture), "FAILED", "User account not found.");
                throw new InvalidOperationException("User account was not found.");
            }

            await _auditLogWriter.TryWriteAsync(
                actingUserId,
                "USER_ACCESS_UPDATE",
                "user_accounts",
                userId.ToString(CultureInfo.InvariantCulture),
                "SUCCESS",
                $"Role='{roleName.Trim()}', Status='{normalizedStatus}'.");
        }

        public async Task<string> ResetPasswordAsync(int userId, int actingUserId)
        {
            if (userId <= 0)
            {
                throw new InvalidOperationException("Invalid user.");
            }

            var temporaryPassword = PasswordSecurity.GenerateTemporaryPassword();
            var passwordHash = PasswordSecurity.HashPassword(temporaryPassword);

            const string sql = @"
UPDATE user_accounts
SET password_hash = @password_hash,
    must_change_password = 1,
    password_changed_at = CURRENT_TIMESTAMP,
    updated_at = CURRENT_TIMESTAMP
WHERE user_id = @user_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureAdminAccessAsync(connection, actingUserId, "reset user passwords", "USER_PASSWORD_RESET", "user_accounts", userId.ToString(CultureInfo.InvariantCulture));

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@password_hash", passwordHash);
            command.Parameters.AddWithValue("@user_id", userId);

            var affected = await command.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                await _auditLogWriter.TryWriteAsync(actingUserId, "USER_PASSWORD_RESET", "user_accounts", userId.ToString(CultureInfo.InvariantCulture), "FAILED", "User account not found.");
                throw new InvalidOperationException("User account was not found.");
            }

            await _auditLogWriter.TryWriteAsync(
                actingUserId,
                "USER_PASSWORD_RESET",
                "user_accounts",
                userId.ToString(CultureInfo.InvariantCulture),
                "SUCCESS",
                "Temporary password issued.");

            return temporaryPassword;
        }

        public async Task DeleteUserAsync(int userId, int actingUserId)
        {
            if (userId <= 0)
            {
                throw new InvalidOperationException("Invalid user.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureAdminAccessAsync(connection, actingUserId, "delete users", "USER_DELETE", "user_accounts", userId.ToString(CultureInfo.InvariantCulture));
            await using var transaction = await connection.BeginTransactionAsync();

            int? employeeId;

            const string resolveSql = @"
SELECT employee_id
FROM user_accounts
WHERE user_id = @user_id
LIMIT 1;";

            await using (var resolve = new MySqlCommand(resolveSql, connection, transaction))
            {
                resolve.Parameters.AddWithValue("@user_id", userId);
                await using var reader = await resolve.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    await _auditLogWriter.TryWriteAsync(actingUserId, "USER_DELETE", "user_accounts", userId.ToString(CultureInfo.InvariantCulture), "FAILED", "User account not found.");
                    throw new InvalidOperationException("User account was not found.");
                }

                employeeId = reader["employee_id"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["employee_id"], CultureInfo.InvariantCulture);
            }

            const string deleteUserSql = @"
DELETE FROM user_accounts
WHERE user_id = @user_id;";

            await using (var deleteUser = new MySqlCommand(deleteUserSql, connection, transaction))
            {
                deleteUser.Parameters.AddWithValue("@user_id", userId);
                var userRows = await deleteUser.ExecuteNonQueryAsync();
                if (userRows == 0)
                {
                    await _auditLogWriter.TryWriteAsync(actingUserId, "USER_DELETE", "user_accounts", userId.ToString(CultureInfo.InvariantCulture), "FAILED", "User account not found during delete.");
                    throw new InvalidOperationException("User account was not found.");
                }
            }

            if (employeeId.HasValue)
            {
                const string deleteEmployeeSql = @"
DELETE FROM employees
WHERE employee_id = @employee_id;";

                await using var deleteEmployee = new MySqlCommand(deleteEmployeeSql, connection, transaction);
                deleteEmployee.Parameters.AddWithValue("@employee_id", employeeId.Value);
                await deleteEmployee.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            await _auditLogWriter.TryWriteAsync(
                actingUserId,
                "USER_DELETE",
                "user_accounts",
                userId.ToString(CultureInfo.InvariantCulture),
                "SUCCESS",
                employeeId.HasValue
                    ? $"Deleted linked employee_id={employeeId.Value}."
                    : "Deleted standalone user account.");
        }

        public async Task<CurrentUserProfileDto?> GetCurrentUserProfileAsync(int userId)
        {
            if (userId <= 0)
            {
                return null;
            }

            var employeeDisplayNameSql = UserAccountIdentitySync.EmployeeDisplayNameSql("e");
            var sql = $@"
SELECT
    ua.user_id,
    ua.username,
    COALESCE(
        {employeeDisplayNameSql},
        NULLIF(TRIM(ua.full_name), ''),
        ''
    ) AS full_name,
    COALESCE(
        NULLIF(TRIM(e.email), ''),
        NULLIF(TRIM(ua.email), ''),
        ''
    ) AS email,
    COALESCE(r.role_name, 'Employee') AS role_name,
    COALESCE(ua.status, 'ACTIVE') AS status,
    ua.last_login_at
FROM user_accounts ua
LEFT JOIN roles r ON r.role_id = ua.role_id
LEFT JOIN employees e ON e.employee_id = ua.employee_id
WHERE ua.user_id = @user_id
LIMIT 1;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@user_id", userId);

                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return new CurrentUserProfileDto(
                    UserId: Convert.ToInt32(reader["user_id"], CultureInfo.InvariantCulture),
                    Username: reader["username"]?.ToString() ?? string.Empty,
                    FullName: reader["full_name"]?.ToString() ?? string.Empty,
                    Email: reader["email"]?.ToString() ?? string.Empty,
                    RoleName: reader["role_name"]?.ToString() ?? "Employee",
                    Status: reader["status"]?.ToString() ?? "ACTIVE",
                    LastLoginAt: reader["last_login_at"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["last_login_at"], CultureInfo.InvariantCulture));
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        public async Task UpdateCurrentUserProfileAsync(UpdateCurrentUserProfileDto dto)
        {
            if (dto.UserId <= 0)
            {
                throw new InvalidOperationException("Invalid current user.");
            }

            if (string.IsNullOrWhiteSpace(dto.Username))
            {
                throw new InvalidOperationException("Username is required.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var normalizedUsername = NormalizeUsername(dto.Username);

            if (await UsernameExistsForOtherUserAsync(connection, normalizedUsername, dto.UserId))
            {
                throw new InvalidOperationException("Username already exists.");
            }

            var linkedEmployeeId = await UserAccountIdentitySync.ResolveLinkedEmployeeIdAsync(connection, dto.UserId);
            await using var transaction = await connection.BeginTransactionAsync();

            const string profileSql = @"
UPDATE user_accounts
SET username = @username,
    full_name = @full_name,
    email = @email,
    updated_at = CURRENT_TIMESTAMP
WHERE user_id = @user_id;";
            if (linkedEmployeeId.HasValue)
            {
                var (lastName, firstName, middleName) = UserAccountIdentitySync.ParseDisplayName(dto.FullName);
                if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
                {
                    throw new InvalidOperationException("Linked employee profiles require a full name in 'Last, First Middle' format.");
                }

                const string updateEmployeeSql = @"
UPDATE employees
SET last_name = @last_name,
    first_name = @first_name,
    middle_name = @middle_name,
    email = @email,
    updated_at = CURRENT_TIMESTAMP
WHERE employee_id = @employee_id;";

                await using (var employeeCommand = new MySqlCommand(updateEmployeeSql, connection, transaction))
                {
                    employeeCommand.Parameters.AddWithValue("@last_name", lastName);
                    employeeCommand.Parameters.AddWithValue("@first_name", firstName);
                    employeeCommand.Parameters.AddWithValue("@middle_name", string.IsNullOrWhiteSpace(middleName) ? DBNull.Value : middleName);
                    employeeCommand.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(dto.Email) ? DBNull.Value : dto.Email.Trim());
                    employeeCommand.Parameters.AddWithValue("@employee_id", linkedEmployeeId.Value);
                    await employeeCommand.ExecuteNonQueryAsync();
                }

                const string linkedProfileSql = @"
UPDATE user_accounts
SET username = @username,
    updated_at = CURRENT_TIMESTAMP
WHERE user_id = @user_id;";

                await using (var linkedProfileCommand = new MySqlCommand(linkedProfileSql, connection, transaction))
                {
                    linkedProfileCommand.Parameters.AddWithValue("@username", normalizedUsername);
                    linkedProfileCommand.Parameters.AddWithValue("@user_id", dto.UserId);
                    var profileAffected = await linkedProfileCommand.ExecuteNonQueryAsync();
                    if (profileAffected == 0)
                    {
                        throw new InvalidOperationException("User profile was not found.");
                    }
                }

                await UserAccountIdentitySync.SyncLinkedAccountAsync(connection, linkedEmployeeId.Value, transaction);
            }
            else
            {
                await using var profileCommand = new MySqlCommand(profileSql, connection, transaction);
                profileCommand.Parameters.AddWithValue("@username", normalizedUsername);
                profileCommand.Parameters.AddWithValue("@full_name", string.IsNullOrWhiteSpace(dto.FullName) ? DBNull.Value : dto.FullName.Trim());
                profileCommand.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(dto.Email) ? DBNull.Value : dto.Email.Trim());
                profileCommand.Parameters.AddWithValue("@user_id", dto.UserId);

                var profileAffected = await profileCommand.ExecuteNonQueryAsync();
                if (profileAffected == 0)
                {
                    throw new InvalidOperationException("User profile was not found.");
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                if (dto.NewPassword.Trim().Length < 8)
                {
                    throw new InvalidOperationException("Password must be at least 8 characters.");
                }

                const string passwordSql = @"
UPDATE user_accounts
SET password_hash = @password_hash,
    must_change_password = 0,
    password_changed_at = CURRENT_TIMESTAMP,
    updated_at = CURRENT_TIMESTAMP
WHERE user_id = @user_id;";

                await using var passwordCommand = new MySqlCommand(passwordSql, connection, transaction);
                passwordCommand.Parameters.AddWithValue("@password_hash", PasswordSecurity.HashPassword(dto.NewPassword.Trim()));
                passwordCommand.Parameters.AddWithValue("@user_id", dto.UserId);
                await passwordCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        private static async Task<int> CountAsync(MySqlConnection connection, string sql)
        {
            await using var command = new MySqlCommand(sql, connection);
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
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
            return value == null || value == DBNull.Value ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string NormalizeUsername(string username) => username.Trim();

        private static async Task<bool> UsernameExistsForOtherUserAsync(MySqlConnection connection, string username, int userId)
        {
            const string sql = @"
SELECT 1
FROM user_accounts
WHERE LOWER(TRIM(username)) = LOWER(@username)
  AND user_id <> @user_id
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@user_id", userId);
            var value = await command.ExecuteScalarAsync();
            return value != null && value != DBNull.Value;
        }

        private static string NormalizeStatus(string? status)
        {
            var text = status?.Trim().ToUpperInvariant();
            return text switch
            {
                "ACTIVE" => "ACTIVE",
                "INACTIVE" => "INACTIVE",
                "LOCKED" => "LOCKED",
                _ => "ACTIVE"
            };
        }

        private async Task EnsureAdminAccessAsync(
            MySqlConnection connection,
            int actingUserId,
            string actionLabel,
            string actionCode,
            string targetType,
            string? targetId)
        {
            var roleName = await AuthorizationGuard.GetRoleNameAsync(connection, actingUserId);
            if (AuthorizationGuard.IsAdmin(roleName))
            {
                return;
            }

            await _auditLogWriter.TryWriteAsync(
                actingUserId > 0 ? actingUserId : null,
                actionCode,
                targetType,
                targetId,
                "DENIED",
                roleName is null
                    ? "Actor role could not be resolved."
                    : $"Role '{roleName}' is not allowed.");

            AuthorizationGuard.DemandAdmin(roleName, actionLabel);
        }
    }
}
