using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        private const string ConsolidatedTransactionsTableName = "consolidated_transactions";
        private const string ConsolidatedProjectNameColumn = "project_name";

        private static readonly DbConnectionSettings LiveHrmsSettings = new()
        {
            Host = "194.59.164.58",
            Port = "3306",
            Database = "u621755393_hrms3b",
            Username = "u621755393_hrms3b_user",
            Password = "Hrms3b@2026"
        };

        private static readonly GgmsConnectionSettings LiveGgmsSettings = new()
        {
            Host = "194.59.164.58",
            Port = "3306",
            Database = "u621755393_ggms",
            Username = "u621755393_ggms_user",
            Password = "Ggms@2026"
        };

        private static readonly CrsConnectionSettings LiveCrsSettings = new()
        {
            Host = "194.59.164.58",
            Port = "3306",
            Database = "u621755393_crs",
            Username = "u621755393_crs_user",
            Password = "Crs@2026"
        };

        private static readonly DbConnectionSettings LocalHrmsFallbackSettings = new()
        {
            Host = "127.0.0.1",
            Port = "3306",
            Database = "hrms_db",
            Username = "hrms_app",
            Password = "15248130"
        };

        private readonly string _workingRoot;
        private readonly string _migrationsPath;
        private readonly DbConnectionSettings _localHrmsSettings;
        private readonly GgmsConnectionSettings _localGgmsSettings;
        private readonly CrsConnectionSettings _localCrsSettings;

        public LiveToLocalSyncService(string storageLocation, string? migrationsPath = null)
            : this(storageLocation, null, null, null, migrationsPath)
        {
        }

        public LiveToLocalSyncService(
            string storageLocation,
            DbConnectionSettings? localHrmsSettings,
            GgmsConnectionSettings? localGgmsSettings,
            CrsConnectionSettings? localCrsSettings,
            string? migrationsPath = null)
        {
            if (string.IsNullOrWhiteSpace(storageLocation))
            {
                throw new ArgumentException("Storage location is required.", nameof(storageLocation));
            }

            _workingRoot = Path.Combine(storageLocation, "SyncCache", "LiveToLocal");
            _migrationsPath = string.IsNullOrWhiteSpace(migrationsPath)
                ? Path.Combine(AppContext.BaseDirectory, "Database", "Migrations")
                : migrationsPath;

            _localHrmsSettings = ResolveLocalHrmsSettings(localHrmsSettings);
            _localGgmsSettings = ResolveLocalGgmsSettings(localGgmsSettings, _localHrmsSettings);
            _localCrsSettings = ResolveLocalCrsSettings(localCrsSettings, _localHrmsSettings);
        }

        public async Task<LiveToLocalSyncResult> SyncAsync(IProgress<string>? progress = null)
        {
            PrepareWorkingRoot();

            try
            {
                var liveHrmsConnectionString = DbConfig.BuildConnectionString(LiveHrmsSettings);
                var localHrmsConnectionString = DbConfig.BuildConnectionString(_localHrmsSettings);
                var liveGgmsConnectionString = GgmsConfig.BuildConnectionString(LiveGgmsSettings);
                var localGgmsConnectionString = GgmsConfig.BuildConnectionString(_localGgmsSettings);
                var liveCrsConnectionString = CrsConfig.BuildConnectionString(LiveCrsSettings);
                var localCrsConnectionString = CrsConfig.BuildConnectionString(_localCrsSettings);

                Report(progress, "Syncing live HRMS into localhost...");
                var hrmsRestore = await SyncHrmsAsync(progress, liveHrmsConnectionString, localHrmsConnectionString);

                Report(progress, "Syncing live GGMS into localhost...");
                await MirrorDatabaseAsync(
                    liveGgmsConnectionString,
                    localGgmsConnectionString,
                    Path.Combine(_workingRoot, "GGMS"));

                Report(progress, "Syncing live CRS beneficiaries into localhost...");
                await SyncCrsBeneficiariesAsync(liveCrsConnectionString, localCrsConnectionString);

                Report(progress, "Verifying local offline snapshot...");
                var hrmsCounts = await QuerySingleAsync(
                    localHrmsConnectionString,
                    @"
SELECT
  (SELECT COUNT(*) FROM employees) AS employees,
  (SELECT COUNT(*) FROM user_accounts) AS user_accounts,
  (SELECT COUNT(*) FROM attendance_logs) AS attendance_logs,
  (SELECT COUNT(*) FROM leave_applications) AS leave_applications,
  (SELECT COUNT(*) FROM payroll_runs) AS payroll_runs,
  (SELECT COUNT(*) FROM employee_document_checklist) AS checklist_rows;");

                var companyProfile = await new CompanyProfileDataService(localHrmsConnectionString)
                    .GetCompanyProfileAsync();

                var allocation = await new GgmsFundAllocationService(
                    localGgmsConnectionString,
                    GgmsOfficeId,
                    GgmsOfficeCode).GetActiveAllocationAsync()
                    ?? new GgmsBudgetAllocationDto(
                        AllocationId: 0,
                        Program: GgmsOfficeCode,
                        AllocatedAmount: 0m,
                        UsedAmount: 0m,
                        RemainingAmount: 0m,
                        Status: "not-found");

                var crsBeneficiaryCount = await ExecuteScalarAsync<long>(
                    localCrsConnectionString,
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

        private async Task<DatabaseRestoreResult> SyncHrmsAsync(
            IProgress<string>? progress,
            string liveHrmsConnectionString,
            string localHrmsConnectionString)
        {
            if (!Directory.Exists(_migrationsPath))
            {
                throw new DirectoryNotFoundException($"Migration path was not found: {_migrationsPath}");
            }

            await EnsureDatabaseExistsAsync(localHrmsConnectionString);

            Report(progress, "Applying local HRMS migrations...");
            await new DbMigrationService(localHrmsConnectionString, _migrationsPath).ApplyPendingMigrationsAsync();

            var hrmsRoot = Path.Combine(_workingRoot, "HRMS");
            var backupPath = await CreateFullBackupAsync(liveHrmsConnectionString, hrmsRoot);

            Report(progress, "Restoring live HRMS snapshot into localhost...");
            var restore = await new DatabaseBackupService(localHrmsConnectionString, hrmsRoot).RestoreBackupAsync(backupPath);

            Report(progress, "Refreshing local company profile and logo path...");
            var liveProfile = await new CompanyProfileDataService(liveHrmsConnectionString).GetCompanyProfileAsync();

            await new CompanyProfileDataService(localHrmsConnectionString)
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
            await EnsureDatabaseExistsAsync(targetConnectionString);
            await EnsureConsolidatedTransactionsTableAsync(targetConnectionString);

            var backupPath = await CreateFullBackupAsync(sourceConnectionString, workingRoot);
            var backupSchemas = await LoadBackupSchemasAsync(backupPath);
            await EnsureTargetTablesExistAsync(targetConnectionString, backupSchemas);

            var restoreService = new DatabaseBackupService(targetConnectionString, workingRoot);

            try
            {
                await restoreService.RestoreBackupAsync(backupPath);
            }
            catch (MySqlException ex) when (IsMissingConsolidatedTransactionsTableError(ex))
            {
                await EnsureConsolidatedTransactionsTableAsync(targetConnectionString);
                await restoreService.RestoreBackupAsync(backupPath);
            }
        }

        private static async Task SyncCrsBeneficiariesAsync(
            string sourceConnectionString,
            string targetConnectionString)
        {
            await EnsureDatabaseExistsAsync(targetConnectionString);

            var sourceSchema = await ReadTableSchemaAsync(sourceConnectionString, "val_beneficiaries");
            await EnsureTargetTablesExistAsync(targetConnectionString, new[] { sourceSchema });

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

        private static async Task<List<BackupTableSchema>> LoadBackupSchemasAsync(string backupPath)
        {
            await using var stream = File.OpenRead(backupPath);
            var backup = await JsonSerializer.DeserializeAsync<DatabaseBackupFile>(stream)
                ?? throw new InvalidOperationException($"Failed to read backup schema from '{backupPath}'.");

            return backup.Tables
                .Where(table => table.Schema != null && !string.IsNullOrWhiteSpace(table.Schema.TableName))
                .Select(table => table.Schema)
                .ToList();
        }

        private static async Task<BackupTableSchema> ReadTableSchemaAsync(string connectionString, string tableName)
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var columns = new List<BackupColumnDefinition>();
            await using (var columnCommand = new MySqlCommand(
                             @"
SELECT COLUMN_NAME, DATA_TYPE, COLUMN_TYPE, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @tableName
ORDER BY ORDINAL_POSITION;",
                             connection))
            {
                columnCommand.Parameters.AddWithValue("@tableName", tableName);

                await using var reader = await columnCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(new BackupColumnDefinition
                    {
                        Name = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        DataType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        ColumnType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        IsNullable = !reader.IsDBNull(3) && string.Equals(reader.GetString(3), "YES", StringComparison.OrdinalIgnoreCase),
                        OrdinalPosition = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                    });
                }
            }

            if (columns.Count == 0)
            {
                throw new InvalidOperationException($"Source table '{tableName}' was not found.");
            }

            var primaryKeys = new List<string>();
            await using (var keyCommand = new MySqlCommand(
                             @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @tableName
  AND CONSTRAINT_NAME = 'PRIMARY'
ORDER BY ORDINAL_POSITION;",
                             connection))
            {
                keyCommand.Parameters.AddWithValue("@tableName", tableName);
                await using var keyReader = await keyCommand.ExecuteReaderAsync();
                while (await keyReader.ReadAsync())
                {
                    if (!keyReader.IsDBNull(0))
                    {
                        primaryKeys.Add(keyReader.GetString(0));
                    }
                }
            }

            return new BackupTableSchema
            {
                TableName = tableName,
                Columns = columns.OrderBy(column => column.OrdinalPosition).ToList(),
                PrimaryKeyColumns = primaryKeys
            };
        }

        private static async Task EnsureTargetTablesExistAsync(
            string targetConnectionString,
            IReadOnlyCollection<BackupTableSchema> sourceSchemas)
        {
            if (sourceSchemas.Count == 0)
            {
                return;
            }

            await using var targetConnection = new MySqlConnection(targetConnectionString);
            await targetConnection.OpenAsync();

            var existingTables = await GetTableNamesAsync(targetConnection);
            foreach (var schema in sourceSchemas.OrderBy(x => x.TableName, StringComparer.OrdinalIgnoreCase))
            {
                if (existingTables.Contains(schema.TableName))
                {
                    continue;
                }

                await CreateTableAsync(targetConnection, schema);
                existingTables.Add(schema.TableName);
            }
        }

        private static async Task<HashSet<string>> GetTableNamesAsync(MySqlConnection connection)
        {
            var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var command = new MySqlCommand(
                @"
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_TYPE = 'BASE TABLE';",
                connection);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            return tableNames;
        }

        private static async Task CreateTableAsync(MySqlConnection connection, BackupTableSchema schema)
        {
            var orderedColumns = schema.Columns.OrderBy(column => column.OrdinalPosition).ToList();
            if (orderedColumns.Count == 0)
            {
                throw new InvalidOperationException($"Cannot create '{schema.TableName}' because the source schema does not include columns.");
            }

            var definitions = new List<string>(orderedColumns.Count + 1);
            foreach (var column in orderedColumns)
            {
                var columnType = !string.IsNullOrWhiteSpace(column.ColumnType)
                    ? column.ColumnType
                    : (!string.IsNullOrWhiteSpace(column.DataType) ? column.DataType : "varchar(255)");

                definitions.Add(
                    $"  {QuoteIdentifier(column.Name)} {columnType} {(column.IsNullable ? "NULL" : "NOT NULL")}");
            }

            if (schema.PrimaryKeyColumns.Count > 0)
            {
                var primaryKeyColumns = string.Join(", ", schema.PrimaryKeyColumns.Select(QuoteIdentifier));
                definitions.Add($"  PRIMARY KEY ({primaryKeyColumns})");
            }

            var sql = new StringBuilder();
            sql.Append("CREATE TABLE ");
            sql.Append(QuoteIdentifier(schema.TableName));
            sql.AppendLine(" (");
            sql.Append(string.Join(",\n", definitions));
            sql.AppendLine();
            sql.Append(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            await using var command = new MySqlCommand(sql.ToString(), connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task EnsureDatabaseExistsAsync(string connectionString)
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Database))
            {
                throw new InvalidOperationException("Target connection string is missing a database name.");
            }

            var databaseName = builder.Database.Trim();
            builder.Database = string.Empty;

            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            var safeDbName = databaseName.Replace("`", "``", StringComparison.Ordinal);
            var sql = $"CREATE DATABASE IF NOT EXISTS `{safeDbName}` DEFAULT CHARACTER SET utf8mb4 DEFAULT COLLATE utf8mb4_unicode_ci;";

            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task EnsureConsolidatedTransactionsTableAsync(string connectionString)
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var sql = @"
CREATE TABLE IF NOT EXISTS consolidated_transactions (
    id                INT AUTO_INCREMENT PRIMARY KEY,
    beneficiary_id    VARCHAR(45) NULL,
    civil_registry_id VARCHAR(45) NULL,
    project_code      VARCHAR(45) NULL,
    project_name      VARCHAR(45) NULL,
    office_id         VARCHAR(45) NULL,
    full_name         VARCHAR(45) NULL,
    first_name        VARCHAR(45) NULL,
    middle_name       VARCHAR(45) NULL,
    last_name         VARCHAR(45) NULL,
    office_name       VARCHAR(45) NULL,
    transaction_type  VARCHAR(45) NULL,
    amount            DECIMAL(20,4) NULL,
    transaction_date  DATE NULL,
    status            VARCHAR(45) NULL,
    created_at        DATETIME DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            await EnsureConsolidatedProjectNameColumnAsync(connection);
        }

        private static async Task EnsureConsolidatedProjectNameColumnAsync(MySqlConnection connection)
        {
            const string existsSql = @"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @table_name
  AND COLUMN_NAME = @column_name;";

            await using (var existsCommand = new MySqlCommand(existsSql, connection))
            {
                existsCommand.Parameters.AddWithValue("@table_name", ConsolidatedTransactionsTableName);
                existsCommand.Parameters.AddWithValue("@column_name", ConsolidatedProjectNameColumn);
                var result = await existsCommand.ExecuteScalarAsync();
                var exists = result != null && result != DBNull.Value && Convert.ToInt32(result, CultureInfo.InvariantCulture) > 0;
                if (exists)
                {
                    return;
                }
            }

            const string alterSql = "ALTER TABLE consolidated_transactions ADD COLUMN project_name VARCHAR(45) NULL AFTER project_code;";
            await using var alterCommand = new MySqlCommand(alterSql, connection);
            await alterCommand.ExecuteNonQueryAsync();
        }

        private static bool IsMissingConsolidatedTransactionsTableError(MySqlException ex)
        {
            return ex.Number == 1146 &&
                   ex.Message.Contains(ConsolidatedTransactionsTableName, StringComparison.OrdinalIgnoreCase);
        }

        private static DbConnectionSettings ResolveLocalHrmsSettings(DbConnectionSettings? explicitSettings)
        {
            if (explicitSettings != null && IsLocalHost(explicitSettings.Host))
            {
                return CloneDbSettings(explicitSettings, LocalHrmsFallbackSettings);
            }

            var configured = DbConfig.GetSettings();
            if (IsLocalHost(configured.Host))
            {
                return CloneDbSettings(configured, LocalHrmsFallbackSettings);
            }

            return CloneDbSettings(LocalHrmsFallbackSettings, LocalHrmsFallbackSettings);
        }

        private static GgmsConnectionSettings ResolveLocalGgmsSettings(
            GgmsConnectionSettings? explicitSettings,
            DbConnectionSettings localHrmsSettings)
        {
            var fallback = new GgmsConnectionSettings
            {
                Host = "127.0.0.1",
                Port = "3306",
                Database = "ggms_db",
                Username = localHrmsSettings.Username,
                Password = localHrmsSettings.Password
            };

            if (explicitSettings != null && IsLocalHost(explicitSettings.Host))
            {
                return CloneGgmsSettings(explicitSettings, fallback);
            }

            var configured = GgmsConfig.GetSettings();
            if (IsLocalHost(configured.Host))
            {
                return CloneGgmsSettings(configured, fallback);
            }

            return CloneGgmsSettings(fallback, fallback);
        }

        private static CrsConnectionSettings ResolveLocalCrsSettings(
            CrsConnectionSettings? explicitSettings,
            DbConnectionSettings localHrmsSettings)
        {
            var fallback = new CrsConnectionSettings
            {
                Host = "127.0.0.1",
                Port = "3306",
                Database = "crs_db",
                Username = localHrmsSettings.Username,
                Password = localHrmsSettings.Password
            };

            if (explicitSettings != null && IsLocalHost(explicitSettings.Host))
            {
                return CloneCrsSettings(explicitSettings, fallback);
            }

            var configured = CrsConfig.GetSettings();
            if (IsLocalHost(configured.Host))
            {
                return CloneCrsSettings(configured, fallback);
            }

            return CloneCrsSettings(fallback, fallback);
        }

        private static DbConnectionSettings CloneDbSettings(DbConnectionSettings source, DbConnectionSettings fallback)
        {
            return new DbConnectionSettings
            {
                Host = string.IsNullOrWhiteSpace(source.Host) ? fallback.Host : source.Host.Trim(),
                Port = string.IsNullOrWhiteSpace(source.Port) ? fallback.Port : source.Port.Trim(),
                Database = string.IsNullOrWhiteSpace(source.Database) ? fallback.Database : source.Database.Trim(),
                Username = string.IsNullOrWhiteSpace(source.Username) ? fallback.Username : source.Username.Trim(),
                Password = source.Password == null ? fallback.Password : source.Password.Trim()
            };
        }

        private static GgmsConnectionSettings CloneGgmsSettings(GgmsConnectionSettings source, GgmsConnectionSettings fallback)
        {
            return new GgmsConnectionSettings
            {
                Host = string.IsNullOrWhiteSpace(source.Host) ? fallback.Host : source.Host.Trim(),
                Port = string.IsNullOrWhiteSpace(source.Port) ? fallback.Port : source.Port.Trim(),
                Database = string.IsNullOrWhiteSpace(source.Database) ? fallback.Database : source.Database.Trim(),
                Username = string.IsNullOrWhiteSpace(source.Username) ? fallback.Username : source.Username.Trim(),
                Password = source.Password == null ? fallback.Password : source.Password.Trim()
            };
        }

        private static CrsConnectionSettings CloneCrsSettings(CrsConnectionSettings source, CrsConnectionSettings fallback)
        {
            return new CrsConnectionSettings
            {
                Host = string.IsNullOrWhiteSpace(source.Host) ? fallback.Host : source.Host.Trim(),
                Port = string.IsNullOrWhiteSpace(source.Port) ? fallback.Port : source.Port.Trim(),
                Database = string.IsNullOrWhiteSpace(source.Database) ? fallback.Database : source.Database.Trim(),
                Username = string.IsNullOrWhiteSpace(source.Username) ? fallback.Username : source.Username.Trim(),
                Password = source.Password == null ? fallback.Password : source.Password.Trim()
            };
        }

        private static bool IsLocalHost(string? host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            return host.Trim().ToLowerInvariant() switch
            {
                "127.0.0.1" => true,
                "localhost" => true,
                "::1" => true,
                _ => false
            };
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

        private static string QuoteIdentifier(string identifier) => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";

        private static int ToInt32(object value) =>
            value == null || value == DBNull.Value
                ? 0
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}
