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
