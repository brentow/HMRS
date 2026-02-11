using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record LeaveStatsDto(int TotalRequests, int PendingRequests, int ApprovedRequests, int RejectedRequests, int LeaveTypes);
    public record LeaveRequestDto(string Employee, string LeaveType, DateTime StartDate, DateTime EndDate, string Status, DateTime RequestedAt);

    public class LeaveDataService
    {
        private readonly string _connectionString;

        public LeaveDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<LeaveStatsDto> GetStatsAsync()
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM leave_requests) AS total_requests,
                    (SELECT COUNT(*) FROM leave_requests WHERE status = 'Pending') AS pending_requests,
                    (SELECT COUNT(*) FROM leave_requests WHERE status = 'Approved') AS approved_requests,
                    (SELECT COUNT(*) FROM leave_requests WHERE status = 'Rejected') AS rejected_requests,
                    (SELECT COUNT(*) FROM leave_types) AS leave_types;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LeaveStatsDto(
                    Convert.ToInt32(reader["total_requests"]),
                    Convert.ToInt32(reader["pending_requests"]),
                    Convert.ToInt32(reader["approved_requests"]),
                    Convert.ToInt32(reader["rejected_requests"]),
                    Convert.ToInt32(reader["leave_types"]));
            }
            return new LeaveStatsDto(0, 0, 0, 0, 0);
        }

        public async Task<IReadOnlyList<LeaveRequestDto>> GetRecentRequestsAsync(int limit = 10)
        {
            const string sql = @"
                SELECT CONCAT(e.first_name,' ',e.last_name) AS employee,
                       lt.name AS leave_type,
                       lr.start_date,
                       lr.end_date,
                       lr.status,
                       lr.requested_at
                FROM leave_requests lr
                INNER JOIN employees e ON e.id = lr.employee_id
                INNER JOIN leave_types lt ON lt.id = lr.leave_type_id
                ORDER BY lr.requested_at DESC
                LIMIT @lim;";

            var list = new List<LeaveRequestDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LeaveRequestDto(
                    reader.GetString(reader.GetOrdinal("employee")),
                    reader.GetString(reader.GetOrdinal("leave_type")),
                    reader.GetDateTime(reader.GetOrdinal("start_date")),
                    reader.GetDateTime(reader.GetOrdinal("end_date")),
                    reader.GetString(reader.GetOrdinal("status")),
                    reader.GetDateTime(reader.GetOrdinal("requested_at"))
                ));
            }
            return list;
        }
    }
}
