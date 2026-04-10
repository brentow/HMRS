-- Base seed data for roles, org structure, employees, users, and baseline module values.

INSERT IGNORE INTO roles (role_name) VALUES
('Admin'),('HR Manager'),('Dept Head'),('Employee');

INSERT IGNORE INTO permissions (perm_code, description) VALUES
('USER_MANAGE','Manage users/roles'),
('EMPLOYEE_MANAGE','Manage employee records'),
('ATTENDANCE_VIEW','View DTR/attendance'),
('ATTENDANCE_EDIT','Adjust attendance'),
('TRAINING_MANAGE','Manage trainings'),
('DTR_CERTIFY','Certify/verify DTR');

INSERT IGNORE INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_name='Admin';

INSERT IGNORE INTO departments (dept_name) VALUES
('Human Resource Management Office'),
('Information Technology Office'),
('Office of the Mayor'),
('General Services Office');

INSERT IGNORE INTO positions (department_id, position_name)
SELECT d.department_id, 'HR Officer'
FROM departments d
WHERE d.dept_name='Human Resource Management Office'
UNION ALL
SELECT d.department_id, 'IT Officer'
FROM departments d
WHERE d.dept_name='Information Technology Office'
UNION ALL
SELECT d.department_id, 'Department Head'
FROM departments d
WHERE d.dept_name='Office of the Mayor'
UNION ALL
SELECT d.department_id, 'Administrative Aide'
FROM departments d
WHERE d.dept_name='General Services Office'
UNION ALL
SELECT d.department_id, 'Clerk'
FROM departments d
WHERE d.dept_name='General Services Office';

INSERT IGNORE INTO appointment_types (type_name) VALUES
('Permanent'),('Casual'),('Contractual'),('Coterminous'),('Job Order / COS');

INSERT IGNORE INTO salary_grades (salary_grade)
SELECT n
FROM (
  SELECT 1 n UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL
  SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9 UNION ALL SELECT 10 UNION ALL
  SELECT 11 UNION ALL SELECT 12 UNION ALL SELECT 13 UNION ALL SELECT 14 UNION ALL SELECT 15 UNION ALL
  SELECT 16 UNION ALL SELECT 17 UNION ALL SELECT 18 UNION ALL SELECT 19 UNION ALL SELECT 20 UNION ALL
  SELECT 21 UNION ALL SELECT 22 UNION ALL SELECT 23 UNION ALL SELECT 24 UNION ALL SELECT 25 UNION ALL
  SELECT 26 UNION ALL SELECT 27 UNION ALL SELECT 28 UNION ALL SELECT 29 UNION ALL SELECT 30 UNION ALL
  SELECT 31 UNION ALL SELECT 32 UNION ALL SELECT 33
) x;

INSERT INTO shifts (shift_name, start_time, end_time, break_minutes, grace_minutes, is_overnight)
VALUES ('Day Shift', '07:00:00', '17:00:00', 60, 10, 0)
ON DUPLICATE KEY UPDATE
  start_time = VALUES(start_time),
  end_time = VALUES(end_time),
  break_minutes = VALUES(break_minutes),
  grace_minutes = VALUES(grace_minutes),
  is_overnight = VALUES(is_overnight);

INSERT INTO biometric_devices (device_name, serial_no, location, ip_address, is_active)
VALUES ('ZKTeco Main Gate','ZK-0001','Main Gate','192.168.1.50',1)
ON DUPLICATE KEY UPDATE
  serial_no = VALUES(serial_no),
  location = VALUES(location),
  ip_address = VALUES(ip_address),
  is_active = VALUES(is_active);

INSERT IGNORE INTO employees (
  employee_no, last_name, first_name, middle_name,
  sex, birth_date, civil_status,
  email, contact_number, address,
  department_id, position_id, appointment_type_id,
  salary_grade, step_no, hire_date,
  tin_no, gsis_bp_no, philhealth_no, pagibig_mid_no,
  emergency_contact, emergency_phone
) VALUES
('E-1001','Dela Cruz','Juan','Santos','MALE','1991-04-12','Married',
 'juan.delacruz@lgu.local','09171234501','Poblacion, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='Information Technology Office'),
 (SELECT position_id FROM positions WHERE position_name='IT Officer' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 18,1,'2026-01-05','000-000-000-001','GSIS-000001','PH-000001','HDMF-000001',
 'Maria Dela Cruz','09170000001'),

('E-1002','Santos','Maria','Lopez','FEMALE','1993-09-23','Single',
 'maria.santos@lgu.local','09171234502','Brgy. Maligaya, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='Information Technology Office'),
 (SELECT position_id FROM positions WHERE position_name='IT Officer' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 18,1,'2026-01-05','000-000-000-002','GSIS-000002','PH-000002','HDMF-000002',
 'Jose Santos','09170000002'),

('E-2001','Reyes','Ana','Cruz','FEMALE','1990-02-10','Married',
 'ana.reyes@lgu.local','09171234503','Brgy. Pag-asa, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='Human Resource Management Office'),
 (SELECT position_id FROM positions WHERE position_name='HR Officer' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 15,1,'2026-01-05','000-000-000-003','GSIS-000003','PH-000003','HDMF-000003',
 'Paulo Reyes','09170000003'),

('E-2002','Ramirez','Jose','Mendoza','MALE','1988-11-05','Married',
 'jose.ramirez@lgu.local','09171234504','Poblacion, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='Human Resource Management Office'),
 (SELECT position_id FROM positions WHERE position_name='HR Officer' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 15,1,'2026-01-05','000-000-000-004','GSIS-000004','PH-000004','HDMF-000004',
 'Liza Ramirez','09170000004'),

('E-3001','Garcia','Roberto','Aquino','MALE','1985-06-18','Married',
 'roberto.garcia@lgu.local','09171234505','Brgy. Mabini, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='Office of the Mayor'),
 (SELECT position_id FROM positions WHERE position_name='Department Head' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 24,1,'2026-01-05','000-000-000-005','GSIS-000005','PH-000005','HDMF-000005',
 'Grace Garcia','09170000005'),

('E-3002','Cruz','Liza','Bautista','FEMALE','1987-03-22','Married',
 'liza.cruz@lgu.local','09171234506','Brgy. Masinop, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='Office of the Mayor'),
 (SELECT position_id FROM positions WHERE position_name='Department Head' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 24,1,'2026-01-05','000-000-000-006','GSIS-000006','PH-000006','HDMF-000006',
 'Carlo Cruz','09170000006'),

('E-4001','Villanueva','Mark','Navarro','MALE','1996-07-14','Single',
 'mark.villanueva@lgu.local','09171234507','Brgy. Masigla, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='General Services Office'),
 (SELECT position_id FROM positions WHERE position_name='Administrative Aide' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 6,1,'2026-01-05','000-000-000-007','GSIS-000007','PH-000007','HDMF-000007',
 'Jasmine Villanueva','09170000007'),

('E-4002','Bautista','Jasmine','Reyes','FEMALE','1997-12-02','Single',
 'jasmine.bautista@lgu.local','09171234508','Poblacion, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='General Services Office'),
 (SELECT position_id FROM positions WHERE position_name='Clerk' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 6,1,'2026-01-05','000-000-000-008','GSIS-000008','PH-000008','HDMF-000008',
 'Ana Bautista','09170000008'),

('E-4003','Mendoza','Carlo','Santos','MALE','1995-01-28','Single',
 'carlo.mendoza@lgu.local','09171234509','Brgy. Pagkakaisa, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='General Services Office'),
 (SELECT position_id FROM positions WHERE position_name='Administrative Aide' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 6,1,'2026-01-05','000-000-000-009','GSIS-000009','PH-000009','HDMF-000009',
 'Jose Mendoza','09170000009'),

('E-4004','Navarro','Grace','Lopez','FEMALE','1994-08-09','Married',
 'grace.navarro@lgu.local','09171234510','Brgy. Mabuhay, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='General Services Office'),
 (SELECT position_id FROM positions WHERE position_name='Clerk' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 6,1,'2026-01-05','000-000-000-010','GSIS-000010','PH-000010','HDMF-000010',
 'Roberto Navarro','09170000010'),

('E-4005','Aquino','Paolo','Garcia','MALE','1998-05-30','Single',
 'paolo.aquino@lgu.local','09171234511','Brgy. Malinis, Lungsod ng San Isidro',
 (SELECT department_id FROM departments WHERE dept_name='General Services Office'),
 (SELECT position_id FROM positions WHERE position_name='Administrative Aide' LIMIT 1),
 (SELECT appointment_type_id FROM appointment_types WHERE type_name='Permanent'),
 6,1,'2026-01-05','000-000-000-011','GSIS-000011','PH-000011','HDMF-000011',
 'Liza Aquino','09170000011');

INSERT IGNORE INTO user_accounts (role_id, employee_id, username, password, full_name, email)
VALUES
((SELECT role_id FROM roles WHERE role_name='Admin'),      (SELECT employee_id FROM employees WHERE employee_no='E-1001'), 'admin1','admin123','Juan Dela Cruz','juan.delacruz@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='Admin'),      (SELECT employee_id FROM employees WHERE employee_no='E-1002'), 'admin2','admin123','Maria Santos','maria.santos@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='HR Manager'), (SELECT employee_id FROM employees WHERE employee_no='E-2001'), 'hr1','admin123','Ana Reyes','ana.reyes@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='HR Manager'), (SELECT employee_id FROM employees WHERE employee_no='E-2002'), 'hr2','admin123','Jose Ramirez','jose.ramirez@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='Dept Head'),  (SELECT employee_id FROM employees WHERE employee_no='E-3001'), 'head1','admin123','Roberto Garcia','roberto.garcia@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='Dept Head'),  (SELECT employee_id FROM employees WHERE employee_no='E-3002'), 'head2','admin123','Liza Cruz','liza.cruz@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='Employee'),   (SELECT employee_id FROM employees WHERE employee_no='E-4001'), 'emp1','admin123','Mark Villanueva','mark.villanueva@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='Employee'),   (SELECT employee_id FROM employees WHERE employee_no='E-4002'), 'emp2','admin123','Jasmine Bautista','jasmine.bautista@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='Employee'),   (SELECT employee_id FROM employees WHERE employee_no='E-4003'), 'emp3','admin123','Carlo Mendoza','carlo.mendoza@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='Employee'),   (SELECT employee_id FROM employees WHERE employee_no='E-4004'), 'emp4','admin123','Grace Navarro','grace.navarro@lgu.local'),
((SELECT role_id FROM roles WHERE role_name='Employee'),   (SELECT employee_id FROM employees WHERE employee_no='E-4005'), 'emp5','admin123','Paolo Aquino','paolo.aquino@lgu.local');

INSERT IGNORE INTO biometric_enrollments (employee_id, biometric_user_id, device_id, status)
SELECT
  e.employee_id,
  CONCAT('BIO-', e.employee_no),
  (SELECT device_id FROM biometric_devices WHERE device_name='ZKTeco Main Gate' LIMIT 1),
  'ACTIVE'
FROM employees e
WHERE e.employee_no IN ('E-1001','E-1002','E-2001','E-2002','E-3001','E-3002','E-4001','E-4002','E-4003','E-4004','E-4005');

INSERT IGNORE INTO shift_assignments (employee_id, shift_id, start_date, assigned_by_user_id, status)
SELECT
  e.employee_id,
  (SELECT shift_id FROM shifts WHERE shift_name='Day Shift' LIMIT 1),
  '2026-02-01',
  (SELECT user_id FROM user_accounts WHERE username='admin1' LIMIT 1),
  'ASSIGNED'
FROM employees e
WHERE e.employee_no IN ('E-1001','E-1002','E-2001','E-2002','E-3001','E-3002','E-4001','E-4002','E-4003','E-4004','E-4005');

INSERT INTO leave_types
(code, name, is_paid, default_credits_per_year, remarks, is_active)
VALUES
('VL',   'Vacation Leave',          1, 15.00, 'Standard VL credits',          1),
('SL',   'Sick Leave',              1, 15.00, 'Standard SL credits',          1),
('SPL',  'Special Privilege Leave', 1,  3.00, 'Special privilege leave',      1),
('ML',   'Maternity Leave',         1,  0.00, 'As applicable',                1),
('PL',   'Paternity Leave',         1,  0.00, 'As applicable',                1),
('BW',   'Bereavement Leave',       1,  0.00, 'As applicable',                1),
('LWOP', 'Leave Without Pay',       0,  0.00, 'Unpaid leave',                 1)
AS new
ON DUPLICATE KEY UPDATE
  name = new.name,
  is_paid = new.is_paid,
  default_credits_per_year = new.default_credits_per_year,
  remarks = new.remarks,
  is_active = new.is_active;
