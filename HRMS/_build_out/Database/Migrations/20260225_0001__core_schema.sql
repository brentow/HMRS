-- Core HRMS schema (RBAC, master data, employees, users, attendance, DTR, training)
-- Safe to run multiple times.

CREATE TABLE IF NOT EXISTS roles (
  role_id     INT AUTO_INCREMENT PRIMARY KEY,
  role_name   VARCHAR(50) NOT NULL UNIQUE,
  description VARCHAR(255) NULL,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS permissions (
  permission_id INT AUTO_INCREMENT PRIMARY KEY,
  perm_code     VARCHAR(100) NOT NULL UNIQUE,
  description   VARCHAR(255) NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS role_permissions (
  role_id       INT NOT NULL,
  permission_id INT NOT NULL,
  PRIMARY KEY (role_id, permission_id),
  CONSTRAINT fk_rp_role FOREIGN KEY (role_id) REFERENCES roles(role_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_rp_perm FOREIGN KEY (permission_id) REFERENCES permissions(permission_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS departments (
  department_id INT AUTO_INCREMENT PRIMARY KEY,
  dept_name     VARCHAR(120) NOT NULL UNIQUE,
  description   VARCHAR(255) NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS positions (
  position_id   INT AUTO_INCREMENT PRIMARY KEY,
  department_id INT NULL,
  position_name VARCHAR(120) NOT NULL,
  description   VARCHAR(255) NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_position (department_id, position_name),
  CONSTRAINT fk_pos_dept FOREIGN KEY (department_id) REFERENCES departments(department_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS appointment_types (
  appointment_type_id INT AUTO_INCREMENT PRIMARY KEY,
  type_name           VARCHAR(60) NOT NULL UNIQUE,
  created_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS salary_grades (
  salary_grade INT PRIMARY KEY,
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS salary_steps (
  salary_step_id   BIGINT AUTO_INCREMENT PRIMARY KEY,
  salary_grade     INT NOT NULL,
  step_no          INT NOT NULL,
  monthly_rate     DECIMAL(12,2) NOT NULL,
  effectivity_date DATE NOT NULL,
  reference_note   VARCHAR(120) NULL,
  UNIQUE KEY uq_grade_step_eff (salary_grade, step_no, effectivity_date),
  CONSTRAINT fk_ss_grade FOREIGN KEY (salary_grade) REFERENCES salary_grades(salary_grade)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS employees (
  employee_id        INT AUTO_INCREMENT PRIMARY KEY,
  employee_no        VARCHAR(30) NOT NULL UNIQUE,
  last_name          VARCHAR(80) NOT NULL,
  first_name         VARCHAR(80) NOT NULL,
  middle_name        VARCHAR(80) NULL,
  sex                ENUM('MALE','FEMALE','OTHER') NULL,
  birth_date         DATE NULL,
  civil_status       VARCHAR(30) NULL,
  email              VARCHAR(150) NULL,
  contact_number     VARCHAR(50) NULL,
  address            VARCHAR(255) NULL,
  department_id      INT NULL,
  position_id        INT NULL,
  appointment_type_id INT NULL,
  salary_grade       INT NULL,
  step_no            INT NULL,
  hire_date          DATE NULL,
  status             ENUM('ACTIVE','ON_LEAVE','RESIGNED','TERMINATED') NOT NULL DEFAULT 'ACTIVE',
  tin_no             VARCHAR(30) NULL,
  gsis_bp_no         VARCHAR(30) NULL,
  philhealth_no      VARCHAR(30) NULL,
  pagibig_mid_no     VARCHAR(30) NULL,
  emergency_contact  VARCHAR(150) NULL,
  emergency_phone    VARCHAR(50) NULL,
  created_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_emp_dept (department_id),
  KEY idx_emp_pos  (position_id),
  CONSTRAINT fk_emp_dept FOREIGN KEY (department_id) REFERENCES departments(department_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_emp_pos FOREIGN KEY (position_id) REFERENCES positions(position_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_emp_appt FOREIGN KEY (appointment_type_id) REFERENCES appointment_types(appointment_type_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_emp_sg FOREIGN KEY (salary_grade) REFERENCES salary_grades(salary_grade)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS user_accounts (
  user_id       INT AUTO_INCREMENT PRIMARY KEY,
  role_id       INT NOT NULL,
  employee_id   INT NULL,
  username      VARCHAR(50) NOT NULL UNIQUE,
  password      VARCHAR(255) NOT NULL,
  full_name     VARCHAR(150) NULL,
  email         VARCHAR(150) NULL,
  status        ENUM('ACTIVE','INACTIVE','LOCKED') NOT NULL DEFAULT 'ACTIVE',
  last_login_at DATETIME NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_user_role FOREIGN KEY (role_id) REFERENCES roles(role_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_user_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS shifts (
  shift_id        INT AUTO_INCREMENT PRIMARY KEY,
  shift_name      VARCHAR(50) NOT NULL UNIQUE,
  start_time      TIME NOT NULL,
  end_time        TIME NOT NULL,
  break_minutes   INT NOT NULL DEFAULT 0,
  grace_minutes   INT NOT NULL DEFAULT 0,
  is_overnight    TINYINT(1) NOT NULL DEFAULT 0,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS shift_assignments (
  assignment_id INT AUTO_INCREMENT PRIMARY KEY,
  employee_id   INT NOT NULL,
  shift_id      INT NOT NULL,
  start_date    DATE NOT NULL,
  end_date      DATE NULL,
  assigned_by_user_id INT NULL,
  status        ENUM('ASSIGNED','CANCELLED') NOT NULL DEFAULT 'ASSIGNED',
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_sa_emp_dates (employee_id, start_date, end_date),
  CONSTRAINT fk_sa_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_sa_shift FOREIGN KEY (shift_id) REFERENCES shifts(shift_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_sa_user FOREIGN KEY (assigned_by_user_id) REFERENCES user_accounts(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS biometric_devices (
  device_id    INT AUTO_INCREMENT PRIMARY KEY,
  device_name  VARCHAR(100) NOT NULL,
  serial_no    VARCHAR(100) NULL,
  location     VARCHAR(150) NULL,
  ip_address   VARCHAR(45) NULL,
  is_active    TINYINT(1) NOT NULL DEFAULT 1,
  last_sync_at DATETIME NULL,
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_device_name (device_name)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS biometric_enrollments (
  enrollment_id      INT AUTO_INCREMENT PRIMARY KEY,
  employee_id        INT NOT NULL,
  biometric_user_id  VARCHAR(50) NOT NULL,
  device_id          INT NULL,
  status             ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_bio_user (biometric_user_id),
  KEY idx_be_emp (employee_id),
  CONSTRAINT fk_be_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_be_dev FOREIGN KEY (device_id) REFERENCES biometric_devices(device_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS attendance_logs (
  log_id      BIGINT AUTO_INCREMENT PRIMARY KEY,
  employee_id INT NOT NULL,
  device_id   INT NULL,
  log_time    DATETIME NOT NULL,
  log_type    ENUM('IN','OUT','BREAK_IN','BREAK_OUT') NOT NULL,
  source      ENUM('BIOMETRIC','MANUAL','IMPORT') NOT NULL DEFAULT 'BIOMETRIC',
  raw_payload JSON NULL,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_al_emp_time (employee_id, log_time),
  CONSTRAINT fk_al_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_al_dev FOREIGN KEY (device_id) REFERENCES biometric_devices(device_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS attendance_remarks (
  remark_id    BIGINT AUTO_INCREMENT PRIMARY KEY,
  employee_id  INT NOT NULL,
  work_date    DATE NOT NULL,
  remark_type  ENUM('OB','TO','WFH','CTO','HOLIDAY','SUSPENDED','OTHER') NOT NULL,
  details      VARCHAR(255) NULL,
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_remark (employee_id, work_date, remark_type),
  CONSTRAINT fk_ar_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS attendance_adjustments (
  adjustment_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  employee_id   INT NOT NULL,
  work_date     DATE NOT NULL,
  requested_in  DATETIME NULL,
  requested_out DATETIME NULL,
  reason        VARCHAR(255) NOT NULL,
  status        ENUM('PENDING','APPROVED','REJECTED') NOT NULL DEFAULT 'PENDING',
  decision_remarks VARCHAR(500) NULL,
  requested_by_user_id  INT NOT NULL,
  approved_by_user_id   INT NULL,
  requested_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  decided_at    DATETIME NULL,
  KEY idx_adj_emp_date (employee_id, work_date),
  CONSTRAINT fk_adj_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_adj_req FOREIGN KEY (requested_by_user_id) REFERENCES user_accounts(user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_adj_app FOREIGN KEY (approved_by_user_id) REFERENCES user_accounts(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

DROP VIEW IF EXISTS v_dtr_daily_raw;
CREATE VIEW v_dtr_daily_raw AS
SELECT
  al.employee_id,
  DATE(al.log_time) AS work_date,
  MIN(CASE WHEN al.log_type='IN'  THEN al.log_time END) AS time_in_raw,
  MAX(CASE WHEN al.log_type='OUT' THEN al.log_time END) AS time_out_raw
FROM attendance_logs al
GROUP BY al.employee_id, DATE(al.log_time);

DROP VIEW IF EXISTS v_dtr_daily_effective;
CREATE VIEW v_dtr_daily_effective AS
SELECT
  d.employee_id,
  d.work_date,
  COALESCE(adj.requested_in,  d.time_in_raw)  AS time_in,
  COALESCE(adj.requested_out, d.time_out_raw) AS time_out,
  TIMESTAMPDIFF(
    MINUTE,
    COALESCE(adj.requested_in, d.time_in_raw),
    COALESCE(adj.requested_out, d.time_out_raw)
  ) AS worked_minutes
FROM v_dtr_daily_raw d
LEFT JOIN attendance_adjustments adj
  ON adj.employee_id = d.employee_id
 AND adj.work_date   = d.work_date
 AND adj.status      = 'APPROVED'
WHERE COALESCE(adj.requested_in, d.time_in_raw) IS NOT NULL
  AND COALESCE(adj.requested_out, d.time_out_raw) IS NOT NULL;

CREATE TABLE IF NOT EXISTS dtr_monthly_certifications (
  cert_id     BIGINT AUTO_INCREMENT PRIMARY KEY,
  employee_id INT NOT NULL,
  yr          INT NOT NULL,
  mo          INT NOT NULL,
  certified_by_user_id INT NULL,
  verified_by_user_id  INT NULL,
  certified_at DATETIME NULL,
  verified_at  DATETIME NULL,
  remarks      VARCHAR(255) NULL,
  UNIQUE KEY uq_cert (employee_id, yr, mo),
  CONSTRAINT fk_cert_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_cert_by FOREIGN KEY (certified_by_user_id) REFERENCES user_accounts(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_ver_by FOREIGN KEY (verified_by_user_id) REFERENCES user_accounts(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS training_courses (
  course_id    BIGINT AUTO_INCREMENT PRIMARY KEY,
  course_name  VARCHAR(150) NOT NULL UNIQUE,
  description  VARCHAR(255) NULL,
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS training_sessions (
  session_id      BIGINT AUTO_INCREMENT PRIMARY KEY,
  course_id       BIGINT NOT NULL,
  session_date    DATE NOT NULL,
  trainer_user_id INT NULL,
  location        VARCHAR(150) NULL,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_course_date (course_id, session_date),
  CONSTRAINT fk_ts_course FOREIGN KEY (course_id) REFERENCES training_courses(course_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ts_trainer FOREIGN KEY (trainer_user_id) REFERENCES user_accounts(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS training_enrollments (
  enrollment_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  session_id    BIGINT NOT NULL,
  employee_id   INT NOT NULL,
  status        ENUM('PENDING','COMPLETED','FAILED') NOT NULL DEFAULT 'PENDING',
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_te_session_emp (session_id, employee_id),
  CONSTRAINT fk_te_session FOREIGN KEY (session_id) REFERENCES training_sessions(session_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_te_emp FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB;
