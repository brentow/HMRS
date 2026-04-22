-- Refresh company profile branding to the ERPMS+ logo asset.

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
