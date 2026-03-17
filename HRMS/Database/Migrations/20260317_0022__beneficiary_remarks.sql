-- Add Remarks and ApprovedRejectedAt columns to BeneficiaryStaging for verification tracking
ALTER TABLE BeneficiaryStaging ADD COLUMN IF NOT EXISTS Remarks VARCHAR(500) NULL;
ALTER TABLE BeneficiaryStaging ADD COLUMN IF NOT EXISTS ApprovedRejectedAt DATETIME NULL;
