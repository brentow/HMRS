using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record DashboardAttendanceTrendData(string[] Labels, double[] InCounts, double[] OutCounts);
    public record DashboardAttendanceCoverageData(string[] Labels, double[] PresentCounts, double[] OnLeaveCounts, double[] MissingCounts);
    public record DashboardPayrollVolumeData(string[] Labels, double[] RunCounts);
    public record DashboardLeaveMixData(double VacationCount, double SickCount, double OtherCount);
    public record DashboardDevelopmentTrendData(string[] Labels, double[] TrainingCounts, double[] PerformanceCounts, double[] CombinedCounts);

    public class DashboardDataService
    {
        private readonly string _connectionString;

        public DashboardDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var stats = new DashboardStats
            {
                TotalEmployees = await CountSafeAsync(connection, "SELECT COUNT(*) FROM employees;"),
                ActiveEmployees = await CountSafeAsync(connection, "SELECT COUNT(*) FROM employees WHERE status = 'ACTIVE';"),
                Departments = await CountSafeAsync(connection, "SELECT COUNT(*) FROM departments;"),
                Positions = await CountSafeAsync(connection, "SELECT COUNT(*) FROM positions;"),
                PresentToday = await CountWithFallbackAsync(
                    connection,
                    "SELECT COUNT(DISTINCT employee_id) FROM v_dtr_daily_effective WHERE work_date = CURDATE();",
                    "SELECT COUNT(DISTINCT employee_id) FROM attendance_logs WHERE DATE(log_time) = CURDATE() AND log_type = 'IN';"),
                PendingAdjustments = await CountSafeAsync(
                    connection,
                    "SELECT COUNT(*) FROM attendance_adjustments WHERE status = 'PENDING';"),
                PendingLeaves = await CountSafeAsync(
                    connection,
                    "SELECT COUNT(*) FROM leave_applications WHERE status IN ('SUBMITTED','RECOMMENDED');"),
                OpenJobs = await CountSafeAsync(connection, "SELECT COUNT(*) FROM job_postings WHERE status = 'OPEN';"),
                ActiveCourses = await CountWithFallbackAsync(
                    connection,
                    "SELECT COUNT(*) FROM training_sessions WHERE session_date >= CURDATE();",
                    "SELECT COUNT(*) FROM training_courses;"),
                PendingTrainingEnrollments = await CountSafeAsync(
                    connection,
                    "SELECT COUNT(*) FROM training_enrollments WHERE status = 'PENDING';"),
                OpenPayrollPeriods = await CountSafeAsync(connection, "SELECT COUNT(*) FROM payroll_periods WHERE status = 'OPEN';"),
                PayrollReleaseQueue = await CountSafeAsync(
                    connection,
                    @"SELECT COUNT(*)
                      FROM payroll_runs pr
                      LEFT JOIN payslip_releases rel ON rel.payroll_run_id = pr.payroll_run_id
                      WHERE pr.status IN ('GENERATED','APPROVED')
                        AND rel.payslip_release_id IS NULL;"),
                OpenPerformanceCycles = await CountSafeAsync(connection, "SELECT COUNT(*) FROM performance_cycles WHERE status = 'OPEN';"),
                ActiveUsers = await CountSafeAsync(connection, "SELECT COUNT(*) FROM user_accounts WHERE status = 'ACTIVE';"),
                ApplicantsInPipeline = await CountSafeAsync(
                    connection,
                    "SELECT COUNT(*) FROM job_applications WHERE status IN ('SUBMITTED','SCREENING','SHORTLISTED','INTERVIEW','OFFERED');")
            };

            return stats;
        }

        public async Task<DashboardStats> GetEmployeeDashboardStatsAsync(int employeeId)
        {
            if (employeeId <= 0)
            {
                return new DashboardStats();
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var stats = new DashboardStats
            {
                MyPendingAdjustments = await CountSafeWithParamsAsync(
                    connection,
                    "SELECT COUNT(*) FROM attendance_adjustments WHERE employee_id = @employee_id AND status = 'PENDING';",
                    new MySqlParameter("@employee_id", employeeId)),
                MyPendingLeaves = await CountSafeWithParamsAsync(
                    connection,
                    "SELECT COUNT(*) FROM leave_applications WHERE employee_id = @employee_id AND status IN ('SUBMITTED','RECOMMENDED');",
                    new MySqlParameter("@employee_id", employeeId)),
                MyActiveEnrollments = await CountSafeWithParamsAsync(
                    connection,
                    "SELECT COUNT(*) FROM training_enrollments WHERE employee_id = @employee_id AND status IN ('PENDING','COMPLETED');",
                    new MySqlParameter("@employee_id", employeeId)),
                MyOpenReviews = await CountSafeWithParamsAsync(
                    connection,
                    "SELECT COUNT(*) FROM performance_reviews WHERE employee_id = @employee_id AND status IN ('DRAFT','SUBMITTED');",
                    new MySqlParameter("@employee_id", employeeId)),
                MyLatestNetPay = await DecimalSafeWithParamsAsync(
                    connection,
                    @"SELECT net_pay
                      FROM payroll_runs
                      WHERE employee_id = @employee_id
                      ORDER BY generated_at DESC, payroll_run_id DESC
                      LIMIT 1;",
                    new MySqlParameter("@employee_id", employeeId))
            };

            stats.MyPendingRequestsText = BuildPendingRequestsText(
                stats.MyPendingAdjustments,
                stats.MyPendingLeaves);

            try
            {
                await using var command = new MySqlCommand(@"
SELECT
    MIN(CASE WHEN log_type = 'IN' THEN TIME(log_time) END) AS time_in,
    MAX(CASE WHEN log_type = 'OUT' THEN TIME(log_time) END) AS time_out
FROM attendance_logs
WHERE employee_id = @employee_id
  AND DATE(log_time) = CURDATE();", connection);
                command.Parameters.AddWithValue("@employee_id", employeeId);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    stats.TodayInText = FormatTimeValue(reader["time_in"]);
                    stats.TodayOutText = FormatTimeValue(reader["time_out"]);
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // Keep employee dashboard usable even if attendance tables are not available.
            }

            stats.MyNextShiftText = await ResolveNextShiftTextAsync(connection, employeeId);
            await FillLatestDecisionUpdateAsync(connection, stats, employeeId);

            return stats;
        }

        public async Task<DashboardAttendanceTrendData> GetAttendanceTrendAsync(int days = 7)
        {
            var clampedDays = Math.Clamp(days, 2, 14);
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-(clampedDays - 1));

            var labels = Enumerable.Range(0, clampedDays)
                .Select(offset => startDate.AddDays(offset).ToString("ddd", CultureInfo.InvariantCulture))
                .ToArray();
            var inCounts = new double[clampedDays];
            var outCounts = new double[clampedDays];

            const string sql = @"
SELECT DATE(log_time) AS work_date,
       SUM(CASE WHEN log_type = 'IN' THEN 1 ELSE 0 END) AS in_count,
       SUM(CASE WHEN log_type = 'OUT' THEN 1 ELSE 0 END) AS out_count
FROM attendance_logs
WHERE DATE(log_time) BETWEEN @start_date AND @end_date
GROUP BY DATE(log_time)
ORDER BY DATE(log_time);";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@start_date", startDate);
                command.Parameters.AddWithValue("@end_date", endDate);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (reader["work_date"] == DBNull.Value)
                    {
                        continue;
                    }

                    var workDate = Convert.ToDateTime(reader["work_date"], CultureInfo.InvariantCulture).Date;
                    var index = (workDate - startDate.Date).Days;
                    if (index < 0 || index >= clampedDays)
                    {
                        continue;
                    }

                    inCounts[index] = reader["in_count"] == DBNull.Value
                        ? 0
                        : Convert.ToDouble(reader["in_count"], CultureInfo.InvariantCulture);
                    outCounts[index] = reader["out_count"] == DBNull.Value
                        ? 0
                        : Convert.ToDouble(reader["out_count"], CultureInfo.InvariantCulture);
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // Module table is not ready yet. Return safe zeroes.
            }

            return new DashboardAttendanceTrendData(labels, inCounts, outCounts);
        }

        public async Task<DashboardAttendanceCoverageData> GetAttendanceCoverageAsync(int days = 14)
        {
            var clampedDays = Math.Clamp(days, 7, 31);
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-(clampedDays - 1));

            var labels = Enumerable.Range(0, clampedDays)
                .Select(offset => startDate.AddDays(offset).ToString("dd MMM", CultureInfo.InvariantCulture))
                .ToArray();

            var present = new double[clampedDays];
            var onLeave = new double[clampedDays];
            var missing = new double[clampedDays];

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var activeEmployees = await CountSafeAsync(
                connection,
                "SELECT COUNT(*) FROM employees WHERE status = 'ACTIVE';");

            const string presentSql = @"
SELECT DATE(log_time) AS work_date,
       COUNT(DISTINCT employee_id) AS present_count
FROM attendance_logs
WHERE DATE(log_time) BETWEEN @start_date AND @end_date
  AND log_type = 'IN'
GROUP BY DATE(log_time)
ORDER BY DATE(log_time);";

            const string leaveSql = @"
WITH RECURSIVE dates AS (
    SELECT @start_date AS work_date
    UNION ALL
    SELECT DATE_ADD(work_date, INTERVAL 1 DAY)
    FROM dates
    WHERE work_date < @end_date
)
SELECT d.work_date,
       COUNT(DISTINCT la.employee_id) AS leave_count
FROM dates d
LEFT JOIN leave_applications la
    ON la.status = 'APPROVED'
   AND d.work_date BETWEEN la.date_from AND la.date_to
GROUP BY d.work_date
ORDER BY d.work_date;";

            try
            {
                await using (var presentCommand = new MySqlCommand(presentSql, connection))
                {
                    presentCommand.Parameters.AddWithValue("@start_date", startDate);
                    presentCommand.Parameters.AddWithValue("@end_date", endDate);

                    await using var reader = await presentCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (reader["work_date"] == DBNull.Value)
                        {
                            continue;
                        }

                        var workDate = Convert.ToDateTime(reader["work_date"], CultureInfo.InvariantCulture).Date;
                        var index = (workDate - startDate.Date).Days;
                        if (index < 0 || index >= clampedDays)
                        {
                            continue;
                        }

                        present[index] = reader["present_count"] == DBNull.Value
                            ? 0
                            : Convert.ToDouble(reader["present_count"], CultureInfo.InvariantCulture);
                    }
                }

                await using (var leaveCommand = new MySqlCommand(leaveSql, connection))
                {
                    leaveCommand.Parameters.AddWithValue("@start_date", startDate);
                    leaveCommand.Parameters.AddWithValue("@end_date", endDate);

                    await using var reader = await leaveCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (reader["work_date"] == DBNull.Value)
                        {
                            continue;
                        }

                        var workDate = Convert.ToDateTime(reader["work_date"], CultureInfo.InvariantCulture).Date;
                        var index = (workDate - startDate.Date).Days;
                        if (index < 0 || index >= clampedDays)
                        {
                            continue;
                        }

                        onLeave[index] = reader["leave_count"] == DBNull.Value
                            ? 0
                            : Convert.ToDouble(reader["leave_count"], CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // Keep dashboard resilient when leave/attendance modules are unavailable.
            }

            for (var i = 0; i < clampedDays; i++)
            {
                missing[i] = Math.Max(0, activeEmployees - present[i] - onLeave[i]);
            }

            return new DashboardAttendanceCoverageData(labels, present, onLeave, missing);
        }

        public async Task<DashboardPayrollVolumeData> GetPayrollVolumeAsync(int months = 7)
        {
            var clampedMonths = Math.Clamp(months, 3, 12);
            var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var startMonth = thisMonth.AddMonths(-(clampedMonths - 1));

            var monthLabels = Enumerable.Range(0, clampedMonths)
                .Select(offset => startMonth.AddMonths(offset))
                .ToArray();
            var labels = monthLabels.Select(m => m.ToString("MMM", CultureInfo.InvariantCulture)).ToArray();
            var runCounts = new double[clampedMonths];

            const string sql = @"
SELECT DATE_FORMAT(pp.pay_date, '%Y-%m-01') AS month_start,
       COUNT(pr.payroll_run_id) AS run_count
FROM payroll_runs pr
INNER JOIN payroll_periods pp ON pp.payroll_period_id = pr.payroll_period_id
WHERE pp.pay_date >= @start_month
GROUP BY DATE_FORMAT(pp.pay_date, '%Y-%m-01')
ORDER BY month_start;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@start_month", startMonth);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var monthText = reader["month_start"]?.ToString();
                    if (string.IsNullOrWhiteSpace(monthText) ||
                        !DateTime.TryParseExact(monthText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthStart))
                    {
                        continue;
                    }

                    var index = ((monthStart.Year - startMonth.Year) * 12) + monthStart.Month - startMonth.Month;
                    if (index < 0 || index >= clampedMonths)
                    {
                        continue;
                    }

                    runCounts[index] = reader["run_count"] == DBNull.Value
                        ? 0
                        : Convert.ToDouble(reader["run_count"], CultureInfo.InvariantCulture);
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // Module table is not ready yet. Return safe zeroes.
            }

            return new DashboardPayrollVolumeData(labels, runCounts);
        }

        public async Task<DashboardLeaveMixData> GetLeaveMixAsync()
        {
            const string sql = @"
SELECT
    COALESCE(SUM(CASE WHEN lt.code = 'VL' AND la.status = 'APPROVED' THEN 1 ELSE 0 END), 0) AS vacation_count,
    COALESCE(SUM(CASE WHEN lt.code = 'SL' AND la.status = 'APPROVED' THEN 1 ELSE 0 END), 0) AS sick_count,
    COALESCE(SUM(CASE WHEN (lt.code IS NULL OR lt.code NOT IN ('VL', 'SL')) AND la.status = 'APPROVED' THEN 1 ELSE 0 END), 0) AS other_count
FROM leave_applications la
LEFT JOIN leave_types lt ON lt.leave_type_id = la.leave_type_id;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new DashboardLeaveMixData(
                        VacationCount: reader["vacation_count"] == DBNull.Value ? 0 : Convert.ToDouble(reader["vacation_count"], CultureInfo.InvariantCulture),
                        SickCount: reader["sick_count"] == DBNull.Value ? 0 : Convert.ToDouble(reader["sick_count"], CultureInfo.InvariantCulture),
                        OtherCount: reader["other_count"] == DBNull.Value ? 0 : Convert.ToDouble(reader["other_count"], CultureInfo.InvariantCulture));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // Module table is not ready yet. Return safe zeroes.
            }

            return new DashboardLeaveMixData(0, 0, 0);
        }

        public async Task<DashboardDevelopmentTrendData> GetDevelopmentTrendAsync(int months = 7)
        {
            var clampedMonths = Math.Clamp(months, 3, 12);
            var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var startMonth = thisMonth.AddMonths(-(clampedMonths - 1));

            var labels = Enumerable.Range(0, clampedMonths)
                .Select(offset => startMonth.AddMonths(offset).ToString("MMM", CultureInfo.InvariantCulture))
                .ToArray();
            var training = new double[clampedMonths];
            var performance = new double[clampedMonths];

            const string trainingSql = @"
SELECT DATE_FORMAT(ts.session_date, '%Y-%m-01') AS month_start,
       COUNT(te.enrollment_id) AS training_count
FROM training_enrollments te
INNER JOIN training_sessions ts ON ts.session_id = te.session_id
WHERE ts.session_date >= @start_month
GROUP BY DATE_FORMAT(ts.session_date, '%Y-%m-01')
ORDER BY month_start;";

            const string performanceSql = @"
SELECT DATE_FORMAT(COALESCE(pr.submitted_at, pr.created_at), '%Y-%m-01') AS month_start,
       COUNT(pr.performance_review_id) AS performance_count
FROM performance_reviews pr
WHERE COALESCE(pr.submitted_at, pr.created_at) >= @start_month
GROUP BY DATE_FORMAT(COALESCE(pr.submitted_at, pr.created_at), '%Y-%m-01')
ORDER BY month_start;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await FillMonthlySeriesAsync(connection, trainingSql, "@start_month", startMonth, clampedMonths, startMonth, training, "training_count");
                await FillMonthlySeriesAsync(connection, performanceSql, "@start_month", startMonth, clampedMonths, startMonth, performance, "performance_count");
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // Module table is not ready yet. Return safe zeroes.
            }

            var combined = training.Zip(performance, (t, p) => t + p).ToArray();
            return new DashboardDevelopmentTrendData(labels, training, performance, combined);
        }

        private static async Task<long> CountWithFallbackAsync(MySqlConnection connection, string primarySql, string fallbackSql)
        {
            var primary = await TryCountAsync(connection, primarySql);
            if (primary.HasValue)
            {
                return primary.Value;
            }

            return await CountSafeAsync(connection, fallbackSql);
        }

        private static async Task<long> CountSafeWithParamsAsync(
            MySqlConnection connection,
            string sql,
            params MySqlParameter[] parameters)
        {
            try
            {
                await using var command = new MySqlCommand(sql, connection);
                if (parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                var value = await command.ExecuteScalarAsync();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return 0;
            }
        }

        private static async Task<decimal> DecimalSafeWithParamsAsync(
            MySqlConnection connection,
            string sql,
            params MySqlParameter[] parameters)
        {
            try
            {
                await using var command = new MySqlCommand(sql, connection);
                if (parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                var value = await command.ExecuteScalarAsync();
                return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return 0m;
            }
        }

        private static string FormatTimeValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "-";
            }

            if (value is TimeSpan span)
            {
                return DateTime.Today.Add(span).ToString("hh:mm tt", CultureInfo.InvariantCulture);
            }

            if (value is DateTime dt)
            {
                return dt.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            }

            if (TimeSpan.TryParse(value.ToString(), out var parsedSpan))
            {
                return DateTime.Today.Add(parsedSpan).ToString("hh:mm tt", CultureInfo.InvariantCulture);
            }

            return "-";
        }

        private static async Task<long> CountSafeAsync(MySqlConnection connection, string sql)
        {
            var result = await TryCountAsync(connection, sql);
            return result ?? 0;
        }

        private static async Task<long?> TryCountAsync(MySqlConnection connection, string sql)
        {
            try
            {
                await using var command = new MySqlCommand(sql, connection);
                var value = await command.ExecuteScalarAsync();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // The module table/view is not installed yet; keep dashboard resilient.
                return null;
            }
        }

        private static async Task FillMonthlySeriesAsync(
            MySqlConnection connection,
            string sql,
            string startMonthParamName,
            DateTime startMonthValue,
            int monthsCount,
            DateTime monthAnchor,
            IList<double> target,
            string valueColumn)
        {
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue(startMonthParamName, startMonthValue);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var monthText = reader["month_start"]?.ToString();
                if (string.IsNullOrWhiteSpace(monthText) ||
                    !DateTime.TryParseExact(monthText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthStart))
                {
                    continue;
                }

                var index = ((monthStart.Year - monthAnchor.Year) * 12) + monthStart.Month - monthAnchor.Month;
                if (index < 0 || index >= monthsCount)
                {
                    continue;
                }

                target[index] = reader[valueColumn] == DBNull.Value
                    ? 0
                    : Convert.ToDouble(reader[valueColumn], CultureInfo.InvariantCulture);
            }

            await reader.CloseAsync();
        }

        private static bool IsMissingObjectError(MySqlException ex) =>
            ex.Number == 1109 || // Unknown table
            ex.Number == 1146 || // Table doesn't exist
            ex.Number == 1356;   // View doesn't exist / invalid

        private static string BuildPendingRequestsText(long pendingAdjustments, long pendingLeaves)
        {
            if (pendingAdjustments <= 0 && pendingLeaves <= 0)
            {
                return "No pending leave or adjustment requests.";
            }

            return $"Pending: {pendingAdjustments} adjustment(s), {pendingLeaves} leave request(s).";
        }

        private static string BuildShiftText(
            string shiftName,
            object? startTime,
            object? endTime,
            DateTime? datePrefix = null)
        {
            var safeName = string.IsNullOrWhiteSpace(shiftName) ? "Shift" : shiftName.Trim();
            var startText = FormatTimeValue(startTime ?? DBNull.Value);
            var endText = FormatTimeValue(endTime ?? DBNull.Value);
            var schedule = startText == "-" && endText == "-"
                ? safeName
                : $"{safeName} ({startText} - {endText})";

            if (datePrefix.HasValue)
            {
                return $"Next: {datePrefix.Value:MMM dd, yyyy} {schedule}";
            }

            return $"Today: {schedule}";
        }

        private async Task<string> ResolveNextShiftTextAsync(MySqlConnection connection, int employeeId)
        {
            const string currentShiftSql = @"
SELECT s.shift_name, s.start_time, s.end_time
FROM shift_assignments sa
INNER JOIN shifts s ON s.shift_id = sa.shift_id
WHERE sa.employee_id = @employee_id
  AND sa.status = 'ASSIGNED'
  AND sa.start_date <= CURDATE()
  AND (sa.end_date IS NULL OR sa.end_date >= CURDATE())
ORDER BY sa.start_date DESC, sa.assignment_id DESC
LIMIT 1;";

            const string nextShiftSql = @"
SELECT s.shift_name, s.start_time, s.end_time, sa.start_date
FROM shift_assignments sa
INNER JOIN shifts s ON s.shift_id = sa.shift_id
WHERE sa.employee_id = @employee_id
  AND sa.status = 'ASSIGNED'
  AND sa.start_date > CURDATE()
ORDER BY sa.start_date ASC, sa.assignment_id ASC
LIMIT 1;";

            try
            {
                await using (var currentShiftCommand = new MySqlCommand(currentShiftSql, connection))
                {
                    currentShiftCommand.Parameters.AddWithValue("@employee_id", employeeId);
                    await using var currentReader = await currentShiftCommand.ExecuteReaderAsync();
                    if (await currentReader.ReadAsync())
                    {
                        return BuildShiftText(
                            currentReader["shift_name"]?.ToString() ?? "Shift",
                            currentReader["start_time"],
                            currentReader["end_time"]);
                    }
                }

                await using (var nextShiftCommand = new MySqlCommand(nextShiftSql, connection))
                {
                    nextShiftCommand.Parameters.AddWithValue("@employee_id", employeeId);
                    await using var nextReader = await nextShiftCommand.ExecuteReaderAsync();
                    if (await nextReader.ReadAsync())
                    {
                        var startDate = nextReader["start_date"] == DBNull.Value
                            ? (DateTime?)null
                            : Convert.ToDateTime(nextReader["start_date"], CultureInfo.InvariantCulture);

                        return BuildShiftText(
                            nextReader["shift_name"]?.ToString() ?? "Shift",
                            nextReader["start_time"],
                            nextReader["end_time"],
                            startDate);
                    }
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return "No shift assigned.";
            }

            return "No shift assigned.";
        }

        private async Task FillLatestDecisionUpdateAsync(
            MySqlConnection connection,
            DashboardStats stats,
            int employeeId)
        {
            const string sql = @"
SELECT module_name, status, event_at, note
FROM (
    SELECT
        'ADJUSTMENT' AS module_name,
        aa.status,
        COALESCE(aa.decided_at, aa.requested_at) AS event_at,
        COALESCE(NULLIF(aa.reason, ''), '-') AS note
    FROM attendance_adjustments aa
    WHERE aa.employee_id = @employee_id
      AND aa.status IN ('APPROVED', 'REJECTED')

    UNION ALL

    SELECT
        'LEAVE' AS module_name,
        la.status,
        COALESCE(la.decision_at, la.filed_at) AS event_at,
        COALESCE(NULLIF(la.decision_remarks, ''), NULLIF(la.reason, ''), '-') AS note
    FROM leave_applications la
    WHERE la.employee_id = @employee_id
      AND la.status IN ('RECOMMENDED', 'APPROVED', 'REJECTED')
) updates
ORDER BY event_at DESC
LIMIT 1;";

            try
            {
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId);
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return;
                }

                var module = reader["module_name"]?.ToString()?.Trim().ToUpperInvariant() ?? "NONE";
                var status = reader["status"]?.ToString()?.Trim().ToUpperInvariant() ?? "UPDATED";
                var eventAt = reader["event_at"] == DBNull.Value
                    ? DateTime.MinValue
                    : Convert.ToDateTime(reader["event_at"], CultureInfo.InvariantCulture);
                var note = reader["note"]?.ToString()?.Trim() ?? "-";
                if (note.Length > 80)
                {
                    note = $"{note[..80]}...";
                }

                var moduleLabel = module == "LEAVE" ? "Leave" : "Adjustment";
                var dateLabel = eventAt == DateTime.MinValue
                    ? "recently"
                    : eventAt.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

                stats.MyLatestDecisionModule = module;
                stats.MyLatestDecisionText = $"{moduleLabel} {status} on {dateLabel}: {note}";
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                // Keep default fallback text.
            }
        }
    }
}
