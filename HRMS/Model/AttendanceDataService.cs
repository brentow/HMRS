using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record AttendanceStatsDto(int TotalLogs, int TodayLogs, int PresentToday, int IncompleteLogs);
    public record AttendanceLogDto(string Employee, DateTime LogDate, TimeSpan? TimeIn, TimeSpan? TimeOut, string Source);

    public class AttendanceDataService
    {
        public AttendanceDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<AttendanceStatsDto> GetStatsAsync()
        {
            return Task.FromResult(new AttendanceStatsDto(0, 0, 0, 0));
        }

        public Task<IReadOnlyList<AttendanceLogDto>> GetRecentLogsAsync(int limit = 12)
        {
            IReadOnlyList<AttendanceLogDto> list = Array.Empty<AttendanceLogDto>();
            return Task.FromResult(list);
        }
    }
}
