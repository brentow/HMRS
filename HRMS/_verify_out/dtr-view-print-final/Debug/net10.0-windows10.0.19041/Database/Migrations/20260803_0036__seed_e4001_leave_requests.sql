-- Showcase leave-request history for the employee-side E-4001 account.
-- The reasons are uniquely tagged so this migration does not alter real requests.

SET @e4001_id := (SELECT employee_id FROM employees WHERE employee_no = 'E-4001' LIMIT 1);
SET @leave_actor_id := (
  SELECT MIN(employee_id)
  FROM employees
  WHERE employee_id <> @e4001_id
);

DELETE FROM leave_applications
WHERE employee_id = @e4001_id
  AND reason LIKE '[SHOWCASE LEAVE]%';

INSERT INTO leave_applications
(
  employee_id,
  leave_type_id,
  date_from,
  date_to,
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
  @e4001_id,
  lt.leave_type_id,
  x.date_from,
  x.date_to,
  x.days_requested,
  x.reason,
  x.status,
  x.filed_at,
  x.decision_at,
  CASE WHEN x.status IN ('RECOMMENDED', 'APPROVED') THEN @leave_actor_id ELSE NULL END,
  CASE WHEN x.status = 'APPROVED' THEN @leave_actor_id ELSE NULL END,
  CASE WHEN x.status = 'APPROVED' THEN @leave_actor_id ELSE NULL END,
  x.decision_remarks
FROM
(
  SELECT 'VL' leave_code, DATE('2026-06-08') date_from, DATE('2026-06-09') date_to, 2.00 days_requested,
         '[SHOWCASE LEAVE] Family vacation' reason, 'APPROVED' status,
         TIMESTAMP('2026-05-25 09:12:00') filed_at, TIMESTAMP('2026-05-27 14:35:00') decision_at,
         'Approved. Vacation leave credits were deducted and the dates were sent to DTR.' decision_remarks
  UNION ALL
  SELECT 'SL', '2026-06-23', '2026-06-23', 1.00,
         '[SHOWCASE LEAVE] Medical consultation', 'APPROVED',
         '2026-06-22 07:41:00', '2026-06-22 10:18:00',
         'Approved as paid sick leave. Medical documentation was reviewed.'
  UNION ALL
  SELECT 'SPL', '2026-07-03', '2026-07-03', 1.00,
         '[SHOWCASE LEAVE] Important family appointment', 'APPROVED',
         '2026-06-26 11:05:00', '2026-06-29 15:22:00',
         'Approved under Special Privilege Leave.'
  UNION ALL
  SELECT 'VL', '2026-07-27', '2026-07-28', 2.00,
         '[SHOWCASE LEAVE] Personal travel', 'REJECTED',
         '2026-07-20 08:50:00', '2026-07-21 16:10:00',
         'Not approved because the requested dates overlap the department closing schedule.'
  UNION ALL
  SELECT 'VL', '2026-09-14', '2026-09-14', 1.00,
         '[SHOWCASE LEAVE] Personal errand', 'CANCELLED',
         '2026-07-29 13:14:00', '2026-08-01 09:20:00',
         'Cancelled by employee. No leave credits were deducted.'
  UNION ALL
  SELECT 'VL', '2026-08-28', '2026-08-28', 1.00,
         '[SHOWCASE LEAVE] Family event', 'SUBMITTED',
         '2026-08-01 10:05:00', NULL,
         'Waiting for supervisor and HR review.'
  UNION ALL
  SELECT 'WLN', '2026-09-04', '2026-09-04', 1.00,
         '[SHOWCASE LEAVE] Wellness and recovery day', 'RECOMMENDED',
         '2026-08-02 08:36:00', NULL,
         'Recommended by supervisor; awaiting final HR approval.'
  UNION ALL
  SELECT 'LWOP', '2026-09-21', '2026-09-22', 2.00,
         '[SHOWCASE LEAVE] Extended personal obligation', 'SUBMITTED',
         '2026-08-03 09:45:00', NULL,
         'Pending HR review. If approved, these unpaid days will flow to payroll.'
) x
INNER JOIN leave_types lt ON lt.code = x.leave_code
WHERE @e4001_id IS NOT NULL;

-- Add one day row per requested calendar day for the seeded one- and two-day requests.
INSERT IGNORE INTO leave_application_days
  (leave_application_id, leave_date, day_fraction, half_day_part)
SELECT la.leave_application_id, DATE_ADD(la.date_from, INTERVAL offsets.day_offset DAY), 1.00, NULL
FROM leave_applications la
CROSS JOIN
(
  SELECT 0 day_offset
  UNION ALL SELECT 1
) offsets
WHERE la.employee_id = @e4001_id
  AND la.reason LIKE '[SHOWCASE LEAVE]%'
  AND DATE_ADD(la.date_from, INTERVAL offsets.day_offset DAY) <= la.date_to;

-- Ensure the employee-side balance tab demonstrates all leave types used above.
INSERT INTO leave_balances
  (employee_id, leave_type_id, `year`, opening_credits, earned, used, adjustments, as_of_date)
SELECT
  @e4001_id,
  lt.leave_type_id,
  2026,
  lt.default_credits_per_year,
  0.00,
  0.00,
  CASE WHEN lt.code = 'VL' THEN 1.00 ELSE 0.00 END,
  '2026-08-03'
FROM leave_types lt
WHERE @e4001_id IS NOT NULL
  AND lt.code IN ('VL', 'SL', 'SPL', 'WLN', 'LWOP')
ON DUPLICATE KEY UPDATE
  opening_credits = VALUES(opening_credits),
  adjustments = VALUES(adjustments),
  as_of_date = VALUES(as_of_date);

UPDATE leave_balances lb
SET lb.used =
(
  SELECT COALESCE(SUM(la.days_requested), 0.00)
  FROM leave_applications la
  WHERE la.employee_id = lb.employee_id
    AND la.leave_type_id = lb.leave_type_id
    AND YEAR(la.date_from) = lb.`year`
    AND la.status = 'APPROVED'
),
lb.as_of_date = '2026-08-03'
WHERE lb.employee_id = @e4001_id
  AND lb.`year` = 2026;
