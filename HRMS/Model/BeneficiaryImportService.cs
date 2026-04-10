using MySqlConnector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record BeneficiaryImportResult(int Imported, int Skipped, int Invalid);

    public sealed class BeneficiaryImportService
    {
        private readonly string _hrmsConnectionString;
        private readonly string _crsConnectionString;
        private const int BatchSize = 500;
        private const int MaxFetchAttempts = 3;

        public BeneficiaryImportService(string hrmsConnectionString, string crsConnectionString)
        {
            _hrmsConnectionString = string.IsNullOrWhiteSpace(hrmsConnectionString)
                ? throw new ArgumentException("HRMS connection string is required.", nameof(hrmsConnectionString))
                : hrmsConnectionString;

            _crsConnectionString = string.IsNullOrWhiteSpace(crsConnectionString)
                ? throw new ArgumentException("CRS connection string is required.", nameof(crsConnectionString))
                : crsConnectionString;
        }

        public async Task<BeneficiaryImportResult> ImportFromCrsAsync()
        {
            const string selectSql = @"
SELECT
    residents_id,
    beneficiary_id,
    civilregistry_id,
    last_name,
    first_name,
    middle_name,
    full_name,
    sex,
    date_of_birth,
    age,
    marital_status,
    address,
    is_pwd,
    pwd_id_no,
    disability_type,
    cause_of_disability,
    is_senior,
    senior_id_no
FROM val_beneficiaries
ORDER BY beneficiary_id, residents_id
LIMIT @limit OFFSET @offset;";

            const string existsSql = @"
SELECT 1
FROM BeneficiaryStaging
WHERE BeneficiaryId = @beneficiary_id
LIMIT 1;";

            const string insertSql = @"
INSERT INTO BeneficiaryStaging
(
    ResidentsId,
    BeneficiaryId,
    CivilRegistryID,
    FirstName,
    LastName,
    MiddleName,
    FullName,
    Sex,
    DateOfBirth,
    Age,
    MaritalStatus,
    Address,
    IsPwd,
    PwdIdNo,
    DisabilityType,
    CauseOfDisability,
    IsSenior,
    SeniorIdNo,
    VerificationStatus,
    ImportedAt
)
VALUES
(
    @residents_id,
    @beneficiary_id,
    @civil_registryid,
    @first_name,
    @last_name,
    @middle_name,
    @full_name,
    @sex,
    @date_of_birth,
    @age,
    @marital_status,
    @address,
    @is_pwd,
    @pwd_id_no,
    @disability_type,
    @cause_of_disability,
    @is_senior,
    @senior_id_no,
    @verification_status,
    @imported_at
);";

            var imported = 0;
            var skipped = 0;
            var invalid = 0;
            var offset = 0;

            try
            {
                while (true)
                {
                    var batch = await ReadBatchAsync(selectSql, offset);
                    if (batch.Count == 0)
                    {
                        break;
                    }

                    await using var hrmsConnection = new MySqlConnection(_hrmsConnectionString);
                    await hrmsConnection.OpenAsync();
                    await BeneficiaryStagingSchemaCompatibility.EnsureAsync(hrmsConnection);
                    await using var transaction = await hrmsConnection.BeginTransactionAsync();

                    await using var exists = new MySqlCommand(existsSql, hrmsConnection, transaction);
                    exists.Parameters.Add("@beneficiary_id", MySqlDbType.VarChar);

                    await using var insert = new MySqlCommand(insertSql, hrmsConnection, transaction);
                    insert.Parameters.Add("@residents_id", MySqlDbType.Int64);
                    insert.Parameters.Add("@beneficiary_id", MySqlDbType.VarChar);
                    insert.Parameters.Add("@civil_registryid", MySqlDbType.VarChar);
                    insert.Parameters.Add("@first_name", MySqlDbType.VarChar);
                    insert.Parameters.Add("@last_name", MySqlDbType.VarChar);
                    insert.Parameters.Add("@middle_name", MySqlDbType.VarChar);
                    insert.Parameters.Add("@full_name", MySqlDbType.VarChar);
                    insert.Parameters.Add("@sex", MySqlDbType.VarChar);
                    insert.Parameters.Add("@date_of_birth", MySqlDbType.VarChar);
                    insert.Parameters.Add("@age", MySqlDbType.VarChar);
                    insert.Parameters.Add("@marital_status", MySqlDbType.VarChar);
                    insert.Parameters.Add("@address", MySqlDbType.VarChar);
                    insert.Parameters.Add("@is_pwd", MySqlDbType.Bit);
                    insert.Parameters.Add("@pwd_id_no", MySqlDbType.VarChar);
                    insert.Parameters.Add("@disability_type", MySqlDbType.VarChar);
                    insert.Parameters.Add("@cause_of_disability", MySqlDbType.VarChar);
                    insert.Parameters.Add("@is_senior", MySqlDbType.Bit);
                    insert.Parameters.Add("@senior_id_no", MySqlDbType.VarChar);
                    insert.Parameters.Add("@verification_status", MySqlDbType.Int32);
                    insert.Parameters.Add("@imported_at", MySqlDbType.DateTime);

                    foreach (var row in batch)
                    {
                        var beneficiaryId = row.BeneficiaryId;
                        if (string.IsNullOrWhiteSpace(beneficiaryId))
                        {
                            invalid++;
                            continue;
                        }

                        exists.Parameters["@beneficiary_id"].Value = beneficiaryId;
                        var found = await exists.ExecuteScalarAsync();
                        if (found != null && found != DBNull.Value)
                        {
                            skipped++;
                            continue;
                        }

                        var civilRegistryId = row.CivilRegistryId;
                        if (string.IsNullOrWhiteSpace(civilRegistryId))
                        {
                            civilRegistryId = beneficiaryId;
                        }

                        insert.Parameters["@residents_id"].Value = row.ResidentsId.HasValue
                            ? row.ResidentsId.Value
                            : DBNull.Value;
                        insert.Parameters["@beneficiary_id"].Value = beneficiaryId;
                        insert.Parameters["@civil_registryid"].Value = civilRegistryId!;
                        insert.Parameters["@first_name"].Value = row.FirstName;
                        insert.Parameters["@last_name"].Value = row.LastName;
                        insert.Parameters["@middle_name"].Value = ToDbValue(row.MiddleName);
                        insert.Parameters["@full_name"].Value = ToDbValue(row.FullName);
                        insert.Parameters["@sex"].Value = ToDbValue(row.Sex);
                        insert.Parameters["@date_of_birth"].Value = ToDbValue(row.DateOfBirth);
                        insert.Parameters["@age"].Value = ToDbValue(row.Age);
                        insert.Parameters["@marital_status"].Value = ToDbValue(row.MaritalStatus);
                        insert.Parameters["@address"].Value = string.IsNullOrWhiteSpace(row.Address) ? "Address not available" : row.Address;
                        insert.Parameters["@is_pwd"].Value = row.IsPwd;
                        insert.Parameters["@pwd_id_no"].Value = ToDbValue(row.PwdIdNo);
                        insert.Parameters["@disability_type"].Value = ToDbValue(row.DisabilityType);
                        insert.Parameters["@cause_of_disability"].Value = ToDbValue(row.CauseOfDisability);
                        insert.Parameters["@is_senior"].Value = row.IsSenior;
                        insert.Parameters["@senior_id_no"].Value = ToDbValue(row.SeniorIdNo);
                        insert.Parameters["@verification_status"].Value = (int)BeneficiaryVerificationStatus.Pending;
                        insert.Parameters["@imported_at"].Value = DateTime.Now;

                        await insert.ExecuteNonQueryAsync();
                        imported++;
                    }

                    await transaction.CommitAsync();

                    if (batch.Count < BatchSize)
                    {
                        break;
                    }

                    offset += BatchSize;
                }

                return new BeneficiaryImportResult(imported, skipped, invalid);
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException(
                    $"Beneficiary import failed (MySQL #{ex.Number}): {ex.Message}",
                    ex);
            }
        }

        private async Task<List<CrsBeneficiaryRow>> ReadBatchAsync(string selectSql, int offset)
        {
            for (var attempt = 1; attempt <= MaxFetchAttempts; attempt++)
            {
                try
                {
                    await using var crsConnection = new MySqlConnection(_crsConnectionString);
                    await crsConnection.OpenAsync();

                    await using var select = new MySqlCommand(selectSql, crsConnection)
                    {
                        CommandTimeout = 180
                    };
                    select.Parameters.AddWithValue("@limit", BatchSize);
                    select.Parameters.AddWithValue("@offset", offset);

                    var batch = new List<CrsBeneficiaryRow>(BatchSize);
                    await using var reader = await select.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        batch.Add(new CrsBeneficiaryRow
                        {
                            ResidentsId = reader["residents_id"] == DBNull.Value
                                ? null
                                : Convert.ToInt64(reader["residents_id"]),
                            BeneficiaryId = ReadTrimmed(reader, "beneficiary_id"),
                            CivilRegistryId = ReadTrimmed(reader, "civilregistry_id"),
                            FirstName = ReadTrimmed(reader, "first_name") ?? string.Empty,
                            LastName = ReadTrimmed(reader, "last_name") ?? string.Empty,
                            MiddleName = ReadTrimmed(reader, "middle_name"),
                            FullName = ReadTrimmed(reader, "full_name"),
                            Sex = ReadTrimmed(reader, "sex"),
                            DateOfBirth = ReadTrimmed(reader, "date_of_birth"),
                            Age = ReadTrimmed(reader, "age"),
                            MaritalStatus = ReadTrimmed(reader, "marital_status"),
                            Address = ReadTrimmed(reader, "address"),
                            IsPwd = ReadBit(reader, "is_pwd"),
                            PwdIdNo = ReadTrimmed(reader, "pwd_id_no"),
                            DisabilityType = ReadTrimmed(reader, "disability_type"),
                            CauseOfDisability = ReadTrimmed(reader, "cause_of_disability"),
                            IsSenior = ReadBit(reader, "is_senior"),
                            SeniorIdNo = ReadTrimmed(reader, "senior_id_no")
                        });
                    }

                    return batch;
                }
                catch (MySqlException) when (attempt < MaxFetchAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
                catch (IOException) when (attempt < MaxFetchAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }

            throw new InvalidOperationException("CRS batch fetch failed after multiple retry attempts.");
        }

        private static object ToDbValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

        private static string? ReadTrimmed(MySqlDataReader reader, string columnName) =>
            reader[columnName] == DBNull.Value ? null : reader[columnName]?.ToString()?.Trim();

        private static bool ReadBit(MySqlDataReader reader, string columnName) =>
            reader[columnName] != DBNull.Value && Convert.ToInt32(reader[columnName]) == 1;

        private sealed class CrsBeneficiaryRow
        {
            public long? ResidentsId { get; init; }
            public string? BeneficiaryId { get; init; }
            public string? CivilRegistryId { get; init; }
            public string FirstName { get; init; } = string.Empty;
            public string LastName { get; init; } = string.Empty;
            public string? MiddleName { get; init; }
            public string? FullName { get; init; }
            public string? Sex { get; init; }
            public string? DateOfBirth { get; init; }
            public string? Age { get; init; }
            public string? MaritalStatus { get; init; }
            public string? Address { get; init; }
            public bool IsPwd { get; init; }
            public string? PwdIdNo { get; init; }
            public string? DisabilityType { get; init; }
            public string? CauseOfDisability { get; init; }
            public bool IsSenior { get; init; }
            public string? SeniorIdNo { get; init; }
        }
    }
}
