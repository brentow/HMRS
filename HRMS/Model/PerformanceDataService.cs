using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record PerformanceStatsDto(int TotalCycles, int OpenCycles, int TotalReviews, int SubmittedReviews, int DraftReviews, double AverageRating);

    public record TopPerformerDto(string Employee, double Rating);

    public class PerformanceDataService
    {
        private readonly string _connectionString;

        public PerformanceDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<PerformanceStatsDto> GetStatsAsync()
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM performance_cycles) AS total_cycles,
                    (SELECT COUNT(*) FROM performance_cycles WHERE status = 'Open') AS open_cycles,
                    (SELECT COUNT(*) FROM performance_reviews) AS total_reviews,
                    (SELECT COUNT(*) FROM performance_reviews WHERE status IN ('Submitted','Completed')) AS submitted_reviews,
                    (SELECT COUNT(*) FROM performance_reviews WHERE status = 'Draft') AS draft_reviews,
                    (SELECT IFNULL(AVG(rating),0) FROM performance_reviews WHERE rating IS NOT NULL) AS avg_rating;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int totalCycles = Convert.ToInt32(reader["total_cycles"]);
                int openCycles = Convert.ToInt32(reader["open_cycles"]);
                int totalReviews = Convert.ToInt32(reader["total_reviews"]);
                int submittedReviews = Convert.ToInt32(reader["submitted_reviews"]);
                int draftReviews = Convert.ToInt32(reader["draft_reviews"]);
                double avgRating = Convert.ToDouble(reader["avg_rating"]);
                return new PerformanceStatsDto(totalCycles, openCycles, totalReviews, submittedReviews, draftReviews, avgRating);
            }

            return new PerformanceStatsDto(0, 0, 0, 0, 0, 0);
        }

        public async Task<IReadOnlyList<TopPerformerDto>> GetTopPerformersAsync(int limit = 5)
        {
            const string sql = @"
                SELECT CONCAT(e.first_name, ' ', e.last_name) AS employee,
                       pr.rating
                FROM performance_reviews pr
                INNER JOIN employees e ON pr.employee_id = e.id
                WHERE pr.rating IS NOT NULL
                ORDER BY pr.rating DESC
                LIMIT @lim;";

            var list = new List<TopPerformerDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(reader.GetOrdinal("employee"));
                var rating = reader.GetDouble(reader.GetOrdinal("rating"));
                list.Add(new TopPerformerDto(name, rating));
            }
            return list;
        }
    }
}
