CREATE TABLE IF NOT EXISTS payroll_concerns (
  payroll_concern_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  payroll_run_id BIGINT NOT NULL,
  employee_id INT NOT NULL,
  reported_by_user_id INT NULL,
  concern_details VARCHAR(1000) NOT NULL,
  status ENUM('OPEN','IN_REVIEW','RESOLVED','REJECTED') NOT NULL DEFAULT 'OPEN',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  resolved_at DATETIME NULL,
  resolved_by_user_id INT NULL,
  resolution_notes VARCHAR(1000) NULL,
  KEY idx_payroll_concerns_run (payroll_run_id),
  KEY idx_payroll_concerns_employee (employee_id, status),
  KEY idx_payroll_concerns_created (created_at),
  CONSTRAINT fk_payroll_concerns_run
    FOREIGN KEY (payroll_run_id) REFERENCES payroll_runs(payroll_run_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_payroll_concerns_employee
    FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_payroll_concerns_reporter
    FOREIGN KEY (reported_by_user_id) REFERENCES user_accounts(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_payroll_concerns_resolver
    FOREIGN KEY (resolved_by_user_id) REFERENCES user_accounts(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
