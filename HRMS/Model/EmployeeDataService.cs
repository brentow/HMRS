using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record EmployeeStatsDto(int TotalEmployees, int ActiveEmployees, int Departments, int Positions);
    public record EmployeeRowDto(string EmployeeNo, string Name, string Department, string Position, DateTime HireDate, string Status);

    public class EmployeeDataService
    {
        private readonly string _connectionString;

        public EmployeeDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<EmployeeStatsDto> GetStatsAsync()
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM employees) AS total_employees,
                    (SELECT COUNT(*) FROM employees WHERE status = 'Active') AS active_employees,
                    (SELECT COUNT(*) FROM departments) AS departments,
                    (SELECT COUNT(*) FROM positions) AS positions;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new EmployeeStatsDto(
                    Convert.ToInt32(reader["total_employees"]),
                    Convert.ToInt32(reader["active_employees"]),
                    Convert.ToInt32(reader["departments"]),
                    Convert.ToInt32(reader["positions"]));
            }
            return new EmployeeStatsDto(0, 0, 0, 0);
        }

        public async Task<IReadOnlyList<EmployeeRowDto>> GetRecentEmployeesAsync(int limit = 10)
        {
            const string sql = @"
                SELECT e.employee_no,
                       CONCAT(e.first_name,' ',e.last_name) AS name,
                       d.name AS department,
                       p.name AS position,
                       e.hire_date,
                       e.status
                FROM employees e
                LEFT JOIN departments d ON d.id = e.department_id
                LEFT JOIN positions p ON p.id = e.position_id
                ORDER BY e.hire_date DESC
                LIMIT @lim;";

            var list = new List<EmployeeRowDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new EmployeeRowDto(
                    reader.GetString(reader.GetOrdinal("employee_no")),
                    reader.GetString(reader.GetOrdinal("name")),
                    reader.IsDBNull(reader.GetOrdinal("department")) ? "" : reader.GetString(reader.GetOrdinal("department")),
                    reader.IsDBNull(reader.GetOrdinal("position")) ? "" : reader.GetString(reader.GetOrdinal("position")),
                    reader.GetDateTime(reader.GetOrdinal("hire_date")),
                    reader.GetString(reader.GetOrdinal("status"))
                ));
            }
            return list;
        }
    }
}
