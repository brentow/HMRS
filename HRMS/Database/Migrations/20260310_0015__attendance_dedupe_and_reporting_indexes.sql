-- Remove exact duplicate attendance punches before enforcing uniqueness.
DELETE al_old
FROM attendance_logs al_old
JOIN attendance_logs al_newer
  ON al_old.employee_id = al_newer.employee_id
 AND al_old.log_time = al_newer.log_time
 AND al_old.log_type = al_newer.log_type
 AND al_old.source = al_newer.source
 AND al_old.log_id < al_newer.log_id;

SET @has_uq_attendance_logs_emp_time_type_source := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'attendance_logs'
    AND INDEX_NAME = 'uq_attendance_logs_emp_time_type_source'
);

SET @sql_uq_attendance_logs_emp_time_type_source := IF(
  @has_uq_attendance_logs_emp_time_type_source = 0,
  'ALTER TABLE attendance_logs ADD UNIQUE KEY uq_attendance_logs_emp_time_type_source (employee_id, log_time, log_type, source)',
  'SELECT 1'
);

PREPARE stmt_uq_attendance_logs_emp_time_type_source FROM @sql_uq_attendance_logs_emp_time_type_source;
EXECUTE stmt_uq_attendance_logs_emp_time_type_source;
DEALLOCATE PREPARE stmt_uq_attendance_logs_emp_time_type_source;

SET @has_idx_attendance_logs_time_type := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'attendance_logs'
    AND INDEX_NAME = 'idx_attendance_logs_time_type'
);

SET @sql_idx_attendance_logs_time_type := IF(
  @has_idx_attendance_logs_time_type = 0,
  'ALTER TABLE attendance_logs ADD KEY idx_attendance_logs_time_type (log_time, log_type)',
  'SELECT 1'
);

PREPARE stmt_idx_attendance_logs_time_type FROM @sql_idx_attendance_logs_time_type;
EXECUTE stmt_idx_attendance_logs_time_type;
DEALLOCATE PREPARE stmt_idx_attendance_logs_time_type;

SET @has_idx_attendance_adjustments_status_requested := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'attendance_adjustments'
    AND INDEX_NAME = 'idx_attendance_adjustments_status_requested'
);

SET @sql_idx_attendance_adjustments_status_requested := IF(
  @has_idx_attendance_adjustments_status_requested = 0,
  'ALTER TABLE attendance_adjustments ADD KEY idx_attendance_adjustments_status_requested (status, requested_at)',
  'SELECT 1'
);

PREPARE stmt_idx_attendance_adjustments_status_requested FROM @sql_idx_attendance_adjustments_status_requested;
EXECUTE stmt_idx_attendance_adjustments_status_requested;
DEALLOCATE PREPARE stmt_idx_attendance_adjustments_status_requested;

SET @has_idx_attendance_adjustments_emp_status_requested := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'attendance_adjustments'
    AND INDEX_NAME = 'idx_attendance_adjustments_emp_status_requested'
);

SET @sql_idx_attendance_adjustments_emp_status_requested := IF(
  @has_idx_attendance_adjustments_emp_status_requested = 0,
  'ALTER TABLE attendance_adjustments ADD KEY idx_attendance_adjustments_emp_status_requested (employee_id, status, requested_at)',
  'SELECT 1'
);

PREPARE stmt_idx_attendance_adjustments_emp_status_requested FROM @sql_idx_attendance_adjustments_emp_status_requested;
EXECUTE stmt_idx_attendance_adjustments_emp_status_requested;
DEALLOCATE PREPARE stmt_idx_attendance_adjustments_emp_status_requested;

SET @has_idx_leave_applications_status_filed := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'leave_applications'
    AND INDEX_NAME = 'idx_leave_applications_status_filed'
);

SET @sql_idx_leave_applications_status_filed := IF(
  @has_idx_leave_applications_status_filed = 0,
  'ALTER TABLE leave_applications ADD KEY idx_leave_applications_status_filed (status, filed_at)',
  'SELECT 1'
);

PREPARE stmt_idx_leave_applications_status_filed FROM @sql_idx_leave_applications_status_filed;
EXECUTE stmt_idx_leave_applications_status_filed;
DEALLOCATE PREPARE stmt_idx_leave_applications_status_filed;

SET @has_idx_leave_applications_emp_status_filed := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'leave_applications'
    AND INDEX_NAME = 'idx_leave_applications_emp_status_filed'
);

SET @sql_idx_leave_applications_emp_status_filed := IF(
  @has_idx_leave_applications_emp_status_filed = 0,
  'ALTER TABLE leave_applications ADD KEY idx_leave_applications_emp_status_filed (employee_id, status, filed_at)',
  'SELECT 1'
);

PREPARE stmt_idx_leave_applications_emp_status_filed FROM @sql_idx_leave_applications_emp_status_filed;
EXECUTE stmt_idx_leave_applications_emp_status_filed;
DEALLOCATE PREPARE stmt_idx_leave_applications_emp_status_filed;

SET @has_idx_payslip_releases_released_at := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'payslip_releases'
    AND INDEX_NAME = 'idx_payslip_releases_released_at'
);

SET @sql_idx_payslip_releases_released_at := IF(
  @has_idx_payslip_releases_released_at = 0,
  'ALTER TABLE payslip_releases ADD KEY idx_payslip_releases_released_at (released_at)',
  'SELECT 1'
);

PREPARE stmt_idx_payslip_releases_released_at FROM @sql_idx_payslip_releases_released_at;
EXECUTE stmt_idx_payslip_releases_released_at;
DEALLOCATE PREPARE stmt_idx_payslip_releases_released_at;

SET @has_idx_payslip_releases_by_released := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'payslip_releases'
    AND INDEX_NAME = 'idx_payslip_releases_by_released'
);

SET @sql_idx_payslip_releases_by_released := IF(
  @has_idx_payslip_releases_by_released = 0,
  'ALTER TABLE payslip_releases ADD KEY idx_payslip_releases_by_released (released_by_employee_id, released_at)',
  'SELECT 1'
);

PREPARE stmt_idx_payslip_releases_by_released FROM @sql_idx_payslip_releases_by_released;
EXECUTE stmt_idx_payslip_releases_by_released;
DEALLOCATE PREPARE stmt_idx_payslip_releases_by_released;

SET @has_idx_training_enrollments_emp_status_created := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'training_enrollments'
    AND INDEX_NAME = 'idx_training_enrollments_emp_status_created'
);

SET @sql_idx_training_enrollments_emp_status_created := IF(
  @has_idx_training_enrollments_emp_status_created = 0,
  'ALTER TABLE training_enrollments ADD KEY idx_training_enrollments_emp_status_created (employee_id, status, created_at)',
  'SELECT 1'
);

PREPARE stmt_idx_training_enrollments_emp_status_created FROM @sql_idx_training_enrollments_emp_status_created;
EXECUTE stmt_idx_training_enrollments_emp_status_created;
DEALLOCATE PREPARE stmt_idx_training_enrollments_emp_status_created;
