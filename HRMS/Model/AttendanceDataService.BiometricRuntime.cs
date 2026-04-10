using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record BiometricTemplateGalleryDto(
        int EnrollmentId,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        string BiometricUserId,
        int? DeviceId,
        string DeviceName,
        byte[] TemplateData,
        string TemplateFormat,
        string TemplateEncoding);

    public partial class AttendanceDataService
    {
        public async Task<IReadOnlyList<BiometricTemplateGalleryDto>> GetBiometricTemplateGalleryAsync(int? deviceId = null)
        {
            const string sql = @"
SELECT
    be.enrollment_id,
    e.employee_id,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    COALESCE(be.biometric_user_id, '-') AS biometric_user_id,
    be.device_id,
    COALESCE(d.device_name, '-') AS device_name,
    be.template_data,
    COALESCE(be.template_format, 'ANSI_378_2004') AS template_format,
    COALESCE(be.template_encoding, 'BINARY') AS template_encoding
FROM biometric_enrollments be
JOIN employees e ON e.employee_id = be.employee_id
LEFT JOIN biometric_devices d ON d.device_id = be.device_id
WHERE be.status = 'ACTIVE'
  AND be.template_data IS NOT NULL
  AND OCTET_LENGTH(be.template_data) > 0
  AND (@device_id IS NULL OR be.device_id = @device_id OR be.device_id IS NULL)
ORDER BY e.employee_no, be.enrollment_id DESC;";

            var list = new List<BiometricTemplateGalleryDto>();
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@device_id", deviceId.HasValue && deviceId.Value > 0 ? deviceId.Value : DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (reader["template_data"] is not byte[] templateBytes || templateBytes.Length == 0)
                {
                    continue;
                }

                list.Add(new BiometricTemplateGalleryDto(
                    ToInt(reader["enrollment_id"]),
                    ToInt(reader["employee_id"]),
                    reader["employee_no"]?.ToString() ?? "-",
                    reader["employee_name"]?.ToString() ?? "-",
                    reader["biometric_user_id"]?.ToString() ?? "-",
                    reader["device_id"] == DBNull.Value ? null : ToInt(reader["device_id"]),
                    reader["device_name"]?.ToString() ?? "-",
                    templateBytes,
                    reader["template_format"]?.ToString() ?? "ANSI_378_2004",
                    reader["template_encoding"]?.ToString() ?? "BINARY"));
            }

            return list;
        }

        public async Task<string?> GetLatestAttendanceLogTypeAsync(int employeeId, DateTime punchTime)
        {
            if (employeeId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT log_type
FROM attendance_logs
WHERE employee_id = @employee_id
  AND DATE(log_time) = DATE(@log_time)
ORDER BY log_time DESC, log_id DESC
LIMIT 1;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@log_time", punchTime);
            var result = await command.ExecuteScalarAsync();

            return result == null || result == DBNull.Value
                ? null
                : Convert.ToString(result, CultureInfo.InvariantCulture);
        }
    }
}
