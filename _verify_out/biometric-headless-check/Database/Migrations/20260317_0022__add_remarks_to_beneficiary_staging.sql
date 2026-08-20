-- Migration 22: Add Remarks column to BeneficiaryStaging for approval/rejection notes.
-- Uses information_schema checks so it works on MySQL/MariaDB variants without
-- ALTER TABLE ... ADD COLUMN IF NOT EXISTS support.

SET @HAS_REMARKS = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'BeneficiaryStaging'
      AND COLUMN_NAME = 'Remarks'
);

SET @ADD_REMARKS_SQL = IF(
    @HAS_REMARKS = 0,
    'ALTER TABLE BeneficiaryStaging ADD COLUMN Remarks VARCHAR(500) NULL AFTER VerificationStatus',
    'SELECT 1'
);
PREPARE stmt FROM @ADD_REMARKS_SQL;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @HAS_APPROVED_REJECTED_AT = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'BeneficiaryStaging'
      AND COLUMN_NAME = 'ApprovedRejectedAt'
);

SET @ADD_APPROVED_REJECTED_AT_SQL = IF(
    @HAS_APPROVED_REJECTED_AT = 0,
    'ALTER TABLE BeneficiaryStaging ADD COLUMN ApprovedRejectedAt DATETIME NULL AFTER Remarks',
    'SELECT 1'
);
PREPARE stmt FROM @ADD_APPROVED_REJECTED_AT_SQL;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
