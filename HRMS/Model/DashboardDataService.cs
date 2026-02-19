using System;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public class DashboardDataService
    {
        public DashboardDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<DashboardStats> GetDashboardStatsAsync()
        {
            return Task.FromResult(new DashboardStats());
        }
    }
}
