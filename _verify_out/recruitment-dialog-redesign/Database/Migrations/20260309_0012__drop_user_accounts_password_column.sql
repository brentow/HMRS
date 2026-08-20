SET @db_name = DATABASE();

SET @has_password = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = @db_name
      AND table_name = 'user_accounts'
      AND column_name = 'password'
);

SET @has_password_hash = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = @db_name
      AND table_name = 'user_accounts'
      AND column_name = 'password_hash'
);

SET @sql = IF(
    @has_password_hash > 0,
    'SELECT COUNT(*) INTO @missing_hash_count FROM user_accounts WHERE COALESCE(password_hash, '''') = '''';',
    'SELECT 0 INTO @missing_hash_count;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    @has_password = 0,
    'SELECT ''user_accounts.password already removed'' AS message;',
    IF(
        @has_password_hash = 0,
        'SELECT ''password_hash column missing; skipped password drop'' AS message;',
        IF(
            @missing_hash_count > 0,
            'SELECT ''password drop skipped: some users have empty password_hash'' AS message;',
            'ALTER TABLE user_accounts DROP COLUMN password;'
        )
    )
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
