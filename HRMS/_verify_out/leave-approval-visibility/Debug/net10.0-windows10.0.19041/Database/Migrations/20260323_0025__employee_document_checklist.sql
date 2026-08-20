-- Migration 0025: Employee document checklist for System Verifier.
-- Uses signed INT for employee_id because employees.employee_id is INT, not UNSIGNED.

CREATE TABLE IF NOT EXISTS employee_document_checklist (
  checklist_id INT AUTO_INCREMENT PRIMARY KEY,
  employee_id INT NOT NULL,
  position_name VARCHAR(150) NOT NULL DEFAULT '',
  employment_type VARCHAR(100) NOT NULL DEFAULT 'Permanent',
  document_code VARCHAR(50) NOT NULL,
  document_name VARCHAR(250) NOT NULL,
  document_tier TINYINT UNSIGNED NOT NULL DEFAULT 1,
  is_required TINYINT(1) NOT NULL DEFAULT 1,
  status VARCHAR(30) NOT NULL DEFAULT 'not_submitted',
  submitted_date DATE NULL,
  expiry_date DATE NULL,
  verified_date DATE NULL,
  verified_by VARCHAR(150) NULL,
  waived_reason VARCHAR(300) NULL,
  expiry_alert_sent TINYINT(1) NOT NULL DEFAULT 0,
  remarks VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_doc_checklist_emp_id (employee_id),
  KEY idx_doc_checklist_status (status),
  KEY idx_doc_checklist_expiry (expiry_date),
  CONSTRAINT fk_doc_checklist_employee
    FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
