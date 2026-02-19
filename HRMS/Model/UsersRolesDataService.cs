using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record UserWithRolesDto(int Id, string Username, bool IsActive, string Roles);

    public class UsersRolesDataService
    {
        public UsersRolesDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<IReadOnlyList<UserWithRolesDto>> GetUsersAsync()
        {
            IReadOnlyList<UserWithRolesDto> list = Array.Empty<UserWithRolesDto>();
            return Task.FromResult(list);
        }

        public Task<int> GetRoleCountAsync()
        {
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<string>> GetRolesAsync()
        {
            IReadOnlyList<string> list = Array.Empty<string>();
            return Task.FromResult(list);
        }

        public Task UpdateUserRoleAsync(int userId, string roleName)
        {
            return Task.CompletedTask;
        }
    }
}
