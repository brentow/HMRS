HRMS SQL Migrations
===================

Place SQL migration files in this folder.

Rules
-----
- File extension: `.sql`
- File naming: sortable prefix + description
  - Example: `20260225_0001__attendance_adjustments_decision_remarks.sql`
- Files run in filename order.
- Applied files are tracked in DB table: `schema_migrations`.

Current migration chain
-----------------------
1. `20260225_0001__core_schema.sql`
2. `20260225_0002__modules_schema.sql`
3. `20260225_0003__seed_base_data.sql`
4. `20260225_0004__seed_attendance_training_dtr.sql`
5. `20260225_0005__seed_extended_operational_data.sql`
6. `20260225_0006__ensure_attendance_adjustments_decision_remarks.sql`
7. `20260225_0007__dedupe_shift_assignments_and_add_unique_key.sql`
8. `20260228_0008__expand_training_enrollment_statuses.sql`
9. `20260305_0009__auth_password_hashing.sql`
10. `20260306_0010__audit_logs.sql`
11. `20260306_0011__payroll_concerns.sql`
12. `20260309_0012__drop_user_accounts_password_column.sql`
13. `20260309_0013__employee_notification_reads.sql`
14. `20260310_0014__account_integrity_and_business_rules.sql`
15. `20260310_0015__attendance_dedupe_and_reporting_indexes.sql`
16. `20260310_0016__expand_audit_log_context.sql`
17. `20260310_0017__sync_linked_user_account_identity.sql`
18. `20260310_0018__expand_employee_sensitive_id_columns.sql`

Usage (from app code)
---------------------
```csharp
var runner = new DbMigrationService(DbConfig.ConnectionString);
var result = await runner.ApplyPendingMigrationsAsync();
```

Notes
-----
- Keep each migration idempotent when possible.
- Avoid destructive changes unless fully reviewed and backed up.
- Migrations should not hardcode `USE hrms_db;` so they run against the configured DB.
