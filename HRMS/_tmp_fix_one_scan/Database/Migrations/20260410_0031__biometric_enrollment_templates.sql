-- Store fingerprint templates for real biometric enrollment and matching.

SET @has_template_data := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'biometric_enrollments'
    AND COLUMN_NAME = 'template_data'
);

SET @sql_template_data := IF(
  @has_template_data = 0,
  'ALTER TABLE biometric_enrollments ADD COLUMN template_data LONGBLOB NULL AFTER status;',
  'SELECT 1;'
);
PREPARE stmt_template_data FROM @sql_template_data;
EXECUTE stmt_template_data;
DEALLOCATE PREPARE stmt_template_data;

SET @has_template_format := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'biometric_enrollments'
    AND COLUMN_NAME = 'template_format'
);

SET @sql_template_format := IF(
  @has_template_format = 0,
  'ALTER TABLE biometric_enrollments ADD COLUMN template_format VARCHAR(80) NULL AFTER template_data;',
  'SELECT 1;'
);
PREPARE stmt_template_format FROM @sql_template_format;
EXECUTE stmt_template_format;
DEALLOCATE PREPARE stmt_template_format;

SET @has_template_encoding := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'biometric_enrollments'
    AND COLUMN_NAME = 'template_encoding'
);

SET @sql_template_encoding := IF(
  @has_template_encoding = 0,
  'ALTER TABLE biometric_enrollments ADD COLUMN template_encoding VARCHAR(20) NULL AFTER template_format;',
  'SELECT 1;'
);
PREPARE stmt_template_encoding FROM @sql_template_encoding;
EXECUTE stmt_template_encoding;
DEALLOCATE PREPARE stmt_template_encoding;

SET @has_template_quality := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'biometric_enrollments'
    AND COLUMN_NAME = 'template_quality'
);

SET @sql_template_quality := IF(
  @has_template_quality = 0,
  'ALTER TABLE biometric_enrollments ADD COLUMN template_quality INT NULL AFTER template_encoding;',
  'SELECT 1;'
);
PREPARE stmt_template_quality FROM @sql_template_quality;
EXECUTE stmt_template_quality;
DEALLOCATE PREPARE stmt_template_quality;

SET @has_template_updated_at := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'biometric_enrollments'
    AND COLUMN_NAME = 'template_updated_at'
);

SET @sql_template_updated_at := IF(
  @has_template_updated_at = 0,
  'ALTER TABLE biometric_enrollments ADD COLUMN template_updated_at DATETIME NULL AFTER template_quality;',
  'SELECT 1;'
);
PREPARE stmt_template_updated_at FROM @sql_template_updated_at;
EXECUTE stmt_template_updated_at;
DEALLOCATE PREPARE stmt_template_updated_at;
