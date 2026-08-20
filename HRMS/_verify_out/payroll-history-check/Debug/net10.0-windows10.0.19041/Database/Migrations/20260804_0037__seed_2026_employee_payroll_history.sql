-- Complete January-August 2026 semi-monthly payroll history for every active employee.
-- These PAY-2026-* periods are demonstration records and do not replace real payroll periods.

DROP TEMPORARY TABLE IF EXISTS tmp_showcase_2026_cutoffs;
CREATE TEMPORARY TABLE tmp_showcase_2026_cutoffs
(
  period_code VARCHAR(30) PRIMARY KEY,
  month_no INT NOT NULL,
  cutoff_no INT NOT NULL,
  date_from DATE NOT NULL,
  date_to DATE NOT NULL,
  pay_date DATE NOT NULL
);

INSERT INTO tmp_showcase_2026_cutoffs
  (period_code, month_no, cutoff_no, date_from, date_to, pay_date)
VALUES
  ('PAY-2026-01-15-CUTOFF',1,15,'2026-01-01','2026-01-15','2026-01-15'),
  ('PAY-2026-01-30-CUTOFF',1,30,'2026-01-16','2026-01-31','2026-01-31'),
  ('PAY-2026-02-15-CUTOFF',2,15,'2026-02-01','2026-02-15','2026-02-15'),
  ('PAY-2026-02-30-CUTOFF',2,30,'2026-02-16','2026-02-28','2026-02-28'),
  ('PAY-2026-03-15-CUTOFF',3,15,'2026-03-01','2026-03-15','2026-03-15'),
  ('PAY-2026-03-30-CUTOFF',3,30,'2026-03-16','2026-03-31','2026-03-31'),
  ('PAY-2026-04-15-CUTOFF',4,15,'2026-04-01','2026-04-15','2026-04-15'),
  ('PAY-2026-04-30-CUTOFF',4,30,'2026-04-16','2026-04-30','2026-04-30'),
  ('PAY-2026-05-15-CUTOFF',5,15,'2026-05-01','2026-05-15','2026-05-15'),
  ('PAY-2026-05-30-CUTOFF',5,30,'2026-05-16','2026-05-31','2026-05-31'),
  ('PAY-2026-06-15-CUTOFF',6,15,'2026-06-01','2026-06-15','2026-06-15'),
  ('PAY-2026-06-30-CUTOFF',6,30,'2026-06-16','2026-06-30','2026-06-30'),
  ('PAY-2026-07-15-CUTOFF',7,15,'2026-07-01','2026-07-15','2026-07-15'),
  ('PAY-2026-07-30-CUTOFF',7,30,'2026-07-16','2026-07-31','2026-07-31'),
  ('PAY-2026-08-15-CUTOFF',8,15,'2026-08-01','2026-08-15','2026-08-15'),
  ('PAY-2026-08-30-CUTOFF',8,30,'2026-08-16','2026-08-31','2026-08-31');

INSERT INTO payroll_periods (period_code, date_from, date_to, pay_date, status)
SELECT period_code, date_from, date_to, pay_date, 'POSTED'
FROM tmp_showcase_2026_cutoffs
ON DUPLICATE KEY UPDATE
  date_from = VALUES(date_from),
  date_to = VALUES(date_to),
  pay_date = VALUES(pay_date),
  status = 'POSTED';

DROP TEMPORARY TABLE IF EXISTS tmp_showcase_2026_compensation;
CREATE TEMPORARY TABLE tmp_showcase_2026_compensation AS
SELECT
  e.employee_id,
  COALESCE(at.type_name, 'Permanent') appointment_type,
  COALESCE(
    (
      SELECT ss.monthly_rate
      FROM salary_steps ss
      WHERE ss.salary_grade = e.salary_grade
        AND ss.step_no = e.step_no
        AND ss.effectivity_date <= '2026-08-31'
      ORDER BY ss.effectivity_date DESC
      LIMIT 1
    ),
    28000.00 + (COALESCE(e.salary_grade, 1) * 750.00)
  ) monthly_rate
FROM employees e
LEFT JOIN appointment_types at ON at.appointment_type_id = e.appointment_type_id
WHERE e.status = 'ACTIVE';

-- Clear only the line items/releases owned by these demonstration periods before rebuilding them.
DELETE pc
FROM payroll_concerns pc
INNER JOIN payroll_runs pr ON pr.payroll_run_id = pc.payroll_run_id
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF';

DELETE rel
FROM payslip_releases rel
INNER JOIN payroll_runs pr ON pr.payroll_run_id = rel.payroll_run_id
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF';

DELETE pri
FROM payroll_run_items pri
INNER JOIN payroll_runs pr ON pr.payroll_run_id = pri.payroll_run_id
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF';

INSERT INTO payroll_runs
(
  payroll_period_id,
  employee_id,
  basic_pay,
  allowances,
  overtime_pay,
  other_earnings,
  gross_pay,
  deductions_total,
  net_pay,
  status,
  generated_at
)
SELECT
  pp.payroll_period_id,
  c.employee_id,
  ROUND(c.monthly_rate / 2.00, 2),
  750.00,
  ROUND(MOD(c.employee_id + x.month_no + x.cutoff_no, 4) * 175.00, 2),
  CASE
    WHEN x.month_no = 5 AND x.cutoff_no = 30 THEN 1500.00
    WHEN x.cutoff_no = 30 THEN 500.00
    ELSE 0.00
  END,
  ROUND(
    (c.monthly_rate / 2.00) + 750.00 +
    (MOD(c.employee_id + x.month_no + x.cutoff_no, 4) * 175.00) +
    CASE WHEN x.month_no = 5 AND x.cutoff_no = 30 THEN 1500.00 WHEN x.cutoff_no = 30 THEN 500.00 ELSE 0.00 END,
    2
  ),
  0.00,
  0.00,
  'RELEASED',
  TIMESTAMP(x.pay_date, '08:00:00')
FROM tmp_showcase_2026_compensation c
CROSS JOIN tmp_showcase_2026_cutoffs x
INNER JOIN payroll_periods pp ON pp.period_code = x.period_code
ON DUPLICATE KEY UPDATE
  basic_pay = VALUES(basic_pay),
  allowances = VALUES(allowances),
  overtime_pay = VALUES(overtime_pay),
  other_earnings = VALUES(other_earnings),
  gross_pay = VALUES(gross_pay),
  deductions_total = 0.00,
  net_pay = 0.00,
  status = 'RELEASED',
  generated_at = VALUES(generated_at);

-- Complete earnings breakdown.
INSERT INTO payroll_run_items (payroll_run_id, item_type, code, description, amount)
SELECT pr.payroll_run_id, 'EARNING', 'BASIC', 'Basic salary for cutoff', pr.basic_pay
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF'
UNION ALL
SELECT pr.payroll_run_id, 'EARNING', 'ALLOW', 'Representation and transportation allowance', pr.allowances
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF' AND pr.allowances > 0
UNION ALL
SELECT pr.payroll_run_id, 'EARNING', 'OVERTIME', 'Approved overtime pay', pr.overtime_pay
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF' AND pr.overtime_pay > 0
UNION ALL
SELECT pr.payroll_run_id, 'EARNING', 'OTHER', 'Bonus and other earnings', pr.other_earnings
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF' AND pr.other_earnings > 0;

-- Statutory deductions are split between the 15th and 30th cutoffs.
INSERT INTO payroll_run_items (payroll_run_id, item_type, code, description, amount)
SELECT pr.payroll_run_id, 'DEDUCTION',
       CASE WHEN c.appointment_type IN ('Job Order','Contractual') THEN 'SSS' ELSE 'GSIS' END,
       CASE WHEN c.appointment_type IN ('Job Order','Contractual') THEN 'SSS employee contribution' ELSE 'GSIS employee contribution' END,
       CASE WHEN c.appointment_type IN ('Job Order','Contractual')
            THEN ROUND(LEAST(500.00, GREATEST(100.00, pr.basic_pay * 0.05)), 2)
            ELSE ROUND(pr.basic_pay * 0.09, 2) END
FROM payroll_runs pr
INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
INNER JOIN tmp_showcase_2026_compensation c ON c.employee_id=pr.employee_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF'
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'PHILHEALTH', 'PhilHealth employee contribution',
       ROUND(LEAST(1250.00, GREATEST(125.00, pr.basic_pay * 0.025)), 2)
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF'
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'PAGIBIG', 'HDMF / Pag-IBIG employee contribution', 100.00
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF'
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'TAX', 'Withholding tax',
       ROUND(GREATEST(0.00, pr.gross_pay - 10416.67) * 0.10, 2)
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF'
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'DTR_MINUS', 'Late and undertime deduction',
       ROUND(MOD(pr.employee_id + MONTH(pp.pay_date) + DAY(pp.pay_date), 4) * 42.50, 2)
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF'
  AND MOD(pr.employee_id + MONTH(pp.pay_date) + DAY(pp.pay_date), 4) > 0
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'ABSENCE', 'Approved absence deduction', 450.00
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF'
  AND MOD(pr.employee_id + MONTH(pp.pay_date), 7) = 0
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'LOAN', 'Government or salary loan repayment', 500.00
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF'
  AND pp.period_code LIKE '%-30-CUTOFF'
  AND MOD(pr.employee_id, 2) = 0
UNION ALL
SELECT pr.payroll_run_id, 'DEDUCTION', 'OTHER_DEDUCTION', 'Cooperative and other deduction', 150.00
FROM payroll_runs pr INNER JOIN payroll_periods pp ON pp.payroll_period_id=pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF'
  AND pp.period_code LIKE '%-30-CUTOFF'
  AND MOD(pr.employee_id, 3) = 0;

UPDATE payroll_runs pr
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
SET
  pr.deductions_total = (
    SELECT COALESCE(SUM(pri.amount), 0.00)
    FROM payroll_run_items pri
    WHERE pri.payroll_run_id = pr.payroll_run_id
      AND pri.item_type = 'DEDUCTION'
  ),
  pr.net_pay = pr.gross_pay - (
    SELECT COALESCE(SUM(pri.amount), 0.00)
    FROM payroll_run_items pri
    WHERE pri.payroll_run_id = pr.payroll_run_id
      AND pri.item_type = 'DEDUCTION'
  )
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF';

SET @showcase_payroll_actor := (SELECT MIN(employee_id) FROM employees WHERE status='ACTIVE');

INSERT INTO payslip_releases (payroll_run_id, released_at, released_by_employee_id, remarks)
SELECT
  pr.payroll_run_id,
  TIMESTAMP(pp.pay_date, '09:00:00'),
  @showcase_payroll_actor,
  CONCAT(
    '[SHOWCASE 2026] ',
    CASE WHEN pp.period_code LIKE '%-15-CUTOFF' THEN '15th cutoff' ELSE '30th cutoff' END,
    ' payslip released with full earnings and deduction details.'
  )
FROM payroll_runs pr
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
WHERE pp.period_code LIKE 'PAY-2026-%-CUTOFF';

DROP TEMPORARY TABLE IF EXISTS tmp_showcase_2026_compensation;
DROP TEMPORARY TABLE IF EXISTS tmp_showcase_2026_cutoffs;
