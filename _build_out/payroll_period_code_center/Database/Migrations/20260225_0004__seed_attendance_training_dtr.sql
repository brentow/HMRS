-- Attendance logs + training + monthly DTR certification seed data.

SET @OLD_SAFE_UPDATES = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

SET @device_id = (
  SELECT device_id
  FROM biometric_devices
  WHERE device_name='ZKTeco Main Gate'
  LIMIT 1
);

DELETE al
FROM attendance_logs al
JOIN employees e ON e.employee_id = al.employee_id
WHERE al.log_time BETWEEN '2026-02-16 00:00:00' AND '2026-02-20 23:59:59'
  AND e.employee_no IN (
    'E-1001','E-1002','E-2001','E-2002','E-3001','E-3002',
    'E-4001','E-4002','E-4003','E-4004','E-4005'
  );

INSERT INTO attendance_logs (employee_id, device_id, log_time, log_type, source)
SELECT
  e.employee_id,
  @device_id,
  TIMESTAMP(d.work_date, '07:00:00'),
  'IN',
  'BIOMETRIC'
FROM employees e
CROSS JOIN (
  SELECT DATE('2026-02-16') AS work_date
  UNION ALL SELECT DATE('2026-02-17')
  UNION ALL SELECT DATE('2026-02-18')
  UNION ALL SELECT DATE('2026-02-19')
  UNION ALL SELECT DATE('2026-02-20')
) d
WHERE e.employee_no IN (
  'E-1001','E-1002','E-2001','E-2002','E-3001','E-3002',
  'E-4001','E-4002','E-4003','E-4004','E-4005'
)
UNION ALL
SELECT
  e.employee_id,
  @device_id,
  TIMESTAMP(d.work_date, '17:00:00'),
  'OUT',
  'BIOMETRIC'
FROM employees e
CROSS JOIN (
  SELECT DATE('2026-02-16') AS work_date
  UNION ALL SELECT DATE('2026-02-17')
  UNION ALL SELECT DATE('2026-02-18')
  UNION ALL SELECT DATE('2026-02-19')
  UNION ALL SELECT DATE('2026-02-20')
) d
WHERE e.employee_no IN (
  'E-1001','E-1002','E-2001','E-2002','E-3001','E-3002',
  'E-4001','E-4002','E-4003','E-4004','E-4005'
);

INSERT IGNORE INTO training_courses (course_name, description) VALUES
('RA 10173 Data Privacy Orientation','Data privacy basics and handling personal information in LGU.'),
('Gender and Development (GAD) Orientation','GAD concepts and LGU implementation.'),
('Records Management and Archiving','Proper records filing, retention, and archiving for offices.'),
('Customer Service for Frontliners','Public service etiquette and frontline communication.'),
('Disaster Risk Reduction and Management (DRRM) Basics','Preparedness and LGU response fundamentals.');

INSERT IGNORE INTO training_sessions (course_id, session_date, trainer_user_id, location)
SELECT c.course_id, '2026-03-03', (SELECT user_id FROM user_accounts WHERE username='hr1' LIMIT 1), 'LGU Training Room'
FROM training_courses c
WHERE c.course_name='RA 10173 Data Privacy Orientation';

INSERT IGNORE INTO training_sessions (course_id, session_date, trainer_user_id, location)
SELECT c.course_id, '2026-03-05', (SELECT user_id FROM user_accounts WHERE username='hr1' LIMIT 1), 'LGU Training Room'
FROM training_courses c
WHERE c.course_name='Gender and Development (GAD) Orientation';

INSERT IGNORE INTO training_sessions (course_id, session_date, trainer_user_id, location)
SELECT c.course_id, '2026-03-10', (SELECT user_id FROM user_accounts WHERE username='hr2' LIMIT 1), 'Records Office'
FROM training_courses c
WHERE c.course_name='Records Management and Archiving';

INSERT IGNORE INTO training_sessions (course_id, session_date, trainer_user_id, location)
SELECT c.course_id, '2026-03-12', (SELECT user_id FROM user_accounts WHERE username='head1' LIMIT 1), 'Public Assistance Desk'
FROM training_courses c
WHERE c.course_name='Customer Service for Frontliners';

INSERT IGNORE INTO training_sessions (course_id, session_date, trainer_user_id, location)
SELECT c.course_id, '2026-03-17', (SELECT user_id FROM user_accounts WHERE username='head2' LIMIT 1), 'DRRM Operations Center'
FROM training_courses c
WHERE c.course_name='Disaster Risk Reduction and Management (DRRM) Basics';

INSERT IGNORE INTO training_enrollments (session_id, employee_id, status)
SELECT
  (
    SELECT s.session_id
    FROM training_sessions s
    JOIN training_courses c ON c.course_id = s.course_id
    WHERE c.course_name='RA 10173 Data Privacy Orientation'
      AND s.session_date='2026-03-03'
    LIMIT 1
  ),
  e.employee_id,
  'PENDING'
FROM employees e
WHERE e.employee_no IN (
  'E-1001','E-1002','E-2001','E-2002','E-3001','E-3002',
  'E-4001','E-4002','E-4003','E-4004','E-4005'
);

SET @yr_seed = 2026;
SET @mo_seed = 2;

INSERT INTO dtr_monthly_certifications
(
  employee_id, yr, mo,
  certified_by_user_id, verified_by_user_id,
  certified_at, verified_at,
  remarks
)
SELECT
  e.employee_id,
  @yr_seed,
  @mo_seed,
  (SELECT user_id FROM user_accounts WHERE username='hr1' LIMIT 1),
  (SELECT user_id FROM user_accounts WHERE username='head1' LIMIT 1),
  NOW(),
  NOW(),
  'Generated via HRMS (sample)'
FROM employees e
WHERE e.employee_no IN (
  'E-1001','E-1002','E-2001','E-2002','E-3001','E-3002',
  'E-4001','E-4002','E-4003','E-4004','E-4005'
)
ON DUPLICATE KEY UPDATE
  certified_by_user_id = VALUES(certified_by_user_id),
  verified_by_user_id  = VALUES(verified_by_user_id),
  certified_at         = VALUES(certified_at),
  verified_at          = VALUES(verified_at),
  remarks              = VALUES(remarks);

SET SQL_SAFE_UPDATES = @OLD_SAFE_UPDATES;
