using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record BeneficiaryImportResult(int Imported, int Skipped, int Invalid);

    public sealed class BeneficiaryImportService
    {
        private readonly string _hrmsConnectionString;
        private readonly string _sulopConnectionString;

        public BeneficiaryImportService(string hrmsConnectionString, string sulopConnectionString)
        {
            _hrmsConnectionString = string.IsNullOrWhiteSpace(hrmsConnectionString)
                ? throw new ArgumentException("HRMS connection string is required.", nameof(hrmsConnectionString))
                : hrmsConnectionString;

            _sulopConnectionString = string.IsNullOrWhiteSpace(sulopConnectionString)
                ? throw new ArgumentException("Sulop connection string is required.", nameof(sulopConnectionString))
                : sulopConnectionString;
        }

        public async Task<BeneficiaryImportResult> ImportFromSulopAsync()
        {
            const string selectSql = @"
SELECT
    civil_registryid,
    firstname,
    lastname,
    middlename,
    address
FROM beneficiaries_validation;";

            const string existsSql = @"
SELECT 1
FROM BeneficiaryStaging
WHERE CivilRegistryID = @civil_registryid
LIMIT 1;";

            const string insertSql = @"
INSERT INTO BeneficiaryStaging
(
    CivilRegistryID,
    FirstName,
    LastName,
    MiddleName,
    Address,
    VerificationStatus,
    ImportedAt
)
VALUES
(
    @civil_registryid,
    @first_name,
    @last_name,
    @middle_name,
    @address,
    @verification_status,
    @imported_at
);";

            var imported = 0;
            var skipped = 0;
            var invalid = 0;

            var sulopRows = new List<(string CivilRegistryId, string FirstName, string LastName, string? MiddleName, string? Address)>();

            try
            {
                // Phase 1: Read all source rows from Sulop first to avoid long-lived cross-database streaming operations.
                await using (var sulopConnection = new MySqlConnection(_sulopConnectionString))
                {
                    await sulopConnection.OpenAsync();
                    await using var select = new MySqlCommand(selectSql, sulopConnection)
                    {
                        CommandTimeout = 180
                    };

                    await using var reader = await select.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        sulopRows.Add((
                            CivilRegistryId: reader["civil_registryid"]?.ToString()?.Trim() ?? string.Empty,
                            FirstName: reader["firstname"]?.ToString()?.Trim() ?? string.Empty,
                            LastName: reader["lastname"]?.ToString()?.Trim() ?? string.Empty,
                            MiddleName: reader["middlename"]?.ToString()?.Trim(),
                            Address: reader["address"]?.ToString()?.Trim()));
                    }
                }

                // Phase 2: Process and insert into HRMS in one transaction.
                await using var hrmsConnection = new MySqlConnection(_hrmsConnectionString);
                await hrmsConnection.OpenAsync();
                await using var transaction = await hrmsConnection.BeginTransactionAsync();

                await using var exists = new MySqlCommand(existsSql, hrmsConnection, transaction);
                exists.Parameters.Add("@civil_registryid", MySqlDbType.VarChar);

                await using var insert = new MySqlCommand(insertSql, hrmsConnection, transaction);
                insert.Parameters.Add("@civil_registryid", MySqlDbType.VarChar);
                insert.Parameters.Add("@first_name", MySqlDbType.VarChar);
                insert.Parameters.Add("@last_name", MySqlDbType.VarChar);
                insert.Parameters.Add("@middle_name", MySqlDbType.VarChar);
                insert.Parameters.Add("@address", MySqlDbType.VarChar);
                insert.Parameters.Add("@verification_status", MySqlDbType.Int32);
                insert.Parameters.Add("@imported_at", MySqlDbType.DateTime);

                foreach (var row in sulopRows)
                {
                    if (string.IsNullOrWhiteSpace(row.CivilRegistryId) ||
                        string.IsNullOrWhiteSpace(row.FirstName) ||
                        string.IsNullOrWhiteSpace(row.LastName))
                    {
                        invalid++;
                        continue;
                    }

                    exists.Parameters["@civil_registryid"].Value = row.CivilRegistryId;
                    var found = await exists.ExecuteScalarAsync();
                    if (found != null && found != DBNull.Value)
                    {
                        skipped++;
                        continue;
                    }

                    insert.Parameters["@civil_registryid"].Value = row.CivilRegistryId;
                    insert.Parameters["@first_name"].Value = row.FirstName;
                    insert.Parameters["@last_name"].Value = row.LastName;
                    insert.Parameters["@middle_name"].Value = string.IsNullOrWhiteSpace(row.MiddleName) ? DBNull.Value : row.MiddleName;
                    insert.Parameters["@address"].Value = string.IsNullOrWhiteSpace(row.Address) ? DBNull.Value : row.Address;
                    insert.Parameters["@verification_status"].Value = (int)BeneficiaryVerificationStatus.Pending;
                    insert.Parameters["@imported_at"].Value = DateTime.Now;

                    await insert.ExecuteNonQueryAsync();
                    imported++;
                }

                await transaction.CommitAsync();
                return new BeneficiaryImportResult(imported, skipped, invalid);
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException(
                    $"Beneficiary import failed (MySQL #{ex.Number}): {ex.Message}",
                    ex);
            }
        }
    }
}
