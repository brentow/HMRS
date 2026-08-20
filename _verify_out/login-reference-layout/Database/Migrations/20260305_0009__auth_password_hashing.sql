SET @db_name = DATABASE();

SET @sql = IF(
    EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = @db_name
          AND table_name = 'user_accounts'
          AND column_name = 'password_hash'
    ),
    'SELECT ''password_hash already exists'' AS message',
    'ALTER TABLE user_accounts ADD COLUMN password_hash VARCHAR(255) NULL AFTER password'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = @db_name
          AND table_name = 'user_accounts'
          AND column_name = 'must_change_password'
    ),
    'SELECT ''must_change_password already exists'' AS message',
    'ALTER TABLE user_accounts ADD COLUMN must_change_password TINYINT(1) NOT NULL DEFAULT 0 AFTER status'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = @db_name
          AND table_name = 'user_accounts'
          AND column_name = 'password_changed_at'
    ),
    'SELECT ''password_changed_at already exists'' AS message',
    'ALTER TABLE user_accounts ADD COLUMN password_changed_at DATETIME NULL AFTER last_login_at'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
