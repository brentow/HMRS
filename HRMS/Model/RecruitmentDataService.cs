using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record RecruitmentStatsDto(int TotalPosts, int OpenPosts, int Applicants, int Interviews);
    public record JobPostDto(string Title, string Department, string Status, DateTime PostedAt);
    public record ApplicantDto(string Name, string JobTitle, string Status, DateTime AppliedAt);

    public class RecruitmentDataService
    {
        private readonly string _connectionString;

        public RecruitmentDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<RecruitmentStatsDto> GetStatsAsync()
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM job_posts) AS total_posts,
                    (SELECT COUNT(*) FROM job_posts WHERE status = 'Open') AS open_posts,
                    (SELECT COUNT(*) FROM applicants) AS applicants,
                    (SELECT COUNT(*) FROM interviews) AS interviews;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RecruitmentStatsDto(
                    Convert.ToInt32(reader["total_posts"]),
                    Convert.ToInt32(reader["open_posts"]),
                    Convert.ToInt32(reader["applicants"]),
                    Convert.ToInt32(reader["interviews"]));
            }
            return new RecruitmentStatsDto(0, 0, 0, 0);
        }

        public async Task<IReadOnlyList<JobPostDto>> GetRecentJobPostsAsync(int limit = 6)
        {
            const string sql = @"
                SELECT jp.title,
                       IFNULL(d.name,'') AS department,
                       jp.status,
                       jp.posted_at
                FROM job_posts jp
                LEFT JOIN departments d ON d.id = jp.department_id
                ORDER BY jp.posted_at DESC
                LIMIT @lim;";

            var list = new List<JobPostDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new JobPostDto(
                    reader.GetString(reader.GetOrdinal("title")),
                    reader.GetString(reader.GetOrdinal("department")),
                    reader.GetString(reader.GetOrdinal("status")),
                    reader.GetDateTime(reader.GetOrdinal("posted_at"))
                ));
            }
            return list;
        }

        public async Task<IReadOnlyList<ApplicantDto>> GetRecentApplicantsAsync(int limit = 8)
        {
            const string sql = @"
                SELECT CONCAT(a.first_name,' ',a.last_name) AS name,
                       jp.title AS job_title,
                       a.status,
                       a.applied_at
                FROM applicants a
                INNER JOIN job_posts jp ON jp.id = a.job_post_id
                ORDER BY a.applied_at DESC
                LIMIT @lim;";

            var list = new List<ApplicantDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ApplicantDto(
                    reader.GetString(reader.GetOrdinal("name")),
                    reader.GetString(reader.GetOrdinal("job_title")),
                    reader.GetString(reader.GetOrdinal("status")),
                    reader.GetDateTime(reader.GetOrdinal("applied_at"))
                ));
            }
            return list;
        }
    }
}
