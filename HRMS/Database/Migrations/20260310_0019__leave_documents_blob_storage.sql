SET @has_file_blob := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'leave_documents'
    AND COLUMN_NAME = 'file_blob'
);

SET @sql_file_blob := IF(
  @has_file_blob = 0,
  'ALTER TABLE leave_documents ADD COLUMN file_blob LONGBLOB NULL AFTER file_path',
  'SELECT 1'
);
PREPARE stmt_file_blob FROM @sql_file_blob;
EXECUTE stmt_file_blob;
DEALLOCATE PREPARE stmt_file_blob;

SET @has_file_size := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'leave_documents'
    AND COLUMN_NAME = 'file_size'
);

SET @sql_file_size := IF(
  @has_file_size = 0,
  'ALTER TABLE leave_documents ADD COLUMN file_size BIGINT NULL AFTER file_blob',
  'SELECT 1'
);
PREPARE stmt_file_size FROM @sql_file_size;
EXECUTE stmt_file_size;
DEALLOCATE PREPARE stmt_file_size;
