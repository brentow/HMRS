using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record LeaveStatsDto(int TotalRequests, int PendingRequests, int ApprovedRequests, int RejectedRequests, int LeaveTypes);
    public record LeaveRequestDto(string Employee, string LeaveType, DateTime StartDate, DateTime EndDate, string Status, DateTime RequestedAt);

    public class LeaveDataService
    {
        public LeaveDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<LeaveStatsDto> GetStatsAsync()
        {
            return Task.FromResult(new LeaveStatsDto(0, 0, 0, 0, 0));
        }

        public Task<IReadOnlyList<LeaveRequestDto>> GetRecentRequestsAsync(int limit = 10)
        {
            IReadOnlyList<LeaveRequestDto> list = Array.Empty<LeaveRequestDto>();
            return Task.FromResult(list);
        }
    }
}
