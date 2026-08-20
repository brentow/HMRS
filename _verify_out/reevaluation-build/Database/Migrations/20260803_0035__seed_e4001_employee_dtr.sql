-- Employee-side August 2026 showcase DTR for the linked account E-4001.
-- IMPORT punches and [SHOWCASE USER DTR] records are isolated from real biometric/manual data.

INSERT INTO shifts (shift_name,start_time,end_time,break_minutes,grace_minutes,is_overnight)
VALUES ('Showcase Standard Shift','08:00:00','17:00:00',60,10,0)
ON DUPLICATE KEY UPDATE start_time=VALUES(start_time),end_time=VALUES(end_time),break_minutes=VALUES(break_minutes),grace_minutes=VALUES(grace_minutes);

INSERT INTO shift_assignments (employee_id,shift_id,start_date,end_date,assigned_by_user_id,status)
SELECT e.employee_id,s.shift_id,'2026-08-01',NULL,NULL,'ASSIGNED'
FROM employees e
INNER JOIN shifts s ON s.shift_name='Showcase Standard Shift'
WHERE e.employee_no='E-4001'
ON DUPLICATE KEY UPDATE shift_id=VALUES(shift_id),end_date=NULL,status='ASSIGNED';

DELETE al
FROM attendance_logs al
INNER JOIN employees e ON e.employee_id=al.employee_id
WHERE e.employee_no='E-4001'
  AND al.log_time >= '2026-08-01'
  AND al.log_time < '2026-09-01'
  AND al.source='IMPORT';

CREATE TEMPORARY TABLE IF NOT EXISTS tmp_e4001_dtr (
  work_date DATE NOT NULL,
  time_in_minute INT NULL,
  time_out_minute INT NULL,
  PRIMARY KEY (work_date)
);

DELETE FROM tmp_e4001_dtr;
INSERT INTO tmp_e4001_dtr (work_date,time_in_minute,time_out_minute)
VALUES
 ('2026-08-03',475,1025),
 ('2026-08-04',505,1020),
 ('2026-08-05',480,945),
 ('2026-08-06',478,1140),
 ('2026-08-11',482,NULL),
 ('2026-08-12',480,1020),
 ('2026-08-13',510,980),
 ('2026-08-14',480,1110),
 ('2026-08-17',475,1020),
 ('2026-08-20',480,1030),
 ('2026-08-21',505,1020),
 ('2026-08-24',480,1120),
 ('2026-08-25',480,1020),
 ('2026-08-26',480,950),
 ('2026-08-27',475,1020),
 ('2026-08-28',480,1035);

INSERT IGNORE INTO attendance_logs (employee_id,device_id,log_time,log_type,source,raw_payload)
SELECT e.employee_id,NULL,DATE_ADD(d.work_date,INTERVAL d.time_in_minute MINUTE),'IN','IMPORT',
       JSON_OBJECT('showcase',true,'employee_side',true,'note','E-4001 employee DTR time-in')
FROM tmp_e4001_dtr d CROSS JOIN employees e
WHERE e.employee_no='E-4001' AND d.time_in_minute IS NOT NULL;

INSERT IGNORE INTO attendance_logs (employee_id,device_id,log_time,log_type,source,raw_payload)
SELECT e.employee_id,NULL,DATE_ADD(d.work_date,INTERVAL d.time_out_minute MINUTE),'OUT','IMPORT',
       JSON_OBJECT('showcase',true,'employee_side',true,'note','E-4001 employee DTR time-out')
FROM tmp_e4001_dtr d CROSS JOIN employees e
WHERE e.employee_no='E-4001' AND d.time_out_minute IS NOT NULL;

INSERT INTO attendance_remarks (employee_id,work_date,remark_type,details)
SELECT e.employee_id,x.work_date,'OTHER',x.details
FROM employees e
CROSS JOIN (
 SELECT DATE('2026-08-04') work_date,'[SHOWCASE USER DTR] Late arrival' details
 UNION ALL SELECT '2026-08-05','[SHOWCASE USER DTR] Undertime / early departure'
 UNION ALL SELECT '2026-08-06','[SHOWCASE USER DTR] Approved overtime work'
 UNION ALL SELECT '2026-08-07','[SHOWCASE USER DTR] ABSENT - unexcused absence'
 UNION ALL SELECT '2026-08-11','[SHOWCASE USER DTR] Incomplete punch - missing time-out'
 UNION ALL SELECT '2026-08-13','[SHOWCASE USER DTR] Late arrival and undertime'
 UNION ALL SELECT '2026-08-14','[SHOWCASE USER DTR] Approved overtime work'
 UNION ALL SELECT '2026-08-19','[SHOWCASE USER DTR] ABSENT - unexcused absence'
 UNION ALL SELECT '2026-08-21','[SHOWCASE USER DTR] Late arrival'
 UNION ALL SELECT '2026-08-24','[SHOWCASE USER DTR] Approved overtime work'
 UNION ALL SELECT '2026-08-26','[SHOWCASE USER DTR] Undertime / early departure'
) x
WHERE e.employee_no='E-4001'
ON DUPLICATE KEY UPDATE details=VALUES(details);

DELETE la
FROM leave_applications la
INNER JOIN employees e ON e.employee_id=la.employee_id
WHERE e.employee_no='E-4001'
  AND la.reason IN ('[SHOWCASE USER DTR] Approved vacation leave','[SHOWCASE USER DTR] Approved sick leave');

INSERT INTO leave_applications
 (employee_id,leave_type_id,date_from,date_to,days_requested,reason,status,filed_at,decision_at,recommended_by_employee_id,approved_by_employee_id,hr_certified_by_employee_id,decision_remarks)
SELECT e.employee_id,lt.leave_type_id,x.leave_date,x.leave_date,1.00,x.reason,'APPROVED',DATE_SUB(x.leave_date,INTERVAL 3 DAY),x.leave_date,actor.employee_id,actor.employee_id,actor.employee_id,
       '[SHOWCASE USER DTR] Approved by HR; no absence deduction.'
FROM employees e
CROSS JOIN (
 SELECT DATE('2026-08-10') leave_date,'VL' leave_code,'[SHOWCASE USER DTR] Approved vacation leave' reason
 UNION ALL SELECT '2026-08-18','SL','[SHOWCASE USER DTR] Approved sick leave'
) x
INNER JOIN leave_types lt ON lt.code=x.leave_code
CROSS JOIN (SELECT MIN(employee_id) employee_id FROM employees WHERE employee_no LIKE 'DEMO-%') actor
WHERE e.employee_no='E-4001';

INSERT IGNORE INTO leave_application_days (leave_application_id,leave_date,day_fraction,half_day_part)
SELECT la.leave_application_id,la.date_from,1.00,NULL
FROM leave_applications la
INNER JOIN employees e ON e.employee_id=la.employee_id
WHERE e.employee_no='E-4001'
  AND la.reason IN ('[SHOWCASE USER DTR] Approved vacation leave','[SHOWCASE USER DTR] Approved sick leave');

INSERT INTO leave_balances (employee_id,leave_type_id,`year`,opening_credits,earned,used,adjustments,as_of_date)
SELECT e.employee_id,lt.leave_type_id,2026,lt.default_credits_per_year,0,0,0,'2026-08-31'
FROM employees e CROSS JOIN leave_types lt
WHERE e.employee_no='E-4001' AND lt.code IN ('VL','SL')
ON DUPLICATE KEY UPDATE as_of_date=VALUES(as_of_date);

UPDATE leave_balances lb
INNER JOIN employees e ON e.employee_id=lb.employee_id
SET lb.used=(
      SELECT COALESCE(SUM(la.days_requested),0)
      FROM leave_applications la
      WHERE la.employee_id=lb.employee_id
        AND la.leave_type_id=lb.leave_type_id
        AND YEAR(la.date_from)=lb.year
        AND la.status='APPROVED'
    ),
    lb.as_of_date='2026-08-31'
WHERE e.employee_no='E-4001' AND lb.year=2026;

-- Add a matching demonstration payroll so HR/Admin and the employee can inspect absence/DTR deductions.
SET @showcase_period_id := (SELECT payroll_period_id FROM payroll_periods WHERE period_code='SHOWCASE-AUG-2026' LIMIT 1);

INSERT INTO payroll_runs
 (payroll_period_id,employee_id,basic_pay,allowances,overtime_pay,other_earnings,gross_pay,deductions_total,net_pay,status,generated_at)
SELECT @showcase_period_id,e.employee_id,28000.00,1200.00,1250.00,0.00,30450.00,0.00,0.00,'RELEASED',NOW()
FROM employees e WHERE e.employee_no='E-4001'
ON DUPLICATE KEY UPDATE basic_pay=VALUES(basic_pay),allowances=VALUES(allowances),overtime_pay=VALUES(overtime_pay),other_earnings=VALUES(other_earnings),gross_pay=VALUES(gross_pay),status='RELEASED',generated_at=NOW();

SET @e4001_run_id := (
 SELECT pr.payroll_run_id FROM payroll_runs pr
 INNER JOIN employees e ON e.employee_id=pr.employee_id
 WHERE pr.payroll_period_id=@showcase_period_id AND e.employee_no='E-4001' LIMIT 1
);

DELETE FROM payroll_run_items WHERE payroll_run_id=@e4001_run_id;
DELETE FROM payslip_releases WHERE payroll_run_id=@e4001_run_id;

INSERT INTO payroll_run_items (payroll_run_id,item_type,code,description,amount)
VALUES
 (@e4001_run_id,'EARNING','BASIC','Basic salary',28000.00),
 (@e4001_run_id,'EARNING','ALLOWANCE','Representation and transportation allowance',1200.00),
 (@e4001_run_id,'EARNING','OVERTIME','Approved overtime pay',1250.00),
 (@e4001_run_id,'DEDUCTION','GSIS','GSIS employee contribution',2520.00),
 (@e4001_run_id,'DEDUCTION','PHILHEALTH','PhilHealth employee contribution',700.00),
 (@e4001_run_id,'DEDUCTION','PAGIBIG','Pag-IBIG employee contribution',200.00),
 (@e4001_run_id,'DEDUCTION','WITHHOLDING_TAX','Withholding tax',900.00),
 (@e4001_run_id,'DEDUCTION','ABSENCE','Two unexcused absence days from employee DTR',2545.45),
 (@e4001_run_id,'DEDUCTION','DTR_MINUS','Late and undertime deduction from employee DTR',623.11);

UPDATE payroll_runs pr
SET pr.deductions_total=(SELECT COALESCE(SUM(pri.amount),0) FROM payroll_run_items pri WHERE pri.payroll_run_id=pr.payroll_run_id AND pri.item_type='DEDUCTION'),
    pr.net_pay=pr.gross_pay-(SELECT COALESCE(SUM(pri.amount),0) FROM payroll_run_items pri WHERE pri.payroll_run_id=pr.payroll_run_id AND pri.item_type='DEDUCTION')
WHERE pr.payroll_run_id=@e4001_run_id;

INSERT INTO payslip_releases (payroll_run_id,released_at,released_by_employee_id,remarks)
SELECT @e4001_run_id,NOW(),MIN(employee_id),'[SHOWCASE USER DTR] Payslip with attendance deductions ready to download.'
FROM employees WHERE employee_no LIKE 'DEMO-%';

DROP TEMPORARY TABLE IF EXISTS tmp_e4001_dtr;
