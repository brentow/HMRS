using HRMS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Input;
using Microsoft.Win32;

namespace HRMS.ViewModel
{
    public partial class AttendanceViewModel
    {
        private int _currentUserId;
        private string _currentUsername = "-";
        private int _selectedDtrYear;
        private int _selectedDtrMonth;
        private int? _selectedDtrEmployeeId;
        private DtrEmployeeSummaryVm? _selectedDtrEmployeeSummary;
        private string _dtrSummaryText = "No DTR loaded.";
        private int _dtrTotalWorkedDays;
        private int _dtrTotalWorkedMinutes;
        private int _dtrPresentCount;
        private int _dtrLateCount;
        private int _dtrAbsentCount;
        private int _dtrLeaveOrHolidayCount;
        private int _dtrUndertimeMinutes;
        private int _dtrOvertimeMinutes;
        private decimal _dtrAttendanceDeduction;
        private DtrCertificationRowVm? _selectedDtrCertification;
        private string _dtrCertificationRemarks = string.Empty;

        public ObservableCollection<LookupOptionVm> DtrEmployeeOptions { get; } = new();
        public ObservableCollection<LookupOptionVm> DtrMonthOptions { get; } = new();
        public ObservableCollection<int> DtrYearOptions { get; } = new();
        public ObservableCollection<DtrEmployeeSummaryVm> DtrEmployeeSummaries { get; } = new();
        public ObservableCollection<DtrDailyRowVm> DtrDailyRows { get; } = new();
        public ObservableCollection<DtrCertificationRowVm> DtrCertificationRows { get; } = new();

        public ICommand LoadDtrCommand { get; private set; } = null!;
        public ICommand ExportDtrCsvCommand { get; private set; } = null!;
        public ICommand ShowDtrWindowCommand { get; private set; } = null!;
        public ICommand CertifyDtrCommand { get; private set; } = null!;
        public ICommand VerifyDtrCommand { get; private set; } = null!;
        public ICommand ClearDtrCertificationCommand { get; private set; } = null!;

        public int SelectedDtrYear
        {
            get => _selectedDtrYear;
            set
            {
                if (_selectedDtrYear == value)
                {
                    return;
                }

                _selectedDtrYear = value;
                OnPropertyChanged();
            }
        }

        public int SelectedDtrMonth
        {
            get => _selectedDtrMonth;
            set
            {
                if (_selectedDtrMonth == value)
                {
                    return;
                }

                _selectedDtrMonth = value;
                OnPropertyChanged();
            }
        }

        public int? SelectedDtrEmployeeId
        {
            get => _selectedDtrEmployeeId;
            set
            {
                if (_selectedDtrEmployeeId == value)
                {
                    return;
                }

                _selectedDtrEmployeeId = value;
                OnPropertyChanged();
            }
        }

        public string DtrSummaryText
        {
            get => _dtrSummaryText;
            private set
            {
                if (_dtrSummaryText == value)
                {
                    return;
                }

                _dtrSummaryText = value;
                OnPropertyChanged();
            }
        }

        public DtrEmployeeSummaryVm? SelectedDtrEmployeeSummary
        {
            get => _selectedDtrEmployeeSummary;
            set
            {
                if (ReferenceEquals(_selectedDtrEmployeeSummary, value))
                {
                    return;
                }

                _selectedDtrEmployeeSummary = value;
                OnPropertyChanged();
            }
        }

        public int DtrTotalWorkedDays
        {
            get => _dtrTotalWorkedDays;
            private set
            {
                if (_dtrTotalWorkedDays == value)
                {
                    return;
                }

                _dtrTotalWorkedDays = value;
                OnPropertyChanged();
            }
        }

        public int DtrTotalWorkedMinutes
        {
            get => _dtrTotalWorkedMinutes;
            private set
            {
                if (_dtrTotalWorkedMinutes == value)
                {
                    return;
                }

                _dtrTotalWorkedMinutes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DtrTotalWorkedHoursText));
            }
        }

        public string DtrTotalWorkedHoursText => FormatMinutes(DtrTotalWorkedMinutes);
        public string DtrActionLabel => "View / Print";
        public string DtrUndertimeText => FormatMinutes(DtrUndertimeMinutes);
        public string DtrOvertimeText => FormatMinutes(DtrOvertimeMinutes);
        public string DtrAttendanceDeductionText => $"PHP {DtrAttendanceDeduction:N2}";

        public async Task<IReadOnlyList<DtrDailyRowVm>> GetEmployeeDtrRowsForViewAsync(int employeeId)
        {
            if (employeeId <= 0)
            {
                return Array.Empty<DtrDailyRowVm>();
            }

            if (IsEmployeeMode &&
                (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0 || _currentEmployeeId.Value != employeeId))
            {
                throw new InvalidOperationException("You can only view your own DTR.");
            }

            var rows = await _dataService.GetDtrDailyRowsAsync(SelectedDtrYear, SelectedDtrMonth, employeeId);
            return rows.Select(row => new DtrDailyRowVm(
                row.EmployeeId,
                row.EmployeeNo,
                row.EmployeeName,
                row.WorkDate,
                row.TimeIn,
                row.TimeOut,
                row.WorkedMinutes,
                row.Remarks,
                row.LateMinutes,
                row.EarlyOutMinutes,
                row.OvertimeMinutes,
                row.ScheduledMinutes,
                row.AttendanceDeduction,
                row.StatusCode)).ToArray();
        }

        public int DtrPresentCount
        {
            get => _dtrPresentCount;
            private set
            {
                if (_dtrPresentCount == value)
                {
                    return;
                }

                _dtrPresentCount = value;
                OnPropertyChanged();
            }
        }

        public int DtrLateCount
        {
            get => _dtrLateCount;
            private set
            {
                if (_dtrLateCount == value)
                {
                    return;
                }

                _dtrLateCount = value;
                OnPropertyChanged();
            }
        }

        public int DtrAbsentCount
        {
            get => _dtrAbsentCount;
            private set
            {
                if (_dtrAbsentCount == value)
                {
                    return;
                }

                _dtrAbsentCount = value;
                OnPropertyChanged();
            }
        }

        public int DtrLeaveOrHolidayCount
        {
            get => _dtrLeaveOrHolidayCount;
            private set
            {
                if (_dtrLeaveOrHolidayCount == value)
                {
                    return;
                }

                _dtrLeaveOrHolidayCount = value;
                OnPropertyChanged();
            }
        }

        public int DtrUndertimeMinutes
        {
            get => _dtrUndertimeMinutes;
            private set
            {
                if (_dtrUndertimeMinutes == value) return;
                _dtrUndertimeMinutes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DtrUndertimeText));
            }
        }

        public int DtrOvertimeMinutes
        {
            get => _dtrOvertimeMinutes;
            private set
            {
                if (_dtrOvertimeMinutes == value) return;
                _dtrOvertimeMinutes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DtrOvertimeText));
            }
        }

        public decimal DtrAttendanceDeduction
        {
            get => _dtrAttendanceDeduction;
            private set
            {
                if (_dtrAttendanceDeduction == value) return;
                _dtrAttendanceDeduction = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DtrAttendanceDeductionText));
            }
        }

        public bool IsDtrEmpty => DtrDailyRows.Count == 0;

        public DtrCertificationRowVm? SelectedDtrCertification
        {
            get => _selectedDtrCertification;
            set
            {
                if (_selectedDtrCertification == value)
                {
                    return;
                }

                _selectedDtrCertification = value;
                OnPropertyChanged();
                DtrCertificationRemarks = value?.Remarks ?? string.Empty;
            }
        }

        public string DtrCertificationRemarks
        {
            get => _dtrCertificationRemarks;
            set
            {
                if (_dtrCertificationRemarks == value)
                {
                    return;
                }

                _dtrCertificationRemarks = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public void SetCurrentUser(int userId, string username, string? roleName)
        {
            _currentUserId = userId;
            _currentUsername = string.IsNullOrWhiteSpace(username) ? "-" : username.Trim();
            _ = ApplyCurrentUserScopeAsync(userId, roleName);
        }

        private void InitializeDtr()
        {
            var now = DateTime.Now;
            SelectedDtrYear = now.Year;
            SelectedDtrMonth = now.Month;

            DtrYearOptions.Clear();
            for (var year = now.Year - 3; year <= now.Year + 1; year++)
            {
                DtrYearOptions.Add(year);
            }

            DtrMonthOptions.Clear();
            for (var month = 1; month <= 12; month++)
            {
                var label = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month);
                DtrMonthOptions.Add(new LookupOptionVm(month, $"{month:00} - {label}"));
            }

            LoadDtrCommand = new AsyncRelayCommand(_ => LoadDtrAsync());
            ExportDtrCsvCommand = new AsyncRelayCommand(_ => ExportDtrCsvAsync());
            ShowDtrWindowCommand = new AsyncRelayCommand(_ => Task.CompletedTask);
            CertifyDtrCommand = new AsyncRelayCommand(_ => CertifySelectedDtrAsync());
            VerifyDtrCommand = new AsyncRelayCommand(_ => VerifySelectedDtrAsync());
            ClearDtrCertificationCommand = new AsyncRelayCommand(_ => ClearSelectedDtrCertificationAsync());
        }

        private async Task LoadDtrAsync(bool silent = false)
        {
            try
            {
                var employeeId = IsEmployeeMode
                    ? (_currentEmployeeId.HasValue && _currentEmployeeId.Value > 0 ? _currentEmployeeId : null)
                    : (SelectedDtrEmployeeId.HasValue && SelectedDtrEmployeeId.Value > 0 ? SelectedDtrEmployeeId : null);

                if (IsEmployeeMode && employeeId.HasValue)
                {
                    SelectedDtrEmployeeId = employeeId.Value;
                }

                var selectedEmployeeId = SelectedDtrCertification?.EmployeeId;

                var dtrRows = await _dataService.GetDtrDailyRowsAsync(SelectedDtrYear, SelectedDtrMonth, employeeId);
                var certRows = await _dataService.GetDtrMonthlyCertificationsAsync(SelectedDtrYear, SelectedDtrMonth, employeeId);

                DtrDailyRows.Clear();
                foreach (var row in dtrRows)
                {
                    DtrDailyRows.Add(new DtrDailyRowVm(
                        row.EmployeeId,
                        row.EmployeeNo,
                        row.EmployeeName,
                        row.WorkDate,
                        row.TimeIn,
                        row.TimeOut,
                        row.WorkedMinutes,
                        row.Remarks,
                        row.LateMinutes,
                        row.EarlyOutMinutes,
                        row.OvertimeMinutes,
                        row.ScheduledMinutes,
                        row.AttendanceDeduction,
                        row.StatusCode));
                }

                RebuildDtrEmployeeSummaries();

                DtrCertificationRows.Clear();
                foreach (var row in certRows)
                {
                    DtrCertificationRows.Add(new DtrCertificationRowVm(
                        row.EmployeeId,
                        row.EmployeeNo,
                        row.EmployeeName,
                        row.WorkedDays,
                        row.WorkedMinutes,
                        row.CertifiedBy,
                        row.CertifiedAt,
                        row.VerifiedBy,
                        row.VerifiedAt,
                        row.Remarks));
                }

                SelectedDtrCertification = selectedEmployeeId.HasValue
                    ? DtrCertificationRows.FirstOrDefault(x => x.EmployeeId == selectedEmployeeId.Value)
                    : null;

                DtrTotalWorkedDays = DtrDailyRows.Count(x => x.IsPresent);
                DtrTotalWorkedMinutes = DtrDailyRows.Sum(x => x.WorkedMinutes);
                UpdateDtrSummaryCounts();
                DtrSummaryText = DtrDailyRows.Count == 0
                    ? "No attendance records found."
                    : $"{DtrDailyRows.Count} daily records | {DtrEmployeeSummaries.Count} employee(s) | Attendance deductions: {DtrAttendanceDeductionText}";
                OnPropertyChanged(nameof(IsDtrEmpty));

                if (!silent)
                {
                    SetMessage("DTR data loaded.", SuccessBrush);
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    SetMessage($"Unable to load DTR: {ex.Message}", ErrorBrush);
                }
            }
        }

        private async Task ExportDtrCsvAsync()
        {
            if (DtrDailyRows.Count == 0)
            {
                SetMessage("No DTR rows to export.", ErrorBrush);
                return;
            }

            try
            {
                var fileName = $"DTR_{SelectedDtrYear}_{SelectedDtrMonth:00}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var dialog = new SaveFileDialog
                {
                    Title = "Save DTR Export",
                    FileName = fileName,
                    DefaultExt = ".csv",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    AddExtension = true,
                    OverwritePrompt = true
                };

                var result = dialog.ShowDialog();
                if (result != true || string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    SetMessage("DTR export canceled.", InfoBrush);
                    return;
                }

                var path = dialog.FileName;

                var builder = new StringBuilder();
                builder.AppendLine("Employee No,Employee Name,Date,Day,AM Arrival,AM Departure,PM Arrival,PM Departure,Scheduled Minutes,Worked Minutes,Worked Hours,Late Minutes,Undertime Minutes,Overtime Minutes,Status,Attendance Deduction,Remarks");

                foreach (var row in DtrDailyRows)
                {
                    builder.AppendLine(string.Join(",",
                        Csv(row.EmployeeNo),
                        Csv(row.EmployeeName),
                        Csv(row.DateText),
                        Csv(row.DayName),
                        Csv(row.AmArrival),
                        Csv(row.AmDeparture),
                        Csv(row.PmArrival),
                        Csv(row.PmDeparture),
                        Csv(row.ScheduledMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.WorkedMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.WorkedHoursText),
                        Csv(row.LateMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.EarlyOutMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.OvertimeMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.StatusDisplay),
                        Csv(row.AttendanceDeduction.ToString("0.00", CultureInfo.InvariantCulture)),
                        Csv(row.Remarks)));
                }

                await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8);
                SetMessage($"DTR exported: {path}", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Export failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task CertifySelectedDtrAsync()
        {
            if (!EnsureAdminOrHrAccess("Certifying DTR"))
            {
                return;
            }

            if (SelectedDtrCertification == null)
            {
                SetMessage("Select an employee in monthly certification first.", ErrorBrush);
                return;
            }

            try
            {
                var remarks = string.IsNullOrWhiteSpace(DtrCertificationRemarks)
                    ? $"Certified by {(_currentUsername == "-" ? "system admin" : _currentUsername)}"
                    : DtrCertificationRemarks.Trim();

                await _dataService.UpsertDtrCertificationAsync(
                    SelectedDtrCertification.EmployeeId,
                    SelectedDtrYear,
                    SelectedDtrMonth,
                    _currentUserId > 0 ? _currentUserId : null,
                    remarks);

                await LoadDtrAsync();
                SetMessage($"DTR certified for {SelectedDtrCertification.EmployeeNo}.", SuccessBrush);
                SystemRefreshBus.Raise("DtrCertificationUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Certification failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task VerifySelectedDtrAsync()
        {
            if (!EnsureAdminOrHrAccess("Verifying DTR"))
            {
                return;
            }

            if (SelectedDtrCertification == null)
            {
                SetMessage("Select an employee in monthly certification first.", ErrorBrush);
                return;
            }

            try
            {
                var remarks = string.IsNullOrWhiteSpace(DtrCertificationRemarks)
                    ? $"Verified by {(_currentUsername == "-" ? "system admin" : _currentUsername)}"
                    : DtrCertificationRemarks.Trim();

                await _dataService.UpsertDtrVerificationAsync(
                    SelectedDtrCertification.EmployeeId,
                    SelectedDtrYear,
                    SelectedDtrMonth,
                    _currentUserId > 0 ? _currentUserId : null,
                    remarks);

                await LoadDtrAsync();
                SetMessage($"DTR verified for {SelectedDtrCertification.EmployeeNo}.", SuccessBrush);
                SystemRefreshBus.Raise("DtrCertificationUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Verification failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ClearSelectedDtrCertificationAsync()
        {
            if (!EnsureAdminOrHrAccess("Clearing DTR certification"))
            {
                return;
            }

            if (SelectedDtrCertification == null)
            {
                SetMessage("Select an employee in monthly certification first.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.ClearDtrCertificationAsync(
                    SelectedDtrCertification.EmployeeId,
                    SelectedDtrYear,
                    SelectedDtrMonth);

                await LoadDtrAsync();
                SetMessage($"Certification removed for {SelectedDtrCertification.EmployeeNo}.", SuccessBrush);
                SystemRefreshBus.Raise("DtrCertificationUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Clear certification failed: {ex.Message}", ErrorBrush);
            }
        }

        private static string Csv(string? input)
        {
            var value = input ?? string.Empty;
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

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

        private void UpdateDtrSummaryCounts()
        {
            DtrPresentCount = DtrDailyRows.Count(x => x.IsPresent);
            DtrLateCount = DtrDailyRows.Count(x => x.LateMinutes > 0);
            DtrAbsentCount = DtrDailyRows.Count(x => x.StatusDisplay == "Absent");
            DtrLeaveOrHolidayCount = DtrDailyRows.Count(x =>
                x.StatusDisplay == "On Leave" ||
                x.StatusDisplay == "Holiday" ||
                x.StatusDisplay == "Travel Order" ||
                x.StatusDisplay == "Official Business");
            DtrUndertimeMinutes = DtrDailyRows.Sum(x => x.EarlyOutMinutes);
            DtrOvertimeMinutes = DtrDailyRows.Sum(x => x.OvertimeMinutes);
            DtrAttendanceDeduction = DtrDailyRows.Sum(x => x.AttendanceDeduction);
        }

        private void RebuildDtrEmployeeSummaries()
        {
            DtrEmployeeSummaries.Clear();

            foreach (var group in DtrDailyRows
                         .GroupBy(x => new { x.EmployeeId, x.EmployeeNo, x.EmployeeName })
                         .OrderBy(x => x.Key.EmployeeName, StringComparer.InvariantCultureIgnoreCase))
            {
                DtrEmployeeSummaries.Add(new DtrEmployeeSummaryVm(
                    group.Key.EmployeeId,
                    group.Key.EmployeeNo,
                    group.Key.EmployeeName,
                    group.Count(x => x.IsPresent),
                    group.Count(x => x.LateMinutes > 0),
                    group.Count(x => x.StatusDisplay == "Absent"),
                    group.Count(x => x.StatusDisplay == "On Leave"),
                    group.Sum(x => x.EarlyOutMinutes),
                    group.Sum(x => x.OvertimeMinutes),
                    group.Sum(x => x.AttendanceDeduction),
                    group.Sum(x => x.WorkedMinutes)));
            }

            if (SelectedDtrEmployeeSummary != null)
            {
                SelectedDtrEmployeeSummary = DtrEmployeeSummaries.FirstOrDefault(x => x.EmployeeId == SelectedDtrEmployeeSummary.EmployeeId);
            }

            if (SelectedDtrEmployeeSummary == null && DtrEmployeeSummaries.Count > 0)
            {
                SelectedDtrEmployeeSummary = DtrEmployeeSummaries[0];
            }
        }

        public IReadOnlyList<DtrDailyRowVm> GetEmployeeDtrRows(int employeeId)
        {
            if (employeeId <= 0)
            {
                return Array.Empty<DtrDailyRowVm>();
            }

            return DtrDailyRows
                .Where(x => x.EmployeeId == employeeId)
                .OrderBy(x => x.WorkDate)
                .ToList();
        }
    }

    public class DtrEmployeeSummaryVm
    {
        public DtrEmployeeSummaryVm(
            int employeeId,
            string employeeNo,
            string employeeName,
            int presentDays,
            int lateDays,
            int absentDays,
            int leaveDays,
            int undertimeMinutes,
            int overtimeMinutes,
            decimal attendanceDeduction,
            int workedMinutes)
        {
            EmployeeId = employeeId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            PresentDays = presentDays;
            LateDays = lateDays;
            AbsentDays = absentDays;
            LeaveDays = leaveDays;
            UndertimeMinutes = undertimeMinutes;
            OvertimeMinutes = overtimeMinutes;
            AttendanceDeduction = attendanceDeduction;
            WorkedMinutes = workedMinutes;
        }

        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public int PresentDays { get; }
        public int LateDays { get; }
        public int AbsentDays { get; }
        public int LeaveDays { get; }
        public int UndertimeMinutes { get; }
        public int OvertimeMinutes { get; }
        public decimal AttendanceDeduction { get; }
        public int WorkedMinutes { get; }
        public string WorkedHoursText => WorkedMinutes <= 0 ? "0h 0m" : $"{WorkedMinutes / 60}h {WorkedMinutes % 60}m";
        public string UndertimeText => UndertimeMinutes <= 0 ? "-" : $"{UndertimeMinutes / 60}h {UndertimeMinutes % 60}m";
        public string OvertimeText => OvertimeMinutes <= 0 ? "-" : $"{OvertimeMinutes / 60}h {OvertimeMinutes % 60}m";
        public string AttendanceDeductionText => AttendanceDeduction <= 0m ? "-" : $"PHP {AttendanceDeduction:N2}";
    }

    public class DtrDailyRowVm
    {
        private static readonly Brush PresentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush LateBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B9831A"));
        private static readonly Brush AbsentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));
        private static readonly Brush LeaveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F80ED"));
        private static readonly Brush HolidayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7353BB"));
        private static readonly Brush WeekendBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
        private static readonly Brush PendingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6A7684"));
        private static readonly Brush DefaultBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4368"));

        public DtrDailyRowVm(
            int employeeId,
            string employeeNo,
            string employeeName,
            DateTime workDate,
            DateTime? timeIn,
            DateTime? timeOut,
            int workedMinutes,
            string remarks,
            int lateMinutes,
            int earlyOutMinutes,
            int overtimeMinutes,
            int scheduledMinutes,
            decimal attendanceDeduction,
            string statusCode)
        {
            EmployeeId = employeeId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            WorkDate = workDate;
            TimeIn = timeIn;
            TimeOut = timeOut;
            WorkedMinutes = workedMinutes;
            Remarks = string.IsNullOrWhiteSpace(remarks) ? "-" : remarks.Trim();
            LateMinutes = lateMinutes;
            EarlyOutMinutes = earlyOutMinutes;
            OvertimeMinutes = overtimeMinutes;
            ScheduledMinutes = scheduledMinutes;
            AttendanceDeduction = attendanceDeduction;
            StatusCode = string.IsNullOrWhiteSpace(statusCode) ? "PENDING" : statusCode.Trim().ToUpperInvariant();
        }

        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public DateTime WorkDate { get; }
        public DateTime? TimeIn { get; }
        public DateTime? TimeOut { get; }
        public int WorkedMinutes { get; }
        public string Remarks { get; }
        public int LateMinutes { get; }
        public int EarlyOutMinutes { get; }
        public int OvertimeMinutes { get; }
        public int ScheduledMinutes { get; }
        public decimal AttendanceDeduction { get; }
        public string StatusCode { get; }
        public bool IsPresent => TimeIn.HasValue || TimeOut.HasValue || WorkedMinutes > 0;

        public string DateText => WorkDate == DateTime.MinValue ? "-" : WorkDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string DayName => WorkDate == DateTime.MinValue ? "-" : WorkDate.ToString("ddd", CultureInfo.InvariantCulture);
        public string AmArrival => TimeIn.HasValue ? TimeIn.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string AmDeparture => TimeIn.HasValue && TimeOut.HasValue ? "12:00 PM" : "--";
        public string PmArrival => TimeIn.HasValue && TimeOut.HasValue ? "01:00 PM" : "--";
        public string PmDeparture => TimeOut.HasValue ? TimeOut.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string TimeInText => TimeIn.HasValue ? TimeIn.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string TimeOutText => TimeOut.HasValue ? TimeOut.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string WorkedHoursText => FormatMinutes(WorkedMinutes);
        public string UndertimeText => EarlyOutMinutes > 0 ? $"{EarlyOutMinutes}m" : "-";
        public string LateText => LateMinutes > 0 ? $"{LateMinutes}m" : "-";
        public string OvertimeText => OvertimeMinutes > 0 ? $"{OvertimeMinutes}m" : "-";
        public string AttendanceDeductionText => AttendanceDeduction > 0m ? $"PHP {AttendanceDeduction:N2}" : "-";
        public string StatusDisplay
        {
            get
            {
                return StatusCode switch
                {
                    "PRESENT" => "Present",
                    "LATE" => "Late",
                    "UNDERTIME" => "Undertime",
                    "LATE_UNDERTIME" => "Late / Undertime",
                    "OVERTIME" => "Overtime",
                    "ABSENT" => "Absent",
                    "LEAVE" => "On Leave",
                    "HOLIDAY" => "Holiday",
                    "WEEKEND" => "Weekend",
                    "PENDING" => "Pending",
                    "INCOMPLETE" => "Incomplete Punch",
                    "TRAVEL_ORDER" => "Travel Order",
                    "OFFICIAL_BUSINESS" => "Official Business",
                    _ => "Present"
                };
            }
        }

        public Brush StatusBrush => StatusDisplay switch
        {
            "Present" => PresentBrush,
            "Late" => LateBrush,
            "Undertime" => LateBrush,
            "Late / Undertime" => LateBrush,
            "Overtime" => PresentBrush,
            "Absent" => AbsentBrush,
            "On Leave" => LeaveBrush,
            "Holiday" => HolidayBrush,
            "Weekend" => WeekendBrush,
            "Pending" => PendingBrush,
            "Incomplete Punch" => AbsentBrush,
            "Travel Order" => HolidayBrush,
            "Official Business" => LeaveBrush,
            _ => DefaultBrush
        };

        public string AttendanceFlag
        {
            get
            {
                if (StatusDisplay is "Absent" or "On Leave" or "Holiday" or "Weekend" or "Pending" or "Incomplete Punch")
                {
                    return StatusDisplay;
                }

                var parts = new System.Collections.Generic.List<string>();
                if (LateMinutes > 0) parts.Add($"Late {LateMinutes}m");
                if (EarlyOutMinutes > 0) parts.Add($"Undertime {EarlyOutMinutes}m");
                if (OvertimeMinutes > 0) parts.Add($"OT {OvertimeMinutes}m");
                return parts.Count == 0 ? "On time" : string.Join(" / ", parts);
            }
        }

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

    public class DtrCertificationRowVm
    {
        public DtrCertificationRowVm(
            int employeeId,
            string employeeNo,
            string employeeName,
            int workedDays,
            int workedMinutes,
            string certifiedBy,
            DateTime? certifiedAt,
            string verifiedBy,
            DateTime? verifiedAt,
            string remarks)
        {
            EmployeeId = employeeId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            WorkedDays = workedDays;
            WorkedMinutes = workedMinutes;
            CertifiedBy = string.IsNullOrWhiteSpace(certifiedBy) || certifiedBy == "-" ? "-" : certifiedBy.Trim();
            CertifiedAt = certifiedAt;
            VerifiedBy = string.IsNullOrWhiteSpace(verifiedBy) || verifiedBy == "-" ? "-" : verifiedBy.Trim();
            VerifiedAt = verifiedAt;
            Remarks = string.IsNullOrWhiteSpace(remarks) ? string.Empty : remarks.Trim();
        }

        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public int WorkedDays { get; }
        public int WorkedMinutes { get; }
        public string CertifiedBy { get; }
        public DateTime? CertifiedAt { get; }
        public string VerifiedBy { get; }
        public DateTime? VerifiedAt { get; }
        public string Remarks { get; }

        public string WorkedHoursText
        {
            get
            {
                if (WorkedMinutes <= 0)
                {
                    return "0h 0m";
                }

                var hours = WorkedMinutes / 60;
                var minutes = WorkedMinutes % 60;
                return $"{hours}h {minutes}m";
            }
        }

        public string CertifiedAtText => CertifiedAt.HasValue
            ? CertifiedAt.Value.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture)
            : "-";

        public string VerifiedAtText => VerifiedAt.HasValue
            ? VerifiedAt.Value.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture)
            : "-";

        public string CertificationState => VerifiedAt.HasValue
            ? "VERIFIED"
            : CertifiedAt.HasValue
                ? "CERTIFIED"
                : "PENDING";
    }
}
