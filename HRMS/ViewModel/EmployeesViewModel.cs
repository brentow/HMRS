using HRMS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public class EmployeesViewModel : INotifyPropertyChanged
    {
        private readonly EmployeeDataService _dataService = new(DbConfig.ConnectionString);
        private readonly EmployeeSelfService _employeeSelfService = new(DbConfig.ConnectionString);

        private int _totalEmployees;
        private int _activeEmployees;
        private int _departments;
        private int _positions;
        private string _searchText = string.Empty;
        private EmployeeRowVm? _selectedEmployee;
        private string _selectedEmployeeAttendanceSummary = "Select an employee to view attendance.";
        private bool _isLoadingSelectedEmployeeAttendance;
        private bool _isEmployeeMode;
        private int? _currentEmployeeId;
        private int _loadRequestVersion;

        public int TotalEmployees { get => _totalEmployees; set { _totalEmployees = value; OnPropertyChanged(); } }
        public int ActiveEmployees { get => _activeEmployees; set { _activeEmployees = value; OnPropertyChanged(); } }
        public int Departments { get => _departments; set { _departments = value; OnPropertyChanged(); } }
        public int Positions { get => _positions; set { _positions = value; OnPropertyChanged(); } }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                RefreshEmployeeSearchResults();
                EmployeesView.Refresh();
            }
        }

        public EmployeeRowVm? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (ReferenceEquals(_selectedEmployee, value))
                {
                    return;
                }

                _selectedEmployee = value;
                OnPropertyChanged();
                _ = LoadSelectedEmployeeAttendanceAsync(value);
            }
        }

        public string SelectedEmployeeAttendanceSummary
        {
            get => _selectedEmployeeAttendanceSummary;
            private set
            {
                if (_selectedEmployeeAttendanceSummary == value)
                {
                    return;
                }

                _selectedEmployeeAttendanceSummary = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoadingSelectedEmployeeAttendance
        {
            get => _isLoadingSelectedEmployeeAttendance;
            private set
            {
                if (_isLoadingSelectedEmployeeAttendance == value)
                {
                    return;
                }

                _isLoadingSelectedEmployeeAttendance = value;
                OnPropertyChanged();
            }
        }

        public bool IsEmployeeMode
        {
            get => _isEmployeeMode;
            private set
            {
                if (_isEmployeeMode == value)
                {
                    return;
                }

                _isEmployeeMode = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<EmployeeRowVm> Employees { get; } = new();
        public ObservableCollection<EmployeeRowVm> EmployeeSearchResults { get; } = new();
        public ObservableCollection<EmployeeAttendanceLogVm> SelectedEmployeeRecentLogs { get; } = new();
        public ObservableCollection<EmployeeAttendanceDayVm> SelectedEmployeeCurrentMonthAttendance { get; } = new();
        public ICollectionView EmployeesView { get; }

        public EmployeesViewModel()
        {
            EmployeesView = CollectionViewSource.GetDefaultView(Employees);
            EmployeesView.Filter = FilterEmployee;
            _ = RefreshAsync();
        }

        public Task RefreshAsync() => LoadAsync();

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            _ = ApplyCurrentUserScopeAsync(user);
        }

        private async Task LoadAsync()
        {
            var requestVersion = Interlocked.Increment(ref _loadRequestVersion);
            var isEmployeeMode = IsEmployeeMode;
            var scopedEmployeeId = isEmployeeMode ? _currentEmployeeId : null;
            var selectedEmployeeNo = SelectedEmployee?.EmployeeNo;

            var list = isEmployeeMode && (!scopedEmployeeId.HasValue || scopedEmployeeId.Value <= 0)
                ? Array.Empty<EmployeeRowDto>()
                : await _dataService.GetRecentEmployeesAsync(scopedEmployeeId: scopedEmployeeId);

            if (requestVersion != _loadRequestVersion)
            {
                return;
            }

            Employees.Clear();
            foreach (var e in list)
            {
                Employees.Add(new EmployeeRowVm(
                    e.EmployeeId,
                    e.EmployeeNo,
                    e.Name,
                    e.Department,
                    e.Position,
                    e.HireDate,
                    e.Status,
                    e.AppointmentType,
                    e.SalaryGrade,
                    e.SalaryStep,
                    e.MonthlySalary,
                    e.TinNo,
                    e.GsisBpNo,
                    e.PhilHealthNo,
                    e.PagibigMidNo,
                    e.LastDtrDate,
                    e.LastTimeIn,
                    e.LastTimeOut,
                    e.LastWorkedMinutes,
                    e.CurrentMonthWorkedDays,
                    e.CurrentMonthWorkedMinutes,
                    e.CurrentMonthLastWorkDate,
                    e.LatestPayrollPeriodCode,
                    e.LatestPayrollStatus,
                    e.LatestPayrollGeneratedAt,
                    e.LatestPayrollBasicPay,
                    e.LatestPayrollAllowances,
                    e.LatestPayrollOvertimePay,
                    e.LatestPayrollOtherEarnings,
                    e.LatestPayrollGrossPay,
                    e.LatestPayrollDeductionsTotal,
                    e.LatestPayrollNetPay,
                    e.LatestPayrollDeductionsSummary
                )
                {
                    StatusColor = e.Status.ToLower() == "active"
                        ? new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E4368"))
                        : new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935"))
                });
            }

            if (isEmployeeMode)
            {
                TotalEmployees = Employees.Count;
                ActiveEmployees = Employees.Count(e =>
                    string.Equals(e.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase));
                Departments = Employees
                    .Select(e => e.Department)
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x != "-")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                Positions = Employees
                    .Select(e => e.Position)
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x != "-")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
            }
            else
            {
                var stats = await _dataService.GetStatsAsync();
                if (requestVersion != _loadRequestVersion)
                {
                    return;
                }

                TotalEmployees = stats.TotalEmployees;
                ActiveEmployees = stats.ActiveEmployees;
                Departments = stats.Departments;
                Positions = stats.Positions;
            }

            if (requestVersion != _loadRequestVersion)
            {
                return;
            }

            EmployeesView.Refresh();
            RefreshEmployeeSearchResults();
            if (!string.IsNullOrWhiteSpace(selectedEmployeeNo))
            {
                SelectEmployeeByNumber(selectedEmployeeNo);
            }

            if (SelectedEmployee == null || !EmployeesView.Cast<EmployeeRowVm>().Any(e => ReferenceEquals(e, SelectedEmployee)))
            {
                SelectedEmployee = EmployeesView.Cast<EmployeeRowVm>().FirstOrDefault();
            }
        }

        private async Task ApplyCurrentUserScopeAsync(AuthenticatedUser? user)
        {
            var isEmployee = string.Equals(user?.RoleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);
            var employeeId = isEmployee ? user?.EmployeeId : null;

            if (isEmployee && (!employeeId.HasValue || employeeId.Value <= 0) && user?.UserId > 0)
            {
                employeeId = await _employeeSelfService.GetEmployeeIdByUserIdAsync(user.UserId);
            }

            var hasScopeChanged = IsEmployeeMode != isEmployee || _currentEmployeeId != employeeId;

            IsEmployeeMode = isEmployee;
            _currentEmployeeId = employeeId;

            if (hasScopeChanged)
            {
                await RefreshAsync();
            }
        }

        private async Task LoadSelectedEmployeeAttendanceAsync(EmployeeRowVm? employee)
        {
            SelectedEmployeeRecentLogs.Clear();
            SelectedEmployeeCurrentMonthAttendance.Clear();

            if (employee == null || string.IsNullOrWhiteSpace(employee.EmployeeNo))
            {
                SelectedEmployeeAttendanceSummary = "Select an employee to view attendance.";
                return;
            }

            IsLoadingSelectedEmployeeAttendance = true;
            try
            {
                var recentLogsTask = _dataService.GetEmployeeRecentAttendanceLogsAsync(employee.EmployeeNo, 24);
                var monthlyAttendanceTask = _dataService.GetEmployeeCurrentMonthAttendanceAsync(employee.EmployeeNo);

                var recentLogs = await recentLogsTask;
                var monthlyAttendance = await monthlyAttendanceTask;

                foreach (var log in recentLogs)
                {
                    SelectedEmployeeRecentLogs.Add(new EmployeeAttendanceLogVm(
                        log.LogTime,
                        log.LogType,
                        log.Source,
                        log.DeviceName));
                }

                foreach (var day in monthlyAttendance)
                {
                    SelectedEmployeeCurrentMonthAttendance.Add(new EmployeeAttendanceDayVm(
                        day.WorkDate,
                        day.TimeIn,
                        day.TimeOut,
                        day.WorkedMinutes,
                        day.Remarks,
                        day.LateMinutes,
                        day.EarlyOutMinutes));
                }

                SelectedEmployeeAttendanceSummary = SelectedEmployeeCurrentMonthAttendance.Count == 0
                    ? "No current-month DTR rows found for this employee."
                    : $"{SelectedEmployeeCurrentMonthAttendance.Count} DTR day(s) loaded for {DateTime.Today:MMMM yyyy}.";
            }
            catch
            {
                SelectedEmployeeAttendanceSummary = "Unable to load employee attendance.";
            }
            finally
            {
                IsLoadingSelectedEmployeeAttendance = false;
            }
        }

        private bool FilterEmployee(object obj)
        {
            if (obj is not EmployeeRowVm employee)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            var term = SearchText.Trim();

            return ScoreEmployeeSearch(employee, term) > 0;
        }

        private void RefreshEmployeeSearchResults()
        {
            EmployeeSearchResults.Clear();

            var term = SearchText.Trim();
            if (string.IsNullOrWhiteSpace(term))
            {
                return;
            }

            var matches = Employees
                .Select(employee => new
                {
                    Employee = employee,
                    Score = ScoreEmployeeSearch(employee, term)
                })
                .Where(match => match.Score > 0)
                .OrderByDescending(match => match.Score)
                .ThenBy(match => match.Employee.Name, StringComparer.OrdinalIgnoreCase)
                .Take(8);

            foreach (var match in matches)
            {
                EmployeeSearchResults.Add(match.Employee);
            }
        }

        private static int ScoreEmployeeSearch(EmployeeRowVm employee, string term)
        {
            var normalizedTerm = NormalizeSearchText(term);
            if (string.IsNullOrWhiteSpace(normalizedTerm))
            {
                return 0;
            }

            var tokens = SplitSearchTokens(normalizedTerm);
            var name = NormalizeSearchText(employee.Name);
            var employeeNo = NormalizeSearchText(employee.EmployeeNo);
            var detailText = NormalizeSearchText(string.Join(
                " ",
                employee.Department,
                employee.Position,
                employee.Status,
                employee.AppointmentType,
                employee.SalaryGrade,
                employee.SalaryStep,
                employee.TinNo,
                employee.GsisBpNo,
                employee.PhilHealthNo,
                employee.PagibigMidNo,
                employee.HireDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)));

            var nameTokens = SplitSearchTokens(name);
            var detailTokens = SplitSearchTokens(detailText);
            var score = 0;

            if (employeeNo == normalizedTerm)
            {
                score = Math.Max(score, 1000);
            }
            else if (employeeNo.Contains(normalizedTerm, StringComparison.Ordinal))
            {
                score = Math.Max(score, 850);
            }

            if (name == normalizedTerm)
            {
                score = Math.Max(score, 950);
            }
            else if (name.StartsWith(normalizedTerm, StringComparison.Ordinal))
            {
                score = Math.Max(score, 900);
            }
            else if (name.Contains(normalizedTerm, StringComparison.Ordinal))
            {
                score = Math.Max(score, 820);
            }

            var nameTokenMatches = tokens.Count(token => TokenMatches(nameTokens, token));
            if (tokens.Length > 0 && nameTokenMatches == tokens.Length)
            {
                score = Math.Max(score, 760 + (nameTokenMatches * 10));
            }
            else if (nameTokenMatches > 0)
            {
                score = Math.Max(score, 360 + (nameTokenMatches * 10));
            }

            if (detailText.Contains(normalizedTerm, StringComparison.Ordinal))
            {
                score = Math.Max(score, 430);
            }

            var detailTokenMatches = tokens.Count(token => TokenMatches(detailTokens, token));
            if (tokens.Length > 0 && detailTokenMatches == tokens.Length)
            {
                score = Math.Max(score, 380 + (detailTokenMatches * 10));
            }
            else if (detailTokenMatches > 0)
            {
                score = Math.Max(score, 180 + (detailTokenMatches * 10));
            }

            return score;
        }

        private static string NormalizeSearchText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = new string(text
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
                .ToArray());

            return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static string[] SplitSearchTokens(string text) =>
            text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        private static bool TokenMatches(IReadOnlyList<string> candidates, string token)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Contains(token, StringComparison.Ordinal) ||
                    token.Contains(candidate, StringComparison.Ordinal))
                {
                    return true;
                }

                if (token.Length >= 3 &&
                    candidate.Length >= 3 &&
                    LevenshteinDistance(candidate, token) <= GetAllowedSearchDistance(token.Length))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetAllowedSearchDistance(int length) =>
            length <= 4 ? 1 : 2;

        private static int LevenshteinDistance(string left, string right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left.Length == 0)
            {
                return right.Length;
            }

            if (right.Length == 0)
            {
                return left.Length;
            }

            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];

            for (var j = 0; j <= right.Length; j++)
            {
                previous[j] = j;
            }

            for (var i = 1; i <= left.Length; i++)
            {
                current[0] = i;

                for (var j = 1; j <= right.Length; j++)
                {
                    var substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                    current[j] = Math.Min(
                        Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + substitutionCost);
                }

                (previous, current) = (current, previous);
            }

            return previous[right.Length];
        }

        public void SelectEmployee(EmployeeRowVm? employee)
        {
            if (employee == null)
            {
                return;
            }

            SelectedEmployee = employee;
            EmployeesView.MoveCurrentTo(employee);
        }

        public void SelectEmployeeByNumber(string? employeeNo)
        {
            if (string.IsNullOrWhiteSpace(employeeNo))
            {
                return;
            }

            var match = Employees.FirstOrDefault(e =>
                string.Equals(e.EmployeeNo, employeeNo, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                return;
            }

            SelectEmployee(match);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class EmployeeAttendanceLogVm
    {
        public EmployeeAttendanceLogVm(DateTime logTime, string logType, string source, string deviceName)
        {
            LogTime = logTime;
            LogType = string.IsNullOrWhiteSpace(logType) ? "-" : logType.Trim();
            Source = string.IsNullOrWhiteSpace(source) ? "-" : source.Trim();
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "-" : deviceName.Trim();
        }

        public DateTime LogTime { get; }
        public string LogType { get; }
        public string Source { get; }
        public string DeviceName { get; }
        public string LogDateText => LogTime == DateTime.MinValue ? "-" : LogTime.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string LogTimeText => LogTime == DateTime.MinValue ? "-" : LogTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
    }

    public class EmployeeAttendanceDayVm
    {
        public EmployeeAttendanceDayVm(
            DateTime workDate,
            TimeSpan? timeIn,
            TimeSpan? timeOut,
            int workedMinutes,
            string remarks,
            int lateMinutes,
            int earlyOutMinutes)
        {
            WorkDate = workDate;
            TimeIn = timeIn;
            TimeOut = timeOut;
            WorkedMinutes = workedMinutes;
            Remarks = string.IsNullOrWhiteSpace(remarks) ? "-" : remarks.Trim();
            LateMinutes = lateMinutes;
            EarlyOutMinutes = earlyOutMinutes;
        }

        public DateTime WorkDate { get; }
        public TimeSpan? TimeIn { get; }
        public TimeSpan? TimeOut { get; }
        public int WorkedMinutes { get; }
        public string Remarks { get; }
        public int LateMinutes { get; }
        public int EarlyOutMinutes { get; }

        public string WorkDateText => WorkDate == DateTime.MinValue ? "-" : WorkDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string DayName => WorkDate == DateTime.MinValue ? "-" : WorkDate.ToString("ddd", CultureInfo.InvariantCulture);
        public string TimeInText => TimeIn.HasValue ? DateTime.Today.Add(TimeIn.Value).ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string TimeOutText => TimeOut.HasValue ? DateTime.Today.Add(TimeOut.Value).ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string WorkedHoursText => FormatMinutes(WorkedMinutes);
        public string AttendanceFlag => LateMinutes > 0 && EarlyOutMinutes > 0
            ? $"Late {LateMinutes}m / Early {EarlyOutMinutes}m"
            : LateMinutes > 0
                ? $"Late {LateMinutes}m"
                : EarlyOutMinutes > 0
                    ? $"Early out {EarlyOutMinutes}m"
                    : "On time";

        private static string FormatMinutes(int workedMinutes)
        {
            if (workedMinutes <= 0)
            {
                return "0h 0m";
            }

            var hours = workedMinutes / 60;
            var minutes = workedMinutes % 60;
            return $"{hours}h {minutes}m";
        }
    }

    public class EmployeeRowVm
    {
        public EmployeeRowVm(
            int employeeId,
            string employeeNo,
            string name,
            string department,
            string position,
            DateTime hireDate,
            string status,
            string appointmentType,
            string salaryGrade,
            string salaryStep,
            decimal monthlySalary,
            string tinNo,
            string gsisBpNo,
            string philHealthNo,
            string pagibigMidNo,
            DateTime? lastDtrDate,
            TimeSpan? lastTimeIn,
            TimeSpan? lastTimeOut,
            int lastWorkedMinutes,
            int currentMonthWorkedDays,
            int currentMonthWorkedMinutes,
            DateTime? currentMonthLastWorkDate,
            string latestPayrollPeriodCode,
            string latestPayrollStatus,
            DateTime? latestPayrollGeneratedAt,
            decimal latestPayrollBasicPay,
            decimal latestPayrollAllowances,
            decimal latestPayrollOvertimePay,
            decimal latestPayrollOtherEarnings,
            decimal latestPayrollGrossPay,
            decimal latestPayrollDeductionsTotal,
            decimal latestPayrollNetPay,
            string latestPayrollDeductionsSummary)
        {
            EmployeeId = employeeId;
            EmployeeNo = employeeNo;
            Name = name;
            Department = department;
            Position = position;
            HireDate = hireDate;
            Status = status;
            AppointmentType = string.IsNullOrWhiteSpace(appointmentType) ? "-" : appointmentType;
            SalaryGrade = string.IsNullOrWhiteSpace(salaryGrade) ? "-" : salaryGrade;
            SalaryStep = string.IsNullOrWhiteSpace(salaryStep) ? "-" : salaryStep;
            MonthlySalary = monthlySalary.ToString("N2", CultureInfo.InvariantCulture);
            TinNo = string.IsNullOrWhiteSpace(tinNo) ? "-" : tinNo;
            GsisBpNo = string.IsNullOrWhiteSpace(gsisBpNo) ? "-" : gsisBpNo;
            PhilHealthNo = string.IsNullOrWhiteSpace(philHealthNo) ? "-" : philHealthNo;
            PagibigMidNo = string.IsNullOrWhiteSpace(pagibigMidNo) ? "-" : pagibigMidNo;
            LastDtrDate = lastDtrDate;
            LastTimeIn = lastTimeIn;
            LastTimeOut = lastTimeOut;
            LastWorkedMinutes = lastWorkedMinutes;
            CurrentMonthWorkedDays = currentMonthWorkedDays;
            CurrentMonthWorkedMinutes = currentMonthWorkedMinutes;
            CurrentMonthLastWorkDate = currentMonthLastWorkDate;
            LatestPayrollPeriodCode = string.IsNullOrWhiteSpace(latestPayrollPeriodCode) ? "-" : latestPayrollPeriodCode;
            LatestPayrollStatus = string.IsNullOrWhiteSpace(latestPayrollStatus) ? "-" : latestPayrollStatus;
            LatestPayrollGeneratedAt = latestPayrollGeneratedAt;
            LatestPayrollBasicPay = latestPayrollBasicPay;
            LatestPayrollAllowances = latestPayrollAllowances;
            LatestPayrollOvertimePay = latestPayrollOvertimePay;
            LatestPayrollOtherEarnings = latestPayrollOtherEarnings;
            LatestPayrollGrossPay = latestPayrollGrossPay;
            LatestPayrollDeductionsTotal = latestPayrollDeductionsTotal;
            LatestPayrollNetPay = latestPayrollNetPay;
            LatestPayrollDeductionsSummary = string.IsNullOrWhiteSpace(latestPayrollDeductionsSummary)
                ? (LatestPayrollPeriodCode == "-" ? "No payroll run generated yet." : "No deduction line items generated yet.")
                : latestPayrollDeductionsSummary;
            LatestPayrollDeductionLines = BuildPayrollDeductionLines(LatestPayrollDeductionsSummary);
        }

        public int EmployeeId { get; }
        public string EmployeeNo { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }
        public System.DateTime HireDate { get; set; }
        public string Status { get; set; }
        public string AppointmentType { get; set; }
        public string SalaryGrade { get; set; }
        public string SalaryStep { get; set; }
        public string MonthlySalary { get; set; }
        public string TinNo { get; set; }
        public string GsisBpNo { get; set; }
        public string PhilHealthNo { get; set; }
        public string PagibigMidNo { get; set; }
        public DateTime? LastDtrDate { get; set; }
        public TimeSpan? LastTimeIn { get; set; }
        public TimeSpan? LastTimeOut { get; set; }
        public int LastWorkedMinutes { get; set; }
        public int CurrentMonthWorkedDays { get; set; }
        public int CurrentMonthWorkedMinutes { get; set; }
        public DateTime? CurrentMonthLastWorkDate { get; set; }
        public string LatestPayrollPeriodCode { get; set; }
        public string LatestPayrollStatus { get; set; }
        public DateTime? LatestPayrollGeneratedAt { get; set; }
        public decimal LatestPayrollBasicPay { get; set; }
        public decimal LatestPayrollAllowances { get; set; }
        public decimal LatestPayrollOvertimePay { get; set; }
        public decimal LatestPayrollOtherEarnings { get; set; }
        public decimal LatestPayrollGrossPay { get; set; }
        public decimal LatestPayrollDeductionsTotal { get; set; }
        public decimal LatestPayrollNetPay { get; set; }
        public string LatestPayrollDeductionsSummary { get; set; }
        public IReadOnlyList<EmployeePayrollLineVm> LatestPayrollDeductionLines { get; }
        public string LastDtrDateText => LastDtrDate?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "No DTR yet";
        public string LastTimeInText => LastTimeIn.HasValue ? DateTime.Today.Add(LastTimeIn.Value).ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string LastTimeOutText => LastTimeOut.HasValue ? DateTime.Today.Add(LastTimeOut.Value).ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string LastWorkedHoursText => LastWorkedMinutes > 0 ? $"{(LastWorkedMinutes / 60d):0.##} hrs" : "--";
        public string CurrentMonthLabel => DateTime.Today.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        public string CurrentMonthWorkedHoursText => CurrentMonthWorkedMinutes > 0 ? $"{(CurrentMonthWorkedMinutes / 60d):0.##} hrs" : "0 hrs";
        public string CurrentMonthLastWorkDateText => CurrentMonthLastWorkDate?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "--";
        public string LatestPayrollGeneratedText => LatestPayrollGeneratedAt?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "Not generated";
        public string LatestPayrollBasicPayText => $"PHP {LatestPayrollBasicPay:N2}";
        public string LatestPayrollAllowancesText => $"PHP {LatestPayrollAllowances:N2}";
        public string LatestPayrollOvertimePayText => $"PHP {LatestPayrollOvertimePay:N2}";
        public string LatestPayrollOtherEarningsText => $"PHP {LatestPayrollOtherEarnings:N2}";
        public string LatestPayrollGrossPayText => $"PHP {LatestPayrollGrossPay:N2}";
        public string LatestPayrollDeductionsTotalText => $"PHP {LatestPayrollDeductionsTotal:N2}";
        public string LatestPayrollNetPayText => $"PHP {LatestPayrollNetPay:N2}";
        public Brush? StatusColor { get; set; }

        private static IReadOnlyList<EmployeePayrollLineVm> BuildPayrollDeductionLines(string deductionsSummary)
        {
            if (string.IsNullOrWhiteSpace(deductionsSummary))
            {
                return new[] { new EmployeePayrollLineVm("No deduction lines generated yet.", string.Empty) };
            }

            return deductionsSummary
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParsePayrollLine)
                .ToArray();
        }

        private static EmployeePayrollLineVm ParsePayrollLine(string rawLine)
        {
            var line = rawLine.Trim();
            const string phpSeparator = ": PHP ";

            var phpIndex = line.LastIndexOf(phpSeparator, StringComparison.OrdinalIgnoreCase);
            if (phpIndex >= 0)
            {
                var label = line[..phpIndex].Trim();
                var amount = $"PHP {line[(phpIndex + phpSeparator.Length)..].Trim()}";
                return new EmployeePayrollLineVm(label, amount);
            }

            var fallbackIndex = line.LastIndexOf(':');
            if (fallbackIndex > 0 && fallbackIndex < line.Length - 1)
            {
                var label = line[..fallbackIndex].Trim();
                var amount = line[(fallbackIndex + 1)..].Trim();
                return new EmployeePayrollLineVm(label, amount);
            }

            return new EmployeePayrollLineVm(line, string.Empty);
        }
    }

    public class EmployeePayrollLineVm
    {
        public EmployeePayrollLineVm(string label, string amount)
        {
            Label = label;
            Amount = amount;
        }

        public string Label { get; }
        public string Amount { get; }
        public bool HasAmount => !string.IsNullOrWhiteSpace(Amount);
    }
}
