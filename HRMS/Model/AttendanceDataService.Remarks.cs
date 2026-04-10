using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record AttendanceRemarkDto(
        long RemarkId,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        DateTime WorkDate,
        string RemarkType,
        string Details,
        DateTime CreatedAt);

    public partial class AttendanceDataService
    {
        public async Task<IReadOnlyList<AttendanceRemarkDto>> GetAttendanceRemarksAsync(int? employeeId = null, int limit = 300)
        {
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
WHERE (@employee_id IS NULL OR ar.employee_id = @employee_id)
ORDER BY ar.work_date DESC, ar.remark_id DESC
LIMIT @limit;";

            var rows = new List<AttendanceRemarkDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new AttendanceRemarkDto(
                        RemarkId: ToLong(reader["remark_id"]),
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        WorkDate: reader["work_date"] == DBNull.Value
                            ? DateTime.Today
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

        public async Task UpsertAttendanceRemarkAsync(int employeeId, DateTime workDate, string? remarkType, string? details)
        {
            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Employee is required.");
            }

            var normalizedRemarkType = NormalizeAttendanceRemarkType(remarkType);

            const string sql = @"
INSERT INTO attendance_remarks (employee_id, work_date, remark_type, details)
VALUES (@employee_id, @work_date, @remark_type, @details)
ON DUPLICATE KEY UPDATE
    details = VALUES(details);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@work_date", workDate.Date);
            command.Parameters.AddWithValue("@remark_type", normalizedRemarkType);
            command.Parameters.AddWithValue("@details", string.IsNullOrWhiteSpace(details) ? DBNull.Value : details.Trim());
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteAttendanceRemarkAsync(long remarkId, int? employeeId = null)
        {
            if (remarkId <= 0)
            {
                throw new InvalidOperationException("Attendance remark is required.");
            }

            const string sql = @"
DELETE FROM attendance_remarks
WHERE remark_id = @remark_id
  AND (@employee_id IS NULL OR employee_id = @employee_id);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@remark_id", remarkId);
            command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);
            var affected = await command.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                throw new InvalidOperationException("Attendance remark not found.");
            }
        }

        private static string NormalizeAttendanceRemarkType(string? remarkType)
        {
            return remarkType?.Trim().ToUpperInvariant() switch
            {
                "OB" => "OB",
                "TO" => "TO",
                "WFH" => "WFH",
                "CTO" => "CTO",
                "HOLIDAY" => "HOLIDAY",
                "SUSPENDED" => "SUSPENDED",
                _ => "OTHER"
            };
        }
    }
}
