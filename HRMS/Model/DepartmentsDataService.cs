using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record DepartmentsStatsDto(int Departments, int Positions, int Employees);
    public record DepartmentRowDto(string Name, int Positions, int Employees);
    public record PositionRowDto(string Name, string Department);

    public class DepartmentsDataService
    {
        private readonly string _connectionString;

        public DepartmentsDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<DepartmentsStatsDto> GetStatsAsync()
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM departments) AS departments,
                    (SELECT COUNT(*) FROM positions) AS positions,
                    (SELECT COUNT(*) FROM employees) AS employees;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new DepartmentsStatsDto(
                    Convert.ToInt32(reader["departments"]),
                    Convert.ToInt32(reader["positions"]),
                    Convert.ToInt32(reader["employees"]));
            }
            return new DepartmentsStatsDto(0, 0, 0);
        }

        public async Task<IReadOnlyList<DepartmentRowDto>> GetDepartmentsAsync()
        {
            const string sql = @"
                SELECT d.name,
                       (SELECT COUNT(*) FROM positions p WHERE p.department_id = d.id) AS positions,
                       (SELECT COUNT(*) FROM employees e WHERE e.department_id = d.id) AS employees
                FROM departments d
                ORDER BY d.name;";

            var list = new List<DepartmentRowDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DepartmentRowDto(
                    reader.GetString(reader.GetOrdinal("name")),
                    reader.GetInt32(reader.GetOrdinal("positions")),
                    reader.GetInt32(reader.GetOrdinal("employees"))
                ));
            }
            return list;
        }

        public async Task<IReadOnlyList<PositionRowDto>> GetPositionsAsync(int limit = 10)
        {
            const string sql = @"
                SELECT p.name,
                       d.name AS department
                FROM positions p
                INNER JOIN departments d ON d.id = p.department_id
                ORDER BY p.name
                LIMIT @lim;";

            var list = new List<PositionRowDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PositionRowDto(
                    reader.GetString(reader.GetOrdinal("name")),
                    reader.GetString(reader.GetOrdinal("department"))
                ));
            }
            return list;
        }
    }
}
