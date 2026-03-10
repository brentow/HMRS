-- Clean up duplicate employee-account links before enforcing one linked account per employee.
UPDATE user_accounts ua
JOIN (
  SELECT employee_id, MIN(user_id) AS keep_user_id
  FROM user_accounts
  WHERE employee_id IS NOT NULL
  GROUP BY employee_id
  HAVING COUNT(*) > 1
) dup ON dup.employee_id = ua.employee_id
SET ua.employee_id = NULL
WHERE ua.user_id <> dup.keep_user_id;

SET @has_uq_user_accounts_employee_id := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'user_accounts'
    AND INDEX_NAME = 'uq_user_accounts_employee_id'
);

SET @sql_uq_user_accounts_employee_id := IF(
  @has_uq_user_accounts_employee_id = 0,
  'ALTER TABLE user_accounts ADD UNIQUE KEY uq_user_accounts_employee_id (employee_id)',
  'SELECT 1'
);

PREPARE stmt_uq_user_accounts_employee_id FROM @sql_uq_user_accounts_employee_id;
EXECUTE stmt_uq_user_accounts_employee_id;
DEALLOCATE PREPARE stmt_uq_user_accounts_employee_id;

-- Repair rows that would fail business-rule CHECK constraints.
UPDATE leave_applications
SET date_to = date_from
WHERE date_to < date_from;

UPDATE leave_applications
SET days_requested = 0
WHERE days_requested < 0;

UPDATE payroll_periods
SET date_to = date_from
WHERE date_to < date_from;

UPDATE payroll_periods
SET pay_date = date_from
WHERE pay_date < date_from;

UPDATE job_postings
SET close_date = open_date
WHERE close_date IS NOT NULL
  AND close_date < open_date;

UPDATE job_postings
SET vacancies = 1
WHERE vacancies IS NULL
   OR vacancies <= 0;

UPDATE job_postings
SET salary_range_max = salary_range_min
WHERE salary_range_min IS NOT NULL
  AND salary_range_max IS NOT NULL
  AND salary_range_max < salary_range_min;

UPDATE performance_cycles
SET end_date = start_date
WHERE end_date < start_date;

SET @has_chk_leave_app_dates := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'leave_applications'
    AND CONSTRAINT_NAME = 'chk_leave_applications_date_range'
    AND CONSTRAINT_TYPE = 'CHECK'
);

SET @sql_chk_leave_app_dates := IF(
  @has_chk_leave_app_dates = 0,
  'ALTER TABLE leave_applications ADD CONSTRAINT chk_leave_applications_date_range CHECK (date_to >= date_from)',
  'SELECT 1'
);

PREPARE stmt_chk_leave_app_dates FROM @sql_chk_leave_app_dates;
EXECUTE stmt_chk_leave_app_dates;
DEALLOCATE PREPARE stmt_chk_leave_app_dates;

SET @has_chk_leave_app_days := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'leave_applications'
    AND CONSTRAINT_NAME = 'chk_leave_applications_days_requested'
    AND CONSTRAINT_TYPE = 'CHECK'
);

SET @sql_chk_leave_app_days := IF(
  @has_chk_leave_app_days = 0,
  'ALTER TABLE leave_applications ADD CONSTRAINT chk_leave_applications_days_requested CHECK (days_requested >= 0)',
  'SELECT 1'
);

PREPARE stmt_chk_leave_app_days FROM @sql_chk_leave_app_days;
EXECUTE stmt_chk_leave_app_days;
DEALLOCATE PREPARE stmt_chk_leave_app_days;

SET @has_chk_payroll_period_dates := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'payroll_periods'
    AND CONSTRAINT_NAME = 'chk_payroll_periods_date_range'
    AND CONSTRAINT_TYPE = 'CHECK'
);

SET @sql_chk_payroll_period_dates := IF(
  @has_chk_payroll_period_dates = 0,
  'ALTER TABLE payroll_periods ADD CONSTRAINT chk_payroll_periods_date_range CHECK (date_to >= date_from)',
  'SELECT 1'
);

PREPARE stmt_chk_payroll_period_dates FROM @sql_chk_payroll_period_dates;
EXECUTE stmt_chk_payroll_period_dates;
DEALLOCATE PREPARE stmt_chk_payroll_period_dates;

SET @has_chk_payroll_period_pay_date := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'payroll_periods'
    AND CONSTRAINT_NAME = 'chk_payroll_periods_pay_date'
    AND CONSTRAINT_TYPE = 'CHECK'
);

SET @sql_chk_payroll_period_pay_date := IF(
  @has_chk_payroll_period_pay_date = 0,
  'ALTER TABLE payroll_periods ADD CONSTRAINT chk_payroll_periods_pay_date CHECK (pay_date >= date_from)',
  'SELECT 1'
);

PREPARE stmt_chk_payroll_period_pay_date FROM @sql_chk_payroll_period_pay_date;
EXECUTE stmt_chk_payroll_period_pay_date;
DEALLOCATE PREPARE stmt_chk_payroll_period_pay_date;

SET @has_chk_job_post_dates := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'job_postings'
    AND CONSTRAINT_NAME = 'chk_job_postings_close_date'
    AND CONSTRAINT_TYPE = 'CHECK'
);

SET @sql_chk_job_post_dates := IF(
  @has_chk_job_post_dates = 0,
  'ALTER TABLE job_postings ADD CONSTRAINT chk_job_postings_close_date CHECK (close_date IS NULL OR close_date >= open_date)',
  'SELECT 1'
);

PREPARE stmt_chk_job_post_dates FROM @sql_chk_job_post_dates;
EXECUTE stmt_chk_job_post_dates;
DEALLOCATE PREPARE stmt_chk_job_post_dates;

SET @has_chk_job_post_vacancies := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'job_postings'
    AND CONSTRAINT_NAME = 'chk_job_postings_vacancies'
    AND CONSTRAINT_TYPE = 'CHECK'
);

SET @sql_chk_job_post_vacancies := IF(
  @has_chk_job_post_vacancies = 0,
  'ALTER TABLE job_postings ADD CONSTRAINT chk_job_postings_vacancies CHECK (vacancies > 0)',
  'SELECT 1'
);

PREPARE stmt_chk_job_post_vacancies FROM @sql_chk_job_post_vacancies;
EXECUTE stmt_chk_job_post_vacancies;
DEALLOCATE PREPARE stmt_chk_job_post_vacancies;

SET @has_chk_job_post_salary := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'job_postings'
    AND CONSTRAINT_NAME = 'chk_job_postings_salary_range'
    AND CONSTRAINT_TYPE = 'CHECK'
);

SET @sql_chk_job_post_salary := IF(
  @has_chk_job_post_salary = 0,
  'ALTER TABLE job_postings ADD CONSTRAINT chk_job_postings_salary_range CHECK (salary_range_min IS NULL OR salary_range_max IS NULL OR salary_range_max >= salary_range_min)',
  'SELECT 1'
);

PREPARE stmt_chk_job_post_salary FROM @sql_chk_job_post_salary;
EXECUTE stmt_chk_job_post_salary;
DEALLOCATE PREPARE stmt_chk_job_post_salary;

SET @has_chk_perf_cycle_dates := (
  SELECT COUNT(1)
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'performance_cycles'
    AND CONSTRAINT_NAME = 'chk_performance_cycles_date_range'
    AND CONSTRAINT_TYPE = 'CHECK'
);

SET @sql_chk_perf_cycle_dates := IF(
  @has_chk_perf_cycle_dates = 0,
  'ALTER TABLE performance_cycles ADD CONSTRAINT chk_performance_cycles_date_range CHECK (end_date >= start_date)',
  'SELECT 1'
);

PREPARE stmt_chk_perf_cycle_dates FROM @sql_chk_perf_cycle_dates;
EXECUTE stmt_chk_perf_cycle_dates;
DEALLOCATE PREPARE stmt_chk_perf_cycle_dates;
