using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record PayrollStatsDto(int TotalPeriods, int OpenPeriods, int TotalItems, decimal TotalNetPay);
    public record PayrollPeriodDto(string Name, DateTime From, DateTime To, string Status);
    public record PayrollRunDto(string Employee, string Period, decimal NetPay, DateTime GeneratedAt);

    public class PayrollDataService
    {
        public PayrollDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<PayrollStatsDto> GetStatsAsync()
        {
            return Task.FromResult(new PayrollStatsDto(0, 0, 0, 0m));
        }

        public Task<IReadOnlyList<PayrollPeriodDto>> GetRecentPeriodsAsync(int limit = 6)
        {
            IReadOnlyList<PayrollPeriodDto> list = Array.Empty<PayrollPeriodDto>();
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<PayrollRunDto>> GetRecentPayrollRunsAsync(int limit = 8)
        {
            IReadOnlyList<PayrollRunDto> list = Array.Empty<PayrollRunDto>();
            return Task.FromResult(list);
        }
    }
}
