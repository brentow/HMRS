using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record DepartmentsStatsDto(
        int Departments,
        int Positions,
        int Employees,
        int DepartmentsWithoutPositions,
        int PositionsWithoutEmployees);

    public record DepartmentRowDto(
        string Name,
        int Positions,
        int Employees,
        decimal EmployeesPerPosition,
        string Health);

    public record PositionRowDto(
        string Name,
        string Department,
        int Employees,
        string Status);

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
    (SELECT COUNT(*) FROM employees) AS employees,
    (
        SELECT COUNT(*)
        FROM departments d
        LEFT JOIN positions p ON p.department_id = d.department_id
        WHERE p.position_id IS NULL
    ) AS departments_without_positions,
    (
        SELECT COUNT(*)
        FROM positions p
        LEFT JOIN employees e ON e.position_id = p.position_id
        WHERE e.employee_id IS NULL
    ) AS positions_without_employees;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new DepartmentsStatsDto(0, 0, 0, 0, 0);
                }

                return new DepartmentsStatsDto(
                    Departments: Convert.ToInt32(reader["departments"], CultureInfo.InvariantCulture),
                    Positions: Convert.ToInt32(reader["positions"], CultureInfo.InvariantCulture),
                    Employees: Convert.ToInt32(reader["employees"], CultureInfo.InvariantCulture),
                    DepartmentsWithoutPositions: Convert.ToInt32(reader["departments_without_positions"], CultureInfo.InvariantCulture),
                    PositionsWithoutEmployees: Convert.ToInt32(reader["positions_without_employees"], CultureInfo.InvariantCulture));
            }
            catch (MySqlException)
            {
                return new DepartmentsStatsDto(0, 0, 0, 0, 0);
            }
        }

        public async Task<IReadOnlyList<DepartmentRowDto>> GetDepartmentsAsync()
        {
            const string sql = @"
SELECT
    d.dept_name,
    COUNT(DISTINCT p.position_id) AS positions,
    COUNT(DISTINCT e.employee_id) AS employees
FROM departments d
LEFT JOIN positions p ON p.department_id = d.department_id
LEFT JOIN employees e ON e.department_id = d.department_id
GROUP BY d.department_id, d.dept_name
ORDER BY d.dept_name;";

            var list = new List<DepartmentRowDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var positions = Convert.ToInt32(reader["positions"], CultureInfo.InvariantCulture);
                    var employees = Convert.ToInt32(reader["employees"], CultureInfo.InvariantCulture);
                    var ratio = positions == 0 ? 0 : Math.Round((decimal)employees / positions, 2);
                    var health = positions == 0
                        ? "No Positions"
                        : employees == 0
                            ? "No Staff"
                            : "Healthy";

                    list.Add(new DepartmentRowDto(
                        Name: reader["dept_name"]?.ToString() ?? string.Empty,
                        Positions: positions,
                        Employees: employees,
                        EmployeesPerPosition: ratio,
                        Health: health));
                }
            }
            catch (MySqlException)
            {
                return Array.Empty<DepartmentRowDto>();
            }

            return list;
        }

        public async Task<IReadOnlyList<PositionRowDto>> GetPositionsAsync(int limit = 500)
        {
            if (limit <= 0)
            {
                return Array.Empty<PositionRowDto>();
            }

            const string sql = @"
SELECT
    p.position_name,
    COALESCE(d.dept_name, '-') AS dept_name,
    COUNT(e.employee_id) AS employees
FROM positions p
LEFT JOIN departments d ON d.department_id = p.department_id
LEFT JOIN employees e ON e.position_id = p.position_id
GROUP BY p.position_id, p.position_name, d.dept_name
ORDER BY dept_name, p.position_name
LIMIT @limit;";

            var list = new List<PositionRowDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@limit", limit);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var employees = Convert.ToInt32(reader["employees"], CultureInfo.InvariantCulture);
                    list.Add(new PositionRowDto(
                        Name: reader["position_name"]?.ToString() ?? string.Empty,
                        Department: reader["dept_name"]?.ToString() ?? "-",
                        Employees: employees,
                        Status: employees > 0 ? "Filled" : "Vacant"));
                }
            }
            catch (MySqlException)
            {
                return Array.Empty<PositionRowDto>();
            }

            return list;
        }

        public async Task AddDepartmentAsync(string departmentName)
        {
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                throw new InvalidOperationException("Department name is required.");
            }

            const string sql = @"
INSERT INTO departments (dept_name)
VALUES (@dept_name);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@dept_name", departmentName.Trim());

            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
            {
                throw new InvalidOperationException("Department already exists.");
            }
        }

        public async Task DeleteDepartmentAsync(string departmentName)
        {
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                throw new InvalidOperationException("Select a department to delete.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var departmentId = await ResolveDepartmentIdAsync(connection, departmentName.Trim());
            if (!departmentId.HasValue)
            {
                throw new InvalidOperationException("Department not found.");
            }

            var positionCount = await CountByDepartmentAsync(connection, "SELECT COUNT(*) FROM positions WHERE department_id = @department_id;", departmentId.Value);
            if (positionCount > 0)
            {
                throw new InvalidOperationException("Delete department positions first before deleting the department.");
            }

            var employeeCount = await CountByDepartmentAsync(connection, "SELECT COUNT(*) FROM employees WHERE department_id = @department_id;", departmentId.Value);
            if (employeeCount > 0)
            {
                throw new InvalidOperationException("Reassign employees first before deleting this department.");
            }

            await using var delete = new MySqlCommand("DELETE FROM departments WHERE department_id = @department_id;", connection);
            delete.Parameters.AddWithValue("@department_id", departmentId.Value);
            await delete.ExecuteNonQueryAsync();
        }

        public async Task AddPositionAsync(string departmentName, string positionName)
        {
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                throw new InvalidOperationException("Select a department first.");
            }

            if (string.IsNullOrWhiteSpace(positionName))
            {
                throw new InvalidOperationException("Position name is required.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var departmentId = await ResolveDepartmentIdAsync(connection, departmentName.Trim());
            if (!departmentId.HasValue)
            {
                throw new InvalidOperationException("Selected department no longer exists.");
            }

            const string sql = @"
INSERT INTO positions (department_id, position_name)
VALUES (@department_id, @position_name);";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@department_id", departmentId.Value);
            command.Parameters.AddWithValue("@position_name", positionName.Trim());

            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
            {
                throw new InvalidOperationException("Position already exists in this department.");
            }
        }

        public async Task DeletePositionAsync(string departmentName, string positionName)
        {
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                throw new InvalidOperationException("Select a department first.");
            }

            if (string.IsNullOrWhiteSpace(positionName))
            {
                throw new InvalidOperationException("Select a position to delete.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            const string positionSql = @"
SELECT p.position_id
FROM positions p
INNER JOIN departments d ON d.department_id = p.department_id
WHERE d.dept_name = @dept_name
  AND p.position_name = @position_name
LIMIT 1;";

            long? positionId = null;
            await using (var positionCmd = new MySqlCommand(positionSql, connection))
            {
                positionCmd.Parameters.AddWithValue("@dept_name", departmentName.Trim());
                positionCmd.Parameters.AddWithValue("@position_name", positionName.Trim());
                var scalar = await positionCmd.ExecuteScalarAsync();
                if (scalar != null && scalar != DBNull.Value)
                {
                    positionId = Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
                }
            }

            if (!positionId.HasValue)
            {
                throw new InvalidOperationException("Position not found for selected department.");
            }

            await using (var employeeCountCmd = new MySqlCommand("SELECT COUNT(*) FROM employees WHERE position_id = @position_id;", connection))
            {
                employeeCountCmd.Parameters.AddWithValue("@position_id", positionId.Value);
                var employeeCount = Convert.ToInt32(await employeeCountCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                if (employeeCount > 0)
                {
                    throw new InvalidOperationException("Reassign employees first before deleting this position.");
                }
            }

            await using var deleteCmd = new MySqlCommand("DELETE FROM positions WHERE position_id = @position_id;", connection);
            deleteCmd.Parameters.AddWithValue("@position_id", positionId.Value);
            await deleteCmd.ExecuteNonQueryAsync();
        }

        private static async Task<int> CountByDepartmentAsync(MySqlConnection connection, string sql, int departmentId)
        {
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@department_id", departmentId);
            var scalar = await command.ExecuteScalarAsync();
            return scalar == null || scalar == DBNull.Value ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }

        private static async Task<int?> ResolveDepartmentIdAsync(MySqlConnection connection, string departmentName)
        {
            const string sql = @"
SELECT department_id
FROM departments
WHERE dept_name = @dept_name
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@dept_name", departmentName);
            var scalar = await command.ExecuteScalarAsync();

            if (scalar == null || scalar == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }
    }
}
