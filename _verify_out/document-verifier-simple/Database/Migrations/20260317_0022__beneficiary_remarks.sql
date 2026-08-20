-- Add Remarks and ApprovedRejectedAt columns to BeneficiaryStaging for verification tracking.
-- Kept idempotent for databases that already ran the sibling 0022 migration file.

SET @HAS_REMARKS = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'BeneficiaryStaging'
      AND COLUMN_NAME = 'Remarks'
);

SET @ADD_REMARKS_SQL = IF(
    @HAS_REMARKS = 0,
    'ALTER TABLE BeneficiaryStaging ADD COLUMN Remarks VARCHAR(500) NULL',
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
    'ALTER TABLE BeneficiaryStaging ADD COLUMN ApprovedRejectedAt DATETIME NULL',
    'SELECT 1'
);
PREPARE stmt FROM @ADD_APPROVED_REJECTED_AT_SQL;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
