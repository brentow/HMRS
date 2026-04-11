CREATE TABLE IF NOT EXISTS BeneficiaryStaging (
    StagingID INT AUTO_INCREMENT PRIMARY KEY,
    CivilRegistryID VARCHAR(100) NOT NULL,
    FirstName VARCHAR(100) NOT NULL,
    LastName VARCHAR(100) NOT NULL,
    MiddleName VARCHAR(100) NULL,
    Address VARCHAR(200) NULL,
    VerificationStatus INT NOT NULL DEFAULT 0,
    ImportedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_beneficiary_staging_civil_registry (CivilRegistryID)
) ENGINE=InnoDB;
