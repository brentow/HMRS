-- Ensure shift assignments do not duplicate for the same employee on the same start date.
-- Keeps the latest assignment row (highest assignment_id), removes older duplicates,
-- then adds a unique key to enforce the rule moving forward.

DELETE sa_old
FROM shift_assignments sa_old
JOIN shift_assignments sa_newer
  ON sa_old.employee_id = sa_newer.employee_id
 AND sa_old.start_date = sa_newer.start_date
 AND sa_old.assignment_id < sa_newer.assignment_id;

SET @has_uq_shift_assignment_emp_start := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'shift_assignments'
    AND INDEX_NAME = 'uq_shift_assignments_emp_start'
);

SET @sql_uq_shift_assignment_emp_start := IF(
  @has_uq_shift_assignment_emp_start = 0,
  'ALTER TABLE shift_assignments ADD UNIQUE KEY uq_shift_assignments_emp_start (employee_id, start_date)',
  'SELECT 1'
);

PREPARE stmt_uq_shift_assignment_emp_start FROM @sql_uq_shift_assignment_emp_start;
EXECUTE stmt_uq_shift_assignment_emp_start;
DEALLOCATE PREPARE stmt_uq_shift_assignment_emp_start;
