using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed class CompanyProfileDataService
    {
        private readonly string _connectionString;

        public CompanyProfileDataService(string connectionString)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? throw new ArgumentException("Connection string is required.", nameof(connectionString))
                : connectionString;
        }

        public async Task<CompanyProfile> GetCompanyProfileAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await EnsureTableAsync(connection);

                const string sql = @"
SELECT company_name, address, owner_name, serial_number, logo_path
FROM company_profile
ORDER BY updated_at DESC, profile_id DESC
LIMIT 1;";

                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    await reader.DisposeAsync();
                    await SaveCompanyProfileAsync(CompanyProfile.Default);
                    return CompanyProfile.Default;
                }

                return new CompanyProfile
                {
                    CompanyName = ReadOrDefault(reader, "company_name", CompanyProfile.Default.CompanyName),
                    Address = ReadOrDefault(reader, "address", CompanyProfile.Default.Address),
                    OwnerName = ReadOrDefault(reader, "owner_name", CompanyProfile.Default.OwnerName),
                    SerialNumber = ReadOrDefault(reader, "serial_number", CompanyProfile.Default.SerialNumber),
                    LogoPath = ReadOrDefault(reader, "logo_path", CompanyProfile.Default.LogoPath)
                };
            }
            catch
            {
                return CompanyProfile.Default;
            }
        }

        public async Task SaveCompanyProfileAsync(CompanyProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureTableAsync(connection);

            var normalized = new CompanyProfile
            {
                CompanyName = string.IsNullOrWhiteSpace(profile.CompanyName) ? CompanyProfile.Default.CompanyName : profile.CompanyName.Trim(),
                Address = string.IsNullOrWhiteSpace(profile.Address) ? CompanyProfile.Default.Address : profile.Address.Trim(),
                OwnerName = string.IsNullOrWhiteSpace(profile.OwnerName) ? CompanyProfile.Default.OwnerName : profile.OwnerName.Trim(),
                SerialNumber = string.IsNullOrWhiteSpace(profile.SerialNumber) ? CompanyProfile.Default.SerialNumber : profile.SerialNumber.Trim(),
                LogoPath = string.IsNullOrWhiteSpace(profile.LogoPath) ? CompanyProfile.Default.LogoPath : profile.LogoPath.Trim()
            };

            const string latestIdSql = @"
SELECT profile_id
FROM company_profile
ORDER BY updated_at DESC, profile_id DESC
LIMIT 1;";

            var latestId = 0;
            await using (var idCommand = new MySqlCommand(latestIdSql, connection))
            {
                var value = await idCommand.ExecuteScalarAsync();
                latestId = value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }

            await using var command = new MySqlCommand(string.Empty, connection);
            command.Parameters.AddWithValue("@company_name", normalized.CompanyName);
            command.Parameters.AddWithValue("@address", normalized.Address);
            command.Parameters.AddWithValue("@owner_name", normalized.OwnerName);
            command.Parameters.AddWithValue("@serial_number", normalized.SerialNumber);
            command.Parameters.AddWithValue("@logo_path", normalized.LogoPath);

            if (latestId <= 0)
            {
                command.CommandText = @"
INSERT INTO company_profile (company_name, address, owner_name, serial_number, logo_path)
VALUES (@company_name, @address, @owner_name, @serial_number, @logo_path);";
            }
            else
            {
                command.CommandText = @"
UPDATE company_profile
SET
    company_name = @company_name,
    address = @address,
    owner_name = @owner_name,
    serial_number = @serial_number,
    logo_path = @logo_path
WHERE profile_id = @profile_id;";
                command.Parameters.AddWithValue("@profile_id", latestId);
            }

            await command.ExecuteNonQueryAsync();
        }

        private static async Task EnsureTableAsync(MySqlConnection connection)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS company_profile (
    profile_id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    company_name VARCHAR(200) NOT NULL,
    address VARCHAR(500) NULL,
    owner_name VARCHAR(200) NULL,
    serial_number VARCHAR(100) NULL,
    logo_path VARCHAR(500) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB;";

            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static string ReadOrDefault(MySqlDataReader reader, string columnName, string fallback)
        {
            var raw = reader[columnName]?.ToString();
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
        }
    }
}
