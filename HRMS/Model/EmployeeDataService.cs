using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record EmployeeStatsDto(int TotalEmployees, int ActiveEmployees, int Departments, int Positions);
    public record EmployeeRowDto(string EmployeeNo, string Name, string Department, string Position, DateTime HireDate, string Status);

    public class EmployeeDataService
    {
        public EmployeeDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<EmployeeStatsDto> GetStatsAsync()
        {
            return Task.FromResult(new EmployeeStatsDto(0, 0, 0, 0));
        }

        public Task<IReadOnlyList<EmployeeRowDto>> GetRecentEmployeesAsync(int limit = 10)
        {
            IReadOnlyList<EmployeeRowDto> list = Array.Empty<EmployeeRowDto>();
            return Task.FromResult(list);
        }
    }
}
