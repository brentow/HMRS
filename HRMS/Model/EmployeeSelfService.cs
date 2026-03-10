using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record EmployeeDocumentDto(
        long SourceId,
        string DocumentType,
        string Title,
        string Details,
        string Status,
        DateTime EventAt,
        string ModuleKey,
        string? FilePath);

    public record EmployeeNotificationDto(
        long SourceId,
        string ModuleKey,
        string Title,
        string Message,
        string Status,
        DateTime EventAt,
        bool IsRead);

    public class EmployeeSelfService
    {
        private const string EnsureNotificationReadsTableSql = @"
CREATE TABLE IF NOT EXISTS employee_notification_reads (
    notification_read_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    employee_id INT NOT NULL,
    module_key VARCHAR(50) NOT NULL,
    source_id BIGINT NOT NULL,
    event_at DATETIME NOT NULL,
    read_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_employee_notification_read (employee_id, module_key, source_id, event_at),
    KEY idx_employee_notification_reads_employee (employee_id, read_at),
    CONSTRAINT fk_employee_notification_reads_employee
        FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
        ON UPDATE CASCADE
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        private readonly string _connectionString;

        public EmployeeSelfService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
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

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@user_id", userId);

                var scalar = await command.ExecuteScalarAsync();
                if (scalar == null || scalar == DBNull.Value)
                {
                    return null;
                }

                return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return null;
            }
        }

        public async Task<IReadOnlyList<EmployeeDocumentDto>> GetEmployeeDocumentsAsync(int employeeId, int limit = 250)
        {
            var safeLimit = Math.Clamp(limit, 20, 1000);
            var results = new List<EmployeeDocumentDto>();

            if (employeeId <= 0)
            {
                return results;
            }

            const string sql = @"
SELECT
    src.source_id,
    src.document_type,
    src.title,
    src.details,
    src.status,
    src.event_at,
    src.module_key,
    src.file_path
FROM (
    SELECT
        pr.payroll_run_id AS source_id,
        'Payslip' COLLATE utf8mb4_general_ci AS document_type,
        CONCAT('Payslip ', COALESCE(pp.period_code, CONCAT('#', pr.payroll_run_id))) COLLATE utf8mb4_general_ci AS title,
        CONCAT('Net Pay: PHP ', FORMAT(COALESCE(pr.net_pay, 0), 2)) COLLATE utf8mb4_general_ci AS details,
        COALESCE(pr.status, 'GENERATED') COLLATE utf8mb4_general_ci AS status,
        COALESCE(rel.released_at, pr.generated_at) AS event_at,
        'PAYROLL' COLLATE utf8mb4_general_ci AS module_key,
        CAST(NULL AS CHAR(255)) COLLATE utf8mb4_general_ci AS file_path
    FROM payroll_runs pr
    LEFT JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
    LEFT JOIN payslip_releases rel ON rel.payroll_run_id = pr.payroll_run_id
    WHERE pr.employee_id = @employee_id

    UNION ALL

    SELECT
        ld.leave_document_id AS source_id,
        'Leave Attachment' COLLATE utf8mb4_general_ci AS document_type,
        COALESCE(NULLIF(ld.file_name, ''), CONCAT('Leave Attachment #', ld.leave_document_id)) COLLATE utf8mb4_general_ci AS title,
        CONCAT(
            COALESCE(lt.name, 'Leave'),
            ' (',
            DATE_FORMAT(la.date_from, '%b %d, %Y'),
            ' - ',
            DATE_FORMAT(la.date_to, '%b %d, %Y'),
            ')'
        ) COLLATE utf8mb4_general_ci AS details,
        COALESCE(la.status, 'SUBMITTED') COLLATE utf8mb4_general_ci AS status,
        COALESCE(ld.uploaded_at, la.filed_at) AS event_at,
        'LEAVE' COLLATE utf8mb4_general_ci AS module_key,
        ld.file_path COLLATE utf8mb4_general_ci AS file_path
    FROM leave_documents ld
    INNER JOIN leave_applications la ON la.leave_application_id = ld.leave_application_id
    LEFT JOIN leave_types lt ON lt.leave_type_id = la.leave_type_id
    WHERE la.employee_id = @employee_id

    UNION ALL

    SELECT
        te.enrollment_id AS source_id,
        'Training Certificate' COLLATE utf8mb4_general_ci AS document_type,
        CONCAT(COALESCE(tc.course_name, 'Training Course'), ' Certificate') COLLATE utf8mb4_general_ci AS title,
        CONCAT('Session Date: ', DATE_FORMAT(ts.session_date, '%b %d, %Y')) COLLATE utf8mb4_general_ci AS details,
        COALESCE(te.status, 'COMPLETED') COLLATE utf8mb4_general_ci AS status,
        CAST(ts.session_date AS DATETIME) AS event_at,
        'DEVELOPMENT' COLLATE utf8mb4_general_ci AS module_key,
        CAST(NULL AS CHAR(255)) COLLATE utf8mb4_general_ci AS file_path
    FROM training_enrollments te
    INNER JOIN training_sessions ts ON ts.session_id = te.session_id
    INNER JOIN training_courses tc ON tc.course_id = ts.course_id
    WHERE te.employee_id = @employee_id
      AND te.status = 'COMPLETED'
) src
ORDER BY src.event_at DESC, src.source_id DESC
LIMIT @limit;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId);
                command.Parameters.AddWithValue("@limit", safeLimit);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new EmployeeDocumentDto(
                        SourceId: ToLong(reader["source_id"]),
                        DocumentType: ToText(reader["document_type"], "Document"),
                        Title: ToText(reader["title"], "Document"),
                        Details: ToText(reader["details"], "-"),
                        Status: ToText(reader["status"], "-"),
                        EventAt: ToDateTime(reader["event_at"]),
                        ModuleKey: ToText(reader["module_key"], "DASHBOARD"),
                        FilePath: reader["file_path"] == DBNull.Value ? null : reader["file_path"]?.ToString()));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // Keep UI stable when optional tables are not yet migrated.
            }

            return results;
        }

        public async Task<IReadOnlyList<EmployeeNotificationDto>> GetEmployeeNotificationsAsync(int employeeId, int limit = 300)
        {
            var safeLimit = Math.Clamp(limit, 20, 1000);
            var results = new List<EmployeeNotificationDto>();

            if (employeeId <= 0)
            {
                return results;
            }

            const string sql = @"
SELECT
    n.source_id,
    n.module_key,
    n.title,
    n.message,
    n.status,
    n.event_at,
    CASE
        WHEN enr.notification_read_id IS NULL THEN 0
        ELSE 1
    END AS is_read
FROM (
    SELECT
        la.leave_application_id AS source_id,
        'LEAVE' COLLATE utf8mb4_general_ci AS module_key,
        'Leave request update' COLLATE utf8mb4_general_ci AS title,
        CONCAT(
            COALESCE(lt.name, 'Leave'),
            ': ',
            COALESCE(la.status, 'SUBMITTED'),
            CASE
                WHEN COALESCE(la.decision_remarks, '') <> '' THEN CONCAT(' - ', la.decision_remarks)
                ELSE ''
            END
        ) COLLATE utf8mb4_general_ci AS message,
        COALESCE(la.status, 'SUBMITTED') COLLATE utf8mb4_general_ci AS status,
        COALESCE(la.decision_at, la.updated_at, la.filed_at) AS event_at
    FROM leave_applications la
    LEFT JOIN leave_types lt ON lt.leave_type_id = la.leave_type_id
    WHERE la.employee_id = @employee_id

    UNION ALL

    SELECT
        aa.adjustment_id AS source_id,
        'ADJUSTMENTS' COLLATE utf8mb4_general_ci AS module_key,
        'Attendance adjustment update' COLLATE utf8mb4_general_ci AS title,
        CONCAT(
            'Work date ',
            DATE_FORMAT(aa.work_date, '%b %d, %Y'),
            ': ',
            COALESCE(aa.status, 'PENDING')
        ) COLLATE utf8mb4_general_ci AS message,
        COALESCE(aa.status, 'PENDING') COLLATE utf8mb4_general_ci AS status,
        COALESCE(aa.decided_at, aa.requested_at) AS event_at
    FROM attendance_adjustments aa
    WHERE aa.employee_id = @employee_id

    UNION ALL

    SELECT
        rel.payslip_release_id AS source_id,
        'PAYROLL' COLLATE utf8mb4_general_ci AS module_key,
        'Payslip released' COLLATE utf8mb4_general_ci AS title,
        CONCAT(
            'Released for ',
            COALESCE(pp.period_code, CONCAT('run #', pr.payroll_run_id)),
            CASE
                WHEN COALESCE(rel.remarks, '') <> '' THEN CONCAT(' - ', rel.remarks)
                ELSE ''
            END
        ) COLLATE utf8mb4_general_ci AS message,
        'RELEASED' COLLATE utf8mb4_general_ci AS status,
        rel.released_at AS event_at
    FROM payslip_releases rel
    INNER JOIN payroll_runs pr ON pr.payroll_run_id = rel.payroll_run_id
    LEFT JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
    WHERE pr.employee_id = @employee_id

    UNION ALL

    SELECT
        te.enrollment_id AS source_id,
        'DEVELOPMENT' COLLATE utf8mb4_general_ci AS module_key,
        'Training enrollment update' COLLATE utf8mb4_general_ci AS title,
        CONCAT(COALESCE(tc.course_name, 'Training'), ': ', COALESCE(te.status, 'REQUESTED')) COLLATE utf8mb4_general_ci AS message,
        COALESCE(te.status, 'REQUESTED') COLLATE utf8mb4_general_ci AS status,
        te.created_at AS event_at
    FROM training_enrollments te
    INNER JOIN training_sessions ts ON ts.session_id = te.session_id
    INNER JOIN training_courses tc ON tc.course_id = ts.course_id
    WHERE te.employee_id = @employee_id
) n
LEFT JOIN employee_notification_reads enr
    ON enr.employee_id = @employee_id
   AND enr.module_key = n.module_key
   AND enr.source_id = n.source_id
   AND enr.event_at = n.event_at
ORDER BY n.event_at DESC, n.source_id DESC
LIMIT @limit;";

            const string fallbackSql = @"
SELECT
    n.source_id,
    n.module_key,
    n.title,
    n.message,
    n.status,
    n.event_at,
    0 AS is_read
FROM (
    SELECT
        la.leave_application_id AS source_id,
        'LEAVE' COLLATE utf8mb4_general_ci AS module_key,
        'Leave request update' COLLATE utf8mb4_general_ci AS title,
        CONCAT(
            COALESCE(lt.name, 'Leave'),
            ': ',
            COALESCE(la.status, 'SUBMITTED'),
            CASE
                WHEN COALESCE(la.decision_remarks, '') <> '' THEN CONCAT(' - ', la.decision_remarks)
                ELSE ''
            END
        ) COLLATE utf8mb4_general_ci AS message,
        COALESCE(la.status, 'SUBMITTED') COLLATE utf8mb4_general_ci AS status,
        COALESCE(la.decision_at, la.updated_at, la.filed_at) AS event_at
    FROM leave_applications la
    LEFT JOIN leave_types lt ON lt.leave_type_id = la.leave_type_id
    WHERE la.employee_id = @employee_id

    UNION ALL

    SELECT
        aa.adjustment_id AS source_id,
        'ADJUSTMENTS' COLLATE utf8mb4_general_ci AS module_key,
        'Attendance adjustment update' COLLATE utf8mb4_general_ci AS title,
        CONCAT(
            'Work date ',
            DATE_FORMAT(aa.work_date, '%b %d, %Y'),
            ': ',
            COALESCE(aa.status, 'PENDING')
        ) COLLATE utf8mb4_general_ci AS message,
        COALESCE(aa.status, 'PENDING') COLLATE utf8mb4_general_ci AS status,
        COALESCE(aa.decided_at, aa.requested_at) AS event_at
    FROM attendance_adjustments aa
    WHERE aa.employee_id = @employee_id

    UNION ALL

    SELECT
        rel.payslip_release_id AS source_id,
        'PAYROLL' COLLATE utf8mb4_general_ci AS module_key,
        'Payslip released' COLLATE utf8mb4_general_ci AS title,
        CONCAT(
            'Released for ',
            COALESCE(pp.period_code, CONCAT('run #', pr.payroll_run_id)),
            CASE
                WHEN COALESCE(rel.remarks, '') <> '' THEN CONCAT(' - ', rel.remarks)
                ELSE ''
            END
        ) COLLATE utf8mb4_general_ci AS message,
        'RELEASED' COLLATE utf8mb4_general_ci AS status,
        rel.released_at AS event_at
    FROM payslip_releases rel
    INNER JOIN payroll_runs pr ON pr.payroll_run_id = rel.payroll_run_id
    LEFT JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
    WHERE pr.employee_id = @employee_id

    UNION ALL

    SELECT
        te.enrollment_id AS source_id,
        'DEVELOPMENT' COLLATE utf8mb4_general_ci AS module_key,
        'Training enrollment update' COLLATE utf8mb4_general_ci AS title,
        CONCAT(COALESCE(tc.course_name, 'Training'), ': ', COALESCE(te.status, 'REQUESTED')) COLLATE utf8mb4_general_ci AS message,
        COALESCE(te.status, 'REQUESTED') COLLATE utf8mb4_general_ci AS status,
        te.created_at AS event_at
    FROM training_enrollments te
    INNER JOIN training_sessions ts ON ts.session_id = te.session_id
    INNER JOIN training_courses tc ON tc.course_id = ts.course_id
    WHERE te.employee_id = @employee_id
) n
ORDER BY n.event_at DESC, n.source_id DESC
LIMIT @limit;";

            try
            {
                await LoadEmployeeNotificationsAsync(sql, employeeId, safeLimit, results);
            }
            catch (MySqlException ex) when (ex.Number == 1146 && ex.Message.Contains("employee_notification_reads", StringComparison.OrdinalIgnoreCase))
            {
                if (await EnsureEmployeeNotificationReadsTableAsync())
                {
                    results.Clear();
                    await LoadEmployeeNotificationsAsync(sql, employeeId, safeLimit, results);
                }
                else
                {
                    await LoadEmployeeNotificationsAsync(fallbackSql, employeeId, safeLimit, results);
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // Keep UI stable when optional tables are not yet migrated.
            }

            return results;
        }

        public async Task MarkEmployeeNotificationAsReadAsync(int employeeId, string moduleKey, long sourceId, DateTime eventAt)
        {
            if (employeeId <= 0 || sourceId <= 0 || eventAt == DateTime.MinValue || string.IsNullOrWhiteSpace(moduleKey))
            {
                return;
            }

            const string sql = @"
INSERT INTO employee_notification_reads (
    employee_id,
    module_key,
    source_id,
    event_at
)
VALUES (
    @employee_id,
    @module_key,
    @source_id,
    @event_at
)
ON DUPLICATE KEY UPDATE
    read_at = CURRENT_TIMESTAMP;";

            try
            {
                await ExecuteMarkEmployeeNotificationAsReadAsync(sql, employeeId, moduleKey, sourceId, eventAt);
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                if (!await EnsureEmployeeNotificationReadsTableAsync())
                {
                    return;
                }

                try
                {
                    await ExecuteMarkEmployeeNotificationAsReadAsync(sql, employeeId, moduleKey, sourceId, eventAt);
                }
                catch (MySqlException retryEx) when (IsMissingObjectError(retryEx))
                {
                    // Keep notification UI usable even if DB permission/migration is not available.
                }
            }
        }

        private async Task LoadEmployeeNotificationsAsync(string sql, int employeeId, int safeLimit, List<EmployeeNotificationDto> results)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@limit", safeLimit);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new EmployeeNotificationDto(
                    SourceId: ToLong(reader["source_id"]),
                    ModuleKey: ToText(reader["module_key"], "DASHBOARD"),
                    Title: ToText(reader["title"], "Update"),
                    Message: ToText(reader["message"], "-"),
                    Status: ToText(reader["status"], "-"),
                    EventAt: ToDateTime(reader["event_at"]),
                    IsRead: ToBool(reader["is_read"])));
            }
        }

        private async Task ExecuteMarkEmployeeNotificationAsReadAsync(string sql, int employeeId, string moduleKey, long sourceId, DateTime eventAt)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@module_key", moduleKey.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("@source_id", sourceId);
            command.Parameters.AddWithValue("@event_at", eventAt);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<bool> EnsureEmployeeNotificationReadsTableAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(EnsureNotificationReadsTableSql, connection);
                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch (MySqlException)
            {
                return false;
            }
        }

        private static long ToLong(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static string ToText(object? value, string fallback)
        {
            if (value == null || value == DBNull.Value)
            {
                return fallback;
            }

            var text = value.ToString();
            return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        }

        private static DateTime ToDateTime(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return DateTime.MinValue;
            }

            if (value is DateTime dt)
            {
                return dt;
            }

            return DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : DateTime.MinValue;
        }

        private static bool ToBool(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return false;
            }

            if (value is bool boolean)
            {
                return boolean;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
        }

        private static bool IsMissingObjectError(MySqlException ex) =>
            ex.Number is 1054 or 1146 or 1049;
    }
}
