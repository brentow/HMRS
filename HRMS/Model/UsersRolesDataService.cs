using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace HRMS.Model
{
    public record UserWithRolesDto(int Id, string Username, bool IsActive, string Roles);

    public class UsersRolesDataService
    {
        private readonly string _connectionString;

        public UsersRolesDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<IReadOnlyList<UserWithRolesDto>> GetUsersAsync()
        {
            const string sql = @"
                SELECT u.id,
                       u.username,
                       u.is_active,
                       IFNULL(GROUP_CONCAT(r.name SEPARATOR ', '), '') AS roles
                FROM users u
                LEFT JOIN user_roles ur ON ur.user_id = u.id
                LEFT JOIN roles r ON r.id = ur.role_id
                GROUP BY u.id, u.username, u.is_active
                ORDER BY u.username;";

            var list = new List<UserWithRolesDto>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(reader.GetOrdinal("id"));
                var username = reader.GetString(reader.GetOrdinal("username"));
                var isActive = reader.GetBoolean(reader.GetOrdinal("is_active"));
                var roles = reader.GetString(reader.GetOrdinal("roles"));
                list.Add(new UserWithRolesDto(id, username, isActive, roles));
            }
            return list;
        }

        public async Task<int> GetRoleCountAsync()
        {
            const string sql = "SELECT COUNT(*) FROM roles;";
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<IReadOnlyList<string>> GetRolesAsync()
        {
            const string sql = "SELECT name FROM roles ORDER BY name;";
            var list = new List<string>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(reader.GetString(reader.GetOrdinal("name")));
            }
            return list;
        }

        public async Task UpdateUserRoleAsync(int userId, string roleName)
        {
            const string sql = @"
                DELETE FROM user_roles WHERE user_id = @uid;
                INSERT INTO user_roles (user_id, role_id)
                SELECT @uid, r.id FROM roles r WHERE r.name = @role LIMIT 1;";

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@role", roleName);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
