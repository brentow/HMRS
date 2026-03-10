using HRMS.Model;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private string _dtrSummaryText = "No DTR loaded.";
        private int _dtrTotalWorkedDays;
        private int _dtrTotalWorkedMinutes;
        private DtrCertificationRowVm? _selectedDtrCertification;
        private string _dtrCertificationRemarks = string.Empty;

        public ObservableCollection<LookupOptionVm> DtrEmployeeOptions { get; } = new();
        public ObservableCollection<LookupOptionVm> DtrMonthOptions { get; } = new();
        public ObservableCollection<int> DtrYearOptions { get; } = new();
        public ObservableCollection<DtrDailyRowVm> DtrDailyRows { get; } = new();
        public ObservableCollection<DtrCertificationRowVm> DtrCertificationRows { get; } = new();

        public ICommand LoadDtrCommand { get; private set; } = null!;
        public ICommand ExportDtrCsvCommand { get; private set; } = null!;
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
                        row.Remarks));
                }

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

                DtrTotalWorkedDays = DtrCertificationRows.Sum(x => x.WorkedDays);
                DtrTotalWorkedMinutes = DtrCertificationRows.Sum(x => x.WorkedMinutes);
                DtrSummaryText = $"{DtrDailyRows.Count} daily records | {DtrCertificationRows.Count} employees";

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
                builder.AppendLine("Employee No,Employee Name,Date,Day,AM Arrival,AM Departure,PM Arrival,PM Departure,Worked Minutes,Worked Hours,Remarks");

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
                        Csv(row.WorkedMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.WorkedHoursText),
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
    }

    public class DtrDailyRowVm
    {
        public DtrDailyRowVm(
            int employeeId,
            string employeeNo,
            string employeeName,
            DateTime workDate,
            DateTime? timeIn,
            DateTime? timeOut,
            int workedMinutes,
            string remarks)
        {
            EmployeeId = employeeId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            WorkDate = workDate;
            TimeIn = timeIn;
            TimeOut = timeOut;
            WorkedMinutes = workedMinutes;
            Remarks = string.IsNullOrWhiteSpace(remarks) ? "-" : remarks.Trim();
        }

        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public DateTime WorkDate { get; }
        public DateTime? TimeIn { get; }
        public DateTime? TimeOut { get; }
        public int WorkedMinutes { get; }
        public string Remarks { get; }

        public string DateText => WorkDate == DateTime.MinValue ? "-" : WorkDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string DayName => WorkDate == DateTime.MinValue ? "-" : WorkDate.ToString("ddd", CultureInfo.InvariantCulture);
        public string AmArrival => TimeIn.HasValue ? TimeIn.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string AmDeparture => TimeIn.HasValue && TimeOut.HasValue ? "12:00 PM" : "--";
        public string PmArrival => TimeIn.HasValue && TimeOut.HasValue ? "01:00 PM" : "--";
        public string PmDeparture => TimeOut.HasValue ? TimeOut.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string WorkedHoursText => FormatMinutes(WorkedMinutes);

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
