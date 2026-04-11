-- Backfill compatibility: ensure attendance_adjustments.decision_remarks exists.

SET @has_column := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'attendance_adjustments'
    AND COLUMN_NAME = 'decision_remarks'
);

SET @sql := IF(
  @has_column = 0,
  'ALTER TABLE attendance_adjustments ADD COLUMN decision_remarks VARCHAR(500) NULL AFTER status;',
  'SELECT 1;'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
