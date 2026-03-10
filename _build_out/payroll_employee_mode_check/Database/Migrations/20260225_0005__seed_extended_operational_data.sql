-- Extended operational/demo seed data for salary steps, attendance, leave, payroll, performance, and recruitment.

START TRANSACTION;

SET @OLD_SAFE_UPDATES = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

-- =========================================================
-- A) SALARY STEPS (sample rates)
-- =========================================================
INSERT INTO salary_steps (salary_grade, step_no, monthly_rate, effectivity_date, reference_note)
VALUES
(6, 1, 15000.00, '2026-01-01', 'Sample LGU rate (demo)'),
(6, 2, 15500.00, '2026-01-01', 'Sample LGU rate (demo)'),
(6, 3, 16000.00, '2026-01-01', 'Sample LGU rate (demo)'),
(15, 1, 35000.00, '2026-01-01', 'Sample LGU rate (demo)'),
(15, 2, 36000.00, '2026-01-01', 'Sample LGU rate (demo)'),
(15, 3, 37000.00, '2026-01-01', 'Sample LGU rate (demo)'),
(18, 1, 45000.00, '2026-01-01', 'Sample LGU rate (demo)'),
(18, 2, 46000.00, '2026-01-01', 'Sample LGU rate (demo)'),
(18, 3, 47000.00, '2026-01-01', 'Sample LGU rate (demo)'),
(24, 1, 65000.00, '2026-01-01', 'Sample LGU rate (demo)'),
(24, 2, 66500.00, '2026-01-01', 'Sample LGU rate (demo)'),
(24, 3, 68000.00, '2026-01-01', 'Sample LGU rate (demo)')
ON DUPLICATE KEY UPDATE
  monthly_rate   = VALUES(monthly_rate),
  reference_note = VALUES(reference_note);

-- =========================================================
-- B) ATTENDANCE REMARKS
-- =========================================================
INSERT INTO attendance_remarks (employee_id, work_date, remark_type, details)
VALUES
((SELECT employee_id FROM employees WHERE employee_no='E-4001' LIMIT 1), '2026-02-19', 'OB', 'Official Business: delivery of supplies to GSO'),
((SELECT employee_id FROM employees WHERE employee_no='E-4002' LIMIT 1), '2026-02-18', 'TO', 'Time Off: personal errand (approved)'),
((SELECT employee_id FROM employees WHERE employee_no='E-3001' LIMIT 1), '2026-02-17', 'OTHER', 'Meeting with Mayor''s office (recorded)'),
((SELECT employee_id FROM employees WHERE employee_no='E-1002' LIMIT 1), '2026-02-20', 'WFH', 'Remote systems monitoring')
ON DUPLICATE KEY UPDATE
  details = VALUES(details);

-- =========================================================
-- C) ATTENDANCE ADJUSTMENTS
-- =========================================================
INSERT INTO attendance_adjustments
(
  employee_id, work_date,
  requested_in, requested_out,
  reason, status,
  requested_by_user_id, approved_by_user_id,
  requested_at, decided_at
)
SELECT
  (SELECT employee_id FROM employees WHERE employee_no='E-4003' LIMIT 1),
  '2026-02-18',
  '2026-02-18 07:05:00',
  '2026-02-18 17:00:00',
  'Biometric failed to read fingerprint; supervisor verified presence.',
  'APPROVED',
  (SELECT user_id FROM user_accounts WHERE username='emp3' LIMIT 1),
  (SELECT user_id FROM user_accounts WHERE username='hr1' LIMIT 1),
  '2026-02-18 12:10:00',
  '2026-02-18 16:00:00'
WHERE NOT EXISTS (
  SELECT 1
  FROM attendance_adjustments
  WHERE employee_id = (SELECT employee_id FROM employees WHERE employee_no='E-4003' LIMIT 1)
    AND work_date = '2026-02-18'
);

INSERT INTO attendance_adjustments
(
  employee_id, work_date,
  requested_in, requested_out,
  reason, status,
  requested_by_user_id, approved_by_user_id,
  requested_at, decided_at
)
SELECT
  (SELECT employee_id FROM employees WHERE employee_no='E-4004' LIMIT 1),
  '2026-02-19',
  '2026-02-19 07:00:00',
  '2026-02-19 17:00:00',
  'Forgot to tap OUT; supervisor confirms 5PM out.',
  'PENDING',
  (SELECT user_id FROM user_accounts WHERE username='emp4' LIMIT 1),
  NULL,
  '2026-02-19 17:30:00',
  NULL
WHERE NOT EXISTS (
  SELECT 1
  FROM attendance_adjustments
  WHERE employee_id = (SELECT employee_id FROM employees WHERE employee_no='E-4004' LIMIT 1)
    AND work_date = '2026-02-19'
);

-- =========================================================
-- D) LEAVE DATA
-- =========================================================
INSERT INTO leave_balances
(
  employee_id, leave_type_id, `year`,
  opening_credits, earned, used, adjustments,
  as_of_date
)
SELECT
  e.employee_id,
  lt.leave_type_id,
  2026,
  lt.default_credits_per_year,
  0.00,
  0.00,
  0.00,
  '2026-02-01'
FROM employees e
JOIN leave_types lt ON lt.code IN ('VL','SL','SPL')
WHERE e.status='ACTIVE'
ON DUPLICATE KEY UPDATE
  opening_credits = VALUES(opening_credits),
  earned          = VALUES(earned),
  used            = VALUES(used),
  adjustments     = VALUES(adjustments),
  as_of_date      = VALUES(as_of_date),
  updated_at      = CURRENT_TIMESTAMP;

INSERT INTO leave_applications
(
  employee_id, leave_type_id,
  date_from, date_to,
  days_requested,
  reason,
  status,
  filed_at,
  decision_at,
  recommended_by_employee_id,
  approved_by_employee_id,
  hr_certified_by_employee_id,
  decision_remarks
)
SELECT
  (SELECT employee_id FROM employees WHERE employee_no='E-4001' LIMIT 1),
  (SELECT leave_type_id FROM leave_types WHERE code='VL' LIMIT 1),
  '2026-02-26','2026-02-27',
  2.00,
  'Family matter (out of town).',
  'APPROVED',
  '2026-02-20 09:00:00',
  '2026-02-21 15:30:00',
  (SELECT employee_id FROM employees WHERE employee_no='E-3001' LIMIT 1),
  (SELECT employee_id FROM employees WHERE employee_no='E-3001' LIMIT 1),
  (SELECT employee_id FROM employees WHERE employee_no='E-2001' LIMIT 1),
  'Approved; ensure turnover of tasks.'
WHERE NOT EXISTS (
  SELECT 1
  FROM leave_applications
  WHERE employee_id=(SELECT employee_id FROM employees WHERE employee_no='E-4001' LIMIT 1)
    AND date_from='2026-02-26'
    AND date_to='2026-02-27'
);

INSERT INTO leave_applications
(
  employee_id, leave_type_id,
  date_from, date_to,
  days_requested,
  reason,
  status,
  filed_at,
  recommended_by_employee_id,
  hr_certified_by_employee_id
)
SELECT
  (SELECT employee_id FROM employees WHERE employee_no='E-4002' LIMIT 1),
  (SELECT leave_type_id FROM leave_types WHERE code='SL' LIMIT 1),
  '2026-02-24','2026-02-24',
  1.00,
  'Not feeling well (fever).',
  'SUBMITTED',
  '2026-02-23 16:20:00',
  (SELECT employee_id FROM employees WHERE employee_no='E-3002' LIMIT 1),
  (SELECT employee_id FROM employees WHERE employee_no='E-2002' LIMIT 1)
WHERE NOT EXISTS (
  SELECT 1
  FROM leave_applications
  WHERE employee_id=(SELECT employee_id FROM employees WHERE employee_no='E-4002' LIMIT 1)
    AND date_from='2026-02-24'
    AND date_to='2026-02-24'
);

INSERT INTO leave_applications
(
  employee_id, leave_type_id,
  date_from, date_to,
  days_requested,
  reason,
  status,
  filed_at,
  decision_at,
  recommended_by_employee_id,
  approved_by_employee_id,
  hr_certified_by_employee_id,
  decision_remarks
)
SELECT
  (SELECT employee_id FROM employees WHERE employee_no='E-2001' LIMIT 1),
  (SELECT leave_type_id FROM leave_types WHERE code='SPL' LIMIT 1),
  '2026-02-28','2026-02-28',
  1.00,
  'Special privilege leave (personal).',
  'APPROVED',
  '2026-02-22 10:15:00',
  '2026-02-23 09:10:00',
  (SELECT employee_id FROM employees WHERE employee_no='E-3001' LIMIT 1),
  (SELECT employee_id FROM employees WHERE employee_no='E-3001' LIMIT 1),
  (SELECT employee_id FROM employees WHERE employee_no='E-2002' LIMIT 1),
  'Approved.'
WHERE NOT EXISTS (
  SELECT 1
  FROM leave_applications
  WHERE employee_id=(SELECT employee_id FROM employees WHERE employee_no='E-2001' LIMIT 1)
    AND date_from='2026-02-28'
    AND date_to='2026-02-28'
);

INSERT INTO leave_application_days (leave_application_id, leave_date, day_fraction, half_day_part)
SELECT la.leave_application_id, '2026-02-26', 1.00, NULL
FROM leave_applications la
WHERE la.employee_id=(SELECT employee_id FROM employees WHERE employee_no='E-4001' LIMIT 1)
  AND la.date_from='2026-02-26'
  AND la.date_to='2026-02-27'
ON DUPLICATE KEY UPDATE day_fraction=VALUES(day_fraction);

INSERT INTO leave_application_days (leave_application_id, leave_date, day_fraction, half_day_part)
SELECT la.leave_application_id, '2026-02-27', 1.00, NULL
FROM leave_applications la
WHERE la.employee_id=(SELECT employee_id FROM employees WHERE employee_no='E-4001' LIMIT 1)
  AND la.date_from='2026-02-26'
  AND la.date_to='2026-02-27'
ON DUPLICATE KEY UPDATE day_fraction=VALUES(day_fraction);

INSERT INTO leave_application_days (leave_application_id, leave_date, day_fraction, half_day_part)
SELECT la.leave_application_id, '2026-02-24', 1.00, NULL
FROM leave_applications la
WHERE la.employee_id=(SELECT employee_id FROM employees WHERE employee_no='E-4002' LIMIT 1)
  AND la.date_from='2026-02-24'
  AND la.date_to='2026-02-24'
ON DUPLICATE KEY UPDATE day_fraction=VALUES(day_fraction);

INSERT INTO leave_application_days (leave_application_id, leave_date, day_fraction, half_day_part)
SELECT la.leave_application_id, '2026-02-28', 1.00, NULL
FROM leave_applications la
WHERE la.employee_id=(SELECT employee_id FROM employees WHERE employee_no='E-2001' LIMIT 1)
  AND la.date_from='2026-02-28'
  AND la.date_to='2026-02-28'
ON DUPLICATE KEY UPDATE day_fraction=VALUES(day_fraction);

INSERT INTO leave_documents
(
  leave_application_id,
  file_name,
  file_path,
  uploaded_at,
  uploaded_by_employee_id
)
SELECT
  la.leave_application_id,
  'medical_certificate.pdf',
  'uploads/leave/2026/medical_certificate_APP-4002.pdf',
  '2026-02-23 18:00:00',
  (SELECT employee_id FROM employees WHERE employee_no='E-4002' LIMIT 1)
FROM leave_applications la
WHERE la.employee_id=(SELECT employee_id FROM employees WHERE employee_no='E-4002' LIMIT 1)
  AND la.date_from='2026-02-24'
  AND la.date_to='2026-02-24'
  AND NOT EXISTS (
    SELECT 1
    FROM leave_documents d
    WHERE d.leave_application_id = la.leave_application_id
  );

UPDATE leave_balances lb
JOIN (
  SELECT
    la.employee_id,
    la.leave_type_id,
    YEAR(la.date_from) AS yr,
    SUM(la.days_requested) AS used_days
  FROM leave_applications la
  WHERE la.status='APPROVED'
    AND YEAR(la.date_from)=2026
  GROUP BY la.employee_id, la.leave_type_id, YEAR(la.date_from)
) x
ON x.employee_id=lb.employee_id
AND x.leave_type_id=lb.leave_type_id
AND x.yr=lb.`year`
SET lb.used = x.used_days,
    lb.as_of_date = '2026-02-28',
    lb.updated_at = CURRENT_TIMESTAMP;

-- =========================================================
-- E) PAYROLL DATA
-- =========================================================
INSERT INTO payroll_periods (period_code, date_from, date_to, pay_date, status)
VALUES ('PP-2026-02B', '2026-02-16', '2026-02-28', '2026-03-05', 'OPEN')
ON DUPLICATE KEY UPDATE
  date_from = VALUES(date_from),
  date_to   = VALUES(date_to),
  pay_date  = VALUES(pay_date),
  status    = VALUES(status),
  updated_at = CURRENT_TIMESTAMP;

SET @pp_id = (
  SELECT payroll_period_id
  FROM payroll_periods
  WHERE period_code='PP-2026-02B'
  LIMIT 1
);

INSERT INTO payroll_runs
(
  payroll_period_id, employee_id,
  basic_pay, allowances, overtime_pay, other_earnings,
  gross_pay, deductions_total, net_pay,
  status, generated_at
)
SELECT
  @pp_id,
  e.employee_id,
  COALESCE(ss.monthly_rate, 0.00) AS basic_pay,
  CASE
    WHEN e.department_id = (
      SELECT department_id
      FROM departments
      WHERE dept_name='General Services Office'
      LIMIT 1
    )
    THEN 1000.00
    ELSE 1500.00
  END AS allowances,
  0.00,
  0.00,
  (
    COALESCE(ss.monthly_rate, 0.00)
    + CASE
        WHEN e.department_id = (
          SELECT department_id
          FROM departments
          WHERE dept_name='General Services Office'
          LIMIT 1
        )
        THEN 1000.00
        ELSE 1500.00
      END
  ),
  0.00,
  0.00,
  'GENERATED',
  NOW()
FROM employees e
LEFT JOIN salary_steps ss
  ON ss.salary_grade = e.salary_grade
 AND ss.step_no = e.step_no
 AND ss.effectivity_date = '2026-01-01'
WHERE e.status='ACTIVE'
ON DUPLICATE KEY UPDATE
  basic_pay      = VALUES(basic_pay),
  allowances     = VALUES(allowances),
  overtime_pay   = VALUES(overtime_pay),
  other_earnings = VALUES(other_earnings),
  gross_pay      = VALUES(gross_pay),
  status         = VALUES(status),
  generated_at   = VALUES(generated_at);

DELETE pri
FROM payroll_run_items pri
JOIN payroll_runs pr ON pr.payroll_run_id = pri.payroll_run_id
WHERE pr.payroll_period_id = @pp_id;

INSERT INTO payroll_run_items (payroll_run_id, item_type, code, description, amount)
SELECT pr.payroll_run_id, 'EARNING', 'ALLOW', 'Allowance', pr.allowances
FROM payroll_runs pr
WHERE pr.payroll_period_id=@pp_id;

INSERT INTO payroll_run_items (payroll_run_id, item_type, code, description, amount)
SELECT pr.payroll_run_id, 'DEDUCTION', 'GSIS', 'GSIS Contribution (sample)', ROUND(pr.basic_pay * 0.09, 2)
FROM payroll_runs pr
WHERE pr.payroll_period_id=@pp_id;

INSERT INTO payroll_run_items (payroll_run_id, item_type, code, description, amount)
SELECT pr.payroll_run_id, 'DEDUCTION', 'PHIC', 'PhilHealth Contribution (sample)', ROUND(pr.basic_pay * 0.04, 2)
FROM payroll_runs pr
WHERE pr.payroll_period_id=@pp_id;

INSERT INTO payroll_run_items (payroll_run_id, item_type, code, description, amount)
SELECT pr.payroll_run_id, 'DEDUCTION', 'HDMF', 'Pag-IBIG Contribution (sample)', 200.00
FROM payroll_runs pr
WHERE pr.payroll_period_id=@pp_id;

UPDATE payroll_runs pr
JOIN (
  SELECT payroll_run_id,
         SUM(CASE WHEN item_type='DEDUCTION' THEN amount ELSE 0 END) AS ded_total,
         SUM(CASE WHEN item_type='EARNING' THEN amount ELSE 0 END) AS earn_total
  FROM payroll_run_items
  GROUP BY payroll_run_id
) x
ON x.payroll_run_id = pr.payroll_run_id
SET
  pr.deductions_total = x.ded_total,
  pr.gross_pay        = (pr.basic_pay + pr.overtime_pay + pr.other_earnings + x.earn_total),
  pr.net_pay          = (pr.basic_pay + pr.overtime_pay + pr.other_earnings + x.earn_total) - x.ded_total
WHERE pr.payroll_period_id=@pp_id;

INSERT INTO payslip_releases (payroll_run_id, released_at, released_by_employee_id, remarks)
SELECT
  pr.payroll_run_id,
  '2026-03-05 09:00:00',
  (SELECT employee_id FROM employees WHERE employee_no='E-2001' LIMIT 1),
  'Released via HRMS (sample)'
FROM payroll_runs pr
JOIN employees e ON e.employee_id = pr.employee_id
WHERE pr.payroll_period_id=@pp_id
  AND e.employee_no IN ('E-1001','E-1002','E-2001','E-2002','E-3001')
  AND NOT EXISTS (
    SELECT 1
    FROM payslip_releases x
    WHERE x.payroll_run_id = pr.payroll_run_id
  );

-- =========================================================
-- F) PERFORMANCE DATA
-- =========================================================
INSERT INTO performance_cycles
(
  cycle_code, name,
  start_date, end_date,
  status,
  created_by_employee_id
)
VALUES
(
  'PC-2026-H1',
  'Performance Cycle H1 2026',
  '2026-01-01',
  '2026-06-30',
  'OPEN',
  (SELECT employee_id FROM employees WHERE employee_no='E-2001' LIMIT 1)
)
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  start_date = VALUES(start_date),
  end_date = VALUES(end_date),
  status = VALUES(status),
  created_by_employee_id = VALUES(created_by_employee_id),
  updated_at = CURRENT_TIMESTAMP;

SET @pc_id = (
  SELECT performance_cycle_id
  FROM performance_cycles
  WHERE cycle_code='PC-2026-H1'
  LIMIT 1
);

INSERT INTO performance_goals
(
  performance_cycle_id,
  employee_id,
  title,
  description,
  weight,
  target_metric
)
SELECT
  @pc_id,
  e.employee_id,
  'Work Quality & Timeliness',
  'Deliver assigned tasks on time; maintain accuracy and quality of output.',
  50.00,
  'On-time completion rate'
FROM employees e
WHERE e.status='ACTIVE'
  AND NOT EXISTS (
    SELECT 1
    FROM performance_goals g
    WHERE g.performance_cycle_id=@pc_id
      AND g.employee_id=e.employee_id
  );

INSERT INTO performance_reviews
(
  performance_cycle_id,
  employee_id,
  reviewer_employee_id,
  overall_rating,
  status,
  remarks,
  submitted_at
)
SELECT
  @pc_id,
  e.employee_id,
  (SELECT employee_id FROM employees WHERE employee_no='E-3001' LIMIT 1),
  4.20,
  'SUBMITTED',
  'Good performance; maintain attendance discipline and accuracy.',
  '2026-02-20 16:30:00'
FROM employees e
WHERE e.department_id = (
    SELECT department_id
    FROM departments
    WHERE dept_name='General Services Office'
    LIMIT 1
  )
  AND NOT EXISTS (
    SELECT 1
    FROM performance_reviews r
    WHERE r.performance_cycle_id=@pc_id
      AND r.employee_id=e.employee_id
  );

INSERT INTO performance_review_items (performance_review_id, criteria, weight, score, comments)
SELECT r.performance_review_id, 'Attendance & Punctuality', 50.00, 4.00,
       'Generally on time; minor issues addressed.'
FROM performance_reviews r
WHERE r.performance_cycle_id=@pc_id
  AND NOT EXISTS (
    SELECT 1
    FROM performance_review_items i
    WHERE i.performance_review_id=r.performance_review_id
      AND i.criteria='Attendance & Punctuality'
  );

INSERT INTO performance_review_items (performance_review_id, criteria, weight, score, comments)
SELECT r.performance_review_id, 'Work Output Quality', 50.00, 4.40,
       'Quality output and good teamwork.'
FROM performance_reviews r
WHERE r.performance_cycle_id=@pc_id
  AND NOT EXISTS (
    SELECT 1
    FROM performance_review_items i
    WHERE i.performance_review_id=r.performance_review_id
      AND i.criteria='Work Output Quality'
  );

-- =========================================================
-- G) RECRUITMENT DATA
-- =========================================================
INSERT INTO job_postings
(
  posting_code,
  title,
  department_id,
  position_id,
  employment_type,
  vacancies,
  salary_grade,
  salary_range_min,
  salary_range_max,
  description,
  requirements,
  status,
  open_date,
  close_date,
  created_by_employee_id
)
VALUES
(
  'JP-2026-001',
  'Administrative Aide I',
  (SELECT department_id FROM departments WHERE dept_name='General Services Office' LIMIT 1),
  (SELECT position_id FROM positions WHERE position_name='Administrative Aide' LIMIT 1),
  'CASUAL',
  2,
  'SG 6',
  14000.00,
  18000.00,
  'Provides clerical support, filing, and assistance in daily office operations.',
  'At least Senior High School graduate; basic computer skills; good communication.',
  'OPEN',
  '2026-02-01',
  '2026-02-28',
  (SELECT employee_id FROM employees WHERE employee_no='E-2001' LIMIT 1)
),
(
  'JP-2026-002',
  'HR Officer I',
  (SELECT department_id FROM departments WHERE dept_name='Human Resource Management Office' LIMIT 1),
  (SELECT position_id FROM positions WHERE position_name='HR Officer' LIMIT 1),
  'PLANTILLA',
  1,
  'SG 15',
  30000.00,
  38000.00,
  'Handles HR records, appointments, DTR verification support, and employee coordination.',
  'Bachelor''s degree; HR/admin experience preferred; government forms knowledge is an advantage.',
  'OPEN',
  '2026-02-01',
  '2026-02-25',
  (SELECT employee_id FROM employees WHERE employee_no='E-2002' LIMIT 1)
),
(
  'JP-2026-003',
  'IT Officer (Systems Support)',
  (SELECT department_id FROM departments WHERE dept_name='Information Technology Office' LIMIT 1),
  (SELECT position_id FROM positions WHERE position_name='IT Officer' LIMIT 1),
  'CONTRACTUAL',
  1,
  'SG 18',
  40000.00,
  52000.00,
  'Supports HRMS deployment, device connectivity, and basic network troubleshooting.',
  'Bachelor''s in IT/CS; troubleshooting; biometric device familiarity is a plus.',
  'OPEN',
  '2026-02-01',
  '2026-02-28',
  (SELECT employee_id FROM employees WHERE employee_no='E-1001' LIMIT 1)
)
ON DUPLICATE KEY UPDATE
  title = VALUES(title),
  department_id = VALUES(department_id),
  position_id = VALUES(position_id),
  employment_type = VALUES(employment_type),
  vacancies = VALUES(vacancies),
  salary_grade = VALUES(salary_grade),
  salary_range_min = VALUES(salary_range_min),
  salary_range_max = VALUES(salary_range_max),
  description = VALUES(description),
  requirements = VALUES(requirements),
  status = VALUES(status),
  open_date = VALUES(open_date),
  close_date = VALUES(close_date),
  created_by_employee_id = VALUES(created_by_employee_id),
  updated_at = CURRENT_TIMESTAMP;

INSERT INTO applicants (applicant_no, last_name, first_name, middle_name, email, mobile_no, address, birth_date)
VALUES
('APP-2026-0001','Torres','Miguel','Dizon','miguel.torres@gmail.com','09170001001','Brgy. Poblacion, Lungsod ng San Isidro','1998-05-12'),
('APP-2026-0002','Flores','Karla','Reyes','karla.flores@gmail.com','09170001002','Brgy. Maligaya, Lungsod ng San Isidro','1999-09-03'),
('APP-2026-0003','Villamor','Jhun','Santos','jhun.villamor@gmail.com','09170001003','Brgy. Pag-asa, Lungsod ng San Isidro','1997-02-20'),
('APP-2026-0004','Bautista','Shaina','Cruz','shaina.bautista@gmail.com','09170001004','Brgy. Mabini, Lungsod ng San Isidro','1996-11-14'),
('APP-2026-0005','Luna','Arnel','Garcia','arnel.luna@gmail.com','09170001005','Brgy. Masigla, Lungsod ng San Isidro','1995-07-28')
AS new
ON DUPLICATE KEY UPDATE
  last_name = new.last_name,
  first_name = new.first_name,
  middle_name = new.middle_name,
  email = new.email,
  mobile_no = new.mobile_no,
  address = new.address,
  birth_date = new.birth_date,
  updated_at = CURRENT_TIMESTAMP;

INSERT INTO job_applications (applicant_id, job_posting_id, applied_at, status, notes)
SELECT a.applicant_id, p.job_posting_id, '2026-02-10 09:10:00', 'SCREENING',
       'Walk-in applicant; complete basic requirements.'
FROM applicants a
JOIN job_postings p ON p.posting_code='JP-2026-001'
WHERE a.applicant_no='APP-2026-0001'
ON DUPLICATE KEY UPDATE status=VALUES(status), notes=VALUES(notes);

INSERT INTO job_applications (applicant_id, job_posting_id, applied_at, status, notes)
SELECT a.applicant_id, p.job_posting_id, '2026-02-10 09:25:00', 'SUBMITTED',
       'Submitted via HR window.'
FROM applicants a
JOIN job_postings p ON p.posting_code='JP-2026-001'
WHERE a.applicant_no='APP-2026-0002'
ON DUPLICATE KEY UPDATE status=VALUES(status), notes=VALUES(notes);

INSERT INTO job_applications (applicant_id, job_posting_id, applied_at, status, notes)
SELECT a.applicant_id, p.job_posting_id, '2026-02-11 10:00:00', 'INTERVIEW',
       'For technical interview; has experience with device integrations.'
FROM applicants a
JOIN job_postings p ON p.posting_code='JP-2026-003'
WHERE a.applicant_no='APP-2026-0003'
ON DUPLICATE KEY UPDATE status=VALUES(status), notes=VALUES(notes);

INSERT INTO job_applications (applicant_id, job_posting_id, applied_at, status, notes)
SELECT a.applicant_id, p.job_posting_id, '2026-02-11 14:30:00', 'OFFERED',
       'Strong background in admin work; recommended for offer.'
FROM applicants a
JOIN job_postings p ON p.posting_code='JP-2026-002'
WHERE a.applicant_no='APP-2026-0004'
ON DUPLICATE KEY UPDATE status=VALUES(status), notes=VALUES(notes);

INSERT INTO job_applications (applicant_id, job_posting_id, applied_at, status, notes)
SELECT a.applicant_id, p.job_posting_id, '2026-02-12 08:45:00', 'SHORTLISTED',
       'Shortlisted; schedule interview.'
FROM applicants a
JOIN job_postings p ON p.posting_code='JP-2026-003'
WHERE a.applicant_no='APP-2026-0005'
ON DUPLICATE KEY UPDATE status=VALUES(status), notes=VALUES(notes);

INSERT INTO interview_schedules
(
  job_application_id,
  interview_datetime,
  interview_type,
  location,
  interviewer_employee_id,
  status,
  remarks
)
SELECT
  ja.job_application_id,
  '2026-02-15 10:00:00',
  'ONSITE',
  'IT Office - Meeting Area',
  (SELECT employee_id FROM employees WHERE employee_no='E-1001' LIMIT 1),
  'SCHEDULED',
  'Bring portfolio or list of previous projects.'
FROM job_applications ja
JOIN applicants a ON a.applicant_id=ja.applicant_id
JOIN job_postings p ON p.job_posting_id=ja.job_posting_id
WHERE a.applicant_no='APP-2026-0003'
  AND p.posting_code='JP-2026-003'
  AND NOT EXISTS (
    SELECT 1
    FROM interview_schedules s
    WHERE s.job_application_id=ja.job_application_id
      AND s.interview_datetime='2026-02-15 10:00:00'
  );

INSERT INTO interview_schedules
(
  job_application_id,
  interview_datetime,
  interview_type,
  location,
  interviewer_employee_id,
  status,
  remarks
)
SELECT
  ja.job_application_id,
  '2026-02-14 09:00:00',
  'ONSITE',
  'HRMO Conference Room',
  (SELECT employee_id FROM employees WHERE employee_no='E-2001' LIMIT 1),
  'SCHEDULED',
  'Bring original copies of requirements.'
FROM job_applications ja
JOIN applicants a ON a.applicant_id=ja.applicant_id
JOIN job_postings p ON p.job_posting_id=ja.job_posting_id
WHERE a.applicant_no='APP-2026-0004'
  AND p.posting_code='JP-2026-002'
  AND NOT EXISTS (
    SELECT 1
    FROM interview_schedules s
    WHERE s.job_application_id=ja.job_application_id
      AND s.interview_datetime='2026-02-14 09:00:00'
  );

INSERT INTO interview_schedules
(
  job_application_id,
  interview_datetime,
  interview_type,
  location,
  interviewer_employee_id,
  status,
  remarks
)
SELECT
  ja.job_application_id,
  '2026-02-16 13:30:00',
  'ONLINE',
  'Google Meet',
  (SELECT employee_id FROM employees WHERE employee_no='E-1002' LIMIT 1),
  'SCHEDULED',
  'Online interview link to be sent via email.'
FROM job_applications ja
JOIN applicants a ON a.applicant_id=ja.applicant_id
JOIN job_postings p ON p.job_posting_id=ja.job_posting_id
WHERE a.applicant_no='APP-2026-0005'
  AND p.posting_code='JP-2026-003'
  AND NOT EXISTS (
    SELECT 1
    FROM interview_schedules s
    WHERE s.job_application_id=ja.job_application_id
      AND s.interview_datetime='2026-02-16 13:30:00'
  );

INSERT INTO job_offers (job_application_id, offered_at, offer_status, salary_offer, start_date, remarks)
SELECT
  ja.job_application_id,
  '2026-02-13 16:00:00',
  'PENDING',
  35000.00,
  '2026-03-01',
  'Subject to submission of complete documents.'
FROM job_applications ja
JOIN applicants a ON a.applicant_id=ja.applicant_id
JOIN job_postings p ON p.job_posting_id=ja.job_posting_id
WHERE a.applicant_no='APP-2026-0004'
  AND p.posting_code='JP-2026-002'
  AND NOT EXISTS (
    SELECT 1
    FROM job_offers o
    WHERE o.job_application_id=ja.job_application_id
  );

INSERT INTO job_offers (job_application_id, offered_at, offer_status, salary_offer, start_date, remarks)
SELECT
  ja.job_application_id,
  '2026-02-16 17:10:00',
  'PENDING',
  48000.00,
  '2026-03-05',
  'Contractual; subject to final interview result.'
FROM job_applications ja
JOIN applicants a ON a.applicant_id=ja.applicant_id
JOIN job_postings p ON p.job_posting_id=ja.job_posting_id
WHERE a.applicant_no='APP-2026-0003'
  AND p.posting_code='JP-2026-003'
  AND NOT EXISTS (
    SELECT 1
    FROM job_offers o
    WHERE o.job_application_id=ja.job_application_id
  );

SET SQL_SAFE_UPDATES = @OLD_SAFE_UPDATES;

COMMIT;
