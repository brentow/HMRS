SET @has_audit_logs_ip_address := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'audit_logs'
    AND COLUMN_NAME = 'ip_address'
);

SET @sql_audit_logs_ip_address := IF(
  @has_audit_logs_ip_address = 0,
  'ALTER TABLE audit_logs ADD COLUMN ip_address VARCHAR(45) NULL AFTER acted_by_user_id',
  'SELECT 1'
);

PREPARE stmt_audit_logs_ip_address FROM @sql_audit_logs_ip_address;
EXECUTE stmt_audit_logs_ip_address;
DEALLOCATE PREPARE stmt_audit_logs_ip_address;

SET @has_audit_logs_client_name := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'audit_logs'
    AND COLUMN_NAME = 'client_name'
);

SET @sql_audit_logs_client_name := IF(
  @has_audit_logs_client_name = 0,
  'ALTER TABLE audit_logs ADD COLUMN client_name VARCHAR(120) NULL AFTER ip_address',
  'SELECT 1'
);

PREPARE stmt_audit_logs_client_name FROM @sql_audit_logs_client_name;
EXECUTE stmt_audit_logs_client_name;
DEALLOCATE PREPARE stmt_audit_logs_client_name;

SET @has_audit_logs_session_id := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'audit_logs'
    AND COLUMN_NAME = 'session_id'
);

SET @sql_audit_logs_session_id := IF(
  @has_audit_logs_session_id = 0,
  'ALTER TABLE audit_logs ADD COLUMN session_id VARCHAR(120) NULL AFTER client_name',
  'SELECT 1'
);

PREPARE stmt_audit_logs_session_id FROM @sql_audit_logs_session_id;
EXECUTE stmt_audit_logs_session_id;
DEALLOCATE PREPARE stmt_audit_logs_session_id;

SET @has_audit_logs_old_values_json := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'audit_logs'
    AND COLUMN_NAME = 'old_values_json'
);

SET @sql_audit_logs_old_values_json := IF(
  @has_audit_logs_old_values_json = 0,
  'ALTER TABLE audit_logs ADD COLUMN old_values_json JSON NULL AFTER details',
  'SELECT 1'
);

PREPARE stmt_audit_logs_old_values_json FROM @sql_audit_logs_old_values_json;
EXECUTE stmt_audit_logs_old_values_json;
DEALLOCATE PREPARE stmt_audit_logs_old_values_json;

SET @has_audit_logs_new_values_json := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'audit_logs'
    AND COLUMN_NAME = 'new_values_json'
);

SET @sql_audit_logs_new_values_json := IF(
  @has_audit_logs_new_values_json = 0,
  'ALTER TABLE audit_logs ADD COLUMN new_values_json JSON NULL AFTER old_values_json',
  'SELECT 1'
);

PREPARE stmt_audit_logs_new_values_json FROM @sql_audit_logs_new_values_json;
EXECUTE stmt_audit_logs_new_values_json;
DEALLOCATE PREPARE stmt_audit_logs_new_values_json;
