using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record PayrollStatsDto(
        int TotalPeriods,
        int OpenPeriods,
        int TotalRuns,
        int ReleasedPayslips,
        decimal TotalNetPay);

    public record PayrollEmployeeOptionDto(
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName);

    public record PayrollPeriodDto(
        long PayrollPeriodId,
        string PeriodCode,
        DateTime DateFrom,
        DateTime DateTo,
        DateTime PayDate,
        string Status,
        DateTime CreatedAt);

    public record PayrollRunDto(
        long PayrollRunId,
        long PayrollPeriodId,
        string PeriodCode,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        decimal BasicPay,
        decimal Allowances,
        decimal OvertimePay,
        decimal OtherEarnings,
        decimal GrossPay,
        decimal DeductionsTotal,
        decimal NetPay,
        string Status,
        DateTime GeneratedAt,
        DateTime? LastReleasedAt,
        int ReleaseCount);

    public record PayrollReleaseDto(
        long PayslipReleaseId,
        long PayrollRunId,
        string PeriodCode,
        string EmployeeNo,
        string EmployeeName,
        DateTime ReleasedAt,
        string RunStatus,
        string ReleasedBy,
        string Remarks);

    public record PayrollRunItemDto(
        long PayrollRunItemId,
        long PayrollRunId,
        string ItemType,
        string Code,
        string Description,
        decimal Amount);

    public record PayrollRunEditorDefaultsDto(
        decimal BasicPay,
        decimal Allowances,
        decimal OvertimePay,
        decimal OtherEarnings,
        decimal DeductionsTotal,
        string Status,
        bool FromExistingRun,
        string EmploymentTypeName,
        string PositionName);

    public record PayrollGovernmentContributionSourceDto(
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

    internal record PayrollEmployeeCompensationDto(
        decimal MonthlyRate,
        decimal DefaultAllowances,
        string EmploymentTypeName,
        string PositionName);

    public class PayrollDataService
    {
        private readonly string _connectionString;
        private readonly AuditLogWriter _auditLogWriter;
        private readonly ConsolidatedTransactionSyncService _consolidatedTransactionSyncService;

        public PayrollDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _auditLogWriter = new AuditLogWriter(_connectionString);
            _consolidatedTransactionSyncService = new ConsolidatedTransactionSyncService(GgmsConfig.ConnectionString);
        }

        public async Task<PayrollStatsDto> GetStatsAsync(int? employeeId = null)
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*)
       FROM payroll_periods pp
      WHERE @employee_id IS NULL
         OR EXISTS (
              SELECT 1
              FROM payroll_runs pr
              WHERE pr.payroll_period_id = pp.payroll_period_id
                AND pr.employee_id = @employee_id
         )
    ) AS total_periods,
    (SELECT COUNT(*)
       FROM payroll_periods pp
      WHERE pp.status = 'OPEN'
        AND (
            @employee_id IS NULL
            OR EXISTS (
                SELECT 1
                FROM payroll_runs pr
                WHERE pr.payroll_period_id = pp.payroll_period_id
                  AND pr.employee_id = @employee_id
            )
        )
    ) AS open_periods,
    (SELECT COUNT(*)
       FROM payroll_runs pr
      WHERE @employee_id IS NULL OR pr.employee_id = @employee_id
    ) AS total_runs,
    (SELECT COUNT(*)
       FROM payslip_releases prl
       JOIN payroll_runs pr ON pr.payroll_run_id = prl.payroll_run_id
      WHERE @employee_id IS NULL OR pr.employee_id = @employee_id
    ) AS released_payslips,
    COALESCE((
      SELECT SUM(pr.net_pay)
      FROM payroll_runs pr
      WHERE @employee_id IS NULL OR pr.employee_id = @employee_id
    ), 0) AS total_net_pay;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue ? employeeId.Value : DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new PayrollStatsDto(0, 0, 0, 0, 0m);
                }

                return new PayrollStatsDto(
                    TotalPeriods: ToInt(reader["total_periods"]),
                    OpenPeriods: ToInt(reader["open_periods"]),
                    TotalRuns: ToInt(reader["total_runs"]),
                    ReleasedPayslips: ToInt(reader["released_payslips"]),
                    TotalNetPay: ToDecimal(reader["total_net_pay"]));
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return new PayrollStatsDto(0, 0, 0, 0, 0m);
            }
        }

        public async Task<IReadOnlyList<PayrollEmployeeOptionDto>> GetEmployeesAsync(int? employeeId = null)
        {
            const string sql = @"
SELECT
    e.employee_id,
    e.employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name
FROM employees e
WHERE e.status = 'ACTIVE'
  AND (@employee_id IS NULL OR e.employee_id = @employee_id)
ORDER BY e.employee_no;";

            var rows = new List<PayrollEmployeeOptionDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue ? employeeId.Value : DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    rows.Add(new PayrollEmployeeOptionDto(
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<PayrollEmployeeOptionDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<PayrollPeriodDto>> GetPeriodsAsync(string? statusFilter = null, string? search = null, int limit = 300, int? employeeId = null)
        {
            const string sql = @"
SELECT
    pp.payroll_period_id,
    pp.period_code,
    pp.date_from,
    pp.date_to,
    pp.pay_date,
    pp.status,
    pp.created_at
FROM payroll_periods pp
WHERE (@status IS NULL OR pp.status = @status)
  AND (
        @employee_id IS NULL OR
        EXISTS (
            SELECT 1
            FROM payroll_runs pr
            WHERE pr.payroll_period_id = pp.payroll_period_id
              AND pr.employee_id = @employee_id
        )
  )
  AND (
        @search = '' OR
        pp.period_code LIKE CONCAT('%', @search, '%')
  )
ORDER BY pp.date_from DESC, pp.payroll_period_id DESC
LIMIT @limit;";

            var rows = new List<PayrollPeriodDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@status", NormalizeStatusFilter(statusFilter));
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue ? employeeId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new PayrollPeriodDto(
                        PayrollPeriodId: ToLong(reader["payroll_period_id"]),
                        PeriodCode: reader["period_code"]?.ToString() ?? "-",
                        DateFrom: reader["date_from"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["date_from"], CultureInfo.InvariantCulture),
                        DateTo: reader["date_to"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["date_to"], CultureInfo.InvariantCulture),
                        PayDate: reader["pay_date"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["pay_date"], CultureInfo.InvariantCulture),
                        Status: reader["status"]?.ToString() ?? "OPEN",
                        CreatedAt: reader["created_at"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["created_at"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<PayrollPeriodDto>();
            }

            return rows;
        }

        public async Task<long> AddPeriodAsync(string periodCode, DateTime dateFrom, DateTime dateTo, DateTime payDate, string? status)
        {
            if (string.IsNullOrWhiteSpace(periodCode))
            {
                throw new InvalidOperationException("Period code is required.");
            }

            var normalizedStatus = NormalizePeriodStatus(status);
            var from = dateFrom.Date;
            var to = dateTo.Date;
            if (to < from)
            {
                throw new InvalidOperationException("Date To cannot be earlier than Date From.");
            }

            const string sql = @"
INSERT INTO payroll_periods (period_code, date_from, date_to, pay_date, status)
VALUES (@period_code, @date_from, @date_to, @pay_date, @status);
SELECT LAST_INSERT_ID();";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@period_code", periodCode.Trim());
            command.Parameters.AddWithValue("@date_from", from);
            command.Parameters.AddWithValue("@date_to", to);
            command.Parameters.AddWithValue("@pay_date", payDate.Date);
            command.Parameters.AddWithValue("@status", normalizedStatus);
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value
                ? 0
                : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        public async Task UpdatePeriodStatusAsync(long payrollPeriodId, string? status, int? actingUserId)
        {
            if (payrollPeriodId <= 0)
            {
                throw new InvalidOperationException("Invalid payroll period.");
            }

            const string sql = @"
UPDATE payroll_periods
SET status = @status,
    updated_at = CURRENT_TIMESTAMP
WHERE payroll_period_id = @payroll_period_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var targetId = payrollPeriodId.ToString(CultureInfo.InvariantCulture);
            var normalizedStatus = NormalizePeriodStatus(status);

            await EnsureAdminOrHrAccessAsync(
                connection,
                actingUserId,
                "update payroll period status",
                "PAYROLL_PERIOD_STATUS_UPDATE",
                "payroll_periods",
                targetId);

            try
            {
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@payroll_period_id", payrollPeriodId);
                command.Parameters.AddWithValue("@status", normalizedStatus);
                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await _auditLogWriter.TryWriteAsync(
                        actingUserId,
                        "PAYROLL_PERIOD_STATUS_UPDATE",
                        "payroll_periods",
                        targetId,
                        "FAILED",
                        "Payroll period not found.");
                    throw new InvalidOperationException("Payroll period not found.");
                }

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_PERIOD_STATUS_UPDATE",
                    "payroll_periods",
                    targetId,
                    "SUCCESS",
                    $"Status='{normalizedStatus}'.");
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_PERIOD_STATUS_UPDATE",
                    "payroll_periods",
                    targetId,
                    "FAILED",
                    ex.Message);
                throw;
            }
        }

        public async Task DeletePeriodAsync(long payrollPeriodId, int? actingUserId)
        {
            if (payrollPeriodId <= 0)
            {
                throw new InvalidOperationException("Invalid payroll period.");
            }

            const string sql = @"
DELETE FROM payroll_periods
WHERE payroll_period_id = @payroll_period_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var targetId = payrollPeriodId.ToString(CultureInfo.InvariantCulture);
            await EnsureAdminOrHrAccessAsync(
                connection,
                actingUserId,
                "delete payroll periods",
                "PAYROLL_PERIOD_DELETE",
                "payroll_periods",
                targetId);

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@payroll_period_id", payrollPeriodId);
            try
            {
                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await _auditLogWriter.TryWriteAsync(
                        actingUserId,
                        "PAYROLL_PERIOD_DELETE",
                        "payroll_periods",
                        targetId,
                        "FAILED",
                        "Payroll period not found.");
                    throw new InvalidOperationException("Payroll period not found.");
                }

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_PERIOD_DELETE",
                    "payroll_periods",
                    targetId,
                    "SUCCESS",
                    "Payroll period deleted.");
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_PERIOD_DELETE",
                    "payroll_periods",
                    targetId,
                    "FAILED",
                    "Cannot delete payroll period with existing payroll runs.");
                throw new InvalidOperationException("Cannot delete payroll period with existing payroll runs.");
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_PERIOD_DELETE",
                    "payroll_periods",
                    targetId,
                    "FAILED",
                    ex.Message);
                throw;
            }
        }

        public async Task<IReadOnlyList<PayrollRunDto>> GetRunsAsync(long? periodId = null, string? statusFilter = null, string? search = null, int limit = 500, int? employeeId = null)
        {
            const string sql = @"
SELECT
    pr.payroll_run_id,
    pr.payroll_period_id,
    pp.period_code,
    pr.employee_id,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    pr.basic_pay,
    pr.allowances,
    pr.overtime_pay,
    pr.other_earnings,
    pr.gross_pay,
    pr.deductions_total,
    pr.net_pay,
    pr.status,
    pr.generated_at,
    rel.last_released_at,
    COALESCE(rel.release_count, 0) AS release_count
FROM payroll_runs pr
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
INNER JOIN employees e ON e.employee_id = pr.employee_id
LEFT JOIN (
    SELECT payroll_run_id, MAX(released_at) AS last_released_at, COUNT(*) AS release_count
    FROM payslip_releases
    GROUP BY payroll_run_id
) rel ON rel.payroll_run_id = pr.payroll_run_id
WHERE (@period_id IS NULL OR pr.payroll_period_id = @period_id)
  AND (@employee_id IS NULL OR pr.employee_id = @employee_id)
  AND (@status IS NULL OR pr.status = @status)
  AND (
        @search = '' OR
        e.employee_no LIKE CONCAT('%', @search, '%') OR
        CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) LIKE CONCAT('%', @search, '%') OR
        pp.period_code LIKE CONCAT('%', @search, '%')
  )
ORDER BY pp.pay_date DESC, e.employee_no
LIMIT @limit;";

            var rows = new List<PayrollRunDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@period_id", periodId.HasValue && periodId.Value > 0 ? periodId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue ? employeeId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@status", NormalizeStatusFilter(statusFilter));
                command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new PayrollRunDto(
                        PayrollRunId: ToLong(reader["payroll_run_id"]),
                        PayrollPeriodId: ToLong(reader["payroll_period_id"]),
                        PeriodCode: reader["period_code"]?.ToString() ?? "-",
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        BasicPay: ToDecimal(reader["basic_pay"]),
                        Allowances: ToDecimal(reader["allowances"]),
                        OvertimePay: ToDecimal(reader["overtime_pay"]),
                        OtherEarnings: ToDecimal(reader["other_earnings"]),
                        GrossPay: ToDecimal(reader["gross_pay"]),
                        DeductionsTotal: ToDecimal(reader["deductions_total"]),
                        NetPay: ToDecimal(reader["net_pay"]),
                        Status: reader["status"]?.ToString() ?? "GENERATED",
                        GeneratedAt: reader["generated_at"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["generated_at"], CultureInfo.InvariantCulture),
                        LastReleasedAt: reader["last_released_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["last_released_at"], CultureInfo.InvariantCulture),
                        ReleaseCount: ToInt(reader["release_count"])));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<PayrollRunDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<PayrollGovernmentContributionSourceDto>> GetGovernmentContributionSourcesAsync(long? periodId = null, int limit = 1000)
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
    pr.status
FROM payroll_runs pr
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
INNER JOIN employees e ON e.employee_id = pr.employee_id
LEFT JOIN appointment_types at ON at.appointment_type_id = e.appointment_type_id
WHERE (@period_id IS NULL OR pr.payroll_period_id = @period_id)
  AND COALESCE(pr.status, 'GENERATED') <> 'VOID'
ORDER BY pp.pay_date DESC, e.employee_no
LIMIT @limit;";

            var rows = new List<PayrollGovernmentContributionSourceDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@period_id", periodId.HasValue && periodId.Value > 0 ? periodId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new PayrollGovernmentContributionSourceDto(
                        PayrollRunId: ToLong(reader["payroll_run_id"]),
                        PayrollPeriodId: ToLong(reader["payroll_period_id"]),
                        PeriodCode: reader["period_code"]?.ToString() ?? "-",
                        PayDate: reader["pay_date"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["pay_date"], CultureInfo.InvariantCulture),
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        EmploymentTypeName: reader["employment_type_name"]?.ToString() ?? "Permanent",
                        BasicPay: ToDecimal(reader["basic_pay"]),
                        RunStatus: reader["status"]?.ToString() ?? "GENERATED"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<PayrollGovernmentContributionSourceDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<PayrollRunItemDto>> GetRunItemsAsync(long payrollRunId, string? itemType = null)
        {
            if (payrollRunId <= 0)
            {
                return Array.Empty<PayrollRunItemDto>();
            }

            const string sql = @"
SELECT
    payroll_run_item_id,
    payroll_run_id,
    item_type,
    code,
    COALESCE(description, '') AS description,
    amount
FROM payroll_run_items
WHERE payroll_run_id = @payroll_run_id
  AND (@item_type = '' OR UPPER(item_type) = @item_type)
ORDER BY
    CASE WHEN UPPER(item_type) = 'EARNING' THEN 0 ELSE 1 END,
    payroll_run_item_id;";

            var rows = new List<PayrollRunItemDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
                command.Parameters.AddWithValue("@item_type", string.IsNullOrWhiteSpace(itemType) ? string.Empty : itemType.Trim().ToUpperInvariant());

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new PayrollRunItemDto(
                        PayrollRunItemId: ToLong(reader["payroll_run_item_id"]),
                        PayrollRunId: ToLong(reader["payroll_run_id"]),
                        ItemType: reader["item_type"]?.ToString() ?? string.Empty,
                        Code: reader["code"]?.ToString() ?? string.Empty,
                        Description: reader["description"]?.ToString() ?? string.Empty,
                        Amount: ToDecimal(reader["amount"])));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<PayrollRunItemDto>();
            }

            return rows;
        }

        public async Task<long> UpsertRunAsync(
            long payrollPeriodId,
            int employeeId,
            decimal basicPay,
            decimal allowances,
            decimal overtimePay,
            decimal otherEarnings,
            decimal deductionsTotal,
            string? status)
        {
            if (payrollPeriodId <= 0 || employeeId <= 0)
            {
                throw new InvalidOperationException("Payroll period and employee are required.");
            }

            _ = deductionsTotal;

            const string sql = @"
INSERT INTO payroll_runs
    (payroll_period_id, employee_id, basic_pay, allowances, overtime_pay, other_earnings, gross_pay, deductions_total, net_pay, status, generated_at)
VALUES
    (@payroll_period_id, @employee_id, @basic_pay, @allowances, @overtime_pay, @other_earnings, @gross_pay, @deductions_total, @net_pay, @status, NOW())
ON DUPLICATE KEY UPDATE
    basic_pay = VALUES(basic_pay),
    allowances = VALUES(allowances),
    overtime_pay = VALUES(overtime_pay),
    other_earnings = VALUES(other_earnings),
    gross_pay = VALUES(gross_pay),
    deductions_total = VALUES(deductions_total),
    net_pay = VALUES(net_pay),
    status = VALUES(status),
    generated_at = CURRENT_TIMESTAMP,
    payroll_run_id = LAST_INSERT_ID(payroll_run_id);
SELECT LAST_INSERT_ID();";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var compensation = await GetEmployeeCompensationAsync(connection, transaction, employeeId);
                var effectiveBasicPay = basicPay > 0m ? basicPay : compensation.MonthlyRate;
                var deductions = PhilippinePayrollDeductions.ComputeAll(
                    basicMonthlySalary: effectiveBasicPay,
                    employmentTypeName: compensation.EmploymentTypeName,
                    allowances: allowances,
                    overtimePay: overtimePay,
                    otherEarnings: otherEarnings);

                await using var command = new MySqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@payroll_period_id", payrollPeriodId);
                command.Parameters.AddWithValue("@employee_id", employeeId);
                command.Parameters.AddWithValue("@basic_pay", effectiveBasicPay);
                command.Parameters.AddWithValue("@allowances", allowances);
                command.Parameters.AddWithValue("@overtime_pay", overtimePay);
                command.Parameters.AddWithValue("@other_earnings", otherEarnings);
                command.Parameters.AddWithValue("@gross_pay", deductions.GrossPay);
                command.Parameters.AddWithValue("@deductions_total", deductions.TotalDeductions);
                command.Parameters.AddWithValue("@net_pay", deductions.NetPay);
                command.Parameters.AddWithValue("@status", NormalizeRunStatus(status));
                var result = await command.ExecuteScalarAsync();
                var payrollRunId = result == null || result == DBNull.Value
                    ? 0L
                    : Convert.ToInt64(result, CultureInfo.InvariantCulture);

                if (payrollRunId <= 0)
                {
                    throw new InvalidOperationException("Payroll run could not be saved.");
                }

                await ReplaceRunItemsAsync(
                    connection,
                    transaction,
                    payrollRunId,
                    effectiveBasicPay,
                    allowances,
                    overtimePay,
                    otherEarnings,
                    compensation.EmploymentTypeName,
                    deductions);

                await transaction.CommitAsync();
                return payrollRunId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PayrollRunEditorDefaultsDto> GetRunEditorDefaultsAsync(long payrollPeriodId, int employeeId)
        {
            if (payrollPeriodId <= 0 || employeeId <= 0)
            {
                return new PayrollRunEditorDefaultsDto(0m, 0m, 0m, 0m, 0m, "GENERATED", false, string.Empty, string.Empty);
            }

            const string existingRunSql = @"
SELECT
    basic_pay,
    allowances,
    overtime_pay,
    other_earnings,
    deductions_total,
    status
FROM payroll_runs
WHERE payroll_period_id = @payroll_period_id
  AND employee_id = @employee_id
LIMIT 1;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                var compensation = await GetEmployeeCompensationAsync(connection, null, employeeId);

                await using (var existingRunCommand = new MySqlCommand(existingRunSql, connection))
                {
                    existingRunCommand.Parameters.AddWithValue("@payroll_period_id", payrollPeriodId);
                    existingRunCommand.Parameters.AddWithValue("@employee_id", employeeId);
                    await using var existingRunReader = await existingRunCommand.ExecuteReaderAsync();
                    if (await existingRunReader.ReadAsync())
                    {
                        return new PayrollRunEditorDefaultsDto(
                            BasicPay: ToDecimal(existingRunReader["basic_pay"]),
                            Allowances: ToDecimal(existingRunReader["allowances"]),
                            OvertimePay: ToDecimal(existingRunReader["overtime_pay"]),
                            OtherEarnings: ToDecimal(existingRunReader["other_earnings"]),
                            DeductionsTotal: ToDecimal(existingRunReader["deductions_total"]),
                            Status: existingRunReader["status"]?.ToString() ?? "GENERATED",
                            FromExistingRun: true,
                            EmploymentTypeName: compensation.EmploymentTypeName,
                            PositionName: compensation.PositionName);
                    }
                }

                var basicPay = compensation.MonthlyRate;
                var allowances = compensation.DefaultAllowances;
                var deductions = PhilippinePayrollDeductions.ComputeAll(
                    basicMonthlySalary: basicPay,
                    employmentTypeName: compensation.EmploymentTypeName,
                    allowances: allowances,
                    overtimePay: 0m,
                    otherEarnings: 0m);

                return new PayrollRunEditorDefaultsDto(
                    BasicPay: basicPay,
                    Allowances: allowances,
                    OvertimePay: 0m,
                    OtherEarnings: 0m,
                    DeductionsTotal: deductions.TotalDeductions,
                    Status: "GENERATED",
                    FromExistingRun: false,
                    EmploymentTypeName: compensation.EmploymentTypeName,
                    PositionName: compensation.PositionName);
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return new PayrollRunEditorDefaultsDto(0m, 0m, 0m, 0m, 0m, "GENERATED", false, string.Empty, string.Empty);
            }
        }

        public async Task UpdateRunStatusAsync(long payrollRunId, string? status, int? actingUserId)
        {
            if (payrollRunId <= 0)
            {
                throw new InvalidOperationException("Invalid payroll run.");
            }

            const string sql = @"
UPDATE payroll_runs
SET status = @status
WHERE payroll_run_id = @payroll_run_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var targetId = payrollRunId.ToString(CultureInfo.InvariantCulture);
            var normalizedStatus = NormalizeRunStatus(status);
            await EnsureAdminOrHrAccessAsync(
                connection,
                actingUserId,
                "update payroll run status",
                "PAYROLL_RUN_STATUS_UPDATE",
                "payroll_runs",
                targetId);
            var previousStatus = await GetPayrollRunStatusAsync(connection, payrollRunId);
            var shouldWriteConsolidated = normalizedStatus == "RELEASED" &&
                                          !string.Equals(previousStatus, "RELEASED", StringComparison.OrdinalIgnoreCase);

            try
            {
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
                command.Parameters.AddWithValue("@status", normalizedStatus);
                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await _auditLogWriter.TryWriteAsync(
                        actingUserId,
                        "PAYROLL_RUN_STATUS_UPDATE",
                        "payroll_runs",
                        targetId,
                        "FAILED",
                        "Payroll run not found.");
                    throw new InvalidOperationException("Payroll run not found.");
                }

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_RUN_STATUS_UPDATE",
                    "payroll_runs",
                    targetId,
                    "SUCCESS",
                    $"Status='{normalizedStatus}'.");

                if (shouldWriteConsolidated)
                {
                    _ = Task.Run(() => TryWriteConsolidatedRunStatusTransactionAsync(
                        actingUserId,
                        payrollRunId));
                }
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_RUN_STATUS_UPDATE",
                    "payroll_runs",
                    targetId,
                    "FAILED",
                    ex.Message);
                throw;
            }
        }

        public async Task DeleteRunAsync(long payrollRunId, int? actingUserId)
        {
            if (payrollRunId <= 0)
            {
                throw new InvalidOperationException("Invalid payroll run.");
            }

            const string sql = @"
DELETE FROM payroll_runs
WHERE payroll_run_id = @payroll_run_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var targetId = payrollRunId.ToString(CultureInfo.InvariantCulture);
            await EnsureAdminOrHrAccessAsync(
                connection,
                actingUserId,
                "delete payroll runs",
                "PAYROLL_RUN_DELETE",
                "payroll_runs",
                targetId);

            try
            {
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await _auditLogWriter.TryWriteAsync(
                        actingUserId,
                        "PAYROLL_RUN_DELETE",
                        "payroll_runs",
                        targetId,
                        "FAILED",
                        "Payroll run not found.");
                    throw new InvalidOperationException("Payroll run not found.");
                }

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_RUN_DELETE",
                    "payroll_runs",
                    targetId,
                    "SUCCESS",
                    "Payroll run deleted.");
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_RUN_DELETE",
                    "payroll_runs",
                    targetId,
                    "FAILED",
                    ex.Message);
                throw;
            }
        }

        public async Task ReleasePayslipAsync(long payrollRunId, int? actingUserId, string? remarks)
        {
            if (payrollRunId <= 0)
            {
                throw new InvalidOperationException("Select payroll run before releasing payslip.");
            }

            const string insertSql = @"
INSERT INTO payslip_releases (payroll_run_id, released_by_employee_id, remarks)
VALUES (@payroll_run_id, @released_by_employee_id, @remarks);";

            const string updateStatusSql = @"
UPDATE payroll_runs
SET status = 'RELEASED'
WHERE payroll_run_id = @payroll_run_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var targetId = payrollRunId.ToString(CultureInfo.InvariantCulture);
            await EnsureAdminOrHrAccessAsync(
                connection,
                actingUserId,
                "release payslips",
                "PAYSLIP_RELEASE",
                "payroll_runs",
                targetId);

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var releasedByEmployeeId = actingUserId.HasValue && actingUserId.Value > 0
                    ? await ResolveEmployeeIdByUserIdAsync(connection, transaction, actingUserId.Value)
                    : null;
                var consolidatedDetails = await GetConsolidatedReleaseDetailsAsync(connection, transaction, payrollRunId);
                long payslipReleaseId = 0;

                await using (var insertCommand = new MySqlCommand(insertSql, connection, transaction))
                {
                    insertCommand.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
                    insertCommand.Parameters.AddWithValue("@released_by_employee_id", releasedByEmployeeId.HasValue && releasedByEmployeeId.Value > 0 ? releasedByEmployeeId.Value : DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
                    await insertCommand.ExecuteNonQueryAsync();
                    payslipReleaseId = insertCommand.LastInsertedId;
                }

                await using (var updateCommand = new MySqlCommand(updateStatusSql, connection, transaction))
                {
                    updateCommand.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
                    var updated = await updateCommand.ExecuteNonQueryAsync();
                    if (updated == 0)
                    {
                        await _auditLogWriter.TryWriteAsync(
                            actingUserId,
                            "PAYSLIP_RELEASE",
                            "payroll_runs",
                            targetId,
                            "FAILED",
                            "Payroll run not found.");
                        throw new InvalidOperationException("Payroll run not found.");
                    }
                }

                await transaction.CommitAsync();
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYSLIP_RELEASE",
                    "payroll_runs",
                    targetId,
                    "SUCCESS",
                    "Payslip released.");

                if (payslipReleaseId > 0 && consolidatedDetails != null)
                {
                    _ = Task.Run(() => TryWriteConsolidatedTransactionAsync(
                        actingUserId,
                        payrollRunId,
                        payslipReleaseId,
                        consolidatedDetails));
                }
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Ignore rollback failure.
                }

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYSLIP_RELEASE",
                    "payroll_runs",
                    targetId,
                    "FAILED",
                    ex.Message);
                throw;
            }
        }

        public async Task<long> ReportPayrollConcernAsync(long payrollRunId, int employeeId, int? actingUserId, string? concernDetails)
        {
            if (payrollRunId <= 0)
            {
                throw new InvalidOperationException("Select payroll run before reporting concern.");
            }

            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Employee profile is required before reporting concern.");
            }

            var details = (concernDetails ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(details))
            {
                throw new InvalidOperationException("Concern details are required.");
            }

            const string ownsRunSql = @"
SELECT COUNT(*)
FROM payroll_runs
WHERE payroll_run_id = @payroll_run_id
  AND employee_id = @employee_id;";

            const string insertSql = @"
INSERT INTO payroll_concerns (payroll_run_id, employee_id, reported_by_user_id, concern_details, status)
VALUES (@payroll_run_id, @employee_id, @reported_by_user_id, @concern_details, 'OPEN');
SELECT LAST_INSERT_ID();";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var auditTarget = payrollRunId.ToString(CultureInfo.InvariantCulture);

            try
            {
                await using (var ownsRunCommand = new MySqlCommand(ownsRunSql, connection))
                {
                    ownsRunCommand.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
                    ownsRunCommand.Parameters.AddWithValue("@employee_id", employeeId);
                    var ownsRunResult = await ownsRunCommand.ExecuteScalarAsync();
                    var ownsRun = ownsRunResult != null && ownsRunResult != DBNull.Value
                        ? Convert.ToInt32(ownsRunResult, CultureInfo.InvariantCulture)
                        : 0;

                    if (ownsRun <= 0)
                    {
                        await _auditLogWriter.TryWriteAsync(
                            actingUserId,
                            "PAYROLL_CONCERN_REPORT",
                            "payroll_runs",
                            auditTarget,
                            "DENIED",
                            "Selected run is not linked to the requesting employee.");
                        throw new InvalidOperationException("You can only report concern for your own payroll run.");
                    }
                }

                await using var insertCommand = new MySqlCommand(insertSql, connection);
                insertCommand.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
                insertCommand.Parameters.AddWithValue("@employee_id", employeeId);
                insertCommand.Parameters.AddWithValue("@reported_by_user_id", actingUserId.HasValue && actingUserId.Value > 0 ? actingUserId.Value : DBNull.Value);
                insertCommand.Parameters.AddWithValue("@concern_details", details);
                var inserted = await insertCommand.ExecuteScalarAsync();
                var concernId = inserted == null || inserted == DBNull.Value
                    ? 0L
                    : Convert.ToInt64(inserted, CultureInfo.InvariantCulture);

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_CONCERN_REPORT",
                    "payroll_concerns",
                    concernId > 0 ? concernId.ToString(CultureInfo.InvariantCulture) : null,
                    "SUCCESS",
                    $"Payroll run #{payrollRunId} concern submitted.");

                return concernId;
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                throw new InvalidOperationException("Payroll concern table is missing. Apply latest migrations and try again.", ex);
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "PAYROLL_CONCERN_REPORT",
                    "payroll_runs",
                    auditTarget,
                    "FAILED",
                    ex.Message);
                throw;
            }
        }

        private async Task TryWriteConsolidatedTransactionAsync(
            int? actingUserId,
            long payrollRunId,
            long payslipReleaseId,
            ConsolidatedReleaseDetails details)
        {
            try
            {
                var profileService = new CompanyProfileDataService(_connectionString);
                var profile = await profileService.GetCompanyProfileAsync();

                var officeCode = ResolveOfficeCode(profile.SerialNumber);
                var officeName = string.IsNullOrWhiteSpace(profile.CompanyName)
                    ? "Human Resources Management System"
                    : profile.CompanyName.Trim();

                var firstName = string.IsNullOrWhiteSpace(details.FirstName) ? "Unknown" : details.FirstName.Trim();
                var lastName = string.IsNullOrWhiteSpace(details.LastName) ? "Unknown" : details.LastName.Trim();
                var middleName = string.IsNullOrWhiteSpace(details.MiddleName) ? null : details.MiddleName.Trim();
                var fullName = BuildFullName(firstName, middleName, lastName);

                var payload = new ConsolidatedTransactionPayload(
                    BeneficiaryId: Truncate45(string.IsNullOrWhiteSpace(details.EmployeeNo) ? $"EMP-{details.EmployeeId}" : details.EmployeeNo),
                    CivilRegistryId: null,
                    ProjectCode: Truncate45($"HRMS-{payslipReleaseId:D6}"),
                    ProjectName: Truncate45(ResolveProjectName(details)),
                    OfficeId: Truncate45(officeCode),
                    FullName: Truncate45(fullName),
                    FirstName: Truncate45(firstName),
                    MiddleName: Truncate45OrNull(middleName),
                    LastName: Truncate45(lastName),
                    OfficeName: Truncate45(officeName),
                    TransactionType: "Payroll Release",
                    Amount: details.NetPay,
                    TransactionDate: details.PayDate.Date,
                    Status: "Released");

                await _consolidatedTransactionSyncService.InsertAsync(payload);
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "CONSOLIDATED_TRANSACTION_INSERT",
                    "payslip_releases",
                    payslipReleaseId.ToString(CultureInfo.InvariantCulture),
                    "SUCCESS",
                    $"GGMS consolidated transaction inserted for payroll run #{payrollRunId}.");
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "CONSOLIDATED_TRANSACTION_INSERT",
                    "payslip_releases",
                    payslipReleaseId.ToString(CultureInfo.InvariantCulture),
                    "FAILED",
                    $"Payslip was released but GGMS consolidated insert failed: {ex.Message}");
            }
        }

        private async Task TryWriteConsolidatedRunStatusTransactionAsync(
            int? actingUserId,
            long payrollRunId)
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var details = await GetConsolidatedReleaseDetailsNoTransactionAsync(connection, payrollRunId);
                if (details == null)
                {
                    return;
                }

                var profileService = new CompanyProfileDataService(_connectionString);
                var profile = await profileService.GetCompanyProfileAsync();
                var officeCode = ResolveOfficeCode(profile.SerialNumber);
                var officeName = string.IsNullOrWhiteSpace(profile.CompanyName)
                    ? "Human Resources Management System"
                    : profile.CompanyName.Trim();

                var firstName = string.IsNullOrWhiteSpace(details.FirstName) ? "Unknown" : details.FirstName.Trim();
                var lastName = string.IsNullOrWhiteSpace(details.LastName) ? "Unknown" : details.LastName.Trim();
                var middleName = string.IsNullOrWhiteSpace(details.MiddleName) ? null : details.MiddleName.Trim();
                var fullName = BuildFullName(firstName, middleName, lastName);

                var payload = new ConsolidatedTransactionPayload(
                    BeneficiaryId: Truncate45(string.IsNullOrWhiteSpace(details.EmployeeNo) ? $"EMP-{details.EmployeeId}" : details.EmployeeNo),
                    CivilRegistryId: null,
                    ProjectCode: Truncate45($"HRMS-{payrollRunId:D6}"),
                    ProjectName: Truncate45(ResolveProjectName(details)),
                    OfficeId: Truncate45(officeCode),
                    FullName: Truncate45(fullName),
                    FirstName: Truncate45(firstName),
                    MiddleName: Truncate45OrNull(middleName),
                    LastName: Truncate45(lastName),
                    OfficeName: Truncate45(officeName),
                    TransactionType: "Payroll Release",
                    Amount: details.NetPay,
                    TransactionDate: details.PayDate.Date,
                    Status: "Released");

                await _consolidatedTransactionSyncService.InsertAsync(payload);
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "CONSOLIDATED_TRANSACTION_INSERT",
                    "payroll_runs",
                    payrollRunId.ToString(CultureInfo.InvariantCulture),
                    "SUCCESS",
                    $"GGMS consolidated transaction inserted from run status release for payroll run #{payrollRunId}.");
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "CONSOLIDATED_TRANSACTION_INSERT",
                    "payroll_runs",
                    payrollRunId.ToString(CultureInfo.InvariantCulture),
                    "FAILED",
                    $"Payroll run status changed to RELEASED but GGMS consolidated insert failed: {ex.Message}");
            }
        }

        private static async Task<ConsolidatedReleaseDetails?> GetConsolidatedReleaseDetailsAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            long payrollRunId)
        {
            const string sql = @"
SELECT
    pr.employee_id,
    COALESCE(e.employee_no, '') AS employee_no,
    COALESCE(e.first_name, '') AS first_name,
    e.middle_name,
    COALESCE(e.last_name, '') AS last_name,
    COALESCE(pr.net_pay, 0) AS net_pay,
    COALESCE(pp.period_code, '') AS period_code,
    COALESCE(pp.pay_date, CURDATE()) AS pay_date
FROM payroll_runs pr
INNER JOIN employees e ON e.employee_id = pr.employee_id
LEFT JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
WHERE pr.payroll_run_id = @payroll_run_id
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new ConsolidatedReleaseDetails(
                EmployeeId: ToInt(reader["employee_id"]),
                EmployeeNo: reader["employee_no"]?.ToString() ?? string.Empty,
                FirstName: reader["first_name"]?.ToString() ?? string.Empty,
                MiddleName: reader["middle_name"] == DBNull.Value ? null : reader["middle_name"]?.ToString(),
                LastName: reader["last_name"]?.ToString() ?? string.Empty,
                NetPay: ToDecimal(reader["net_pay"]),
                PeriodCode: reader["period_code"]?.ToString() ?? string.Empty,
                PayDate: reader["pay_date"] == DBNull.Value
                    ? DateTime.Today
                    : Convert.ToDateTime(reader["pay_date"], CultureInfo.InvariantCulture));
        }

        private static async Task<ConsolidatedReleaseDetails?> GetConsolidatedReleaseDetailsNoTransactionAsync(
            MySqlConnection connection,
            long payrollRunId)
        {
            const string sql = @"
SELECT
    pr.employee_id,
    COALESCE(e.employee_no, '') AS employee_no,
    COALESCE(e.first_name, '') AS first_name,
    e.middle_name,
    COALESCE(e.last_name, '') AS last_name,
    COALESCE(pr.net_pay, 0) AS net_pay,
    COALESCE(pp.period_code, '') AS period_code,
    COALESCE(pp.pay_date, CURDATE()) AS pay_date
FROM payroll_runs pr
INNER JOIN employees e ON e.employee_id = pr.employee_id
LEFT JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
WHERE pr.payroll_run_id = @payroll_run_id
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new ConsolidatedReleaseDetails(
                EmployeeId: ToInt(reader["employee_id"]),
                EmployeeNo: reader["employee_no"]?.ToString() ?? string.Empty,
                FirstName: reader["first_name"]?.ToString() ?? string.Empty,
                MiddleName: reader["middle_name"] == DBNull.Value ? null : reader["middle_name"]?.ToString(),
                LastName: reader["last_name"]?.ToString() ?? string.Empty,
                NetPay: ToDecimal(reader["net_pay"]),
                PeriodCode: reader["period_code"]?.ToString() ?? string.Empty,
                PayDate: reader["pay_date"] == DBNull.Value
                    ? DateTime.Today
                    : Convert.ToDateTime(reader["pay_date"], CultureInfo.InvariantCulture));
        }

        private static async Task<string?> GetPayrollRunStatusAsync(
            MySqlConnection connection,
            long payrollRunId)
        {
            const string sql = @"
SELECT status
FROM payroll_runs
WHERE payroll_run_id = @payroll_run_id
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? null : value.ToString();
        }

        private static string ResolveOfficeCode(string? serialNumber)
        {
            const string fallback = "OFF-2026-0007";
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                return fallback;
            }

            var match = Regex.Match(serialNumber, @"OFF-\d{4}-\d{4,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Value.ToUpperInvariant() : fallback;
        }

        private static string BuildFullName(string firstName, string? middleName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(middleName))
            {
                return $"{firstName} {lastName}".Trim();
            }

            return $"{firstName} {middleName.Trim()} {lastName}".Trim();
        }

        private static string ResolveProjectName(ConsolidatedReleaseDetails details)
        {
            var periodCode = string.IsNullOrWhiteSpace(details.PeriodCode)
                ? null
                : details.PeriodCode.Trim();

            if (string.IsNullOrWhiteSpace(periodCode))
            {
                return $"Payroll {details.PayDate:MMMM yyyy}";
            }

            return $"Payroll {periodCode}";
        }

        private static string Truncate45(string? value)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return normalized.Length <= 45 ? normalized : normalized.Substring(0, 45);
        }

        private static string? Truncate45OrNull(string? value)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (normalized == null)
            {
                return null;
            }

            return normalized.Length <= 45 ? normalized : normalized.Substring(0, 45);
        }

        public async Task<IReadOnlyList<PayrollReleaseDto>> GetReleaseLogsAsync(string? search = null, int limit = 500, int? employeeId = null)
        {
            const string sql = @"
SELECT
    prl.payslip_release_id,
    prl.payroll_run_id,
    pp.period_code,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    prl.released_at,
    COALESCE(pr.status, 'RELEASED') AS run_status,
    COALESCE(CONCAT(rb.last_name, ', ', rb.first_name, IFNULL(CONCAT(' ', rb.middle_name), '')), '-') AS released_by,
    COALESCE(prl.remarks, '') AS remarks
FROM payslip_releases prl
INNER JOIN payroll_runs pr ON pr.payroll_run_id = prl.payroll_run_id
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
INNER JOIN employees e ON e.employee_id = pr.employee_id
LEFT JOIN employees rb ON rb.employee_id = prl.released_by_employee_id
WHERE (
    @search = '' OR
    pp.period_code LIKE CONCAT('%', @search, '%') OR
    e.employee_no LIKE CONCAT('%', @search, '%') OR
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) LIKE CONCAT('%', @search, '%') OR
    COALESCE(prl.remarks, '') LIKE CONCAT('%', @search, '%')
)
  AND (@employee_id IS NULL OR pr.employee_id = @employee_id)
ORDER BY prl.released_at DESC, prl.payslip_release_id DESC
LIMIT @limit;";

            var rows = new List<PayrollReleaseDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue ? employeeId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new PayrollReleaseDto(
                        PayslipReleaseId: ToLong(reader["payslip_release_id"]),
                        PayrollRunId: ToLong(reader["payroll_run_id"]),
                        PeriodCode: reader["period_code"]?.ToString() ?? "-",
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        ReleasedAt: reader["released_at"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["released_at"], CultureInfo.InvariantCulture),
                        RunStatus: reader["run_status"]?.ToString() ?? "RELEASED",
                        ReleasedBy: reader["released_by"]?.ToString() ?? "-",
                        Remarks: reader["remarks"]?.ToString() ?? string.Empty));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<PayrollReleaseDto>();
            }

            return rows;
        }

        public async Task<int?> GetEmployeeIdByUserIdAsync(int userId)
        {
            if (userId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT employee_id, username, email, full_name
FROM user_accounts
WHERE user_id = @user_id
LIMIT 1;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@user_id", userId);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var linkedEmployeeId = reader["employee_id"] == DBNull.Value
                ? (int?)null
                : Convert.ToInt32(reader["employee_id"], CultureInfo.InvariantCulture);
            if (linkedEmployeeId.HasValue && linkedEmployeeId.Value > 0)
            {
                return linkedEmployeeId.Value;
            }

            var username = reader["username"]?.ToString();
            var email = reader["email"]?.ToString();
            var fullName = reader["full_name"]?.ToString();
            await reader.CloseAsync();

            var resolvedEmployeeId = await TryResolveEmployeeIdAsync(connection, username, email, fullName);
            if (!resolvedEmployeeId.HasValue || resolvedEmployeeId.Value <= 0)
            {
                return null;
            }

            const string linkSql = @"
UPDATE user_accounts
SET employee_id = @employee_id
WHERE user_id = @user_id
  AND (employee_id IS NULL OR employee_id = 0);";

            await using var linkCommand = new MySqlCommand(linkSql, connection);
            linkCommand.Parameters.AddWithValue("@employee_id", resolvedEmployeeId.Value);
            linkCommand.Parameters.AddWithValue("@user_id", userId);
            await linkCommand.ExecuteNonQueryAsync();

            return resolvedEmployeeId.Value;
        }

        private static async Task<int?> ResolveEmployeeIdByUserIdAsync(MySqlConnection connection, MySqlTransaction transaction, int userId)
        {
            const string sql = @"
SELECT employee_id
FROM user_accounts
WHERE user_id = @user_id
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@user_id", userId);
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value
                ? null
                : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private static async Task<int?> TryResolveEmployeeIdAsync(MySqlConnection connection, string? username, string? email, string? fullName)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                const string emailSql = @"
SELECT employee_id
FROM employees
WHERE email = @email
LIMIT 1;";

                await using var emailCommand = new MySqlCommand(emailSql, connection);
                emailCommand.Parameters.AddWithValue("@email", email.Trim());
                var emailValue = await emailCommand.ExecuteScalarAsync();
                if (emailValue != null && emailValue != DBNull.Value)
                {
                    return Convert.ToInt32(emailValue, CultureInfo.InvariantCulture);
                }
            }

            var candidates = BuildEmployeeNoCandidates(username);
            if (candidates.Count > 0)
            {
                const string employeeNoSql = @"
SELECT employee_id
FROM employees
WHERE employee_no = @employee_no
LIMIT 1;";

                foreach (var employeeNo in candidates)
                {
                    await using var employeeNoCommand = new MySqlCommand(employeeNoSql, connection);
                    employeeNoCommand.Parameters.AddWithValue("@employee_no", employeeNo);
                    var value = await employeeNoCommand.ExecuteScalarAsync();
                    if (value != null && value != DBNull.Value)
                    {
                        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            const string nameSql = @"
SELECT employee_id, first_name, middle_name, last_name
FROM employees;";

            var normalizedTarget = NormalizePersonName(fullName);
            if (string.IsNullOrWhiteSpace(normalizedTarget))
            {
                return null;
            }

            await using var nameCommand = new MySqlCommand(nameSql, connection);
            await using var nameReader = await nameCommand.ExecuteReaderAsync();
            while (await nameReader.ReadAsync())
            {
                var employeeId = ToInt(nameReader["employee_id"]);
                if (employeeId <= 0)
                {
                    continue;
                }

                var first = nameReader["first_name"]?.ToString() ?? string.Empty;
                var middle = nameReader["middle_name"]?.ToString() ?? string.Empty;
                var last = nameReader["last_name"]?.ToString() ?? string.Empty;

                if (NameMatches(normalizedTarget, first, middle, last))
                {
                    return employeeId;
                }
            }

            return null;
        }

        private static List<string> BuildEmployeeNoCandidates(string? username)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddCandidate(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                var trimmed = value.Trim().ToUpperInvariant();
                if (seen.Add(trimmed))
                {
                    results.Add(trimmed);
                }
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return results;
            }

            var normalized = username.Trim().ToUpperInvariant();
            AddCandidate(normalized);

            if (normalized.StartsWith("E-", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = normalized.Substring(2);
                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    AddCandidate($"E-{suffix}");
                }
            }

            var digits = ExtractDigits(normalized);
            if (!string.IsNullOrWhiteSpace(digits))
            {
                AddCandidate($"E-{digits}");
            }

            return results;
        }

        private static string ExtractDigits(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var buffer = new char[text.Length];
            var index = 0;
            foreach (var ch in text)
            {
                if (!char.IsDigit(ch))
                {
                    continue;
                }

                buffer[index++] = ch;
            }

            return index == 0
                ? string.Empty
                : new string(buffer, 0, index);
        }

        private static bool NameMatches(string normalizedTarget, string first, string middle, string last)
        {
            var firstMiddleLast = NormalizePersonName($"{first} {middle} {last}");
            if (!string.IsNullOrWhiteSpace(firstMiddleLast) &&
                string.Equals(normalizedTarget, firstMiddleLast, StringComparison.Ordinal))
            {
                return true;
            }

            var firstLast = NormalizePersonName($"{first} {last}");
            if (!string.IsNullOrWhiteSpace(firstLast) &&
                string.Equals(normalizedTarget, firstLast, StringComparison.Ordinal))
            {
                return true;
            }

            var lastFirstMiddle = NormalizePersonName($"{last} {first} {middle}");
            if (!string.IsNullOrWhiteSpace(lastFirstMiddle) &&
                string.Equals(normalizedTarget, lastFirstMiddle, StringComparison.Ordinal))
            {
                return true;
            }

            var lastFirst = NormalizePersonName($"{last} {first}");
            if (!string.IsNullOrWhiteSpace(lastFirst) &&
                string.Equals(normalizedTarget, lastFirst, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static string NormalizePersonName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var buffer = new char[value.Length];
            var index = 0;
            var lastWasSpace = false;

            foreach (var ch in value.Trim())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    buffer[index++] = char.ToUpperInvariant(ch);
                    lastWasSpace = false;
                    continue;
                }

                if (!lastWasSpace)
                {
                    buffer[index++] = ' ';
                    lastWasSpace = true;
                }
            }

            return new string(buffer, 0, index).Trim();
        }

        private static async Task<PayrollEmployeeCompensationDto> GetEmployeeCompensationAsync(MySqlConnection connection, MySqlTransaction? transaction, int employeeId)
        {
            const string sql = @"
SELECT
    COALESCE(ss.monthly_rate, 0) AS monthly_rate,
    CASE
        WHEN COALESCE(d.dept_name, '') = 'General Services Office' THEN 1000.00
        ELSE 1500.00
    END AS default_allowances,
    COALESCE(at.type_name, 'Permanent') AS employment_type,
    COALESCE(p.position_name, '-') AS position_name
FROM employees e
LEFT JOIN departments d ON d.department_id = e.department_id
LEFT JOIN appointment_types at ON at.appointment_type_id = e.appointment_type_id
LEFT JOIN positions p ON p.position_id = e.position_id
LEFT JOIN (
    SELECT src.salary_grade, src.step_no, src.monthly_rate
    FROM salary_steps src
    INNER JOIN (
        SELECT salary_grade, step_no, MAX(effectivity_date) AS effectivity_date
        FROM salary_steps
        WHERE effectivity_date <= CURDATE()
        GROUP BY salary_grade, step_no
    ) eff
      ON eff.salary_grade = src.salary_grade
     AND eff.step_no = src.step_no
     AND eff.effectivity_date = src.effectivity_date
) ss
  ON ss.salary_grade = e.salary_grade
 AND ss.step_no = e.step_no
WHERE e.employee_id = @employee_id
LIMIT 1;";

            await using var command = transaction == null
                ? new MySqlCommand(sql, connection)
                : new MySqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Employee payroll profile was not found.");
            }

            return new PayrollEmployeeCompensationDto(
                MonthlyRate: ToDecimal(reader["monthly_rate"]),
                DefaultAllowances: ToDecimal(reader["default_allowances"]),
                EmploymentTypeName: reader["employment_type"]?.ToString() ?? "Permanent",
                PositionName: reader["position_name"]?.ToString() ?? "-");
        }

        private static async Task ReplaceRunItemsAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            long payrollRunId,
            decimal basicPay,
            decimal allowances,
            decimal overtimePay,
            decimal otherEarnings,
            string employmentTypeName,
            PayrollDeductionResult deductions)
        {
            const string deleteSql = @"
DELETE FROM payroll_run_items
WHERE payroll_run_id = @payroll_run_id;";

            await using (var deleteCommand = new MySqlCommand(deleteSql, connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            if (basicPay > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "EARNING", "BASIC", "Basic Pay", basicPay);
            }

            if (allowances > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "EARNING", "ALLOW", "Allowances", allowances);
            }

            if (overtimePay > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "EARNING", "OVERTIME", "Overtime Pay", overtimePay);
            }

            if (otherEarnings > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "EARNING", "OTHER", "Other Earnings", otherEarnings);
            }

            var retirementAmount = deductions.GsisContribution + deductions.SssContribution;
            if (retirementAmount > 0m)
            {
                var normalizedType = employmentTypeName?.Trim().ToLowerInvariant() ?? string.Empty;
                var isSss = normalizedType.Contains("job order", StringComparison.Ordinal) ||
                            normalizedType.Contains("contractual", StringComparison.Ordinal) ||
                            normalizedType.Contains("cos", StringComparison.Ordinal);

                await InsertRunItemAsync(
                    connection,
                    transaction,
                    payrollRunId,
                    "DEDUCTION",
                    isSss ? "SSS" : "GSIS",
                    isSss ? "SSS Contribution" : "GSIS Contribution (9%)",
                    retirementAmount);
            }

            if (deductions.PhilHealthContribution > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "DEDUCTION", "PHILHEALTH", "PhilHealth Contribution (2.5%)", deductions.PhilHealthContribution);
            }

            if (deductions.PagIBIGContribution > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "DEDUCTION", "PAGIBIG", "Pag-IBIG Contribution (2%)", deductions.PagIBIGContribution);
            }

            if (deductions.TaxWithheld > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "DEDUCTION", "TAX", "Withholding Tax (BIR/TRAIN)", deductions.TaxWithheld);
            }

            if (deductions.AbsenceDeduction > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "DEDUCTION", "ABSENCE", "Absence Deduction", deductions.AbsenceDeduction);
            }

            if (deductions.LateDeduction > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "DEDUCTION", "LATE", "Late Deduction", deductions.LateDeduction);
            }

            if (deductions.LoanDeduction > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "DEDUCTION", "LOAN", "Loan Deduction", deductions.LoanDeduction);
            }

            if (deductions.OtherDeductions > 0m)
            {
                await InsertRunItemAsync(connection, transaction, payrollRunId, "DEDUCTION", "OTHER_DEDUCTION", "Other Deductions", deductions.OtherDeductions);
            }
        }

        private static async Task InsertRunItemAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            long payrollRunId,
            string itemType,
            string code,
            string description,
            decimal amount)
        {
            const string sql = @"
INSERT INTO payroll_run_items (payroll_run_id, item_type, code, description, amount)
VALUES (@payroll_run_id, @item_type, @code, @description, @amount);";

            await using var command = new MySqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@payroll_run_id", payrollRunId);
            command.Parameters.AddWithValue("@item_type", itemType);
            command.Parameters.AddWithValue("@code", code);
            command.Parameters.AddWithValue("@description", description);
            command.Parameters.AddWithValue("@amount", amount);
            await command.ExecuteNonQueryAsync();
        }

        private async Task EnsureAdminOrHrAccessAsync(
            MySqlConnection connection,
            int? actingUserId,
            string actionLabel,
            string actionCode,
            string targetType,
            string? targetId)
        {
            var actorId = actingUserId.HasValue && actingUserId.Value > 0
                ? actingUserId.Value
                : 0;
            var roleName = await AuthorizationGuard.GetRoleNameAsync(connection, actorId);
            if (AuthorizationGuard.IsAdminOrHr(roleName))
            {
                return;
            }

            await _auditLogWriter.TryWriteAsync(
                actorId > 0 ? actorId : null,
                actionCode,
                targetType,
                targetId,
                "DENIED",
                roleName is null
                    ? "Actor role could not be resolved."
                    : $"Role '{roleName}' is not allowed.");

            AuthorizationGuard.DemandAdminOrHr(roleName, actionLabel);
        }

        private static object NormalizeStatusFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DBNull.Value;
            }

            var normalized = value.Trim().ToUpperInvariant();
            return normalized == "ALL" ? DBNull.Value : normalized;
        }

        private static string NormalizePeriodStatus(string? status)
        {
            return status?.Trim().ToUpperInvariant() switch
            {
                "OPEN" => "OPEN",
                "LOCKED" => "LOCKED",
                "POSTED" => "POSTED",
                "CANCELLED" => "CANCELLED",
                _ => "OPEN"
            };
        }

        private static string NormalizeRunStatus(string? status)
        {
            return status?.Trim().ToUpperInvariant() switch
            {
                "DRAFT" => "DRAFT",
                "GENERATED" => "GENERATED",
                "APPROVED" => "APPROVED",
                "RELEASED" => "RELEASED",
                "VOID" => "VOID",
                _ => "GENERATED"
            };
        }

        private static int ToInt(object value) =>
            value == DBNull.Value || value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);

        private static long ToLong(object value) =>
            value == DBNull.Value || value == null ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);

        private static decimal ToDecimal(object value) =>
            value == DBNull.Value || value == null ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

        private static bool IsMissingObjectError(MySqlException ex) =>
            ex.Number == 1109 || ex.Number == 1146 || ex.Number == 1356;

        private sealed record ConsolidatedReleaseDetails(
            int EmployeeId,
            string EmployeeNo,
            string FirstName,
            string? MiddleName,
            string LastName,
            decimal NetPay,
            string PeriodCode,
            DateTime PayDate);

        /// <summary>
        /// One-click GENERATE: creates payroll runs for ALL active employees in the given period.
        /// Uses each employee's stored monthly rate and default allowances.
        /// </summary>
        public async Task<int> GenerateAllRunsAsync(long payrollPeriodId)
        {
            if (payrollPeriodId <= 0)
            {
                throw new InvalidOperationException("Payroll period is required.");
            }

            var employees = await GetEmployeesAsync(null);
            var generated = 0;

            foreach (var emp in employees)
            {
                try
                {
                    await UpsertRunAsync(
                        payrollPeriodId,
                        emp.EmployeeId,
                        basicPay: 0m,
                        allowances: 0m,
                        overtimePay: 0m,
                        otherEarnings: 0m,
                        deductionsTotal: 0m,
                        status: "GENERATED");
                    generated++;
                }
                catch
                {
                    // Skip employees that fail (e.g. missing compensation data)
                }
            }

            return generated;
        }

        /// <summary>
        /// Loads SSS contribution brackets from the database.
        /// Returns empty list if table doesn't exist yet.
        /// </summary>
        public async Task<IReadOnlyList<SssBracketDto>> GetSssBracketsAsync()
        {
            const string sql = @"
SELECT salary_credit, min_range, max_range, ee_share, er_share, ec_share, total_contribution
FROM sss_contribution_brackets
WHERE is_active = 1
ORDER BY min_range;";

            var list = new List<SssBracketDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new SssBracketDto(
                        ToDecimal(reader["salary_credit"]),
                        ToDecimal(reader["min_range"]),
                        ToDecimal(reader["max_range"]),
                        ToDecimal(reader["ee_share"]),
                        ToDecimal(reader["er_share"]),
                        ToDecimal(reader["ec_share"]),
                        ToDecimal(reader["total_contribution"])));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
            }

            return list;
        }

        /// <summary>
        /// Loads GSIS contribution rates from the database.
        /// </summary>
        public async Task<GsisBracketDto?> GetGsisBracketAsync()
        {
            const string sql = @"
SELECT ee_rate, er_rate, description
FROM gsis_contribution_brackets
WHERE is_active = 1
ORDER BY effective_year DESC
LIMIT 1;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new GsisBracketDto(
                        ToDecimal(reader["ee_rate"]),
                        ToDecimal(reader["er_rate"]),
                        reader["description"]?.ToString() ?? string.Empty);
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
            }

            return null;
        }

        /// <summary>
        /// Loads PhilHealth contribution parameters from the database.
        /// </summary>
        public async Task<PhilHealthBracketDto?> GetPhilHealthBracketAsync()
        {
            const string sql = @"
SELECT premium_rate, min_monthly_premium, max_monthly_premium, ee_share_pct, er_share_pct, salary_floor, salary_ceiling
FROM philhealth_contribution_brackets
WHERE is_active = 1
ORDER BY effective_year DESC
LIMIT 1;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new PhilHealthBracketDto(
                        ToDecimal(reader["premium_rate"]),
                        ToDecimal(reader["min_monthly_premium"]),
                        ToDecimal(reader["max_monthly_premium"]),
                        ToDecimal(reader["ee_share_pct"]),
                        ToDecimal(reader["er_share_pct"]),
                        ToDecimal(reader["salary_floor"]),
                        ToDecimal(reader["salary_ceiling"]));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
            }

            return null;
        }

        /// <summary>
        /// Loads Pag-IBIG contribution brackets from the database.
        /// </summary>
        public async Task<IReadOnlyList<PagIbigBracketDto>> GetPagIbigBracketsAsync()
        {
            const string sql = @"
SELECT min_salary, max_salary, ee_rate, er_rate, max_ee_contribution, max_er_contribution
FROM pagibig_contribution_brackets
WHERE is_active = 1
ORDER BY min_salary;";

            var list = new List<PagIbigBracketDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new PagIbigBracketDto(
                        ToDecimal(reader["min_salary"]),
                        ToDecimal(reader["max_salary"]),
                        ToDecimal(reader["ee_rate"]),
                        ToDecimal(reader["er_rate"]),
                        ToDecimal(reader["max_ee_contribution"]),
                        ToDecimal(reader["max_er_contribution"])));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
            }

            return list;
        }
    }

    public record SssBracketDto(
        decimal SalaryCredit,
        decimal MinRange,
        decimal MaxRange,
        decimal EeShare,
        decimal ErShare,
        decimal EcShare,
        decimal TotalContribution);

    public record GsisBracketDto(
        decimal EeRate,
        decimal ErRate,
        string Description);

    public record PhilHealthBracketDto(
        decimal PremiumRate,
        decimal MinMonthlyPremium,
        decimal MaxMonthlyPremium,
        decimal EeSharePct,
        decimal ErSharePct,
        decimal SalaryFloor,
        decimal SalaryCeiling);

    public record PagIbigBracketDto(
        decimal MinSalary,
        decimal MaxSalary,
        decimal EeRate,
        decimal ErRate,
        decimal MaxEeContribution,
        decimal MaxErContribution);
}
