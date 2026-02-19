using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record DepartmentsStatsDto(int Departments, int Positions, int Employees);
    public record DepartmentRowDto(string Name, int Positions, int Employees);
    public record PositionRowDto(string Name, string Department);

    public class DepartmentsDataService
    {
        public DepartmentsDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<DepartmentsStatsDto> GetStatsAsync()
        {
            return Task.FromResult(new DepartmentsStatsDto(0, 0, 0));
        }

        public Task<IReadOnlyList<DepartmentRowDto>> GetDepartmentsAsync()
        {
            IReadOnlyList<DepartmentRowDto> list = Array.Empty<DepartmentRowDto>();
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<PositionRowDto>> GetPositionsAsync(int limit = 10)
        {
            IReadOnlyList<PositionRowDto> list = Array.Empty<PositionRowDto>();
            return Task.FromResult(list);
        }
    }
}
