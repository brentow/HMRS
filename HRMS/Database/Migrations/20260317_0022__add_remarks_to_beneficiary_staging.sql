-- Migration 22: Add Remarks column to BeneficiaryStaging for approval/rejection notes
ALTER TABLE BeneficiaryStaging
ADD COLUMN IF NOT EXISTS Remarks VARCHAR(500) NULL AFTER VerificationStatus;

ALTER TABLE BeneficiaryStaging
ADD COLUMN IF NOT EXISTS ApprovedRejectedAt DATETIME NULL AFTER Remarks;
