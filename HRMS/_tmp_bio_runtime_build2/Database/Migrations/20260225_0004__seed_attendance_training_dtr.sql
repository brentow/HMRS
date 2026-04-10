-- Training seed data.

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
