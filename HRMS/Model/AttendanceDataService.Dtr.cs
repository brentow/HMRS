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
        string Remarks);

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
    d.employee_id,
    COALESCE(e.employee_no,'-') employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
    d.work_date,
    d.time_in,
    d.time_out,
    COALESCE(d.worked_minutes, 0) worked_minutes,
    COALESCE(GROUP_CONCAT(DISTINCT ar.remark_type ORDER BY ar.remark_type SEPARATOR ', '), '') remarks
FROM v_dtr_daily_effective d
JOIN employees e ON e.employee_id = d.employee_id
LEFT JOIN attendance_remarks ar
    ON ar.employee_id = d.employee_id
   AND ar.work_date = d.work_date
WHERE YEAR(d.work_date) = @year
  AND MONTH(d.work_date) = @month
  AND (@employee_id IS NULL OR d.employee_id = @employee_id)
GROUP BY d.employee_id, e.employee_no, e.last_name, e.first_name, e.middle_name,
         d.work_date, d.time_in, d.time_out, d.worked_minutes
ORDER BY e.employee_no, d.work_date;";

            var list = new List<DtrDailyDto>();
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
                    list.Add(new DtrDailyDto(
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        WorkDate: reader["work_date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["work_date"], CultureInfo.InvariantCulture),
                        TimeIn: reader["time_in"] == DBNull.Value ? null : Convert.ToDateTime(reader["time_in"], CultureInfo.InvariantCulture),
                        TimeOut: reader["time_out"] == DBNull.Value ? null : Convert.ToDateTime(reader["time_out"], CultureInfo.InvariantCulture),
                        WorkedMinutes: ToInt(reader["worked_minutes"]),
                        Remarks: reader["remarks"]?.ToString() ?? string.Empty));
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
