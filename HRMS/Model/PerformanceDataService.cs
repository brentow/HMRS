using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record PerformanceStatsDto(int TotalCycles, int OpenCycles, int TotalReviews, int SubmittedReviews, int DraftReviews, double AverageRating);

    public record TopPerformerDto(string Employee, double Rating);

    public class PerformanceDataService
    {
        public PerformanceDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<PerformanceStatsDto> GetStatsAsync()
        {
            return Task.FromResult(new PerformanceStatsDto(0, 0, 0, 0, 0, 0));
        }

        public Task<IReadOnlyList<TopPerformerDto>> GetTopPerformersAsync(int limit = 5)
        {
            IReadOnlyList<TopPerformerDto> list = Array.Empty<TopPerformerDto>();
            return Task.FromResult(list);
        }
    }
}
