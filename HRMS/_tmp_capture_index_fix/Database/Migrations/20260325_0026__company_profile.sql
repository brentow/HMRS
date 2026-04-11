-- Company profile branding data for login/dashboard identity panels.

CREATE TABLE IF NOT EXISTS company_profile (
  profile_id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  company_name VARCHAR(200) NOT NULL,
  address VARCHAR(500) NULL,
  owner_name VARCHAR(200) NULL,
  serial_number VARCHAR(100) NULL,
  logo_path VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB;

INSERT INTO company_profile (
  company_name,
  address,
  owner_name,
  serial_number,
  logo_path
)
SELECT
  'Human Resources Management System',
  'Human Resource Management Office',
  'HRMS Control Center',
  'Office ID 18 / OFF-2026-0007',
  'HRMS/Images/ePRIME_logo.png'
WHERE NOT EXISTS (SELECT 1 FROM company_profile);
