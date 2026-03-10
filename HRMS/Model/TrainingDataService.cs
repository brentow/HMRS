using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record TrainingCourseDto(
        int Id,
        string Title,
        string Provider,
        string Description,
        double Hours,
        string Status);

    public record TrainingEnrollmentDto(
        long EnrollmentId,
        int EmployeeId,
        string Employee,
        string Course,
        DateTime? ScheduleDate,
        string Status);

    public record TrainingEmployeeDto(int Id, string Name);

    public class TrainingDataService
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _enrollmentSchemaLock = new(1, 1);
        private bool _enrollmentSchemaEnsured;

        public TrainingDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<IReadOnlyList<TrainingCourseDto>> GetCoursesAsync()
        {
            const string sql = @"
SELECT
    c.course_id AS id,
    c.course_name AS title,
    COALESCE(
        NULLIF(TRIM(meta.provider), ''),
        MAX(COALESCE(ua.full_name, ua.username)),
        'LGU Training Unit'
    ) AS provider,
    COALESCE(c.description, '') AS description,
    COALESCE(meta.default_hours, COUNT(DISTINCT s.session_id) * 8, 0) AS hours,
    CASE
        WHEN UPPER(COALESCE(meta.manual_status, '')) = 'INACTIVE' THEN 'Inactive'
        WHEN UPPER(COALESCE(meta.manual_status, '')) = 'ACTIVE' THEN 'Active'
        WHEN COUNT(DISTINCT s.session_id) = 0 THEN 'Inactive'
        WHEN MAX(s.session_date) >= CURDATE() THEN 'Active'
        ELSE 'Completed'
    END AS status
FROM training_courses c
LEFT JOIN training_sessions s ON s.course_id = c.course_id
LEFT JOIN user_accounts ua ON ua.user_id = s.trainer_user_id
LEFT JOIN (
    SELECT
        course_id,
        MAX(NULLIF(TRIM(provider), '')) AS provider,
        MAX(default_hours) AS default_hours,
        CASE
            WHEN SUM(CASE WHEN UPPER(COALESCE(manual_status, '')) = 'INACTIVE' THEN 1 ELSE 0 END) > 0 THEN 'INACTIVE'
            WHEN SUM(CASE WHEN UPPER(COALESCE(manual_status, '')) = 'ACTIVE' THEN 1 ELSE 0 END) > 0 THEN 'ACTIVE'
            ELSE NULL
        END AS manual_status
    FROM training_course_settings
    GROUP BY course_id
) meta ON meta.course_id = c.course_id
GROUP BY c.course_id, c.course_name, c.description, meta.provider, meta.default_hours, meta.manual_status
ORDER BY c.course_name;";

            var list = new List<TrainingCourseDto>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureCourseSettingsTableAsync(connection);
            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new TrainingCourseDto(
                    Id: Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    Title: reader["title"]?.ToString() ?? string.Empty,
                    Provider: reader["provider"]?.ToString() ?? "LGU Training Unit",
                    Description: reader["description"]?.ToString() ?? string.Empty,
                    Hours: reader["hours"] == DBNull.Value ? 0 : Convert.ToDouble(reader["hours"], CultureInfo.InvariantCulture),
                    Status: reader["status"]?.ToString() ?? "Inactive"));
            }

            return list;
        }

        public async Task UpdateCourseAsync(TrainingCourseDto course)
        {
            ArgumentNullException.ThrowIfNull(course);

            const string updateCourseSql = @"
UPDATE training_courses
SET course_name = @title,
    description = @description
WHERE course_id = @id;";

            const string upsertMetaSql = @"
INSERT INTO training_course_settings (course_id, provider, default_hours, manual_status)
VALUES (@id, @provider, @hours, @status)
ON DUPLICATE KEY UPDATE
    provider = VALUES(provider),
    default_hours = VALUES(default_hours),
    manual_status = VALUES(manual_status),
    updated_at = CURRENT_TIMESTAMP;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureCourseSettingsTableAsync(connection);
            await using var transaction = await connection.BeginTransactionAsync();

            await using (var command = new MySqlCommand(updateCourseSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@id", course.Id);
                command.Parameters.AddWithValue("@title", course.Title.Trim());
                command.Parameters.AddWithValue("@description", DbValue(course.Description));
                await command.ExecuteNonQueryAsync();
            }

            await using (var metaCommand = new MySqlCommand(upsertMetaSql, connection, transaction))
            {
                metaCommand.Parameters.AddWithValue("@id", course.Id);
                metaCommand.Parameters.AddWithValue("@provider", DbValue(course.Provider));
                metaCommand.Parameters.AddWithValue("@hours", DbHoursValue(course.Hours));
                metaCommand.Parameters.AddWithValue("@status", DbCourseStatusValue(course.Status));
                await metaCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        public async Task<int> AddCourseAsync(TrainingCourseDto course)
        {
            ArgumentNullException.ThrowIfNull(course);

            const string insertCourseSql = @"
INSERT INTO training_courses (course_name, description)
VALUES (@title, @description);
SELECT LAST_INSERT_ID();";

            const string upsertMetaSql = @"
INSERT INTO training_course_settings (course_id, provider, default_hours, manual_status)
VALUES (@id, @provider, @hours, @status)
ON DUPLICATE KEY UPDATE
    provider = VALUES(provider),
    default_hours = VALUES(default_hours),
    manual_status = VALUES(manual_status),
    updated_at = CURRENT_TIMESTAMP;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureCourseSettingsTableAsync(connection);
            await using var transaction = await connection.BeginTransactionAsync();

            int newId;
            await using (var command = new MySqlCommand(insertCourseSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@title", course.Title.Trim());
                command.Parameters.AddWithValue("@description", DbValue(course.Description));
                var result = await command.ExecuteScalarAsync();
                newId = result == null || result == DBNull.Value
                    ? 0
                    : Convert.ToInt32(result, CultureInfo.InvariantCulture);
            }

            await using (var metaCommand = new MySqlCommand(upsertMetaSql, connection, transaction))
            {
                metaCommand.Parameters.AddWithValue("@id", newId);
                metaCommand.Parameters.AddWithValue("@provider", DbValue(course.Provider));
                metaCommand.Parameters.AddWithValue("@hours", DbHoursValue(course.Hours));
                metaCommand.Parameters.AddWithValue("@status", DbCourseStatusValue(course.Status));
                await metaCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return newId;
        }

        public async Task DeleteCourseAsync(int courseId)
        {
            const string sql = "DELETE FROM training_courses WHERE course_id = @id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", courseId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<TrainingEmployeeDto>> GetEmployeesAsync()
        {
            const string sql = @"
SELECT
    e.employee_id,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name
FROM employees e
WHERE e.status = 'ACTIVE'
ORDER BY e.last_name, e.first_name;";

            var list = new List<TrainingEmployeeDto>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new TrainingEmployeeDto(
                    Id: Convert.ToInt32(reader["employee_id"], CultureInfo.InvariantCulture),
                    Name: reader["employee_name"]?.ToString() ?? string.Empty));
            }

            return list;
        }

        public async Task<int> AddEnrollmentAsync(int employeeId, int courseId, DateTime scheduleDate, string status)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureEnrollmentStatusSchemaAsync(connection);
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                int sessionId;

                const string findSessionSql = @"
SELECT session_id
FROM training_sessions
WHERE course_id = @courseId
ORDER BY ABS(DATEDIFF(session_date, @scheduleDate)), session_date DESC
LIMIT 1;";

                await using (var findSession = new MySqlCommand(findSessionSql, connection, transaction))
                {
                    findSession.Parameters.AddWithValue("@courseId", courseId);
                    findSession.Parameters.AddWithValue("@scheduleDate", scheduleDate.Date);
                    var existing = await findSession.ExecuteScalarAsync();
                    sessionId = existing == null || existing == DBNull.Value
                        ? 0
                        : Convert.ToInt32(existing, CultureInfo.InvariantCulture);
                }

                if (sessionId == 0)
                {
                    const string addSessionSql = @"
INSERT INTO training_sessions (course_id, session_date, trainer_user_id, location)
VALUES (@courseId, @scheduleDate, NULL, 'TBD');
SELECT LAST_INSERT_ID();";

                    await using var addSession = new MySqlCommand(addSessionSql, connection, transaction);
                    addSession.Parameters.AddWithValue("@courseId", courseId);
                    addSession.Parameters.AddWithValue("@scheduleDate", scheduleDate.Date);
                    var newSession = await addSession.ExecuteScalarAsync();
                    sessionId = newSession == null || newSession == DBNull.Value
                        ? 0
                        : Convert.ToInt32(newSession, CultureInfo.InvariantCulture);
                }

                const string enrollSql = @"
INSERT INTO training_enrollments (session_id, employee_id, status)
VALUES (@sessionId, @employeeId, @status)
ON DUPLICATE KEY UPDATE
    status = VALUES(status),
    created_at = CURRENT_TIMESTAMP,
    enrollment_id = LAST_INSERT_ID(enrollment_id);
SELECT LAST_INSERT_ID();";

                await using var enroll = new MySqlCommand(enrollSql, connection, transaction);
                enroll.Parameters.AddWithValue("@sessionId", sessionId);
                enroll.Parameters.AddWithValue("@employeeId", employeeId);
                enroll.Parameters.AddWithValue("@status", NormalizeEnrollmentStatus(status));
                var result = await enroll.ExecuteScalarAsync();

                await transaction.CommitAsync();

                return result == null || result == DBNull.Value
                    ? 0
                    : Convert.ToInt32(result, CultureInfo.InvariantCulture);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IReadOnlyList<TrainingEnrollmentDto>> GetEnrollmentsAsync(int? employeeId = null)
        {
            const string sql = @"
SELECT
    te.enrollment_id AS enrollment_id,
    te.employee_id AS employee_id,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    c.course_name AS course_name,
    s.session_date AS schedule_date,
    CASE UPPER(COALESCE(te.status, ''))
        WHEN 'PENDING' THEN 'Enrolled'
        WHEN 'REQUESTED' THEN 'Requested'
        WHEN 'ENROLLED' THEN 'Enrolled'
        WHEN 'COMPLETED' THEN 'Completed'
        WHEN 'REJECTED' THEN 'Rejected'
        WHEN 'FAILED' THEN 'Failed'
        ELSE 'Enrolled'
    END AS status
FROM training_enrollments te
INNER JOIN training_sessions s ON s.session_id = te.session_id
INNER JOIN training_courses c ON c.course_id = s.course_id
INNER JOIN employees e ON e.employee_id = te.employee_id
WHERE (@employee_id IS NULL OR te.employee_id = @employee_id)
ORDER BY s.session_date DESC, c.course_name, employee_name;";

            var list = new List<TrainingEnrollmentDto>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureEnrollmentStatusSchemaAsync(connection);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new TrainingEnrollmentDto(
                    EnrollmentId: Convert.ToInt64(reader["enrollment_id"], CultureInfo.InvariantCulture),
                    EmployeeId: Convert.ToInt32(reader["employee_id"], CultureInfo.InvariantCulture),
                    Employee: reader["employee_name"]?.ToString() ?? string.Empty,
                    Course: reader["course_name"]?.ToString() ?? string.Empty,
                    ScheduleDate: reader["schedule_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["schedule_date"], CultureInfo.InvariantCulture),
                    Status: reader["status"]?.ToString() ?? "Enrolled"));
            }

            return list;
        }

        public async Task<int?> GetEmployeeIdByUserIdAsync(int userId)
        {
            if (userId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT employee_id
FROM user_accounts
WHERE user_id = @user_id
LIMIT 1;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public async Task UpdateEnrollmentStatusAsync(long enrollmentId, string status)
        {
            const string sql = @"
UPDATE training_enrollments
SET status = @status
WHERE enrollment_id = @id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureEnrollmentStatusSchemaAsync(connection);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", enrollmentId);
            command.Parameters.AddWithValue("@status", NormalizeEnrollmentStatus(status));
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteEnrollmentAsync(long enrollmentId)
        {
            const string sql = "DELETE FROM training_enrollments WHERE enrollment_id = @id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", enrollmentId);
            await command.ExecuteNonQueryAsync();
        }

        private static object DbValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

        private static object DbHoursValue(double value) =>
            value <= 0 ? DBNull.Value : value;

        private static object DbCourseStatusValue(string? status)
        {
            return status?.Trim().ToUpperInvariant() switch
            {
                "INACTIVE" => "INACTIVE",
                "ACTIVE" => "ACTIVE",
                _ => DBNull.Value
            };
        }

        private static string NormalizeEnrollmentStatus(string? status)
        {
            var value = status?.Trim().ToUpperInvariant();
            return value switch
            {
                "REQUEST" => "REQUESTED",
                "REQUESTED" => "REQUESTED",
                "ENROLLED" => "ENROLLED",
                "ENROLL" => "ENROLLED",
                "SCHEDULED" => "ENROLLED",
                "COMPLETE" => "COMPLETED",
                "COMPLETED" => "COMPLETED",
                "REJECT" => "REJECTED",
                "REJECTED" => "REJECTED",
                "FAILED" => "FAILED",
                _ => "REQUESTED"
            };
        }

        private async Task EnsureEnrollmentStatusSchemaAsync(MySqlConnection connection)
        {
            if (_enrollmentSchemaEnsured)
            {
                return;
            }

            await _enrollmentSchemaLock.WaitAsync();
            try
            {
                if (_enrollmentSchemaEnsured)
                {
                    return;
                }

                const string statusTypeSql = @"
SELECT COLUMN_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'training_enrollments'
  AND COLUMN_NAME = 'status'
LIMIT 1;";

                string? columnType;
                await using (var typeCommand = new MySqlCommand(statusTypeSql, connection))
                {
                    columnType = (await typeCommand.ExecuteScalarAsync())?.ToString();
                }

                if (string.IsNullOrWhiteSpace(columnType))
                {
                    _enrollmentSchemaEnsured = true;
                    return;
                }

                var normalized = columnType.ToUpperInvariant();
                var requiredStates = new[] { "'REQUESTED'", "'ENROLLED'", "'REJECTED'" };
                var missingRequired = requiredStates.Any(state => !normalized.Contains(state, StringComparison.Ordinal));

                if (missingRequired)
                {
                    const string alterStatusSql = @"
ALTER TABLE training_enrollments
MODIFY COLUMN status ENUM('PENDING','REQUESTED','ENROLLED','COMPLETED','REJECTED','FAILED')
NOT NULL DEFAULT 'REQUESTED';";

                    await using var alterCommand = new MySqlCommand(alterStatusSql, connection);
                    await alterCommand.ExecuteNonQueryAsync();
                }

                _enrollmentSchemaEnsured = true;
            }
            finally
            {
                _enrollmentSchemaLock.Release();
            }
        }

        private static async Task EnsureCourseSettingsTableAsync(MySqlConnection connection)
        {
            const string getTypeSql = @"
SELECT COLUMN_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'training_courses'
  AND COLUMN_NAME = 'course_id'
LIMIT 1;";

            string idType = "BIGINT";
            await using (var typeCmd = new MySqlCommand(getTypeSql, connection))
            {
                var value = await typeCmd.ExecuteScalarAsync();
                if (value is string s && !string.IsNullOrWhiteSpace(s))
                {
                    idType = s;
                }
            }

            var createSql = $@"
CREATE TABLE IF NOT EXISTS training_course_settings (
  course_id {idType} NOT NULL,
  provider VARCHAR(150) NULL,
  default_hours DECIMAL(8,2) NULL,
  manual_status ENUM('ACTIVE','INACTIVE') NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (course_id),
  CONSTRAINT fk_tcs_course FOREIGN KEY (course_id) REFERENCES training_courses(course_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            await using var createCmd = new MySqlCommand(createSql, connection);
            await createCmd.ExecuteNonQueryAsync();
        }
    }
}
