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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HRMS.ViewModel
{
    public sealed class BiometricAttendanceKioskViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#315A7B"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));
        private static readonly Brush WarningBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7791F"));

        private readonly AttendanceDataService _dataService = new(DbConfig.ConnectionString);
        private readonly DigitalPersonaRuntimeService _digitalPersonaRuntimeService = new();
        private readonly int? _scopedEmployeeId;
        private readonly bool _isEmployeeScoped;
        private readonly DispatcherTimer _clockTimer;
        private readonly AsyncRelayCommand _refreshCommand;
        private readonly AsyncRelayCommand _timeInCommand;
        private readonly AsyncRelayCommand _timeOutCommand;
        private CancellationTokenSource? _activePunchCancellation;

        private BiometricEmployeeOption? _selectedEmployee;
        private BiometricDeviceOption? _selectedDevice;
        private EmployeeShiftScheduleDto _currentShift = DefaultShift;
        private DateTime _selectedDtrMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private bool _suppressSelectionLoad;
        private bool _isBusy;
        private string _currentDateText = string.Empty;
        private string _currentTimeText = string.Empty;
        private string _scheduleText = "Default schedule: 07:00 AM - 05:00 PM";
        private string _workDateText = DateTime.Today.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        private string _morningInText = "-";
        private string _morningOutText = "-";
        private string _afternoonInText = "-";
        private string _afternoonOutText = "-";
        private string _lateText = "0 min";
        private string _undertimeText = "0 min";
        private string _minusText = "No minus";
        private string _missingText = "No missing punch";
        private string _workedText = "0h 0m";
        private string _lastPunchText = "No punch yet";
        private string _actionMessage = "Choose Time In or Time Out, then scan a finger to identify the employee.";
        private Brush _actionMessageBrush = InfoBrush;

        private static EmployeeShiftScheduleDto DefaultShift { get; } = new(
            ShiftName: "Default Day Shift",
            StartTime: TimeSpan.FromHours(7),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 60,
            GraceMinutes: 10,
            IsOvernight: false);

        public BiometricAttendanceKioskViewModel(AuthenticatedUser? user)
        {
            _scopedEmployeeId = user?.EmployeeId;
            _isEmployeeScoped = _scopedEmployeeId.HasValue && !AuthorizationGuard.IsAdminOrHr(user?.RoleName);

            _refreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
            _timeInCommand = new AsyncRelayCommand(_ => PunchAsync("IN"));
            _timeOutCommand = new AsyncRelayCommand(_ => PunchAsync("OUT"));

            RefreshCommand = _refreshCommand;
            TimeInCommand = _timeInCommand;
            TimeOutCommand = _timeOutCommand;

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (_, _) => UpdateClock();
            UpdateClock();
            _clockTimer.Start();
        }

        public ObservableCollection<BiometricEmployeeOption> Employees { get; } = new();
        public ObservableCollection<BiometricDeviceOption> Devices { get; } = new();
        public ObservableCollection<BiometricPunchRowVm> TodayPunches { get; } = new();
        public ObservableCollection<BiometricDtrDayVm> DtrRows { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand TimeInCommand { get; }
        public ICommand TimeOutCommand { get; }

        public string PageTitle => _isEmployeeScoped ? "My Biometric Attendance" : "Biometric Attendance";
        public string PageSubtitle => _isEmployeeScoped
            ? "Scan your fingerprint to record your Time In or Time Out."
            : "The fingerprint reader identifies the employee automatically.";
        public string EmployeePanelTitle => _isEmployeeScoped ? "My Attendance" : "Employee";
        public string EmployeePanelSubtitle => _isEmployeeScoped
            ? "Employee access is limited to your own attendance."
            : "Choose the employee who will punch attendance.";
        public string ScopeNoticeText => _isEmployeeScoped
            ? "Employee mode: you can only view and punch your own record."
            : "Admin/HR mode: you can view and punch active employees.";
        public bool CanChangeEmployee => !_isEmployeeScoped && !IsBusy;
        public bool CanChangeDevice => !IsBusy && Devices.Count > 0;
        public string DtrMonthText => $"{_selectedDtrMonth:MMMM} 1 - {_selectedDtrMonth.AddMonths(1).AddDays(-1):dd, yyyy}";
        public string DtrEmployeeText => $"{SelectedEmployeeName} ({SelectedEmployeeNo})";

        public BiometricEmployeeOption? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (Equals(_selectedEmployee, value))
                {
                    return;
                }

                _selectedEmployee = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedEmployeeName));
                OnPropertyChanged(nameof(SelectedEmployeeNo));
                OnPropertyChanged(nameof(DtrEmployeeText));
                OnPropertyChanged(nameof(CanPunch));
                OnPropertyChanged(nameof(CanChangeEmployee));
                RaiseCommandStates();
                if (!_suppressSelectionLoad)
                {
                    _ = LoadSelectedEmployeeDayAsync();
                }
            }
        }

        public string SelectedEmployeeName => SelectedEmployee?.EmployeeName ?? "Scan fingerprint to identify";
        public string SelectedEmployeeNo => SelectedEmployee?.EmployeeNo ?? "Ready for biometric identification";

        public BiometricDeviceOption? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (Equals(_selectedDevice, value))
                {
                    return;
                }

                _selectedDevice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDeviceName));
                OnPropertyChanged(nameof(CanPunch));
                RaiseCommandStates();
            }
        }

        public string SelectedDeviceName => SelectedDevice?.DeviceName ?? "No biometric device selected";

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                {
                    return;
                }

                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPunch));
                OnPropertyChanged(nameof(CanChangeEmployee));
                OnPropertyChanged(nameof(CanChangeDevice));
                RaiseCommandStates();
            }
        }

        public bool CanPunch =>
            !IsBusy &&
            SelectedDevice != null &&
            (!_isEmployeeScoped || SelectedEmployee?.EmployeeId == _scopedEmployeeId);

        public string CurrentDateText
        {
            get => _currentDateText;
            private set
            {
                if (_currentDateText != value)
                {
                    _currentDateText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentTimeText
        {
            get => _currentTimeText;
            private set
            {
                if (_currentTimeText != value)
                {
                    _currentTimeText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WorkDateText
        {
            get => _workDateText;
            private set
            {
                if (_workDateText != value)
                {
                    _workDateText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ScheduleText
        {
            get => _scheduleText;
            private set
            {
                if (_scheduleText != value)
                {
                    _scheduleText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MorningInText
        {
            get => _morningInText;
            private set
            {
                if (_morningInText != value)
                {
                    _morningInText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MorningOutText
        {
            get => _morningOutText;
            private set
            {
                if (_morningOutText != value)
                {
                    _morningOutText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string AfternoonInText
        {
            get => _afternoonInText;
            private set
            {
                if (_afternoonInText != value)
                {
                    _afternoonInText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string AfternoonOutText
        {
            get => _afternoonOutText;
            private set
            {
                if (_afternoonOutText != value)
                {
                    _afternoonOutText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LateText
        {
            get => _lateText;
            private set
            {
                if (_lateText != value)
                {
                    _lateText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string UndertimeText
        {
            get => _undertimeText;
            private set
            {
                if (_undertimeText != value)
                {
                    _undertimeText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MinusText
        {
            get => _minusText;
            private set
            {
                if (_minusText != value)
                {
                    _minusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MissingText
        {
            get => _missingText;
            private set
            {
                if (_missingText != value)
                {
                    _missingText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WorkedText
        {
            get => _workedText;
            private set
            {
                if (_workedText != value)
                {
                    _workedText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LastPunchText
        {
            get => _lastPunchText;
            private set
            {
                if (_lastPunchText != value)
                {
                    _lastPunchText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ActionMessage
        {
            get => _actionMessage;
            private set
            {
                if (_actionMessage != value)
                {
                    _actionMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public Brush ActionMessageBrush
        {
            get => _actionMessageBrush;
            private set
            {
                if (!Equals(_actionMessageBrush, value))
                {
                    _actionMessageBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        public async Task InitializeAsync()
        {
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var previousEmployeeId = SelectedEmployee?.EmployeeId;
                var previousDeviceId = SelectedDevice?.DeviceId;
                var employees = await _dataService.GetEmployeesLookupAsync();
                var devices = await _dataService.GetBiometricDevicesAsync();
                if (_isEmployeeScoped)
                {
                    employees = employees
                        .Where(employee => employee.EmployeeId == _scopedEmployeeId)
                        .ToList();
                }

                Employees.Clear();
                Devices.Clear();

                foreach (var employee in employees)
                {
                    Employees.Add(new BiometricEmployeeOption(
                        employee.EmployeeId,
                        employee.EmployeeNo,
                        employee.EmployeeName));
                }

                foreach (var device in devices.Where(device => device.IsActive))
                {
                    Devices.Add(new BiometricDeviceOption(
                        device.DeviceId,
                        device.DeviceName,
                        device.SerialNo,
                        device.Location,
                        device.IpAddress));
                }

                _suppressSelectionLoad = true;
                SelectedEmployee = _isEmployeeScoped
                    ? Employees.FirstOrDefault()
                    : previousEmployeeId.HasValue
                        ? Employees.FirstOrDefault(x => x.EmployeeId == previousEmployeeId.Value)
                        : null;
                SelectedDevice = previousDeviceId.HasValue
                    ? Devices.FirstOrDefault(x => x.DeviceId == previousDeviceId.Value) ?? Devices.FirstOrDefault()
                    : Devices.FirstOrDefault();
                _suppressSelectionLoad = false;
                OnPropertyChanged(nameof(CanChangeDevice));

                if (Employees.Count == 0 || (_isEmployeeScoped && SelectedEmployee == null))
                {
                    ClearDay(_isEmployeeScoped ? "Your account is not linked to an active employee." : "No active employees found.");
                    SetMessage(
                        _isEmployeeScoped
                            ? "Your user account is not linked to an active employee record."
                            : "No active employees are available for attendance.",
                        WarningBrush);
                }
                else if (SelectedDevice == null)
                {
                    if (SelectedEmployee != null)
                    {
                        await LoadSelectedEmployeeDayAsync();
                    }

                    SetMessage("No active biometric device found. Add or activate a device in Attendance Timekeeping Hub first.", WarningBrush);
                }
                else
                {
                    if (SelectedEmployee != null)
                    {
                        await LoadSelectedEmployeeDayAsync();
                    }
                    else
                    {
                        ClearDay("Scan a fingerprint to load today's attendance.");
                    }

                    SetMessage(
                        _isEmployeeScoped
                            ? $"Your attendance is ready. Device: {SelectedDevice.DeviceName}. Click Time In or Time Out, then scan your finger."
                            : $"Reader ready. Click Time In or Time Out, then scan a finger to identify the employee.",
                        SuccessBrush);
                }
            }
            catch (Exception ex)
            {
                ClearDay("Unable to load attendance.");
                SetMessage($"Unable to load biometric attendance: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task PunchAsync(string logType)
        {
            if (_isEmployeeScoped && SelectedEmployee == null)
            {
                SetMessage("Your account is not linked to an active employee profile.", WarningBrush);
                return;
            }

            if (SelectedDevice == null)
            {
                SetMessage("Choose a biometric device first.", WarningBrush);
                return;
            }

            if (_isEmployeeScoped && SelectedEmployee!.EmployeeId != _scopedEmployeeId)
            {
                SetMessage("Employee mode can only punch your own attendance.", ErrorBrush);
                return;
            }

            var punchCancellation = new CancellationTokenSource();
            _activePunchCancellation = punchCancellation;
            IsBusy = true;
            try
            {
                var matchedEnrollment = await ScanSelectedEmployeeFingerprintAsync(logType, punchCancellation.Token);
                if (matchedEnrollment == null)
                {
                    return;
                }

                var recognizedEmployee = Employees.FirstOrDefault(
                    employee => employee.EmployeeId == matchedEnrollment.Enrollment.EmployeeId);
                if (recognizedEmployee == null)
                {
                    SetMessage("Fingerprint recognized, but the employee profile is not active.", WarningBrush);
                    return;
                }

                _suppressSelectionLoad = true;
                SelectedEmployee = recognizedEmployee;
                _suppressSelectionLoad = false;

                punchCancellation.Token.ThrowIfCancellationRequested();
                var punchTime = DateTime.Now;
                await _dataService.AddAttendanceLogAsync(
                    recognizedEmployee.EmployeeId,
                    deviceId: SelectedDevice.DeviceId,
                    logTime: punchTime,
                    logType: logType,
                    source: "BIOMETRIC");
                await _dataService.MarkDeviceSyncedNowAsync(SelectedDevice.DeviceId);

                await LoadSelectedEmployeeDayAsync();
                SetMessage($"Welcome, {recognizedEmployee.EmployeeName}. {logType} recorded at {punchTime:hh:mm tt}.", SuccessBrush);
                SystemRefreshBus.Raise("BiometricAttendanceLogged");
            }
            catch (OperationCanceledException)
            {
                SetMessage($"{logType} fingerprint scan cancelled.", InfoBrush);
            }
            catch (Exception)
            {
                SetMessage($"The fingerprint scan could not be completed. Check the reader and try {logType} again.", ErrorBrush);
            }
            finally
            {
                if (ReferenceEquals(_activePunchCancellation, punchCancellation))
                {
                    _activePunchCancellation = null;
                }

                punchCancellation.Dispose();
                IsBusy = false;
            }
        }

        public void CancelActivePunch()
        {
            if (_activePunchCancellation is { IsCancellationRequested: false } cancellation)
            {
                cancellation.Cancel();
            }
        }

        private async Task<BiometricMatchedEnrollment?> ScanSelectedEmployeeFingerprintAsync(
            string logType,
            CancellationToken cancellationToken)
        {
            if (_isEmployeeScoped && SelectedEmployee == null)
            {
                SetMessage("Your account is not linked to an active employee profile.", WarningBrush);
                return null;
            }

            if (SelectedDevice == null)
            {
                SetMessage("Choose a biometric device first.", WarningBrush);
                return null;
            }

            var selectedDevice = SelectedDevice;
            var gallery = await _dataService.GetBiometricTemplateGalleryAsync(selectedDevice.DeviceId);
            if (gallery.Count == 0)
            {
                SetMessage($"No enrolled fingerprint templates found for {selectedDevice.DeviceName}. Enroll fingerprints or choose another biometric device.", WarningBrush);
                return null;
            }

            if (_isEmployeeScoped && !gallery.Any(x => x.EmployeeId == SelectedEmployee!.EmployeeId))
            {
                SetMessage("No enrolled fingerprint was found for your profile on this reader.", WarningBrush);
                return null;
            }

            SetMessage(
                _isEmployeeScoped
                    ? $"Place your finger on {selectedDevice.DeviceName} to record {logType}."
                    : $"Place a finger on {selectedDevice.DeviceName}. HRMS will identify the employee automatically.",
                InfoBrush);

            var match = await _digitalPersonaRuntimeService.IdentifyAsync(
                gallery.Select(x => new BiometricStoredTemplate(
                    x.EnrollmentId,
                    x.EmployeeId,
                    x.EmployeeNo,
                    x.EmployeeName,
                    x.BiometricUserId,
                    x.DeviceId,
                    x.DeviceName,
                    x.TemplateData,
                    x.TemplateFormat,
                    x.TemplateEncoding)).ToList(),
                cancellationToken: cancellationToken);

            if (match == null)
            {
                SetMessage("Fingerprint not recognized. Use an enrolled finger, place it flat on the reader, and try again.", WarningBrush);
                return null;
            }

            if (_isEmployeeScoped && match.Enrollment.EmployeeId != SelectedEmployee!.EmployeeId)
            {
                SetMessage("This fingerprint does not belong to your employee profile. Attendance was not recorded.", WarningBrush);
                return null;
            }

            SetMessage($"Fingerprint recognized: {match.Enrollment.EmployeeName}. Recording {logType}...", SuccessBrush);
            return match;
        }

        private async Task LoadSelectedEmployeeDayAsync()
        {
            if (SelectedEmployee == null)
            {
                ClearDay("No employee selected.");
                return;
            }

            var workDate = DateTime.Today;
            WorkDateText = workDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);

            try
            {
                var shift = await _dataService.GetEmployeeShiftForDateAsync(SelectedEmployee.EmployeeId, workDate);
                _currentShift = shift ?? DefaultShift;
                ScheduleText = BuildScheduleText(_currentShift, shift == null);

                var logs = await _dataService.GetEmployeeAttendanceLogsForDateAsync(SelectedEmployee.EmployeeId, workDate);
                ApplyDay(logs.ToList(), workDate);
                await LoadSelectedEmployeeDtrMonthAsync();
            }
            catch (Exception ex)
            {
                ClearDay("Unable to load this employee's attendance.");
                SetMessage($"Unable to load employee punches: {ex.Message}", ErrorBrush);
            }
        }

        private void ApplyDay(IReadOnlyList<BiometricKioskPunchDto> logs, DateTime workDate)
        {
            TodayPunches.Clear();
            foreach (var log in logs.OrderByDescending(x => x.LogTime))
            {
                TodayPunches.Add(new BiometricPunchRowVm(log));
            }

            var ordered = logs.OrderBy(x => x.LogTime).ThenBy(x => x.LogId).ToList();
            var inLogs = ordered.Where(x => IsLogType(x, "IN")).ToList();
            var outLogs = ordered.Where(x => IsLogType(x, "OUT")).ToList();

            var noon = TimeSpan.FromHours(12);
            var lunchEnd = TimeSpan.FromHours(13);

            var morningIn = inLogs.FirstOrDefault(x => x.LogTime.TimeOfDay < noon);
            var morningOut = outLogs.FirstOrDefault(x => x.LogTime.TimeOfDay < lunchEnd);
            var afternoonIn = inLogs.FirstOrDefault(x => x.LogTime.TimeOfDay >= noon);
            var afternoonOut = outLogs.LastOrDefault(x => x.LogTime.TimeOfDay >= noon);

            MorningInText = FormatPunchTime(morningIn?.LogTime);
            MorningOutText = FormatPunchTime(morningOut?.LogTime);
            AfternoonInText = FormatPunchTime(afternoonIn?.LogTime);
            AfternoonOutText = FormatPunchTime(afternoonOut?.LogTime);

            var firstIn = inLogs.FirstOrDefault()?.LogTime;
            var lastOut = outLogs.LastOrDefault()?.LogTime;
            LastPunchText = ordered.Count == 0
                ? "No punch yet"
                : $"{ordered[^1].LogType} at {ordered[^1].LogTime:hh:mm tt}";

            var startTime = workDate.Date.Add(_currentShift.StartTime);
            var endTime = workDate.Date.Add(_currentShift.EndTime);
            if (_currentShift.IsOvernight && endTime <= startTime)
            {
                endTime = endTime.AddDays(1);
            }

            var lateMinutes = firstIn.HasValue
                ? Math.Max(0, (int)Math.Ceiling((firstIn.Value - startTime.AddMinutes(_currentShift.GraceMinutes)).TotalMinutes))
                : 0;
            var undertimeMinutes = lastOut.HasValue
                ? Math.Max(0, (int)Math.Ceiling((endTime - lastOut.Value).TotalMinutes))
                : 0;

            LateText = lateMinutes > 0 ? $"-{lateMinutes} min" : "0 min";
            UndertimeText = undertimeMinutes > 0 ? $"-{undertimeMinutes} min" : "0 min";
            MinusText = lateMinutes + undertimeMinutes > 0 ? $"-{lateMinutes + undertimeMinutes} min" : "No minus";

            var missing = new List<string>();
            if (!firstIn.HasValue && DateTime.Now > startTime.AddMinutes(_currentShift.GraceMinutes))
            {
                missing.Add("time in");
            }

            if (!lastOut.HasValue && DateTime.Now > endTime)
            {
                missing.Add("time out");
            }

            MissingText = missing.Count == 0 ? "No missing punch" : $"Missing {string.Join(" and ", missing)}";
            WorkedText = FormatMinutes(CalculateWorkedMinutes(ordered));
        }

        private void ClearDay(string statusText)
        {
            TodayPunches.Clear();
            DtrRows.Clear();
            _currentShift = DefaultShift;
            ScheduleText = BuildScheduleText(DefaultShift, true);
            WorkDateText = DateTime.Today.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
            MorningInText = "-";
            MorningOutText = "-";
            AfternoonInText = "-";
            AfternoonOutText = "-";
            LateText = "0 min";
            UndertimeText = "0 min";
            MinusText = "No minus";
            MissingText = statusText;
            WorkedText = "0h 0m";
            LastPunchText = "No punch yet";
        }

        private async Task LoadSelectedEmployeeDtrMonthAsync()
        {
            if (SelectedEmployee == null)
            {
                DtrRows.Clear();
                return;
            }

            var monthLogs = await _dataService.GetEmployeeAttendanceLogsForMonthAsync(
                SelectedEmployee.EmployeeId,
                _selectedDtrMonth.Year,
                _selectedDtrMonth.Month);
            var monthRemarks = await _dataService.GetEmployeeAttendanceRemarksForMonthAsync(
                SelectedEmployee.EmployeeId,
                _selectedDtrMonth.Year,
                _selectedDtrMonth.Month);

            RebuildDtrRows(monthLogs, monthRemarks);
        }

        private void RebuildDtrRows(
            IReadOnlyList<BiometricKioskPunchDto> monthLogs,
            IReadOnlyList<AttendanceRemarkDto> monthRemarks)
        {
            DtrRows.Clear();

            var logsByDate = monthLogs
                .GroupBy(log => log.LogTime.Date)
                .ToDictionary(group => group.Key, group => group.OrderBy(x => x.LogTime).ThenBy(x => x.LogId).ToList());
            var remarksByDate = monthRemarks
                .GroupBy(remark => remark.WorkDate.Date)
                .ToDictionary(group => group.Key, group => group.ToList());

            var daysInMonth = DateTime.DaysInMonth(_selectedDtrMonth.Year, _selectedDtrMonth.Month);
            for (var day = 1; day <= daysInMonth; day++)
            {
                var workDate = new DateTime(_selectedDtrMonth.Year, _selectedDtrMonth.Month, day);
                logsByDate.TryGetValue(workDate, out var dayLogs);
                remarksByDate.TryGetValue(workDate, out var dayRemarks);

                dayLogs ??= new List<BiometricKioskPunchDto>();
                dayRemarks ??= new List<AttendanceRemarkDto>();

                var slots = ResolveDailySlots(dayLogs);
                var firstIn = dayLogs.FirstOrDefault(log => IsLogType(log, "IN"))?.LogTime;
                var lastOut = dayLogs.LastOrDefault(log => IsLogType(log, "OUT"))?.LogTime;
                var minusMinutes = CalculateMinusMinutes(workDate, firstIn, lastOut);
                var status = BuildDtrStatus(workDate, dayLogs, dayRemarks, firstIn, lastOut);

                DtrRows.Add(new BiometricDtrDayVm(
                    Day: day,
                    AmArrival: slots.MorningIn,
                    AmDeparture: slots.MorningOut,
                    PmArrival: slots.AfternoonIn,
                    PmDeparture: slots.AfternoonOut,
                    UndertimeHours: minusMinutes > 0 ? (minusMinutes / 60).ToString(CultureInfo.InvariantCulture) : string.Empty,
                    UndertimeMinutes: minusMinutes > 0 ? (minusMinutes % 60).ToString(CultureInfo.InvariantCulture) : string.Empty,
                    Status: status));
            }
        }

        private DailyPunchSlots ResolveDailySlots(IReadOnlyList<BiometricKioskPunchDto> dayLogs)
        {
            var inLogs = dayLogs.Where(x => IsLogType(x, "IN")).OrderBy(x => x.LogTime).ToList();
            var outLogs = dayLogs.Where(x => IsLogType(x, "OUT")).OrderBy(x => x.LogTime).ToList();

            var noon = TimeSpan.FromHours(12);
            var lunchEnd = TimeSpan.FromHours(13);

            var morningIn = inLogs.FirstOrDefault(x => x.LogTime.TimeOfDay < noon)?.LogTime;
            var morningOut = outLogs.FirstOrDefault(x => x.LogTime.TimeOfDay < lunchEnd)?.LogTime;
            var afternoonIn = inLogs.FirstOrDefault(x => x.LogTime.TimeOfDay >= noon)?.LogTime;
            var afternoonOut = outLogs.LastOrDefault(x => x.LogTime.TimeOfDay >= noon)?.LogTime;

            return new DailyPunchSlots(
                MorningIn: FormatDtrTime(morningIn),
                MorningOut: FormatDtrTime(morningOut),
                AfternoonIn: FormatDtrTime(afternoonIn),
                AfternoonOut: FormatDtrTime(afternoonOut));
        }

        private int CalculateMinusMinutes(DateTime workDate, DateTime? firstIn, DateTime? lastOut)
        {
            if (workDate.Date > DateTime.Today)
            {
                return 0;
            }

            var startTime = workDate.Date.Add(_currentShift.StartTime);
            var endTime = workDate.Date.Add(_currentShift.EndTime);
            if (_currentShift.IsOvernight && endTime <= startTime)
            {
                endTime = endTime.AddDays(1);
            }

            var lateMinutes = firstIn.HasValue
                ? Math.Max(0, (int)Math.Ceiling((firstIn.Value - startTime.AddMinutes(_currentShift.GraceMinutes)).TotalMinutes))
                : 0;
            var undertimeMinutes = lastOut.HasValue
                ? Math.Max(0, (int)Math.Ceiling((endTime - lastOut.Value).TotalMinutes))
                : 0;

            return lateMinutes + undertimeMinutes;
        }

        private static string BuildDtrStatus(
            DateTime workDate,
            IReadOnlyList<BiometricKioskPunchDto> dayLogs,
            IReadOnlyList<AttendanceRemarkDto> dayRemarks,
            DateTime? firstIn,
            DateTime? lastOut)
        {
            var remarkStatus = ResolveRemarkStatus(dayRemarks);
            if (!string.IsNullOrWhiteSpace(remarkStatus))
            {
                return remarkStatus;
            }

            if (workDate.DayOfWeek == DayOfWeek.Saturday)
            {
                return "SATURDAY";
            }

            if (workDate.DayOfWeek == DayOfWeek.Sunday)
            {
                return "SUNDAY";
            }

            if (workDate.Date > DateTime.Today)
            {
                return string.Empty;
            }

            if (dayLogs.Count == 0)
            {
                return "MISSING";
            }

            if (!firstIn.HasValue)
            {
                return "MISSING IN";
            }

            return !lastOut.HasValue ? "MISSING OUT" : string.Empty;
        }

        private static string ResolveRemarkStatus(IReadOnlyList<AttendanceRemarkDto> dayRemarks)
        {
            var types = dayRemarks
                .Select(remark => (remark.RemarkType ?? string.Empty).Trim().ToUpperInvariant())
                .Where(type => !string.IsNullOrWhiteSpace(type))
                .ToList();

            if (types.Contains("HOLIDAY"))
            {
                return "HOLIDAY";
            }

            if (types.Contains("TO"))
            {
                return "TRAVEL ORDER";
            }

            if (types.Contains("OB"))
            {
                return "OFFICIAL BUSINESS";
            }

            if (types.Contains("WFH"))
            {
                return "WFH";
            }

            if (types.Contains("CTO"))
            {
                return "CTO";
            }

            if (types.Contains("SUSPENDED"))
            {
                return "SUSPENDED";
            }

            return types.FirstOrDefault() ?? string.Empty;
        }

        private static int CalculateWorkedMinutes(IReadOnlyList<BiometricKioskPunchDto> orderedLogs)
        {
            DateTime? openIn = null;
            var total = 0;

            foreach (var log in orderedLogs)
            {
                if (IsLogType(log, "IN"))
                {
                    openIn = log.LogTime;
                    continue;
                }

                if (IsLogType(log, "OUT") && openIn.HasValue && log.LogTime > openIn.Value)
                {
                    total += (int)Math.Round((log.LogTime - openIn.Value).TotalMinutes);
                    openIn = null;
                }
            }

            return Math.Max(0, total);
        }

        private static bool IsLogType(BiometricKioskPunchDto log, string type) =>
            string.Equals(log.LogType, type, StringComparison.OrdinalIgnoreCase);

        private static string FormatPunchTime(DateTime? value) =>
            value.HasValue ? value.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "-";

        private static string FormatDtrTime(DateTime? value) =>
            value.HasValue ? value.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : string.Empty;

        private static string FormatMinutes(int minutes) =>
            $"{minutes / 60}h {minutes % 60}m";

        private static string BuildScheduleText(EmployeeShiftScheduleDto shift, bool isDefault)
        {
            var prefix = isDefault ? "Default schedule" : shift.ShiftName;
            return $"{prefix}: {FormatScheduleTime(shift.StartTime)} - {FormatScheduleTime(shift.EndTime)} | break {shift.BreakMinutes} min | grace {shift.GraceMinutes} min";
        }

        private static string FormatScheduleTime(TimeSpan value) =>
            DateTime.Today.Add(value).ToString("hh:mm tt", CultureInfo.InvariantCulture);

        private void UpdateClock()
        {
            var now = DateTime.Now;
            CurrentDateText = now.ToString("dddd, MMMM dd, yyyy", CultureInfo.InvariantCulture);
            CurrentTimeText = now.ToString("hh:mm:ss tt", CultureInfo.InvariantCulture);
        }

        private void SetMessage(string message, Brush brush)
        {
            ActionMessage = message;
            ActionMessageBrush = brush;
        }

        private void RaiseCommandStates()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _timeInCommand.RaiseCanExecuteChanged();
            _timeOutCommand.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            CancelActivePunch();
            _clockTimer.Stop();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed record BiometricEmployeeOption(int EmployeeId, string EmployeeNo, string EmployeeName)
    {
        public string DisplayName => $"{EmployeeNo} - {EmployeeName}";
    }

    public sealed record BiometricDeviceOption(int DeviceId, string DeviceName, string SerialNo, string Location, string IpAddress)
    {
        public string DisplayName
        {
            get
            {
                var detail = FirstUseful(Location, SerialNo, IpAddress);
                return string.IsNullOrWhiteSpace(detail)
                    ? DeviceName
                    : $"{DeviceName} - {detail}";
            }
        }

        private static string FirstUseful(params string[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value.Trim() != "-")?.Trim() ?? string.Empty;
    }

    public sealed record DailyPunchSlots(
        string MorningIn,
        string MorningOut,
        string AfternoonIn,
        string AfternoonOut);

    public sealed record BiometricDtrDayVm(
        int Day,
        string AmArrival,
        string AmDeparture,
        string PmArrival,
        string PmDeparture,
        string UndertimeHours,
        string UndertimeMinutes,
        string Status);

    public sealed class BiometricPunchRowVm
    {
        public BiometricPunchRowVm(BiometricKioskPunchDto dto)
        {
            EmployeeName = dto.EmployeeName;
            LogType = dto.LogType;
            LogDate = dto.LogTime.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
            LogTime = dto.LogTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            Source = dto.Source;
            DeviceName = string.IsNullOrWhiteSpace(dto.DeviceName) || dto.DeviceName == "-"
                ? "Kiosk"
                : dto.DeviceName;
        }

        public string EmployeeName { get; }
        public string LogType { get; }
        public string LogDate { get; }
        public string LogTime { get; }
        public string Source { get; }
        public string DeviceName { get; }
    }
}
