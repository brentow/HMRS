-- Cross-module showcase data for product demonstrations.
-- All records are isolated with DEMO/SHOWCASE identifiers.

CREATE TEMPORARY TABLE IF NOT EXISTS tmp_showcase_employees (
  demo_no INT PRIMARY KEY,
  last_name VARCHAR(80) NOT NULL,
  first_name VARCHAR(80) NOT NULL,
  middle_name VARCHAR(80) NULL,
  sex VARCHAR(10) NOT NULL,
  department_name VARCHAR(120) NOT NULL,
  position_name VARCHAR(120) NOT NULL,
  appointment_type VARCHAR(60) NOT NULL,
  basic_pay DECIMAL(12,2) NOT NULL
);

DELETE FROM tmp_showcase_employees;

INSERT INTO tmp_showcase_employees
  (demo_no, last_name, first_name, middle_name, sex, department_name, position_name, appointment_type, basic_pay)
VALUES
  (1,  'Santos',    'Adrian',   'Reyes',    'MALE',   'Showcase Executive Office', 'Administrative Officer', 'Permanent',  32000.00),
  (2,  'Reyes',     'Bianca',   'Cruz',     'FEMALE', 'Showcase Human Resources',  'HR Officer',             'Permanent',  34500.00),
  (3,  'Cruz',      'Carlo',    'Mendoza',  'MALE',   'Showcase Finance',          'Accountant',             'Permanent',  37000.00),
  (4,  'Garcia',    'Diana',    'Lopez',    'FEMALE', 'Showcase Information Tech','Systems Analyst',        'Permanent',  40500.00),
  (5,  'Mendoza',   'Ethan',    'Santos',   'MALE',   'Showcase Operations',       'Operations Officer',     'Permanent',  33000.00),
  (6,  'Lopez',     'Faith',    'Garcia',   'FEMALE', 'Showcase Human Resources',  'Recruitment Specialist', 'Casual',     28000.00),
  (7,  'Aquino',    'Gabriel',  'Torres',   'MALE',   'Showcase Finance',          'Budget Officer',         'Permanent',  42000.00),
  (8,  'Torres',    'Hannah',   'Aquino',   'FEMALE', 'Showcase Information Tech','Software Developer',     'Job Order',  36000.00),
  (9,  'Ramos',     'Isaac',    'Flores',   'MALE',   'Showcase Operations',       'Records Officer',        'Permanent',  29500.00),
  (10, 'Flores',    'Julia',    'Ramos',    'FEMALE', 'Showcase Executive Office', 'Executive Assistant',    'Permanent',  38500.00),
  (11, 'Navarro',   'Kevin',    'Diaz',     'MALE',   'Showcase Human Resources',  'Training Officer',       'Permanent',  35000.00),
  (12, 'Diaz',      'Lara',     'Navarro',  'FEMALE', 'Showcase Finance',          'Payroll Officer',        'Permanent',  44000.00),
  (13, 'Castillo',  'Marco',    'Villanueva','MALE',  'Showcase Information Tech','Network Administrator',  'Contractual',39000.00),
  (14, 'Villanueva','Nina',     'Castillo', 'FEMALE', 'Showcase Operations',       'Planning Officer',       'Permanent',  46000.00),
  (15, 'Fernandez', 'Owen',     'Lim',      'MALE',   'Showcase Executive Office', 'Legal Assistant',        'Casual',     31000.00),
  (16, 'Lim',       'Paula',    'Fernandez','FEMALE', 'Showcase Human Resources',  'Benefits Officer',       'Permanent',  41500.00),
  (17, 'Mercado',   'Quinn',    'Soriano',  'OTHER',  'Showcase Finance',          'Finance Analyst',        'Job Order',  36500.00),
  (18, 'Soriano',   'Rina',     'Mercado',  'FEMALE', 'Showcase Information Tech','IT Support Specialist',  'Contractual',30000.00),
  (19, 'Bautista',  'Samuel',   'Pascual',  'MALE',   'Showcase Operations',       'Field Coordinator',      'Permanent',  34000.00),
  (20, 'Pascual',   'Trisha',   'Bautista', 'FEMALE', 'Showcase Executive Office', 'Communications Officer', 'Permanent',  43500.00);

INSERT INTO departments (dept_name, description)
SELECT DISTINCT department_name, 'Showcase department populated with demonstration records.'
FROM tmp_showcase_employees
ON DUPLICATE KEY UPDATE description=VALUES(description);

INSERT INTO positions (department_id, position_name, description)
SELECT d.department_id, s.position_name, 'Showcase position for the demonstration dataset.'
FROM tmp_showcase_employees s
INNER JOIN departments d ON d.dept_name=s.department_name
ON DUPLICATE KEY UPDATE description=VALUES(description);

INSERT INTO appointment_types (type_name)
SELECT DISTINCT appointment_type FROM tmp_showcase_employees
ON DUPLICATE KEY UPDATE type_name=VALUES(type_name);

INSERT IGNORE INTO salary_grades (salary_grade)
SELECT demo_no FROM tmp_showcase_employees;

INSERT INTO salary_steps (salary_grade, step_no, monthly_rate, effectivity_date, reference_note)
SELECT demo_no, 1, basic_pay, '2026-01-01', 'Showcase salary schedule'
FROM tmp_showcase_employees
ON DUPLICATE KEY UPDATE monthly_rate=VALUES(monthly_rate), reference_note=VALUES(reference_note);

INSERT INTO employees
  (employee_no, last_name, first_name, middle_name, sex, birth_date, civil_status,
   email, contact_number, address, department_id, position_id, appointment_type_id,
   salary_grade, step_no, hire_date, status, tin_no, gsis_bp_no, philhealth_no,
   pagibig_mid_no, emergency_contact, emergency_phone)
SELECT
  CONCAT('DEMO-', LPAD(s.demo_no, 3, '0')),
  s.last_name, s.first_name, s.middle_name, s.sex,
  DATE_ADD('1985-01-01', INTERVAL (s.demo_no * 211) DAY),
  CASE WHEN MOD(s.demo_no, 3)=0 THEN 'Single' WHEN MOD(s.demo_no, 3)=1 THEN 'Married' ELSE 'Widowed' END,
  CONCAT(LOWER(s.first_name), '.', LOWER(s.last_name), '@showcase.local'),
  CONCAT('0917', LPAD(7000000+s.demo_no, 7, '0')),
  CONCAT(s.demo_no, ' Showcase Avenue, Demo City'),
  d.department_id, p.position_id, at.appointment_type_id,
  s.demo_no, 1, DATE_SUB(CURDATE(), INTERVAL (365+s.demo_no*45) DAY), 'ACTIVE',
  CONCAT('DEMO-TIN-', LPAD(s.demo_no, 3, '0')),
  CONCAT('DEMO-GSIS-', LPAD(s.demo_no, 3, '0')),
  CONCAT('DEMO-PHIC-', LPAD(s.demo_no, 3, '0')),
  CONCAT('DEMO-HDMF-', LPAD(s.demo_no, 3, '0')),
  CONCAT('Emergency Contact ', s.demo_no), CONCAT('0918', LPAD(8000000+s.demo_no, 7, '0'))
FROM tmp_showcase_employees s
INNER JOIN departments d ON d.dept_name=s.department_name
INNER JOIN positions p ON p.department_id=d.department_id AND p.position_name=s.position_name
INNER JOIN appointment_types at ON at.type_name=s.appointment_type
ON DUPLICATE KEY UPDATE
  last_name=VALUES(last_name), first_name=VALUES(first_name), middle_name=VALUES(middle_name),
  sex=VALUES(sex), email=VALUES(email), contact_number=VALUES(contact_number), address=VALUES(address),
  department_id=VALUES(department_id), position_id=VALUES(position_id), appointment_type_id=VALUES(appointment_type_id),
  salary_grade=VALUES(salary_grade), step_no=VALUES(step_no), status='ACTIVE',
  tin_no=VALUES(tin_no), gsis_bp_no=VALUES(gsis_bp_no), philhealth_no=VALUES(philhealth_no), pagibig_mid_no=VALUES(pagibig_mid_no);

-- Scheduling and attendance: five recent weekdays plus today's time-in.
INSERT INTO shifts (shift_name, start_time, end_time, break_minutes, grace_minutes, is_overnight)
VALUES ('Showcase Standard Shift', '08:00:00', '17:00:00', 60, 10, 0)
ON DUPLICATE KEY UPDATE start_time=VALUES(start_time), end_time=VALUES(end_time), break_minutes=VALUES(break_minutes), grace_minutes=VALUES(grace_minutes);

INSERT INTO shift_assignments (employee_id, shift_id, start_date, end_date, assigned_by_user_id, status)
SELECT e.employee_id, sh.shift_id, DATE_SUB(CURDATE(), INTERVAL 365 DAY), NULL, NULL, 'ASSIGNED'
FROM employees e
INNER JOIN shifts sh ON sh.shift_name='Showcase Standard Shift'
WHERE e.employee_no LIKE 'DEMO-%'
ON DUPLICATE KEY UPDATE shift_id=VALUES(shift_id), end_date=NULL, status='ASSIGNED';

CREATE TEMPORARY TABLE IF NOT EXISTS tmp_showcase_days (day_offset INT PRIMARY KEY);
DELETE FROM tmp_showcase_days;
INSERT INTO tmp_showcase_days VALUES (1),(2),(3),(4),(5),(6),(7),(8);

INSERT IGNORE INTO attendance_logs (employee_id, device_id, log_time, log_type, source, raw_payload)
SELECT e.employee_id, NULL,
       DATE_ADD(DATE_SUB(CURDATE(), INTERVAL dd.day_offset DAY), INTERVAL (480 + MOD(s.demo_no*7+dd.day_offset*3, 31)) MINUTE),
       'IN', 'IMPORT', JSON_OBJECT('showcase', true, 'note', 'Demonstration time-in')
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-', LPAD(s.demo_no,3,'0'))
CROSS JOIN tmp_showcase_days dd
WHERE WEEKDAY(DATE_SUB(CURDATE(), INTERVAL dd.day_offset DAY)) < 5;

INSERT IGNORE INTO attendance_logs (employee_id, device_id, log_time, log_type, source, raw_payload)
SELECT e.employee_id, NULL,
       DATE_ADD(DATE_SUB(CURDATE(), INTERVAL dd.day_offset DAY), INTERVAL (1020 + MOD(s.demo_no*5+dd.day_offset, 46)) MINUTE),
       'OUT', 'IMPORT', JSON_OBJECT('showcase', true, 'note', 'Demonstration time-out')
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-', LPAD(s.demo_no,3,'0'))
CROSS JOIN tmp_showcase_days dd
WHERE WEEKDAY(DATE_SUB(CURDATE(), INTERVAL dd.day_offset DAY)) < 5
  AND NOT (MOD(s.demo_no, 9)=0 AND dd.day_offset=1);

INSERT IGNORE INTO attendance_logs (employee_id, device_id, log_time, log_type, source, raw_payload)
SELECT e.employee_id, NULL,
       DATE_ADD(CURDATE(), INTERVAL (480 + MOD(s.demo_no*4, 29)) MINUTE),
       'IN', 'IMPORT', JSON_OBJECT('showcase', true, 'note', 'Today demonstration time-in')
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-', LPAD(s.demo_no,3,'0'));

INSERT INTO attendance_remarks (employee_id, work_date, remark_type, details)
SELECT e.employee_id, DATE_SUB(CURDATE(), INTERVAL (1+MOD(s.demo_no,5)) DAY),
       CASE WHEN MOD(s.demo_no,3)=0 THEN 'OB' WHEN MOD(s.demo_no,3)=1 THEN 'WFH' ELSE 'CTO' END,
       'Showcase attendance remark'
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-', LPAD(s.demo_no,3,'0'))
ON DUPLICATE KEY UPDATE details=VALUES(details);

-- Leave balances, applications, calendar days, and downloadable sample attachments.
INSERT INTO leave_types (code, name, is_paid, default_credits_per_year, remarks, is_active)
VALUES
 ('VL','Vacation Leave',1,15.00,'Standard vacation leave',1),
 ('SL','Sick Leave',1,15.00,'Standard sick leave',1),
 ('SPL','Special Privilege Leave',1,3.00,'Special privilege leave',1)
ON DUPLICATE KEY UPDATE name=VALUES(name), is_paid=VALUES(is_paid), default_credits_per_year=VALUES(default_credits_per_year), is_active=1;

INSERT INTO leave_balances
  (employee_id, leave_type_id, `year`, opening_credits, earned, used, adjustments, as_of_date)
SELECT e.employee_id, lt.leave_type_id, YEAR(CURDATE()),
       lt.default_credits_per_year, 1.25, MOD(s.demo_no,5), IF(MOD(s.demo_no,7)=0,1.00,0.00), CURDATE()
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-', LPAD(s.demo_no,3,'0'))
CROSS JOIN leave_types lt
WHERE lt.code IN ('VL','SL','SPL')
ON DUPLICATE KEY UPDATE opening_credits=VALUES(opening_credits), earned=VALUES(earned), used=VALUES(used), adjustments=VALUES(adjustments), as_of_date=VALUES(as_of_date);

DELETE la FROM leave_applications la
INNER JOIN employees e ON e.employee_id=la.employee_id
WHERE e.employee_no LIKE 'DEMO-%' AND la.reason LIKE '[SHOWCASE]%';

INSERT INTO leave_applications
  (employee_id, leave_type_id, date_from, date_to, days_requested, reason, status, filed_at,
   decision_at, recommended_by_employee_id, approved_by_employee_id, hr_certified_by_employee_id, decision_remarks)
SELECT e.employee_id, lt.leave_type_id,
       DATE_ADD(CURDATE(), INTERVAL (s.demo_no-10) DAY), DATE_ADD(CURDATE(), INTERVAL (s.demo_no-10) DAY),
       1.00, CONCAT('[SHOWCASE] ', CASE MOD(s.demo_no,4) WHEN 0 THEN 'Medical appointment' WHEN 1 THEN 'Family commitment' WHEN 2 THEN 'Personal errand' ELSE 'Scheduled vacation' END),
       ELT(MOD(s.demo_no-1,6)+1,'SUBMITTED','RECOMMENDED','APPROVED','REJECTED','DRAFT','APPROVED'),
       DATE_SUB(NOW(), INTERVAL s.demo_no HOUR),
       IF(MOD(s.demo_no-1,6) IN (2,3,5), NOW(), NULL),
       IF(MOD(s.demo_no-1,6) IN (1,2,3,5), reviewer.employee_id, NULL),
       IF(MOD(s.demo_no-1,6) IN (2,3,5), reviewer.employee_id, NULL),
       IF(MOD(s.demo_no-1,6) IN (2,5), reviewer.employee_id, NULL),
       CASE WHEN MOD(s.demo_no-1,6)=3 THEN 'Showcase rejection: insufficient supporting details.' ELSE 'Showcase workflow record.' END
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-', LPAD(s.demo_no,3,'0'))
INNER JOIN leave_types lt ON lt.code=ELT(MOD(s.demo_no-1,3)+1,'VL','SL','SPL')
CROSS JOIN (SELECT MIN(employee_id) AS employee_id FROM employees WHERE employee_no LIKE 'DEMO-%') reviewer;

INSERT IGNORE INTO leave_application_days (leave_application_id, leave_date, day_fraction, half_day_part)
SELECT la.leave_application_id, la.date_from, 1.00, NULL
FROM leave_applications la
INNER JOIN employees e ON e.employee_id=la.employee_id
WHERE e.employee_no LIKE 'DEMO-%' AND la.reason LIKE '[SHOWCASE]%';

INSERT INTO leave_documents
  (leave_application_id, file_name, file_path, file_blob, file_size, uploaded_at, uploaded_by_employee_id)
SELECT la.leave_application_id, CONCAT(e.employee_no, '-supporting-note.txt'), 'showcase://leave-supporting-note.txt',
       CAST(CONCAT('Showcase leave attachment for ', e.employee_no, '.') AS BINARY),
       CHAR_LENGTH(CONCAT('Showcase leave attachment for ', e.employee_no, '.')), NOW(), e.employee_id
FROM leave_applications la
INNER JOIN employees e ON e.employee_id=la.employee_id
WHERE e.employee_no LIKE 'DEMO-%' AND la.reason LIKE '[SHOWCASE]%';

-- Payroll: 20 runs with government contributions, tax, attendance, loan, and other deductions.
INSERT INTO payroll_periods (period_code, date_from, date_to, pay_date, status)
VALUES ('SHOWCASE-AUG-2026', DATE_SUB(CURDATE(), INTERVAL 8 DAY), DATE_SUB(CURDATE(), INTERVAL 1 DAY), CURDATE(), 'POSTED')
ON DUPLICATE KEY UPDATE date_from=VALUES(date_from), date_to=VALUES(date_to), pay_date=VALUES(pay_date), status='POSTED';

SET @showcase_period_id := (SELECT payroll_period_id FROM payroll_periods WHERE period_code='SHOWCASE-AUG-2026' LIMIT 1);

DELETE pc FROM payroll_concerns pc INNER JOIN payroll_runs pr ON pr.payroll_run_id=pc.payroll_run_id WHERE pr.payroll_period_id=@showcase_period_id;
DELETE prl FROM payslip_releases prl INNER JOIN payroll_runs pr ON pr.payroll_run_id=prl.payroll_run_id WHERE pr.payroll_period_id=@showcase_period_id;
DELETE pri FROM payroll_run_items pri INNER JOIN payroll_runs pr ON pr.payroll_run_id=pri.payroll_run_id WHERE pr.payroll_period_id=@showcase_period_id;

INSERT INTO payroll_runs
  (payroll_period_id, employee_id, basic_pay, allowances, overtime_pay, other_earnings, gross_pay, deductions_total, net_pay, status, generated_at)
SELECT @showcase_period_id, e.employee_id, s.basic_pay,
       1500.00 + (s.demo_no*50), MOD(s.demo_no,4)*375.00, IF(MOD(s.demo_no,5)=0,500.00,0.00),
       s.basic_pay + 1500.00 + (s.demo_no*50) + MOD(s.demo_no,4)*375.00 + IF(MOD(s.demo_no,5)=0,500.00,0.00),
       0.00, 0.00, 'RELEASED', NOW()
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-', LPAD(s.demo_no,3,'0'))
ON DUPLICATE KEY UPDATE basic_pay=VALUES(basic_pay), allowances=VALUES(allowances), overtime_pay=VALUES(overtime_pay),
 other_earnings=VALUES(other_earnings), gross_pay=VALUES(gross_pay), status='RELEASED', generated_at=VALUES(generated_at);

INSERT INTO payroll_run_items (payroll_run_id, item_type, code, description, amount)
SELECT pr.payroll_run_id, 'EARNING', 'BASIC', 'Basic salary', pr.basic_pay FROM payroll_runs pr WHERE pr.payroll_period_id=@showcase_period_id
UNION ALL
SELECT pr.payroll_run_id, 'EARNING', 'ALLOWANCE', 'Representation and transportation allowance', pr.allowances FROM payroll_runs pr WHERE pr.payroll_period_id=@showcase_period_id
UNION ALL
SELECT pr.payroll_run_id, 'EARNING', 'OVERTIME', 'Approved overtime pay', pr.overtime_pay FROM payroll_runs pr WHERE pr.payroll_period_id=@showcase_period_id AND pr.overtime_pay>0
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', IF(at.type_name IN ('Job Order','Contractual'),'SSS','GSIS'),
       IF(at.type_name IN ('Job Order','Contractual'),'SSS employee contribution','GSIS employee contribution'),
       IF(at.type_name IN ('Job Order','Contractual'), LEAST(1000.00, GREATEST(200.00, ROUND(pr.basic_pay*0.05,2))), ROUND(pr.basic_pay*0.09,2))
FROM payroll_runs pr INNER JOIN employees e ON e.employee_id=pr.employee_id LEFT JOIN appointment_types at ON at.appointment_type_id=e.appointment_type_id WHERE pr.payroll_period_id=@showcase_period_id
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'PHILHEALTH', 'PhilHealth employee contribution', LEAST(2500.00,GREATEST(250.00,ROUND(pr.basic_pay*0.025,2))) FROM payroll_runs pr WHERE pr.payroll_period_id=@showcase_period_id
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'PAGIBIG', 'Pag-IBIG employee contribution', 200.00 FROM payroll_runs pr WHERE pr.payroll_period_id=@showcase_period_id
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'WITHHOLDING_TAX', 'Withholding tax', ROUND(GREATEST(0,pr.basic_pay-20833)*0.12,2) FROM payroll_runs pr WHERE pr.payroll_period_id=@showcase_period_id
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'ABSENCE', 'Absence deduction', ROUND(pr.basic_pay/22*0.5,2) FROM payroll_runs pr INNER JOIN employees e ON e.employee_id=pr.employee_id WHERE pr.payroll_period_id=@showcase_period_id AND MOD(CAST(SUBSTRING(e.employee_no,6) AS UNSIGNED),4)=0
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'DTR_MINUS', 'Late / undertime deduction', ROUND(pr.basic_pay/22/8/60*(5+MOD(CAST(SUBSTRING(e.employee_no,6) AS UNSIGNED)*7,40)),2) FROM payroll_runs pr INNER JOIN employees e ON e.employee_id=pr.employee_id WHERE pr.payroll_period_id=@showcase_period_id
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'LOAN', 'Government or salary loan', 750.00+CAST(SUBSTRING(e.employee_no,6) AS UNSIGNED)*50 FROM payroll_runs pr INNER JOIN employees e ON e.employee_id=pr.employee_id WHERE pr.payroll_period_id=@showcase_period_id AND MOD(CAST(SUBSTRING(e.employee_no,6) AS UNSIGNED),3)=0
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'OTHER_DEDUCTION', 'Cooperative / other deduction', 250.00 FROM payroll_runs pr INNER JOIN employees e ON e.employee_id=pr.employee_id WHERE pr.payroll_period_id=@showcase_period_id AND MOD(CAST(SUBSTRING(e.employee_no,6) AS UNSIGNED),5)=0;

UPDATE payroll_runs pr
SET pr.deductions_total=(SELECT COALESCE(SUM(pri.amount),0) FROM payroll_run_items pri WHERE pri.payroll_run_id=pr.payroll_run_id AND pri.item_type='DEDUCTION'),
    pr.net_pay=pr.gross_pay-(SELECT COALESCE(SUM(pri.amount),0) FROM payroll_run_items pri WHERE pri.payroll_run_id=pr.payroll_run_id AND pri.item_type='DEDUCTION')
WHERE pr.payroll_period_id=@showcase_period_id;

INSERT INTO payslip_releases (payroll_run_id, released_at, released_by_employee_id, remarks)
SELECT pr.payroll_run_id, NOW(), actor.employee_id, 'Showcase payslip released and ready to download.'
FROM payroll_runs pr
CROSS JOIN (SELECT MIN(employee_id) AS employee_id FROM employees WHERE employee_no LIKE 'DEMO-%') actor
WHERE pr.payroll_period_id=@showcase_period_id;

INSERT INTO payroll_concerns (payroll_run_id, employee_id, concern_details, status, created_at, resolution_notes)
SELECT pr.payroll_run_id, pr.employee_id,
       CONCAT('[SHOWCASE] Sample payroll inquiry for ', e.employee_no),
       ELT(MOD(CAST(SUBSTRING(e.employee_no,6) AS UNSIGNED),3)+1,'OPEN','IN_REVIEW','RESOLVED'),
       NOW(), IF(MOD(CAST(SUBSTRING(e.employee_no,6) AS UNSIGNED),3)=2,'Verified during showcase payroll review.',NULL)
FROM payroll_runs pr INNER JOIN employees e ON e.employee_id=pr.employee_id
WHERE pr.payroll_period_id=@showcase_period_id AND CAST(SUBSTRING(e.employee_no,6) AS UNSIGNED)<=6;

-- Recruitment pipeline: postings, 20 candidates, applications, interviews, and offers.
INSERT INTO job_postings
  (posting_code,title,department_id,position_id,employment_type,vacancies,salary_grade,salary_range_min,salary_range_max,description,requirements,status,open_date,close_date,created_by_employee_id)
SELECT CONCAT('SHOWCASE-JOB-',LPAD(x.n,2,'0')), x.title, d.department_id, p.position_id, x.employment_type, x.vacancies,
       CONCAT('SG ',x.n+8), 28000+x.n*3000, 42000+x.n*4000,
       'Showcase vacancy used to demonstrate the recruitment pipeline.', 'Education, experience, eligibility, and competency requirements.',
       IF(x.n=4,'CLOSED','OPEN'), DATE_SUB(CURDATE(),INTERVAL 15 DAY), DATE_ADD(CURDATE(),INTERVAL 30 DAY), actor.employee_id
FROM (
 SELECT 1 n,'HR Assistant' title,'Showcase Human Resources' dept,'Recruitment Specialist' pos,'CASUAL' employment_type,2 vacancies
 UNION ALL SELECT 2,'Junior Developer','Showcase Information Tech','Software Developer','JOB_ORDER',3
 UNION ALL SELECT 3,'Accounting Clerk','Showcase Finance','Accountant','PLANTILLA',1
 UNION ALL SELECT 4,'Field Assistant','Showcase Operations','Field Coordinator','CONTRACTUAL',4
) x
INNER JOIN departments d ON d.dept_name=x.dept
INNER JOIN positions p ON p.department_id=d.department_id AND p.position_name=x.pos
CROSS JOIN (SELECT MIN(employee_id) employee_id FROM employees WHERE employee_no LIKE 'DEMO-%') actor
ON DUPLICATE KEY UPDATE title=VALUES(title), status=VALUES(status), close_date=VALUES(close_date), vacancies=VALUES(vacancies);

INSERT INTO applicants (applicant_no,last_name,first_name,middle_name,email,mobile_no,address,birth_date)
SELECT CONCAT('SHOWCASE-APP-',LPAD(s.demo_no,3,'0')),
       CONCAT('Candidate',LPAD(s.demo_no,2,'0')), CONCAT('Applicant',LPAD(s.demo_no,2,'0')), 'Demo',
       CONCAT('candidate',LPAD(s.demo_no,2,'0'),'@showcase.local'), CONCAT('0995',LPAD(6000000+s.demo_no,7,'0')),
       CONCAT(s.demo_no,' Recruitment Lane, Demo City'), DATE_ADD('1990-01-01',INTERVAL s.demo_no*137 DAY)
FROM tmp_showcase_employees s
ON DUPLICATE KEY UPDATE email=VALUES(email), mobile_no=VALUES(mobile_no), address=VALUES(address);

INSERT INTO job_applications (applicant_id,job_posting_id,applied_at,status,notes)
SELECT a.applicant_id, jp.job_posting_id, DATE_SUB(NOW(),INTERVAL s.demo_no DAY),
       ELT(MOD(s.demo_no-1,7)+1,'SUBMITTED','SCREENING','SHORTLISTED','INTERVIEW','OFFERED','HIRED','REJECTED'),
       '[SHOWCASE] Complete sample application record.'
FROM tmp_showcase_employees s
INNER JOIN applicants a ON a.applicant_no=CONCAT('SHOWCASE-APP-',LPAD(s.demo_no,3,'0'))
INNER JOIN job_postings jp ON jp.posting_code=CONCAT('SHOWCASE-JOB-',LPAD(MOD(s.demo_no-1,4)+1,2,'0'))
ON DUPLICATE KEY UPDATE status=VALUES(status), notes=VALUES(notes);

DELETE i FROM interview_schedules i INNER JOIN job_applications ja ON ja.job_application_id=i.job_application_id INNER JOIN applicants a ON a.applicant_id=ja.applicant_id WHERE a.applicant_no LIKE 'SHOWCASE-APP-%';
DELETE o FROM job_offers o INNER JOIN job_applications ja ON ja.job_application_id=o.job_application_id INNER JOIN applicants a ON a.applicant_id=ja.applicant_id WHERE a.applicant_no LIKE 'SHOWCASE-APP-%';

INSERT INTO interview_schedules (job_application_id,interview_datetime,interview_type,location,interviewer_employee_id,status,remarks)
SELECT ja.job_application_id, DATE_ADD(NOW(),INTERVAL s.demo_no DAY), ELT(MOD(s.demo_no,3)+1,'PHONE','ONLINE','ONSITE'),
       IF(MOD(s.demo_no,3)=1,'Microsoft Teams','Showcase Conference Room'), actor.employee_id,
       IF(s.demo_no<=5,'DONE','SCHEDULED'),'[SHOWCASE] Interview evaluation and schedule.'
FROM tmp_showcase_employees s
INNER JOIN applicants a ON a.applicant_no=CONCAT('SHOWCASE-APP-',LPAD(s.demo_no,3,'0'))
INNER JOIN job_applications ja ON ja.applicant_id=a.applicant_id
CROSS JOIN (SELECT MIN(employee_id) employee_id FROM employees WHERE employee_no LIKE 'DEMO-%') actor
WHERE s.demo_no<=12;

INSERT INTO job_offers (job_application_id,offered_at,offer_status,salary_offer,start_date,remarks)
SELECT ja.job_application_id,NOW(),ELT(MOD(s.demo_no,3)+1,'PENDING','ACCEPTED','DECLINED'),28000+s.demo_no*750,
       DATE_ADD(CURDATE(),INTERVAL 30 DAY),'[SHOWCASE] Demonstration job offer.'
FROM tmp_showcase_employees s
INNER JOIN applicants a ON a.applicant_no=CONCAT('SHOWCASE-APP-',LPAD(s.demo_no,3,'0'))
INNER JOIN job_applications ja ON ja.applicant_id=a.applicant_id
WHERE s.demo_no BETWEEN 5 AND 10;

-- Training: three programs with 20 enrollments and mixed completion states.
INSERT INTO training_courses (course_name,description)
VALUES
 ('SHOWCASE: Data Privacy and Cybersecurity','Mandatory awareness and incident-response training.'),
 ('SHOWCASE: Supervisory Development','Leadership, coaching, and performance management workshop.'),
 ('SHOWCASE: Government Payroll Fundamentals','Payroll controls, statutory deductions, and reconciliation.')
ON DUPLICATE KEY UPDATE description=VALUES(description);

INSERT INTO training_sessions (course_id,session_date,trainer_user_id,location)
SELECT course_id, DATE_ADD(CURDATE(),INTERVAL CASE course_name WHEN 'SHOWCASE: Data Privacy and Cybersecurity' THEN 7 WHEN 'SHOWCASE: Supervisory Development' THEN 14 ELSE 21 END DAY),
       NULL, 'Showcase Training Hall'
FROM training_courses WHERE course_name LIKE 'SHOWCASE:%'
ON DUPLICATE KEY UPDATE location=VALUES(location);

INSERT INTO training_enrollments (session_id,employee_id,status)
SELECT ts.session_id,e.employee_id,ELT(MOD(s.demo_no-1,4)+1,'REQUESTED','ENROLLED','COMPLETED','PENDING')
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-',LPAD(s.demo_no,3,'0'))
INNER JOIN training_courses tc ON tc.course_name=ELT(MOD(s.demo_no-1,3)+1,'SHOWCASE: Data Privacy and Cybersecurity','SHOWCASE: Supervisory Development','SHOWCASE: Government Payroll Fundamentals')
INNER JOIN training_sessions ts ON ts.course_id=tc.course_id
ON DUPLICATE KEY UPDATE status=VALUES(status);

-- Performance: one active cycle, 20 reviews, goals, and scored criteria.
INSERT INTO performance_cycles (cycle_code,name,start_date,end_date,status,created_by_employee_id)
SELECT 'SHOWCASE-2026','2026 Showcase Performance Cycle','2026-01-01','2026-12-31','OPEN',MIN(employee_id)
FROM employees WHERE employee_no LIKE 'DEMO-%'
ON DUPLICATE KEY UPDATE name=VALUES(name),status='OPEN';

SET @showcase_cycle_id := (SELECT performance_cycle_id FROM performance_cycles WHERE cycle_code='SHOWCASE-2026' LIMIT 1);
DELETE pri FROM performance_review_items pri INNER JOIN performance_reviews pr ON pr.performance_review_id=pri.performance_review_id WHERE pr.performance_cycle_id=@showcase_cycle_id;
DELETE FROM performance_reviews WHERE performance_cycle_id=@showcase_cycle_id;
DELETE FROM performance_goals WHERE performance_cycle_id=@showcase_cycle_id;

INSERT INTO performance_goals (performance_cycle_id,employee_id,title,description,weight,target_metric)
SELECT @showcase_cycle_id,e.employee_id,
       CONCAT('Service delivery objective ',s.demo_no),'Deliver measurable improvements for the assigned office.',100.00,
       CONCAT('Complete at least ',90+MOD(s.demo_no,10),'% of committed outputs on time.')
FROM tmp_showcase_employees s INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-',LPAD(s.demo_no,3,'0'));

INSERT INTO performance_reviews (performance_cycle_id,employee_id,reviewer_employee_id,overall_rating,status,remarks,submitted_at,decided_at)
SELECT @showcase_cycle_id,e.employee_id,reviewer.employee_id,3.20+MOD(s.demo_no,9)*0.18,
       ELT(MOD(s.demo_no-1,4)+1,'DRAFT','SUBMITTED','APPROVED','REJECTED'),
       '[SHOWCASE] Demonstration review with competency and output scores.',
       IF(MOD(s.demo_no-1,4)>0,NOW(),NULL),IF(MOD(s.demo_no-1,4) IN (2,3),NOW(),NULL)
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-',LPAD(s.demo_no,3,'0'))
CROSS JOIN (SELECT MIN(employee_id) employee_id FROM employees WHERE employee_no LIKE 'DEMO-%') reviewer;

INSERT INTO performance_review_items (performance_review_id,criteria,weight,score,comments)
SELECT pr.performance_review_id,'Quality and timeliness of outputs',60.00,3.00+MOD(s.demo_no,10)*0.18,'Consistent delivery against committed targets.'
FROM performance_reviews pr INNER JOIN employees e ON e.employee_id=pr.employee_id INNER JOIN tmp_showcase_employees s ON e.employee_no=CONCAT('DEMO-',LPAD(s.demo_no,3,'0')) WHERE pr.performance_cycle_id=@showcase_cycle_id
UNION ALL
SELECT pr.performance_review_id,'Core and leadership competencies',40.00,3.10+MOD(s.demo_no,8)*0.20,'Demonstrates teamwork, accountability, and service focus.'
FROM performance_reviews pr INNER JOIN employees e ON e.employee_id=pr.employee_id INNER JOIN tmp_showcase_employees s ON e.employee_no=CONCAT('DEMO-',LPAD(s.demo_no,3,'0')) WHERE pr.performance_cycle_id=@showcase_cycle_id;

-- System verifier: three checklist documents per demo employee.
DELETE c FROM employee_document_checklist c INNER JOIN employees e ON e.employee_id=c.employee_id WHERE e.employee_no LIKE 'DEMO-%';

INSERT INTO employee_document_checklist
 (employee_id,position_name,employment_type,document_code,document_name,document_tier,is_required,status,submitted_date,expiry_date,verified_date,verified_by,remarks,file_name,file_path,file_blob,file_size,uploaded_at,uploaded_by_employee_id)
SELECT e.employee_id,s.position_name,s.appointment_type,docs.code,docs.name,docs.tier,1,
       ELT(MOD(s.demo_no+docs.tier,4)+1,'verified','submitted','not_submitted','expired'),
       IF(MOD(s.demo_no+docs.tier,4)<>2,DATE_SUB(CURDATE(),INTERVAL s.demo_no DAY),NULL),
       IF(docs.tier=2,DATE_ADD(CURDATE(),INTERVAL (s.demo_no-10)*30 DAY),NULL),
       IF(MOD(s.demo_no+docs.tier,4)=0,CURDATE(),NULL),
       IF(MOD(s.demo_no+docs.tier,4)=0,'Showcase HR Verifier',NULL),
       'Showcase employee compliance document.',
       IF(MOD(s.demo_no+docs.tier,4)<>2,CONCAT(e.employee_no,'-',LOWER(docs.code),'.txt'),NULL),
       IF(MOD(s.demo_no+docs.tier,4)<>2,CONCAT('showcase://',LOWER(docs.code),'.txt'),NULL),
       IF(MOD(s.demo_no+docs.tier,4)<>2,CAST(CONCAT('Showcase document ',docs.name,' for ',e.employee_no) AS BINARY),NULL),
       IF(MOD(s.demo_no+docs.tier,4)<>2,CHAR_LENGTH(CONCAT('Showcase document ',docs.name,' for ',e.employee_no)),NULL),
       IF(MOD(s.demo_no+docs.tier,4)<>2,NOW(),NULL),
       IF(MOD(s.demo_no+docs.tier,4)<>2,e.employee_id,NULL)
FROM tmp_showcase_employees s
INNER JOIN employees e ON e.employee_no=CONCAT('DEMO-',LPAD(s.demo_no,3,'0'))
CROSS JOIN (
 SELECT 'PDS' code,'Personal Data Sheet' name,1 tier
 UNION ALL SELECT 'NBI','NBI Clearance',2
 UNION ALL SELECT 'TOR','Transcript of Records',3
) docs;

-- Beneficiary verification queue: 20 realistic records with mixed statuses.
INSERT INTO BeneficiaryStaging
 (CivilRegistryID,FirstName,LastName,MiddleName,Address,VerificationStatus,ImportedAt,Remarks,ApprovedRejectedAt,ResidentsId,BeneficiaryId,FullName,Sex,DateOfBirth,Age,MaritalStatus,IsPwd,PwdIdNo,DisabilityType,CauseOfDisability,IsSenior,SeniorIdNo)
SELECT CONCAT('SHOWCASE-CR-',LPAD(s.demo_no,4,'0')),
       CONCAT('Beneficiary',LPAD(s.demo_no,2,'0')),CONCAT('Household',LPAD(s.demo_no,2,'0')),'Demo',
       CONCAT(s.demo_no,' Community Road, Demo Barangay'),MOD(s.demo_no,3),NOW(),
       CASE MOD(s.demo_no,3) WHEN 0 THEN 'Pending showcase verification.' WHEN 1 THEN 'Validated against showcase registry.' ELSE 'Showcase record requires correction.' END,
       IF(MOD(s.demo_no,3)=0,NULL,NOW()),900000+s.demo_no,CONCAT('SHOWCASE-BEN-',LPAD(s.demo_no,4,'0')),
       CONCAT('Beneficiary',LPAD(s.demo_no,2,'0'),' Demo Household',LPAD(s.demo_no,2,'0')),
       IF(MOD(s.demo_no,2)=0,'Female','Male'),DATE_FORMAT(DATE_ADD('1950-01-01',INTERVAL s.demo_no*401 DAY),'%Y-%m-%d'),
       CAST(TIMESTAMPDIFF(YEAR,DATE_ADD('1950-01-01',INTERVAL s.demo_no*401 DAY),CURDATE()) AS CHAR),
       IF(MOD(s.demo_no,2)=0,'Married','Single'),IF(MOD(s.demo_no,5)=0,1,0),
       IF(MOD(s.demo_no,5)=0,CONCAT('PWD-DEMO-',LPAD(s.demo_no,3,'0')),NULL),
       IF(MOD(s.demo_no,5)=0,'Mobility impairment',NULL),IF(MOD(s.demo_no,5)=0,'Showcase medical condition',NULL),
       IF(TIMESTAMPDIFF(YEAR,DATE_ADD('1950-01-01',INTERVAL s.demo_no*401 DAY),CURDATE())>=60,1,0),
       IF(TIMESTAMPDIFF(YEAR,DATE_ADD('1950-01-01',INTERVAL s.demo_no*401 DAY),CURDATE())>=60,CONCAT('SC-DEMO-',LPAD(s.demo_no,3,'0')),NULL)
FROM tmp_showcase_employees s
ON DUPLICATE KEY UPDATE FirstName=VALUES(FirstName),LastName=VALUES(LastName),Address=VALUES(Address),VerificationStatus=VALUES(VerificationStatus),Remarks=VALUES(Remarks),BeneficiaryId=VALUES(BeneficiaryId),FullName=VALUES(FullName);

DROP TEMPORARY TABLE IF EXISTS tmp_showcase_days;
DROP TEMPORARY TABLE IF EXISTS tmp_showcase_employees;
