using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record LiveToLocalSyncResult(
        int HrmsTableCount,
        int HrmsRowCount,
        int EmployeeCount,
        int UserCount,
        int AttendanceLogCount,
        int LeaveApplicationCount,
        int PayrollRunCount,
        int ChecklistRowCount,
        string CompanyName,
        string CompanyLogoPath,
        decimal GgmsAllocatedAmount,
        decimal GgmsRemainingAmount,
        long CrsBeneficiaryCount);

    public sealed class LiveToLocalSyncService
    {
        private const long GgmsOfficeId = 18;
        private const string GgmsOfficeCode = "OFF-2026-0007";

        private static readonly DbConnectionSettings LiveHrmsSettings = new()
        {
            Host = "194.59.164.58",
            Port = "3306",
            Database = "u621755393_hrms3b",
            Username = "u621755393_hrms3b_user",
            Password = "Hrms3b@2026"
        };

        private static readonly DbConnectionSettings LocalHrmsSettings = new()
        {
            Host = "127.0.0.1",
            Port = "3306",
            Database = "hrms_db",
            Username = "hrms_app",
            Password = "15248130"
        };

        private static readonly GgmsConnectionSettings LiveGgmsSettings = new()
        {
            Host = "194.59.164.58",
            Port = "3306",
            Database = "u621755393_ggms",
            Username = "u621755393_ggms_user",
            Password = "Ggms@2026"
        };

        private static readonly GgmsConnectionSettings LocalGgmsSettings = new()
        {
            Host = "127.0.0.1",
            Port = "3306",
            Database = "ggms_db",
            Username = "hrms_app",
            Password = "15248130"
        };

        private static readonly CrsConnectionSettings LiveCrsSettings = new()
        {
            Host = "194.59.164.58",
            Port = "3306",
            Database = "u621755393_crs",
            Username = "u621755393_crs_user",
            Password = "Crs@2026"
        };

        private static readonly CrsConnectionSettings LocalCrsSettings = new()
        {
            Host = "127.0.0.1",
            Port = "3306",
            Database = "crs_db",
            Username = "hrms_app",
            Password = "15248130"
        };

        private readonly string _workingRoot;
        private readonly string _migrationsPath;

        public LiveToLocalSyncService(string storageLocation, string? migrationsPath = null)
        {
            if (string.IsNullOrWhiteSpace(storageLocation))
            {
                throw new ArgumentException("Storage location is required.", nameof(storageLocation));
            }

            _workingRoot = Path.Combine(storageLocation, "SyncCache", "LiveToLocal");
            _migrationsPath = string.IsNullOrWhiteSpace(migrationsPath)
                ? Path.Combine(AppContext.BaseDirectory, "Database", "Migrations")
                : migrationsPath;
        }

        public async Task<LiveToLocalSyncResult> SyncAsync(IProgress<string>? progress = null)
        {
            PrepareWorkingRoot();

            try
            {
                Report(progress, "Syncing live HRMS into localhost...");
                var hrmsRestore = await SyncHrmsAsync(progress);

                Report(progress, "Syncing live GGMS into localhost...");
                await MirrorDatabaseAsync(
                    GgmsConfig.BuildConnectionString(LiveGgmsSettings),
                    GgmsConfig.BuildConnectionString(LocalGgmsSettings),
                    Path.Combine(_workingRoot, "GGMS"));

                Report(progress, "Syncing live CRS beneficiaries into localhost...");
                await SyncCrsBeneficiariesAsync();

                Report(progress, "Verifying local offline snapshot...");
                var hrmsCounts = await QuerySingleAsync(
                    DbConfig.BuildConnectionString(LocalHrmsSettings),
                    @"
SELECT
  (SELECT COUNT(*) FROM employees) AS employees,
  (SELECT COUNT(*) FROM user_accounts) AS user_accounts,
  (SELECT COUNT(*) FROM attendance_logs) AS attendance_logs,
  (SELECT COUNT(*) FROM leave_applications) AS leave_applications,
  (SELECT COUNT(*) FROM payroll_runs) AS payroll_runs,
  (SELECT COUNT(*) FROM employee_document_checklist) AS checklist_rows;");

                var companyProfile = await new CompanyProfileDataService(DbConfig.BuildConnectionString(LocalHrmsSettings))
                    .GetCompanyProfileAsync();

                var allocation = await new GgmsFundAllocationService(
                    GgmsConfig.BuildConnectionString(LocalGgmsSettings),
                    GgmsOfficeId,
                    GgmsOfficeCode).GetActiveAllocationAsync()
                    ?? throw new InvalidOperationException(
                        $"Local GGMS allocation for Office ID {GgmsOfficeId} ({GgmsOfficeCode}) was not found after sync.");

                var crsBeneficiaryCount = await ExecuteScalarAsync<long>(
                    CrsConfig.BuildConnectionString(LocalCrsSettings),
                    "SELECT COUNT(*) FROM val_beneficiaries;");

                Report(progress, "Live-to-local sync completed.");

                return new LiveToLocalSyncResult(
                    HrmsTableCount: hrmsRestore.TableCount,
                    HrmsRowCount: hrmsRestore.RowCount,
                    EmployeeCount: ToInt32(hrmsCounts["employees"]),
                    UserCount: ToInt32(hrmsCounts["user_accounts"]),
                    AttendanceLogCount: ToInt32(hrmsCounts["attendance_logs"]),
                    LeaveApplicationCount: ToInt32(hrmsCounts["leave_applications"]),
                    PayrollRunCount: ToInt32(hrmsCounts["payroll_runs"]),
                    ChecklistRowCount: ToInt32(hrmsCounts["checklist_rows"]),
                    CompanyName: string.IsNullOrWhiteSpace(companyProfile.CompanyName)
                        ? CompanyProfile.Default.CompanyName
                        : companyProfile.CompanyName,
                    CompanyLogoPath: string.IsNullOrWhiteSpace(companyProfile.LogoPath)
                        ? CompanyProfile.Default.LogoPath
                        : companyProfile.LogoPath,
                    GgmsAllocatedAmount: allocation.AllocatedAmount,
                    GgmsRemainingAmount: allocation.RemainingAmount,
                    CrsBeneficiaryCount: crsBeneficiaryCount);
            }
            finally
            {
                TryDeleteWorkingRoot();
            }
        }

        private async Task<DatabaseRestoreResult> SyncHrmsAsync(IProgress<string>? progress)
        {
            if (!Directory.Exists(_migrationsPath))
            {
                throw new DirectoryNotFoundException($"Migration path was not found: {_migrationsPath}");
            }

            Report(progress, "Applying local HRMS migrations...");
            await new DbMigrationService(
                DbConfig.BuildConnectionString(LocalHrmsSettings),
                _migrationsPath).ApplyPendingMigrationsAsync();

            var hrmsRoot = Path.Combine(_workingRoot, "HRMS");
            var backupPath = await CreateFullBackupAsync(
                DbConfig.BuildConnectionString(LiveHrmsSettings),
                hrmsRoot);

            Report(progress, "Restoring live HRMS snapshot into localhost...");
            var restore = await new DatabaseBackupService(
                DbConfig.BuildConnectionString(LocalHrmsSettings),
                hrmsRoot).RestoreBackupAsync(backupPath);

            Report(progress, "Refreshing local company profile and logo path...");
            var liveProfile = await new CompanyProfileDataService(DbConfig.BuildConnectionString(LiveHrmsSettings))
                .GetCompanyProfileAsync();

            await new CompanyProfileDataService(DbConfig.BuildConnectionString(LocalHrmsSettings))
                .SaveCompanyProfileAsync(new CompanyProfile
                {
                    CompanyName = string.IsNullOrWhiteSpace(liveProfile.CompanyName) ? CompanyProfile.Default.CompanyName : liveProfile.CompanyName,
                    Address = string.IsNullOrWhiteSpace(liveProfile.Address) ? CompanyProfile.Default.Address : liveProfile.Address,
                    OwnerName = string.IsNullOrWhiteSpace(liveProfile.OwnerName) ? CompanyProfile.Default.OwnerName : liveProfile.OwnerName,
                    SerialNumber = string.IsNullOrWhiteSpace(liveProfile.SerialNumber) ? CompanyProfile.Default.SerialNumber : liveProfile.SerialNumber,
                    LogoPath = string.IsNullOrWhiteSpace(liveProfile.LogoPath) ? CompanyProfile.Default.LogoPath : liveProfile.LogoPath
                });

            return restore;
        }

        private static async Task MirrorDatabaseAsync(
            string sourceConnectionString,
            string targetConnectionString,
            string workingRoot)
        {
            var backupPath = await CreateFullBackupAsync(sourceConnectionString, workingRoot);
            await new DatabaseBackupService(targetConnectionString, workingRoot).RestoreBackupAsync(backupPath);
        }

        private static async Task SyncCrsBeneficiariesAsync()
        {
            var sourceConnectionString = CrsConfig.BuildConnectionString(LiveCrsSettings);
            var targetConnectionString = CrsConfig.BuildConnectionString(LocalCrsSettings);

            var table = new DataTable();

            await using (var sourceConnection = new MySqlConnection(sourceConnectionString))
            {
                await sourceConnection.OpenAsync();
                await using var sourceCommand = new MySqlCommand(
                    "SELECT * FROM val_beneficiaries ORDER BY beneficiary_id, residents_id;",
                    sourceConnection);
                await using var reader = await sourceCommand.ExecuteReaderAsync();
                table.Load(reader);
            }

            await using var targetConnection = new MySqlConnection(targetConnectionString);
            await targetConnection.OpenAsync();
            await using var transaction = await targetConnection.BeginTransactionAsync();

            try
            {
                var targetColumns = await GetTableColumnsAsync(targetConnection, transaction, "val_beneficiaries");
                var sharedColumns = table.Columns
                    .Cast<DataColumn>()
                    .Where(column => targetColumns.Contains(column.ColumnName))
                    .ToList();

                if (sharedColumns.Count == 0)
                {
                    throw new InvalidOperationException("Local CRS table 'val_beneficiaries' does not share any compatible columns with the live CRS source.");
                }

                await using (var deleteCommand = new MySqlCommand("DELETE FROM val_beneficiaries;", targetConnection, transaction))
                {
                    await deleteCommand.ExecuteNonQueryAsync();
                }

                var columnList = string.Join(", ", sharedColumns.Select(column => $"`{column.ColumnName}`"));
                const int batchSize = 100;

                for (var rowOffset = 0; rowOffset < table.Rows.Count; rowOffset += batchSize)
                {
                    var rowCount = Math.Min(batchSize, table.Rows.Count - rowOffset);
                    var sql = new StringBuilder();
                    sql.Append("INSERT INTO val_beneficiaries (");
                    sql.Append(columnList);
                    sql.Append(") VALUES ");

                    await using var insertCommand = new MySqlCommand(string.Empty, targetConnection, transaction);

                    for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
                    {
                        if (rowIndex > 0)
                        {
                            sql.Append(", ");
                        }

                        sql.Append('(');
                        for (var columnIndex = 0; columnIndex < sharedColumns.Count; columnIndex++)
                        {
                            if (columnIndex > 0)
                            {
                                sql.Append(", ");
                            }

                            var parameterName = $"@p_{rowOffset + rowIndex}_{columnIndex}";
                            sql.Append(parameterName);
                            var value = table.Rows[rowOffset + rowIndex][sharedColumns[columnIndex]];
                            insertCommand.Parameters.AddWithValue(parameterName, value == DBNull.Value ? DBNull.Value : value);
                        }

                        sql.Append(')');
                    }

                    sql.Append(';');
                    insertCommand.CommandText = sql.ToString();
                    await insertCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                }

                throw;
            }
        }

        private static async Task<HashSet<string>> GetTableColumnsAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string tableName)
        {
            await using var command = new MySqlCommand(
                @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @tableName;",
                connection,
                transaction);
            command.Parameters.AddWithValue("@tableName", tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    columns.Add(reader.GetString(0));
                }
            }

            return columns;
        }

        private static async Task<string> CreateFullBackupAsync(string connectionString, string workingRoot)
        {
            PrepareDirectory(workingRoot);
            var backupService = new DatabaseBackupService(connectionString, workingRoot);
            var backup = await backupService.CreateFullBackupAsync();
            if (!backup.Created || string.IsNullOrWhiteSpace(backup.FilePath))
            {
                throw new InvalidOperationException("Full backup could not be created for synchronization.");
            }

            return backup.FilePath;
        }

        private void PrepareWorkingRoot()
        {
            if (Directory.Exists(_workingRoot))
            {
                Directory.Delete(_workingRoot, recursive: true);
            }

            Directory.CreateDirectory(_workingRoot);
        }

        private void TryDeleteWorkingRoot()
        {
            try
            {
                if (Directory.Exists(_workingRoot))
                {
                    Directory.Delete(_workingRoot, recursive: true);
                }
            }
            catch
            {
                // Sync cache cleanup is best-effort only.
            }
        }

        private static void PrepareDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            Directory.CreateDirectory(path);
        }

        private static void Report(IProgress<string>? progress, string message) =>
            progress?.Report(message);

        private static async Task<Dictionary<string, object>> QuerySingleAsync(string connectionString, string sql)
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Expected one row but the query returned none.");
            }

            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? 0 : reader.GetValue(i);
            }

            return row;
        }

        private static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql)
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(sql, connection);
            var value = await command.ExecuteScalarAsync();
            if (value == null || value == DBNull.Value)
            {
                return default!;
            }

            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }

        private static int ToInt32(object value) =>
            value == null || value == DBNull.Value
                ? 0
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}
