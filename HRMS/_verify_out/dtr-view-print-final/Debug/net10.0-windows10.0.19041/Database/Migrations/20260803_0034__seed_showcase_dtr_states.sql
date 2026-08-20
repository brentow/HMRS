-- Demonstration DTR states for the 20 isolated DEMO employees.
-- Covers overtime, undertime, late, absence, incomplete punch, and approved leave.

DELETE al
FROM attendance_logs al
INNER JOIN employees e ON e.employee_id=al.employee_id
WHERE e.employee_no IN ('DEMO-001','DEMO-002','DEMO-003','DEMO-004','DEMO-005','DEMO-006','DEMO-007','DEMO-008','DEMO-009')
  AND DATE(al.log_time)=CURDATE()
  AND al.source='IMPORT';

CREATE TEMPORARY TABLE IF NOT EXISTS tmp_showcase_dtr_punches (
  employee_no VARCHAR(30) NOT NULL,
  minute_of_day INT NOT NULL,
  log_type VARCHAR(10) NOT NULL,
  PRIMARY KEY (employee_no, minute_of_day, log_type)
);

DELETE FROM tmp_showcase_dtr_punches;
INSERT INTO tmp_showcase_dtr_punches (employee_no,minute_of_day,log_type)
VALUES
 ('DEMO-001',478,'IN'), ('DEMO-001',1150,'OUT'),
 ('DEMO-002',480,'IN'), ('DEMO-002',930,'OUT'),
 ('DEMO-003',515,'IN'), ('DEMO-003',1020,'OUT'),
 ('DEMO-006',482,'IN'),
 ('DEMO-007',480,'IN'), ('DEMO-007',1025,'OUT'),
 ('DEMO-008',475,'IN'), ('DEMO-008',1100,'OUT');

INSERT IGNORE INTO attendance_logs (employee_id,device_id,log_time,log_type,source,raw_payload)
SELECT e.employee_id,NULL,DATE_ADD(CURDATE(),INTERVAL p.minute_of_day MINUTE),p.log_type,'IMPORT',
       JSON_OBJECT('showcase',true,'state','DTR calculation demonstration')
FROM tmp_showcase_dtr_punches p
INNER JOIN employees e ON e.employee_no=p.employee_no;

INSERT INTO attendance_remarks (employee_id,work_date,remark_type,details)
SELECT e.employee_id,CURDATE(),'OTHER','ABSENT - showcase unexcused absence'
FROM employees e WHERE e.employee_no IN ('DEMO-004','DEMO-009')
ON DUPLICATE KEY UPDATE details=VALUES(details);

DELETE la
FROM leave_applications la
INNER JOIN employees e ON e.employee_id=la.employee_id
WHERE e.employee_no='DEMO-005' AND la.reason='[SHOWCASE DTR] Approved vacation leave';

INSERT INTO leave_applications
 (employee_id,leave_type_id,date_from,date_to,days_requested,reason,status,filed_at,decision_at,recommended_by_employee_id,approved_by_employee_id,hr_certified_by_employee_id,decision_remarks)
SELECT e.employee_id,lt.leave_type_id,CURDATE(),CURDATE(),1.00,
       '[SHOWCASE DTR] Approved vacation leave','APPROVED',DATE_SUB(NOW(),INTERVAL 2 DAY),NOW(),actor.employee_id,actor.employee_id,actor.employee_id,
       'Approved showcase leave: no absence or attendance deduction.'
FROM employees e
INNER JOIN leave_types lt ON lt.code='VL'
CROSS JOIN (SELECT MIN(employee_id) employee_id FROM employees WHERE employee_no LIKE 'DEMO-%') actor
WHERE e.employee_no='DEMO-005';

INSERT IGNORE INTO leave_application_days (leave_application_id,leave_date,day_fraction,half_day_part)
SELECT la.leave_application_id,la.date_from,1.00,NULL
FROM leave_applications la
INNER JOIN employees e ON e.employee_id=la.employee_id
WHERE e.employee_no='DEMO-005' AND la.reason='[SHOWCASE DTR] Approved vacation leave';

UPDATE leave_balances lb
INNER JOIN employees e ON e.employee_id=lb.employee_id
INNER JOIN leave_types lt ON lt.leave_type_id=lb.leave_type_id AND lt.code='VL'
SET lb.used=(
      SELECT COALESCE(SUM(la.days_requested),0)
      FROM leave_applications la
      WHERE la.employee_id=e.employee_id
        AND la.leave_type_id=lt.leave_type_id
        AND YEAR(la.date_from)=YEAR(CURDATE())
        AND la.status='APPROVED'
    ),
    lb.as_of_date=CURDATE()
WHERE e.employee_no='DEMO-005' AND lb.year=YEAR(CURDATE());

-- Make the two showcased absences visible in the existing demonstration payroll deductions.
SET @showcase_period_id := (SELECT payroll_period_id FROM payroll_periods WHERE period_code='SHOWCASE-AUG-2026' LIMIT 1);

DELETE pri
FROM payroll_run_items pri
INNER JOIN payroll_runs pr ON pr.payroll_run_id=pri.payroll_run_id
INNER JOIN employees e ON e.employee_id=pr.employee_id
WHERE pr.payroll_period_id=@showcase_period_id
  AND e.employee_no IN ('DEMO-004','DEMO-009')
  AND pri.item_type='DEDUCTION'
  AND UPPER(pri.code) IN ('ABSENCE','ABSENCE_DEDUCTION');

INSERT INTO payroll_run_items (payroll_run_id,item_type,code,description,amount)
SELECT pr.payroll_run_id,'DEDUCTION','ABSENCE','One-day unexcused absence from showcase DTR',ROUND(pr.basic_pay/22,2)
FROM payroll_runs pr
INNER JOIN employees e ON e.employee_id=pr.employee_id
WHERE pr.payroll_period_id=@showcase_period_id
  AND e.employee_no IN ('DEMO-004','DEMO-009');

UPDATE payroll_runs pr
SET pr.deductions_total=(SELECT COALESCE(SUM(pri.amount),0) FROM payroll_run_items pri WHERE pri.payroll_run_id=pr.payroll_run_id AND pri.item_type='DEDUCTION'),
    pr.net_pay=pr.gross_pay-(SELECT COALESCE(SUM(pri.amount),0) FROM payroll_run_items pri WHERE pri.payroll_run_id=pr.payroll_run_id AND pri.item_type='DEDUCTION')
WHERE pr.payroll_period_id=@showcase_period_id;

DROP TEMPORARY TABLE IF EXISTS tmp_showcase_dtr_punches;
