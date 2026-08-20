-- =========================================================
-- GOVERNMENT CONTRIBUTION BRACKET TABLES
-- Instructor requirement: bracket tables stored in DB ("give it a big table")
-- Separate tables for SSS, GSIS, PhilHealth, Pag-IBIG
-- EE (Employee) and ER (Employer) shares always separate
-- =========================================================

-- SSS Contribution Table (2025 schedule, full seed data)
CREATE TABLE IF NOT EXISTS sss_contribution_brackets (
  bracket_id INT AUTO_INCREMENT PRIMARY KEY,
  salary_credit DECIMAL(12,2) NOT NULL,
  min_range DECIMAL(12,2) NOT NULL,
  max_range DECIMAL(12,2) NOT NULL,
  ee_share DECIMAL(12,2) NOT NULL COMMENT 'Employee share',
  er_share DECIMAL(12,2) NOT NULL COMMENT 'Employer share',
  ec_share DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT 'EC (Employer Compensation)',
  total_contribution DECIMAL(12,2) NOT NULL,
  effective_year INT NOT NULL DEFAULT 2025,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  KEY idx_sss_range (min_range, max_range),
  KEY idx_sss_active_year (is_active, effective_year)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- GSIS Contribution Table
CREATE TABLE IF NOT EXISTS gsis_contribution_brackets (
  bracket_id INT AUTO_INCREMENT PRIMARY KEY,
  ee_rate DECIMAL(6,4) NOT NULL COMMENT 'Employee rate (e.g. 0.0900 = 9%)',
  er_rate DECIMAL(6,4) NOT NULL COMMENT 'Employer rate (e.g. 0.1200 = 12%)',
  description VARCHAR(100) NULL,
  effective_year INT NOT NULL DEFAULT 2025,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  KEY idx_gsis_active (is_active, effective_year)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- PhilHealth Contribution Table
CREATE TABLE IF NOT EXISTS philhealth_contribution_brackets (
  bracket_id INT AUTO_INCREMENT PRIMARY KEY,
  premium_rate DECIMAL(6,4) NOT NULL COMMENT 'Total premium rate (e.g. 0.0500 = 5%)',
  min_monthly_premium DECIMAL(12,2) NOT NULL DEFAULT 500.00,
  max_monthly_premium DECIMAL(12,2) NOT NULL DEFAULT 5000.00,
  ee_share_pct DECIMAL(6,4) NOT NULL DEFAULT 0.5000 COMMENT 'Employee share percentage of premium (50%)',
  er_share_pct DECIMAL(6,4) NOT NULL DEFAULT 0.5000 COMMENT 'Employer share percentage of premium (50%)',
  salary_floor DECIMAL(12,2) NOT NULL DEFAULT 10000.00,
  salary_ceiling DECIMAL(12,2) NOT NULL DEFAULT 100000.00,
  description VARCHAR(100) NULL,
  effective_year INT NOT NULL DEFAULT 2025,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  KEY idx_ph_active (is_active, effective_year)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Pag-IBIG Contribution Table
CREATE TABLE IF NOT EXISTS pagibig_contribution_brackets (
  bracket_id INT AUTO_INCREMENT PRIMARY KEY,
  min_salary DECIMAL(12,2) NOT NULL,
  max_salary DECIMAL(12,2) NOT NULL,
  ee_rate DECIMAL(6,4) NOT NULL COMMENT 'Employee rate',
  er_rate DECIMAL(6,4) NOT NULL COMMENT 'Employer rate',
  max_ee_contribution DECIMAL(12,2) NOT NULL DEFAULT 200.00,
  max_er_contribution DECIMAL(12,2) NOT NULL DEFAULT 200.00,
  description VARCHAR(100) NULL,
  effective_year INT NOT NULL DEFAULT 2025,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  KEY idx_pagibig_range (min_salary, max_salary),
  KEY idx_pagibig_active (is_active, effective_year)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- =========================================================
-- SEED: Full SSS 2025 Contribution Table
-- Based on RA 11199 (Social Security Act of 2018) schedule
-- =========================================================
INSERT INTO sss_contribution_brackets
  (salary_credit, min_range, max_range, ee_share, er_share, ec_share, total_contribution, effective_year)
VALUES
  (4000.00,     0.00,  4249.99,   200.00,   400.00,  10.00,  610.00, 2025),
  (4500.00,  4250.00,  4749.99,   225.00,   450.00,  10.00,  685.00, 2025),
  (5000.00,  4750.00,  5249.99,   250.00,   500.00,  10.00,  760.00, 2025),
  (5500.00,  5250.00,  5749.99,   275.00,   550.00,  10.00,  835.00, 2025),
  (6000.00,  5750.00,  6249.99,   300.00,   600.00,  10.00,  910.00, 2025),
  (6500.00,  6250.00,  6749.99,   325.00,   650.00,  10.00,  985.00, 2025),
  (7000.00,  6750.00,  7249.99,   350.00,   700.00,  10.00, 1060.00, 2025),
  (7500.00,  7250.00,  7749.99,   375.00,   750.00,  10.00, 1135.00, 2025),
  (8000.00,  7750.00,  8249.99,   400.00,   800.00,  10.00, 1210.00, 2025),
  (8500.00,  8250.00,  8749.99,   425.00,   850.00,  10.00, 1285.00, 2025),
  (9000.00,  8750.00,  9249.99,   450.00,   900.00,  10.00, 1360.00, 2025),
  (9500.00,  9250.00,  9749.99,   475.00,   950.00,  10.00, 1435.00, 2025),
  (10000.00, 9750.00, 10249.99,   500.00,  1000.00,  10.00, 1510.00, 2025),
  (10500.00,10250.00, 10749.99,   525.00,  1050.00,  10.00, 1585.00, 2025),
  (11000.00,10750.00, 11249.99,   550.00,  1100.00,  10.00, 1660.00, 2025),
  (11500.00,11250.00, 11749.99,   575.00,  1150.00,  10.00, 1735.00, 2025),
  (12000.00,11750.00, 12249.99,   600.00,  1200.00,  10.00, 1810.00, 2025),
  (12500.00,12250.00, 12749.99,   625.00,  1250.00,  10.00, 1885.00, 2025),
  (13000.00,12750.00, 13249.99,   650.00,  1300.00,  10.00, 1960.00, 2025),
  (13500.00,13250.00, 13749.99,   675.00,  1350.00,  10.00, 2035.00, 2025),
  (14000.00,13750.00, 14249.99,   700.00,  1400.00,  10.00, 2110.00, 2025),
  (14500.00,14250.00, 14749.99,   725.00,  1450.00,  10.00, 2185.00, 2025),
  (15000.00,14750.00, 15249.99,   750.00,  1500.00,  10.00, 2260.00, 2025),
  (15500.00,15250.00, 15749.99,   775.00,  1550.00,  10.00, 2335.00, 2025),
  (16000.00,15750.00, 16249.99,   800.00,  1600.00,  10.00, 2410.00, 2025),
  (16500.00,16250.00, 16749.99,   825.00,  1650.00,  10.00, 2485.00, 2025),
  (17000.00,16750.00, 17249.99,   850.00,  1700.00,  10.00, 2560.00, 2025),
  (17500.00,17250.00, 17749.99,   875.00,  1750.00,  10.00, 2635.00, 2025),
  (18000.00,17750.00, 18249.99,   900.00,  1800.00,  10.00, 2710.00, 2025),
  (18500.00,18250.00, 18749.99,   925.00,  1850.00,  10.00, 2785.00, 2025),
  (19000.00,18750.00, 19249.99,   950.00,  1900.00,  10.00, 2860.00, 2025),
  (19500.00,19250.00, 19749.99,   975.00,  1950.00,  10.00, 2935.00, 2025),
  (20000.00,19750.00, 20249.99,  1000.00,  2000.00,  10.00, 3010.00, 2025),
  (20500.00,20250.00, 20749.99,  1025.00,  2050.00,  10.00, 3085.00, 2025),
  (21000.00,20750.00, 21249.99,  1050.00,  2100.00,  10.00, 3160.00, 2025),
  (21500.00,21250.00, 21749.99,  1075.00,  2150.00,  10.00, 3235.00, 2025),
  (22000.00,21750.00, 22249.99,  1100.00,  2200.00,  10.00, 3310.00, 2025),
  (22500.00,22250.00, 22749.99,  1125.00,  2250.00,  10.00, 3385.00, 2025),
  (23000.00,22750.00, 23249.99,  1150.00,  2300.00,  10.00, 3460.00, 2025),
  (23500.00,23250.00, 23749.99,  1175.00,  2350.00,  10.00, 3535.00, 2025),
  (24000.00,23750.00, 24249.99,  1200.00,  2400.00,  10.00, 3610.00, 2025),
  (24500.00,24250.00, 24749.99,  1225.00,  2450.00,  10.00, 3685.00, 2025),
  (25000.00,24750.00, 25249.99,  1250.00,  2500.00,  10.00, 3760.00, 2025),
  (25500.00,25250.00, 25749.99,  1275.00,  2550.00,  10.00, 3835.00, 2025),
  (26000.00,25750.00, 26249.99,  1300.00,  2600.00,  10.00, 3910.00, 2025),
  (26500.00,26250.00, 26749.99,  1325.00,  2650.00,  10.00, 3985.00, 2025),
  (27000.00,26750.00, 27249.99,  1350.00,  2700.00,  10.00, 4060.00, 2025),
  (27500.00,27250.00, 27749.99,  1375.00,  2750.00,  10.00, 4135.00, 2025),
  (28000.00,27750.00, 28249.99,  1400.00,  2800.00,  10.00, 4210.00, 2025),
  (28500.00,28250.00, 28749.99,  1425.00,  2850.00,  10.00, 4285.00, 2025),
  (29000.00,28750.00, 29249.99,  1450.00,  2900.00,  10.00, 4360.00, 2025),
  (29500.00,29250.00, 29749.99,  1475.00,  2950.00,  10.00, 4435.00, 2025),
  (30000.00,29750.00, 99999999.99, 1500.00, 3000.00, 10.00, 4510.00, 2025);

-- =========================================================
-- SEED: GSIS 2025 Rates
-- =========================================================
INSERT INTO gsis_contribution_brackets
  (ee_rate, er_rate, description, effective_year)
VALUES
  (0.0900, 0.1200, 'Standard GSIS rate for permanent/regular government employees', 2025);

-- =========================================================
-- SEED: PhilHealth 2025 Rates
-- =========================================================
INSERT INTO philhealth_contribution_brackets
  (premium_rate, min_monthly_premium, max_monthly_premium, ee_share_pct, er_share_pct, salary_floor, salary_ceiling, description, effective_year)
VALUES
  (0.0500, 500.00, 5000.00, 0.5000, 0.5000, 10000.00, 100000.00, 'PhilHealth 2025 premium rate (5% shared equally)', 2025);

-- =========================================================
-- SEED: Pag-IBIG 2025 Rates
-- =========================================================
INSERT INTO pagibig_contribution_brackets
  (min_salary, max_salary, ee_rate, er_rate, max_ee_contribution, max_er_contribution, description, effective_year)
VALUES
  (0.00, 1500.00, 0.0100, 0.0200, 200.00, 200.00, 'Pag-IBIG: salary <= 1500 (1% EE, 2% ER)', 2025),
  (1500.01, 99999999.99, 0.0200, 0.0200, 200.00, 200.00, 'Pag-IBIG: salary > 1500 (2% EE, 2% ER)', 2025);
