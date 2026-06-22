using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record DtrDailyDto(
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        DateTime WorkDate,
        DateTime? TimeIn,
        DateTime? TimeOut,
        int WorkedMinutes,
        string Remarks,
        int LateMinutes,
        int EarlyOutMinutes);

    public record DtrMonthlyCertificationDto(
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        int WorkedDays,
        int WorkedMinutes,
        string CertifiedBy,
        DateTime? CertifiedAt,
        string VerifiedBy,
        DateTime? VerifiedAt,
        string Remarks);

    public partial class AttendanceDataService
    {
        public async Task<IReadOnlyList<DtrDailyDto>> GetDtrDailyRowsAsync(int year, int month, int? employeeId = null)
        {
            const string sql = @"
SELECT
    e.employee_id,
    COALESCE(e.employee_no,'-') employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
    cal.work_date,
    COALESCE(adj.requested_in, raw.time_in_raw) AS time_in,
    COALESCE(adj.requested_out, raw.time_out_raw) AS time_out,
    CASE
        WHEN COALESCE(adj.requested_in, raw.time_in_raw) IS NULL
          OR COALESCE(adj.requested_out, raw.time_out_raw) IS NULL
        THEN 0
        ELSE TIMESTAMPDIFF(
            MINUTE,
            COALESCE(adj.requested_in, raw.time_in_raw),
            COALESCE(adj.requested_out, raw.time_out_raw))
    END AS worked_minutes,
    COALESCE(GROUP_CONCAT(DISTINCT ar.remark_type ORDER BY ar.remark_type SEPARATOR ', '), '') remarks,
    CASE
        WHEN COALESCE(adj.requested_in, raw.time_in_raw) IS NULL THEN 0
        ELSE GREATEST(
            COALESCE(
                TIMESTAMPDIFF(
                    MINUTE,
                    DATE_ADD(
                        TIMESTAMP(cal.work_date, COALESCE(s.start_time, TIME('07:00:00'))),
                        INTERVAL COALESCE(s.grace_minutes, 10) MINUTE),
                    COALESCE(adj.requested_in, raw.time_in_raw)),
                0),
            0)
    END AS late_minutes,
    CASE
        WHEN COALESCE(adj.requested_out, raw.time_out_raw) IS NULL THEN 0
        ELSE GREATEST(
            COALESCE(
                TIMESTAMPDIFF(
                MINUTE,
                COALESCE(adj.requested_out, raw.time_out_raw),
                CASE
                    WHEN COALESCE(s.is_overnight, 0) = 1
                    THEN DATE_ADD(TIMESTAMP(cal.work_date, COALESCE(s.end_time, TIME('17:00:00'))), INTERVAL 1 DAY)
                    ELSE TIMESTAMP(cal.work_date, COALESCE(s.end_time, TIME('17:00:00')))
                END),
                0),
            0)
    END AS early_out_minutes
FROM employees e
JOIN (
    SELECT DATE_ADD(@month_start, INTERVAL n DAY) AS work_date
    FROM (
        SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
        UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9
        UNION ALL SELECT 10 UNION ALL SELECT 11 UNION ALL SELECT 12 UNION ALL SELECT 13 UNION ALL SELECT 14
        UNION ALL SELECT 15 UNION ALL SELECT 16 UNION ALL SELECT 17 UNION ALL SELECT 18 UNION ALL SELECT 19
        UNION ALL SELECT 20 UNION ALL SELECT 21 UNION ALL SELECT 22 UNION ALL SELECT 23 UNION ALL SELECT 24
        UNION ALL SELECT 25 UNION ALL SELECT 26 UNION ALL SELECT 27 UNION ALL SELECT 28 UNION ALL SELECT 29
        UNION ALL SELECT 30
    ) days
    WHERE DATE_ADD(@month_start, INTERVAL n DAY) < @next_month
) cal
LEFT JOIN (
    SELECT
        al.employee_id,
        DATE(al.log_time) AS work_date,
        MIN(CASE WHEN al.log_type='IN' THEN al.log_time END) AS time_in_raw,
        MAX(CASE WHEN al.log_type='OUT' THEN al.log_time END) AS time_out_raw
    FROM attendance_logs al
    WHERE al.log_time >= @month_start
      AND al.log_time < @next_month
    GROUP BY al.employee_id, DATE(al.log_time)
) raw
    ON raw.employee_id = e.employee_id
   AND raw.work_date = cal.work_date
LEFT JOIN attendance_adjustments adj
    ON adj.employee_id = e.employee_id
   AND adj.work_date = cal.work_date
   AND adj.status = 'APPROVED'
LEFT JOIN attendance_remarks ar
    ON ar.employee_id = e.employee_id
   AND ar.work_date = cal.work_date
LEFT JOIN shift_assignments sa
    ON sa.assignment_id = (
        SELECT sa2.assignment_id
        FROM shift_assignments sa2
        WHERE sa2.employee_id = e.employee_id
          AND sa2.status = 'ASSIGNED'
          AND sa2.start_date <= cal.work_date
          AND (sa2.end_date IS NULL OR sa2.end_date >= cal.work_date)
        ORDER BY sa2.start_date DESC, sa2.assignment_id DESC
        LIMIT 1
    )
LEFT JOIN shifts s ON s.shift_id = sa.shift_id
WHERE e.status = 'ACTIVE'
  AND (@employee_id IS NULL OR e.employee_id = @employee_id)
GROUP BY e.employee_id, e.employee_no, e.last_name, e.first_name, e.middle_name,
         cal.work_date, raw.time_in_raw, raw.time_out_raw, adj.requested_in, adj.requested_out,
         s.start_time, s.end_time, s.grace_minutes, s.is_overnight
ORDER BY e.employee_no, cal.work_date;";

            var list = new List<DtrDailyDto>();
            try
            {
                var monthStart = new DateTime(year, month, 1);
                var nextMonth = monthStart.AddMonths(1);

                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@month_start", monthStart);
                command.Parameters.AddWithValue("@next_month", nextMonth);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DtrDailyDto(
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        WorkDate: reader["work_date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["work_date"], CultureInfo.InvariantCulture),
                        TimeIn: reader["time_in"] == DBNull.Value ? null : Convert.ToDateTime(reader["time_in"], CultureInfo.InvariantCulture),
                        TimeOut: reader["time_out"] == DBNull.Value ? null : Convert.ToDateTime(reader["time_out"], CultureInfo.InvariantCulture),
                        WorkedMinutes: ToInt(reader["worked_minutes"]),
                        Remarks: reader["remarks"]?.ToString() ?? string.Empty,
                        LateMinutes: ToInt(reader["late_minutes"]),
                        EarlyOutMinutes: ToInt(reader["early_out_minutes"])));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<DtrDailyDto>();
            }

            return list;
        }

        public async Task<IReadOnlyList<DtrMonthlyCertificationDto>> GetDtrMonthlyCertificationsAsync(int year, int month, int? employeeId = null)
        {
            const string sql = @"
SELECT
    e.employee_id,
    COALESCE(e.employee_no,'-') employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
    COALESCE(COUNT(d.work_date), 0) worked_days,
    COALESCE(SUM(d.worked_minutes), 0) worked_minutes,
    COALESCE(NULLIF(cb.full_name,''), cb.username, '-') certified_by,
    cert.certified_at,
    COALESCE(NULLIF(vb.full_name,''), vb.username, '-') verified_by,
    cert.verified_at,
    COALESCE(cert.remarks, '') remarks
FROM employees e
LEFT JOIN v_dtr_daily_effective d
       ON d.employee_id = e.employee_id
      AND YEAR(d.work_date) = @year
      AND MONTH(d.work_date) = @month
LEFT JOIN dtr_monthly_certifications cert
       ON cert.employee_id = e.employee_id
      AND cert.yr = @year
      AND cert.mo = @month
LEFT JOIN user_accounts cb ON cb.user_id = cert.certified_by_user_id
LEFT JOIN user_accounts vb ON vb.user_id = cert.verified_by_user_id
WHERE e.status = 'ACTIVE'
  AND (@employee_id IS NULL OR e.employee_id = @employee_id)
GROUP BY
    e.employee_id, e.employee_no, e.last_name, e.first_name, e.middle_name,
    cb.full_name, cb.username, cert.certified_at,
    vb.full_name, vb.username, cert.verified_at, cert.remarks
ORDER BY e.employee_no;";

            var list = new List<DtrMonthlyCertificationDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@year", year);
                command.Parameters.AddWithValue("@month", month);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DtrMonthlyCertificationDto(
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        WorkedDays: ToInt(reader["worked_days"]),
                        WorkedMinutes: ToInt(reader["worked_minutes"]),
                        CertifiedBy: reader["certified_by"]?.ToString() ?? "-",
                        CertifiedAt: reader["certified_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["certified_at"], CultureInfo.InvariantCulture),
                        VerifiedBy: reader["verified_by"]?.ToString() ?? "-",
                        VerifiedAt: reader["verified_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["verified_at"], CultureInfo.InvariantCulture),
                        Remarks: reader["remarks"]?.ToString() ?? string.Empty));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<DtrMonthlyCertificationDto>();
            }

            return list;
        }

        public async Task UpsertDtrCertificationAsync(int employeeId, int year, int month, int? certifiedByUserId, string? remarks)
        {
            const string sql = @"
INSERT INTO dtr_monthly_certifications
    (employee_id, yr, mo, certified_by_user_id, certified_at, remarks)
VALUES
    (@employee_id, @year, @month, @certified_by_user_id, NOW(), @remarks)
ON DUPLICATE KEY UPDATE
    certified_by_user_id = VALUES(certified_by_user_id),
    certified_at = NOW(),
    remarks = VALUES(remarks);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.AddWithValue("@month", month);
            command.Parameters.AddWithValue("@certified_by_user_id", certifiedByUserId.HasValue && certifiedByUserId.Value > 0 ? certifiedByUserId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpsertDtrVerificationAsync(int employeeId, int year, int month, int? verifiedByUserId, string? remarks)
        {
            const string sql = @"
INSERT INTO dtr_monthly_certifications
    (employee_id, yr, mo, verified_by_user_id, verified_at, remarks)
VALUES
    (@employee_id, @year, @month, @verified_by_user_id, NOW(), @remarks)
ON DUPLICATE KEY UPDATE
    verified_by_user_id = VALUES(verified_by_user_id),
    verified_at = NOW(),
    remarks = VALUES(remarks);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.AddWithValue("@month", month);
            command.Parameters.AddWithValue("@verified_by_user_id", verifiedByUserId.HasValue && verifiedByUserId.Value > 0 ? verifiedByUserId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
            await command.ExecuteNonQueryAsync();
        }

        public async Task ClearDtrCertificationAsync(int employeeId, int year, int month)
        {
            const string sql = @"
DELETE FROM dtr_monthly_certifications
WHERE employee_id = @employee_id
  AND yr = @year
  AND mo = @month;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.AddWithValue("@month", month);
            await command.ExecuteNonQueryAsync();
        }
    }
}
