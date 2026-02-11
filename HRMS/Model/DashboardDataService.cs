using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public class DashboardDataService
    {
        private readonly string _connectionString;

        public DashboardDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            const string query = @"
                SELECT
                    (SELECT COUNT(*) FROM employees) AS TotalEmployees,
                    (SELECT COUNT(*) FROM employees WHERE status = 'Active') AS ActiveEmployees,
                    (SELECT COUNT(*) FROM departments) AS Departments,
                    (SELECT COUNT(*) FROM positions) AS Positions,
                    (SELECT COUNT(*) FROM attendance_logs WHERE log_date = CURDATE() AND time_in IS NOT NULL) AS PresentToday,
                    (SELECT COUNT(*) FROM leave_requests WHERE status = 'Pending') AS PendingLeaves,
                    (SELECT COUNT(*) FROM job_posts WHERE status = 'Open') AS OpenJobs,
                    (SELECT COUNT(*) FROM training_courses WHERE is_active = 1) AS ActiveCourses,
                    (SELECT COUNT(*) FROM payroll_periods WHERE status = 'Open') AS OpenPayrollPeriods,
                    (SELECT COUNT(*) FROM performance_cycles WHERE status = 'Open') AS OpenPerformanceCycles,
                    (SELECT COUNT(*) FROM users WHERE is_active = 1) AS ActiveUsers,
                    (SELECT COUNT(*) FROM applicants WHERE status = 'Applied') AS ApplicantsInPipeline;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(query, connection);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                long ReadLong(string name) => reader.IsDBNull(reader.GetOrdinal(name))
                    ? 0
                    : reader.GetInt64(reader.GetOrdinal(name));

                return new DashboardStats
                {
                    TotalEmployees = ReadLong("TotalEmployees"),
                    ActiveEmployees = ReadLong("ActiveEmployees"),
                    Departments = ReadLong("Departments"),
                    Positions = ReadLong("Positions"),
                    PresentToday = ReadLong("PresentToday"),
                    PendingLeaves = ReadLong("PendingLeaves"),
                    OpenJobs = ReadLong("OpenJobs"),
                    ActiveCourses = ReadLong("ActiveCourses"),
                    OpenPayrollPeriods = ReadLong("OpenPayrollPeriods"),
                    OpenPerformanceCycles = ReadLong("OpenPerformanceCycles"),
                    ActiveUsers = ReadLong("ActiveUsers"),
                    ApplicantsInPipeline = ReadLong("ApplicantsInPipeline")
                };
            }

            return new DashboardStats();
        }
    }
}
