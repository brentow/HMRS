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
        int EarlyOutMinutes,
        int OvertimeMinutes,
        int ScheduledMinutes,
        decimal AttendanceDeduction,
        string StatusCode);

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
    CONCAT_WS(', ',
        NULLIF(COALESCE(GROUP_CONCAT(
            DISTINCT CONCAT(ar.remark_type, CASE WHEN COALESCE(ar.details, '') = '' THEN '' ELSE CONCAT(': ', ar.details) END)
            ORDER BY ar.remark_type SEPARATOR ', '
        ), ''), ''),
        NULLIF(COALESCE(GROUP_CONCAT(DISTINCT CONCAT('LEAVE: ', COALESCE(lt.code, lt.name)) ORDER BY lt.code SEPARATOR ', '), ''), '')
    ) remarks,
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
    END AS early_out_minutes,
    COALESCE(s.start_time, TIME('07:00:00')) AS shift_start_time,
    COALESCE(s.end_time, TIME('17:00:00')) AS shift_end_time,
    COALESCE(s.break_minutes, 60) AS break_minutes,
    COALESCE(s.grace_minutes, 10) AS shift_grace_minutes,
    COALESCE(s.is_overnight, 0) AS is_overnight,
    COALESCE(ss.monthly_rate, 0) AS monthly_rate,
    MAX(CASE WHEN la.leave_application_id IS NOT NULL THEN 1 ELSE 0 END) AS has_approved_leave,
    MAX(CASE WHEN ar.remark_type IN ('HOLIDAY','TO','OB','CTO','SUSPENDED','WFH') THEN 1 ELSE 0 END) AS has_excused_remark
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
LEFT JOIN leave_applications la
    ON la.employee_id = e.employee_id
   AND la.status = 'APPROVED'
   AND cal.work_date BETWEEN la.date_from AND la.date_to
LEFT JOIN leave_types lt
    ON lt.leave_type_id = la.leave_type_id
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
LEFT JOIN (
    SELECT src.salary_grade, src.step_no, src.monthly_rate
    FROM salary_steps src
    INNER JOIN (
        SELECT salary_grade, step_no, MAX(effectivity_date) AS effectivity_date
        FROM salary_steps
        WHERE effectivity_date <= CURDATE()
        GROUP BY salary_grade, step_no
    ) eff
      ON eff.salary_grade = src.salary_grade
     AND eff.step_no = src.step_no
     AND eff.effectivity_date = src.effectivity_date
) ss
  ON ss.salary_grade = e.salary_grade
 AND ss.step_no = e.step_no
WHERE e.status = 'ACTIVE'
  AND (@employee_id IS NULL OR e.employee_id = @employee_id)
  AND (e.hire_date IS NULL OR cal.work_date >= e.hire_date)
GROUP BY e.employee_id, e.employee_no, e.last_name, e.first_name, e.middle_name,
         cal.work_date, raw.time_in_raw, raw.time_out_raw, adj.requested_in, adj.requested_out,
         s.start_time, s.end_time, s.break_minutes, s.grace_minutes, s.is_overnight, ss.monthly_rate
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
                    list.Add(BuildDtrDailyRow(reader));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<DtrDailyDto>();
            }

            return list;
        }

        private static DtrDailyDto BuildDtrDailyRow(MySqlDataReader reader)
        {
            var workDate = reader["work_date"] == DBNull.Value
                ? DateTime.MinValue
                : Convert.ToDateTime(reader["work_date"], CultureInfo.InvariantCulture).Date;
            var timeIn = reader["time_in"] == DBNull.Value
                ? (DateTime?)null
                : Convert.ToDateTime(reader["time_in"], CultureInfo.InvariantCulture);
            var timeOut = reader["time_out"] == DBNull.Value
                ? (DateTime?)null
                : Convert.ToDateTime(reader["time_out"], CultureInfo.InvariantCulture);
            var shiftStartTime = ReadTimeSpan(reader["shift_start_time"], new TimeSpan(7, 0, 0));
            var shiftEndTime = ReadTimeSpan(reader["shift_end_time"], new TimeSpan(17, 0, 0));
            var breakMinutes = Math.Max(0, ToInt(reader["break_minutes"]));
            var graceMinutes = Math.Max(0, ToInt(reader["shift_grace_minutes"]));
            var isOvernight = ToInt(reader["is_overnight"]) == 1;
            var monthlyRate = reader["monthly_rate"] == DBNull.Value
                ? 0m
                : Convert.ToDecimal(reader["monthly_rate"], CultureInfo.InvariantCulture);
            var remarks = reader["remarks"]?.ToString() ?? string.Empty;
            var hasApprovedLeave = ToInt(reader["has_approved_leave"]) == 1;
            var hasExcusedRemark = ToInt(reader["has_excused_remark"]) == 1;

            var shiftStart = workDate.Add(shiftStartTime);
            var shiftEnd = workDate.Add(shiftEndTime);
            if (isOvernight || shiftEnd <= shiftStart)
            {
                shiftEnd = shiftEnd.AddDays(1);
            }

            var scheduleSpanMinutes = Math.Max(0, (int)Math.Round((shiftEnd - shiftStart).TotalMinutes));
            var scheduledMinutes = Math.Max(0, scheduleSpanMinutes - breakMinutes);
            var hasCompletePunch = timeIn.HasValue && timeOut.HasValue && timeOut.Value >= timeIn.Value;
            var rawWorkedMinutes = hasCompletePunch
                ? Math.Max(0, (int)Math.Round((timeOut!.Value - timeIn!.Value).TotalMinutes))
                : 0;
            var appliedBreakMinutes = rawWorkedMinutes > 300 ? Math.Min(breakMinutes, rawWorkedMinutes) : 0;
            var workedMinutes = Math.Max(0, rawWorkedMinutes - appliedBreakMinutes);
            var lateMinutes = timeIn.HasValue
                ? Math.Max(0, (int)Math.Floor((timeIn.Value - shiftStart.AddMinutes(graceMinutes)).TotalMinutes))
                : 0;
            var earlyOutMinutes = timeOut.HasValue
                ? Math.Max(0, (int)Math.Ceiling((shiftEnd - timeOut.Value).TotalMinutes))
                : 0;
            var overtimeMinutes = timeOut.HasValue
                ? Math.Max(0, (int)Math.Floor((timeOut.Value - shiftEnd).TotalMinutes))
                : 0;

            var statusCode = ResolveDtrStatus(
                workDate,
                timeIn.HasValue,
                timeOut.HasValue,
                remarks,
                hasApprovedLeave,
                lateMinutes,
                earlyOutMinutes,
                overtimeMinutes);

            decimal attendanceDeduction = 0m;
            if (monthlyRate > 0m)
            {
                if (string.Equals(statusCode, "ABSENT", StringComparison.OrdinalIgnoreCase))
                {
                    attendanceDeduction = Math.Round(monthlyRate / 22m, 2, MidpointRounding.AwayFromZero);
                }
                else if (hasCompletePunch && !hasApprovedLeave && !hasExcusedRemark && !IsWeekend(workDate))
                {
                    attendanceDeduction = Math.Round(
                        monthlyRate / 22m / 8m / 60m * (lateMinutes + earlyOutMinutes),
                        2,
                        MidpointRounding.AwayFromZero);
                }
            }

            return new DtrDailyDto(
                EmployeeId: ToInt(reader["employee_id"]),
                EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                WorkDate: workDate,
                TimeIn: timeIn,
                TimeOut: timeOut,
                WorkedMinutes: workedMinutes,
                Remarks: remarks,
                LateMinutes: lateMinutes,
                EarlyOutMinutes: earlyOutMinutes,
                OvertimeMinutes: overtimeMinutes,
                ScheduledMinutes: scheduledMinutes,
                AttendanceDeduction: attendanceDeduction,
                StatusCode: statusCode);
        }

        private static string ResolveDtrStatus(
            DateTime workDate,
            bool hasTimeIn,
            bool hasTimeOut,
            string remarks,
            bool hasApprovedLeave,
            int lateMinutes,
            int earlyOutMinutes,
            int overtimeMinutes)
        {
            var normalizedRemarks = remarks?.ToUpperInvariant() ?? string.Empty;
            if (normalizedRemarks.Contains("HOLIDAY", StringComparison.Ordinal)) return "HOLIDAY";
            if (hasApprovedLeave || normalizedRemarks.Contains("LEAVE", StringComparison.Ordinal) || normalizedRemarks.Contains("CTO", StringComparison.Ordinal)) return "LEAVE";
            if (normalizedRemarks.Contains("TO", StringComparison.Ordinal) || normalizedRemarks.Contains("TRAVEL", StringComparison.Ordinal)) return "TRAVEL_ORDER";
            if (normalizedRemarks.Contains("OB", StringComparison.Ordinal) || normalizedRemarks.Contains("OFFICIAL", StringComparison.Ordinal) || normalizedRemarks.Contains("WFH", StringComparison.Ordinal)) return "OFFICIAL_BUSINESS";
            if (normalizedRemarks.Contains("ABSENT", StringComparison.Ordinal)) return "ABSENT";
            if (!hasTimeIn && !hasTimeOut && IsWeekend(workDate)) return "WEEKEND";
            if (!hasTimeIn && !hasTimeOut && workDate.Date >= DateTime.Today) return "PENDING";
            if (!hasTimeIn && !hasTimeOut) return "ABSENT";
            if (!hasTimeIn || !hasTimeOut) return "INCOMPLETE";
            if (lateMinutes > 0 && earlyOutMinutes > 0) return "LATE_UNDERTIME";
            if (earlyOutMinutes > 0) return "UNDERTIME";
            if (lateMinutes > 0) return "LATE";
            if (overtimeMinutes > 0) return "OVERTIME";
            return "PRESENT";
        }

        private static bool IsWeekend(DateTime date) =>
            date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        private static TimeSpan ReadTimeSpan(object value, TimeSpan fallback)
        {
            if (value == null || value == DBNull.Value) return fallback;
            if (value is TimeSpan timeSpan) return timeSpan;
            return TimeSpan.TryParse(value.ToString(), CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
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
