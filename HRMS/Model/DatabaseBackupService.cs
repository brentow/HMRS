using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed class DatabaseBackupService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = true
        };

        private readonly string _connectionString;
        private readonly string _backupsRoot;
        private readonly string _metadataPath;

        public DatabaseBackupService(string connectionString, string storageLocation)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? throw new ArgumentException("Connection string is required.", nameof(connectionString))
                : connectionString;

            if (string.IsNullOrWhiteSpace(storageLocation))
            {
                throw new ArgumentException("Storage location is required.", nameof(storageLocation));
            }

            _backupsRoot = Path.Combine(storageLocation, "Backups");
            _metadataPath = Path.Combine(_backupsRoot, "backup_metadata.json");
        }

        public string BackupsRoot => _backupsRoot;
        public string MetadataPath => _metadataPath;

        public async Task<DatabaseBackupMetadata> GetMetadataAsync()
        {
            Directory.CreateDirectory(_backupsRoot);
            return await LoadMetadataAsync();
        }

        public async Task<DatabaseBackupResult> CreateFullBackupAsync()
        {
            var snapshot = await CaptureSnapshotAsync();
            var nowUtc = DateTime.UtcNow;
            var backupFile = await CreateBackupEnvelopeAsync(DatabaseBackupType.Full, nowUtc);
            backupFile.Tables = snapshot;

            var filePath = BuildBackupFilePath(backupFile.DatabaseName, DatabaseBackupType.Full, nowUtc);
            await SaveBackupFileAsync(filePath, backupFile);
            await UpdateMetadataAsync(backupFile, filePath);

            return new DatabaseBackupResult(
                Created: true,
                BackupType: DatabaseBackupType.Full,
                FilePath: filePath,
                TableCount: snapshot.Count,
                RowCount: snapshot.Sum(x => x.Rows.Count),
                ChangeCount: snapshot.Sum(x => x.Rows.Count));
        }

        public async Task<DatabaseBackupResult> CreateDifferentialBackupAsync()
        {
            var metadata = await LoadMetadataAsync();
            var latestFull = GetLatestBackup(metadata, DatabaseBackupType.Full)
                ?? throw new InvalidOperationException("A full backup is required before creating a differential backup.");

            var currentSnapshot = await CaptureSnapshotAsync();
            var currentMap = ToSnapshotMap(currentSnapshot);
            var baseState = await BuildStateForBackupAsync(
                latestFull,
                metadata,
                new Dictionary<string, Dictionary<string, BackupTableSnapshot>>(StringComparer.OrdinalIgnoreCase));

            var deltas = ComputeDeltas(currentMap, baseState);
            var changeCount = deltas.Sum(x => x.Upserts.Count + x.Deletes.Count);
            if (changeCount == 0)
            {
                return new DatabaseBackupResult(
                    Created: false,
                    BackupType: DatabaseBackupType.Differential,
                    FilePath: null,
                    TableCount: currentSnapshot.Count,
                    RowCount: currentSnapshot.Sum(x => x.Rows.Count),
                    ChangeCount: 0);
            }

            var nowUtc = DateTime.UtcNow;
            var backupFile = await CreateBackupEnvelopeAsync(DatabaseBackupType.Differential, nowUtc);
            backupFile.BaseFullBackupId = latestFull.BackupId;
            backupFile.ParentBackupId = latestFull.BackupId;
            backupFile.Deltas = deltas;

            var filePath = BuildBackupFilePath(backupFile.DatabaseName, DatabaseBackupType.Differential, nowUtc);
            await SaveBackupFileAsync(filePath, backupFile);
            await UpdateMetadataAsync(backupFile, filePath);

            return new DatabaseBackupResult(
                Created: true,
                BackupType: DatabaseBackupType.Differential,
                FilePath: filePath,
                TableCount: deltas.Count,
                RowCount: currentSnapshot.Sum(x => x.Rows.Count),
                ChangeCount: changeCount);
        }

        public async Task<DatabaseBackupResult> CreateIncrementalBackupAsync()
        {
            var metadata = await LoadMetadataAsync();
            var latestFull = GetLatestBackup(metadata, DatabaseBackupType.Full)
                ?? throw new InvalidOperationException("A full backup is required before creating an incremental backup.");
            var latestBackup = GetLatestBackup(metadata, null) ?? latestFull;

            var currentSnapshot = await CaptureSnapshotAsync();
            var currentMap = ToSnapshotMap(currentSnapshot);
            var baseState = await BuildStateForBackupAsync(
                latestBackup,
                metadata,
                new Dictionary<string, Dictionary<string, BackupTableSnapshot>>(StringComparer.OrdinalIgnoreCase));

            var deltas = ComputeDeltas(currentMap, baseState);
            var changeCount = deltas.Sum(x => x.Upserts.Count + x.Deletes.Count);
            if (changeCount == 0)
            {
                return new DatabaseBackupResult(
                    Created: false,
                    BackupType: DatabaseBackupType.Incremental,
                    FilePath: null,
                    TableCount: currentSnapshot.Count,
                    RowCount: currentSnapshot.Sum(x => x.Rows.Count),
                    ChangeCount: 0);
            }

            var nowUtc = DateTime.UtcNow;
            var backupFile = await CreateBackupEnvelopeAsync(DatabaseBackupType.Incremental, nowUtc);
            backupFile.BaseFullBackupId = string.Equals(latestBackup.BackupType, DatabaseBackupType.Full.ToString(), StringComparison.OrdinalIgnoreCase)
                ? latestBackup.BackupId
                : latestBackup.BaseFullBackupId ?? latestFull.BackupId;
            backupFile.ParentBackupId = latestBackup.BackupId;
            backupFile.Deltas = deltas;

            var filePath = BuildBackupFilePath(backupFile.DatabaseName, DatabaseBackupType.Incremental, nowUtc);
            await SaveBackupFileAsync(filePath, backupFile);
            await UpdateMetadataAsync(backupFile, filePath);

            return new DatabaseBackupResult(
                Created: true,
                BackupType: DatabaseBackupType.Incremental,
                FilePath: filePath,
                TableCount: deltas.Count,
                RowCount: currentSnapshot.Sum(x => x.Rows.Count),
                ChangeCount: changeCount);
        }

        public async Task<DatabaseRestoreResult> RestoreBackupAsync(string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
            {
                throw new ArgumentException("Backup file path is required.", nameof(backupFilePath));
            }

            var metadata = await LoadMetadataAsync();
            var selectedEntry = await ResolveRestoreEntryAsync(metadata, backupFilePath);
            var state = await BuildStateForBackupAsync(
                selectedEntry,
                metadata,
                new Dictionary<string, Dictionary<string, BackupTableSnapshot>>(StringComparer.OrdinalIgnoreCase));

            var snapshots = state.Values
                .OrderBy(x => x.Schema.TableName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await RestoreSnapshotsAsync(snapshots);

            return new DatabaseRestoreResult(
                BackupType: ParseBackupType(selectedEntry.BackupType),
                FilePath: backupFilePath,
                TableCount: snapshots.Count,
                RowCount: snapshots.Sum(x => x.Rows.Count));
        }

        public async Task<DatabaseBackupDeleteResult> DeleteBackupAsync(string backupId)
        {
            if (string.IsNullOrWhiteSpace(backupId))
            {
                throw new ArgumentException("Backup id is required.", nameof(backupId));
            }

            var metadata = await LoadMetadataAsync();
            var selectedEntry = metadata.Backups.FirstOrDefault(x =>
                string.Equals(x.BackupId, backupId.Trim(), StringComparison.OrdinalIgnoreCase));

            if (selectedEntry == null)
            {
                throw new InvalidOperationException("Selected backup was not found in backup metadata.");
            }

            var deleteEntries = ResolveDeleteEntries(metadata, selectedEntry.BackupId);
            if (deleteEntries.Count == 0)
            {
                throw new InvalidOperationException("No backup files were resolved for deletion.");
            }

            var deletedFiles = 0;
            var missingFiles = 0;

            foreach (var entry in deleteEntries
                .OrderByDescending(x => ParseUtc(x.CreatedAtUtc)))
            {
                var filePath = ResolveFilePath(entry.RelativePath);
                if (!File.Exists(filePath))
                {
                    missingFiles++;
                    continue;
                }

                File.Delete(filePath);
                deletedFiles++;
                TryDeleteEmptyParentDirectories(Path.GetDirectoryName(filePath));
            }

            var deletedIds = deleteEntries
                .Select(x => x.BackupId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            metadata.Backups.RemoveAll(x => deletedIds.Contains(x.BackupId));

            var latestAny = GetLatestBackup(metadata, null);
            metadata.LastBackupId = latestAny?.BackupId;
            metadata.LastBackupType = latestAny?.BackupType;
            metadata.LastFull = GetLatestBackup(metadata, DatabaseBackupType.Full)?.CreatedAtUtc;
            metadata.LastDifferential = GetLatestBackup(metadata, DatabaseBackupType.Differential)?.CreatedAtUtc;
            metadata.LastIncremental = GetLatestBackup(metadata, DatabaseBackupType.Incremental)?.CreatedAtUtc;

            await SaveMetadataAsync(metadata);

            return new DatabaseBackupDeleteResult(
                BackupId: selectedEntry.BackupId,
                DeletedEntries: deleteEntries.Count,
                DeletedFiles: deletedFiles,
                MissingFiles: missingFiles);
        }

        private async Task<DatabaseBackupFile> CreateBackupEnvelopeAsync(DatabaseBackupType backupType, DateTime createdAtUtc)
        {
            var (databaseName, sourceHost) = await GetDatabaseInfoAsync();
            return new DatabaseBackupFile
            {
                BackupId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                BackupType = backupType.ToString(),
                DatabaseName = databaseName,
                SourceHost = sourceHost,
                CreatedAtUtc = createdAtUtc.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        private async Task<(string DatabaseName, string SourceHost)> GetDatabaseInfoAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DATABASE(), @@hostname;";
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return ("hrms_db", "localhost");
            }

            return (
                reader.IsDBNull(0) ? "hrms_db" : reader.GetString(0),
                reader.IsDBNull(1) ? "localhost" : reader.GetString(1));
        }

        private async Task<List<BackupTableSnapshot>> CaptureSnapshotAsync()
        {
            var snapshots = new List<BackupTableSnapshot>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var tableNames = await GetTableNamesAsync(connection);
            foreach (var tableName in tableNames)
            {
                var schema = await GetTableSchemaAsync(connection, tableName);
                var rows = await ReadRowsAsync(connection, schema);
                snapshots.Add(new BackupTableSnapshot
                {
                    Schema = schema,
                    Rows = rows
                });
            }

            return snapshots;
        }

        private async Task RestoreSnapshotsAsync(IReadOnlyList<BackupTableSnapshot> snapshots)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var tableNames = await GetTableNamesAsync(connection, transaction);
                await ExecuteSqlAsync(connection, transaction, "SET FOREIGN_KEY_CHECKS = 0;");

                foreach (var tableName in tableNames)
                {
                    await ExecuteSqlAsync(connection, transaction, $"DELETE FROM {QuoteIdentifier(tableName)};");
                }

                foreach (var snapshot in snapshots)
                {
                    await RestoreTableAsync(connection, transaction, snapshot);
                }

                await ExecuteSqlAsync(connection, transaction, "SET FOREIGN_KEY_CHECKS = 1;");
                await transaction.CommitAsync();
            }
            catch
            {
                try
                {
                    await ExecuteSqlAsync(connection, transaction, "SET FOREIGN_KEY_CHECKS = 1;");
                }
                catch
                {
                }

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

        private async Task<DatabaseBackupMetadata> LoadMetadataAsync()
        {
            if (!File.Exists(_metadataPath))
            {
                return new DatabaseBackupMetadata();
            }

            await using var stream = File.OpenRead(_metadataPath);
            var metadata = await JsonSerializer.DeserializeAsync<DatabaseBackupMetadata>(stream, JsonOptions);
            metadata ??= new DatabaseBackupMetadata();
            metadata.Backups ??= new List<DatabaseBackupCatalogEntry>();
            return metadata;
        }

        private async Task SaveMetadataAsync(DatabaseBackupMetadata metadata)
        {
            Directory.CreateDirectory(_backupsRoot);
            await using var stream = File.Create(_metadataPath);
            await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions);
        }

        private async Task SaveBackupFileAsync(string filePath, DatabaseBackupFile backupFile)
        {
            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, backupFile, JsonOptions);
        }

        private async Task<DatabaseBackupFile> LoadBackupFileAsync(string filePath)
        {
            await using var stream = File.OpenRead(filePath);
            var backup = await JsonSerializer.DeserializeAsync<DatabaseBackupFile>(stream, JsonOptions);
            return backup ?? throw new InvalidOperationException($"Unable to read backup file '{filePath}'.");
        }

        private async Task UpdateMetadataAsync(DatabaseBackupFile backupFile, string filePath)
        {
            var metadata = await LoadMetadataAsync();
            var relativePath = Path.GetRelativePath(_backupsRoot, filePath);

            metadata.Backups.RemoveAll(x => string.Equals(x.BackupId, backupFile.BackupId, StringComparison.OrdinalIgnoreCase));
            metadata.Backups.Add(new DatabaseBackupCatalogEntry
            {
                BackupId = backupFile.BackupId,
                BackupType = backupFile.BackupType,
                RelativePath = relativePath,
                DatabaseName = backupFile.DatabaseName,
                CreatedAtUtc = backupFile.CreatedAtUtc,
                BaseFullBackupId = backupFile.BaseFullBackupId,
                ParentBackupId = backupFile.ParentBackupId
            });

            metadata.LastBackupId = backupFile.BackupId;
            metadata.LastBackupType = backupFile.BackupType;
            if (string.Equals(backupFile.BackupType, DatabaseBackupType.Full.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                metadata.LastFull = backupFile.CreatedAtUtc;
            }
            else if (string.Equals(backupFile.BackupType, DatabaseBackupType.Differential.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                metadata.LastDifferential = backupFile.CreatedAtUtc;
            }
            else if (string.Equals(backupFile.BackupType, DatabaseBackupType.Incremental.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                metadata.LastIncremental = backupFile.CreatedAtUtc;
            }

            metadata.Backups = metadata.Backups
                .OrderBy(x => ParseUtc(x.CreatedAtUtc))
                .ToList();

            await SaveMetadataAsync(metadata);
        }

        private async Task<List<string>> GetTableNamesAsync(MySqlConnection connection, MySqlTransaction? transaction = null)
        {
            const string sql = @"
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_TYPE = 'BASE TABLE'
  AND TABLE_NAME <> 'schema_migrations'
ORDER BY TABLE_NAME;";

            var tableNames = new List<string>();
            await using var command = new MySqlCommand(sql, connection);
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            return tableNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<BackupTableSchema> GetTableSchemaAsync(MySqlConnection connection, string tableName)
        {
            const string columnSql = @"
SELECT COLUMN_NAME, DATA_TYPE, COLUMN_TYPE, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @table_name
ORDER BY ORDINAL_POSITION;";

            const string primaryKeySql = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @table_name
  AND CONSTRAINT_NAME = 'PRIMARY'
ORDER BY ORDINAL_POSITION;";

            var schema = new BackupTableSchema
            {
                TableName = tableName
            };

            await using (var columnCommand = new MySqlCommand(columnSql, connection))
            {
                columnCommand.Parameters.AddWithValue("@table_name", tableName);
                await using var reader = await columnCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    schema.Columns.Add(new BackupColumnDefinition
                    {
                        Name = reader.GetString(0),
                        DataType = reader.GetString(1),
                        ColumnType = reader.GetString(2),
                        IsNullable = string.Equals(reader.GetString(3), "YES", StringComparison.OrdinalIgnoreCase),
                        OrdinalPosition = reader.GetInt32(4)
                    });
                }
            }

            await using (var keyCommand = new MySqlCommand(primaryKeySql, connection))
            {
                keyCommand.Parameters.AddWithValue("@table_name", tableName);
                await using var reader = await keyCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    schema.PrimaryKeyColumns.Add(reader.GetString(0));
                }
            }

            if (schema.PrimaryKeyColumns.Count == 0)
            {
                foreach (var column in schema.Columns.OrderBy(x => x.OrdinalPosition))
                {
                    schema.PrimaryKeyColumns.Add(column.Name);
                }
            }

            return schema;
        }

        private static async Task<List<Dictionary<string, string?>>> ReadRowsAsync(MySqlConnection connection, BackupTableSchema schema)
        {
            var orderByClause = schema.PrimaryKeyColumns.Count == 0
                ? string.Empty
                : " ORDER BY " + string.Join(", ", schema.PrimaryKeyColumns.Select(QuoteIdentifier));

            var sql = $"SELECT * FROM {QuoteIdentifier(schema.TableName)}{orderByClause};";
            var rows = new List<Dictionary<string, string?>>();

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in schema.Columns.OrderBy(x => x.OrdinalPosition))
                {
                    row[column.Name] = SerializeValue(column, reader[column.Name]);
                }
                rows.Add(row);
            }

            return rows;
        }

        private static async Task RestoreTableAsync(MySqlConnection connection, MySqlTransaction transaction, BackupTableSnapshot snapshot)
        {
            if (snapshot.Rows.Count == 0)
            {
                return;
            }

            var columns = snapshot.Schema.Columns.OrderBy(x => x.OrdinalPosition).ToList();
            var columnList = string.Join(", ", columns.Select(x => QuoteIdentifier(x.Name)));
            var parameterList = string.Join(", ", columns.Select((_, index) => $"@p{index}"));
            var sql = $"INSERT INTO {QuoteIdentifier(snapshot.Schema.TableName)} ({columnList}) VALUES ({parameterList});";

            foreach (var row in snapshot.Rows)
            {
                await using var command = new MySqlCommand(sql, connection, transaction);
                for (var i = 0; i < columns.Count; i++)
                {
                    row.TryGetValue(columns[i].Name, out var serialized);
                    command.Parameters.AddWithValue($"@p{i}", DeserializeValue(columns[i], serialized));
                }

                await command.ExecuteNonQueryAsync();
            }
        }

        private static async Task ExecuteSqlAsync(MySqlConnection connection, MySqlTransaction transaction, string sql)
        {
            await using var command = new MySqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<DatabaseBackupCatalogEntry> ResolveRestoreEntryAsync(DatabaseBackupMetadata metadata, string backupFilePath)
        {
            var fullPath = Path.GetFullPath(backupFilePath);
            var existing = metadata.Backups.FirstOrDefault(entry =>
                string.Equals(
                    Path.GetFullPath(ResolveFilePath(entry.RelativePath)),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return existing;
            }

            var backupFile = await LoadBackupFileAsync(backupFilePath);
            if (!string.Equals(backupFile.BackupType, DatabaseBackupType.Full.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Selected differential/incremental backup is not registered in backup metadata, so its restore chain cannot be resolved.");
            }

            return new DatabaseBackupCatalogEntry
            {
                BackupId = backupFile.BackupId,
                BackupType = backupFile.BackupType,
                RelativePath = backupFilePath,
                DatabaseName = backupFile.DatabaseName,
                CreatedAtUtc = backupFile.CreatedAtUtc,
                BaseFullBackupId = backupFile.BaseFullBackupId,
                ParentBackupId = backupFile.ParentBackupId
            };
        }

        private async Task<Dictionary<string, BackupTableSnapshot>> BuildStateForBackupAsync(
            DatabaseBackupCatalogEntry entry,
            DatabaseBackupMetadata metadata,
            IDictionary<string, Dictionary<string, BackupTableSnapshot>> cache)
        {
            if (cache.TryGetValue(entry.BackupId, out var cached))
            {
                return CloneState(cached);
            }

            var backupFile = await LoadBackupFileAsync(ResolveFilePath(entry.RelativePath));
            Dictionary<string, BackupTableSnapshot> state;

            if (ParseBackupType(backupFile.BackupType) == DatabaseBackupType.Full)
            {
                state = ToSnapshotMap(backupFile.Tables);
            }
            else
            {
                var parentId = !string.IsNullOrWhiteSpace(backupFile.ParentBackupId)
                    ? backupFile.ParentBackupId
                    : backupFile.BaseFullBackupId;

                if (string.IsNullOrWhiteSpace(parentId))
                {
                    throw new InvalidOperationException($"Backup '{entry.BackupId}' is missing its restore chain metadata.");
                }

                var parentEntry = metadata.Backups.FirstOrDefault(x => string.Equals(x.BackupId, parentId, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Backup '{entry.BackupId}' depends on '{parentId}', which is missing from backup metadata.");

                state = await BuildStateForBackupAsync(parentEntry, metadata, cache);
                ApplyDeltas(state, backupFile.Deltas);
            }

            cache[entry.BackupId] = CloneState(state);
            return CloneState(state);
        }

        private static List<BackupTableDelta> ComputeDeltas(
            IReadOnlyDictionary<string, BackupTableSnapshot> current,
            IReadOnlyDictionary<string, BackupTableSnapshot> baseline)
        {
            var tableNames = current.Keys
                .Concat(baseline.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

            var deltas = new List<BackupTableDelta>();

            foreach (var tableName in tableNames)
            {
                current.TryGetValue(tableName, out var currentSnapshot);
                baseline.TryGetValue(tableName, out var baselineSnapshot);

                var effectiveSchema = currentSnapshot?.Schema ?? baselineSnapshot?.Schema;
                if (effectiveSchema == null)
                {
                    continue;
                }

                var currentRows = currentSnapshot?.Rows ?? new List<Dictionary<string, string?>>();
                var baselineRows = baselineSnapshot?.Rows ?? new List<Dictionary<string, string?>>();

                var currentMap = currentRows.ToDictionary(
                    row => BuildRowKey(effectiveSchema.PrimaryKeyColumns, row),
                    CloneRow,
                    StringComparer.Ordinal);

                var baselineMap = baselineRows.ToDictionary(
                    row => BuildRowKey(effectiveSchema.PrimaryKeyColumns, row),
                    CloneRow,
                    StringComparer.Ordinal);

                var schemasMatch = currentSnapshot != null
                    && baselineSnapshot != null
                    && SchemasEquivalent(currentSnapshot.Schema, baselineSnapshot.Schema);

                var upserts = new List<Dictionary<string, string?>>();
                var deletes = new List<Dictionary<string, string?>>();

                if (!schemasMatch)
                {
                    upserts.AddRange(currentMap.Values.Select(CloneRow));
                }
                else
                {
                    foreach (var (key, row) in currentMap)
                    {
                        if (!baselineMap.TryGetValue(key, out var baselineRow) || !RowsEqual(effectiveSchema, row, baselineRow))
                        {
                            upserts.Add(CloneRow(row));
                        }
                    }
                }

                foreach (var missingKey in baselineMap.Keys.Except(currentMap.Keys, StringComparer.Ordinal))
                {
                    deletes.Add(ExtractKeyRow(effectiveSchema, baselineMap[missingKey]));
                }

                if (upserts.Count == 0 && deletes.Count == 0)
                {
                    continue;
                }

                deltas.Add(new BackupTableDelta
                {
                    Schema = CloneSchema(effectiveSchema),
                    Upserts = upserts,
                    Deletes = deletes
                });
            }

            return deltas;
        }

        private static void ApplyDeltas(
            IDictionary<string, BackupTableSnapshot> state,
            IEnumerable<BackupTableDelta> deltas)
        {
            foreach (var delta in deltas)
            {
                if (!state.TryGetValue(delta.Schema.TableName, out var snapshot))
                {
                    snapshot = new BackupTableSnapshot
                    {
                        Schema = CloneSchema(delta.Schema),
                        Rows = new List<Dictionary<string, string?>>()
                    };
                    state[delta.Schema.TableName] = snapshot;
                }
                else
                {
                    snapshot.Schema = CloneSchema(delta.Schema);
                }

                var rowMap = snapshot.Rows.ToDictionary(
                    row => BuildRowKey(snapshot.Schema.PrimaryKeyColumns, row),
                    CloneRow,
                    StringComparer.Ordinal);

                foreach (var deleteRow in delta.Deletes)
                {
                    rowMap.Remove(BuildRowKey(snapshot.Schema.PrimaryKeyColumns, deleteRow));
                }

                foreach (var upsertRow in delta.Upserts)
                {
                    rowMap[BuildRowKey(snapshot.Schema.PrimaryKeyColumns, upsertRow)] = CloneRow(upsertRow);
                }

                snapshot.Rows = rowMap
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => x.Value)
                    .ToList();
            }
        }

        private static Dictionary<string, BackupTableSnapshot> ToSnapshotMap(IEnumerable<BackupTableSnapshot> snapshots)
        {
            var map = new Dictionary<string, BackupTableSnapshot>(StringComparer.OrdinalIgnoreCase);

            foreach (var snapshot in snapshots)
            {
                if (snapshot?.Schema == null || string.IsNullOrWhiteSpace(snapshot.Schema.TableName))
                {
                    continue;
                }

                map[snapshot.Schema.TableName] = CloneSnapshot(snapshot);
            }

            return map;
        }

        private string BuildBackupFilePath(string databaseName, DatabaseBackupType backupType, DateTime createdAtUtc)
        {
            var folder = backupType switch
            {
                DatabaseBackupType.Full => "full",
                DatabaseBackupType.Differential => "differential",
                _ => "incremental"
            };

            return Path.Combine(
                _backupsRoot,
                folder,
                $"{SanitizeFileName(databaseName)}_{backupType.ToString().ToLowerInvariant()}_{createdAtUtc:yyyyMMdd_HHmmss}.json");
        }

        private string ResolveFilePath(string relativeOrAbsolutePath)
        {
            return Path.IsPathRooted(relativeOrAbsolutePath)
                ? relativeOrAbsolutePath
                : Path.Combine(_backupsRoot, relativeOrAbsolutePath);
        }

        private static List<DatabaseBackupCatalogEntry> ResolveDeleteEntries(DatabaseBackupMetadata metadata, string backupId)
        {
            var entries = new List<DatabaseBackupCatalogEntry>();
            var queue = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            queue.Enqueue(backupId);
            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                if (!visited.Add(currentId))
                {
                    continue;
                }

                var currentEntry = metadata.Backups.FirstOrDefault(x =>
                    string.Equals(x.BackupId, currentId, StringComparison.OrdinalIgnoreCase));
                if (currentEntry == null)
                {
                    continue;
                }

                entries.Add(currentEntry);

                foreach (var child in metadata.Backups.Where(x =>
                             !string.IsNullOrWhiteSpace(x.ParentBackupId) &&
                             string.Equals(x.ParentBackupId, currentId, StringComparison.OrdinalIgnoreCase)))
                {
                    queue.Enqueue(child.BackupId);
                }
            }

            return entries;
        }

        private static DatabaseBackupCatalogEntry? GetLatestBackup(DatabaseBackupMetadata metadata, DatabaseBackupType? backupType)
        {
            return metadata.Backups
                .Where(x => backupType == null || string.Equals(x.BackupType, backupType.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => ParseUtc(x.CreatedAtUtc))
                .LastOrDefault();
        }

        private static DatabaseBackupType ParseBackupType(string value)
        {
            return Enum.TryParse<DatabaseBackupType>(value, true, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"Unsupported backup type '{value}'.");
        }

        private static DateTime ParseUtc(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DateTime.MinValue
                : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private void TryDeleteEmptyParentDirectories(string? startingDirectory)
        {
            if (string.IsNullOrWhiteSpace(startingDirectory))
            {
                return;
            }

            var current = startingDirectory;
            while (!string.IsNullOrWhiteSpace(current) &&
                   current.StartsWith(_backupsRoot, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(current, _backupsRoot, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (Directory.Exists(current) &&
                        !Directory.EnumerateFileSystemEntries(current).Any())
                    {
                        Directory.Delete(current, recursive: false);
                        current = Path.GetDirectoryName(current);
                        continue;
                    }
                }
                catch
                {
                }

                break;
            }
        }

        private static bool SchemasEquivalent(BackupTableSchema left, BackupTableSchema right)
        {
            if (left.Columns.Count != right.Columns.Count || left.PrimaryKeyColumns.Count != right.PrimaryKeyColumns.Count)
            {
                return false;
            }

            for (var i = 0; i < left.PrimaryKeyColumns.Count; i++)
            {
                if (!string.Equals(left.PrimaryKeyColumns[i], right.PrimaryKeyColumns[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            for (var i = 0; i < left.Columns.Count; i++)
            {
                var leftColumn = left.Columns[i];
                var rightColumn = right.Columns[i];
                if (!string.Equals(leftColumn.Name, rightColumn.Name, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(leftColumn.DataType, rightColumn.DataType, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(leftColumn.ColumnType, rightColumn.ColumnType, StringComparison.OrdinalIgnoreCase) ||
                    leftColumn.IsNullable != rightColumn.IsNullable)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool RowsEqual(BackupTableSchema schema, IReadOnlyDictionary<string, string?> left, IReadOnlyDictionary<string, string?> right)
        {
            foreach (var column in schema.Columns.OrderBy(x => x.OrdinalPosition))
            {
                left.TryGetValue(column.Name, out var leftValue);
                right.TryGetValue(column.Name, out var rightValue);
                if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildRowKey(IReadOnlyList<string> keyColumns, IReadOnlyDictionary<string, string?> row)
        {
            return string.Join("|", keyColumns.Select(column =>
            {
                row.TryGetValue(column, out var value);
                return $"{column}={value ?? "<null>"}";
            }));
        }

        private static Dictionary<string, string?> ExtractKeyRow(BackupTableSchema schema, IReadOnlyDictionary<string, string?> row)
        {
            var keyRow = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var keyColumn in schema.PrimaryKeyColumns)
            {
                row.TryGetValue(keyColumn, out var value);
                keyRow[keyColumn] = value;
            }
            return keyRow;
        }

        private static Dictionary<string, BackupTableSnapshot> CloneState(IReadOnlyDictionary<string, BackupTableSnapshot> source)
        {
            return source.ToDictionary(x => x.Key, x => CloneSnapshot(x.Value), StringComparer.OrdinalIgnoreCase);
        }

        private static BackupTableSnapshot CloneSnapshot(BackupTableSnapshot source)
        {
            return new BackupTableSnapshot
            {
                Schema = CloneSchema(source.Schema),
                Rows = source.Rows.Select(CloneRow).ToList()
            };
        }

        private static BackupTableSchema CloneSchema(BackupTableSchema source)
        {
            return new BackupTableSchema
            {
                TableName = source.TableName,
                PrimaryKeyColumns = source.PrimaryKeyColumns.ToList(),
                Columns = source.Columns
                    .OrderBy(x => x.OrdinalPosition)
                    .Select(x => new BackupColumnDefinition
                    {
                        Name = x.Name,
                        DataType = x.DataType,
                        ColumnType = x.ColumnType,
                        IsNullable = x.IsNullable,
                        OrdinalPosition = x.OrdinalPosition
                    })
                    .ToList()
            };
        }

        private static Dictionary<string, string?> CloneRow(IReadOnlyDictionary<string, string?> row)
        {
            return row.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }

        private static string QuoteIdentifier(string identifier) => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        }

        private static bool IsBooleanColumn(BackupColumnDefinition column)
        {
            return string.Equals(column.DataType, "bit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.ColumnType, "tinyint(1)", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.ColumnType, "bit(1)", StringComparison.OrdinalIgnoreCase);
        }

        private static string? SerializeValue(BackupColumnDefinition column, object value)
        {
            if (value == DBNull.Value || value == null)
            {
                return null;
            }

            if (IsBooleanColumn(column))
            {
                return ToBoolean(value) ? "1" : "0";
            }

            return column.DataType.ToLowerInvariant() switch
            {
                "date" => value is DateTime dateOnly
                    ? dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "datetime" => value is DateTime dateTime
                    ? dateTime.ToString("O", CultureInfo.InvariantCulture)
                    : Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToString("O", CultureInfo.InvariantCulture),
                "timestamp" => value is DateTime timestamp
                    ? timestamp.ToString("O", CultureInfo.InvariantCulture)
                    : Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToString("O", CultureInfo.InvariantCulture),
                "time" => value switch
                {
                    TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
                    DateTime timeAsDateTime => timeAsDateTime.TimeOfDay.ToString("c", CultureInfo.InvariantCulture),
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture)
                },
                "binary" or "varbinary" or "tinyblob" or "blob" or "mediumblob" or "longblob"
                    => value is byte[] bytes ? Convert.ToBase64String(bytes) : Convert.ToString(value, CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            };
        }

        private static object DeserializeValue(BackupColumnDefinition column, string? serialized)
        {
            if (serialized == null)
            {
                return DBNull.Value;
            }

            if (IsBooleanColumn(column))
            {
                return serialized == "1" || serialized.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            return column.DataType.ToLowerInvariant() switch
            {
                "tinyint" => byte.TryParse(serialized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tiny)
                    ? tiny
                    : Convert.ToInt16(serialized, CultureInfo.InvariantCulture),
                "smallint" => Convert.ToInt16(serialized, CultureInfo.InvariantCulture),
                "mediumint" or "int" or "integer" => Convert.ToInt32(serialized, CultureInfo.InvariantCulture),
                "bigint" => Convert.ToInt64(serialized, CultureInfo.InvariantCulture),
                "decimal" or "numeric" => Convert.ToDecimal(serialized, CultureInfo.InvariantCulture),
                "float" => Convert.ToSingle(serialized, CultureInfo.InvariantCulture),
                "double" or "real" => Convert.ToDouble(serialized, CultureInfo.InvariantCulture),
                "date" or "datetime" or "timestamp" => DateTime.Parse(serialized, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                "time" => TimeSpan.Parse(serialized, CultureInfo.InvariantCulture),
                "binary" or "varbinary" or "tinyblob" or "blob" or "mediumblob" or "longblob"
                    => Convert.FromBase64String(serialized),
                _ => serialized
            };
        }

        private static bool ToBoolean(object value)
        {
            return value switch
            {
                bool b => b,
                sbyte sb => sb != 0,
                byte bt => bt != 0,
                short s => s != 0,
                ushort us => us != 0,
                int i => i != 0,
                uint ui => ui != 0,
                long l => l != 0,
                ulong ul => ul != 0,
                string text when bool.TryParse(text, out var parsed) => parsed,
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt) => parsedInt != 0,
                _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
            };
        }
    }
}
