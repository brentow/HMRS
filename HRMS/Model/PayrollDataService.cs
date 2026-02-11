using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record PayrollStatsDto(int TotalPeriods, int OpenPeriods, int TotalItems, decimal TotalNetPay);
    public record PayrollPeriodDto(string Name, DateTime From, DateTime To, string Status);
    public record PayrollRunDto(string Employee, string Period, decimal NetPay, DateTime GeneratedAt);

    public class PayrollDataService
    {
        private readonly string _connectionString;

        public PayrollDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<PayrollStatsDto> GetStatsAsync()
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM payroll_periods) AS total_periods,
                    (SELECT COUNT(*) FROM payroll_periods WHERE status = 'Open') AS open_periods,
                    (SELECT COUNT(*) FROM payroll_items) AS total_items,
                    (SELECT IFNULL(SUM(net_pay),0) FROM payroll_items) AS total_netpay;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new PayrollStatsDto(
                    Convert.ToInt32(reader["total_periods"]),
                    Convert.ToInt32(reader["open_periods"]),
                    Convert.ToInt32(reader["total_items"]),
                    Convert.ToDecimal(reader["total_netpay"]));
            }
            return new PayrollStatsDto(0, 0, 0, 0);
        }

        public async Task<IReadOnlyList<PayrollPeriodDto>> GetRecentPeriodsAsync(int limit = 6)
        {
            const string sql = @"
                SELECT period_name, date_from, date_to, status
                FROM payroll_periods
                ORDER BY date_from DESC
                LIMIT @lim;";

            var list = new List<PayrollPeriodDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PayrollPeriodDto(
                    reader.GetString(reader.GetOrdinal("period_name")),
                    reader.GetDateTime(reader.GetOrdinal("date_from")),
                    reader.GetDateTime(reader.GetOrdinal("date_to")),
                    reader.GetString(reader.GetOrdinal("status"))
                ));
            }
            return list;
        }

        public async Task<IReadOnlyList<PayrollRunDto>> GetRecentPayrollRunsAsync(int limit = 8)
        {
            const string sql = @"
                SELECT CONCAT(e.first_name,' ', e.last_name) AS employee,
                       pp.period_name,
                       pi.net_pay,
                       pi.generated_at
                FROM payroll_items pi
                INNER JOIN employees e ON e.id = pi.employee_id
                INNER JOIN payroll_periods pp ON pp.id = pi.payroll_period_id
                ORDER BY pi.generated_at DESC
                LIMIT @lim;";

            var list = new List<PayrollRunDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PayrollRunDto(
                    reader.GetString(reader.GetOrdinal("employee")),
                    reader.GetString(reader.GetOrdinal("period_name")),
                    reader.GetDecimal(reader.GetOrdinal("net_pay")),
                    reader.GetDateTime(reader.GetOrdinal("generated_at"))
                ));
            }
            return list;
        }
    }
}
