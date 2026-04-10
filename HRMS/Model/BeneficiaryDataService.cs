using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    /// <summary>
    /// Data access service for Beneficiary Staging workflow.
    /// Manages queries and updates for beneficiary verification and account creation.
    /// </summary>
    public class BeneficiaryDataService
    {
        private readonly string _connectionString;

        public BeneficiaryDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Retrieves staging beneficiaries filtered by verification status and optional search text.
        /// Returns paged results to keep UI responsive for large datasets.
        /// </summary>
        public async Task<(IReadOnlyList<BeneficiaryStagingDto> Items, int TotalCount)> GetStagingBeneficiariesAsync(
            BeneficiaryVerificationStatus? verificationStatus = null,
            string? searchText = null,
            int page = 1,
            int pageSize = 50)
        {
            const string countSql = @"
SELECT COUNT(*)
FROM BeneficiaryStaging
WHERE (@status IS NULL OR VerificationStatus = @status)
  AND (
      @search = ''
      OR COALESCE(BeneficiaryId, '') LIKE CONCAT('%', @search, '%')
      OR COALESCE(FullName, '') LIKE CONCAT('%', @search, '%')
      OR COALESCE(FirstName, '') LIKE CONCAT('%', @search, '%')
      OR COALESCE(LastName, '') LIKE CONCAT('%', @search, '%')
  );";

            const string sql = @"
SELECT
    StagingID,
    COALESCE(BeneficiaryId, '') AS BeneficiaryID,
    COALESCE(NULLIF(BeneficiaryId, ''), CivilRegistryID) AS CivilRegistryID,
    FirstName,
    LastName,
    COALESCE(MiddleName, '') AS MiddleName,
    COALESCE(FullName, '') AS FullName,
    COALESCE(Address, '') AS Address,
    COALESCE(Sex, '') AS Sex,
    COALESCE(Age, '') AS Age,
    COALESCE(IsPwd, 0) AS IsPwd,
    COALESCE(IsSenior, 0) AS IsSenior,
    VerificationStatus,
    ImportedAt,
    COALESCE(Remarks, '') AS Remarks,
    ApprovedRejectedAt,
    MasterID
FROM BeneficiaryStaging
WHERE (@status IS NULL OR VerificationStatus = @status)
  AND (
      @search = ''
      OR COALESCE(BeneficiaryId, '') LIKE CONCAT('%', @search, '%')
      OR COALESCE(FullName, '') LIKE CONCAT('%', @search, '%')
      OR COALESCE(FirstName, '') LIKE CONCAT('%', @search, '%')
      OR COALESCE(LastName, '') LIKE CONCAT('%', @search, '%')
  )
ORDER BY
    CASE VerificationStatus
        WHEN 0 THEN 1  -- Pending first
        WHEN 1 THEN 2  -- Approved second
        WHEN 2 THEN 3  -- Rejected last
    END,
    ImportedAt DESC
LIMIT @limit OFFSET @offset;";

            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 50 : pageSize;
            var offset = (page - 1) * pageSize;
            var normalizedSearch = (searchText ?? string.Empty).Trim();

            var results = new List<BeneficiaryStagingDto>();
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await BeneficiaryStagingSchemaCompatibility.EnsureAsync(connection);

            int totalCount;
            await using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = countSql;
                countCommand.Parameters.AddWithValue("@status", verificationStatus.HasValue ? (object)(int)verificationStatus.Value : null);
                countCommand.Parameters.AddWithValue("@search", normalizedSearch);
                var countObj = await countCommand.ExecuteScalarAsync();
                totalCount = Convert.ToInt32(countObj ?? 0, CultureInfo.InvariantCulture);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@status", verificationStatus.HasValue ? (object)(int)verificationStatus.Value : null);
            command.Parameters.AddWithValue("@search", normalizedSearch);
            command.Parameters.AddWithValue("@limit", pageSize);
            command.Parameters.AddWithValue("@offset", offset);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var statusInt = reader.GetInt32(12);
                var status = (BeneficiaryVerificationStatus)statusInt;

                results.Add(new BeneficiaryStagingDto(
                    StagingID: reader.GetInt32(0),
                    BeneficiaryID: reader.GetString(1),
                    CivilRegistryID: reader.GetString(2),
                    FirstName: reader.GetString(3),
                    LastName: reader.GetString(4),
                    MiddleName: string.IsNullOrWhiteSpace(reader.GetString(5)) ? null : reader.GetString(5),
                    FullName: string.IsNullOrWhiteSpace(reader.GetString(6)) ? null : reader.GetString(6),
                    Address: string.IsNullOrWhiteSpace(reader.GetString(7)) ? null : reader.GetString(7),
                    Sex: string.IsNullOrWhiteSpace(reader.GetString(8)) ? null : reader.GetString(8),
                    Age: string.IsNullOrWhiteSpace(reader.GetString(9)) ? null : reader.GetString(9),
                    IsPwd: reader["IsPwd"] != DBNull.Value && Convert.ToBoolean(reader["IsPwd"], CultureInfo.InvariantCulture),
                    IsSenior: reader["IsSenior"] != DBNull.Value && Convert.ToBoolean(reader["IsSenior"], CultureInfo.InvariantCulture),
                    VerificationStatus: status,
                    ImportedAt: reader.GetDateTime(13),
                    Remarks: string.IsNullOrWhiteSpace(reader.GetString(14)) ? null : reader.GetString(14),
                    ApprovedRejectedAt: reader.IsDBNull(15) ? null : reader.GetDateTime(15),
                    MasterID: reader.IsDBNull(16) ? null : reader.GetInt32(16)
                ));
            }

            return (new ReadOnlyCollection<BeneficiaryStagingDto>(results), totalCount);
        }

        /// <summary>
        /// Updates the verification status and remarks for a beneficiary in staging.
        /// </summary>
        /// <param name="stagingId">The StagingID of the beneficiary record.</param>
        /// <param name="newStatus">Approved (1) or Rejected (2). Pending (0) is initial only.</param>
        /// <param name="remarks">Optional remarks for approval/rejection decision.</param>
        public async Task UpdateVerificationStatusAsync(
            int stagingId,
            BeneficiaryVerificationStatus newStatus,
            string? remarks = null)
        {
            if (stagingId <= 0)
            {
                throw new ArgumentException("Invalid staging ID.", nameof(stagingId));
            }

            if (newStatus != BeneficiaryVerificationStatus.Approved && newStatus != BeneficiaryVerificationStatus.Rejected)
            {
                throw new ArgumentException("Status must be Approved or Rejected.", nameof(newStatus));
            }

            const string sql = @"
UPDATE BeneficiaryStaging
SET
    VerificationStatus = @status,
    Remarks = @remarks,
    ApprovedRejectedAt = NOW()
WHERE StagingID = @staging_id;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await BeneficiaryStagingSchemaCompatibility.EnsureAsync(connection);

                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@status", (int)newStatus);
                command.Parameters.AddWithValue("@remarks", remarks ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@staging_id", stagingId);

                await command.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex) when (ex.Number == 1146)
            {
                throw new InvalidOperationException(
                    "BeneficiaryStaging table not found. Ensure you have run the Seed Database migration.",
                    ex);
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to update beneficiary verification status: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Counts beneficiaries in each verification status (Pending, Approved, Rejected).
        /// </summary>
        public async Task<(int Pending, int Approved, int Rejected)> GetStatusCountsAsync()
        {
            const string sql = @"
SELECT
    COUNT(CASE WHEN VerificationStatus = 0 THEN 1 END) AS pending_count,
    COUNT(CASE WHEN VerificationStatus = 1 THEN 1 END) AS approved_count,
    COUNT(CASE WHEN VerificationStatus = 2 THEN 1 END) AS rejected_count
FROM BeneficiaryStaging;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await BeneficiaryStagingSchemaCompatibility.EnsureAsync(connection);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            }

            return (0, 0, 0);
        }

        /// <summary>
        /// Creates a new user account for an approved beneficiary.
        /// Generates username, temporary password, and links account to beneficiary record.
        /// </summary>
        /// <param name="stagingId">The StagingID of the approved beneficiary.</param>
        /// <param name="beneficiary">The beneficiary staging DTO with name information.</param>
        /// <returns>Tuple of (userId, username, temporaryPassword)</returns>
        public async Task<(int UserId, string Username, string TemporaryPassword)> CreateBeneficiaryAccountAsync(
            int stagingId,
            BeneficiaryStagingDto beneficiary)
        {
            if (stagingId <= 0)
            {
                throw new ArgumentException("Invalid staging ID.", nameof(stagingId));
            }

            if (beneficiary == null)
            {
                throw new ArgumentNullException(nameof(beneficiary));
            }

            // Generate username: FirstName.LastName (lowercase, no spaces)
            var username = $"{beneficiary.FirstName}.{beneficiary.LastName}"
                .ToLowerInvariant()
                .Replace(" ", "");

            // Ensure uniqueness by appending timestamp if needed
            var baseUsername = username;
            var counter = 1;

            // Required by integration spec for newly approved beneficiary accounts.
            var temporaryPassword = "Constituent@2026";
            var passwordHash = PasswordSecurity.HashPassword(temporaryPassword);

            // Full name for account
            var fullName = $"{beneficiary.FirstName} {beneficiary.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(beneficiary.MiddleName))
            {
                fullName = $"{beneficiary.FirstName} {beneficiary.MiddleName} {beneficiary.LastName}";
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await BeneficiaryStagingSchemaCompatibility.EnsureAsync(connection);

            // Check if username exists and generate unique one if needed
            while (await UsernameExistsAsync(connection, username))
            {
                username = $"{baseUsername}{counter}";
                counter++;
            }

            // Get or create "Beneficiary" role
            var beneficiaryRoleId = await GetOrCreateBeneficiaryRoleAsync(connection);

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // Insert new user account
                const string insertUserSql = @"
INSERT INTO user_accounts (
    role_id,
    employee_id,
    username,
    password_hash,
    must_change_password,
    password_changed_at,
    full_name,
    email,
    status
)
VALUES (
    @role_id,
    NULL,
    @username,
    @password_hash,
    1,
    NULL,
    @full_name,
    @email,
    'ACTIVE'
);";

                await using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = insertUserSql;
                insertCommand.Transaction = transaction;
                insertCommand.Parameters.AddWithValue("@role_id", beneficiaryRoleId);
                insertCommand.Parameters.AddWithValue("@username", username);
                insertCommand.Parameters.AddWithValue("@password_hash", passwordHash);
                insertCommand.Parameters.AddWithValue("@full_name", fullName);
                var identity = string.IsNullOrWhiteSpace(beneficiary.CivilRegistryID) ? $"beneficiary{stagingId}" : beneficiary.CivilRegistryID;
                insertCommand.Parameters.AddWithValue("@email", $"{identity}@beneficiary.local"); // Placeholder email

                var inserted = await insertCommand.ExecuteNonQueryAsync();
                if (inserted <= 0 || insertCommand.LastInsertedId <= 0)
                {
                    throw new InvalidOperationException("Failed to create user account.");
                }
                var userId = checked((int)insertCommand.LastInsertedId);

                // Link user account to beneficiary staging record
                const string linkStagingSql = @"
UPDATE BeneficiaryStaging
SET MasterID = @master_id
WHERE StagingID = @staging_id;";

                await using var linkCommand = connection.CreateCommand();
                linkCommand.CommandText = linkStagingSql;
                linkCommand.Transaction = transaction;
                linkCommand.Parameters.AddWithValue("@master_id", userId);
                linkCommand.Parameters.AddWithValue("@staging_id", stagingId);

                await linkCommand.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                return (userId, username, temporaryPassword);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<bool> UsernameExistsAsync(MySqlConnection connection, string username)
        {
            const string sql = "SELECT COUNT(*) FROM user_accounts WHERE username = @username COLLATE utf8mb4_general_ci;";

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@username", username);

            var result = await command.ExecuteScalarAsync();
            return result != null && result != DBNull.Value && (long)result > 0;
        }

        private async Task<int> GetOrCreateBeneficiaryRoleAsync(MySqlConnection connection)
        {
            // Check if "Beneficiary" role exists
            const string selectSql = "SELECT role_id FROM roles WHERE role_name = 'Beneficiary' LIMIT 1;";

            await using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = selectSql;

            var result = await selectCommand.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToInt32(result, CultureInfo.InvariantCulture);
            }

            // Create "Beneficiary" role if it doesn't exist
            const string insertSql = @"
INSERT INTO roles (role_name, description)
VALUES ('Beneficiary', 'Role for CRS-imported beneficiaries');";

            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = insertSql;

            var inserted = await insertCommand.ExecuteNonQueryAsync();
            if (inserted <= 0 || insertCommand.LastInsertedId <= 0)
            {
                throw new InvalidOperationException("Failed to create beneficiary role.");
            }
            return checked((int)insertCommand.LastInsertedId);
        }
    }
}
