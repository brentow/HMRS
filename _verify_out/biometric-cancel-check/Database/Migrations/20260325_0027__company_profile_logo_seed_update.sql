-- Align existing company profile branding defaults with current HRMS system identity.
-- Keep serial mapped to GGMS office identity: Office ID 18 / OFF-2026-0007.

UPDATE company_profile
SET logo_path = 'HRMS/Images/ePRIME_logo.png'
WHERE logo_path IS NULL
   OR TRIM(logo_path) = ''
   OR REPLACE(TRIM(logo_path), '\\', '/') IN (
       '/Images/HRMS_logo_cropped.png',
       'Images/HRMS_logo_cropped.png',
       'HRMS/Images/HRMS_logo_cropped.png',
       '/Images/ePRIME_logo.png',
       'Images/ePRIME_logo.png',
       'HRMS/Images/ePRIME_logo.png',
       '/Images/ERPMS_logo.png',
       'Images/ERPMS_logo.png',
       'HRMS/Images/ERPMS_logo.png');

UPDATE company_profile
SET serial_number = 'Office ID 18 / OFF-2026-0007'
WHERE serial_number IS NULL
   OR TRIM(serial_number) = '';
