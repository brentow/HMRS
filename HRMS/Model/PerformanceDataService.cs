using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record PerformanceStatsDto(int TotalCycles, int OpenCycles, int TotalReviews, int SubmittedReviews, int DraftReviews, double AverageRating);

    public record TopPerformerDto(string Employee, double Rating);

    public record PerformanceCycleDetailDto(
        long Id,
        string CycleCode,
        string Name,
        DateTime StartDate,
        DateTime EndDate,
        string Status,
        string CreatedBy);

    public record PerformanceReviewDetailDto(
        long Id,
        string CycleCode,
        string Employee,
        string Reviewer,
        double? Rating,
        string Status,
        string Remarks,
        int ItemsCount);

    public class PerformanceDataService
    {
        private readonly string _connectionString;

        public PerformanceDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<PerformanceStatsDto> GetStatsAsync(int? employeeId = null)
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*)
     FROM performance_cycles pc
     WHERE (@employee_id IS NULL OR EXISTS (
         SELECT 1
         FROM performance_reviews pr
         WHERE pr.performance_cycle_id = pc.performance_cycle_id
           AND pr.employee_id = @employee_id
     ))) AS total_cycles,
    (SELECT COUNT(*)
     FROM performance_cycles pc
     WHERE pc.status = 'OPEN'
       AND (@employee_id IS NULL OR EXISTS (
           SELECT 1
           FROM performance_reviews pr
           WHERE pr.performance_cycle_id = pc.performance_cycle_id
             AND pr.employee_id = @employee_id
       ))) AS open_cycles,
    (SELECT COUNT(*)
     FROM performance_reviews pr
     WHERE (@employee_id IS NULL OR pr.employee_id = @employee_id)) AS total_reviews,
    (SELECT COUNT(*)
     FROM performance_reviews pr
     WHERE pr.status = 'SUBMITTED'
       AND (@employee_id IS NULL OR pr.employee_id = @employee_id)) AS submitted_reviews,
    (SELECT COUNT(*)
     FROM performance_reviews pr
     WHERE pr.status = 'DRAFT'
       AND (@employee_id IS NULL OR pr.employee_id = @employee_id)) AS draft_reviews,
    COALESCE((
        SELECT AVG(pr.overall_rating)
        FROM performance_reviews pr
        WHERE pr.overall_rating IS NOT NULL
          AND (@employee_id IS NULL OR pr.employee_id = @employee_id)
    ), 0) AS avg_rating;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new PerformanceStatsDto(0, 0, 0, 0, 0, 0);
                }

                return new PerformanceStatsDto(
                    TotalCycles: Convert.ToInt32(reader["total_cycles"], CultureInfo.InvariantCulture),
                    OpenCycles: Convert.ToInt32(reader["open_cycles"], CultureInfo.InvariantCulture),
                    TotalReviews: Convert.ToInt32(reader["total_reviews"], CultureInfo.InvariantCulture),
                    SubmittedReviews: Convert.ToInt32(reader["submitted_reviews"], CultureInfo.InvariantCulture),
                    DraftReviews: Convert.ToInt32(reader["draft_reviews"], CultureInfo.InvariantCulture),
                    AverageRating: reader["avg_rating"] == DBNull.Value ? 0 : Convert.ToDouble(reader["avg_rating"], CultureInfo.InvariantCulture));
            }
            catch (MySqlException)
            {
                return new PerformanceStatsDto(0, 0, 0, 0, 0, 0);
            }
        }

        public async Task<IReadOnlyList<TopPerformerDto>> GetTopPerformersAsync(int limit = 5, int? employeeId = null)
        {
            if (limit <= 0)
            {
                return Array.Empty<TopPerformerDto>();
            }

            const string sql = @"
SELECT
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    ROUND(COALESCE(AVG(pr.overall_rating), 0), 2) AS avg_rating
FROM employees e
LEFT JOIN performance_reviews pr ON pr.employee_id = e.employee_id
WHERE e.status = 'ACTIVE'
  AND (@employee_id IS NULL OR e.employee_id = @employee_id)
GROUP BY e.employee_id, employee_name
ORDER BY avg_rating DESC
LIMIT @limit;";

            var list = new List<TopPerformerDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@limit", limit);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new TopPerformerDto(
                        Employee: reader["employee_name"]?.ToString() ?? string.Empty,
                        Rating: reader["avg_rating"] == DBNull.Value ? 0 : Convert.ToDouble(reader["avg_rating"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException)
            {
                return Array.Empty<TopPerformerDto>();
            }

            return list;
        }

        public async Task<IReadOnlyList<PerformanceCycleDetailDto>> GetCyclesAsync(int? employeeId = null)
        {
            const string sql = @"
SELECT
    pc.performance_cycle_id AS cycle_id,
    pc.cycle_code,
    pc.name,
    pc.start_date,
    pc.end_date,
    pc.status,
    COALESCE(CONCAT(cb.last_name, ', ', cb.first_name, IFNULL(CONCAT(' ', cb.middle_name), '')), '-') AS created_by
FROM performance_cycles pc
LEFT JOIN employees cb ON cb.employee_id = pc.created_by_employee_id
WHERE (@employee_id IS NULL OR EXISTS (
        SELECT 1
        FROM performance_reviews pr
        WHERE pr.performance_cycle_id = pc.performance_cycle_id
          AND pr.employee_id = @employee_id
    ))
ORDER BY pc.start_date DESC, pc.performance_cycle_id DESC;";

            var list = new List<PerformanceCycleDetailDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new PerformanceCycleDetailDto(
                        Id: Convert.ToInt64(reader["cycle_id"], CultureInfo.InvariantCulture),
                        CycleCode: reader["cycle_code"]?.ToString() ?? string.Empty,
                        Name: reader["name"]?.ToString() ?? string.Empty,
                        StartDate: reader["start_date"] == DBNull.Value
                            ? DateTime.Today
                            : Convert.ToDateTime(reader["start_date"], CultureInfo.InvariantCulture),
                        EndDate: reader["end_date"] == DBNull.Value
                            ? DateTime.Today
                            : Convert.ToDateTime(reader["end_date"], CultureInfo.InvariantCulture),
                        Status: reader["status"]?.ToString() ?? "DRAFT",
                        CreatedBy: reader["created_by"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException)
            {
                return Array.Empty<PerformanceCycleDetailDto>();
            }

            return list;
        }

        public async Task UpdateCycleAsync(long cycleId, string? name, DateTime startDate, DateTime endDate, string? status)
        {
            const string sql = @"
UPDATE performance_cycles
SET name = @name,
    start_date = @start_date,
    end_date = @end_date,
    status = @status
WHERE performance_cycle_id = @cycle_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@cycle_id", cycleId);
            command.Parameters.AddWithValue("@name", (name ?? string.Empty).Trim());
            command.Parameters.AddWithValue("@start_date", startDate.Date);
            command.Parameters.AddWithValue("@end_date", endDate.Date);
            command.Parameters.AddWithValue("@status", NormalizeCycleStatus(status));
            await command.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<PerformanceReviewDetailDto>> GetReviewsAsync(int? employeeId = null)
        {
            const string sql = @"
SELECT
    pr.performance_review_id AS review_id,
    pc.cycle_code,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    CONCAT(r.last_name, ', ', r.first_name, IFNULL(CONCAT(' ', r.middle_name), '')) AS reviewer_name,
    pr.overall_rating,
    pr.status,
    COALESCE(pr.remarks, '') AS remarks,
    COUNT(pri.performance_review_item_id) AS items_count
FROM performance_reviews pr
INNER JOIN performance_cycles pc ON pc.performance_cycle_id = pr.performance_cycle_id
INNER JOIN employees e ON e.employee_id = pr.employee_id
INNER JOIN employees r ON r.employee_id = pr.reviewer_employee_id
LEFT JOIN performance_review_items pri ON pri.performance_review_id = pr.performance_review_id
WHERE (@employee_id IS NULL OR pr.employee_id = @employee_id)
GROUP BY
    pr.performance_review_id,
    pc.cycle_code,
    employee_name,
    reviewer_name,
    pr.overall_rating,
    pr.status,
    pr.remarks,
    pc.start_date
ORDER BY pc.start_date DESC, pr.performance_review_id DESC;";

            var list = new List<PerformanceReviewDetailDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new PerformanceReviewDetailDto(
                        Id: Convert.ToInt64(reader["review_id"], CultureInfo.InvariantCulture),
                        CycleCode: reader["cycle_code"]?.ToString() ?? string.Empty,
                        Employee: reader["employee_name"]?.ToString() ?? string.Empty,
                        Reviewer: reader["reviewer_name"]?.ToString() ?? string.Empty,
                        Rating: reader["overall_rating"] == DBNull.Value
                            ? null
                            : Convert.ToDouble(reader["overall_rating"], CultureInfo.InvariantCulture),
                        Status: reader["status"]?.ToString() ?? "DRAFT",
                        Remarks: reader["remarks"]?.ToString() ?? string.Empty,
                        ItemsCount: Convert.ToInt32(reader["items_count"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException)
            {
                return Array.Empty<PerformanceReviewDetailDto>();
            }

            return list;
        }

        public async Task UpdateReviewAsync(long reviewId, double? rating, string? status, string? remarks)
        {
            const string sql = @"
UPDATE performance_reviews
SET overall_rating = @overall_rating,
    status = @status,
    remarks = @remarks,
    updated_at = CURRENT_TIMESTAMP
WHERE performance_review_id = @review_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@review_id", reviewId);
            command.Parameters.AddWithValue("@overall_rating", rating.HasValue ? rating.Value : DBNull.Value);
            command.Parameters.AddWithValue("@status", NormalizeReviewStatus(status));
            command.Parameters.AddWithValue("@remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
            await command.ExecuteNonQueryAsync();
        }

        public async Task<int?> GetEmployeeIdByUserIdAsync(int userId)
        {
            if (userId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT employee_id
FROM user_accounts
WHERE user_id = @user_id
LIMIT 1;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string NormalizeCycleStatus(string? status)
        {
            return status?.Trim().ToUpperInvariant() switch
            {
                "OPEN" => "OPEN",
                "CLOSED" => "CLOSED",
                "ARCHIVED" => "ARCHIVED",
                _ => "DRAFT"
            };
        }

        private static string NormalizeReviewStatus(string? status)
        {
            return status?.Trim().ToUpperInvariant() switch
            {
                "SUBMITTED" => "SUBMITTED",
                "APPROVED" => "APPROVED",
                "REJECTED" => "REJECTED",
                _ => "DRAFT"
            };
        }
    }
}
