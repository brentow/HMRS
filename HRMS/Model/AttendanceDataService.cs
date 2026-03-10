using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record AttendanceStatsDto(
        int TotalLogs,
        int TodayLogs,
        int PresentToday,
        int IncompleteLogs,
        int PendingAdjustments,
        int ActiveDevices);

    public record AttendanceLogDto(
        long LogId,
        string EmployeeNo,
        string EmployeeName,
        DateTime LogTime,
        string LogType,
        string Source,
        string DeviceName);

    public record AttendanceAdjustmentDto(
        long AdjustmentId,
        string EmployeeNo,
        string EmployeeName,
        DateTime WorkDate,
        DateTime? RequestedIn,
        DateTime? RequestedOut,
        string Reason,
        string Status,
        DateTime RequestedAt,
        string DecisionRemarks,
        DateTime? DecidedAt);

    public record AttendanceAdjustmentCountsDto(
        int Pending,
        int Approved,
        int Rejected);

    public record BiometricDeviceDto(
        int DeviceId,
        string DeviceName,
        string SerialNo,
        string Location,
        string IpAddress,
        bool IsActive,
        DateTime? LastSyncAt);

    public record EmployeeLookupDto(int EmployeeId, string EmployeeNo, string EmployeeName);

    public record EnrollmentEmployeeProfileDto(
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        string DepartmentName,
        string PositionName,
        string CurrentEnrollmentStatus);

    public record BiometricEnrollmentDto(
        int EnrollmentId,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        string BiometricUserId,
        int? DeviceId,
        string DeviceName,
        string Status,
        DateTime CreatedAt);

    public record ShiftDto(
        int ShiftId,
        string ShiftName,
        TimeSpan StartTime,
        TimeSpan EndTime,
        int BreakMinutes,
        int GraceMinutes,
        bool IsOvernight);

    public record ShiftAssignmentDto(
        int AssignmentId,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        int ShiftId,
        string ShiftName,
        DateTime StartDate,
        DateTime? EndDate,
        string Status);

    public partial class AttendanceDataService
    {
        private readonly string _connectionString;
        private readonly AuditLogWriter _auditLogWriter;
        private bool? _hasAdjustmentDecisionRemarksColumn;

        public AttendanceDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _auditLogWriter = new AuditLogWriter(_connectionString);
        }

        public async Task<AttendanceStatsDto> GetStatsAsync()
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*) FROM attendance_logs) AS total_logs,
    (SELECT COUNT(*) FROM attendance_logs WHERE DATE(log_time)=CURDATE()) AS today_logs,
    (SELECT COUNT(DISTINCT employee_id) FROM attendance_logs WHERE DATE(log_time)=CURDATE() AND log_type='IN') AS present_today,
    (SELECT COUNT(*) FROM (
        SELECT employee_id, DATE(log_time) work_date,
               SUM(CASE WHEN log_type='IN' THEN 1 ELSE 0 END) in_count,
               SUM(CASE WHEN log_type='OUT' THEN 1 ELSE 0 END) out_count
        FROM attendance_logs WHERE DATE(log_time)=CURDATE()
        GROUP BY employee_id, DATE(log_time)
        HAVING in_count > 0 AND out_count = 0
    ) t) AS incomplete_logs,
    (SELECT COUNT(*) FROM attendance_adjustments WHERE status='PENDING') AS pending_adjustments,
    (SELECT COUNT(*) FROM biometric_devices WHERE is_active=1) AS active_devices;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new AttendanceStatsDto(
                        ToInt(reader["total_logs"]),
                        ToInt(reader["today_logs"]),
                        ToInt(reader["present_today"]),
                        ToInt(reader["incomplete_logs"]),
                        ToInt(reader["pending_adjustments"]),
                        ToInt(reader["active_devices"]));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
            }

            return new AttendanceStatsDto(0, 0, 0, 0, 0, 0);
        }

        public async Task<IReadOnlyList<AttendanceLogDto>> GetRecentLogsAsync(int limit = 250)
        {
            const string sql = @"
SELECT
    al.log_id,
    COALESCE(e.employee_no,'-') employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
    al.log_time,
    COALESCE(al.log_type,'-') log_type,
    COALESCE(al.source,'-') source,
    COALESCE(d.device_name,'-') device_name
FROM attendance_logs al
JOIN employees e ON e.employee_id = al.employee_id
LEFT JOIN biometric_devices d ON d.device_id = al.device_id
ORDER BY al.log_time DESC
LIMIT @limit;";

            var list = new List<AttendanceLogDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new AttendanceLogDto(
                        ToLong(reader["log_id"]),
                        reader["employee_no"]?.ToString() ?? "-",
                        reader["employee_name"]?.ToString() ?? "-",
                        reader["log_time"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["log_time"], CultureInfo.InvariantCulture),
                        reader["log_type"]?.ToString() ?? "-",
                        reader["source"]?.ToString() ?? "-",
                        reader["device_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<AttendanceLogDto>();
            }
            return list;
        }

        public async Task<IReadOnlyList<AttendanceAdjustmentDto>> GetPendingAdjustmentsAsync(int limit = 100, string? statusFilter = "PENDING")
        {
            const string sqlWithDecisionRemarks = @"
SELECT
    aa.adjustment_id,
    COALESCE(e.employee_no,'-') employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
    aa.work_date, aa.requested_in, aa.requested_out,
    COALESCE(aa.reason,'-') reason,
    COALESCE(aa.status,'PENDING') status,
    aa.requested_at,
    COALESCE(aa.decision_remarks,'') decision_remarks,
    aa.decided_at
FROM attendance_adjustments aa
JOIN employees e ON e.employee_id = aa.employee_id
WHERE (@status IS NULL OR aa.status = @status)
ORDER BY aa.requested_at DESC
LIMIT @limit;";

            const string sqlWithoutDecisionRemarks = @"
SELECT
    aa.adjustment_id,
    COALESCE(e.employee_no,'-') employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
    aa.work_date, aa.requested_in, aa.requested_out,
    COALESCE(aa.reason,'-') reason,
    COALESCE(aa.status,'PENDING') status,
    aa.requested_at,
    '' AS decision_remarks,
    aa.decided_at
FROM attendance_adjustments aa
JOIN employees e ON e.employee_id = aa.employee_id
WHERE (@status IS NULL OR aa.status = @status)
ORDER BY aa.requested_at DESC
LIMIT @limit;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var hasDecisionRemarks = await HasAdjustmentDecisionRemarksColumnAsync(connection);
                var primarySql = hasDecisionRemarks ? sqlWithDecisionRemarks : sqlWithoutDecisionRemarks;

                try
                {
                    return await ReadAdjustmentsAsync(connection, primarySql, limit, statusFilter);
                }
                catch (MySqlException ex) when (IsUnknownColumnError(ex))
                {
                    _hasAdjustmentDecisionRemarksColumn = false;
                    return await ReadAdjustmentsAsync(connection, sqlWithoutDecisionRemarks, limit, statusFilter);
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<AttendanceAdjustmentDto>();
            }
        }

        public async Task<AttendanceAdjustmentCountsDto> GetAdjustmentStatusCountsAsync()
        {
            const string sql = @"
SELECT
    SUM(CASE WHEN status = 'PENDING' THEN 1 ELSE 0 END) AS pending_count,
    SUM(CASE WHEN status = 'APPROVED' THEN 1 ELSE 0 END) AS approved_count,
    SUM(CASE WHEN status = 'REJECTED' THEN 1 ELSE 0 END) AS rejected_count
FROM attendance_adjustments;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new AttendanceAdjustmentCountsDto(
                        Pending: ToInt(reader["pending_count"]),
                        Approved: ToInt(reader["approved_count"]),
                        Rejected: ToInt(reader["rejected_count"]));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
            }

            return new AttendanceAdjustmentCountsDto(0, 0, 0);
        }

        public async Task DeleteLogAsync(long logId)
        {
            const string sql = "DELETE FROM attendance_logs WHERE log_id=@id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", logId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateAdjustmentStatusAsync(long adjustmentId, string status, string? decisionRemarks = null, int? actingUserId = null)
        {
            if (adjustmentId <= 0)
            {
                throw new InvalidOperationException("Invalid adjustment request.");
            }

            const string sqlWithDecisionRemarks = @"
UPDATE attendance_adjustments
SET status=@status,
    decided_at=NOW(),
    approved_by_user_id=@approved_by_user_id,
    decision_remarks=@decision_remarks
WHERE adjustment_id=@id;";

            const string sqlWithoutDecisionRemarks = @"
UPDATE attendance_adjustments
SET status=@status,
    decided_at=NOW(),
    approved_by_user_id=@approved_by_user_id
WHERE adjustment_id=@id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureAdminOrHrAccessAsync(
                connection,
                actingUserId,
                "update attendance adjustment status",
                "ATTENDANCE_ADJUSTMENT_DECISION",
                "attendance_adjustments",
                adjustmentId.ToString(CultureInfo.InvariantCulture));

            var normalizedStatus = NormalizeAdjustmentStatus(status);
            var hasDecisionRemarks = await HasAdjustmentDecisionRemarksColumnAsync(connection);
            var targetId = adjustmentId.ToString(CultureInfo.InvariantCulture);
            var decidedByUserId = actingUserId.HasValue && actingUserId.Value > 0
                ? actingUserId.Value
                : (int?)null;
            if (hasDecisionRemarks)
            {
                try
                {
                    await using var command = new MySqlCommand(sqlWithDecisionRemarks, connection);
                    command.Parameters.AddWithValue("@status", normalizedStatus);
                    command.Parameters.AddWithValue("@approved_by_user_id", decidedByUserId.HasValue ? decidedByUserId.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@decision_remarks", string.IsNullOrWhiteSpace(decisionRemarks) ? DBNull.Value : decisionRemarks.Trim());
                    command.Parameters.AddWithValue("@id", adjustmentId);
                    var affected = await command.ExecuteNonQueryAsync();
                    if (affected == 0)
                    {
                        await _auditLogWriter.TryWriteAsync(
                            decidedByUserId,
                            "ATTENDANCE_ADJUSTMENT_DECISION",
                            "attendance_adjustments",
                            targetId,
                            "FAILED",
                            "Adjustment request not found.");
                        throw new InvalidOperationException("Adjustment request was not found.");
                    }

                    await _auditLogWriter.TryWriteAsync(
                        decidedByUserId,
                        "ATTENDANCE_ADJUSTMENT_DECISION",
                        "attendance_adjustments",
                        targetId,
                        "SUCCESS",
                        $"Status='{normalizedStatus}'.");
                    return;
                }
                catch (MySqlException ex) when (IsUnknownColumnError(ex))
                {
                    _hasAdjustmentDecisionRemarksColumn = false;
                }
            }

            try
            {
                await using var fallback = new MySqlCommand(sqlWithoutDecisionRemarks, connection);
                fallback.Parameters.AddWithValue("@status", normalizedStatus);
                fallback.Parameters.AddWithValue("@approved_by_user_id", decidedByUserId.HasValue ? decidedByUserId.Value : DBNull.Value);
                fallback.Parameters.AddWithValue("@id", adjustmentId);

                var affected = await fallback.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    await _auditLogWriter.TryWriteAsync(
                        decidedByUserId,
                        "ATTENDANCE_ADJUSTMENT_DECISION",
                        "attendance_adjustments",
                        targetId,
                        "FAILED",
                        "Adjustment request not found.");
                    throw new InvalidOperationException("Adjustment request was not found.");
                }

                await _auditLogWriter.TryWriteAsync(
                    decidedByUserId,
                    "ATTENDANCE_ADJUSTMENT_DECISION",
                    "attendance_adjustments",
                    targetId,
                    "SUCCESS",
                    $"Status='{normalizedStatus}'.");
            }
            catch (Exception ex)
            {
                await _auditLogWriter.TryWriteAsync(
                    decidedByUserId,
                    "ATTENDANCE_ADJUSTMENT_DECISION",
                    "attendance_adjustments",
                    targetId,
                    "FAILED",
                    ex.Message);
                throw;
            }
        }

        public async Task<long> CreateAdjustmentAsync(
            int employeeId,
            DateTime workDate,
            DateTime? requestedIn,
            DateTime? requestedOut,
            string reason,
            int requestedByUserId)
        {
            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Employee is required.");
            }

            if (requestedByUserId <= 0)
            {
                throw new InvalidOperationException("Requesting user is required.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException("Reason is required.");
            }

            const string sql = @"
INSERT INTO attendance_adjustments
(
    employee_id,
    work_date,
    requested_in,
    requested_out,
    reason,
    status,
    requested_by_user_id,
    requested_at
)
VALUES
(
    @employee_id,
    @work_date,
    @requested_in,
    @requested_out,
    @reason,
    'PENDING',
    @requested_by_user_id,
    NOW()
);
SELECT LAST_INSERT_ID();";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@work_date", workDate.Date);
            command.Parameters.AddWithValue("@requested_in", requestedIn.HasValue ? requestedIn.Value : DBNull.Value);
            command.Parameters.AddWithValue("@requested_out", requestedOut.HasValue ? requestedOut.Value : DBNull.Value);
            command.Parameters.AddWithValue("@reason", reason.Trim());
            command.Parameters.AddWithValue("@requested_by_user_id", requestedByUserId);

            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        public async Task DeleteOwnPendingAdjustmentAsync(long adjustmentId, int employeeId)
        {
            const string sql = @"
DELETE FROM attendance_adjustments
WHERE adjustment_id = @id
  AND employee_id = @employee_id
  AND status = 'PENDING';";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", adjustmentId);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<IReadOnlyList<AttendanceAdjustmentDto>> ReadAdjustmentsAsync(MySqlConnection connection, string sql, int limit, string? statusFilter)
        {
            var list = new List<AttendanceAdjustmentDto>();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
            command.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(statusFilter) || string.Equals(statusFilter, "ALL", StringComparison.OrdinalIgnoreCase)
                ? DBNull.Value
                : statusFilter.Trim().ToUpperInvariant());
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AttendanceAdjustmentDto(
                    ToLong(reader["adjustment_id"]),
                    reader["employee_no"]?.ToString() ?? "-",
                    reader["employee_name"]?.ToString() ?? "-",
                    reader["work_date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["work_date"], CultureInfo.InvariantCulture),
                    reader["requested_in"] == DBNull.Value ? null : Convert.ToDateTime(reader["requested_in"], CultureInfo.InvariantCulture),
                    reader["requested_out"] == DBNull.Value ? null : Convert.ToDateTime(reader["requested_out"], CultureInfo.InvariantCulture),
                    reader["reason"]?.ToString() ?? "-",
                    reader["status"]?.ToString() ?? "PENDING",
                    reader["requested_at"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["requested_at"], CultureInfo.InvariantCulture),
                    reader["decision_remarks"]?.ToString() ?? string.Empty,
                    reader["decided_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["decided_at"], CultureInfo.InvariantCulture)));
            }

            return list;
        }

        private async Task<bool> HasAdjustmentDecisionRemarksColumnAsync(MySqlConnection connection)
        {
            if (_hasAdjustmentDecisionRemarksColumn.HasValue)
            {
                return _hasAdjustmentDecisionRemarksColumn.Value;
            }

            const string sql = @"
SELECT 1
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'attendance_adjustments'
  AND COLUMN_NAME = 'decision_remarks'
LIMIT 1;";

            try
            {
                await using var command = new MySqlCommand(sql, connection);
                var result = await command.ExecuteScalarAsync();
                _hasAdjustmentDecisionRemarksColumn = result != null && result != DBNull.Value;
            }
            catch
            {
                _hasAdjustmentDecisionRemarksColumn = false;
            }

            return _hasAdjustmentDecisionRemarksColumn.Value;
        }

        public async Task<IReadOnlyList<BiometricDeviceDto>> GetBiometricDevicesAsync()
        {
            const string sql = @"
SELECT device_id, COALESCE(device_name,'-') device_name, COALESCE(serial_no,'-') serial_no,
       COALESCE(location,'-') location, COALESCE(ip_address,'-') ip_address,
       COALESCE(is_active,0) is_active, last_sync_at
FROM biometric_devices
ORDER BY device_name;";

            var list = new List<BiometricDeviceDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new BiometricDeviceDto(
                        ToInt(reader["device_id"]),
                        reader["device_name"]?.ToString() ?? "-",
                        reader["serial_no"]?.ToString() ?? "-",
                        reader["location"]?.ToString() ?? "-",
                        reader["ip_address"]?.ToString() ?? "-",
                        ToInt(reader["is_active"]) == 1,
                        reader["last_sync_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["last_sync_at"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<BiometricDeviceDto>();
            }
            return list;
        }

        public async Task AddDeviceAsync(string deviceName, string? serialNo, string? location, string? ipAddress, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                throw new InvalidOperationException("Device name is required.");
            }

            const string sql = @"
INSERT INTO biometric_devices (device_name, serial_no, location, ip_address, is_active)
VALUES (@name,@serial,@location,@ip,@active)
ON DUPLICATE KEY UPDATE
serial_no=VALUES(serial_no), location=VALUES(location), ip_address=VALUES(ip_address), is_active=VALUES(is_active);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", deviceName.Trim());
            command.Parameters.AddWithValue("@serial", string.IsNullOrWhiteSpace(serialNo) ? DBNull.Value : serialNo.Trim());
            command.Parameters.AddWithValue("@location", string.IsNullOrWhiteSpace(location) ? DBNull.Value : location.Trim());
            command.Parameters.AddWithValue("@ip", string.IsNullOrWhiteSpace(ipAddress) ? DBNull.Value : ipAddress.Trim());
            command.Parameters.AddWithValue("@active", isActive ? 1 : 0);
            await command.ExecuteNonQueryAsync();
        }

        public async Task ToggleDeviceActiveAsync(int deviceId, bool isActive)
        {
            const string sql = "UPDATE biometric_devices SET is_active=@active WHERE device_id=@id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@active", isActive ? 1 : 0);
            command.Parameters.AddWithValue("@id", deviceId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task MarkDeviceSyncedNowAsync(int deviceId)
        {
            const string sql = "UPDATE biometric_devices SET last_sync_at=NOW() WHERE device_id=@id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", deviceId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<EmployeeLookupDto>> GetEmployeesLookupAsync()
        {
            const string sql = @"
SELECT employee_id, employee_no,
       CONCAT(last_name, ', ', first_name, IFNULL(CONCAT(' ', middle_name), '')) employee_name
FROM employees
WHERE status='ACTIVE'
ORDER BY employee_no;";

            var list = new List<EmployeeLookupDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new EmployeeLookupDto(
                        ToInt(reader["employee_id"]),
                        reader["employee_no"]?.ToString() ?? "-",
                        reader["employee_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<EmployeeLookupDto>();
            }
            return list;
        }

        public async Task<EnrollmentEmployeeProfileDto?> GetEnrollmentEmployeeProfileAsync(int employeeId)
        {
            if (employeeId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT
    e.employee_id,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    COALESCE(d.dept_name, '-') AS department_name,
    COALESCE(p.position_name, '-') AS position_name,
    COALESCE((
        SELECT be.status
        FROM biometric_enrollments be
        WHERE be.employee_id = e.employee_id
        ORDER BY be.created_at DESC, be.enrollment_id DESC
        LIMIT 1
    ), 'NOT ENROLLED') AS current_enrollment_status
FROM employees e
LEFT JOIN departments d ON d.department_id = e.department_id
LEFT JOIN positions p ON p.position_id = e.position_id
WHERE e.employee_id = @employee_id
LIMIT 1;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId);
                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return new EnrollmentEmployeeProfileDto(
                    ToInt(reader["employee_id"]),
                    reader["employee_no"]?.ToString() ?? "-",
                    reader["employee_name"]?.ToString() ?? "-",
                    reader["department_name"]?.ToString() ?? "-",
                    reader["position_name"]?.ToString() ?? "-",
                    reader["current_enrollment_status"]?.ToString() ?? "NOT ENROLLED");
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return null;
            }
        }

        public async Task<int?> GetEmployeeIdByUserIdAsync(int userId)
        {
            if (userId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT employee_id
FROM user_accounts
WHERE user_id = @user_id
LIMIT 1;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@user_id", userId);

            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            return ToInt(result);
        }

        public async Task<IReadOnlyList<BiometricEnrollmentDto>> GetBiometricEnrollmentsAsync(int limit = 500)
        {
            const string sql = @"
SELECT be.enrollment_id, e.employee_id, COALESCE(e.employee_no,'-') employee_no,
       CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
       COALESCE(be.biometric_user_id,'-') biometric_user_id,
       be.device_id, COALESCE(d.device_name,'-') device_name,
       COALESCE(be.status,'ACTIVE') status, be.created_at
FROM biometric_enrollments be
JOIN employees e ON e.employee_id = be.employee_id
LEFT JOIN biometric_devices d ON d.device_id = be.device_id
ORDER BY be.created_at DESC
LIMIT @limit;";

            var list = new List<BiometricEnrollmentDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new BiometricEnrollmentDto(
                        ToInt(reader["enrollment_id"]),
                        ToInt(reader["employee_id"]),
                        reader["employee_no"]?.ToString() ?? "-",
                        reader["employee_name"]?.ToString() ?? "-",
                        reader["biometric_user_id"]?.ToString() ?? "-",
                        reader["device_id"] == DBNull.Value ? null : ToInt(reader["device_id"]),
                        reader["device_name"]?.ToString() ?? "-",
                        reader["status"]?.ToString() ?? "ACTIVE",
                        reader["created_at"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["created_at"], CultureInfo.InvariantCulture)));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<BiometricEnrollmentDto>();
            }
            return list;
        }

        public async Task AddBiometricEnrollmentAsync(int employeeId, string biometricUserId, int? deviceId, string status)
        {
            if (employeeId <= 0 || string.IsNullOrWhiteSpace(biometricUserId))
            {
                throw new InvalidOperationException("Employee and biometric user ID are required.");
            }

            const string sql = @"
INSERT INTO biometric_enrollments (employee_id, biometric_user_id, device_id, status)
VALUES (@employee_id, @bio_id, @device_id, @status);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@bio_id", biometricUserId.Trim());
            command.Parameters.AddWithValue("@device_id", deviceId.HasValue && deviceId.Value > 0 ? deviceId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@status", NormalizeEnrollmentStatus(status));
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateBiometricEnrollmentStatusAsync(int enrollmentId, string status)
        {
            const string sql = "UPDATE biometric_enrollments SET status=@status WHERE enrollment_id=@id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@status", NormalizeEnrollmentStatus(status));
            command.Parameters.AddWithValue("@id", enrollmentId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteBiometricEnrollmentAsync(int enrollmentId)
        {
            const string sql = "DELETE FROM biometric_enrollments WHERE enrollment_id=@id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", enrollmentId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<ShiftDto>> GetShiftsAsync()
        {
            const string sql = @"
SELECT shift_id, COALESCE(shift_name,'-') shift_name, start_time, end_time,
       COALESCE(break_minutes,0) break_minutes, COALESCE(grace_minutes,0) grace_minutes,
       COALESCE(is_overnight,0) is_overnight
FROM shifts
ORDER BY shift_name;";

            var list = new List<ShiftDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ShiftDto(
                        ToInt(reader["shift_id"]),
                        reader["shift_name"]?.ToString() ?? "-",
                        reader["start_time"] == DBNull.Value ? TimeSpan.Zero : TimeSpan.Parse(reader["start_time"].ToString() ?? "00:00:00", CultureInfo.InvariantCulture),
                        reader["end_time"] == DBNull.Value ? TimeSpan.Zero : TimeSpan.Parse(reader["end_time"].ToString() ?? "00:00:00", CultureInfo.InvariantCulture),
                        ToInt(reader["break_minutes"]),
                        ToInt(reader["grace_minutes"]),
                        ToInt(reader["is_overnight"]) == 1));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<ShiftDto>();
            }
            return list;
        }

        public async Task AddShiftAsync(string shiftName, TimeSpan startTime, TimeSpan endTime, int breakMinutes, int graceMinutes, bool isOvernight)
        {
            if (string.IsNullOrWhiteSpace(shiftName))
            {
                throw new InvalidOperationException("Shift name is required.");
            }

            const string sql = @"
INSERT INTO shifts (shift_name, start_time, end_time, break_minutes, grace_minutes, is_overnight)
VALUES (@name, @start_time, @end_time, @break_minutes, @grace_minutes, @overnight)
ON DUPLICATE KEY UPDATE
start_time=VALUES(start_time),
end_time=VALUES(end_time),
break_minutes=VALUES(break_minutes),
grace_minutes=VALUES(grace_minutes),
is_overnight=VALUES(is_overnight);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", shiftName.Trim());
            command.Parameters.AddWithValue("@start_time", startTime);
            command.Parameters.AddWithValue("@end_time", endTime);
            command.Parameters.AddWithValue("@break_minutes", Math.Max(0, breakMinutes));
            command.Parameters.AddWithValue("@grace_minutes", Math.Max(0, graceMinutes));
            command.Parameters.AddWithValue("@overnight", isOvernight ? 1 : 0);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteShiftAsync(int shiftId)
        {
            const string sql = "DELETE FROM shifts WHERE shift_id=@id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", shiftId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<ShiftAssignmentDto>> GetShiftAssignmentsAsync(int limit = 600)
        {
            const string sql = @"
SELECT sa.assignment_id, e.employee_id, COALESCE(e.employee_no,'-') employee_no,
       CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) employee_name,
       s.shift_id, COALESCE(s.shift_name,'-') shift_name,
       sa.start_date, sa.end_date, COALESCE(sa.status,'ASSIGNED') status
FROM shift_assignments sa
JOIN employees e ON e.employee_id = sa.employee_id
JOIN shifts s ON s.shift_id = sa.shift_id
ORDER BY sa.start_date DESC, e.employee_no
LIMIT @limit;";

            var list = new List<ShiftAssignmentDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ShiftAssignmentDto(
                        ToInt(reader["assignment_id"]),
                        ToInt(reader["employee_id"]),
                        reader["employee_no"]?.ToString() ?? "-",
                        reader["employee_name"]?.ToString() ?? "-",
                        ToInt(reader["shift_id"]),
                        reader["shift_name"]?.ToString() ?? "-",
                        reader["start_date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["start_date"], CultureInfo.InvariantCulture),
                        reader["end_date"] == DBNull.Value ? null : Convert.ToDateTime(reader["end_date"], CultureInfo.InvariantCulture),
                        reader["status"]?.ToString() ?? "ASSIGNED"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<ShiftAssignmentDto>();
            }
            return list;
        }

        public async Task AddShiftAssignmentAsync(int employeeId, int shiftId, DateTime startDate, DateTime? endDate, string status)
        {
            if (employeeId <= 0 || shiftId <= 0)
            {
                throw new InvalidOperationException("Employee and shift are required.");
            }

            var normalizedStartDate = startDate.Date;
            var normalizedEndDate = endDate?.Date;
            if (normalizedEndDate.HasValue && normalizedEndDate.Value < normalizedStartDate)
            {
                throw new InvalidOperationException("Shift assignment end date cannot be earlier than the start date.");
            }

            const string sql = @"
INSERT INTO shift_assignments (employee_id, shift_id, start_date, end_date, assigned_by_user_id, status)
VALUES (@employee_id, @shift_id, @start_date, @end_date, NULL, @status)
ON DUPLICATE KEY UPDATE
    shift_id = VALUES(shift_id),
    end_date = VALUES(end_date),
    status = VALUES(status),
    assigned_by_user_id = VALUES(assigned_by_user_id);";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureNoOverlappingShiftAssignmentAsync(connection, employeeId, normalizedStartDate, normalizedEndDate);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@shift_id", shiftId);
            command.Parameters.AddWithValue("@start_date", normalizedStartDate);
            command.Parameters.AddWithValue("@end_date", normalizedEndDate.HasValue ? normalizedEndDate.Value : DBNull.Value);
            command.Parameters.AddWithValue("@status", NormalizeAssignmentStatus(status));
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateShiftAssignmentStatusAsync(int assignmentId, string status)
        {
            const string sql = "UPDATE shift_assignments SET status=@status WHERE assignment_id=@id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@status", NormalizeAssignmentStatus(status));
            command.Parameters.AddWithValue("@id", assignmentId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteShiftAssignmentAsync(int assignmentId)
        {
            const string sql = "DELETE FROM shift_assignments WHERE assignment_id=@id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", assignmentId);
            await command.ExecuteNonQueryAsync();
        }

        private static string NormalizeAdjustmentStatus(string? status)
        {
            var value = status?.Trim().ToUpperInvariant();
            return value switch
            {
                "APPROVED" => "APPROVED",
                "REJECTED" => "REJECTED",
                _ => throw new InvalidOperationException("Adjustment status must be APPROVED or REJECTED.")
            };
        }

        private static string NormalizeEnrollmentStatus(string? status)
        {
            var value = status?.Trim().ToUpperInvariant();
            return value switch
            {
                "ACTIVE" => "ACTIVE",
                "INACTIVE" => "INACTIVE",
                _ => throw new InvalidOperationException("Enrollment status must be ACTIVE or INACTIVE.")
            };
        }

        private static string NormalizeAssignmentStatus(string? status)
        {
            var value = status?.Trim().ToUpperInvariant();
            return value switch
            {
                "ASSIGNED" => "ASSIGNED",
                "CANCELLED" => "CANCELLED",
                _ => throw new InvalidOperationException("Assignment status must be ASSIGNED or CANCELLED.")
            };
        }

        private static async Task EnsureNoOverlappingShiftAssignmentAsync(
            MySqlConnection connection,
            int employeeId,
            DateTime startDate,
            DateTime? endDate)
        {
            const string sql = @"
SELECT COUNT(*)
FROM shift_assignments
WHERE employee_id = @employee_id
  AND status = 'ASSIGNED'
  AND start_date <> @start_date
  AND start_date <= COALESCE(@end_date, '9999-12-31')
  AND COALESCE(end_date, '9999-12-31') >= @start_date;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@start_date", startDate);
            command.Parameters.AddWithValue("@end_date", endDate.HasValue ? endDate.Value : DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            var overlapCount = result == null || result == DBNull.Value
                ? 0
                : Convert.ToInt32(result, CultureInfo.InvariantCulture);

            if (overlapCount > 0)
            {
                throw new InvalidOperationException("This employee already has an overlapping assigned shift for the selected date range.");
            }
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

        private static int ToInt(object value) =>
            value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);

        private static long ToLong(object value) =>
            value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);

        private static bool IsMissingObjectError(MySqlException ex) =>
            ex.Number == 1109 || ex.Number == 1146 || ex.Number == 1356;

        private static bool IsUnknownColumnError(MySqlException ex) =>
            ex.Number == 1054 ||
            ex.Message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase);
    }
}
