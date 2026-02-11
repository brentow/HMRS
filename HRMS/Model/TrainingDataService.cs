using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record TrainingCourseDto(
        int Id,
        string Title,
        string Provider,
        string Description,
        double Hours,
        string Status);

    public record TrainingEnrollmentDto(
        string Employee,
        string Course,
        DateTime? ScheduleDate,
        string Status);

    public record TrainingEmployeeDto(int Id, string Name);

    public class TrainingDataService
    {
        private readonly string _connectionString;

        public TrainingDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<IReadOnlyList<TrainingCourseDto>> GetCoursesAsync()
        {
            const string sql = @"
                SELECT id,
                       title,
                       IFNULL(provider, '') AS provider,
                       IFNULL(description, '') AS description,
                       IFNULL(hours, 0) AS hours,
                       CASE WHEN is_active = 1 THEN 'Active' ELSE 'Inactive' END AS status
                FROM training_courses
                ORDER BY title;";

            var list = new List<TrainingCourseDto>();

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(reader.GetOrdinal("id"));
                string title = reader.GetString(reader.GetOrdinal("title"));
                string provider = reader.GetString(reader.GetOrdinal("provider"));
                string description = reader.GetString(reader.GetOrdinal("description"));
                double hours = reader.GetDouble(reader.GetOrdinal("hours"));
                string status = reader.GetString(reader.GetOrdinal("status"));

                list.Add(new TrainingCourseDto(id, title, provider, description, hours, status));
            }
            return list;
        }

        public async Task UpdateCourseAsync(TrainingCourseDto course)
        {
            const string sql = @"
                UPDATE training_courses
                   SET title = @title,
                       provider = @provider,
                       description = @description,
                       hours = @hours,
                       is_active = @is_active
                 WHERE id = @id;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", course.Title);
            cmd.Parameters.AddWithValue("@provider", course.Provider);
            cmd.Parameters.AddWithValue("@description", course.Description);
            cmd.Parameters.AddWithValue("@hours", course.Hours);
            cmd.Parameters.AddWithValue("@is_active", string.Equals(course.Status, "Active", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", course.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> AddCourseAsync(TrainingCourseDto course)
        {
            const string sql = @"
                INSERT INTO training_courses (title, provider, description, hours, is_active)
                VALUES (@title, @provider, @description, @hours, @is_active);
                SELECT LAST_INSERT_ID();";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@title", course.Title);
            cmd.Parameters.AddWithValue("@provider", course.Provider);
            cmd.Parameters.AddWithValue("@description", course.Description);
            cmd.Parameters.AddWithValue("@hours", course.Hours);
            cmd.Parameters.AddWithValue("@is_active", string.Equals(course.Status, "Active", StringComparison.OrdinalIgnoreCase) ? 1 : 0);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task DeleteCourseAsync(int courseId)
        {
            const string sql = @"DELETE FROM training_courses WHERE id = @id;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", courseId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<TrainingEmployeeDto>> GetEmployeesAsync()
        {
            const string sql = @"
                SELECT id, CONCAT(first_name, ' ', last_name) AS name
                FROM employees
                ORDER BY name;";

            var list = new List<TrainingEmployeeDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(reader.GetOrdinal("id"));
                var name = reader.GetString(reader.GetOrdinal("name"));
                list.Add(new TrainingEmployeeDto(id, name));
            }
            return list;
        }

        public async Task<int> AddEnrollmentAsync(int employeeId, int courseId, DateTime scheduleDate, string status)
        {
            const string sql = @"
                INSERT INTO training_enrollments (employee_id, course_id, schedule_date, status)
                VALUES (@emp, @course, @date, @status);
                SELECT LAST_INSERT_ID();";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@emp", employeeId);
            cmd.Parameters.AddWithValue("@course", courseId);
            cmd.Parameters.AddWithValue("@date", scheduleDate);
            cmd.Parameters.AddWithValue("@status", status);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<IReadOnlyList<TrainingEnrollmentDto>> GetEnrollmentsAsync()
        {
            const string sql = @"
                SELECT
                    e.first_name AS employee_first,
                    e.last_name AS employee_last,
                    tc.title AS course_title,
                    te.schedule_date,
                    te.status
                FROM training_enrollments te
                INNER JOIN employees e ON te.employee_id = e.id
                INNER JOIN training_courses tc ON te.course_id = tc.id
                ORDER BY te.schedule_date DESC;";

            var list = new List<TrainingEnrollmentDto>();

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var employee = $"{reader.GetString(reader.GetOrdinal("employee_first"))} {reader.GetString(reader.GetOrdinal("employee_last"))}".Trim();
                var course = reader.GetString(reader.GetOrdinal("course_title"));
                var date = reader.IsDBNull(reader.GetOrdinal("schedule_date"))
                    ? (DateTime?)null
                    : reader.GetDateTime(reader.GetOrdinal("schedule_date"));
                var status = reader.IsDBNull(reader.GetOrdinal("status"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("status"));

                list.Add(new TrainingEnrollmentDto(employee, course, date, status));
            }

            return list;
        }
    }
}
