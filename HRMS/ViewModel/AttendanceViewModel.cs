using HRMS.Model;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public partial class AttendanceViewModel : INotifyPropertyChanged
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5B6C"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private readonly AttendanceDataService _dataService = new(DbConfig.ConnectionString);
        private readonly List<AttendanceLogVm> _allLogs = new();
        private readonly Random _enrollmentCaptureRandom = new();

        private int _totalLogs;
        private int _todayLogs;
        private int _presentToday;
        private int _incompleteLogs;
        private int _pendingAdjustments;
        private int _activeDevices;
        private string _searchText = string.Empty;
        private string _selectedSourceFilter = "All";
        private string _selectedTypeFilter = "All";
        private DateTime? _selectedDateFilter;

        private string _newDeviceName = string.Empty;
        private string _newDeviceSerial = string.Empty;
        private string _newDeviceLocation = string.Empty;
        private string _newDeviceIp = string.Empty;
        private bool _newDeviceIsActive = true;

        private int? _selectedEnrollmentEmployeeId;
        private int? _selectedEnrollmentDeviceId;
        private string _newBiometricUserId = string.Empty;
        private string _newEnrollmentStatus = "ACTIVE";
        private bool _isEnrollmentFlowVisible;
        private string _enrollmentEmployeeNo = "-";
        private string _enrollmentEmployeeName = "-";
        private string _enrollmentDepartment = "-";
        private string _enrollmentPosition = "-";
        private string _enrollmentCurrentStatus = "NOT ENROLLED";
        private string _enrollmentInstructionText = "Place finger 3 times to capture biometric template.";
        private string _enrollmentProgressText = "Capture 0/3";
        private int _enrollmentCaptureCompleted;
        private string _enrollmentQualityResult = "Waiting for first capture.";
        private string _enrollmentResultText = "No enrollment attempt yet.";

        private string _newShiftName = string.Empty;
        private string _newShiftStart = "07:00";
        private string _newShiftEnd = "17:00";
        private int _newShiftBreakMinutes = 60;
        private int _newShiftGraceMinutes = 10;
        private bool _newShiftIsOvernight;

        private int? _selectedAssignmentEmployeeId;
        private int? _selectedAssignmentShiftId;
        private DateTime _newAssignmentStartDate = DateTime.Today;
        private DateTime? _newAssignmentEndDate;
        private string _newAssignmentStatus = "ASSIGNED";
        private DateTime _newAdjustmentWorkDate = DateTime.Today;
        private string _newAdjustmentTimeInText = "07:00";
        private string _newAdjustmentTimeOutText = "17:00";
        private string _newAdjustmentReason = string.Empty;
        private string _employeeCurrentShiftSummary = "No active shift today.";
        private string _employeeWeekShiftSummary = "No shift assignment this week.";

        private string _actionMessage = "Ready";
        private Brush _actionMessageBrush = InfoBrush;
        private bool _isBusy;
        private bool _isEmployeeMode;
        private int? _currentEmployeeId;

        public int TotalLogs { get => _totalLogs; private set { _totalLogs = value; OnPropertyChanged(); } }
        public int TodayLogs
        {
            get => _todayLogs;
            private set
            {
                if (_todayLogs == value)
                {
                    return;
                }

                _todayLogs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FourthAdjustmentsCardValue));
            }
        }
        public int PresentToday { get => _presentToday; private set { _presentToday = value; OnPropertyChanged(); } }
        public int IncompleteLogs { get => _incompleteLogs; private set { _incompleteLogs = value; OnPropertyChanged(); } }
        public int PendingAdjustments
        {
            get => _pendingAdjustments;
            private set
            {
                if (_pendingAdjustments == value)
                {
                    return;
                }

                _pendingAdjustments = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAdjustmentRequests));
                OnPropertyChanged(nameof(FourthAdjustmentsCardValue));
            }
        }
        public int ActiveDevices { get => _activeDevices; private set { _activeDevices = value; OnPropertyChanged(); } }
        public string TotalLogsCardLabel => IsEmployeeMode ? "My Total Logs" : "Total Logs";
        public string TodayLogsCardLabel => IsEmployeeMode ? "My Logs Today" : "Today Logs";
        public string PresentCardLabel => IsEmployeeMode ? "Timed In Today" : "Present";
        public string IncompleteCardLabel => IsEmployeeMode ? "Missing Time Out" : "Incomplete";
        public string PendingAdjCardLabel => IsEmployeeMode ? "My Pending Adj" : "Pending Adj";
        public string ActiveDevicesCardLabel => IsEmployeeMode ? "My Active Devices" : "Active Devices";
        public string PendingAdjustmentsCardLabel => IsEmployeeMode ? "My Pending Requests" : "Pending Adjustments";
        public string ApprovedAdjustmentsCardLabel => IsEmployeeMode ? "My Approved" : "Approved";
        public string RejectedAdjustmentsCardLabel => IsEmployeeMode ? "My Rejected" : "Rejected";
        public string FourthAdjustmentsCardLabel => IsEmployeeMode ? "My Total Requests" : "Today Logs";
        public int FourthAdjustmentsCardValue => IsEmployeeMode ? TotalAdjustmentRequests : TodayLogs;
        public int TotalAdjustmentRequests => PendingAdjustments + ApprovedAdjustments + RejectedAdjustments;
        public string AdjustmentSearchHint => IsEmployeeMode
            ? "Search work date, status, or reason"
            : "Search employee, reason, or remarks";
        public string AdjustmentsPageTitle => IsEmployeeMode ? "My Attendance Adjustments" : "Attendance Adjustments";
        public string AdjustmentsPageSubtitle => IsEmployeeMode
            ? "File and track your own DTR correction requests."
            : "Approve or reject DTR correction requests from employees.";
        public string AttendancePageTitle => IsEmployeeMode ? "My Attendance Hub" : "Attendance Timekeeping Hub";
        public string AttendancePageSubtitle => IsEmployeeMode
            ? "Track your logs, assigned shifts, and monthly DTR."
            : "Biometric devices, enrollments, shifts, and assignments for DTR.";
        public string ShiftAssignmentsTabHeader => IsEmployeeMode ? "My Shift" : "Shift Assignments";
        public string DtrTabHeader => IsEmployeeMode ? "My DTR" : "DTR";
        public string ShiftAssignmentsInfoText => IsEmployeeMode
            ? "This section shows your assigned shifts. Contact Admin/HR for schedule changes."
            : "Shift assignments are managed by Admin/HR.";

        public string EmployeeCurrentShiftSummary
        {
            get => _employeeCurrentShiftSummary;
            private set
            {
                if (_employeeCurrentShiftSummary == value)
                {
                    return;
                }

                _employeeCurrentShiftSummary = value;
                OnPropertyChanged();
            }
        }

        public string EmployeeWeekShiftSummary
        {
            get => _employeeWeekShiftSummary;
            private set
            {
                if (_employeeWeekShiftSummary == value)
                {
                    return;
                }

                _employeeWeekShiftSummary = value;
                OnPropertyChanged();
            }
        }

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
                OnPropertyChanged(nameof(IsAdminOrHrMode));
                OnPropertyChanged(nameof(TotalLogsCardLabel));
                OnPropertyChanged(nameof(TodayLogsCardLabel));
                OnPropertyChanged(nameof(PresentCardLabel));
                OnPropertyChanged(nameof(IncompleteCardLabel));
                OnPropertyChanged(nameof(PendingAdjCardLabel));
                OnPropertyChanged(nameof(ActiveDevicesCardLabel));
                OnPropertyChanged(nameof(PendingAdjustmentsCardLabel));
                OnPropertyChanged(nameof(ApprovedAdjustmentsCardLabel));
                OnPropertyChanged(nameof(RejectedAdjustmentsCardLabel));
                OnPropertyChanged(nameof(FourthAdjustmentsCardLabel));
                OnPropertyChanged(nameof(FourthAdjustmentsCardValue));
                OnPropertyChanged(nameof(AdjustmentSearchHint));
                OnPropertyChanged(nameof(AdjustmentsPageTitle));
                OnPropertyChanged(nameof(AdjustmentsPageSubtitle));
                OnPropertyChanged(nameof(AttendancePageTitle));
                OnPropertyChanged(nameof(AttendancePageSubtitle));
                OnPropertyChanged(nameof(ShiftAssignmentsTabHeader));
                OnPropertyChanged(nameof(DtrTabHeader));
                OnPropertyChanged(nameof(ShiftAssignmentsInfoText));
            }
        }

        public bool IsAdminOrHrMode => !IsEmployeeMode;

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
                ApplyLogFilters();
            }
        }

        public string SelectedSourceFilter
        {
            get => _selectedSourceFilter;
            set
            {
                if (_selectedSourceFilter == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _selectedSourceFilter = value;
                OnPropertyChanged();
                ApplyLogFilters();
            }
        }

        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (_selectedTypeFilter == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _selectedTypeFilter = value;
                OnPropertyChanged();
                ApplyLogFilters();
            }
        }

        public DateTime? SelectedDateFilter
        {
            get => _selectedDateFilter;
            set
            {
                if (_selectedDateFilter == value)
                {
                    return;
                }

                _selectedDateFilter = value;
                OnPropertyChanged();
                ApplyLogFilters();
            }
        }

        public string NewDeviceName
        {
            get => _newDeviceName;
            set { if (_newDeviceName != value) { _newDeviceName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string NewDeviceSerial
        {
            get => _newDeviceSerial;
            set { if (_newDeviceSerial != value) { _newDeviceSerial = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string NewDeviceLocation
        {
            get => _newDeviceLocation;
            set { if (_newDeviceLocation != value) { _newDeviceLocation = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string NewDeviceIp
        {
            get => _newDeviceIp;
            set { if (_newDeviceIp != value) { _newDeviceIp = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public bool NewDeviceIsActive
        {
            get => _newDeviceIsActive;
            set { if (_newDeviceIsActive != value) { _newDeviceIsActive = value; OnPropertyChanged(); } }
        }

        public int? SelectedEnrollmentEmployeeId
        {
            get => _selectedEnrollmentEmployeeId;
            set
            {
                if (_selectedEnrollmentEmployeeId == value)
                {
                    return;
                }

                _selectedEnrollmentEmployeeId = value;
                OnPropertyChanged();
                _ = LoadEnrollmentEmployeeProfileAsync();
            }
        }

        public int? SelectedEnrollmentDeviceId
        {
            get => _selectedEnrollmentDeviceId;
            set { if (_selectedEnrollmentDeviceId != value) { _selectedEnrollmentDeviceId = value; OnPropertyChanged(); } }
        }

        public string NewBiometricUserId
        {
            get => _newBiometricUserId;
            set { if (_newBiometricUserId != value) { _newBiometricUserId = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string NewEnrollmentStatus
        {
            get => _newEnrollmentStatus;
            set { if (_newEnrollmentStatus != value && !string.IsNullOrWhiteSpace(value)) { _newEnrollmentStatus = value; OnPropertyChanged(); } }
        }

        public bool IsEnrollmentFlowVisible
        {
            get => _isEnrollmentFlowVisible;
            private set
            {
                if (_isEnrollmentFlowVisible == value)
                {
                    return;
                }

                _isEnrollmentFlowVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEnrollmentLauncherVisible));
            }
        }

        public bool IsEnrollmentLauncherVisible => !IsEnrollmentFlowVisible;
        public string EnrollmentEmployeeNo { get => _enrollmentEmployeeNo; private set { if (_enrollmentEmployeeNo != value) { _enrollmentEmployeeNo = value; OnPropertyChanged(); } } }
        public string EnrollmentEmployeeName { get => _enrollmentEmployeeName; private set { if (_enrollmentEmployeeName != value) { _enrollmentEmployeeName = value; OnPropertyChanged(); } } }
        public string EnrollmentDepartment { get => _enrollmentDepartment; private set { if (_enrollmentDepartment != value) { _enrollmentDepartment = value; OnPropertyChanged(); } } }
        public string EnrollmentPosition { get => _enrollmentPosition; private set { if (_enrollmentPosition != value) { _enrollmentPosition = value; OnPropertyChanged(); } } }
        public string EnrollmentCurrentStatus { get => _enrollmentCurrentStatus; private set { if (_enrollmentCurrentStatus != value) { _enrollmentCurrentStatus = value; OnPropertyChanged(); } } }
        public string EnrollmentInstructionText { get => _enrollmentInstructionText; private set { if (_enrollmentInstructionText != value) { _enrollmentInstructionText = value; OnPropertyChanged(); } } }
        public string EnrollmentProgressText { get => _enrollmentProgressText; private set { if (_enrollmentProgressText != value) { _enrollmentProgressText = value; OnPropertyChanged(); } } }
        public string EnrollmentQualityResult { get => _enrollmentQualityResult; private set { if (_enrollmentQualityResult != value) { _enrollmentQualityResult = value; OnPropertyChanged(); } } }
        public string EnrollmentResultText { get => _enrollmentResultText; private set { if (_enrollmentResultText != value) { _enrollmentResultText = value; OnPropertyChanged(); } } }
        public bool IsEnrollmentReadyToSave => _enrollmentCaptureCompleted >= 3;

        public string NewShiftName
        {
            get => _newShiftName;
            set { if (_newShiftName != value) { _newShiftName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string NewShiftStart
        {
            get => _newShiftStart;
            set { if (_newShiftStart != value) { _newShiftStart = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string NewShiftEnd
        {
            get => _newShiftEnd;
            set { if (_newShiftEnd != value) { _newShiftEnd = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public int NewShiftBreakMinutes
        {
            get => _newShiftBreakMinutes;
            set { if (_newShiftBreakMinutes != value) { _newShiftBreakMinutes = value; OnPropertyChanged(); } }
        }

        public int NewShiftGraceMinutes
        {
            get => _newShiftGraceMinutes;
            set { if (_newShiftGraceMinutes != value) { _newShiftGraceMinutes = value; OnPropertyChanged(); } }
        }

        public bool NewShiftIsOvernight
        {
            get => _newShiftIsOvernight;
            set { if (_newShiftIsOvernight != value) { _newShiftIsOvernight = value; OnPropertyChanged(); } }
        }

        public int? SelectedAssignmentEmployeeId
        {
            get => _selectedAssignmentEmployeeId;
            set { if (_selectedAssignmentEmployeeId != value) { _selectedAssignmentEmployeeId = value; OnPropertyChanged(); } }
        }

        public int? SelectedAssignmentShiftId
        {
            get => _selectedAssignmentShiftId;
            set { if (_selectedAssignmentShiftId != value) { _selectedAssignmentShiftId = value; OnPropertyChanged(); } }
        }

        public DateTime NewAssignmentStartDate
        {
            get => _newAssignmentStartDate;
            set { if (_newAssignmentStartDate != value) { _newAssignmentStartDate = value; OnPropertyChanged(); } }
        }

        public DateTime? NewAssignmentEndDate
        {
            get => _newAssignmentEndDate;
            set { if (_newAssignmentEndDate != value) { _newAssignmentEndDate = value; OnPropertyChanged(); } }
        }

        public string NewAssignmentStatus
        {
            get => _newAssignmentStatus;
            set { if (_newAssignmentStatus != value && !string.IsNullOrWhiteSpace(value)) { _newAssignmentStatus = value; OnPropertyChanged(); } }
        }

        public DateTime NewAdjustmentWorkDate
        {
            get => _newAdjustmentWorkDate;
            set
            {
                if (_newAdjustmentWorkDate == value)
                {
                    return;
                }

                _newAdjustmentWorkDate = value;
                OnPropertyChanged();
            }
        }

        public string NewAdjustmentTimeInText
        {
            get => _newAdjustmentTimeInText;
            set
            {
                if (_newAdjustmentTimeInText == value)
                {
                    return;
                }

                _newAdjustmentTimeInText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string NewAdjustmentTimeOutText
        {
            get => _newAdjustmentTimeOutText;
            set
            {
                if (_newAdjustmentTimeOutText == value)
                {
                    return;
                }

                _newAdjustmentTimeOutText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string NewAdjustmentReason
        {
            get => _newAdjustmentReason;
            set
            {
                if (_newAdjustmentReason == value)
                {
                    return;
                }

                _newAdjustmentReason = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string ActionMessage
        {
            get => _actionMessage;
            private set
            {
                if (_actionMessage == value)
                {
                    return;
                }

                _actionMessage = value;
                OnPropertyChanged();
            }
        }

        public Brush ActionMessageBrush
        {
            get => _actionMessageBrush;
            private set
            {
                if (_actionMessageBrush == value)
                {
                    return;
                }

                _actionMessageBrush = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> SourceFilters { get; } = new()
        {
            "All",
            "BIOMETRIC",
            "MANUAL",
            "IMPORT"
        };

        public ObservableCollection<string> TypeFilters { get; } = new()
        {
            "All",
            "IN",
            "OUT",
            "BREAK_IN",
            "BREAK_OUT"
        };

        public ObservableCollection<string> EnrollmentStatuses { get; } = new()
        {
            "ACTIVE",
            "INACTIVE"
        };

        public ObservableCollection<string> AssignmentStatuses { get; } = new()
        {
            "ASSIGNED",
            "CANCELLED"
        };

        public ObservableCollection<AttendanceLogVm> Logs { get; } = new();
        public ObservableCollection<AttendanceAdjustmentVm> Adjustments { get; } = new();
        public ObservableCollection<BiometricDeviceVm> BiometricDevices { get; } = new();
        public ObservableCollection<BiometricEnrollmentVm> BiometricEnrollments { get; } = new();
        public ObservableCollection<ShiftVm> Shifts { get; } = new();
        public ObservableCollection<ShiftAssignmentVm> ShiftAssignments { get; } = new();
        public ObservableCollection<LookupOptionVm> EmployeeOptions { get; } = new();
        public ObservableCollection<LookupOptionVm> DeviceOptions { get; } = new();
        public ObservableCollection<LookupOptionVm> ShiftOptions { get; } = new();
        public ObservableCollection<string> EnrollmentAuditLogs { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand DeleteLogCommand { get; }
        public ICommand ApproveAdjustmentCommand { get; }
        public ICommand RejectAdjustmentCommand { get; }
        public ICommand ClearDateFilterCommand { get; }
        public ICommand AddDeviceCommand { get; }
        public ICommand ToggleDeviceActiveCommand { get; }
        public ICommand SyncDeviceCommand { get; }
        public ICommand AddEnrollmentCommand { get; }
        public ICommand OpenEnrollmentFlowCommand { get; }
        public ICommand BackFromEnrollmentFlowCommand { get; }
        public ICommand CaptureEnrollmentCommand { get; }
        public ICommand ToggleEnrollmentStatusCommand { get; }
        public ICommand DeleteEnrollmentCommand { get; }
        public ICommand AddShiftCommand { get; }
        public ICommand DeleteShiftCommand { get; }
        public ICommand AddAssignmentCommand { get; }
        public ICommand ToggleAssignmentStatusCommand { get; }
        public ICommand DeleteAssignmentCommand { get; }
        public ICommand SubmitAdjustmentRequestCommand { get; }
        public ICommand CancelMyAdjustmentCommand { get; }

        public AttendanceViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
            DeleteLogCommand = new AsyncRelayCommand(DeleteLogAsync);
            ApproveAdjustmentCommand = new AsyncRelayCommand(ApproveAdjustmentAsync);
            RejectAdjustmentCommand = new AsyncRelayCommand(RejectAdjustmentAsync);
            ClearDateFilterCommand = new AsyncRelayCommand(_ =>
            {
                SelectedDateFilter = null;
                return Task.CompletedTask;
            });
            AddDeviceCommand = new AsyncRelayCommand(_ => AddDeviceAsync());
            ToggleDeviceActiveCommand = new AsyncRelayCommand(ToggleDeviceActiveAsync);
            SyncDeviceCommand = new AsyncRelayCommand(SyncDeviceAsync);
            OpenEnrollmentFlowCommand = new AsyncRelayCommand(_ => OpenEnrollmentFlowAsync());
            BackFromEnrollmentFlowCommand = new AsyncRelayCommand(_ => BackFromEnrollmentFlowAsync());
            CaptureEnrollmentCommand = new AsyncRelayCommand(_ => CaptureEnrollmentAsync());
            AddEnrollmentCommand = new AsyncRelayCommand(_ => AddEnrollmentAsync());
            ToggleEnrollmentStatusCommand = new AsyncRelayCommand(ToggleEnrollmentStatusAsync);
            DeleteEnrollmentCommand = new AsyncRelayCommand(DeleteEnrollmentAsync);
            AddShiftCommand = new AsyncRelayCommand(_ => AddShiftAsync());
            DeleteShiftCommand = new AsyncRelayCommand(DeleteShiftAsync);
            AddAssignmentCommand = new AsyncRelayCommand(_ => AddAssignmentAsync());
            ToggleAssignmentStatusCommand = new AsyncRelayCommand(ToggleAssignmentStatusAsync);
            DeleteAssignmentCommand = new AsyncRelayCommand(DeleteAssignmentAsync);
            SubmitAdjustmentRequestCommand = new AsyncRelayCommand(_ => SubmitAdjustmentRequestAsync());
            CancelMyAdjustmentCommand = new AsyncRelayCommand(CancelMyAdjustmentAsync);
            InitializeAdjustmentsAdmin();
            InitializeDtr();
            InitializeLogsAdmin();

            _ = RefreshAsync();
        }

        public async Task ApplyCurrentUserScopeAsync(int userId, string? roleName)
        {
            IsEmployeeMode = string.Equals(roleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);
            _currentEmployeeId = null;

            if (userId > 0)
            {
                try
                {
                    _currentEmployeeId = await _dataService.GetEmployeeIdByUserIdAsync(userId);
                }
                catch
                {
                    _currentEmployeeId = null;
                }
            }

            if (IsEmployeeMode && _currentEmployeeId.HasValue && _currentEmployeeId.Value > 0)
            {
                SelectedDtrEmployeeId = _currentEmployeeId.Value;
                SelectedManualLogEmployeeId = _currentEmployeeId.Value;
            }

            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetMessage("Refreshing attendance module...", InfoBrush);

            try
            {
                var statsTask = _dataService.GetStatsAsync();
                var logsTask = _dataService.GetRecentLogsAsync(300);
                var adjustmentsTask = _dataService.GetPendingAdjustmentsAsync(300, null);
                var adjustmentCountsTask = _dataService.GetAdjustmentStatusCountsAsync();
                var devicesTask = _dataService.GetBiometricDevicesAsync();
                var employeesTask = _dataService.GetEmployeesLookupAsync();
                var enrollmentsTask = _dataService.GetBiometricEnrollmentsAsync(600);
                var shiftsTask = _dataService.GetShiftsAsync();
                var assignmentsTask = _dataService.GetShiftAssignmentsAsync(700);

                var stats = await statsTask;
                var logs = await logsTask;
                var adjustments = await adjustmentsTask;
                var adjustmentCounts = await adjustmentCountsTask;
                var devices = await devicesTask;
                var employees = await employeesTask;
                var enrollments = await enrollmentsTask;
                var shifts = await shiftsTask;
                var assignments = await assignmentsTask;

                var scopedEmployee = IsEmployeeMode && _currentEmployeeId.HasValue
                    ? employees.FirstOrDefault(x => x.EmployeeId == _currentEmployeeId.Value)
                    : null;
                var scopedEmployeeNo = scopedEmployee?.EmployeeNo;

                var effectiveLogs = IsEmployeeMode && !string.IsNullOrWhiteSpace(scopedEmployeeNo)
                    ? logs.Where(x => string.Equals(x.EmployeeNo, scopedEmployeeNo, StringComparison.OrdinalIgnoreCase)).ToList()
                    : logs;

                var effectiveAdjustments = IsEmployeeMode && !string.IsNullOrWhiteSpace(scopedEmployeeNo)
                    ? adjustments.Where(x => string.Equals(x.EmployeeNo, scopedEmployeeNo, StringComparison.OrdinalIgnoreCase)).ToList()
                    : adjustments;

                var effectiveAssignments = IsEmployeeMode && _currentEmployeeId.HasValue
                    ? assignments.Where(x => x.EmployeeId == _currentEmployeeId.Value).ToList()
                    : assignments;
                var effectiveEnrollments = IsEmployeeMode && _currentEmployeeId.HasValue
                    ? enrollments.Where(x => x.EmployeeId == _currentEmployeeId.Value).ToList()
                    : enrollments;

                if (IsEmployeeMode)
                {
                    var today = DateTime.Today;
                    var weekEnd = today.AddDays(6);
                    var assignedNow = effectiveAssignments.FirstOrDefault(x =>
                        string.Equals(x.Status, "ASSIGNED", StringComparison.OrdinalIgnoreCase) &&
                        x.StartDate.Date <= today &&
                        (!x.EndDate.HasValue || x.EndDate.Value.Date >= today));
                    var assignedThisWeek = effectiveAssignments.Where(x =>
                        string.Equals(x.Status, "ASSIGNED", StringComparison.OrdinalIgnoreCase) &&
                        x.StartDate.Date <= weekEnd &&
                        (!x.EndDate.HasValue || x.EndDate.Value.Date >= today)).ToList();

                    EmployeeCurrentShiftSummary = assignedNow is null
                        ? "No active shift today."
                        : $"{assignedNow.ShiftName} | {assignedNow.StartDate:MMM dd, yyyy} to {(assignedNow.EndDate.HasValue ? assignedNow.EndDate.Value.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) : "Open")}";
                    EmployeeWeekShiftSummary = assignedThisWeek.Count == 0
                        ? "No shift assignment this week."
                        : $"{assignedThisWeek.Count} active assignment(s) this week.";
                }
                else
                {
                    EmployeeCurrentShiftSummary = "-";
                    EmployeeWeekShiftSummary = "-";
                }

                if (IsEmployeeMode)
                {
                    var todayLogs = effectiveLogs.Where(x => x.LogTime.Date == DateTime.Today).ToList();
                    var hasTodayIn = todayLogs.Any(x => string.Equals(x.LogType, "IN", StringComparison.OrdinalIgnoreCase));
                    var hasTodayOut = todayLogs.Any(x => string.Equals(x.LogType, "OUT", StringComparison.OrdinalIgnoreCase));

                    TotalLogs = effectiveLogs.Count;
                    TodayLogs = todayLogs.Count;
                    PresentToday = hasTodayIn ? 1 : 0;
                    IncompleteLogs = hasTodayIn && !hasTodayOut ? 1 : 0;
                    PendingAdjustments = effectiveAdjustments.Count(x => string.Equals(x.Status, "PENDING", StringComparison.OrdinalIgnoreCase));
                    var activeDeviceIds = devices
                        .Where(x => x.IsActive)
                        .Select(x => x.DeviceId)
                        .ToHashSet();
                    ActiveDevices = effectiveEnrollments.Count(x =>
                        string.Equals(x.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) &&
                        x.DeviceId.HasValue &&
                        activeDeviceIds.Contains(x.DeviceId.Value));
                }
                else
                {
                    TotalLogs = stats.TotalLogs;
                    TodayLogs = stats.TodayLogs;
                    PresentToday = stats.PresentToday;
                    IncompleteLogs = stats.IncompleteLogs;
                    PendingAdjustments = adjustmentCounts.Pending;
                    ActiveDevices = stats.ActiveDevices;
                }

                _allLogs.Clear();
                foreach (var log in effectiveLogs)
                {
                    _allLogs.Add(new AttendanceLogVm(
                        log.LogId,
                        log.EmployeeNo,
                        log.EmployeeName,
                        log.LogTime,
                        log.LogType,
                        log.Source,
                        log.DeviceName));
                }

                var effectiveAdjustmentCounts = IsEmployeeMode
                    ? new AttendanceAdjustmentCountsDto(
                        Pending: effectiveAdjustments.Count(x => string.Equals(x.Status, "PENDING", StringComparison.OrdinalIgnoreCase)),
                        Approved: effectiveAdjustments.Count(x => string.Equals(x.Status, "APPROVED", StringComparison.OrdinalIgnoreCase)),
                        Rejected: effectiveAdjustments.Count(x => string.Equals(x.Status, "REJECTED", StringComparison.OrdinalIgnoreCase)))
                    : adjustmentCounts;

                RebuildAdjustments(effectiveAdjustments, effectiveAdjustmentCounts);

                BiometricDevices.Clear();
                DeviceOptions.Clear();
                DeviceOptions.Add(new LookupOptionVm(0, "No device"));
                foreach (var device in devices)
                {
                    BiometricDevices.Add(new BiometricDeviceVm(
                        device.DeviceId,
                        device.DeviceName,
                        device.SerialNo,
                        device.Location,
                        device.IpAddress,
                        device.IsActive,
                        device.LastSyncAt));

                    DeviceOptions.Add(new LookupOptionVm(device.DeviceId, device.DeviceName));
                }

                EmployeeOptions.Clear();
                DtrEmployeeOptions.Clear();

                if (IsEmployeeMode && scopedEmployee != null)
                {
                    var selfLabel = $"{scopedEmployee.EmployeeNo} - {scopedEmployee.EmployeeName}";
                    EmployeeOptions.Add(new LookupOptionVm(scopedEmployee.EmployeeId, selfLabel));
                    DtrEmployeeOptions.Add(new LookupOptionVm(scopedEmployee.EmployeeId, selfLabel));
                    SelectedDtrEmployeeId = scopedEmployee.EmployeeId;
                }
                else
                {
                    DtrEmployeeOptions.Add(new LookupOptionVm(0, "All employees"));
                    foreach (var employee in employees)
                    {
                        var label = $"{employee.EmployeeNo} - {employee.EmployeeName}";
                        EmployeeOptions.Add(new LookupOptionVm(employee.EmployeeId, label));
                        DtrEmployeeOptions.Add(new LookupOptionVm(employee.EmployeeId, label));
                    }

                    if (!SelectedDtrEmployeeId.HasValue || !DtrEmployeeOptions.Any(x => x.Id == SelectedDtrEmployeeId.Value))
                    {
                        SelectedDtrEmployeeId = 0;
                    }
                }

                if (!SelectedEnrollmentEmployeeId.HasValue || EmployeeOptions.All(x => x.Id != SelectedEnrollmentEmployeeId.Value))
                {
                    SelectedEnrollmentEmployeeId = null;
                    ClearEnrollmentEmployeeProfile();
                }

                SyncLogsAdminLookups();

                BiometricEnrollments.Clear();
                foreach (var enrollment in effectiveEnrollments)
                {
                    BiometricEnrollments.Add(new BiometricEnrollmentVm(
                        enrollment.EnrollmentId,
                        enrollment.EmployeeNo,
                        enrollment.EmployeeName,
                        enrollment.BiometricUserId,
                        enrollment.DeviceName,
                        enrollment.Status,
                        enrollment.CreatedAt));
                }

                await LoadEnrollmentEmployeeProfileAsync();

                Shifts.Clear();
                ShiftOptions.Clear();
                foreach (var shift in shifts)
                {
                    Shifts.Add(new ShiftVm(
                        shift.ShiftId,
                        shift.ShiftName,
                        shift.StartTime,
                        shift.EndTime,
                        shift.BreakMinutes,
                        shift.GraceMinutes,
                        shift.IsOvernight));

                    ShiftOptions.Add(new LookupOptionVm(shift.ShiftId, shift.ShiftName));
                }

                ShiftAssignments.Clear();
                foreach (var assignment in effectiveAssignments)
                {
                    ShiftAssignments.Add(new ShiftAssignmentVm(
                        assignment.AssignmentId,
                        assignment.EmployeeNo,
                        assignment.EmployeeName,
                        assignment.ShiftName,
                        assignment.StartDate,
                        assignment.EndDate,
                        assignment.Status));
                }

                ApplyLogFilters();
                await LoadDtrAsync(silent: true);
                SetMessage("Attendance data updated.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to refresh attendance: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteLogAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Deleting attendance logs"))
            {
                return;
            }

            if (parameter is not AttendanceLogVm log)
            {
                return;
            }

            try
            {
                await _dataService.DeleteLogAsync(log.LogId);
                await RefreshAsync();
                SetMessage($"Deleted attendance log for {log.EmployeeNo}.", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceLogDeleted");
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                SetMessage("Cannot delete this log because it is referenced by another record.", ErrorBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Delete failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ApproveAdjustmentAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Approving adjustments"))
            {
                return;
            }

            await UpdateAdjustmentStatusAsync(parameter, "APPROVED");
        }

        private async Task RejectAdjustmentAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Rejecting adjustments"))
            {
                return;
            }

            await UpdateAdjustmentStatusAsync(parameter, "REJECTED");
        }

        private async Task UpdateAdjustmentStatusAsync(object? parameter, string status)
        {
            var adjustment = parameter as AttendanceAdjustmentVm ?? SelectedAdjustment;
            if (adjustment is null)
            {
                SetMessage("Select an adjustment request first.", ErrorBrush);
                return;
            }

            var decisionReason = (AdjustmentDecisionReason ?? string.Empty).Trim();
            if (string.Equals(status, "REJECTED", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(decisionReason))
            {
                SetMessage("Reason is required when rejecting a request.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.UpdateAdjustmentStatusAsync(
                    adjustment.AdjustmentId,
                    status,
                    decisionReason,
                    _currentUserId > 0 ? _currentUserId : null);
                AdjustmentDecisionReason = string.Empty;
                await RefreshAsync();
                SetMessage($"Adjustment {adjustment.AdjustmentId} set to {status}.", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceAdjustmentUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to update adjustment: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SubmitAdjustmentRequestAsync()
        {
            if (!IsEmployeeMode)
            {
                SetMessage("Submitting adjustment requests is available in employee mode.", ErrorBrush);
                return;
            }

            if (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0 || _currentUserId <= 0)
            {
                SetMessage("Your employee profile is not linked to this account.", ErrorBrush);
                return;
            }

            var reason = (NewAdjustmentReason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                SetMessage("Please provide a reason for your adjustment request.", ErrorBrush);
                return;
            }

            DateTime? requestedIn = null;
            DateTime? requestedOut = null;
            var hasIn = !string.IsNullOrWhiteSpace(NewAdjustmentTimeInText);
            var hasOut = !string.IsNullOrWhiteSpace(NewAdjustmentTimeOutText);
            var inTime = default(TimeSpan);
            var outTime = default(TimeSpan);

            if (hasIn && !TryParseTime(NewAdjustmentTimeInText, out inTime))
            {
                SetMessage("Invalid Time In format. Use HH:mm (example: 07:05).", ErrorBrush);
                return;
            }

            if (hasOut && !TryParseTime(NewAdjustmentTimeOutText, out outTime))
            {
                SetMessage("Invalid Time Out format. Use HH:mm (example: 17:00).", ErrorBrush);
                return;
            }

            if (hasIn)
            {
                requestedIn = NewAdjustmentWorkDate.Date + inTime;
            }

            if (hasOut)
            {
                requestedOut = NewAdjustmentWorkDate.Date + outTime;
            }

            if (!requestedIn.HasValue && !requestedOut.HasValue)
            {
                SetMessage("Provide at least Time In or Time Out.", ErrorBrush);
                return;
            }

            if (requestedIn.HasValue && requestedOut.HasValue && requestedOut.Value <= requestedIn.Value)
            {
                SetMessage("Time Out must be later than Time In.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.CreateAdjustmentAsync(
                    _currentEmployeeId.Value,
                    NewAdjustmentWorkDate.Date,
                    requestedIn,
                    requestedOut,
                    reason,
                    _currentUserId);

                NewAdjustmentReason = string.Empty;
                await RefreshAsync();
                SetMessage("Adjustment request submitted.", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceAdjustmentRequested");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to submit adjustment request: {ex.Message}", ErrorBrush);
            }
        }

        private async Task CancelMyAdjustmentAsync(object? parameter)
        {
            if (!IsEmployeeMode)
            {
                SetMessage("Cancel request is available in employee mode.", ErrorBrush);
                return;
            }

            if (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0)
            {
                SetMessage("Your employee profile is not linked to this account.", ErrorBrush);
                return;
            }

            var adjustment = parameter as AttendanceAdjustmentVm ?? SelectedAdjustment;
            if (adjustment is null)
            {
                SetMessage("Select a request first.", ErrorBrush);
                return;
            }

            if (!string.Equals(adjustment.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                SetMessage("Only pending requests can be cancelled.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.DeleteOwnPendingAdjustmentAsync(adjustment.AdjustmentId, _currentEmployeeId.Value);
                await RefreshAsync();
                SetMessage("Pending request cancelled.", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceAdjustmentCancelled");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to cancel request: {ex.Message}", ErrorBrush);
            }
        }

        private async Task AddDeviceAsync()
        {
            if (!EnsureAdminOrHrAccess("Managing biometric devices"))
            {
                return;
            }

            try
            {
                await _dataService.AddDeviceAsync(NewDeviceName, NewDeviceSerial, NewDeviceLocation, NewDeviceIp, NewDeviceIsActive);
                NewDeviceName = string.Empty;
                NewDeviceSerial = string.Empty;
                NewDeviceLocation = string.Empty;
                NewDeviceIp = string.Empty;
                NewDeviceIsActive = true;
                await RefreshAsync();
                SetMessage("Biometric device saved.", SuccessBrush);
                SystemRefreshBus.Raise("BiometricDeviceSaved");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save device: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ToggleDeviceActiveAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Managing biometric devices"))
            {
                return;
            }

            if (parameter is not BiometricDeviceVm device)
            {
                return;
            }

            try
            {
                await _dataService.ToggleDeviceActiveAsync(device.DeviceId, !device.IsActive);
                await RefreshAsync();
                SetMessage($"Device {device.DeviceName} status updated.", SuccessBrush);
                SystemRefreshBus.Raise("BiometricDeviceStatusUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to update device: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SyncDeviceAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Syncing biometric devices"))
            {
                return;
            }

            if (parameter is not BiometricDeviceVm device)
            {
                return;
            }

            try
            {
                await _dataService.MarkDeviceSyncedNowAsync(device.DeviceId);
                await RefreshAsync();
                SetMessage($"Device {device.DeviceName} marked synced.", SuccessBrush);
                SystemRefreshBus.Raise("BiometricDeviceSynced");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to sync device: {ex.Message}", ErrorBrush);
            }
        }

        private async Task OpenEnrollmentFlowAsync()
        {
            if (!EnsureAdminOrHrAccess("Managing biometric enrollments"))
            {
                return;
            }

            IsEnrollmentFlowVisible = true;
            ResetEnrollmentCaptureState(clearAuditLogs: true);
            EnrollmentResultText = "Ready to capture.";
            NewEnrollmentStatus = "ACTIVE";

            if ((!SelectedEnrollmentEmployeeId.HasValue || SelectedEnrollmentEmployeeId.Value <= 0) && EmployeeOptions.Count > 0)
            {
                SelectedEnrollmentEmployeeId = EmployeeOptions[0].Id;
            }

            if ((!SelectedEnrollmentDeviceId.HasValue || SelectedEnrollmentDeviceId.Value <= 0) && DeviceOptions.Any(x => x.Id > 0))
            {
                SelectedEnrollmentDeviceId = DeviceOptions.First(x => x.Id > 0).Id;
            }

            await LoadEnrollmentEmployeeProfileAsync();
            AddEnrollmentAudit("Enrollment flow opened.");
        }

        private Task BackFromEnrollmentFlowAsync()
        {
            if (!EnsureAdminOrHrAccess("Managing biometric enrollments"))
            {
                return Task.CompletedTask;
            }

            IsEnrollmentFlowVisible = false;
            ResetEnrollmentCaptureState(clearAuditLogs: true);
            EnrollmentResultText = "No enrollment attempt yet.";
            SetMessage("Biometric enrollment flow closed.", InfoBrush);
            return Task.CompletedTask;
        }

        private async Task CaptureEnrollmentAsync()
        {
            if (!EnsureAdminOrHrAccess("Managing biometric enrollments"))
            {
                return;
            }

            if (!IsEnrollmentFlowVisible)
            {
                SetMessage("Open the enrollment flow first.", ErrorBrush);
                return;
            }

            if (!SelectedEnrollmentEmployeeId.HasValue || SelectedEnrollmentEmployeeId.Value <= 0)
            {
                SetMessage("Select an employee first.", ErrorBrush);
                return;
            }

            if (!SelectedEnrollmentDeviceId.HasValue || SelectedEnrollmentDeviceId.Value <= 0)
            {
                SetMessage("Select a biometric device before capture.", ErrorBrush);
                return;
            }

            if (_enrollmentCaptureCompleted >= 3)
            {
                EnrollmentQualityResult = "Capture already complete (3/3).";
                EnrollmentResultText = "Ready to generate template and save enrollment.";
                return;
            }

            await LoadEnrollmentEmployeeProfileAsync();

            var targetCaptureIndex = _enrollmentCaptureCompleted + 1;
            var qualityScore = _enrollmentCaptureRandom.Next(45, 101);
            const int minimumQuality = 65;
            if (qualityScore < minimumQuality)
            {
                EnrollmentQualityResult = $"Capture {targetCaptureIndex}/3 rejected (quality {qualityScore}%). Repeat this step.";
                EnrollmentResultText = "Low quality capture. Please scan the same finger again.";
                AddEnrollmentAudit($"Capture {targetCaptureIndex}/3 failed quality check ({qualityScore}%).");
                SetMessage("Low-quality capture detected. Repeat the scan.", ErrorBrush);
                return;
            }

            _enrollmentCaptureCompleted++;
            EnrollmentProgressText = $"Capture {_enrollmentCaptureCompleted}/3";
            EnrollmentQualityResult = $"Capture {_enrollmentCaptureCompleted}/3 accepted (quality {qualityScore}%).";
            AddEnrollmentAudit($"Capture {_enrollmentCaptureCompleted}/3 passed quality check ({qualityScore}%).");
            OnPropertyChanged(nameof(IsEnrollmentReadyToSave));

            if (_enrollmentCaptureCompleted >= 3)
            {
                EnrollmentResultText = "3/3 captures completed. Template can now be generated and saved.";
                AddEnrollmentAudit("All 3 captures completed successfully.");
                SetMessage("Capture complete. Click Complete Enrollment.", SuccessBrush);
                return;
            }

            EnrollmentResultText = $"Capture complete. Next step: {_enrollmentCaptureCompleted + 1}/3.";
        }

        private async Task AddEnrollmentAsync()
        {
            if (!EnsureAdminOrHrAccess("Managing biometric enrollments"))
            {
                return;
            }

            if (!SelectedEnrollmentEmployeeId.HasValue || SelectedEnrollmentEmployeeId.Value <= 0)
            {
                SetMessage("Select an employee for enrollment.", ErrorBrush);
                return;
            }

            if (!SelectedEnrollmentDeviceId.HasValue || SelectedEnrollmentDeviceId.Value <= 0)
            {
                SetMessage("Select a biometric device for enrollment.", ErrorBrush);
                return;
            }

            if (_enrollmentCaptureCompleted < 3)
            {
                SetMessage("Capture must reach 3/3 before saving enrollment.", ErrorBrush);
                return;
            }

            try
            {
                await LoadEnrollmentEmployeeProfileAsync();
                if (string.IsNullOrWhiteSpace(NewBiometricUserId))
                {
                    NewBiometricUserId = BuildDefaultBiometricUserId(EnrollmentEmployeeNo);
                }

                await _dataService.AddBiometricEnrollmentAsync(
                    SelectedEnrollmentEmployeeId.Value,
                    NewBiometricUserId,
                    SelectedEnrollmentDeviceId,
                    "ACTIVE");

                EnrollmentResultText = "Enrollment successful. Template saved to device and database mapping created.";
                EnrollmentCurrentStatus = "ACTIVE";
                AddEnrollmentAudit("Template generated from 3 captures.");
                AddEnrollmentAudit($"Enrollment saved to biometric_enrollments (employee_id={SelectedEnrollmentEmployeeId.Value}, biometric_user_id={NewBiometricUserId}, device_id={SelectedEnrollmentDeviceId.Value}, status=ACTIVE).");
                AddEnrollmentAudit("Audit log entry recorded.");
                ResetEnrollmentCaptureState(clearAuditLogs: false);

                await RefreshAsync();
                SetMessage("Biometric enrollment saved.", SuccessBrush);
                SystemRefreshBus.Raise("BiometricEnrollmentSaved");
            }
            catch (Exception ex)
            {
                EnrollmentResultText = "Enrollment failed.";
                AddEnrollmentAudit($"Enrollment failed: {ex.Message}");
                SetMessage($"Unable to save enrollment: {ex.Message}", ErrorBrush);
            }
        }

        private async Task LoadEnrollmentEmployeeProfileAsync()
        {
            if (!SelectedEnrollmentEmployeeId.HasValue || SelectedEnrollmentEmployeeId.Value <= 0)
            {
                ClearEnrollmentEmployeeProfile();
                return;
            }

            try
            {
                var profile = await _dataService.GetEnrollmentEmployeeProfileAsync(SelectedEnrollmentEmployeeId.Value);
                if (profile is null)
                {
                    ClearEnrollmentEmployeeProfile();
                    return;
                }

                EnrollmentEmployeeNo = profile.EmployeeNo;
                EnrollmentEmployeeName = profile.EmployeeName;
                EnrollmentDepartment = profile.DepartmentName;
                EnrollmentPosition = profile.PositionName;
                EnrollmentCurrentStatus = profile.CurrentEnrollmentStatus;

                if (string.IsNullOrWhiteSpace(NewBiometricUserId))
                {
                    NewBiometricUserId = BuildDefaultBiometricUserId(profile.EmployeeNo);
                }
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to load employee profile: {ex.Message}", ErrorBrush);
            }
        }

        private void ResetEnrollmentCaptureState(bool clearAuditLogs)
        {
            _enrollmentCaptureCompleted = 0;
            EnrollmentInstructionText = "Place finger 3 times to capture biometric template.";
            EnrollmentProgressText = "Capture 0/3";
            EnrollmentQualityResult = "Waiting for first capture.";
            OnPropertyChanged(nameof(IsEnrollmentReadyToSave));
            if (clearAuditLogs)
            {
                EnrollmentAuditLogs.Clear();
            }
        }

        private void ClearEnrollmentEmployeeProfile()
        {
            EnrollmentEmployeeNo = "-";
            EnrollmentEmployeeName = "-";
            EnrollmentDepartment = "-";
            EnrollmentPosition = "-";
            EnrollmentCurrentStatus = "NOT ENROLLED";
            NewBiometricUserId = string.Empty;
        }

        private string BuildDefaultBiometricUserId(string employeeNo)
        {
            if (string.IsNullOrWhiteSpace(employeeNo) || employeeNo == "-")
            {
                return "BIO-NEW";
            }

            return $"BIO-{employeeNo.Trim()}";
        }

        private void AddEnrollmentAudit(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            EnrollmentAuditLogs.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message.Trim()}");
            const int maxAuditEntries = 40;
            while (EnrollmentAuditLogs.Count > maxAuditEntries)
            {
                EnrollmentAuditLogs.RemoveAt(EnrollmentAuditLogs.Count - 1);
            }
        }

        private async Task ToggleEnrollmentStatusAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Managing biometric enrollments"))
            {
                return;
            }

            if (parameter is not BiometricEnrollmentVm enrollment)
            {
                return;
            }

            try
            {
                var nextStatus = string.Equals(enrollment.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                    ? "INACTIVE"
                    : "ACTIVE";

                await _dataService.UpdateBiometricEnrollmentStatusAsync(enrollment.EnrollmentId, nextStatus);
                await RefreshAsync();
                SetMessage($"Enrollment {enrollment.BiometricUserId} set to {nextStatus}.", SuccessBrush);
                SystemRefreshBus.Raise("BiometricEnrollmentStatusUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to update enrollment: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DeleteEnrollmentAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Managing biometric enrollments"))
            {
                return;
            }

            if (parameter is not BiometricEnrollmentVm enrollment)
            {
                return;
            }

            try
            {
                await _dataService.DeleteBiometricEnrollmentAsync(enrollment.EnrollmentId);
                await RefreshAsync();
                SetMessage($"Enrollment {enrollment.BiometricUserId} removed.", SuccessBrush);
                SystemRefreshBus.Raise("BiometricEnrollmentDeleted");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to delete enrollment: {ex.Message}", ErrorBrush);
            }
        }

        private async Task AddShiftAsync()
        {
            if (!EnsureAdminOrHrAccess("Managing shifts"))
            {
                return;
            }

            try
            {
                if (!TryParseTime(NewShiftStart, out var start) || !TryParseTime(NewShiftEnd, out var end))
                {
                    SetMessage("Shift time format should be HH:mm.", ErrorBrush);
                    return;
                }

                await _dataService.AddShiftAsync(
                    NewShiftName,
                    start,
                    end,
                    NewShiftBreakMinutes,
                    NewShiftGraceMinutes,
                    NewShiftIsOvernight);

                NewShiftName = string.Empty;
                NewShiftStart = "07:00";
                NewShiftEnd = "17:00";
                NewShiftBreakMinutes = 60;
                NewShiftGraceMinutes = 10;
                NewShiftIsOvernight = false;

                await RefreshAsync();
                SetMessage("Shift saved.", SuccessBrush);
                SystemRefreshBus.Raise("ShiftSaved");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save shift: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DeleteShiftAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Managing shifts"))
            {
                return;
            }

            if (parameter is not ShiftVm shift)
            {
                return;
            }

            try
            {
                await _dataService.DeleteShiftAsync(shift.ShiftId);
                await RefreshAsync();
                SetMessage($"Shift {shift.ShiftName} deleted.", SuccessBrush);
                SystemRefreshBus.Raise("ShiftDeleted");
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                SetMessage("Cannot delete shift: it is used by assignments.", ErrorBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to delete shift: {ex.Message}", ErrorBrush);
            }
        }

        private async Task AddAssignmentAsync()
        {
            if (!EnsureAdminOrHrAccess("Managing shift assignments"))
            {
                return;
            }

            if (!SelectedAssignmentEmployeeId.HasValue || !SelectedAssignmentShiftId.HasValue)
            {
                SetMessage("Select employee and shift before saving assignment.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.AddShiftAssignmentAsync(
                    SelectedAssignmentEmployeeId.Value,
                    SelectedAssignmentShiftId.Value,
                    NewAssignmentStartDate,
                    NewAssignmentEndDate,
                    NewAssignmentStatus);

                SelectedAssignmentEmployeeId = null;
                SelectedAssignmentShiftId = null;
                NewAssignmentStartDate = DateTime.Today;
                NewAssignmentEndDate = null;
                NewAssignmentStatus = "ASSIGNED";

                await RefreshAsync();
                SetMessage("Shift assignment saved.", SuccessBrush);
                SystemRefreshBus.Raise("ShiftAssignmentSaved");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save assignment: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ToggleAssignmentStatusAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Managing shift assignments"))
            {
                return;
            }

            if (parameter is not ShiftAssignmentVm assignment)
            {
                return;
            }

            try
            {
                var nextStatus = string.Equals(assignment.Status, "ASSIGNED", StringComparison.OrdinalIgnoreCase)
                    ? "CANCELLED"
                    : "ASSIGNED";

                await _dataService.UpdateShiftAssignmentStatusAsync(assignment.AssignmentId, nextStatus);
                await RefreshAsync();
                SetMessage($"Assignment {assignment.EmployeeNo} set to {nextStatus}.", SuccessBrush);
                SystemRefreshBus.Raise("ShiftAssignmentStatusUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to update assignment: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DeleteAssignmentAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAccess("Managing shift assignments"))
            {
                return;
            }

            if (parameter is not ShiftAssignmentVm assignment)
            {
                return;
            }

            try
            {
                await _dataService.DeleteShiftAssignmentAsync(assignment.AssignmentId);
                await RefreshAsync();
                SetMessage($"Assignment {assignment.EmployeeNo} deleted.", SuccessBrush);
                SystemRefreshBus.Raise("ShiftAssignmentDeleted");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to delete assignment: {ex.Message}", ErrorBrush);
            }
        }

        private void ApplyLogFilters()
        {
            IEnumerable<AttendanceLogVm> query = _allLogs;

            var search = SearchText.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(row =>
                    Contains(row.EmployeeNo, search) ||
                    Contains(row.EmployeeName, search) ||
                    Contains(row.DeviceName, search) ||
                    Contains(row.LogType, search) ||
                    Contains(row.Source, search));
            }

            if (!string.Equals(SelectedSourceFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(row => string.Equals(row.Source, SelectedSourceFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(SelectedTypeFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(row => string.Equals(row.LogType, SelectedTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedDateFilter.HasValue)
            {
                var filterDate = SelectedDateFilter.Value.Date;
                query = query.Where(row => row.LogTime.Date == filterDate);
            }

            Logs.Clear();
            foreach (var row in query)
            {
                Logs.Add(row);
            }
        }

        private static bool Contains(string source, string search) =>
            !string.IsNullOrWhiteSpace(source) &&
            source.Contains(search, StringComparison.OrdinalIgnoreCase);

        private static bool TryParseTime(string? value, out TimeSpan time)
        {
            var input = (value ?? string.Empty).Trim();
            return TimeSpan.TryParseExact(
                       input,
                       new[] { "h\\:mm", "hh\\:mm", "h\\:mm\\:ss", "hh\\:mm\\:ss" },
                       CultureInfo.InvariantCulture,
                       out time)
                   || TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out time);
        }

        private void SetMessage(string message, Brush brush)
        {
            ActionMessage = message;
            ActionMessageBrush = brush;
        }

        private bool EnsureAdminOrHrAccess(string actionName)
        {
            if (!IsEmployeeMode)
            {
                return true;
            }

            SetMessage($"{actionName} is available only for Admin/HR.", ErrorBrush);
            return false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AttendanceLogVm
    {
        public AttendanceLogVm(long logId, string employeeNo, string employeeName, DateTime logTime, string logType, string source, string deviceName)
        {
            LogId = logId;
            EmployeeNo = employeeNo;
            EmployeeName = employeeName;
            LogTime = logTime;
            LogType = logType;
            Source = source;
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "-" : deviceName;
        }

        public long LogId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public DateTime LogTime { get; }
        public string LogType { get; }
        public string Source { get; }
        public string DeviceName { get; }
        public string LogDateText => LogTime == DateTime.MinValue ? "-" : LogTime.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string LogTimeText => LogTime == DateTime.MinValue ? "-" : LogTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
        public Brush LogTypeBrush => LogType.ToUpperInvariant() switch
        {
            "IN" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B")),
            "OUT" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4368")),
            "BREAK_IN" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDBD55")),
            "BREAK_OUT" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B61CC")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A98AA"))
        };
        public Brush SourceBrush => Source.ToUpperInvariant() switch
        {
            "BIOMETRIC" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4368")),
            "MANUAL" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDBD55")),
            "IMPORT" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A98AA"))
        };
    }

    public class AttendanceAdjustmentVm
    {
        private static readonly Brush PendingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B9831A"));
        private static readonly Brush ApprovedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush RejectedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));
        private static readonly Brush DefaultBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A98AA"));

        public AttendanceAdjustmentVm(
            long adjustmentId,
            string employeeNo,
            string employeeName,
            DateTime workDate,
            DateTime? requestedIn,
            DateTime? requestedOut,
            string reason,
            string status,
            DateTime requestedAt,
            string decisionRemarks,
            DateTime? decidedAt)
        {
            AdjustmentId = adjustmentId;
            EmployeeNo = employeeNo;
            EmployeeName = employeeName;
            WorkDate = workDate;
            RequestedIn = requestedIn;
            RequestedOut = requestedOut;
            Reason = reason;
            Status = status;
            RequestedAt = requestedAt;
            DecisionRemarks = string.IsNullOrWhiteSpace(decisionRemarks) ? "-" : decisionRemarks.Trim();
            DecidedAt = decidedAt;
        }

        public long AdjustmentId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public DateTime WorkDate { get; }
        public DateTime? RequestedIn { get; }
        public DateTime? RequestedOut { get; }
        public string Reason { get; }
        public string Status { get; }
        public DateTime RequestedAt { get; }
        public string DecisionRemarks { get; }
        public DateTime? DecidedAt { get; }
        public string WorkDateText => WorkDate == DateTime.MinValue ? "-" : WorkDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string RequestedInText => RequestedIn.HasValue ? RequestedIn.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string RequestedOutText => RequestedOut.HasValue ? RequestedOut.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string RequestedAtText => RequestedAt == DateTime.MinValue ? "-" : RequestedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
        public string DecidedAtText => DecidedAt.HasValue ? DecidedAt.Value.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture) : "-";
        public Brush StatusBrush => Status.ToUpperInvariant() switch
        {
            "PENDING" => PendingBrush,
            "APPROVED" => ApprovedBrush,
            "REJECTED" => RejectedBrush,
            _ => DefaultBrush
        };
    }

    public class LookupOptionVm
    {
        public LookupOptionVm(int id, string label)
        {
            Id = id;
            Label = string.IsNullOrWhiteSpace(label) ? "-" : label.Trim();
        }

        public int Id { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }

    public class BiometricDeviceVm
    {
        private static readonly Brush ActiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush InactiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A98AA"));

        public BiometricDeviceVm(
            int deviceId,
            string deviceName,
            string serialNo,
            string location,
            string ipAddress,
            bool isActive,
            DateTime? lastSyncAt)
        {
            DeviceId = deviceId;
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "-" : deviceName.Trim();
            SerialNo = string.IsNullOrWhiteSpace(serialNo) ? "-" : serialNo.Trim();
            Location = string.IsNullOrWhiteSpace(location) ? "-" : location.Trim();
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "-" : ipAddress.Trim();
            IsActive = isActive;
            LastSyncAt = lastSyncAt;
        }

        public int DeviceId { get; }
        public string DeviceName { get; }
        public string SerialNo { get; }
        public string Location { get; }
        public string IpAddress { get; }
        public bool IsActive { get; }
        public DateTime? LastSyncAt { get; }

        public string StatusText => IsActive ? "ACTIVE" : "INACTIVE";
        public Brush StatusBrush => IsActive ? ActiveBrush : InactiveBrush;
        public string LastSyncText => LastSyncAt.HasValue
            ? LastSyncAt.Value.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture)
            : "Never";
        public string ToggleStatusText => IsActive ? "Deactivate" : "Activate";
    }

    public class BiometricEnrollmentVm
    {
        private static readonly Brush ActiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush InactiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A98AA"));

        public BiometricEnrollmentVm(
            int enrollmentId,
            string employeeNo,
            string employeeName,
            string biometricUserId,
            string deviceName,
            string status,
            DateTime createdAt)
        {
            EnrollmentId = enrollmentId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            BiometricUserId = string.IsNullOrWhiteSpace(biometricUserId) ? "-" : biometricUserId.Trim();
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "-" : deviceName.Trim();
            Status = string.IsNullOrWhiteSpace(status) ? "ACTIVE" : status.Trim().ToUpperInvariant();
            CreatedAt = createdAt;
        }

        public int EnrollmentId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public string BiometricUserId { get; }
        public string DeviceName { get; }
        public string Status { get; }
        public DateTime CreatedAt { get; }

        public string CreatedAtText => CreatedAt == DateTime.MinValue
            ? "-"
            : CreatedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
        public Brush StatusBrush => string.Equals(Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            ? ActiveBrush
            : InactiveBrush;
        public string ToggleStatusText => string.Equals(Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            ? "Set Inactive"
            : "Set Active";
    }

    public class ShiftVm
    {
        private static readonly Brush OvernightStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7353BB"));
        private static readonly Brush DayStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4368"));

        public ShiftVm(
            int shiftId,
            string shiftName,
            TimeSpan startTime,
            TimeSpan endTime,
            int breakMinutes,
            int graceMinutes,
            bool isOvernight)
        {
            ShiftId = shiftId;
            ShiftName = string.IsNullOrWhiteSpace(shiftName) ? "-" : shiftName.Trim();
            StartTime = startTime;
            EndTime = endTime;
            BreakMinutes = breakMinutes;
            GraceMinutes = graceMinutes;
            IsOvernight = isOvernight;
        }

        public int ShiftId { get; }
        public string ShiftName { get; }
        public TimeSpan StartTime { get; }
        public TimeSpan EndTime { get; }
        public int BreakMinutes { get; }
        public int GraceMinutes { get; }
        public bool IsOvernight { get; }

        public string StartTimeText => DateTime.Today.Add(StartTime).ToString("hh:mm tt", CultureInfo.InvariantCulture);
        public string EndTimeText => DateTime.Today.Add(EndTime).ToString("hh:mm tt", CultureInfo.InvariantCulture);
        public string ScheduleText => $"{StartTimeText} - {EndTimeText}";
        public string OvernightText => IsOvernight ? "OVERNIGHT" : "DAY";
        public Brush OvernightBrush => IsOvernight ? OvernightStatusBrush : DayStatusBrush;
    }

    public class ShiftAssignmentVm
    {
        private static readonly Brush AssignedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush CancelledBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A98AA"));

        public ShiftAssignmentVm(
            int assignmentId,
            string employeeNo,
            string employeeName,
            string shiftName,
            DateTime startDate,
            DateTime? endDate,
            string status)
        {
            AssignmentId = assignmentId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            ShiftName = string.IsNullOrWhiteSpace(shiftName) ? "-" : shiftName.Trim();
            StartDate = startDate;
            EndDate = endDate;
            Status = string.IsNullOrWhiteSpace(status) ? "ASSIGNED" : status.Trim().ToUpperInvariant();
        }

        public int AssignmentId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public string ShiftName { get; }
        public DateTime StartDate { get; }
        public DateTime? EndDate { get; }
        public string Status { get; }

        public string StartDateText => StartDate == DateTime.MinValue
            ? "-"
            : StartDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string EndDateText => EndDate.HasValue
            ? EndDate.Value.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)
            : "Open";
        public Brush StatusBrush => string.Equals(Status, "ASSIGNED", StringComparison.OrdinalIgnoreCase)
            ? AssignedBrush
            : CancelledBrush;
        public string ToggleStatusText => string.Equals(Status, "ASSIGNED", StringComparison.OrdinalIgnoreCase)
            ? "Cancel"
            : "Assign";
    }
}
