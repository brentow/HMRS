using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record LeaveStatsDto(
        int TotalRequests,
        int PendingRequests,
        int ApprovedRequests,
        int RejectedRequests,
        int LeaveTypes,
        int EmployeesWithBalances,
        int AttachmentsCount);

    public record LeaveTypeDto(
        int LeaveTypeId,
        string Code,
        string Name,
        bool IsPaid,
        decimal DefaultCreditsPerYear,
        bool IsActive);

    public record LeaveEmployeeOptionDto(
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName);

    public record LeaveRequestDto(
        long LeaveApplicationId,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        int LeaveTypeId,
        string LeaveTypeCode,
        string LeaveTypeName,
        DateTime DateFrom,
        DateTime DateTo,
        decimal DaysRequested,
        string Reason,
        string Status,
        DateTime FiledAt,
        DateTime? DecisionAt,
        string DecisionRemarks,
        int AttachmentCount);

    public record LeaveBalanceDto(
        long LeaveBalanceId,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        int LeaveTypeId,
        string LeaveTypeName,
        int Year,
        decimal OpeningCredits,
        decimal Earned,
        decimal Used,
        decimal Adjustments,
        DateTime AsOfDate);

    public record LeaveAttachmentDto(
        long LeaveDocumentId,
        long LeaveApplicationId,
        string EmployeeNo,
        string EmployeeName,
        string FileName,
        string FilePath,
        DateTime UploadedAt);

    public class LeaveDataService
    {
        private readonly string _connectionString;
        private readonly AuditLogWriter _auditLogWriter;
        private bool? _hasDecisionRemarksColumn;

        public LeaveDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _auditLogWriter = new AuditLogWriter(_connectionString);
        }

        public async Task<LeaveStatsDto> GetStatsAsync(int? employeeId = null)
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*) FROM leave_applications la
      WHERE (@employee_id IS NULL OR la.employee_id = @employee_id)) AS total_requests,
    (SELECT COUNT(*) FROM leave_applications la
      WHERE la.status IN ('SUBMITTED','RECOMMENDED')
        AND (@employee_id IS NULL OR la.employee_id = @employee_id)) AS pending_requests,
    (SELECT COUNT(*) FROM leave_applications la
      WHERE la.status = 'APPROVED'
        AND (@employee_id IS NULL OR la.employee_id = @employee_id)) AS approved_requests,
    (SELECT COUNT(*) FROM leave_applications la
      WHERE la.status = 'REJECTED'
        AND (@employee_id IS NULL OR la.employee_id = @employee_id)) AS rejected_requests,
    (SELECT COUNT(*) FROM leave_types WHERE is_active = 1) AS leave_types,
    (SELECT COUNT(DISTINCT lb.employee_id) FROM leave_balances lb
      WHERE (@employee_id IS NULL OR lb.employee_id = @employee_id)) AS employees_with_balances,
    (SELECT COUNT(*)
       FROM leave_documents ld
       JOIN leave_applications la ON la.leave_application_id = ld.leave_application_id
      WHERE (@employee_id IS NULL OR la.employee_id = @employee_id)) AS attachments_count;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue ? employeeId.Value : DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new LeaveStatsDto(
                        TotalRequests: ToInt(reader["total_requests"]),
                        PendingRequests: ToInt(reader["pending_requests"]),
                        ApprovedRequests: ToInt(reader["approved_requests"]),
                        RejectedRequests: ToInt(reader["rejected_requests"]),
                        LeaveTypes: ToInt(reader["leave_types"]),
                        EmployeesWithBalances: ToInt(reader["employees_with_balances"]),
                        AttachmentsCount: ToInt(reader["attachments_count"]));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
            }

            return new LeaveStatsDto(0, 0, 0, 0, 0, 0, 0);
        }

        public async Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly = true)
        {
            const string sql = @"
SELECT leave_type_id, code, name, is_paid, default_credits_per_year, is_active
FROM leave_types
WHERE (@active_only = 0 OR is_active = 1)
ORDER BY name;";

            var rows = new List<LeaveTypeDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@active_only", activeOnly ? 1 : 0);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new LeaveTypeDto(
                        LeaveTypeId: ToInt(reader["leave_type_id"]),
                        Code: reader["code"]?.ToString() ?? string.Empty,
                        Name: reader["name"]?.ToString() ?? string.Empty,
                        IsPaid: ToInt(reader["is_paid"]) == 1,
                        DefaultCreditsPerYear: ToDecimal(reader["default_credits_per_year"]),
                        IsActive: ToInt(reader["is_active"]) == 1));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<LeaveTypeDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<LeaveEmployeeOptionDto>> GetEmployeesAsync(int? employeeId = null)
        {
            const string sql = @"
SELECT
    employee_id,
    employee_no,
    CONCAT(last_name, ', ', first_name, IFNULL(CONCAT(' ', middle_name), '')) AS employee_name
FROM employees
WHERE status = 'ACTIVE'
  AND (@employee_id IS NULL OR employee_id = @employee_id)
ORDER BY employee_no;";

            var rows = new List<LeaveEmployeeOptionDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue ? employeeId.Value : DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new LeaveEmployeeOptionDto(
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<LeaveEmployeeOptionDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<LeaveRequestDto>> GetLeaveRequestsAsync(string? statusFilter = null, string? search = null, int limit = 300, int? employeeId = null)
        {
            const string sqlWithDecisionRemarks = @"
SELECT
    la.leave_application_id,
    la.employee_id,
    COALESCE(e.employee_no,'-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    la.leave_type_id,
    COALESCE(lt.code,'-') AS leave_type_code,
    COALESCE(lt.name,'-') AS leave_type_name,
    la.date_from,
    la.date_to,
    COALESCE(la.days_requested,0) AS days_requested,
    COALESCE(la.reason,'') AS reason,
    COALESCE(la.status,'SUBMITTED') AS status,
    la.filed_at,
    la.decision_at,
    COALESCE(la.decision_remarks,'') AS decision_remarks,
    COALESCE(doc.docs_count, 0) AS docs_count
FROM leave_applications la
JOIN employees e ON e.employee_id = la.employee_id
JOIN leave_types lt ON lt.leave_type_id = la.leave_type_id
LEFT JOIN (
    SELECT leave_application_id, COUNT(*) AS docs_count
    FROM leave_documents
    GROUP BY leave_application_id
) doc ON doc.leave_application_id = la.leave_application_id
WHERE (@status IS NULL OR la.status = @status)
  AND (@employee_id IS NULL OR la.employee_id = @employee_id)
  AND (
      @search = '' OR
      e.employee_no LIKE CONCAT('%', @search, '%') OR
      CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) LIKE CONCAT('%', @search, '%') OR
      lt.name LIKE CONCAT('%', @search, '%') OR
      la.reason LIKE CONCAT('%', @search, '%')
  )
ORDER BY la.filed_at DESC
LIMIT @limit;";

            const string sqlWithoutDecisionRemarks = @"
SELECT
    la.leave_application_id,
    la.employee_id,
    COALESCE(e.employee_no,'-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    la.leave_type_id,
    COALESCE(lt.code,'-') AS leave_type_code,
    COALESCE(lt.name,'-') AS leave_type_name,
    la.date_from,
    la.date_to,
    COALESCE(la.days_requested,0) AS days_requested,
    COALESCE(la.reason,'') AS reason,
    COALESCE(la.status,'SUBMITTED') AS status,
    la.filed_at,
    la.decision_at,
    '' AS decision_remarks,
    COALESCE(doc.docs_count, 0) AS docs_count
FROM leave_applications la
JOIN employees e ON e.employee_id = la.employee_id
JOIN leave_types lt ON lt.leave_type_id = la.leave_type_id
LEFT JOIN (
    SELECT leave_application_id, COUNT(*) AS docs_count
    FROM leave_documents
    GROUP BY leave_application_id
) doc ON doc.leave_application_id = la.leave_application_id
WHERE (@status IS NULL OR la.status = @status)
  AND (@employee_id IS NULL OR la.employee_id = @employee_id)
  AND (
      @search = '' OR
      e.employee_no LIKE CONCAT('%', @search, '%') OR
      CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) LIKE CONCAT('%', @search, '%') OR
      lt.name LIKE CONCAT('%', @search, '%') OR
      la.reason LIKE CONCAT('%', @search, '%')
  )
ORDER BY la.filed_at DESC
LIMIT @limit;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var hasDecisionRemarks = await HasDecisionRemarksColumnAsync(connection);
                var sql = hasDecisionRemarks ? sqlWithDecisionRemarks : sqlWithoutDecisionRemarks;
                try
                {
                    return await ReadRequestsAsync(connection, sql, statusFilter, search, limit, employeeId);
                }
                catch (MySqlException ex) when (IsUnknownColumnError(ex))
                {
                    _hasDecisionRemarksColumn = false;
                    return await ReadRequestsAsync(connection, sqlWithoutDecisionRemarks, statusFilter, search, limit, employeeId);
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<LeaveRequestDto>();
            }
        }

        public async Task<long> AddLeaveRequestAsync(
            int employeeId,
            int leaveTypeId,
            DateTime dateFrom,
            DateTime dateTo,
            decimal? daysRequested,
            string? reason)
        {
            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Employee is required.");
            }

            if (leaveTypeId <= 0)
            {
                throw new InvalidOperationException("Leave type is required.");
            }

            var from = dateFrom.Date;
            var to = dateTo.Date;
            if (to < from)
            {
                (from, to) = (to, from);
            }

            var computedDays = Math.Max(1m, Convert.ToDecimal((to - from).TotalDays + 1, CultureInfo.InvariantCulture));
            var finalDays = daysRequested.HasValue && daysRequested.Value > 0 ? daysRequested.Value : computedDays;

            const string insertRequestSql = @"
INSERT INTO leave_applications
    (employee_id, leave_type_id, date_from, date_to, days_requested, reason, status, filed_at)
VALUES
    (@employee_id, @leave_type_id, @date_from, @date_to, @days_requested, @reason, 'SUBMITTED', NOW());
SELECT LAST_INSERT_ID();";

            const string insertDaySql = @"
INSERT IGNORE INTO leave_application_days
    (leave_application_id, leave_date, day_fraction, half_day_part)
VALUES
    (@leave_application_id, @leave_date, 1.00, NULL);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                long leaveApplicationId;
                await using (var command = new MySqlCommand(insertRequestSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@employee_id", employeeId);
                    command.Parameters.AddWithValue("@leave_type_id", leaveTypeId);
                    command.Parameters.AddWithValue("@date_from", from);
                    command.Parameters.AddWithValue("@date_to", to);
                    command.Parameters.AddWithValue("@days_requested", finalDays);
                    command.Parameters.AddWithValue("@reason", string.IsNullOrWhiteSpace(reason) ? DBNull.Value : reason.Trim());

                    var result = await command.ExecuteScalarAsync();
                    leaveApplicationId = result == null || result == DBNull.Value
                        ? 0
                        : Convert.ToInt64(result, CultureInfo.InvariantCulture);
                }

                if (leaveApplicationId <= 0)
                {
                    throw new InvalidOperationException("Unable to create leave request.");
                }

                await using (var dayCommand = new MySqlCommand(insertDaySql, connection, transaction))
                {
                    dayCommand.Parameters.Add("@leave_application_id", MySqlDbType.Int64);
                    dayCommand.Parameters.Add("@leave_date", MySqlDbType.Date);

                    for (var date = from; date <= to; date = date.AddDays(1))
                    {
                        dayCommand.Parameters["@leave_application_id"].Value = leaveApplicationId;
                        dayCommand.Parameters["@leave_date"].Value = date;
                        await dayCommand.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
                return leaveApplicationId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateLeaveRequestStatusAsync(long leaveApplicationId, string status, string? decisionRemarks, int? actingUserId)
        {
            if (leaveApplicationId <= 0)
            {
                throw new InvalidOperationException("Invalid leave request.");
            }

            var normalizedStatus = NormalizeRequestStatus(status);
            var targetId = leaveApplicationId.ToString(CultureInfo.InvariantCulture);

            const string sqlWithRemarks = @"
UPDATE leave_applications
SET
    status = @status,
    decision_at = CASE WHEN @set_decision = 1 THEN NOW() ELSE decision_at END,
    decision_remarks = @decision_remarks,
    recommended_by_employee_id = CASE WHEN @set_recommended = 1 THEN @actor_employee_id ELSE recommended_by_employee_id END,
    approved_by_employee_id = CASE WHEN @set_approved = 1 THEN @actor_employee_id ELSE approved_by_employee_id END
WHERE leave_application_id = @leave_application_id;";

            const string sqlWithoutRemarks = @"
UPDATE leave_applications
SET
    status = @status,
    decision_at = CASE WHEN @set_decision = 1 THEN NOW() ELSE decision_at END,
    recommended_by_employee_id = CASE WHEN @set_recommended = 1 THEN @actor_employee_id ELSE recommended_by_employee_id END,
    approved_by_employee_id = CASE WHEN @set_approved = 1 THEN @actor_employee_id ELSE approved_by_employee_id END
WHERE leave_application_id = @leave_application_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureAdminOrHrAccessAsync(
                connection,
                actingUserId,
                "update leave request status",
                "LEAVE_STATUS_UPDATE",
                "leave_applications",
                targetId);

            var actorEmployeeId = actingUserId.HasValue && actingUserId.Value > 0
                ? await ResolveEmployeeIdByUserIdAsync(connection, actingUserId.Value)
                : null;
            var hasDecisionRemarks = await HasDecisionRemarksColumnAsync(connection);

            var setDecision = normalizedStatus is "APPROVED" or "REJECTED";
            var setRecommended = normalizedStatus == "RECOMMENDED";
            var setApproved = normalizedStatus == "APPROVED";

            try
            {
                var sql = hasDecisionRemarks ? sqlWithRemarks : sqlWithoutRemarks;
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@status", normalizedStatus);
                command.Parameters.AddWithValue("@set_decision", setDecision ? 1 : 0);
                command.Parameters.AddWithValue("@set_recommended", setRecommended ? 1 : 0);
                command.Parameters.AddWithValue("@set_approved", setApproved ? 1 : 0);
                command.Parameters.AddWithValue("@actor_employee_id", actorEmployeeId.HasValue && actorEmployeeId.Value > 0 ? actorEmployeeId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@leave_application_id", leaveApplicationId);
                if (hasDecisionRemarks)
                {
                    command.Parameters.AddWithValue("@decision_remarks", string.IsNullOrWhiteSpace(decisionRemarks) ? DBNull.Value : decisionRemarks.Trim());
                }

                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await _auditLogWriter.TryWriteAsync(
                        actingUserId,
                        "LEAVE_STATUS_UPDATE",
                        "leave_applications",
                        targetId,
                        "FAILED",
                        "Leave request was not found.");
                    throw new InvalidOperationException("Leave request was not found.");
                }

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "LEAVE_STATUS_UPDATE",
                    "leave_applications",
                    targetId,
                    "SUCCESS",
                    $"Status='{normalizedStatus}'.");
            }
            catch (MySqlException ex) when (IsUnknownColumnError(ex))
            {
                _hasDecisionRemarksColumn = false;
                await using var fallback = new MySqlCommand(sqlWithoutRemarks, connection);
                fallback.Parameters.AddWithValue("@status", normalizedStatus);
                fallback.Parameters.AddWithValue("@set_decision", setDecision ? 1 : 0);
                fallback.Parameters.AddWithValue("@set_recommended", setRecommended ? 1 : 0);
                fallback.Parameters.AddWithValue("@set_approved", setApproved ? 1 : 0);
                fallback.Parameters.AddWithValue("@actor_employee_id", actorEmployeeId.HasValue && actorEmployeeId.Value > 0 ? actorEmployeeId.Value : DBNull.Value);
                fallback.Parameters.AddWithValue("@leave_application_id", leaveApplicationId);
                var affected = await fallback.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await _auditLogWriter.TryWriteAsync(
                        actingUserId,
                        "LEAVE_STATUS_UPDATE",
                        "leave_applications",
                        targetId,
                        "FAILED",
                        "Leave request was not found.");
                    throw new InvalidOperationException("Leave request was not found.");
                }

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "LEAVE_STATUS_UPDATE",
                    "leave_applications",
                    targetId,
                    "SUCCESS",
                    $"Status='{normalizedStatus}'.");
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "LEAVE_STATUS_UPDATE",
                    "leave_applications",
                    targetId,
                    "FAILED",
                    ex.Message);
                throw;
            }
        }

        public async Task CancelOwnPendingLeaveRequestAsync(long leaveApplicationId, int employeeId, int? actingUserId = null)
        {
            if (leaveApplicationId <= 0)
            {
                throw new InvalidOperationException("Invalid leave request.");
            }

            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Employee is required.");
            }

            const string sqlWithRemarks = @"
UPDATE leave_applications
SET
    status = 'CANCELLED',
    decision_at = NOW(),
    decision_remarks = @decision_remarks
WHERE leave_application_id = @leave_application_id
  AND employee_id = @employee_id
  AND status IN ('SUBMITTED', 'RECOMMENDED');";

            const string sqlWithoutRemarks = @"
UPDATE leave_applications
SET
    status = 'CANCELLED',
    decision_at = NOW()
WHERE leave_application_id = @leave_application_id
  AND employee_id = @employee_id
  AND status IN ('SUBMITTED', 'RECOMMENDED');";

            var targetId = leaveApplicationId.ToString(CultureInfo.InvariantCulture);

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (actingUserId.HasValue && actingUserId.Value > 0)
            {
                var actorEmployeeId = await ResolveEmployeeIdByUserIdAsync(connection, actingUserId.Value);
                if (!actorEmployeeId.HasValue || actorEmployeeId.Value <= 0 || actorEmployeeId.Value != employeeId)
                {
                    await _auditLogWriter.TryWriteAsync(
                        actingUserId,
                        "LEAVE_REQUEST_CANCEL",
                        "leave_applications",
                        targetId,
                        "DENIED",
                        "Actor attempted to cancel a leave request not owned by the linked employee.");
                    throw new InvalidOperationException("You can only cancel your own pending leave request.");
                }
            }

            try
            {
                var hasDecisionRemarks = await HasDecisionRemarksColumnAsync(connection);
                var sql = hasDecisionRemarks ? sqlWithRemarks : sqlWithoutRemarks;
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@leave_application_id", leaveApplicationId);
                command.Parameters.AddWithValue("@employee_id", employeeId);
                if (hasDecisionRemarks)
                {
                    command.Parameters.AddWithValue("@decision_remarks", "Cancelled by employee.");
                }

                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await _auditLogWriter.TryWriteAsync(
                        actingUserId,
                        "LEAVE_REQUEST_CANCEL",
                        "leave_applications",
                        targetId,
                        "FAILED",
                        "Request not found, not owned by employee, or no longer pending.");
                    throw new InvalidOperationException("Only your pending request can be cancelled.");
                }

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "LEAVE_REQUEST_CANCEL",
                    "leave_applications",
                    targetId,
                    "SUCCESS",
                    "Employee cancelled pending leave request.");
            }
            catch (MySqlException ex) when (IsUnknownColumnError(ex))
            {
                _hasDecisionRemarksColumn = false;
                await using var fallback = new MySqlCommand(sqlWithoutRemarks, connection);
                fallback.Parameters.AddWithValue("@leave_application_id", leaveApplicationId);
                fallback.Parameters.AddWithValue("@employee_id", employeeId);
                var affected = await fallback.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await _auditLogWriter.TryWriteAsync(
                        actingUserId,
                        "LEAVE_REQUEST_CANCEL",
                        "leave_applications",
                        targetId,
                        "FAILED",
                        "Request not found, not owned by employee, or no longer pending.");
                    throw new InvalidOperationException("Only your pending request can be cancelled.");
                }

                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "LEAVE_REQUEST_CANCEL",
                    "leave_applications",
                    targetId,
                    "SUCCESS",
                    "Employee cancelled pending leave request.");
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    actingUserId,
                    "LEAVE_REQUEST_CANCEL",
                    "leave_applications",
                    targetId,
                    "FAILED",
                    ex.Message);
                throw;
            }
        }

        public async Task<IReadOnlyList<LeaveBalanceDto>> GetLeaveBalancesAsync(string? search = null, int? year = null, int? employeeId = null)
        {
            const string sql = @"
SELECT
    lb.leave_balance_id,
    lb.employee_id,
    COALESCE(e.employee_no,'-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    lb.leave_type_id,
    COALESCE(lt.name,'-') AS leave_type_name,
    lb.year,
    COALESCE(lb.opening_credits, 0) AS opening_credits,
    COALESCE(lb.earned, 0) AS earned,
    COALESCE(lb.used, 0) AS used,
    COALESCE(lb.adjustments, 0) AS adjustments,
    lb.as_of_date
FROM leave_balances lb
JOIN employees e ON e.employee_id = lb.employee_id
JOIN leave_types lt ON lt.leave_type_id = lb.leave_type_id
WHERE (@year IS NULL OR lb.year = @year)
  AND (@employee_id IS NULL OR lb.employee_id = @employee_id)
  AND (
      @search = '' OR
      e.employee_no LIKE CONCAT('%', @search, '%') OR
      CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) LIKE CONCAT('%', @search, '%') OR
      lt.name LIKE CONCAT('%', @search, '%')
  )
ORDER BY lb.year DESC, e.employee_no, lt.name;";

            var rows = new List<LeaveBalanceDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@year", year.HasValue && year.Value > 0 ? year.Value : DBNull.Value);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue ? employeeId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new LeaveBalanceDto(
                        LeaveBalanceId: ToLong(reader["leave_balance_id"]),
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        LeaveTypeId: ToInt(reader["leave_type_id"]),
                        LeaveTypeName: reader["leave_type_name"]?.ToString() ?? "-",
                        Year: ToInt(reader["year"]),
                        OpeningCredits: ToDecimal(reader["opening_credits"]),
                        Earned: ToDecimal(reader["earned"]),
                        Used: ToDecimal(reader["used"]),
                        Adjustments: ToDecimal(reader["adjustments"]),
                        AsOfDate: reader["as_of_date"] == DBNull.Value
                            ? DateTime.Today
                            : Convert.ToDateTime(reader["as_of_date"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<LeaveBalanceDto>();
            }

            return rows;
        }

        public async Task UpsertLeaveBalanceAsync(
            int employeeId,
            int leaveTypeId,
            int year,
            decimal openingCredits,
            decimal earned,
            decimal used,
            decimal adjustments,
            DateTime asOfDate)
        {
            if (employeeId <= 0 || leaveTypeId <= 0 || year <= 0)
            {
                throw new InvalidOperationException("Employee, leave type, and year are required.");
            }

            const string sql = @"
INSERT INTO leave_balances
    (employee_id, leave_type_id, year, opening_credits, earned, used, adjustments, as_of_date)
VALUES
    (@employee_id, @leave_type_id, @year, @opening_credits, @earned, @used, @adjustments, @as_of_date)
ON DUPLICATE KEY UPDATE
    opening_credits = VALUES(opening_credits),
    earned = VALUES(earned),
    used = VALUES(used),
    adjustments = VALUES(adjustments),
    as_of_date = VALUES(as_of_date),
    updated_at = CURRENT_TIMESTAMP;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@leave_type_id", leaveTypeId);
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.AddWithValue("@opening_credits", openingCredits);
            command.Parameters.AddWithValue("@earned", earned);
            command.Parameters.AddWithValue("@used", used);
            command.Parameters.AddWithValue("@adjustments", adjustments);
            command.Parameters.AddWithValue("@as_of_date", asOfDate.Date);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<LeaveAttachmentDto>> GetLeaveAttachmentsAsync(string? search = null, int limit = 300, int? employeeId = null)
        {
            const string sql = @"
SELECT
    ld.leave_document_id,
    ld.leave_application_id,
    COALESCE(e.employee_no,'-') AS employee_no,
    COALESCE(CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')), '-') AS employee_name,
    COALESCE(ld.file_name,'-') AS file_name,
    COALESCE(ld.file_path,'') AS file_path,
    ld.uploaded_at
FROM leave_documents ld
LEFT JOIN leave_applications la ON la.leave_application_id = ld.leave_application_id
LEFT JOIN employees e ON e.employee_id = la.employee_id
WHERE (
    @search = '' OR
    COALESCE(e.employee_no,'') LIKE CONCAT('%', @search, '%') OR
    COALESCE(CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')), '') LIKE CONCAT('%', @search, '%') OR
    COALESCE(ld.file_name,'') LIKE CONCAT('%', @search, '%') OR
    COALESCE(ld.file_path,'') LIKE CONCAT('%', @search, '%')
)
AND (@employee_id IS NULL OR la.employee_id = @employee_id)
ORDER BY ld.uploaded_at DESC
LIMIT @limit;";

            var rows = new List<LeaveAttachmentDto>();

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
                    rows.Add(new LeaveAttachmentDto(
                        LeaveDocumentId: ToLong(reader["leave_document_id"]),
                        LeaveApplicationId: ToLong(reader["leave_application_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        FileName: reader["file_name"]?.ToString() ?? "-",
                        FilePath: reader["file_path"]?.ToString() ?? string.Empty,
                        UploadedAt: reader["uploaded_at"] == DBNull.Value
                            ? DateTime.MinValue
                            : Convert.ToDateTime(reader["uploaded_at"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<LeaveAttachmentDto>();
            }

            return rows;
        }

        public async Task AddLeaveAttachmentAsync(long leaveApplicationId, string filePath, int? uploadedByUserId)
        {
            if (leaveApplicationId <= 0)
            {
                throw new InvalidOperationException("Select a leave request before attaching a file.");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException("File path is required.");
            }

            var employeeId = uploadedByUserId.HasValue && uploadedByUserId.Value > 0
                ? await ResolveEmployeeIdByUserIdAsync(uploadedByUserId.Value)
                : null;

            const string sql = @"
INSERT INTO leave_documents
    (leave_application_id, file_name, file_path, uploaded_by_employee_id)
VALUES
    (@leave_application_id, @file_name, @file_path, @uploaded_by_employee_id);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@leave_application_id", leaveApplicationId);
            command.Parameters.AddWithValue("@file_name", Path.GetFileName(filePath));
            command.Parameters.AddWithValue("@file_path", filePath.Trim());
            command.Parameters.AddWithValue("@uploaded_by_employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteLeaveAttachmentAsync(long leaveDocumentId)
        {
            if (leaveDocumentId <= 0)
            {
                throw new InvalidOperationException("Invalid attachment.");
            }

            const string sql = @"
DELETE FROM leave_documents
WHERE leave_document_id = @leave_document_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@leave_document_id", leaveDocumentId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<int?> GetEmployeeIdByUserIdAsync(int userId)
        {
            return await ResolveEmployeeIdByUserIdAsync(userId);
        }

        private async Task<IReadOnlyList<LeaveRequestDto>> ReadRequestsAsync(
            MySqlConnection connection,
            string sql,
            string? statusFilter,
            string? search,
            int limit,
            int? employeeId)
        {
            var rows = new List<LeaveRequestDto>();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@status", NormalizeStatusFilter(statusFilter));
            command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
            command.Parameters.AddWithValue("@employee_id", employeeId.HasValue ? employeeId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@limit", Math.Max(1, limit));

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new LeaveRequestDto(
                    LeaveApplicationId: ToLong(reader["leave_application_id"]),
                    EmployeeId: ToInt(reader["employee_id"]),
                    EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                    EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                    LeaveTypeId: ToInt(reader["leave_type_id"]),
                    LeaveTypeCode: reader["leave_type_code"]?.ToString() ?? "-",
                    LeaveTypeName: reader["leave_type_name"]?.ToString() ?? "-",
                    DateFrom: reader["date_from"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["date_from"], CultureInfo.InvariantCulture),
                    DateTo: reader["date_to"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["date_to"], CultureInfo.InvariantCulture),
                    DaysRequested: ToDecimal(reader["days_requested"]),
                    Reason: reader["reason"]?.ToString() ?? string.Empty,
                    Status: reader["status"]?.ToString() ?? "SUBMITTED",
                    FiledAt: reader["filed_at"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["filed_at"], CultureInfo.InvariantCulture),
                    DecisionAt: reader["decision_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["decision_at"], CultureInfo.InvariantCulture),
                    DecisionRemarks: reader["decision_remarks"]?.ToString() ?? string.Empty,
                    AttachmentCount: ToInt(reader["docs_count"])));
            }

            return rows;
        }

        private async Task<bool> HasDecisionRemarksColumnAsync(MySqlConnection connection)
        {
            if (_hasDecisionRemarksColumn.HasValue)
            {
                return _hasDecisionRemarksColumn.Value;
            }

            const string sql = @"
SELECT 1
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'leave_applications'
  AND COLUMN_NAME = 'decision_remarks'
LIMIT 1;";

            try
            {
                await using var command = new MySqlCommand(sql, connection);
                var result = await command.ExecuteScalarAsync();
                _hasDecisionRemarksColumn = result != null && result != DBNull.Value;
            }
            catch
            {
                _hasDecisionRemarksColumn = false;
            }

            return _hasDecisionRemarksColumn.Value;
        }

        private async Task<int?> ResolveEmployeeIdByUserIdAsync(int userId)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return await ResolveEmployeeIdByUserIdAsync(connection, userId);
        }

        private static async Task<int?> ResolveEmployeeIdByUserIdAsync(MySqlConnection connection, int userId)
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
            if (resolvedEmployeeId.HasValue && resolvedEmployeeId.Value > 0)
            {
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

            return null;
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
            if (candidates.Count == 0)
            {
                return null;
            }

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

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var resolvedByName = await TryResolveEmployeeIdByFullNameAsync(connection, fullName);
                if (resolvedByName.HasValue && resolvedByName.Value > 0)
                {
                    return resolvedByName.Value;
                }
            }

            return null;
        }

        private static async Task<int?> TryResolveEmployeeIdByFullNameAsync(MySqlConnection connection, string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            const string sql = @"
SELECT
    employee_id,
    first_name,
    middle_name,
    last_name
FROM employees
;";

            var normalizedTarget = NormalizePersonName(fullName);
            if (string.IsNullOrWhiteSpace(normalizedTarget))
            {
                return null;
            }

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var employeeId = ToInt(reader["employee_id"]);
                if (employeeId <= 0)
                {
                    continue;
                }

                var first = reader["first_name"]?.ToString() ?? string.Empty;
                var middle = reader["middle_name"]?.ToString() ?? string.Empty;
                var last = reader["last_name"]?.ToString() ?? string.Empty;

                if (NameMatches(normalizedTarget, first, middle, last))
                {
                    return employeeId;
                }
            }

            return null;
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

            var normalized = new string(buffer, 0, index).Trim();
            return normalized;
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

        private static object NormalizeStatusFilter(string? statusFilter)
        {
            var value = statusFilter?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(value) || value == "ALL")
            {
                return DBNull.Value;
            }

            return value;
        }

        private static string NormalizeRequestStatus(string? status)
        {
            var value = status?.Trim().ToUpperInvariant();
            return value switch
            {
                "DRAFT" => "DRAFT",
                "SUBMITTED" => "SUBMITTED",
                "RECOMMENDED" => "RECOMMENDED",
                "APPROVED" => "APPROVED",
                "REJECTED" => "REJECTED",
                "CANCELLED" => "CANCELLED",
                _ => throw new InvalidOperationException("Unsupported leave request status.")
            };
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

        private static bool IsMissingObjectError(MySqlException ex) =>
            ex.Number == 1109 || ex.Number == 1146 || ex.Number == 1356;

        private static bool IsUnknownColumnError(MySqlException ex) =>
            ex.Number == 1054 ||
            ex.Message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase);

        private static int ToInt(object value) =>
            value == DBNull.Value || value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);

        private static long ToLong(object value) =>
            value == DBNull.Value || value == null ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);

        private static decimal ToDecimal(object value) =>
            value == DBNull.Value || value == null ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }
}
