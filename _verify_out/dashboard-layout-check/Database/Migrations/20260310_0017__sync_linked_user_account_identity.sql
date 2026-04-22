UPDATE user_accounts ua
INNER JOIN employees e ON e.employee_id = ua.employee_id
SET ua.full_name = COALESCE(
        NULLIF(TRIM(CONCAT_WS(', ',
            NULLIF(TRIM(e.last_name), ''),
            NULLIF(TRIM(CONCAT_WS(' ',
                NULLIF(TRIM(e.first_name), ''),
                NULLIF(TRIM(e.middle_name), '')
            )), '')
        )), ''),
        ua.full_name
    ),
    ua.email = CASE
        WHEN e.email IS NULL OR TRIM(e.email) = '' THEN NULL
        ELSE TRIM(e.email)
    END,
    ua.updated_at = CURRENT_TIMESTAMP
WHERE ua.employee_id IS NOT NULL;
