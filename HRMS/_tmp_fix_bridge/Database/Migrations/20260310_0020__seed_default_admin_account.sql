INSERT INTO user_accounts
(
    role_id,
    employee_id,
    username,
    password_hash,
    full_name,
    email,
    status,
    must_change_password,
    password_changed_at
)
SELECT
    r.role_id,
    NULL,
    'admin',
    'pbkdf2_sha256$120000$30lvV8CkqAt3idRfKNK5Nw==$i76cxHu2wmbdFRAa6uRRmAW01RD1Hv/RonkXpJss4eE=',
    'Admin',
    'admin@gmail.com',
    'ACTIVE',
    0,
    CURRENT_TIMESTAMP
FROM roles r
WHERE r.role_name = 'Admin'
ON DUPLICATE KEY UPDATE
    role_id = VALUES(role_id),
    password_hash = VALUES(password_hash),
    full_name = VALUES(full_name),
    email = VALUES(email),
    status = 'ACTIVE',
    must_change_password = 0,
    password_changed_at = CURRENT_TIMESTAMP,
    employee_id = user_accounts.employee_id;
