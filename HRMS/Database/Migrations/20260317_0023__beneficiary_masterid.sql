-- Add MasterID column linking to user_accounts.user_id.
-- This script is defensive to avoid FK errors on mixed MySQL/MariaDB deployments.

-- Ensure the staging table can host foreign keys.
ALTER TABLE BeneficiaryStaging ENGINE=InnoDB;

-- Match MasterID type to user_accounts.user_id (e.g. int vs int unsigned).
SET @USER_ID_COLUMN_TYPE = (
		SELECT COLUMN_TYPE
		FROM information_schema.COLUMNS
		WHERE TABLE_SCHEMA = DATABASE()
			AND TABLE_NAME = 'user_accounts'
			AND COLUMN_NAME = 'user_id'
		LIMIT 1
);

SET @ADD_MASTERID_SQL = IF(
		@USER_ID_COLUMN_TYPE IS NULL,
		'SELECT 1',
		CONCAT('ALTER TABLE BeneficiaryStaging ADD COLUMN IF NOT EXISTS MasterID ', @USER_ID_COLUMN_TYPE, ' NULL')
);
PREPARE stmt FROM @ADD_MASTERID_SQL;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Add index if missing.
SET @IDX_EXISTS = (
		SELECT COUNT(*)
		FROM information_schema.STATISTICS
		WHERE TABLE_SCHEMA = DATABASE()
			AND TABLE_NAME = 'BeneficiaryStaging'
			AND INDEX_NAME = 'idx_beneficiary_masterid'
);

SET @ADD_INDEX_SQL = IF(
		@IDX_EXISTS = 0,
		'ALTER TABLE BeneficiaryStaging ADD INDEX idx_beneficiary_masterid (MasterID)',
		'SELECT 1'
);
PREPARE stmt FROM @ADD_INDEX_SQL;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Add FK only when both sides are InnoDB and the FK does not yet exist.
SET @FK_EXISTS = (
		SELECT COUNT(*)
		FROM information_schema.TABLE_CONSTRAINTS
		WHERE CONSTRAINT_SCHEMA = DATABASE()
			AND TABLE_NAME = 'BeneficiaryStaging'
			AND CONSTRAINT_TYPE = 'FOREIGN KEY'
			AND CONSTRAINT_NAME = 'FK_BeneficiaryStaging_UserAccounts'
);

SET @CHILD_ENGINE = (
		SELECT ENGINE
		FROM information_schema.TABLES
		WHERE TABLE_SCHEMA = DATABASE()
			AND TABLE_NAME = 'BeneficiaryStaging'
		LIMIT 1
);

SET @PARENT_ENGINE = (
		SELECT ENGINE
		FROM information_schema.TABLES
		WHERE TABLE_SCHEMA = DATABASE()
			AND TABLE_NAME = 'user_accounts'
		LIMIT 1
);

SET @ADD_FK_SQL = IF(
		@FK_EXISTS = 0 AND @CHILD_ENGINE = 'InnoDB' AND @PARENT_ENGINE = 'InnoDB' AND @USER_ID_COLUMN_TYPE IS NOT NULL,
		'ALTER TABLE BeneficiaryStaging ADD CONSTRAINT FK_BeneficiaryStaging_UserAccounts FOREIGN KEY (MasterID) REFERENCES user_accounts(user_id) ON DELETE SET NULL ON UPDATE CASCADE',
		'SELECT 1'
);
PREPARE stmt FROM @ADD_FK_SQL;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
