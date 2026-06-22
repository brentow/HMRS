using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record BiometricKioskPunchDto(
        long LogId,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        DateTime LogTime,
        string LogType,
        string Source,
        string DeviceName);

    public record EmployeeShiftScheduleDto(
        string ShiftName,
        TimeSpan StartTime,
        TimeSpan EndTime,
        int BreakMinutes,
        int GraceMinutes,
        bool IsOvernight);

    public partial class AttendanceDataService
    {
        public async Task<IReadOnlyList<BiometricKioskPunchDto>> GetEmployeeAttendanceLogsForDateAsync(int employeeId, DateTime workDate)
        {
            if (employeeId <= 0)
            {
                return Array.Empty<BiometricKioskPunchDto>();
            }

            const string sql = @"
SELECT
    al.log_id,
    e.employee_id,
    COALESCE(e.employee_no,'-') employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
    al.log_time,
    COALESCE(al.log_type,'-') log_type,
    COALESCE(al.source,'-') source,
    COALESCE(d.device_name,'-') device_name
FROM attendance_logs al
JOIN employees e ON e.employee_id = al.employee_id
LEFT JOIN biometric_devices d ON d.device_id = al.device_id
WHERE al.employee_id = @employee_id
  AND DATE(al.log_time) = DATE(@work_date)
ORDER BY al.log_time, al.log_id;";

            var list = new List<BiometricKioskPunchDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId);
                command.Parameters.AddWithValue("@work_date", workDate.Date);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new BiometricKioskPunchDto(
                        LogId: ToLong(reader["log_id"]),
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        LogTime: reader["log_time"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["log_time"], CultureInfo.InvariantCulture),
                        LogType: reader["log_type"]?.ToString() ?? "-",
                        Source: reader["source"]?.ToString() ?? "-",
                        DeviceName: reader["device_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<BiometricKioskPunchDto>();
            }

            return list;
        }

        public async Task<IReadOnlyList<BiometricKioskPunchDto>> GetEmployeeAttendanceLogsForMonthAsync(int employeeId, int year, int month)
        {
            if (employeeId <= 0)
            {
                return Array.Empty<BiometricKioskPunchDto>();
            }

            var monthStart = new DateTime(year, month, 1);
            var nextMonth = monthStart.AddMonths(1);

            const string sql = @"
SELECT
    al.log_id,
    e.employee_id,
    COALESCE(e.employee_no,'-') employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
    al.log_time,
    COALESCE(al.log_type,'-') log_type,
    COALESCE(al.source,'-') source,
    COALESCE(d.device_name,'-') device_name
FROM attendance_logs al
JOIN employees e ON e.employee_id = al.employee_id
LEFT JOIN biometric_devices d ON d.device_id = al.device_id
WHERE al.employee_id = @employee_id
  AND al.log_time >= @month_start
  AND al.log_time < @next_month
ORDER BY al.log_time, al.log_id;";

            var list = new List<BiometricKioskPunchDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId);
                command.Parameters.AddWithValue("@month_start", monthStart);
                command.Parameters.AddWithValue("@next_month", nextMonth);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new BiometricKioskPunchDto(
                        LogId: ToLong(reader["log_id"]),
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        LogTime: reader["log_time"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["log_time"], CultureInfo.InvariantCulture),
                        LogType: reader["log_type"]?.ToString() ?? "-",
                        Source: reader["source"]?.ToString() ?? "-",
                        DeviceName: reader["device_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<BiometricKioskPunchDto>();
            }

            return list;
        }

        public async Task<IReadOnlyList<AttendanceRemarkDto>> GetEmployeeAttendanceRemarksForMonthAsync(int employeeId, int year, int month)
        {
            if (employeeId <= 0)
            {
                return Array.Empty<AttendanceRemarkDto>();
            }

            var monthStart = new DateTime(year, month, 1);
            var nextMonth = monthStart.AddMonths(1);

            const string sql = @"
SELECT
    ar.remark_id,
    ar.employee_id,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    ar.work_date,
    ar.remark_type,
    COALESCE(ar.details, '') AS details,
    ar.created_at
FROM attendance_remarks ar
INNER JOIN employees e ON e.employee_id = ar.employee_id
WHERE ar.employee_id = @employee_id
  AND ar.work_date >= @month_start
  AND ar.work_date < @next_month
ORDER BY ar.work_date, ar.remark_id;";

            var rows = new List<AttendanceRemarkDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId);
                command.Parameters.AddWithValue("@month_start", monthStart.Date);
                command.Parameters.AddWithValue("@next_month", nextMonth.Date);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new AttendanceRemarkDto(
                        RemarkId: ToLong(reader["remark_id"]),
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        WorkDate: reader["work_date"] == DBNull.Value
                            ? monthStart
                            : Convert.ToDateTime(reader["work_date"], CultureInfo.InvariantCulture),
                        RemarkType: reader["remark_type"]?.ToString() ?? "OTHER",
                        Details: reader["details"]?.ToString() ?? string.Empty,
                        CreatedAt: reader["created_at"] == DBNull.Value
                            ? DateTime.Today
                            : Convert.ToDateTime(reader["created_at"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<AttendanceRemarkDto>();
            }

            return rows;
        }

        public async Task<EmployeeShiftScheduleDto?> GetEmployeeShiftForDateAsync(int employeeId, DateTime workDate)
        {
            if (employeeId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT
    COALESCE(s.shift_name, 'Assigned Shift') shift_name,
    s.start_time,
    s.end_time,
    COALESCE(s.break_minutes, 0) break_minutes,
    COALESCE(s.grace_minutes, 0) grace_minutes,
    COALESCE(s.is_overnight, 0) is_overnight
FROM shift_assignments sa
JOIN shifts s ON s.shift_id = sa.shift_id
WHERE sa.employee_id = @employee_id
  AND sa.status = 'ASSIGNED'
  AND sa.start_date <= @work_date
  AND (sa.end_date IS NULL OR sa.end_date >= @work_date)
ORDER BY sa.start_date DESC, sa.assignment_id DESC
LIMIT 1;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId);
                command.Parameters.AddWithValue("@work_date", workDate.Date);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new EmployeeShiftScheduleDto(
                        ShiftName: reader["shift_name"]?.ToString() ?? "Assigned Shift",
                        StartTime: reader["start_time"] == DBNull.Value ? TimeSpan.FromHours(7) : TimeSpan.Parse(reader["start_time"].ToString() ?? "07:00:00", CultureInfo.InvariantCulture),
                        EndTime: reader["end_time"] == DBNull.Value ? TimeSpan.FromHours(17) : TimeSpan.Parse(reader["end_time"].ToString() ?? "17:00:00", CultureInfo.InvariantCulture),
                        BreakMinutes: ToInt(reader["break_minutes"]),
                        GraceMinutes: ToInt(reader["grace_minutes"]),
                        IsOvernight: ToInt(reader["is_overnight"]) == 1);
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return null;
            }

            return null;
        }
    }
}
