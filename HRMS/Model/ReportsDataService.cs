using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record ReportCatalogItem(
        string Key,
        string CategoryKey,
        string CategoryName,
        string ReportName,
        string Description,
        bool SupportsDateRange,
        bool VisibleToEmployees);

    public sealed record ReportDataset(
        string ReportKey,
        string CategoryName,
        string ReportName,
        string Description,
        bool SupportsDateRange,
        DateTime GeneratedAt,
        DateTime? DateFrom,
        DateTime? DateTo,
        DataTable Table);

    public sealed class ReportsDataService
    {
        private readonly string _connectionString;

        private static readonly IReadOnlyList<ReportCatalogItem> Catalog =
        [
            new ReportCatalogItem("EMPLOYEE_MASTERLIST", "CORE_HR", "Core HR", "Employee Masterlist", "Employees with department, position, contact, and status.", false, false),
            new ReportCatalogItem("DEPARTMENT_SUMMARY", "CORE_HR", "Core HR", "Department Summary", "Department-level summary of positions and employees.", false, false),
            new ReportCatalogItem("USER_ACCESS", "CORE_HR", "Core HR", "User Access & Roles", "User accounts, linked employee profile, role, and access status.", false, false),

            new ReportCatalogItem("ATTENDANCE_LOGS", "ATTENDANCE", "Attendance", "Attendance Logs", "Biometric/manual attendance logs by date and employee.", true, true),
            new ReportCatalogItem("ATTENDANCE_DTR", "ATTENDANCE", "Attendance", "DTR Daily Summary", "Daily time record summary with IN/OUT and worked hours.", true, true),
            new ReportCatalogItem("ATTENDANCE_ADJUSTMENTS", "ATTENDANCE", "Attendance", "Attendance Adjustments", "Adjustment requests with approval and decision details.", true, true),
            new ReportCatalogItem("ATTENDANCE_REMARKS", "ATTENDANCE", "Attendance", "Attendance Remarks", "OB/TO/WFH/CTO and other attendance remark records.", true, true),

            new ReportCatalogItem("LEAVE_APPLICATIONS", "LEAVE", "Leave", "Leave Applications", "Leave requests and approval status details.", true, true),
            new ReportCatalogItem("LEAVE_BALANCES", "LEAVE", "Leave", "Leave Balances", "Current leave balance summary per employee and type.", false, true),

            new ReportCatalogItem("PAYROLL_RUNS", "PAYROLL", "Payroll", "Payroll Runs", "Payroll run amounts and status history.", true, true),
            new ReportCatalogItem("PAYSLIP_RELEASES", "PAYROLL", "Payroll", "Payslip Releases", "Released payslips with release details.", true, true),
            new ReportCatalogItem("GOV_DEDUCTIONS_ALL", "PAYROLL", "Payroll", "Government Deductions - All", "Combined SSS/GSIS, PhilHealth, and Pag-IBIG deductions per payroll run.", true, true),
            new ReportCatalogItem("GOV_DEDUCTIONS_SSS", "PAYROLL", "Payroll", "SSS Deductions", "SSS employee and employer share report.", true, true),
            new ReportCatalogItem("GOV_DEDUCTIONS_GSIS", "PAYROLL", "Payroll", "GSIS Deductions", "GSIS employee and employer share report.", true, true),
            new ReportCatalogItem("GOV_DEDUCTIONS_PHILHEALTH", "PAYROLL", "Payroll", "PhilHealth Deductions", "PhilHealth employee and employer share report.", true, true),
            new ReportCatalogItem("GOV_DEDUCTIONS_PAGIBIG", "PAYROLL", "Payroll", "Pag-IBIG Deductions", "Pag-IBIG employee and employer share report.", true, true),

            new ReportCatalogItem("RECRUITMENT_APPLICATIONS", "RECRUITMENT", "Recruitment", "Job Applications", "Applicant pipeline status by posting.", true, false),

            new ReportCatalogItem("TRAINING_ENROLLMENTS", "DEVELOPMENT", "Development", "Training Enrollments", "Course enrollment records and completion status.", true, true),
            new ReportCatalogItem("PERFORMANCE_REVIEWS", "DEVELOPMENT", "Development", "Performance Reviews", "Performance cycle review records and ratings.", true, true),

            new ReportCatalogItem("DOCUMENT_CHECKLIST", "DOCUMENTS", "Documents & Compliance", "Document Checklist Status", "Employee document checklist compliance report.", true, true),
            new ReportCatalogItem("AUDIT_TRAIL", "TRANSACTIONS", "Transactions & Security", "Audit Trail", "System transaction actions from audit logs.", true, true)
        ];

        public ReportsDataService(string connectionString)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? throw new ArgumentException("Connection string is required.", nameof(connectionString))
                : connectionString;
        }

        public IReadOnlyList<ReportCatalogItem> GetCatalog(bool isEmployeeMode)
        {
            return Catalog
                .Where(item => !isEmployeeMode || item.VisibleToEmployees)
                .OrderBy(item => item.CategoryName, StringComparer.Ordinal)
                .ThenBy(item => item.ReportName, StringComparer.Ordinal)
                .ToList();
        }

        public IReadOnlyList<(string CategoryKey, string CategoryName)> GetCategories(bool isEmployeeMode)
        {
            return GetCatalog(isEmployeeMode)
                .GroupBy(item => new { item.CategoryKey, item.CategoryName })
                .Select(group => (group.Key.CategoryKey, group.Key.CategoryName))
                .OrderBy(item => item.CategoryName, StringComparer.Ordinal)
                .ToList();
        }

        public IReadOnlyList<ReportCatalogItem> GetReportsByCategory(string? categoryKey, bool isEmployeeMode)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
            {
                return [];
            }

            return GetCatalog(isEmployeeMode)
                .Where(item => string.Equals(item.CategoryKey, categoryKey.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.ReportName, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<ReportDataset> LoadReportAsync(
            string reportKey,
            DateTime? dateFrom,
            DateTime? dateTo,
            bool isEmployeeMode,
            int? employeeId,
            int? userId)
        {
            if (string.IsNullOrWhiteSpace(reportKey))
            {
                throw new ArgumentException("Report key is required.", nameof(reportKey));
            }

            var catalogItem = GetCatalog(isEmployeeMode)
                .FirstOrDefault(item => string.Equals(item.Key, reportKey.Trim(), StringComparison.OrdinalIgnoreCase));
            if (catalogItem == null)
            {
                throw new InvalidOperationException("Report is not available for the current access scope.");
            }

            if (!catalogItem.SupportsDateRange)
            {
                dateFrom = null;
                dateTo = null;
            }
            else if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value.Date > dateTo.Value.Date)
            {
                throw new InvalidOperationException("Date From cannot be later than Date To.");
            }

            if (isEmployeeMode)
            {
                var needsUserScope = string.Equals(catalogItem.Key, "AUDIT_TRAIL", StringComparison.OrdinalIgnoreCase);
                if (needsUserScope)
                {
                    if (!userId.HasValue || userId.Value <= 0)
                    {
                        throw new InvalidOperationException("Your account is not linked to a valid user scope for this report.");
                    }
                }
                else if (!employeeId.HasValue || employeeId.Value <= 0)
                {
                    throw new InvalidOperationException("Your account is not linked to an employee profile for this report.");
                }
            }

            var table = catalogItem.Key switch
            {
                "EMPLOYEE_MASTERLIST" => await LoadEmployeeMasterlistAsync(isEmployeeMode, employeeId),
                "DEPARTMENT_SUMMARY" => await LoadDepartmentSummaryAsync(),
                "USER_ACCESS" => await LoadUserAccessAsync(),

                "ATTENDANCE_LOGS" => await LoadAttendanceLogsAsync(dateFrom, dateTo, isEmployeeMode, employeeId),
                "ATTENDANCE_DTR" => await LoadAttendanceDtrAsync(dateFrom, dateTo, isEmployeeMode, employeeId),
                "ATTENDANCE_ADJUSTMENTS" => await LoadAttendanceAdjustmentsAsync(dateFrom, dateTo, isEmployeeMode, employeeId),
                "ATTENDANCE_REMARKS" => await LoadAttendanceRemarksAsync(dateFrom, dateTo, isEmployeeMode, employeeId),

                "LEAVE_APPLICATIONS" => await LoadLeaveApplicationsAsync(dateFrom, dateTo, isEmployeeMode, employeeId),
                "LEAVE_BALANCES" => await LoadLeaveBalancesAsync(isEmployeeMode, employeeId),

                "PAYROLL_RUNS" => await LoadPayrollRunsAsync(dateFrom, dateTo, isEmployeeMode, employeeId),
                "PAYSLIP_RELEASES" => await LoadPayslipReleasesAsync(dateFrom, dateTo, isEmployeeMode, employeeId),
                "GOV_DEDUCTIONS_ALL" => await LoadGovernmentDeductionsAsync("ALL", dateFrom, dateTo, isEmployeeMode, employeeId),
                "GOV_DEDUCTIONS_SSS" => await LoadGovernmentDeductionsAsync("SSS", dateFrom, dateTo, isEmployeeMode, employeeId),
                "GOV_DEDUCTIONS_GSIS" => await LoadGovernmentDeductionsAsync("GSIS", dateFrom, dateTo, isEmployeeMode, employeeId),
                "GOV_DEDUCTIONS_PHILHEALTH" => await LoadGovernmentDeductionsAsync("PHILHEALTH", dateFrom, dateTo, isEmployeeMode, employeeId),
                "GOV_DEDUCTIONS_PAGIBIG" => await LoadGovernmentDeductionsAsync("PAGIBIG", dateFrom, dateTo, isEmployeeMode, employeeId),

                "RECRUITMENT_APPLICATIONS" => await LoadRecruitmentApplicationsAsync(dateFrom, dateTo),
                "TRAINING_ENROLLMENTS" => await LoadTrainingEnrollmentsAsync(dateFrom, dateTo, isEmployeeMode, employeeId),
                "PERFORMANCE_REVIEWS" => await LoadPerformanceReviewsAsync(dateFrom, dateTo, isEmployeeMode, employeeId),
                "DOCUMENT_CHECKLIST" => await LoadDocumentChecklistAsync(dateFrom, dateTo, isEmployeeMode, employeeId),
                "AUDIT_TRAIL" => await LoadAuditTrailAsync(dateFrom, dateTo, isEmployeeMode, userId),
                _ => throw new InvalidOperationException("Unsupported report key.")
            };

            return new ReportDataset(
                ReportKey: catalogItem.Key,
                CategoryName: catalogItem.CategoryName,
                ReportName: catalogItem.ReportName,
                Description: catalogItem.Description,
                SupportsDateRange: catalogItem.SupportsDateRange,
                GeneratedAt: DateTime.Now,
                DateFrom: dateFrom?.Date,
                DateTo: dateTo?.Date,
                Table: table);
        }

        private async Task<DataTable> LoadEmployeeMasterlistAsync(bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    COALESCE(d.dept_name, '-') AS `Department`,
    COALESCE(p.position_name, '-') AS `Position`,
    COALESCE(e.email, '-') AS `Email`,
    COALESCE(e.contact_number, '-') AS `Contact`,
    COALESCE(e.status, '-') AS `Status`,
    CASE WHEN e.hire_date IS NULL THEN '-' ELSE DATE_FORMAT(e.hire_date, '%Y-%m-%d') END AS `Hire Date`
FROM employees e
LEFT JOIN departments d ON d.department_id = e.department_id
LEFT JOIN positions p ON p.position_id = e.position_id
WHERE (@employee_id IS NULL OR e.employee_id = @employee_id)
ORDER BY e.employee_no;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadDepartmentSummaryAsync()
        {
            const string sql = @"
SELECT
    d.dept_name AS `Department`,
    COUNT(DISTINCT p.position_id) AS `Positions`,
    COUNT(DISTINCT e.employee_id) AS `Employees`
FROM departments d
LEFT JOIN positions p ON p.department_id = d.department_id
LEFT JOIN employees e ON e.department_id = d.department_id
GROUP BY d.department_id, d.dept_name
ORDER BY d.dept_name;";

            return await ExecuteDataTableAsync(sql, null);
        }

        private async Task<DataTable> LoadUserAccessAsync()
        {
            const string sql = @"
SELECT
    ua.user_id AS `User ID`,
    ua.username AS `Username`,
    COALESCE(r.role_name, 'User') AS `Role`,
    COALESCE(CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')), '-') AS `Linked Employee`,
    COALESCE(ua.email, '-') AS `Email`,
    ua.status AS `Status`,
    CASE WHEN ua.last_login_at IS NULL THEN '-' ELSE DATE_FORMAT(ua.last_login_at, '%Y-%m-%d %H:%i') END AS `Last Login`,
    DATE_FORMAT(ua.created_at, '%Y-%m-%d %H:%i') AS `Created At`
FROM user_accounts ua
LEFT JOIN roles r ON r.role_id = ua.role_id
LEFT JOIN employees e ON e.employee_id = ua.employee_id
ORDER BY ua.username;";

            return await ExecuteDataTableAsync(sql, null);
        }

        private async Task<DataTable> LoadAttendanceLogsAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    DATE_FORMAT(al.log_time, '%Y-%m-%d') AS `Date`,
    DATE_FORMAT(al.log_time, '%H:%i:%s') AS `Time`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    al.log_type AS `Log Type`,
    al.source AS `Source`,
    COALESCE(bd.device_name, '-') AS `Device`
FROM attendance_logs al
INNER JOIN employees e ON e.employee_id = al.employee_id
LEFT JOIN biometric_devices bd ON bd.device_id = al.device_id
WHERE (@employee_id IS NULL OR al.employee_id = @employee_id)
  AND (@date_from IS NULL OR DATE(al.log_time) >= @date_from)
  AND (@date_to IS NULL OR DATE(al.log_time) <= @date_to)
ORDER BY al.log_time DESC
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadAttendanceDtrAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    DATE_FORMAT(v.work_date, '%Y-%m-%d') AS `Work Date`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    DATE_FORMAT(v.time_in, '%H:%i:%s') AS `Time In`,
    DATE_FORMAT(v.time_out, '%H:%i:%s') AS `Time Out`,
    v.worked_minutes AS `Worked Minutes`,
    ROUND(v.worked_minutes / 60, 2) AS `Worked Hours`
FROM v_dtr_daily_effective v
INNER JOIN employees e ON e.employee_id = v.employee_id
WHERE (@employee_id IS NULL OR v.employee_id = @employee_id)
  AND (@date_from IS NULL OR v.work_date >= @date_from)
  AND (@date_to IS NULL OR v.work_date <= @date_to)
ORDER BY v.work_date DESC, e.employee_no
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadAttendanceAdjustmentsAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    a.adjustment_id AS `Adjustment ID`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    DATE_FORMAT(a.work_date, '%Y-%m-%d') AS `Work Date`,
    CASE WHEN a.requested_in IS NULL THEN '-' ELSE DATE_FORMAT(a.requested_in, '%Y-%m-%d %H:%i') END AS `Requested In`,
    CASE WHEN a.requested_out IS NULL THEN '-' ELSE DATE_FORMAT(a.requested_out, '%Y-%m-%d %H:%i') END AS `Requested Out`,
    a.status AS `Status`,
    COALESCE(req.username, '-') AS `Requested By`,
    COALESCE(app.username, '-') AS `Approved By`,
    CASE WHEN a.requested_at IS NULL THEN '-' ELSE DATE_FORMAT(a.requested_at, '%Y-%m-%d %H:%i') END AS `Requested At`,
    CASE WHEN a.decided_at IS NULL THEN '-' ELSE DATE_FORMAT(a.decided_at, '%Y-%m-%d %H:%i') END AS `Decided At`,
    COALESCE(a.reason, '-') AS `Reason`,
    COALESCE(a.decision_remarks, '-') AS `Decision Remarks`
FROM attendance_adjustments a
INNER JOIN employees e ON e.employee_id = a.employee_id
LEFT JOIN user_accounts req ON req.user_id = a.requested_by_user_id
LEFT JOIN user_accounts app ON app.user_id = a.approved_by_user_id
WHERE (@employee_id IS NULL OR a.employee_id = @employee_id)
  AND (@date_from IS NULL OR a.work_date >= @date_from)
  AND (@date_to IS NULL OR a.work_date <= @date_to)
ORDER BY a.requested_at DESC, a.adjustment_id DESC
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadAttendanceRemarksAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    r.remark_id AS `Remark ID`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    DATE_FORMAT(r.work_date, '%Y-%m-%d') AS `Work Date`,
    r.remark_type AS `Remark Type`,
    COALESCE(r.details, '-') AS `Details`,
    DATE_FORMAT(r.created_at, '%Y-%m-%d %H:%i') AS `Created At`
FROM attendance_remarks r
INNER JOIN employees e ON e.employee_id = r.employee_id
WHERE (@employee_id IS NULL OR r.employee_id = @employee_id)
  AND (@date_from IS NULL OR r.work_date >= @date_from)
  AND (@date_to IS NULL OR r.work_date <= @date_to)
ORDER BY r.work_date DESC, r.remark_id DESC
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadLeaveApplicationsAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    la.leave_application_id AS `Application ID`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    lt.name AS `Leave Type`,
    DATE_FORMAT(la.date_from, '%Y-%m-%d') AS `Date From`,
    DATE_FORMAT(la.date_to, '%Y-%m-%d') AS `Date To`,
    la.days_requested AS `Days`,
    la.status AS `Status`,
    CASE WHEN la.filed_at IS NULL THEN '-' ELSE DATE_FORMAT(la.filed_at, '%Y-%m-%d %H:%i') END AS `Filed At`,
    COALESCE(la.decision_remarks, '-') AS `Decision Remarks`
FROM leave_applications la
INNER JOIN employees e ON e.employee_id = la.employee_id
INNER JOIN leave_types lt ON lt.leave_type_id = la.leave_type_id
WHERE (@employee_id IS NULL OR la.employee_id = @employee_id)
  AND (@date_from IS NULL OR la.date_from >= @date_from)
  AND (@date_to IS NULL OR la.date_to <= @date_to)
ORDER BY la.filed_at DESC, la.leave_application_id DESC
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadLeaveBalancesAsync(bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    lt.name AS `Leave Type`,
    lb.`year` AS `Year`,
    lb.opening_credits AS `Opening`,
    lb.earned AS `Earned`,
    lb.used AS `Used`,
    lb.adjustments AS `Adjustments`,
    (lb.opening_credits + lb.earned + lb.adjustments - lb.used) AS `Available`,
    DATE_FORMAT(lb.as_of_date, '%Y-%m-%d') AS `As Of`
FROM leave_balances lb
INNER JOIN employees e ON e.employee_id = lb.employee_id
INNER JOIN leave_types lt ON lt.leave_type_id = lb.leave_type_id
WHERE (@employee_id IS NULL OR lb.employee_id = @employee_id)
ORDER BY e.employee_no, lt.name, lb.`year` DESC;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadPayrollRunsAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    pr.payroll_run_id AS `Run ID`,
    pp.period_code AS `Period`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    pr.gross_pay AS `Gross Pay`,
    pr.deductions_total AS `Deductions`,
    pr.net_pay AS `Net Pay`,
    pr.status AS `Status`,
    DATE_FORMAT(pr.generated_at, '%Y-%m-%d %H:%i') AS `Generated At`
FROM payroll_runs pr
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
INNER JOIN employees e ON e.employee_id = pr.employee_id
WHERE (@employee_id IS NULL OR pr.employee_id = @employee_id)
  AND (@date_from IS NULL OR DATE(pr.generated_at) >= @date_from)
  AND (@date_to IS NULL OR DATE(pr.generated_at) <= @date_to)
ORDER BY pr.generated_at DESC, pr.payroll_run_id DESC
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadPayslipReleasesAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    prl.payslip_release_id AS `Release ID`,
    pp.period_code AS `Period`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    DATE_FORMAT(prl.released_at, '%Y-%m-%d %H:%i') AS `Released At`,
    COALESCE(rel_by.employee_no, '-') AS `Released By`,
    COALESCE(prl.remarks, '-') AS `Remarks`
FROM payslip_releases prl
INNER JOIN payroll_runs pr ON pr.payroll_run_id = prl.payroll_run_id
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
INNER JOIN employees e ON e.employee_id = pr.employee_id
LEFT JOIN employees rel_by ON rel_by.employee_id = prl.released_by_employee_id
WHERE (@employee_id IS NULL OR pr.employee_id = @employee_id)
  AND (@date_from IS NULL OR DATE(prl.released_at) >= @date_from)
  AND (@date_to IS NULL OR DATE(prl.released_at) <= @date_to)
ORDER BY prl.released_at DESC, prl.payslip_release_id DESC
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadGovernmentDeductionsAsync(
            string contributionType,
            DateTime? dateFrom,
            DateTime? dateTo,
            bool isEmployeeMode,
            int? employeeId)
        {
            var sources = await LoadGovernmentContributionSourcesAsync(dateFrom, dateTo, isEmployeeMode, employeeId);
            var normalizedType = contributionType.Trim().ToUpperInvariant();

            return normalizedType == "ALL"
                ? BuildGovernmentDeductionsAllTable(sources)
                : BuildGovernmentDeductionsAgencyTable(sources, normalizedType);
        }

        private async Task<IReadOnlyList<GovernmentContributionSourceRow>> LoadGovernmentContributionSourcesAsync(
            DateTime? dateFrom,
            DateTime? dateTo,
            bool isEmployeeMode,
            int? employeeId)
        {
            const string sql = @"
SELECT
    pr.payroll_run_id,
    pr.payroll_period_id,
    pp.period_code,
    pp.pay_date,
    pr.employee_id,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    COALESCE(at.type_name, 'Permanent') AS employment_type_name,
    pr.basic_pay,
    COALESCE(pr.status, 'GENERATED') AS run_status
FROM payroll_runs pr
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
INNER JOIN employees e ON e.employee_id = pr.employee_id
LEFT JOIN appointment_types at ON at.appointment_type_id = e.appointment_type_id
WHERE (@employee_id IS NULL OR pr.employee_id = @employee_id)
  AND (@date_from IS NULL OR pp.pay_date >= @date_from)
  AND (@date_to IS NULL OR pp.pay_date <= @date_to)
  AND COALESCE(pr.status, 'GENERATED') <> 'VOID'
ORDER BY pp.pay_date DESC, e.employee_no
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            var rows = new List<GovernmentContributionSourceRow>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new GovernmentContributionSourceRow(
                        PayrollRunId: ToLong(reader["payroll_run_id"]),
                        PayrollPeriodId: ToLong(reader["payroll_period_id"]),
                        PeriodCode: reader["period_code"]?.ToString()?.Trim() ?? "-",
                        PayDate: reader["pay_date"] == DBNull.Value
                            ? DateTime.Today
                            : Convert.ToDateTime(reader["pay_date"], CultureInfo.InvariantCulture),
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString()?.Trim() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString()?.Trim() ?? "-",
                        EmploymentTypeName: reader["employment_type_name"]?.ToString()?.Trim() ?? "Permanent",
                        BasicPay: ToDecimal(reader["basic_pay"]),
                        RunStatus: reader["run_status"]?.ToString()?.Trim() ?? "GENERATED"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                throw new InvalidOperationException("Report source table is not available. Please run/update database migrations first.");
            }

            return rows;
        }

        private static DataTable BuildGovernmentDeductionsAllTable(IReadOnlyList<GovernmentContributionSourceRow> sources)
        {
            var table = new DataTable("ReportRows");
            table.Columns.Add("Run ID", typeof(long));
            table.Columns.Add("Period");
            table.Columns.Add("Pay Date");
            table.Columns.Add("Employee No");
            table.Columns.Add("Employee Name");
            table.Columns.Add("Appointment Type");
            table.Columns.Add("Basic Pay", typeof(decimal));
            table.Columns.Add("SSS (Employee)", typeof(decimal));
            table.Columns.Add("SSS (Employer)", typeof(decimal));
            table.Columns.Add("GSIS (Employee)", typeof(decimal));
            table.Columns.Add("GSIS (Employer)", typeof(decimal));
            table.Columns.Add("PhilHealth (Employee)", typeof(decimal));
            table.Columns.Add("PhilHealth (Employer)", typeof(decimal));
            table.Columns.Add("Pag-IBIG (Employee)", typeof(decimal));
            table.Columns.Add("Pag-IBIG (Employer)", typeof(decimal));
            table.Columns.Add("Total Employee Share", typeof(decimal));
            table.Columns.Add("Total Employer Share", typeof(decimal));
            table.Columns.Add("Total Remittance", typeof(decimal));
            table.Columns.Add("Run Status");

            foreach (var source in sources.OrderByDescending(x => x.PayDate).ThenBy(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase))
            {
                var deductions = PhilippinePayrollDeductions.ComputeAll(source.BasicPay, source.EmploymentTypeName);
                var employeeTotal = deductions.SssContribution + deductions.GsisContribution + deductions.PhilHealthContribution + deductions.PagIBIGContribution;
                var employerTotal = deductions.SssEmployerShare + deductions.GsisEmployerShare + deductions.PhilHealthEmployerShare + deductions.PagIBIGEmployerShare;
                var remittanceTotal = employeeTotal + employerTotal;
                if (remittanceTotal <= 0m)
                {
                    continue;
                }

                table.Rows.Add(
                    source.PayrollRunId,
                    source.PeriodCode,
                    source.PayDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    source.EmployeeNo,
                    source.EmployeeName,
                    source.EmploymentTypeName,
                    source.BasicPay,
                    deductions.SssContribution,
                    deductions.SssEmployerShare,
                    deductions.GsisContribution,
                    deductions.GsisEmployerShare,
                    deductions.PhilHealthContribution,
                    deductions.PhilHealthEmployerShare,
                    deductions.PagIBIGContribution,
                    deductions.PagIBIGEmployerShare,
                    employeeTotal,
                    employerTotal,
                    remittanceTotal,
                    source.RunStatus);
            }

            return table;
        }

        private static DataTable BuildGovernmentDeductionsAgencyTable(IReadOnlyList<GovernmentContributionSourceRow> sources, string contributionType)
        {
            var table = new DataTable("ReportRows");
            table.Columns.Add("Run ID", typeof(long));
            table.Columns.Add("Period");
            table.Columns.Add("Pay Date");
            table.Columns.Add("Employee No");
            table.Columns.Add("Employee Name");
            table.Columns.Add("Appointment Type");
            table.Columns.Add("Basic Pay", typeof(decimal));
            table.Columns.Add("Employee Share", typeof(decimal));
            table.Columns.Add("Employer Share", typeof(decimal));
            table.Columns.Add("Total Remittance", typeof(decimal));
            table.Columns.Add("Run Status");

            foreach (var source in sources.OrderByDescending(x => x.PayDate).ThenBy(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase))
            {
                var deductions = PhilippinePayrollDeductions.ComputeAll(source.BasicPay, source.EmploymentTypeName);
                var (employeeShare, employerShare) = GetGovernmentContributionAmounts(deductions, contributionType);
                var totalRemittance = employeeShare + employerShare;
                if (totalRemittance <= 0m)
                {
                    continue;
                }

                table.Rows.Add(
                    source.PayrollRunId,
                    source.PeriodCode,
                    source.PayDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    source.EmployeeNo,
                    source.EmployeeName,
                    source.EmploymentTypeName,
                    source.BasicPay,
                    employeeShare,
                    employerShare,
                    totalRemittance,
                    source.RunStatus);
            }

            return table;
        }

        private static (decimal EmployeeShare, decimal EmployerShare) GetGovernmentContributionAmounts(
            PayrollDeductionResult deductions,
            string contributionType)
        {
            return contributionType switch
            {
                "SSS" => (deductions.SssContribution, deductions.SssEmployerShare),
                "GSIS" => (deductions.GsisContribution, deductions.GsisEmployerShare),
                "PHILHEALTH" => (deductions.PhilHealthContribution, deductions.PhilHealthEmployerShare),
                "PAGIBIG" => (deductions.PagIBIGContribution, deductions.PagIBIGEmployerShare),
                _ => (0m, 0m)
            };
        }

        private async Task<DataTable> LoadRecruitmentApplicationsAsync(DateTime? dateFrom, DateTime? dateTo)
        {
            const string sql = @"
SELECT
    ja.job_application_id AS `Application ID`,
    jp.posting_code AS `Posting Code`,
    jp.title AS `Job Title`,
    CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) AS `Applicant`,
    ja.status AS `Status`,
    CASE WHEN ja.applied_at IS NULL THEN '-' ELSE DATE_FORMAT(ja.applied_at, '%Y-%m-%d %H:%i') END AS `Applied At`
FROM job_applications ja
INNER JOIN applicants a ON a.applicant_id = ja.applicant_id
INNER JOIN job_postings jp ON jp.job_posting_id = ja.job_posting_id
WHERE (@date_from IS NULL OR DATE(ja.applied_at) >= @date_from)
  AND (@date_to IS NULL OR DATE(ja.applied_at) <= @date_to)
ORDER BY ja.applied_at DESC, ja.job_application_id DESC
LIMIT 5000;";

            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadTrainingEnrollmentsAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    te.enrollment_id AS `Enrollment ID`,
    tc.course_name AS `Course`,
    DATE_FORMAT(ts.session_date, '%Y-%m-%d') AS `Session Date`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    te.status AS `Status`,
    DATE_FORMAT(te.created_at, '%Y-%m-%d %H:%i') AS `Created At`
FROM training_enrollments te
INNER JOIN training_sessions ts ON ts.session_id = te.session_id
INNER JOIN training_courses tc ON tc.course_id = ts.course_id
INNER JOIN employees e ON e.employee_id = te.employee_id
WHERE (@employee_id IS NULL OR te.employee_id = @employee_id)
  AND (@date_from IS NULL OR ts.session_date >= @date_from)
  AND (@date_to IS NULL OR ts.session_date <= @date_to)
ORDER BY ts.session_date DESC, te.enrollment_id DESC
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadPerformanceReviewsAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    pr.performance_review_id AS `Review ID`,
    pc.cycle_code AS `Cycle Code`,
    pc.name AS `Cycle`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    COALESCE(rev.employee_no, '-') AS `Reviewer No`,
    COALESCE(CONCAT(rev.last_name, ', ', rev.first_name, IFNULL(CONCAT(' ', rev.middle_name), '')), '-') AS `Reviewer`,
    COALESCE(pr.overall_rating, 0) AS `Rating`,
    pr.status AS `Status`,
    CASE WHEN pr.submitted_at IS NULL THEN '-' ELSE DATE_FORMAT(pr.submitted_at, '%Y-%m-%d %H:%i') END AS `Submitted At`,
    CASE WHEN pr.decided_at IS NULL THEN '-' ELSE DATE_FORMAT(pr.decided_at, '%Y-%m-%d %H:%i') END AS `Decided At`
FROM performance_reviews pr
INNER JOIN performance_cycles pc ON pc.performance_cycle_id = pr.performance_cycle_id
INNER JOIN employees e ON e.employee_id = pr.employee_id
LEFT JOIN employees rev ON rev.employee_id = pr.reviewer_employee_id
WHERE (@employee_id IS NULL OR pr.employee_id = @employee_id)
  AND (@date_from IS NULL OR DATE(COALESCE(pr.submitted_at, pr.created_at)) >= @date_from)
  AND (@date_to IS NULL OR DATE(COALESCE(pr.submitted_at, pr.created_at)) <= @date_to)
ORDER BY COALESCE(pr.submitted_at, pr.created_at) DESC, pr.performance_review_id DESC
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadDocumentChecklistAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? employeeId)
        {
            const string sql = @"
SELECT
    dc.checklist_id AS `Checklist ID`,
    e.employee_no AS `Employee No`,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS `Employee Name`,
    dc.position_name AS `Position`,
    dc.employment_type AS `Employment Type`,
    dc.document_code AS `Document Code`,
    dc.document_name AS `Document Name`,
    dc.status AS `Status`,
    CASE WHEN dc.submitted_date IS NULL THEN '-' ELSE DATE_FORMAT(dc.submitted_date, '%Y-%m-%d') END AS `Submitted`,
    CASE WHEN dc.expiry_date IS NULL THEN '-' ELSE DATE_FORMAT(dc.expiry_date, '%Y-%m-%d') END AS `Expiry`,
    CASE WHEN dc.verified_date IS NULL THEN '-' ELSE DATE_FORMAT(dc.verified_date, '%Y-%m-%d') END AS `Verified`,
    COALESCE(dc.verified_by, '-') AS `Verified By`,
    DATE_FORMAT(dc.updated_at, '%Y-%m-%d %H:%i') AS `Updated At`
FROM employee_document_checklist dc
INNER JOIN employees e ON e.employee_id = dc.employee_id
WHERE (@employee_id IS NULL OR dc.employee_id = @employee_id)
  AND (@date_from IS NULL OR DATE(dc.updated_at) >= @date_from)
  AND (@date_to IS NULL OR DATE(dc.updated_at) <= @date_to)
ORDER BY dc.updated_at DESC, dc.checklist_id DESC
LIMIT 5000;";

            var scopedEmployeeId = isEmployeeMode ? employeeId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@employee_id", scopedEmployeeId.HasValue && scopedEmployeeId.Value > 0
                    ? scopedEmployeeId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> LoadAuditTrailAsync(DateTime? dateFrom, DateTime? dateTo, bool isEmployeeMode, int? userId)
        {
            const string sql = @"
SELECT
    al.audit_log_id AS `Audit ID`,
    CASE WHEN al.created_at IS NULL THEN '-' ELSE DATE_FORMAT(al.created_at, '%Y-%m-%d %H:%i') END AS `Date / Time`,
    al.action_code AS `Action`,
    al.target_type AS `Module`,
    COALESCE(al.target_id, '-') AS `Target`,
    al.result_status AS `Status`,
    COALESCE(NULLIF(ua.username, ''), 'System') AS `User`,
    COALESCE(al.details, '-') AS `Details`
FROM audit_logs al
LEFT JOIN user_accounts ua ON ua.user_id = al.acted_by_user_id
WHERE (@user_id IS NULL OR al.acted_by_user_id = @user_id)
  AND (@date_from IS NULL OR DATE(al.created_at) >= @date_from)
  AND (@date_to IS NULL OR DATE(al.created_at) <= @date_to)
ORDER BY al.created_at DESC, al.audit_log_id DESC
LIMIT 5000;";

            var scopedUserId = isEmployeeMode ? userId : null;
            return await ExecuteDataTableAsync(sql, command =>
            {
                command.Parameters.AddWithValue("@user_id", scopedUserId.HasValue && scopedUserId.Value > 0
                    ? scopedUserId.Value
                    : DBNull.Value);
                command.Parameters.AddWithValue("@date_from", dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@date_to", dateTo.HasValue ? dateTo.Value.Date : DBNull.Value);
            });
        }

        private async Task<DataTable> ExecuteDataTableAsync(string sql, Action<MySqlCommand>? configureCommand)
        {
            var table = new DataTable("ReportRows");

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                configureCommand?.Invoke(command);

                await using var reader = await command.ExecuteReaderAsync();
                table.Load(reader);
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                throw new InvalidOperationException("Report source table is not available. Please run/update database migrations first.");
            }

            return table;
        }

        private static int ToInt(object value)
        {
            if (value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static long ToLong(object value)
        {
            if (value == DBNull.Value)
            {
                return 0L;
            }

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static decimal ToDecimal(object value)
        {
            if (value == DBNull.Value)
            {
                return 0m;
            }

            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        private static bool IsMissingObjectError(MySqlException ex) =>
            ex.Number is 1146 or 1054;

        private sealed record GovernmentContributionSourceRow(
            long PayrollRunId,
            long PayrollPeriodId,
            string PeriodCode,
            DateTime PayDate,
            int EmployeeId,
            string EmployeeNo,
            string EmployeeName,
            string EmploymentTypeName,
            decimal BasicPay,
            string RunStatus);
    }
}
