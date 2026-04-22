-- Migration 0028: add employee-uploaded file storage to document checklist rows.
-- Uses information_schema checks for compatibility with older local MySQL versions.

SET @has_file_name := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'employee_document_checklist'
    AND COLUMN_NAME = 'file_name'
);

SET @sql_file_name := IF(
  @has_file_name = 0,
  'ALTER TABLE employee_document_checklist ADD COLUMN file_name VARCHAR(255) NULL AFTER remarks',
  'SELECT 1'
);
PREPARE stmt_doc_checklist_file_name FROM @sql_file_name;
EXECUTE stmt_doc_checklist_file_name;
DEALLOCATE PREPARE stmt_doc_checklist_file_name;

SET @has_file_path := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'employee_document_checklist'
    AND COLUMN_NAME = 'file_path'
);

SET @sql_file_path := IF(
  @has_file_path = 0,
  'ALTER TABLE employee_document_checklist ADD COLUMN file_path VARCHAR(500) NULL AFTER file_name',
  'SELECT 1'
);
PREPARE stmt_doc_checklist_file_path FROM @sql_file_path;
EXECUTE stmt_doc_checklist_file_path;
DEALLOCATE PREPARE stmt_doc_checklist_file_path;

SET @has_file_blob := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'employee_document_checklist'
    AND COLUMN_NAME = 'file_blob'
);

SET @sql_file_blob := IF(
  @has_file_blob = 0,
  'ALTER TABLE employee_document_checklist ADD COLUMN file_blob LONGBLOB NULL AFTER file_path',
  'SELECT 1'
);
PREPARE stmt_doc_checklist_file_blob FROM @sql_file_blob;
EXECUTE stmt_doc_checklist_file_blob;
DEALLOCATE PREPARE stmt_doc_checklist_file_blob;

SET @has_file_size := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'employee_document_checklist'
    AND COLUMN_NAME = 'file_size'
);

SET @sql_file_size := IF(
  @has_file_size = 0,
  'ALTER TABLE employee_document_checklist ADD COLUMN file_size BIGINT NULL AFTER file_blob',
  'SELECT 1'
);
PREPARE stmt_doc_checklist_file_size FROM @sql_file_size;
EXECUTE stmt_doc_checklist_file_size;
DEALLOCATE PREPARE stmt_doc_checklist_file_size;

SET @has_uploaded_at := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'employee_document_checklist'
    AND COLUMN_NAME = 'uploaded_at'
);

SET @sql_uploaded_at := IF(
  @has_uploaded_at = 0,
  'ALTER TABLE employee_document_checklist ADD COLUMN uploaded_at DATETIME NULL AFTER file_size',
  'SELECT 1'
);
PREPARE stmt_doc_checklist_uploaded_at FROM @sql_uploaded_at;
EXECUTE stmt_doc_checklist_uploaded_at;
DEALLOCATE PREPARE stmt_doc_checklist_uploaded_at;

SET @has_uploaded_by_employee_id := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'employee_document_checklist'
    AND COLUMN_NAME = 'uploaded_by_employee_id'
);

SET @sql_uploaded_by_employee_id := IF(
  @has_uploaded_by_employee_id = 0,
  'ALTER TABLE employee_document_checklist ADD COLUMN uploaded_by_employee_id INT NULL AFTER uploaded_at',
  'SELECT 1'
);
PREPARE stmt_doc_checklist_uploaded_by_employee_id FROM @sql_uploaded_by_employee_id;
EXECUTE stmt_doc_checklist_uploaded_by_employee_id;
DEALLOCATE PREPARE stmt_doc_checklist_uploaded_by_employee_id;

SET @has_idx_doc_checklist_uploaded_at := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'employee_document_checklist'
    AND INDEX_NAME = 'idx_doc_checklist_uploaded_at'
);

SET @sql_idx_doc_checklist_uploaded_at := IF(
  @has_idx_doc_checklist_uploaded_at = 0,
  'ALTER TABLE employee_document_checklist ADD INDEX idx_doc_checklist_uploaded_at (uploaded_at)',
  'SELECT 1'
);
PREPARE stmt_idx_doc_checklist_uploaded_at FROM @sql_idx_doc_checklist_uploaded_at;
EXECUTE stmt_idx_doc_checklist_uploaded_at;
DEALLOCATE PREPARE stmt_idx_doc_checklist_uploaded_at;
