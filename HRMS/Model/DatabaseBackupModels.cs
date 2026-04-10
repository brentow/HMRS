using System.Collections.Generic;

namespace HRMS.Model
{
    public enum DatabaseBackupType
    {
        Full,
        Differential,
        Incremental
    }

    public sealed record DatabaseBackupResult(
        bool Created,
        DatabaseBackupType BackupType,
        string? FilePath,
        int TableCount,
        int RowCount,
        int ChangeCount);

    public sealed record DatabaseRestoreResult(
        DatabaseBackupType BackupType,
        string FilePath,
        int TableCount,
        int RowCount);

    public sealed record DatabaseBackupDeleteResult(
        string BackupId,
        int DeletedEntries,
        int DeletedFiles,
        int MissingFiles);

    public sealed class DatabaseBackupCatalogEntry
    {
        public string BackupId { get; set; } = string.Empty;
        public string BackupType { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string? BaseFullBackupId { get; set; }
        public string? ParentBackupId { get; set; }
    }

    public sealed class DatabaseBackupMetadata
    {
        public string? LastFull { get; set; }
        public string? LastDifferential { get; set; }
        public string? LastIncremental { get; set; }
        public string? LastBackupId { get; set; }
        public string? LastBackupType { get; set; }
        public List<DatabaseBackupCatalogEntry> Backups { get; set; } = new();
    }

    public sealed class DatabaseBackupFile
    {
        public int FormatVersion { get; set; } = 1;
        public string BackupId { get; set; } = string.Empty;
        public string BackupType { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string SourceHost { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string? BaseFullBackupId { get; set; }
        public string? ParentBackupId { get; set; }
        public List<BackupTableSnapshot> Tables { get; set; } = new();
        public List<BackupTableDelta> Deltas { get; set; } = new();
    }

    public sealed class BackupTableSnapshot
    {
        public BackupTableSchema Schema { get; set; } = new();
        public List<Dictionary<string, string?>> Rows { get; set; } = new();
    }

    public sealed class BackupTableDelta
    {
        public BackupTableSchema Schema { get; set; } = new();
        public List<Dictionary<string, string?>> Upserts { get; set; } = new();
        public List<Dictionary<string, string?>> Deletes { get; set; } = new();
    }

    public sealed class BackupTableSchema
    {
        public string TableName { get; set; } = string.Empty;
        public List<string> PrimaryKeyColumns { get; set; } = new();
        public List<BackupColumnDefinition> Columns { get; set; } = new();
    }

    public sealed class BackupColumnDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string ColumnType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public int OrdinalPosition { get; set; }
    }
}
