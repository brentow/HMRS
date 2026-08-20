-- Remove seeded attendance demo data from existing databases.

START TRANSACTION;

SET @OLD_SAFE_UPDATES = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

DELETE aa
FROM attendance_adjustments aa
JOIN employees e ON e.employee_id = aa.employee_id
WHERE (e.employee_no='E-4003' AND aa.work_date='2026-02-18' AND aa.reason='Biometric failed to read fingerprint; supervisor verified presence.')
   OR (e.employee_no='E-4004' AND aa.work_date='2026-02-19' AND aa.reason='Forgot to tap OUT; supervisor confirms 5PM out.');

DELETE ar
FROM attendance_remarks ar
JOIN employees e ON e.employee_id = ar.employee_id
WHERE (e.employee_no='E-4001' AND ar.work_date='2026-02-19' AND ar.remark_type='OB' AND ar.details='Official Business: delivery of supplies to GSO')
   OR (e.employee_no='E-4002' AND ar.work_date='2026-02-18' AND ar.remark_type='TO' AND ar.details='Time Off: personal errand (approved)')
   OR (e.employee_no='E-3001' AND ar.work_date='2026-02-17' AND ar.remark_type='OTHER' AND ar.details='Meeting with Mayor''s office (recorded)')
   OR (e.employee_no='E-1002' AND ar.work_date='2026-02-20' AND ar.remark_type='WFH' AND ar.details='Remote systems monitoring');

DELETE dtr
FROM dtr_monthly_certifications dtr
JOIN employees e ON e.employee_id = dtr.employee_id
WHERE e.employee_no IN (
    'E-1001','E-1002','E-2001','E-2002','E-3001','E-3002',
    'E-4001','E-4002','E-4003','E-4004','E-4005'
  )
  AND dtr.yr = 2026
  AND dtr.mo = 2
  AND dtr.remarks = 'Generated via HRMS (sample)';

DELETE al
FROM attendance_logs al
JOIN employees e ON e.employee_id = al.employee_id
WHERE e.employee_no IN (
    'E-1001','E-1002','E-2001','E-2002','E-3001','E-3002',
    'E-4001','E-4002','E-4003','E-4004','E-4005'
  )
  AND al.log_time BETWEEN '2026-02-16 00:00:00' AND '2026-02-20 23:59:59'
  AND al.source = 'BIOMETRIC';

DELETE be
FROM biometric_enrollments be
JOIN employees e ON e.employee_id = be.employee_id
WHERE e.employee_no IN (
    'E-1001','E-1002','E-2001','E-2002','E-3001','E-3002',
    'E-4001','E-4002','E-4003','E-4004','E-4005'
  )
  AND be.biometric_user_id = CONCAT('BIO-', e.employee_no);

DELETE sa
FROM shift_assignments sa
JOIN employees e ON e.employee_id = sa.employee_id
LEFT JOIN shifts s ON s.shift_id = sa.shift_id
LEFT JOIN user_accounts ua ON ua.user_id = sa.assigned_by_user_id
WHERE e.employee_no IN (
    'E-1001','E-1002','E-2001','E-2002','E-3001','E-3002',
    'E-4001','E-4002','E-4003','E-4004','E-4005'
  )
  AND sa.start_date = '2026-02-01'
  AND sa.status = 'ASSIGNED'
  AND COALESCE(s.shift_name, '') = 'Day Shift'
  AND COALESCE(ua.username, '') = 'admin1';

DELETE FROM biometric_devices
WHERE device_name='ZKTeco Main Gate'
  AND serial_no='ZK-0001'
  AND location='Main Gate'
  AND ip_address='192.168.1.50';

SET SQL_SAFE_UPDATES = @OLD_SAFE_UPDATES;

COMMIT;
