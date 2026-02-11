using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record AttendanceStatsDto(int TotalLogs, int TodayLogs, int PresentToday, int IncompleteLogs);
    public record AttendanceLogDto(string Employee, DateTime LogDate, TimeSpan? TimeIn, TimeSpan? TimeOut, string Source);

    public class AttendanceDataService
    {
        private readonly string _connectionString;

        public AttendanceDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<AttendanceStatsDto> GetStatsAsync()
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM attendance_logs) AS total_logs,
                    (SELECT COUNT(*) FROM attendance_logs WHERE log_date = CURDATE()) AS today_logs,
                    (SELECT COUNT(*) FROM attendance_logs WHERE log_date = CURDATE() AND time_in IS NOT NULL) AS present_today,
                    (SELECT COUNT(*) FROM attendance_logs WHERE time_out IS NULL) AS incomplete_logs;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new AttendanceStatsDto(
                    Convert.ToInt32(reader["total_logs"]),
                    Convert.ToInt32(reader["today_logs"]),
                    Convert.ToInt32(reader["present_today"]),
                    Convert.ToInt32(reader["incomplete_logs"]));
            }
            return new AttendanceStatsDto(0, 0, 0, 0);
        }

        public async Task<IReadOnlyList<AttendanceLogDto>> GetRecentLogsAsync(int limit = 12)
        {
            const string sql = @"
                SELECT CONCAT(e.first_name,' ',e.last_name) AS employee,
                       a.log_date,
                       a.time_in,
                       a.time_out,
                       IFNULL(a.source,'') AS source
                FROM attendance_logs a
                INNER JOIN employees e ON e.id = a.employee_id
                ORDER BY a.log_date DESC
                LIMIT @lim;";

            var list = new List<AttendanceLogDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(reader.GetOrdinal("employee"));
                var date = reader.GetDateTime(reader.GetOrdinal("log_date"));
                var timeIn = reader.IsDBNull(reader.GetOrdinal("time_in")) ? (TimeSpan?)null : (TimeSpan)reader.GetValue(reader.GetOrdinal("time_in"));
                var timeOut = reader.IsDBNull(reader.GetOrdinal("time_out")) ? (TimeSpan?)null : (TimeSpan)reader.GetValue(reader.GetOrdinal("time_out"));
                var source = reader.GetString(reader.GetOrdinal("source"));
                list.Add(new AttendanceLogDto(name, date, timeIn, timeOut, source));
            }
            return list;
        }
    }
}
