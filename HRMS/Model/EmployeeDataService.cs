using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record EmployeeStatsDto(int TotalEmployees, int ActiveEmployees, int Departments, int Positions);
    public record LookupItemDto(int Id, string Name);
    public record PositionLookupDto(int PositionId, int? DepartmentId, string Name);
    public record NewEmployeeDto(
        string EmployeeNo,
        string LastName,
        string FirstName,
        string? MiddleName,
        string? Sex,
        DateTime? BirthDate,
        string? CivilStatus,
        string? Email,
        string? ContactNumber,
        string? Address,
        int? DepartmentId,
        int? PositionId,
        int? AppointmentTypeId,
        int? SalaryGrade,
        int? StepNo,
        DateTime? HireDate,
        string? TinNo,
        string? GsisBpNo,
        string? PhilHealthNo,
        string? PagibigMidNo,
        string? EmergencyContact,
        string? EmergencyPhone);

    public record UpdateEmployeeProfileDto(
        string OriginalEmployeeNo,
        string EmployeeNo,
        string FullName,
        string DepartmentName,
        string PositionName,
        string Status,
        string AppointmentTypeName,
        string SalaryGradeText,
        string SalaryStepText,
        DateTime HireDate,
        string TinNo,
        string GsisBpNo,
        string PhilHealthNo,
        string PagibigMidNo);

    public record EmployeeRowDto(
        string EmployeeNo,
        string Name,
        string Department,
        string Position,
        DateTime HireDate,
        string Status,
        string AppointmentType,
        string SalaryGrade,
        string SalaryStep,
        decimal MonthlySalary,
        string TinNo,
        string GsisBpNo,
        string PhilHealthNo,
        string PagibigMidNo,
        DateTime? LastDtrDate,
        TimeSpan? LastTimeIn,
        TimeSpan? LastTimeOut,
        int LastWorkedMinutes);

    public class EmployeeDataService
    {
        private readonly string _connectionString;
        private static readonly SemaphoreSlim GovernmentIdProtectionLock = new(1, 1);
        private static bool _governmentIdProtectionEnsured;

        public EmployeeDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<EmployeeStatsDto> GetStatsAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var totalEmployees = await CountAsync(connection, "SELECT COUNT(*) FROM employees;");
                var activeEmployees = await CountAsync(connection, "SELECT COUNT(*) FROM employees WHERE status = 'ACTIVE';");
                var departments = await CountAsync(connection, "SELECT COUNT(*) FROM departments;");
                var positions = await CountAsync(connection, "SELECT COUNT(*) FROM positions;");

                return new EmployeeStatsDto(totalEmployees, activeEmployees, departments, positions);
            }
            catch (MySqlException)
            {
                // Keep Employees page usable even if DB objects are not yet ready.
                return new EmployeeStatsDto(0, 0, 0, 0);
            }
        }

        public async Task<IReadOnlyList<EmployeeRowDto>> GetRecentEmployeesAsync(int limit = 500)
        {
            if (limit <= 0)
            {
                return Array.Empty<EmployeeRowDto>();
            }

            const string sql = @"
SELECT
    e.employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    COALESCE(d.dept_name, '-') AS dept_name,
    COALESCE(p.position_name, '-') AS position_name,
    e.hire_date,
    COALESCE(e.status, 'ACTIVE') AS status,
    COALESCE(at.type_name, '-') AS appointment_type,
    CASE
        WHEN e.salary_grade IS NULL THEN '-'
        ELSE CONCAT('SG-', e.salary_grade)
    END AS salary_grade,
    CASE
        WHEN e.step_no IS NULL THEN '-'
        ELSE CAST(e.step_no AS CHAR)
    END AS salary_step,
    COALESCE(ss.monthly_rate, 0) AS monthly_salary,
    e.tin_no,
    e.gsis_bp_no,
    e.philhealth_no,
    e.pagibig_mid_no,
    dtr.work_date AS last_dtr_date,
    TIME(dtr.time_in) AS last_time_in,
    TIME(dtr.time_out) AS last_time_out,
    COALESCE(dtr.worked_minutes, 0) AS worked_minutes
FROM employees e
LEFT JOIN departments d ON d.department_id = e.department_id
LEFT JOIN positions p ON p.position_id = e.position_id
LEFT JOIN appointment_types at ON at.appointment_type_id = e.appointment_type_id
LEFT JOIN salary_steps ss
       ON ss.salary_grade = e.salary_grade
      AND ss.step_no = e.step_no
      AND ss.effectivity_date = (
            SELECT MAX(ss2.effectivity_date)
            FROM salary_steps ss2
            WHERE ss2.salary_grade = e.salary_grade
              AND ss2.step_no = e.step_no
              AND ss2.effectivity_date <= CURDATE()
      )
LEFT JOIN (
    SELECT d1.employee_id, d1.work_date, d1.time_in, d1.time_out, d1.worked_minutes
    FROM v_dtr_daily_effective d1
    INNER JOIN (
        SELECT employee_id, MAX(work_date) AS max_work_date
        FROM v_dtr_daily_effective
        GROUP BY employee_id
    ) latest
            ON latest.employee_id = d1.employee_id
           AND latest.max_work_date = d1.work_date
) dtr ON dtr.employee_id = e.employee_id
ORDER BY e.employee_no
LIMIT @limit;";

            const string fallbackSql = @"
SELECT
    e.employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    COALESCE(d.dept_name, '-') AS dept_name,
    COALESCE(p.position_name, '-') AS position_name,
    e.hire_date,
    COALESCE(e.status, 'ACTIVE') AS status,
    COALESCE(at.type_name, '-') AS appointment_type,
    CASE
        WHEN e.salary_grade IS NULL THEN '-'
        ELSE CONCAT('SG-', e.salary_grade)
    END AS salary_grade,
    CASE
        WHEN e.step_no IS NULL THEN '-'
        ELSE CAST(e.step_no AS CHAR)
    END AS salary_step,
    COALESCE(ss.monthly_rate, 0) AS monthly_salary,
    e.tin_no,
    e.gsis_bp_no,
    e.philhealth_no,
    e.pagibig_mid_no,
    dtr.work_date AS last_dtr_date,
    TIME(dtr.time_in) AS last_time_in,
    TIME(dtr.time_out) AS last_time_out,
    COALESCE(dtr.worked_minutes, 0) AS worked_minutes
FROM employees e
LEFT JOIN departments d ON d.department_id = e.department_id
LEFT JOIN positions p ON p.position_id = e.position_id
LEFT JOIN appointment_types at ON at.appointment_type_id = e.appointment_type_id
LEFT JOIN salary_steps ss
       ON ss.salary_grade = e.salary_grade
      AND ss.step_no = e.step_no
      AND ss.effectivity_date = (
            SELECT MAX(ss2.effectivity_date)
            FROM salary_steps ss2
            WHERE ss2.salary_grade = e.salary_grade
              AND ss2.step_no = e.step_no
              AND ss2.effectivity_date <= CURDATE()
      )
LEFT JOIN (
    SELECT
        daily.employee_id,
        daily.work_date,
        daily.time_in,
        daily.time_out,
        CASE
            WHEN daily.time_in IS NULL OR daily.time_out IS NULL THEN 0
            ELSE TIMESTAMPDIFF(MINUTE, daily.time_in, daily.time_out)
        END AS worked_minutes
    FROM (
        SELECT
            al.employee_id,
            DATE(al.log_time) AS work_date,
            MIN(CASE WHEN al.log_type = 'IN' THEN al.log_time END) AS time_in,
            MAX(CASE WHEN al.log_type = 'OUT' THEN al.log_time END) AS time_out
        FROM attendance_logs al
        GROUP BY al.employee_id, DATE(al.log_time)
    ) daily
    INNER JOIN (
        SELECT
            al.employee_id,
            MAX(DATE(al.log_time)) AS max_work_date
        FROM attendance_logs al
        GROUP BY al.employee_id
    ) latest
            ON latest.employee_id = daily.employee_id
           AND latest.max_work_date = daily.work_date
) dtr ON dtr.employee_id = e.employee_id
ORDER BY e.employee_no
LIMIT @limit;";

            var list = new List<EmployeeRowDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await EnsureGovernmentIdsProtectedAsync(connection);

                await using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@limit", limit);

                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var hireDate = reader["hire_date"] == DBNull.Value
                            ? DateTime.Today
                            : Convert.ToDateTime(reader["hire_date"], CultureInfo.InvariantCulture);

                        list.Add(new EmployeeRowDto(
                            EmployeeNo: reader["employee_no"]?.ToString() ?? string.Empty,
                            Name: reader["employee_name"]?.ToString() ?? string.Empty,
                            Department: reader["dept_name"]?.ToString() ?? "-",
                            Position: reader["position_name"]?.ToString() ?? "-",
                            HireDate: hireDate,
                            Status: reader["status"]?.ToString() ?? "ACTIVE",
                            AppointmentType: reader["appointment_type"]?.ToString() ?? "-",
                            SalaryGrade: reader["salary_grade"]?.ToString() ?? "-",
                            SalaryStep: reader["salary_step"]?.ToString() ?? "-",
                            MonthlySalary: reader["monthly_salary"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["monthly_salary"], CultureInfo.InvariantCulture),
                            TinNo: SafeGovernmentIdValue(reader["tin_no"]),
                            GsisBpNo: SafeGovernmentIdValue(reader["gsis_bp_no"]),
                            PhilHealthNo: SafeGovernmentIdValue(reader["philhealth_no"]),
                            PagibigMidNo: SafeGovernmentIdValue(reader["pagibig_mid_no"]),
                            LastDtrDate: reader["last_dtr_date"] == DBNull.Value ? null : Convert.ToDateTime(reader["last_dtr_date"], CultureInfo.InvariantCulture),
                            LastTimeIn: ToTimeSpan(reader["last_time_in"]),
                            LastTimeOut: ToTimeSpan(reader["last_time_out"]),
                            LastWorkedMinutes: reader["worked_minutes"] == DBNull.Value ? 0 : Convert.ToInt32(reader["worked_minutes"], CultureInfo.InvariantCulture)
                        ));
                    }
                }
            }
            catch (MySqlException)
            {
                try
                {
                    await using var connection = new MySqlConnection(_connectionString);
                    await connection.OpenAsync();
                    await EnsureGovernmentIdsProtectedAsync(connection);

                    await using var command = new MySqlCommand(fallbackSql, connection);
                    command.Parameters.AddWithValue("@limit", limit);

                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var hireDate = reader["hire_date"] == DBNull.Value
                            ? DateTime.Today
                            : Convert.ToDateTime(reader["hire_date"], CultureInfo.InvariantCulture);

                        list.Add(new EmployeeRowDto(
                            EmployeeNo: reader["employee_no"]?.ToString() ?? string.Empty,
                            Name: reader["employee_name"]?.ToString() ?? string.Empty,
                            Department: reader["dept_name"]?.ToString() ?? "-",
                            Position: reader["position_name"]?.ToString() ?? "-",
                            HireDate: hireDate,
                            Status: reader["status"]?.ToString() ?? "ACTIVE",
                            AppointmentType: reader["appointment_type"]?.ToString() ?? "-",
                            SalaryGrade: reader["salary_grade"]?.ToString() ?? "-",
                            SalaryStep: reader["salary_step"]?.ToString() ?? "-",
                            MonthlySalary: reader["monthly_salary"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["monthly_salary"], CultureInfo.InvariantCulture),
                            TinNo: SafeGovernmentIdValue(reader["tin_no"]),
                            GsisBpNo: SafeGovernmentIdValue(reader["gsis_bp_no"]),
                            PhilHealthNo: SafeGovernmentIdValue(reader["philhealth_no"]),
                            PagibigMidNo: SafeGovernmentIdValue(reader["pagibig_mid_no"]),
                            LastDtrDate: reader["last_dtr_date"] == DBNull.Value ? null : Convert.ToDateTime(reader["last_dtr_date"], CultureInfo.InvariantCulture),
                            LastTimeIn: ToTimeSpan(reader["last_time_in"]),
                            LastTimeOut: ToTimeSpan(reader["last_time_out"]),
                            LastWorkedMinutes: reader["worked_minutes"] == DBNull.Value ? 0 : Convert.ToInt32(reader["worked_minutes"], CultureInfo.InvariantCulture)
                        ));
                    }
                }
                catch (MySqlException)
                {
                    return Array.Empty<EmployeeRowDto>();
                }
            }

            return list;
        }

        public async Task<IReadOnlyList<LookupItemDto>> GetDepartmentsLookupAsync()
        {
            const string sql = @"
SELECT department_id, dept_name
FROM departments
ORDER BY dept_name;";

            var list = new List<LookupItemDto>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureGovernmentIdsProtectedAsync(connection);

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LookupItemDto(
                    Id: Convert.ToInt32(reader["department_id"], CultureInfo.InvariantCulture),
                    Name: reader["dept_name"]?.ToString() ?? string.Empty));
            }

            return list;
        }

        public async Task<IReadOnlyList<PositionLookupDto>> GetPositionsLookupAsync()
        {
            const string sql = @"
SELECT position_id, department_id, position_name
FROM positions
ORDER BY position_name;";

            var list = new List<PositionLookupDto>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PositionLookupDto(
                    PositionId: Convert.ToInt32(reader["position_id"], CultureInfo.InvariantCulture),
                    DepartmentId: reader["department_id"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(reader["department_id"], CultureInfo.InvariantCulture),
                    Name: reader["position_name"]?.ToString() ?? string.Empty));
            }

            return list;
        }

        public async Task<IReadOnlyList<LookupItemDto>> GetAppointmentTypesLookupAsync()
        {
            const string sql = @"
SELECT appointment_type_id, type_name
FROM appointment_types
ORDER BY type_name;";

            var list = new List<LookupItemDto>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LookupItemDto(
                    Id: Convert.ToInt32(reader["appointment_type_id"], CultureInfo.InvariantCulture),
                    Name: reader["type_name"]?.ToString() ?? string.Empty));
            }

            return list;
        }

        public async Task<IReadOnlyList<int>> GetSalaryGradesAsync()
        {
            const string sql = @"
SELECT salary_grade
FROM salary_grades
ORDER BY salary_grade;";

            var list = new List<int>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(Convert.ToInt32(reader["salary_grade"], CultureInfo.InvariantCulture));
            }

            return list;
        }

        public async Task<string> GetNextEmployeeNoAsync()
        {
            const string fallback = "E-1001";
            const string sql = @"
SELECT
    employee_no,
    CAST(REGEXP_SUBSTR(employee_no, '[0-9]+$') AS UNSIGNED) AS numeric_part
FROM employees
WHERE employee_no REGEXP '[0-9]+$'
ORDER BY numeric_part DESC
LIMIT 1;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return fallback;
                }

                var lastEmployeeNo = reader["employee_no"]?.ToString()?.Trim() ?? string.Empty;
                var numericPart = reader["numeric_part"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["numeric_part"], CultureInfo.InvariantCulture);

                if (numericPart <= 0)
                {
                    return fallback;
                }

                var match = Regex.Match(lastEmployeeNo, @"(?<prefix>.*?)(?<digits>\d+)$");
                var prefix = match.Success ? match.Groups["prefix"].Value : "E-";
                var digitWidth = match.Success ? match.Groups["digits"].Value.Length : 4;
                digitWidth = Math.Max(4, digitWidth);

                var nextNumber = numericPart + 1;
                return $"{prefix}{nextNumber.ToString($"D{digitWidth}", CultureInfo.InvariantCulture)}";
            }
            catch (MySqlException)
            {
                return fallback;
            }
        }

        public async Task<int> AddEmployeeAsync(NewEmployeeDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            const string sql = @"
INSERT INTO employees (
    employee_no,
    last_name,
    first_name,
    middle_name,
    sex,
    birth_date,
    civil_status,
    email,
    contact_number,
    address,
    department_id,
    position_id,
    appointment_type_id,
    salary_grade,
    step_no,
    hire_date,
    tin_no,
    gsis_bp_no,
    philhealth_no,
    pagibig_mid_no,
    emergency_contact,
    emergency_phone,
    status
)
VALUES (
    @employee_no,
    @last_name,
    @first_name,
    @middle_name,
    @sex,
    @birth_date,
    @civil_status,
    @email,
    @contact_number,
    @address,
    @department_id,
    @position_id,
    @appointment_type_id,
    @salary_grade,
    @step_no,
    @hire_date,
    @tin_no,
    @gsis_bp_no,
    @philhealth_no,
    @pagibig_mid_no,
    @emergency_contact,
    @emergency_phone,
    'ACTIVE'
);

SELECT LAST_INSERT_ID();";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_no", dto.EmployeeNo.Trim());
            command.Parameters.AddWithValue("@last_name", dto.LastName.Trim());
            command.Parameters.AddWithValue("@first_name", dto.FirstName.Trim());
            command.Parameters.AddWithValue("@middle_name", DbValue(dto.MiddleName));
            command.Parameters.AddWithValue("@sex", DbValue(NormalizeSex(dto.Sex)));
            command.Parameters.AddWithValue("@birth_date", DbValue(dto.BirthDate));
            command.Parameters.AddWithValue("@civil_status", DbValue(dto.CivilStatus));
            command.Parameters.AddWithValue("@email", DbValue(dto.Email));
            command.Parameters.AddWithValue("@contact_number", DbValue(dto.ContactNumber));
            command.Parameters.AddWithValue("@address", DbValue(dto.Address));
            command.Parameters.AddWithValue("@department_id", DbValue(dto.DepartmentId));
            command.Parameters.AddWithValue("@position_id", DbValue(dto.PositionId));
            command.Parameters.AddWithValue("@appointment_type_id", DbValue(dto.AppointmentTypeId));
            command.Parameters.AddWithValue("@salary_grade", DbValue(dto.SalaryGrade));
            command.Parameters.AddWithValue("@step_no", DbValue(dto.StepNo));
            command.Parameters.AddWithValue("@hire_date", DbValue(dto.HireDate));
            command.Parameters.AddWithValue("@tin_no", DbValue(SensitiveIdProtector.ProtectForStorage(dto.TinNo)));
            command.Parameters.AddWithValue("@gsis_bp_no", DbValue(SensitiveIdProtector.ProtectForStorage(dto.GsisBpNo)));
            command.Parameters.AddWithValue("@philhealth_no", DbValue(SensitiveIdProtector.ProtectForStorage(dto.PhilHealthNo)));
            command.Parameters.AddWithValue("@pagibig_mid_no", DbValue(SensitiveIdProtector.ProtectForStorage(dto.PagibigMidNo)));
            command.Parameters.AddWithValue("@emergency_contact", DbValue(dto.EmergencyContact));
            command.Parameters.AddWithValue("@emergency_phone", DbValue(dto.EmergencyPhone));

            var scalar = await command.ExecuteScalarAsync();
            return scalar == null || scalar == DBNull.Value ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }

        public async Task CreateDefaultUserAccountForEmployeeAsync(
            int employeeId,
            string employeeNo,
            string firstName,
            string lastName,
            string? email)
        {
            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Invalid employee id for user account creation.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureGovernmentIdsProtectedAsync(connection);

            if (await EmployeeAccountExistsAsync(connection, employeeId))
            {
                return;
            }

            var roleId = await ResolveRoleIdAsync(connection, "Employee");
            if (!roleId.HasValue)
            {
                throw new InvalidOperationException("Role 'Employee' was not found.");
            }

            var username = await BuildNextUsernameAsync(connection, employeeNo);
            var fullName = UserAccountIdentitySync.BuildEmployeeDisplayName(firstName, lastName, null);

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

            var tempPassword = string.IsNullOrWhiteSpace(employeeNo)
                ? PasswordSecurity.GenerateTemporaryPassword()
                : employeeNo.Trim();

            await using var insert = new MySqlCommand(insertSql, connection);
            insert.Parameters.AddWithValue("@role_id", roleId.Value);
            insert.Parameters.AddWithValue("@employee_id", employeeId);
            insert.Parameters.AddWithValue("@username", username);
            insert.Parameters.AddWithValue("@password_hash", PasswordSecurity.HashPassword(tempPassword));
            insert.Parameters.AddWithValue("@must_change_password", 1);
            insert.Parameters.AddWithValue("@full_name", string.IsNullOrWhiteSpace(fullName) ? DBNull.Value : fullName);
            insert.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email.Trim());
            await insert.ExecuteNonQueryAsync();
            await UserAccountIdentitySync.SyncLinkedAccountAsync(connection, employeeId);
        }

        public async Task UpdateEmployeeProfileAsync(UpdateEmployeeProfileDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            if (string.IsNullOrWhiteSpace(dto.OriginalEmployeeNo))
            {
                throw new InvalidOperationException("Original employee number is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.EmployeeNo))
            {
                throw new InvalidOperationException("Employee number is required.");
            }

            var (lastName, firstName, middleName) = ParseFullName(dto.FullName);
            if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
            {
                throw new InvalidOperationException("Full Name must be in 'Last Name, First Name Middle Name' format.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureGovernmentIdsProtectedAsync(connection);

            var departmentId = await ResolveDepartmentIdAsync(connection, dto.DepartmentName);
            var positionId = await ResolvePositionIdAsync(connection, dto.PositionName, departmentId);
            var appointmentTypeId = await ResolveAppointmentTypeIdAsync(connection, dto.AppointmentTypeName);

            const string sql = @"
UPDATE employees
SET employee_no = @new_employee_no,
    last_name = @last_name,
    first_name = @first_name,
    middle_name = @middle_name,
    department_id = @department_id,
    position_id = @position_id,
    appointment_type_id = @appointment_type_id,
    salary_grade = @salary_grade,
    step_no = @step_no,
    hire_date = @hire_date,
    status = @status,
    tin_no = @tin_no,
    gsis_bp_no = @gsis_bp_no,
    philhealth_no = @philhealth_no,
    pagibig_mid_no = @pagibig_mid_no
WHERE employee_no = @original_employee_no;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@new_employee_no", dto.EmployeeNo.Trim());
            command.Parameters.AddWithValue("@original_employee_no", dto.OriginalEmployeeNo.Trim());
            command.Parameters.AddWithValue("@last_name", lastName);
            command.Parameters.AddWithValue("@first_name", firstName);
            command.Parameters.AddWithValue("@middle_name", DbValue(middleName));
            command.Parameters.AddWithValue("@department_id", DbValue(departmentId));
            command.Parameters.AddWithValue("@position_id", DbValue(positionId));
            command.Parameters.AddWithValue("@appointment_type_id", DbValue(appointmentTypeId));
            command.Parameters.AddWithValue("@salary_grade", DbValue(ParseNullableIntText(dto.SalaryGradeText)));
            command.Parameters.AddWithValue("@step_no", DbValue(ParseNullableIntText(dto.SalaryStepText)));
            command.Parameters.AddWithValue("@hire_date", dto.HireDate);
            command.Parameters.AddWithValue("@status", NormalizeStatus(dto.Status));
            command.Parameters.AddWithValue("@tin_no", DbValue(SensitiveIdProtector.ProtectForStorage(dto.TinNo)));
            command.Parameters.AddWithValue("@gsis_bp_no", DbValue(SensitiveIdProtector.ProtectForStorage(dto.GsisBpNo)));
            command.Parameters.AddWithValue("@philhealth_no", DbValue(SensitiveIdProtector.ProtectForStorage(dto.PhilHealthNo)));
            command.Parameters.AddWithValue("@pagibig_mid_no", DbValue(SensitiveIdProtector.ProtectForStorage(dto.PagibigMidNo)));

            var affected = await command.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                throw new InvalidOperationException("Employee record not found.");
            }

            var employeeId = await ResolveEmployeeIdByEmployeeNoAsync(connection, dto.EmployeeNo.Trim());
            if (employeeId.HasValue)
            {
                await UserAccountIdentitySync.SyncLinkedAccountAsync(connection, employeeId.Value);
            }
        }

        private static string SafeGovernmentIdValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "-";
            }

            var text = value.ToString();
            string? decrypted;
            try
            {
                decrypted = SensitiveIdProtector.UnprotectToPlaintext(text);
            }
            catch
            {
                decrypted = SensitiveIdProtector.Normalize(text);
            }

            return string.IsNullOrWhiteSpace(decrypted) ? "-" : decrypted;
        }

        private static async Task EnsureGovernmentIdsProtectedAsync(MySqlConnection connection)
        {
            if (_governmentIdProtectionEnsured)
            {
                return;
            }

            await GovernmentIdProtectionLock.WaitAsync();
            try
            {
                if (_governmentIdProtectionEnsured)
                {
                    return;
                }

                const string selectSql = @"
SELECT employee_id, tin_no, gsis_bp_no, philhealth_no, pagibig_mid_no
FROM employees
WHERE (tin_no IS NOT NULL AND TRIM(tin_no) <> '' AND tin_no NOT LIKE 'enc:v1:%')
   OR (gsis_bp_no IS NOT NULL AND TRIM(gsis_bp_no) <> '' AND gsis_bp_no NOT LIKE 'enc:v1:%')
   OR (philhealth_no IS NOT NULL AND TRIM(philhealth_no) <> '' AND philhealth_no NOT LIKE 'enc:v1:%')
   OR (pagibig_mid_no IS NOT NULL AND TRIM(pagibig_mid_no) <> '' AND pagibig_mid_no NOT LIKE 'enc:v1:%');";

                var pendingRows = new List<(int EmployeeId, string? TinNo, string? GsisBpNo, string? PhilHealthNo, string? PagibigMidNo)>();

                await using (var select = new MySqlCommand(selectSql, connection))
                await using (var reader = await select.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        pendingRows.Add((
                            EmployeeId: Convert.ToInt32(reader["employee_id"], CultureInfo.InvariantCulture),
                            TinNo: reader["tin_no"] == DBNull.Value ? null : reader["tin_no"]?.ToString(),
                            GsisBpNo: reader["gsis_bp_no"] == DBNull.Value ? null : reader["gsis_bp_no"]?.ToString(),
                            PhilHealthNo: reader["philhealth_no"] == DBNull.Value ? null : reader["philhealth_no"]?.ToString(),
                            PagibigMidNo: reader["pagibig_mid_no"] == DBNull.Value ? null : reader["pagibig_mid_no"]?.ToString()));
                    }
                }

                if (pendingRows.Count == 0)
                {
                    _governmentIdProtectionEnsured = true;
                    return;
                }

                const string updateSql = @"
UPDATE employees
SET tin_no = @tin_no,
    gsis_bp_no = @gsis_bp_no,
    philhealth_no = @philhealth_no,
    pagibig_mid_no = @pagibig_mid_no
WHERE employee_id = @employee_id;";

                foreach (var row in pendingRows)
                {
                    await using var update = new MySqlCommand(updateSql, connection);
                    update.Parameters.AddWithValue("@employee_id", row.EmployeeId);
                    update.Parameters.AddWithValue("@tin_no", DbValue(SensitiveIdProtector.ProtectForStorage(row.TinNo)));
                    update.Parameters.AddWithValue("@gsis_bp_no", DbValue(SensitiveIdProtector.ProtectForStorage(row.GsisBpNo)));
                    update.Parameters.AddWithValue("@philhealth_no", DbValue(SensitiveIdProtector.ProtectForStorage(row.PhilHealthNo)));
                    update.Parameters.AddWithValue("@pagibig_mid_no", DbValue(SensitiveIdProtector.ProtectForStorage(row.PagibigMidNo)));
                    await update.ExecuteNonQueryAsync();
                }

                _governmentIdProtectionEnsured = true;
            }
            finally
            {
                GovernmentIdProtectionLock.Release();
            }
        }

        private static async Task<int> CountAsync(MySqlConnection connection, string sql)
        {
            await using var command = new MySqlCommand(sql, connection);
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static object DbValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

        private static object DbValue(int? value) => value.HasValue ? value.Value : DBNull.Value;

        private static object DbValue(DateTime? value) => value.HasValue ? value.Value : DBNull.Value;

        private static int? ParseNullableIntText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int?)null;
        }

        private static string NormalizeStatus(string? status)
        {
            var value = status?.Trim().ToUpperInvariant();
            return value switch
            {
                "ACTIVE" => "ACTIVE",
                "ON_LEAVE" => "ON_LEAVE",
                "RESIGNED" => "RESIGNED",
                "TERMINATED" => "TERMINATED",
                _ => "ACTIVE"
            };
        }

        private static (string LastName, string FirstName, string? MiddleName) ParseFullName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return (string.Empty, string.Empty, null);
            }

            var text = fullName.Trim();
            if (text.Contains(','))
            {
                var parts = text.Split(new[] { ',' }, 2, StringSplitOptions.RemoveEmptyEntries);
                var lastName = parts.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
                var firstMiddle = parts.ElementAtOrDefault(1)?.Trim() ?? string.Empty;
                var tokens = firstMiddle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var firstName = tokens.ElementAtOrDefault(0) ?? string.Empty;
                var middleName = tokens.Length > 1 ? string.Join(" ", tokens.Skip(1)) : null;
                return (lastName, firstName, middleName);
            }

            var nameTokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nameTokens.Length == 1)
            {
                return (nameTokens[0], string.Empty, null);
            }

            if (nameTokens.Length == 2)
            {
                return (nameTokens[1], nameTokens[0], null);
            }

            var first = nameTokens[0];
            var last = nameTokens[^1];
            var middle = string.Join(" ", nameTokens.Skip(1).Take(nameTokens.Length - 2));
            return (last, first, string.IsNullOrWhiteSpace(middle) ? null : middle);
        }

        private static async Task<int?> ResolveDepartmentIdAsync(MySqlConnection connection, string? departmentName)
        {
            if (string.IsNullOrWhiteSpace(departmentName) || departmentName.Trim() == "-")
            {
                return null;
            }

            const string sql = @"
SELECT department_id
FROM departments
WHERE dept_name = @name
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", departmentName.Trim());
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static async Task<int?> ResolvePositionIdAsync(MySqlConnection connection, string? positionName, int? departmentId)
        {
            if (string.IsNullOrWhiteSpace(positionName) || positionName.Trim() == "-")
            {
                return null;
            }

            const string sqlWithDepartment = @"
SELECT position_id
FROM positions
WHERE position_name = @name
  AND (department_id = @department_id OR department_id IS NULL)
ORDER BY CASE WHEN department_id = @department_id THEN 0 ELSE 1 END
LIMIT 1;";

            const string sqlWithoutDepartment = @"
SELECT position_id
FROM positions
WHERE position_name = @name
LIMIT 1;";

            await using var command = new MySqlCommand(departmentId.HasValue ? sqlWithDepartment : sqlWithoutDepartment, connection);
            command.Parameters.AddWithValue("@name", positionName.Trim());
            if (departmentId.HasValue)
            {
                command.Parameters.AddWithValue("@department_id", departmentId.Value);
            }

            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static async Task<int?> ResolveAppointmentTypeIdAsync(MySqlConnection connection, string? appointmentType)
        {
            if (string.IsNullOrWhiteSpace(appointmentType) || appointmentType.Trim() == "-")
            {
                return null;
            }

            const string sql = @"
SELECT appointment_type_id
FROM appointment_types
WHERE type_name = @name
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", appointmentType.Trim());
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string? NormalizeSex(string? sex)
        {
            if (string.IsNullOrWhiteSpace(sex))
            {
                return null;
            }

            return sex.Trim().ToUpperInvariant();
        }

        private static TimeSpan? ToTimeSpan(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            if (value is TimeSpan ts)
            {
                return ts;
            }

            if (value is DateTime dt)
            {
                return dt.TimeOfDay;
            }

            if (TimeSpan.TryParse(value.ToString(), CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static async Task<int?> ResolveEmployeeIdByEmployeeNoAsync(MySqlConnection connection, string employeeNo)
        {
            const string sql = @"
SELECT employee_id
FROM employees
WHERE employee_no = @employee_no
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_no", employeeNo);
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static async Task<bool> EmployeeAccountExistsAsync(MySqlConnection connection, int employeeId)
        {
            const string sql = @"
SELECT 1
FROM user_accounts
WHERE employee_id = @employee_id
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            var value = await command.ExecuteScalarAsync();
            return value != null && value != DBNull.Value;
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

        private static async Task<string> BuildNextUsernameAsync(MySqlConnection connection, string employeeNo)
        {
            var digits = new string((employeeNo ?? string.Empty).Where(char.IsDigit).ToArray());
            var baseUsername = string.IsNullOrWhiteSpace(digits) ? "employee" : $"emp{digits}";
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
    }
}
