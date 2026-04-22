-- Modules schema (Leave, Recruitment, Payroll, Performance)
-- Depends on core schema migration.

-- Detect referenced PK column types to avoid FK mismatch (INT/BIGINT, signed/unsigned).
SET @EMP_ID_TYPE := COALESCE(
  (
    SELECT COLUMN_TYPE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'employees'
      AND COLUMN_NAME = 'employee_id'
    LIMIT 1
  ),
  'INT'
);

SET @DEPT_ID_TYPE := COALESCE(
  (
    SELECT COLUMN_TYPE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'departments'
      AND COLUMN_NAME = 'department_id'
    LIMIT 1
  ),
  'INT'
);

SET @POS_ID_TYPE := COALESCE(
  (
    SELECT COLUMN_TYPE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'positions'
      AND COLUMN_NAME = 'position_id'
    LIMIT 1
  ),
  'INT'
);

-- =========================================================
-- LEAVE MODULE
-- =========================================================
SET @sql := '
CREATE TABLE IF NOT EXISTS leave_types (
  leave_type_id INT AUTO_INCREMENT PRIMARY KEY,
  code VARCHAR(20) NOT NULL UNIQUE,
  name VARCHAR(120) NOT NULL,
  is_paid TINYINT(1) NOT NULL DEFAULT 1,
  default_credits_per_year DECIMAL(6,2) NOT NULL DEFAULT 0.00,
  remarks VARCHAR(255) NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;';
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS leave_balances (
  leave_balance_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  employee_id ', @EMP_ID_TYPE, ' NOT NULL,
  leave_type_id INT NOT NULL,
  `year` SMALLINT NOT NULL,
  opening_credits DECIMAL(8,2) NOT NULL DEFAULT 0.00,
  earned DECIMAL(8,2) NOT NULL DEFAULT 0.00,
  used DECIMAL(8,2) NOT NULL DEFAULT 0.00,
  adjustments DECIMAL(8,2) NOT NULL DEFAULT 0.00,
  as_of_date DATE NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uq_leave_balance_emp_type_year (employee_id, leave_type_id, `year`),
  KEY idx_leave_balance_emp (employee_id),
  KEY idx_leave_balance_type (leave_type_id),
  CONSTRAINT fk_leave_balance_employee
    FOREIGN KEY (employee_id) REFERENCES employees(employee_id),
  CONSTRAINT fk_leave_balance_type
    FOREIGN KEY (leave_type_id) REFERENCES leave_types(leave_type_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS leave_applications (
  leave_application_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  employee_id ', @EMP_ID_TYPE, ' NOT NULL,
  leave_type_id INT NOT NULL,
  date_from DATE NOT NULL,
  date_to DATE NOT NULL,
  days_requested DECIMAL(6,2) NOT NULL DEFAULT 1.00,
  reason VARCHAR(500) NULL,
  status ENUM(''DRAFT'',''SUBMITTED'',''RECOMMENDED'',''APPROVED'',''REJECTED'',''CANCELLED'') NOT NULL DEFAULT ''SUBMITTED'',
  filed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  decision_at DATETIME NULL,
  recommended_by_employee_id ', @EMP_ID_TYPE, ' NULL,
  approved_by_employee_id ', @EMP_ID_TYPE, ' NULL,
  hr_certified_by_employee_id ', @EMP_ID_TYPE, ' NULL,
  decision_remarks VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_leave_app_emp (employee_id),
  KEY idx_leave_app_type (leave_type_id),
  KEY idx_leave_app_dates (date_from, date_to),
  KEY idx_leave_app_status (status),
  CONSTRAINT fk_leave_app_employee
    FOREIGN KEY (employee_id) REFERENCES employees(employee_id),
  CONSTRAINT fk_leave_app_type
    FOREIGN KEY (leave_type_id) REFERENCES leave_types(leave_type_id),
  CONSTRAINT fk_leave_app_recommended_by
    FOREIGN KEY (recommended_by_employee_id) REFERENCES employees(employee_id),
  CONSTRAINT fk_leave_app_approved_by
    FOREIGN KEY (approved_by_employee_id) REFERENCES employees(employee_id),
  CONSTRAINT fk_leave_app_hr_certified_by
    FOREIGN KEY (hr_certified_by_employee_id) REFERENCES employees(employee_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := '
CREATE TABLE IF NOT EXISTS leave_application_days (
  leave_application_day_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  leave_application_id BIGINT NOT NULL,
  leave_date DATE NOT NULL,
  day_fraction DECIMAL(4,2) NOT NULL DEFAULT 1.00,
  half_day_part ENUM(''AM'',''PM'') NULL,
  UNIQUE KEY uq_leave_app_day (leave_application_id, leave_date, half_day_part),
  KEY idx_leave_app_day_date (leave_date),
  CONSTRAINT fk_leave_app_day_app
    FOREIGN KEY (leave_application_id) REFERENCES leave_applications(leave_application_id)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;';
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS leave_documents (
  leave_document_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  leave_application_id BIGINT NOT NULL,
  file_name VARCHAR(255) NOT NULL,
  file_path VARCHAR(500) NOT NULL,
  uploaded_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  uploaded_by_employee_id ', @EMP_ID_TYPE, ' NULL,
  KEY idx_leave_docs_app (leave_application_id),
  CONSTRAINT fk_leave_docs_app
    FOREIGN KEY (leave_application_id) REFERENCES leave_applications(leave_application_id)
    ON DELETE CASCADE,
  CONSTRAINT fk_leave_docs_uploader
    FOREIGN KEY (uploaded_by_employee_id) REFERENCES employees(employee_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- =========================================================
-- RECRUITMENT MODULE
-- =========================================================
SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS job_postings (
  job_posting_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  posting_code VARCHAR(40) NOT NULL UNIQUE,
  title VARCHAR(150) NOT NULL,
  department_id ', @DEPT_ID_TYPE, ' NULL,
  position_id ', @POS_ID_TYPE, ' NULL,
  employment_type ENUM(''PLANTILLA'',''CASUAL'',''JOB_ORDER'',''CONTRACTUAL'',''TEMPORARY'') NOT NULL DEFAULT ''CASUAL'',
  vacancies INT NOT NULL DEFAULT 1,
  salary_grade VARCHAR(20) NULL,
  salary_range_min DECIMAL(12,2) NULL,
  salary_range_max DECIMAL(12,2) NULL,
  description TEXT NULL,
  requirements TEXT NULL,
  status ENUM(''DRAFT'',''OPEN'',''CLOSED'',''CANCELLED'') NOT NULL DEFAULT ''OPEN'',
  open_date DATE NOT NULL,
  close_date DATE NULL,
  created_by_employee_id ', @EMP_ID_TYPE, ' NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_job_post_dept (department_id),
  KEY idx_job_post_pos (position_id),
  KEY idx_job_post_status (status),
  CONSTRAINT fk_job_post_dept FOREIGN KEY (department_id) REFERENCES departments(department_id),
  CONSTRAINT fk_job_post_pos FOREIGN KEY (position_id) REFERENCES positions(position_id),
  CONSTRAINT fk_job_post_creator FOREIGN KEY (created_by_employee_id) REFERENCES employees(employee_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := '
CREATE TABLE IF NOT EXISTS applicants (
  applicant_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  applicant_no VARCHAR(40) NOT NULL UNIQUE,
  last_name VARCHAR(80) NOT NULL,
  first_name VARCHAR(80) NOT NULL,
  middle_name VARCHAR(80) NULL,
  email VARCHAR(160) NULL,
  mobile_no VARCHAR(30) NULL,
  address VARCHAR(255) NULL,
  birth_date DATE NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_applicant_name (last_name, first_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;';
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := '
CREATE TABLE IF NOT EXISTS job_applications (
  job_application_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  applicant_id BIGINT NOT NULL,
  job_posting_id BIGINT NOT NULL,
  applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  status ENUM(''SUBMITTED'',''SCREENING'',''SHORTLISTED'',''INTERVIEW'',''OFFERED'',''HIRED'',''REJECTED'',''WITHDRAWN'') NOT NULL DEFAULT ''SUBMITTED'',
  notes VARCHAR(500) NULL,
  KEY idx_job_app_status (status),
  UNIQUE KEY uq_job_app_unique (applicant_id, job_posting_id),
  CONSTRAINT fk_job_app_applicant FOREIGN KEY (applicant_id) REFERENCES applicants(applicant_id),
  CONSTRAINT fk_job_app_posting FOREIGN KEY (job_posting_id) REFERENCES job_postings(job_posting_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;';
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS interview_schedules (
  interview_schedule_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  job_application_id BIGINT NOT NULL,
  interview_datetime DATETIME NOT NULL,
  interview_type ENUM(''PHONE'',''ONLINE'',''ONSITE'') NOT NULL DEFAULT ''ONSITE'',
  location VARCHAR(255) NULL,
  interviewer_employee_id ', @EMP_ID_TYPE, ' NULL,
  status ENUM(''SCHEDULED'',''DONE'',''CANCELLED'',''NO_SHOW'') NOT NULL DEFAULT ''SCHEDULED'',
  remarks VARCHAR(500) NULL,
  KEY idx_interview_datetime (interview_datetime),
  CONSTRAINT fk_interview_app FOREIGN KEY (job_application_id) REFERENCES job_applications(job_application_id) ON DELETE CASCADE,
  CONSTRAINT fk_interview_interviewer FOREIGN KEY (interviewer_employee_id) REFERENCES employees(employee_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := '
CREATE TABLE IF NOT EXISTS job_offers (
  job_offer_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  job_application_id BIGINT NOT NULL,
  offered_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  offer_status ENUM(''PENDING'',''ACCEPTED'',''DECLINED'',''CANCELLED'') NOT NULL DEFAULT ''PENDING'',
  salary_offer DECIMAL(12,2) NULL,
  start_date DATE NULL,
  remarks VARCHAR(500) NULL,
  CONSTRAINT fk_job_offer_app FOREIGN KEY (job_application_id) REFERENCES job_applications(job_application_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;';
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- =========================================================
-- PAYROLL MODULE
-- =========================================================
SET @sql := '
CREATE TABLE IF NOT EXISTS payroll_periods (
  payroll_period_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  period_code VARCHAR(30) NOT NULL UNIQUE,
  date_from DATE NOT NULL,
  date_to DATE NOT NULL,
  pay_date DATE NOT NULL,
  status ENUM(''OPEN'',''LOCKED'',''POSTED'',''CANCELLED'') NOT NULL DEFAULT ''OPEN'',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_payroll_period_dates (date_from, date_to),
  KEY idx_payroll_period_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;';
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS payroll_runs (
  payroll_run_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  payroll_period_id BIGINT NOT NULL,
  employee_id ', @EMP_ID_TYPE, ' NOT NULL,
  basic_pay DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  allowances DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  overtime_pay DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  other_earnings DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  gross_pay DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  deductions_total DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  net_pay DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  status ENUM(''DRAFT'',''GENERATED'',''APPROVED'',''RELEASED'',''VOID'') NOT NULL DEFAULT ''GENERATED'',
  generated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_payroll_run_period_emp (payroll_period_id, employee_id),
  KEY idx_payroll_run_emp (employee_id),
  KEY idx_payroll_run_status (status),
  CONSTRAINT fk_payroll_run_period FOREIGN KEY (payroll_period_id) REFERENCES payroll_periods(payroll_period_id),
  CONSTRAINT fk_payroll_run_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := '
CREATE TABLE IF NOT EXISTS payroll_run_items (
  payroll_run_item_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  payroll_run_id BIGINT NOT NULL,
  item_type ENUM(''EARNING'',''DEDUCTION'') NOT NULL,
  code VARCHAR(30) NOT NULL,
  description VARCHAR(150) NULL,
  amount DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  KEY idx_payroll_item_run (payroll_run_id),
  KEY idx_payroll_item_type (item_type),
  CONSTRAINT fk_payroll_item_run FOREIGN KEY (payroll_run_id) REFERENCES payroll_runs(payroll_run_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;';
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS payslip_releases (
  payslip_release_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  payroll_run_id BIGINT NOT NULL,
  released_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  released_by_employee_id ', @EMP_ID_TYPE, ' NULL,
  remarks VARCHAR(255) NULL,
  CONSTRAINT fk_payslip_release_run FOREIGN KEY (payroll_run_id) REFERENCES payroll_runs(payroll_run_id) ON DELETE CASCADE,
  CONSTRAINT fk_payslip_release_by FOREIGN KEY (released_by_employee_id) REFERENCES employees(employee_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- =========================================================
-- PERFORMANCE MODULE
-- =========================================================
SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS performance_cycles (
  performance_cycle_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  cycle_code VARCHAR(40) NOT NULL UNIQUE,
  name VARCHAR(150) NOT NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  status ENUM(''DRAFT'',''OPEN'',''CLOSED'',''ARCHIVED'') NOT NULL DEFAULT ''OPEN'',
  created_by_employee_id ', @EMP_ID_TYPE, ' NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_perf_cycle_dates (start_date, end_date),
  KEY idx_perf_cycle_status (status),
  CONSTRAINT fk_perf_cycle_creator FOREIGN KEY (created_by_employee_id) REFERENCES employees(employee_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS performance_goals (
  performance_goal_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  performance_cycle_id BIGINT NOT NULL,
  employee_id ', @EMP_ID_TYPE, ' NOT NULL,
  title VARCHAR(180) NOT NULL,
  description TEXT NULL,
  weight DECIMAL(6,2) NOT NULL DEFAULT 0.00,
  target_metric VARCHAR(255) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_perf_goal_emp (employee_id),
  KEY idx_perf_goal_cycle (performance_cycle_id),
  CONSTRAINT fk_perf_goal_cycle FOREIGN KEY (performance_cycle_id) REFERENCES performance_cycles(performance_cycle_id) ON DELETE CASCADE,
  CONSTRAINT fk_perf_goal_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := CONCAT('
CREATE TABLE IF NOT EXISTS performance_reviews (
  performance_review_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  performance_cycle_id BIGINT NOT NULL,
  employee_id ', @EMP_ID_TYPE, ' NOT NULL,
  reviewer_employee_id ', @EMP_ID_TYPE, ' NOT NULL,
  overall_rating DECIMAL(6,2) NULL,
  status ENUM(''DRAFT'',''SUBMITTED'',''APPROVED'',''REJECTED'') NOT NULL DEFAULT ''DRAFT'',
  remarks VARCHAR(800) NULL,
  submitted_at DATETIME NULL,
  decided_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_perf_review_emp (employee_id),
  KEY idx_perf_review_reviewer (reviewer_employee_id),
  KEY idx_perf_review_cycle (performance_cycle_id),
  KEY idx_perf_review_status (status),
  CONSTRAINT fk_perf_review_cycle FOREIGN KEY (performance_cycle_id) REFERENCES performance_cycles(performance_cycle_id),
  CONSTRAINT fk_perf_review_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id),
  CONSTRAINT fk_perf_review_reviewer FOREIGN KEY (reviewer_employee_id) REFERENCES employees(employee_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := '
CREATE TABLE IF NOT EXISTS performance_review_items (
  performance_review_item_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  performance_review_id BIGINT NOT NULL,
  criteria VARCHAR(180) NOT NULL,
  weight DECIMAL(6,2) NOT NULL DEFAULT 0.00,
  score DECIMAL(6,2) NULL,
  comments VARCHAR(500) NULL,
  KEY idx_perf_review_item_review (performance_review_id),
  CONSTRAINT fk_perf_review_item_review
    FOREIGN KEY (performance_review_id) REFERENCES performance_reviews(performance_review_id)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;';
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
