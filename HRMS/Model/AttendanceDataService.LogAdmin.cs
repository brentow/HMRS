using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record AttendanceLogInsertDto(
        int EmployeeId,
        int? DeviceId,
        DateTime LogTime,
        string LogType,
        string Source);

    public partial class AttendanceDataService
    {
        public async Task AddAttendanceLogAsync(int employeeId, int? deviceId, DateTime logTime, string logType, string source)
        {
            await AddAttendanceLogsBulkAsync(new[]
            {
                new AttendanceLogInsertDto(
                    EmployeeId: employeeId,
                    DeviceId: deviceId,
                    LogTime: logTime,
                    LogType: logType,
                    Source: source)
            });
        }

        public async Task<int> AddAttendanceLogsBulkAsync(IReadOnlyList<AttendanceLogInsertDto> logs)
        {
            if (logs == null || logs.Count == 0)
            {
                return 0;
            }

            const string sql = @"
INSERT INTO attendance_logs (employee_id, device_id, log_time, log_type, source)
VALUES (@employee_id, @device_id, @log_time, @log_type, @source);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new MySqlCommand(sql, connection, transaction);

            command.Parameters.Add("@employee_id", MySqlDbType.Int32);
            command.Parameters.Add("@device_id", MySqlDbType.Int32);
            command.Parameters.Add("@log_time", MySqlDbType.DateTime);
            command.Parameters.Add("@log_type", MySqlDbType.VarChar);
            command.Parameters.Add("@source", MySqlDbType.VarChar);

            var inserted = 0;
            foreach (var log in logs)
            {
                if (log.EmployeeId <= 0)
                {
                    continue;
                }

                command.Parameters["@employee_id"].Value = log.EmployeeId;
                command.Parameters["@device_id"].Value = log.DeviceId.HasValue && log.DeviceId.Value > 0 ? log.DeviceId.Value : DBNull.Value;
                command.Parameters["@log_time"].Value = log.LogTime;
                command.Parameters["@log_type"].Value = NormalizeLogType(log.LogType);
                command.Parameters["@source"].Value = NormalizeLogSource(log.Source);

                inserted += await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return inserted;
        }

        public async Task UpdateAttendanceLogAsync(long logId, int employeeId, int? deviceId, DateTime logTime, string logType, string source)
        {
            if (logId <= 0)
            {
                throw new InvalidOperationException("Invalid attendance log.");
            }

            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Employee is required.");
            }

            const string sql = @"
UPDATE attendance_logs
SET employee_id = @employee_id,
    device_id = @device_id,
    log_time = @log_time,
    log_type = @log_type,
    source = @source
WHERE log_id = @log_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@log_id", logId);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@device_id", deviceId.HasValue && deviceId.Value > 0 ? deviceId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@log_time", logTime);
            command.Parameters.AddWithValue("@log_type", NormalizeLogType(logType));
            command.Parameters.AddWithValue("@source", NormalizeLogSource(source));

            var affected = await command.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                throw new InvalidOperationException("Attendance log not found.");
            }
        }

        private static string NormalizeLogType(string? logType)
        {
            var value = (logType ?? string.Empty).Trim().ToUpperInvariant();
            return value switch
            {
                "IN" => "IN",
                "OUT" => "OUT",
                "BREAK_IN" => "BREAK_IN",
                "BREAK_OUT" => "BREAK_OUT",
                _ => throw new InvalidOperationException("Log type must be IN, OUT, BREAK_IN, or BREAK_OUT.")
            };
        }

        private static string NormalizeLogSource(string? source)
        {
            var value = (source ?? string.Empty).Trim().ToUpperInvariant();
            return value switch
            {
                "BIOMETRIC" => "BIOMETRIC",
                "MANUAL" => "MANUAL",
                "IMPORT" => "IMPORT",
                _ => throw new InvalidOperationException("Source must be BIOMETRIC, MANUAL, or IMPORT.")
            };
        }
    }
}
