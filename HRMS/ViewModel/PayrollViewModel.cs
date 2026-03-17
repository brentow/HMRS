using HRMS.Model;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QColors = QuestPDF.Helpers.Colors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public class PayrollViewModel : INotifyPropertyChanged
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5B6C"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private readonly PayrollDataService _dataService = new(DbConfig.ConnectionString);
        private readonly List<PayrollPeriodVm> _allPeriods = new();
        private readonly List<PayrollRunVm> _allRuns = new();
        private readonly List<PayrollReleaseLogVm> _allReleaseLogs = new();
        private const int SulopOfficeId = 3;

        private int _currentUserId;
        private string _currentUsername = "-";
        private bool _isEmployeeMode;
        private int? _currentEmployeeId;
        private bool _refreshQueued;
        private int _totalPeriods;
        private int _openPeriods;
        private int _totalRuns;
        private int _releasedPayslips;
        private decimal _totalNetPay;
        private decimal _ytdGrossPay;
        private decimal _ytdDeductions;
        private decimal _ytdNetPay;
        private bool _isBusy;
        private long _sulopAllocationId;
        private string _sulopProgram = "-";
        private decimal _sulopAllocatedAmount;
        private decimal _sulopUsedAmount;
        private decimal _sulopRemainingAmount;
        private string _sulopSyncStatus = "Sulop allocation not synced yet.";
        private Brush _sulopSyncStatusBrush = InfoBrush;

        private string _periodSearchText = string.Empty;
        private string _selectedPeriodStatusFilter = "All";
        private string _runSearchText = string.Empty;
        private long _selectedRunPeriodFilterId;
        private string _selectedRunStatusFilter = "All";
        private string _releaseSearchText = string.Empty;

        private string _newPeriodCode = string.Empty;
        private DateTime _newPeriodDateFrom = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _newPeriodDateTo = new(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month));
        private DateTime _newPeriodPayDate = DateTime.Today;
        private string _newPeriodStatus = "OPEN";

        private long? _selectedRunPeriodId;
        private long? _selectedRunEmployeeId;
        private decimal _runBasicPay;
        private decimal _runAllowances;
        private decimal _runOvertimePay;
        private decimal _runOtherEarnings;
        private decimal _runDeductions;
        private string _runStatus = "GENERATED";
        private int _runEditorLoadVersion;

        private PayrollRunVm? _selectedRun;
        private long? _selectedReleaseRunId;
        private string _releaseRemarks = string.Empty;
        private string _payrollConcernDetails = string.Empty;

        private string _actionMessage = "Ready.";
        private Brush _actionMessageBrush = InfoBrush;

        public int TotalPeriods { get => _totalPeriods; private set { _totalPeriods = value; OnPropertyChanged(); } }
        public int OpenPeriods { get => _openPeriods; private set { _openPeriods = value; OnPropertyChanged(); } }
        public int TotalRuns { get => _totalRuns; private set { _totalRuns = value; OnPropertyChanged(); } }
        public int ReleasedPayslips { get => _releasedPayslips; private set { _releasedPayslips = value; OnPropertyChanged(); } }
        public decimal TotalNetPay
        {
            get => _totalNetPay;
            private set
            {
                _totalNetPay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalNetPayText));
            }
        }
        public string TotalNetPayText => $"PHP {TotalNetPay:N2}";
        public decimal YtdGrossPay
        {
            get => _ytdGrossPay;
            private set
            {
                _ytdGrossPay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(YtdGrossPayText));
            }
        }
        public decimal YtdDeductions
        {
            get => _ytdDeductions;
            private set
            {
                _ytdDeductions = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(YtdDeductionsText));
            }
        }
        public decimal YtdNetPay
        {
            get => _ytdNetPay;
            private set
            {
                _ytdNetPay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(YtdNetPayText));
            }
        }
        public string YtdLabel => $"YTD {DateTime.Today:yyyy}";
        public string YtdGrossPayText => $"PHP {YtdGrossPay:N2}";
        public string YtdDeductionsText => $"PHP {YtdDeductions:N2}";
        public string YtdNetPayText => $"PHP {YtdNetPay:N2}";
        public long SulopAllocationId
        {
            get => _sulopAllocationId;
            private set
            {
                if (_sulopAllocationId == value)
                {
                    return;
                }

                _sulopAllocationId = value;
                OnPropertyChanged();
            }
        }
        public string SulopProgram
        {
            get => _sulopProgram;
            private set
            {
                if (string.Equals(_sulopProgram, value, StringComparison.Ordinal))
                {
                    return;
                }

                _sulopProgram = value;
                OnPropertyChanged();
            }
        }
        public decimal SulopAllocatedAmount
        {
            get => _sulopAllocatedAmount;
            private set
            {
                if (_sulopAllocatedAmount == value)
                {
                    return;
                }

                _sulopAllocatedAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SulopAllocatedAmountText));
            }
        }
        public decimal SulopUsedAmount
        {
            get => _sulopUsedAmount;
            private set
            {
                if (_sulopUsedAmount == value)
                {
                    return;
                }

                _sulopUsedAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SulopUsedAmountText));
            }
        }
        public decimal SulopRemainingAmount
        {
            get => _sulopRemainingAmount;
            private set
            {
                if (_sulopRemainingAmount == value)
                {
                    return;
                }

                _sulopRemainingAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SulopRemainingAmountText));
            }
        }
        public string SulopAllocatedAmountText => $"PHP {SulopAllocatedAmount:N2}";
        public string SulopUsedAmountText => $"PHP {SulopUsedAmount:N2}";
        public string SulopRemainingAmountText => $"PHP {SulopRemainingAmount:N2}";
        public string SulopSyncStatus
        {
            get => _sulopSyncStatus;
            private set
            {
                if (string.Equals(_sulopSyncStatus, value, StringComparison.Ordinal))
                {
                    return;
                }

                _sulopSyncStatus = value;
                OnPropertyChanged();
            }
        }
        public Brush SulopSyncStatusBrush
        {
            get => _sulopSyncStatusBrush;
            private set
            {
                if (Equals(_sulopSyncStatusBrush, value))
                {
                    return;
                }

                _sulopSyncStatusBrush = value;
                OnPropertyChanged();
            }
        }
        public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); } }
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
                OnPropertyChanged(nameof(PageHeaderTitle));
                OnPropertyChanged(nameof(PageHeaderSubtitle));
                OnPropertyChanged(nameof(RunsCardLabel));
                OnPropertyChanged(nameof(ReleasesCardLabel));
                OnPropertyChanged(nameof(NetPayCardLabel));
                OnPropertyChanged(nameof(PeriodsTabLabel));
                OnPropertyChanged(nameof(RunsTabLabel));
                OnPropertyChanged(nameof(ReleaseTabLabel));
                OnPropertyChanged(nameof(RunActionsHeader));
                OnPropertyChanged(nameof(ReleaseActionsHeader));
                OnPropertyChanged(nameof(CanReportPayrollConcern));
            }
        }
        public bool IsAdminOrHrMode => !IsEmployeeMode;
        public string PageHeaderTitle => IsEmployeeMode ? "My Payroll" : "Payroll Administration";
        public string PageHeaderSubtitle => IsEmployeeMode
            ? "Review your payroll runs and released payslips."
            : "Manage payroll periods, employee payroll runs, and payslip release logs.";
        public string RunsCardLabel => IsEmployeeMode ? "My Payroll Runs" : "Payroll Runs";
        public string ReleasesCardLabel => IsEmployeeMode ? "My Released Payslips" : "Released Payslips";
        public string NetPayCardLabel => IsEmployeeMode ? "My Total Net Pay" : "Total Net Pay";
        public string PeriodsTabLabel => IsEmployeeMode ? "Payroll Periods" : "Payroll Periods";
        public string RunsTabLabel => IsEmployeeMode ? "My Payroll Runs" : "Payroll Runs Per Employee";
        public string ReleaseTabLabel => IsEmployeeMode ? "My Payslip Release Logs" : "Payslip Release Logs";
        public string RunActionsHeader => IsEmployeeMode ? "My Actions" : "Actions";
        public string ReleaseActionsHeader => IsEmployeeMode ? "My Actions" : "Actions";
        public bool CanReportPayrollConcern =>
            IsEmployeeMode &&
            _currentEmployeeId.HasValue &&
            _currentEmployeeId.Value > 0 &&
            SelectedRun is not null &&
            SelectedRun.EmployeeId == _currentEmployeeId.Value;
        public string PayrollConcernDetails
        {
            get => _payrollConcernDetails;
            set
            {
                if (_payrollConcernDetails == value)
                {
                    return;
                }

                _payrollConcernDetails = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
        public string SelectedRunPeriodLabel => SelectedRun?.PeriodCode ?? "-";
        public string SelectedRunGeneratedLabel => SelectedRun?.GeneratedAtText ?? "-";
        public string SelectedRunStatusLabel => SelectedRun?.Status ?? "-";

        public string PeriodSearchText
        {
            get => _periodSearchText;
            set
            {
                if (_periodSearchText == value) return;
                _periodSearchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyPeriodFilters();
            }
        }

        public string SelectedPeriodStatusFilter
        {
            get => _selectedPeriodStatusFilter;
            set
            {
                if (_selectedPeriodStatusFilter == value || string.IsNullOrWhiteSpace(value)) return;
                _selectedPeriodStatusFilter = value;
                OnPropertyChanged();
                ApplyPeriodFilters();
            }
        }

        public string RunSearchText
        {
            get => _runSearchText;
            set
            {
                if (_runSearchText == value) return;
                _runSearchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyRunFilters();
            }
        }

        public long SelectedRunPeriodFilterId
        {
            get => _selectedRunPeriodFilterId;
            set
            {
                if (_selectedRunPeriodFilterId == value) return;
                _selectedRunPeriodFilterId = value;
                OnPropertyChanged();
                ApplyRunFilters();
            }
        }

        public string SelectedRunStatusFilter
        {
            get => _selectedRunStatusFilter;
            set
            {
                if (_selectedRunStatusFilter == value || string.IsNullOrWhiteSpace(value)) return;
                _selectedRunStatusFilter = value;
                OnPropertyChanged();
                ApplyRunFilters();
            }
        }

        public string ReleaseSearchText
        {
            get => _releaseSearchText;
            set
            {
                if (_releaseSearchText == value) return;
                _releaseSearchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyReleaseFilters();
            }
        }

        public string NewPeriodCode { get => _newPeriodCode; set { if (_newPeriodCode != value) { _newPeriodCode = value ?? string.Empty; OnPropertyChanged(); } } }
        public DateTime NewPeriodDateFrom { get => _newPeriodDateFrom; set { if (_newPeriodDateFrom != value) { _newPeriodDateFrom = value; OnPropertyChanged(); } } }
        public DateTime NewPeriodDateTo { get => _newPeriodDateTo; set { if (_newPeriodDateTo != value) { _newPeriodDateTo = value; OnPropertyChanged(); } } }
        public DateTime NewPeriodPayDate { get => _newPeriodPayDate; set { if (_newPeriodPayDate != value) { _newPeriodPayDate = value; OnPropertyChanged(); } } }
        public string NewPeriodStatus { get => _newPeriodStatus; set { if (_newPeriodStatus != value && !string.IsNullOrWhiteSpace(value)) { _newPeriodStatus = value; OnPropertyChanged(); } } }

        public long? SelectedRunPeriodId
        {
            get => _selectedRunPeriodId;
            set
            {
                if (_selectedRunPeriodId == value)
                {
                    return;
                }

                _selectedRunPeriodId = value;
                OnPropertyChanged();
                QueueRunEditorDefaultsLoad();
            }
        }

        public long? SelectedRunEmployeeId
        {
            get => _selectedRunEmployeeId;
            set
            {
                if (_selectedRunEmployeeId == value)
                {
                    return;
                }

                _selectedRunEmployeeId = value;
                OnPropertyChanged();
                QueueRunEditorDefaultsLoad();
            }
        }
        public decimal RunBasicPay { get => _runBasicPay; set { if (_runBasicPay != value) { _runBasicPay = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunGrossPreview)); OnPropertyChanged(nameof(RunNetPreview)); } } }
        public decimal RunAllowances { get => _runAllowances; set { if (_runAllowances != value) { _runAllowances = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunGrossPreview)); OnPropertyChanged(nameof(RunNetPreview)); } } }
        public decimal RunOvertimePay { get => _runOvertimePay; set { if (_runOvertimePay != value) { _runOvertimePay = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunGrossPreview)); OnPropertyChanged(nameof(RunNetPreview)); } } }
        public decimal RunOtherEarnings { get => _runOtherEarnings; set { if (_runOtherEarnings != value) { _runOtherEarnings = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunGrossPreview)); OnPropertyChanged(nameof(RunNetPreview)); } } }
        public decimal RunDeductions { get => _runDeductions; set { if (_runDeductions != value) { _runDeductions = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunNetPreview)); } } }
        public string RunStatus { get => _runStatus; set { if (_runStatus != value && !string.IsNullOrWhiteSpace(value)) { _runStatus = value; OnPropertyChanged(); } } }
        public decimal RunGrossPreview => RunBasicPay + RunAllowances + RunOvertimePay + RunOtherEarnings;
        public decimal RunNetPreview => RunGrossPreview - RunDeductions;

        public PayrollRunVm? SelectedRun
        {
            get => _selectedRun;
            set
            {
                if (_selectedRun == value) return;
                _selectedRun = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanReportPayrollConcern));
                OnPropertyChanged(nameof(SelectedRunPeriodLabel));
                OnPropertyChanged(nameof(SelectedRunGeneratedLabel));
                OnPropertyChanged(nameof(SelectedRunStatusLabel));
                if (_selectedRun != null)
                {
                    SelectedReleaseRunId = _selectedRun.PayrollRunId;
                }
            }
        }

        public long? SelectedReleaseRunId { get => _selectedReleaseRunId; set { if (_selectedReleaseRunId != value) { _selectedReleaseRunId = value; OnPropertyChanged(); } } }
        public string ReleaseRemarks { get => _releaseRemarks; set { if (_releaseRemarks != value) { _releaseRemarks = value ?? string.Empty; OnPropertyChanged(); } } }
        public string ActionMessage { get => _actionMessage; private set { if (_actionMessage != value) { _actionMessage = value; OnPropertyChanged(); } } }
        public Brush ActionMessageBrush { get => _actionMessageBrush; private set { if (_actionMessageBrush != value) { _actionMessageBrush = value; OnPropertyChanged(); } } }

        public ObservableCollection<string> PeriodStatusFilters { get; } = new() { "All", "OPEN", "LOCKED", "POSTED", "CANCELLED" };
        public ObservableCollection<string> EditablePeriodStatuses { get; } = new() { "OPEN", "LOCKED", "POSTED", "CANCELLED" };
        public ObservableCollection<string> RunStatusFilters { get; } = new() { "All", "DRAFT", "GENERATED", "APPROVED", "RELEASED", "VOID" };
        public ObservableCollection<string> EditableRunStatuses { get; } = new() { "DRAFT", "GENERATED", "APPROVED", "RELEASED", "VOID" };

        public ObservableCollection<PayrollLookupOptionVm> PeriodOptions { get; } = new();
        public ObservableCollection<PayrollLookupOptionVm> EmployeeOptions { get; } = new();
        public ObservableCollection<PayrollLookupOptionVm> RunOptions { get; } = new();
        public ObservableCollection<PayrollPeriodVm> PayrollPeriods { get; } = new();
        public ObservableCollection<PayrollRunVm> PayrollRuns { get; } = new();
        public ObservableCollection<PayrollReleaseLogVm> PayslipReleases { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand AddPeriodCommand { get; }
        public ICommand SavePeriodCommand { get; }
        public ICommand DeletePeriodCommand { get; }
        public ICommand UpsertRunCommand { get; }
        public ICommand SaveRunStatusCommand { get; }
        public ICommand DeleteRunCommand { get; }
        public ICommand ReleasePayslipCommand { get; }
        public ICommand DownloadPayslipCommand { get; }
        public ICommand PrintPayslipCommand { get; }
        public ICommand ReportPayrollConcernCommand { get; }

        public PayrollViewModel()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
            AddPeriodCommand = new AsyncRelayCommand(_ => AddPeriodAsync());
            SavePeriodCommand = new AsyncRelayCommand(SavePeriodAsync);
            DeletePeriodCommand = new AsyncRelayCommand(DeletePeriodAsync);
            UpsertRunCommand = new AsyncRelayCommand(_ => UpsertRunAsync());
            SaveRunStatusCommand = new AsyncRelayCommand(SaveRunStatusAsync);
            DeleteRunCommand = new AsyncRelayCommand(DeleteRunAsync);
            ReleasePayslipCommand = new AsyncRelayCommand(ReleasePayslipAsync);
            DownloadPayslipCommand = new AsyncRelayCommand(DownloadPayslipAsync);
            PrintPayslipCommand = new AsyncRelayCommand(PrintPayslipAsync);
            ReportPayrollConcernCommand = new AsyncRelayCommand(ReportPayrollConcernAsync);

            QueueRefresh();
        }

        public void SetCurrentUser(int userId, string username, string? roleName)
        {
            _currentUserId = userId;
            _currentUsername = string.IsNullOrWhiteSpace(username) ? "-" : username.Trim();
            IsEmployeeMode = string.Equals(roleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);
            _currentEmployeeId = null;
            PayrollConcernDetails = string.Empty;
            QueueRefresh();
        }

        public async Task RefreshAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetMessage("Loading payroll module...", InfoBrush);

            try
            {
                if (IsEmployeeMode && _currentUserId > 0 && (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0))
                {
                    _currentEmployeeId = await _dataService.GetEmployeeIdByUserIdAsync(_currentUserId);
                }

                if (IsEmployeeMode && (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0))
                {
                    ClearForUnlinkedEmployee();
                    SetMessage("Your employee profile is not linked to this account.", ErrorBrush);
                    return;
                }

                var scopedEmployeeId = IsEmployeeMode ? _currentEmployeeId : null;

                var statsTask = _dataService.GetStatsAsync(scopedEmployeeId);
                var periodsTask = _dataService.GetPeriodsAsync(limit: 400, employeeId: scopedEmployeeId);
                var employeesTask = _dataService.GetEmployeesAsync(scopedEmployeeId);
                var runsTask = _dataService.GetRunsAsync(limit: 700, employeeId: scopedEmployeeId);
                var releasesTask = _dataService.GetReleaseLogsAsync(limit: 700, employeeId: scopedEmployeeId);
                var sulopAllocationTask = LoadSulopAllocationAsync();

                var stats = await statsTask;
                var periods = await periodsTask;
                var employees = await employeesTask;
                var runs = await runsTask;
                var releases = await releasesTask;
                await sulopAllocationTask;

                TotalPeriods = stats.TotalPeriods;
                OpenPeriods = stats.OpenPeriods;
                TotalRuns = stats.TotalRuns;
                ReleasedPayslips = stats.ReleasedPayslips;
                TotalNetPay = stats.TotalNetPay;

                RebuildPeriodRows(periods);
                RebuildRunRows(runs);
                RebuildReleaseRows(releases);
                RebuildOptions(periods, employees, runs);
                RecalculateYtdTotals();

                if (IsEmployeeMode && _currentEmployeeId.HasValue && _currentEmployeeId.Value > 0)
                {
                    SelectedRunEmployeeId = _currentEmployeeId.Value;
                }

                if ((!SelectedRunPeriodId.HasValue || SelectedRunPeriodId.Value <= 0) && PeriodOptions.Count > 1)
                {
                    SelectedRunPeriodId = PeriodOptions[1].Id;
                }

                if ((!SelectedRunEmployeeId.HasValue || SelectedRunEmployeeId.Value <= 0) && EmployeeOptions.Count > 0)
                {
                    SelectedRunEmployeeId = EmployeeOptions[0].Id;
                }

                if ((!SelectedReleaseRunId.HasValue || SelectedReleaseRunId.Value <= 0) && RunOptions.Count > 0)
                {
                    SelectedReleaseRunId = RunOptions[0].Id;
                }

                if (PayrollRuns.Count > 0)
                {
                    var selectedId = SelectedRun?.PayrollRunId ?? 0;
                    SelectedRun = PayrollRuns.FirstOrDefault(x => x.PayrollRunId == selectedId) ?? PayrollRuns[0];
                }
                else
                {
                    SelectedRun = null;
                }

                OnPropertyChanged(nameof(CanReportPayrollConcern));

                SetMessage("Payroll module refreshed.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to refresh payroll module: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;

                if (_refreshQueued)
                {
                    _refreshQueued = false;
                    _ = RefreshAsync();
                }
            }
        }

        private async Task LoadSulopAllocationAsync()
        {
            try
            {
                var sulopService = new SulopFundAllocationService(SulopConfig.ConnectionString, SulopOfficeId);
                var allocation = await sulopService.GetActiveAllocationAsync();
                if (allocation == null)
                {
                    SulopAllocationId = 0;
                    SulopProgram = "-";
                    SulopAllocatedAmount = 0m;
                    SulopUsedAmount = 0m;
                    SulopRemainingAmount = 0m;
                    SulopSyncStatus = "No active Sulop allocation found for Office ID 3.";
                    SulopSyncStatusBrush = ErrorBrush;
                    return;
                }

                SulopAllocationId = allocation.AllocationId;
                SulopProgram = allocation.Program;
                SulopAllocatedAmount = allocation.AllocatedAmount;
                SulopUsedAmount = allocation.UsedAmount;
                SulopRemainingAmount = allocation.RemainingAmount;
                SulopSyncStatus = $"Sulop allocation synced (ID #{allocation.AllocationId}, Program: {allocation.Program}).";
                SulopSyncStatusBrush = SuccessBrush;
            }
            catch (Exception ex)
            {
                SulopSyncStatus = $"Sulop sync failed: {ex.Message}";
                SulopSyncStatusBrush = ErrorBrush;
            }
        }

        private async Task AddPeriodAsync()
        {
            if (!EnsureAdminOrHrAction("create payroll periods"))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPeriodCode))
            {
                SetMessage("Period code is required.", ErrorBrush);
                return;
            }

            try
            {
                var id = await _dataService.AddPeriodAsync(NewPeriodCode, NewPeriodDateFrom, NewPeriodDateTo, NewPeriodPayDate, NewPeriodStatus);
                NewPeriodCode = string.Empty;
                await RefreshAsync();
                SetMessage($"Payroll period #{id} created.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollPeriodAdded");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to create period: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SavePeriodAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAction("update payroll periods"))
            {
                return;
            }

            if (parameter is not PayrollPeriodVm row)
            {
                SetMessage("Select payroll period row first.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.UpdatePeriodStatusAsync(
                    row.PayrollPeriodId,
                    row.Status,
                    _currentUserId > 0 ? _currentUserId : null);
                await RefreshAsync();
                SetMessage($"Payroll period {row.PeriodCode} saved.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollPeriodUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save period: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DeletePeriodAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAction("delete payroll periods"))
            {
                return;
            }

            if (parameter is not PayrollPeriodVm row)
            {
                SetMessage("Select payroll period row first.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.DeletePeriodAsync(
                    row.PayrollPeriodId,
                    _currentUserId > 0 ? _currentUserId : null);
                await RefreshAsync();
                SetMessage($"Payroll period {row.PeriodCode} deleted.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollPeriodDeleted");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to delete period: {ex.Message}", ErrorBrush);
            }
        }

        private async Task UpsertRunAsync()
        {
            if (!EnsureAdminOrHrAction("save payroll runs"))
            {
                return;
            }

            if (!SelectedRunPeriodId.HasValue || SelectedRunPeriodId.Value <= 0)
            {
                SetMessage("Select payroll period for payroll run.", ErrorBrush);
                return;
            }

            if (!SelectedRunEmployeeId.HasValue || SelectedRunEmployeeId.Value <= 0)
            {
                SetMessage("Select employee for payroll run.", ErrorBrush);
                return;
            }

            try
            {
                var runId = await _dataService.UpsertRunAsync(
                    SelectedRunPeriodId.Value,
                    (int)SelectedRunEmployeeId.Value,
                    RunBasicPay,
                    RunAllowances,
                    RunOvertimePay,
                    RunOtherEarnings,
                    RunDeductions,
                    RunStatus);

                await RefreshAsync();
                SelectedRun = PayrollRuns.FirstOrDefault(x => x.PayrollRunId == runId);
                SetMessage($"Payroll run #{runId} saved.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollRunSaved");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save payroll run: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SaveRunStatusAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAction("update payroll run status"))
            {
                return;
            }

            if (parameter is not PayrollRunVm row)
            {
                SetMessage("Select payroll run row first.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.UpdateRunStatusAsync(
                    row.PayrollRunId,
                    row.Status,
                    _currentUserId > 0 ? _currentUserId : null);
                await RefreshAsync();
                SelectedRun = PayrollRuns.FirstOrDefault(x => x.PayrollRunId == row.PayrollRunId);
                SetMessage($"Run #{row.PayrollRunId} status updated.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollRunStatusUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to update run status: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DeleteRunAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAction("delete payroll runs"))
            {
                return;
            }

            if (parameter is not PayrollRunVm row)
            {
                SetMessage("Select payroll run row first.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.DeleteRunAsync(
                    row.PayrollRunId,
                    _currentUserId > 0 ? _currentUserId : null);
                await RefreshAsync();
                SetMessage($"Run #{row.PayrollRunId} deleted.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollRunDeleted");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to delete run: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ReleasePayslipAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAction("release payslips"))
            {
                return;
            }

            PayrollRunVm? targetRun = null;
            long runId = 0;
            if (parameter is PayrollRunVm runRow)
            {
                runId = runRow.PayrollRunId;
                targetRun = runRow;
            }
            else if (SelectedReleaseRunId.HasValue && SelectedReleaseRunId.Value > 0)
            {
                runId = SelectedReleaseRunId.Value;
                targetRun = _allRuns.FirstOrDefault(x => x.PayrollRunId == runId)
                            ?? PayrollRuns.FirstOrDefault(x => x.PayrollRunId == runId);
            }

            if (runId <= 0)
            {
                SetMessage("Select payroll run to release.", ErrorBrush);
                return;
            }

            if (targetRun == null)
            {
                SetMessage("Could not resolve selected payroll run.", ErrorBrush);
                return;
            }

            var disbursementAmount = targetRun.NetPay;
            if (disbursementAmount <= 0)
            {
                SetMessage("Cannot release payslip with zero or negative net pay.", ErrorBrush);
                return;
            }

            if (SulopAllocationId <= 0)
            {
                SetMessage("Sulop allocation is not available for Office ID 3. Refresh and verify connection first.", ErrorBrush);
                return;
            }

            if (disbursementAmount > SulopRemainingAmount)
            {
                SetMessage(
                    $"Amount exceeds remaining allocation. Remaining: {SulopRemainingAmountText}, Requested: PHP {disbursementAmount:N2}.",
                    ErrorBrush);
                return;
            }

            try
            {
                var sulopService = new SulopFundAllocationService(SulopConfig.ConnectionString, SulopOfficeId);
                var purpose = $"Payroll disbursement ({targetRun.PeriodCode})";
                var description = string.IsNullOrWhiteSpace(ReleaseRemarks)
                    ? $"HRMS payroll release for {targetRun.EmployeeNo} - {targetRun.EmployeeName}."
                    : ReleaseRemarks.Trim();

                var sulopResult = await sulopService.RecordPayrollDisbursementAsync(
                    allocationId: SulopAllocationId,
                    amount: disbursementAmount,
                    recipientName: targetRun.EmployeeName,
                    purpose: purpose,
                    description: description);

                await _dataService.ReleasePayslipAsync(runId, _currentUserId > 0 ? _currentUserId : null, ReleaseRemarks);
                ReleaseRemarks = string.Empty;
                await RefreshAsync();
                SelectedRun = PayrollRuns.FirstOrDefault(x => x.PayrollRunId == runId);
                SetMessage(
                    $"Payslip released and Sulop posted. Run #{runId}, Txn #{sulopResult.TransactionId}, Remaining: PHP {sulopResult.RemainingAfter:N2}.",
                    SuccessBrush);
                SystemRefreshBus.Raise("PayslipReleased");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to release payslip: {ex.Message}", ErrorBrush);
            }
        }

        private Task DownloadPayslipAsync(object? parameter)
        {
            if (!TryResolvePayslipRow(parameter, out var row))
            {
                SetMessage("Select a payroll run or release log first.", ErrorBrush);
                return Task.CompletedTask;
            }

            if (!row.CanOpenPayslip)
            {
                SetMessage("Payslip is not yet released for this run.", ErrorBrush);
                return Task.CompletedTask;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Download Payslip",
                    Filter = "PDF File (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    FileName = $"Payslip-{row.PeriodCode}-{row.EmployeeNo}.pdf"
                };

                if (dialog.ShowDialog() != true)
                {
                    SetMessage("Payslip download cancelled.", InfoBrush);
                    return Task.CompletedTask;
                }

                BuildPayslipPdf(row).GeneratePdf(dialog.FileName);
                SetMessage($"Payslip downloaded: {Path.GetFileName(dialog.FileName)}", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to download payslip: {ex.Message}", ErrorBrush);
            }

            return Task.CompletedTask;
        }

        private Task PrintPayslipAsync(object? parameter)
        {
            if (!TryResolvePayslipRow(parameter, out var row))
            {
                SetMessage("Select a payroll run or release log first.", ErrorBrush);
                return Task.CompletedTask;
            }

            if (!row.CanOpenPayslip)
            {
                SetMessage("Payslip is not yet released for this run.", ErrorBrush);
                return Task.CompletedTask;
            }

            try
            {
                var safePeriod = string.IsNullOrWhiteSpace(row.PeriodCode) ? "PERIOD" : row.PeriodCode.Replace("/", "-");
                var safeEmpNo = string.IsNullOrWhiteSpace(row.EmployeeNo) ? "EMP" : row.EmployeeNo.Replace("/", "-");
                var tempPdf = Path.Combine(
                    Path.GetTempPath(),
                    $"HRMS-Payslip-{safePeriod}-{safeEmpNo}-{DateTime.Now:yyyyMMddHHmmss}.pdf");

                BuildPayslipPdf(row).GeneratePdf(tempPdf);

                var printStart = new ProcessStartInfo
                {
                    FileName = tempPdf,
                    Verb = "print",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                try
                {
                    Process.Start(printStart);
                    SetMessage("Payslip sent to printer.", SuccessBrush);
                }
                catch
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempPdf,
                        UseShellExecute = true
                    });
                    SetMessage("Direct print is unavailable on this PC. Payslip opened; press Ctrl+P to print.", InfoBrush);
                }
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to print payslip: {ex.Message}", ErrorBrush);
            }

            return Task.CompletedTask;
        }

        private async Task ReportPayrollConcernAsync(object? parameter)
        {
            if (!IsEmployeeMode)
            {
                SetMessage("Report concern is available in employee payroll view.", ErrorBrush);
                return;
            }

            if (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0)
            {
                SetMessage("Your employee profile is not linked to this account.", ErrorBrush);
                return;
            }

            var run = parameter as PayrollRunVm ?? SelectedRun;
            if (run is null)
            {
                SetMessage("Select a payroll run before reporting concern.", ErrorBrush);
                return;
            }

            if (run.EmployeeId != _currentEmployeeId.Value)
            {
                SetMessage("You can only report concern for your own payroll run.", ErrorBrush);
                return;
            }

            var details = (PayrollConcernDetails ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(details))
            {
                SetMessage("Please enter concern details first.", ErrorBrush);
                return;
            }

            if (details.Length > 1000)
            {
                SetMessage("Concern details cannot exceed 1000 characters.", ErrorBrush);
                return;
            }

            try
            {
                var concernId = await _dataService.ReportPayrollConcernAsync(
                    run.PayrollRunId,
                    _currentEmployeeId.Value,
                    _currentUserId > 0 ? _currentUserId : null,
                    details);

                PayrollConcernDetails = string.Empty;
                SetMessage($"Payroll concern #{concernId} submitted for run #{run.PayrollRunId}.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollConcernReported");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to report payroll concern: {ex.Message}", ErrorBrush);
            }
        }

        private static QuestPDF.Infrastructure.IDocument BuildPayslipPdf(PayrollRunVm row)
        {
            var generatedAt = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
            var releasedAt = string.IsNullOrWhiteSpace(row.LastReleasedAtText) || row.LastReleasedAtText == "-"
                ? "Not released"
                : row.LastReleasedAtText;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(column =>
                    {
                        column.Spacing(4);
                        column.Item().Text("HRMS PAYSLIP").FontSize(18).Bold().FontColor(QColors.Blue.Darken3);
                        column.Item().Text($"Generated: {generatedAt}").FontColor(QColors.Grey.Darken2);
                    });

                    page.Content().PaddingTop(12).Column(column =>
                    {
                        column.Spacing(8);
                        column.Item().Text($"Period: {row.PeriodCode}");
                        column.Item().Text($"Employee No: {row.EmployeeNo}");
                        column.Item().Text($"Employee: {row.EmployeeName}");
                        column.Item().Text($"Run Status: {row.Status}");
                        column.Item().Text($"Released: {releasedAt}");

                        column.Item().PaddingTop(8).Element(e => e.LineHorizontal(1).LineColor(QColors.Grey.Lighten2));

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().PaddingVertical(4).Text("Basic Pay");
                            table.Cell().PaddingVertical(4).AlignRight().Text($"{row.BasicPay:N2}");

                            table.Cell().PaddingVertical(4).Text("Allowances");
                            table.Cell().PaddingVertical(4).AlignRight().Text($"{row.Allowances:N2}");

                            table.Cell().PaddingVertical(4).Text("Overtime Pay");
                            table.Cell().PaddingVertical(4).AlignRight().Text($"{row.OvertimePay:N2}");

                            table.Cell().PaddingVertical(4).Text("Other Earnings");
                            table.Cell().PaddingVertical(4).AlignRight().Text($"{row.OtherEarnings:N2}");

                            table.Cell().PaddingVertical(4).Text("Gross Pay").Bold();
                            table.Cell().PaddingVertical(4).AlignRight().Text($"{row.GrossPay:N2}").Bold();

                            table.Cell().PaddingVertical(4).Text("Deductions");
                            table.Cell().PaddingVertical(4).AlignRight().Text($"{row.DeductionsTotal:N2}");

                            table.Cell().PaddingTop(8).Text("Net Pay").Bold().FontColor(QColors.Green.Darken2);
                            table.Cell().PaddingTop(8).AlignRight().Text($"{row.NetPay:N2}").Bold().FontColor(QColors.Green.Darken2);
                        });
                    });

                    page.Footer().AlignCenter().Text("This payslip was generated by HRMS.");
                });
            });
        }

        private bool TryResolvePayslipRow(object? parameter, out PayrollRunVm row)
        {
            if (parameter is PayrollRunVm runRow)
            {
                row = runRow;
                return true;
            }

            if (parameter is PayrollReleaseLogVm releaseRow)
            {
                var runFromAll = _allRuns.FirstOrDefault(x => x.PayrollRunId == releaseRow.PayrollRunId);
                if (runFromAll != null)
                {
                    row = runFromAll;
                    return true;
                }

                var runFromFiltered = PayrollRuns.FirstOrDefault(x => x.PayrollRunId == releaseRow.PayrollRunId);
                if (runFromFiltered != null)
                {
                    row = runFromFiltered;
                    return true;
                }
            }

            row = null!;
            return false;
        }

        private void QueueRefresh()
        {
            if (IsBusy)
            {
                _refreshQueued = true;
                return;
            }

            _ = RefreshAsync();
        }

        private void QueueRunEditorDefaultsLoad()
        {
            var version = ++_runEditorLoadVersion;
            _ = LoadRunEditorDefaultsAsync(version);
        }

        private async Task LoadRunEditorDefaultsAsync(int version)
        {
            if (!SelectedRunPeriodId.HasValue || SelectedRunPeriodId.Value <= 0 ||
                !SelectedRunEmployeeId.HasValue || SelectedRunEmployeeId.Value <= 0)
            {
                if (version != _runEditorLoadVersion)
                {
                    return;
                }

                ApplyRunEditorValues(0m, 0m, 0m, 0m, 0m, "GENERATED");
                return;
            }

            var periodId = SelectedRunPeriodId.Value;
            var employeeId = (int)SelectedRunEmployeeId.Value;

            var existingRun = _allRuns.FirstOrDefault(x =>
                x.PayrollPeriodId == periodId &&
                x.EmployeeId == employeeId);

            try
            {
                var defaults = existingRun == null
                    ? await _dataService.GetRunEditorDefaultsAsync(periodId, employeeId)
                    : new PayrollRunEditorDefaultsDto(
                        existingRun.BasicPay,
                        existingRun.Allowances,
                        existingRun.OvertimePay,
                        existingRun.OtherEarnings,
                        existingRun.DeductionsTotal,
                        existingRun.Status,
                        true);

                if (version != _runEditorLoadVersion)
                {
                    return;
                }

                ApplyRunEditorValues(
                    defaults.BasicPay,
                    defaults.Allowances,
                    defaults.OvertimePay,
                    defaults.OtherEarnings,
                    defaults.DeductionsTotal,
                    defaults.Status);
            }
            catch (Exception ex)
            {
                if (version != _runEditorLoadVersion)
                {
                    return;
                }

                SetMessage($"Unable to load payroll defaults: {ex.Message}", ErrorBrush);
            }
        }

        private void ApplyRunEditorValues(
            decimal basicPay,
            decimal allowances,
            decimal overtimePay,
            decimal otherEarnings,
            decimal deductions,
            string? status)
        {
            RunBasicPay = basicPay;
            RunAllowances = allowances;
            RunOvertimePay = overtimePay;
            RunOtherEarnings = otherEarnings;
            RunDeductions = deductions;
            RunStatus = NormalizeEditableRunStatus(status);
        }

        private void RebuildPeriodRows(IReadOnlyList<PayrollPeriodDto> periods)
        {
            _allPeriods.Clear();
            foreach (var period in periods)
            {
                _allPeriods.Add(new PayrollPeriodVm(
                    period.PayrollPeriodId,
                    period.PeriodCode,
                    period.DateFrom,
                    period.DateTo,
                    period.PayDate,
                    period.Status,
                    period.CreatedAt));
            }

            ApplyPeriodFilters();
        }

        private void RebuildRunRows(IReadOnlyList<PayrollRunDto> runs)
        {
            _allRuns.Clear();
            foreach (var run in runs)
            {
                _allRuns.Add(new PayrollRunVm(
                    run.PayrollRunId,
                    run.PayrollPeriodId,
                    run.PeriodCode,
                    run.EmployeeId,
                    run.EmployeeNo,
                    run.EmployeeName,
                    run.BasicPay,
                    run.Allowances,
                    run.OvertimePay,
                    run.OtherEarnings,
                    run.GrossPay,
                    run.DeductionsTotal,
                    run.NetPay,
                    run.Status,
                    run.GeneratedAt,
                    run.LastReleasedAt,
                    run.ReleaseCount));
            }

            ApplyRunFilters();
        }

        private void RecalculateYtdTotals()
        {
            var currentYear = DateTime.Today.Year;
            IEnumerable<PayrollRunVm> source = _allRuns.Where(x => x.GeneratedAt.Year == currentYear);

            if (IsEmployeeMode && _currentEmployeeId.HasValue && _currentEmployeeId.Value > 0)
            {
                source = source.Where(x => x.EmployeeId == _currentEmployeeId.Value);
            }

            YtdGrossPay = source.Sum(x => x.GrossPay);
            YtdDeductions = source.Sum(x => x.DeductionsTotal);
            YtdNetPay = source.Sum(x => x.NetPay);
        }

        private void RebuildReleaseRows(IReadOnlyList<PayrollReleaseDto> logs)
        {
            _allReleaseLogs.Clear();
            foreach (var log in logs)
            {
                _allReleaseLogs.Add(new PayrollReleaseLogVm(
                    log.PayslipReleaseId,
                    log.PayrollRunId,
                    log.PeriodCode,
                    log.EmployeeNo,
                    log.EmployeeName,
                    log.ReleasedAt,
                    log.RunStatus,
                    log.ReleasedBy,
                    log.Remarks));
            }

            ApplyReleaseFilters();
        }

        private void RebuildOptions(IReadOnlyList<PayrollPeriodDto> periods, IReadOnlyList<PayrollEmployeeOptionDto> employees, IReadOnlyList<PayrollRunDto> runs)
        {
            var selectedFilter = SelectedRunPeriodFilterId;
            var selectedRunPeriod = SelectedRunPeriodId;
            var selectedEmployee = SelectedRunEmployeeId;
            var selectedReleaseRun = SelectedReleaseRunId;

            PeriodOptions.Clear();
            PeriodOptions.Add(new PayrollLookupOptionVm(0, "All periods"));
            foreach (var period in periods)
            {
                PeriodOptions.Add(new PayrollLookupOptionVm(period.PayrollPeriodId, period.PeriodCode));
            }

            if (!PeriodOptions.Any(x => x.Id == selectedFilter))
            {
                SelectedRunPeriodFilterId = 0;
            }

            if (!PeriodOptions.Any(x => x.Id == selectedRunPeriod))
            {
                SelectedRunPeriodId = PeriodOptions.Count > 1 ? PeriodOptions[1].Id : null;
            }

            EmployeeOptions.Clear();
            foreach (var employee in employees)
            {
                EmployeeOptions.Add(new PayrollLookupOptionVm(employee.EmployeeId, $"{employee.EmployeeNo} - {employee.EmployeeName}"));
            }

            if (!EmployeeOptions.Any(x => x.Id == selectedEmployee))
            {
                SelectedRunEmployeeId = EmployeeOptions.Count > 0 ? EmployeeOptions[0].Id : null;
            }

            RunOptions.Clear();
            foreach (var run in runs.OrderByDescending(x => x.GeneratedAt))
            {
                RunOptions.Add(new PayrollLookupOptionVm(run.PayrollRunId, $"#{run.PayrollRunId} - {run.PeriodCode} - {run.EmployeeNo}"));
            }

            if (!RunOptions.Any(x => x.Id == selectedReleaseRun))
            {
                SelectedReleaseRunId = RunOptions.Count > 0 ? RunOptions[0].Id : null;
            }
        }

        private void ApplyPeriodFilters()
        {
            IEnumerable<PayrollPeriodVm> query = _allPeriods;

            if (!string.Equals(SelectedPeriodStatusFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals(x.Status, SelectedPeriodStatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            var search = (PeriodSearchText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    Contains(x.PeriodCode, search) ||
                    Contains(x.Status, search) ||
                    Contains(x.DateFromText, search) ||
                    Contains(x.DateToText, search));
            }

            PayrollPeriods.Clear();
            foreach (var row in query)
            {
                PayrollPeriods.Add(row);
            }
        }

        private void ApplyRunFilters()
        {
            var selectedId = SelectedRun?.PayrollRunId;
            IEnumerable<PayrollRunVm> query = _allRuns;

            if (SelectedRunPeriodFilterId > 0)
            {
                query = query.Where(x => x.PayrollPeriodId == SelectedRunPeriodFilterId);
            }

            if (!string.Equals(SelectedRunStatusFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals(x.Status, SelectedRunStatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            var search = (RunSearchText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    Contains(x.EmployeeNo, search) ||
                    Contains(x.EmployeeName, search) ||
                    Contains(x.PeriodCode, search));
            }

            PayrollRuns.Clear();
            foreach (var row in query)
            {
                PayrollRuns.Add(row);
            }

            if (selectedId.HasValue)
            {
                SelectedRun = PayrollRuns.FirstOrDefault(x => x.PayrollRunId == selectedId.Value);
            }

            if (SelectedRun == null)
            {
                SelectedRun = PayrollRuns.Count > 0 ? PayrollRuns[0] : null;
            }
        }

        private void ApplyReleaseFilters()
        {
            IEnumerable<PayrollReleaseLogVm> query = _allReleaseLogs;
            var search = (ReleaseSearchText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    Contains(x.PeriodCode, search) ||
                    Contains(x.EmployeeNo, search) ||
                    Contains(x.EmployeeName, search) ||
                    Contains(x.Status, search) ||
                    Contains(x.ReleasedBy, search) ||
                    Contains(x.Remarks, search));
            }

            PayslipReleases.Clear();
            foreach (var row in query)
            {
                PayslipReleases.Add(row);
            }
        }

        private static bool Contains(string source, string search) =>
            !string.IsNullOrWhiteSpace(source) &&
            source.Contains(search, StringComparison.OrdinalIgnoreCase);

        private static string NormalizeEditableRunStatus(string? status)
        {
            var normalized = status?.Trim().ToUpperInvariant();
            return normalized switch
            {
                "DRAFT" => "DRAFT",
                "GENERATED" => "GENERATED",
                "APPROVED" => "APPROVED",
                "RELEASED" => "RELEASED",
                "VOID" => "VOID",
                _ => "GENERATED"
            };
        }

        private bool EnsureAdminOrHrAction(string actionName)
        {
            if (IsAdminOrHrMode)
            {
                return true;
            }

            SetMessage($"You can only view your payroll. You cannot {actionName}.", ErrorBrush);
            return false;
        }

        private void SetMessage(string message, Brush brush)
        {
            ActionMessage = message;
            ActionMessageBrush = brush;
        }

        private void ClearForUnlinkedEmployee()
        {
            TotalPeriods = 0;
            OpenPeriods = 0;
            TotalRuns = 0;
            ReleasedPayslips = 0;
            TotalNetPay = 0m;
            YtdGrossPay = 0m;
            YtdDeductions = 0m;
            YtdNetPay = 0m;

            _allPeriods.Clear();
            _allRuns.Clear();
            _allReleaseLogs.Clear();
            PayrollPeriods.Clear();
            PayrollRuns.Clear();
            PayslipReleases.Clear();

            PeriodOptions.Clear();
            PeriodOptions.Add(new PayrollLookupOptionVm(0, "All periods"));
            EmployeeOptions.Clear();
            RunOptions.Clear();

            SelectedRun = null;
            SelectedRunPeriodId = null;
            SelectedRunEmployeeId = null;
            SelectedReleaseRunId = null;
            PayrollConcernDetails = string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PayrollLookupOptionVm
    {
        public PayrollLookupOptionVm(long id, string label)
        {
            Id = id;
            Label = string.IsNullOrWhiteSpace(label) ? "-" : label.Trim();
        }

        public long Id { get; }
        public string Label { get; }
    }

    public class PayrollPeriodVm : INotifyPropertyChanged
    {
        private string _status;

        public PayrollPeriodVm(long payrollPeriodId, string periodCode, DateTime dateFrom, DateTime dateTo, DateTime payDate, string status, DateTime createdAt)
        {
            PayrollPeriodId = payrollPeriodId;
            PeriodCode = periodCode;
            DateFrom = dateFrom;
            DateTo = dateTo;
            PayDate = payDate;
            _status = string.IsNullOrWhiteSpace(status) ? "OPEN" : status.Trim().ToUpperInvariant();
            CreatedAt = createdAt;
        }

        public long PayrollPeriodId { get; }
        public string PeriodCode { get; }
        public DateTime DateFrom { get; }
        public DateTime DateTo { get; }
        public DateTime PayDate { get; }
        public DateTime CreatedAt { get; }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _status = value.Trim().ToUpperInvariant();
                OnPropertyChanged();
            }
        }

        public string DateFromText => DateFrom.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string DateToText => DateTo.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string PayDateText => PayDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string CreatedAtText => CreatedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PayrollRunVm : INotifyPropertyChanged
    {
        private string _status;

        public PayrollRunVm(
            long payrollRunId,
            long payrollPeriodId,
            string periodCode,
            int employeeId,
            string employeeNo,
            string employeeName,
            decimal basicPay,
            decimal allowances,
            decimal overtimePay,
            decimal otherEarnings,
            decimal grossPay,
            decimal deductionsTotal,
            decimal netPay,
            string status,
            DateTime generatedAt,
            DateTime? lastReleasedAt,
            int releaseCount)
        {
            PayrollRunId = payrollRunId;
            PayrollPeriodId = payrollPeriodId;
            PeriodCode = periodCode;
            EmployeeId = employeeId;
            EmployeeNo = employeeNo;
            EmployeeName = employeeName;
            BasicPay = basicPay;
            Allowances = allowances;
            OvertimePay = overtimePay;
            OtherEarnings = otherEarnings;
            GrossPay = grossPay;
            DeductionsTotal = deductionsTotal;
            NetPay = netPay;
            _status = string.IsNullOrWhiteSpace(status) ? "GENERATED" : status.Trim().ToUpperInvariant();
            GeneratedAt = generatedAt;
            LastReleasedAt = lastReleasedAt;
            ReleaseCount = releaseCount;
        }

        public long PayrollRunId { get; }
        public long PayrollPeriodId { get; }
        public string PeriodCode { get; }
        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public decimal BasicPay { get; }
        public decimal Allowances { get; }
        public decimal OvertimePay { get; set; }
        public decimal OtherEarnings { get; }
        public decimal GrossPay { get; }
        public decimal DeductionsTotal { get; }
        public decimal NetPay { get; }
        public DateTime GeneratedAt { get; }
        public DateTime? LastReleasedAt { get; }
        public int ReleaseCount { get; }
        public bool CanOpenPayslip => ReleaseCount > 0 || string.Equals(Status, "RELEASED", StringComparison.OrdinalIgnoreCase);

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _status = value.Trim().ToUpperInvariant();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanOpenPayslip));
            }
        }

        public string GeneratedAtText => GeneratedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
        public string LastReleasedAtText => LastReleasedAt.HasValue
            ? LastReleasedAt.Value.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture)
            : "-";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PayrollReleaseLogVm
    {
        public PayrollReleaseLogVm(
            long payslipReleaseId,
            long payrollRunId,
            string periodCode,
            string employeeNo,
            string employeeName,
            DateTime releasedAt,
            string status,
            string releasedBy,
            string remarks)
        {
            PayslipReleaseId = payslipReleaseId;
            PayrollRunId = payrollRunId;
            PeriodCode = periodCode;
            EmployeeNo = employeeNo;
            EmployeeName = employeeName;
            ReleasedAt = releasedAt;
            Status = string.IsNullOrWhiteSpace(status) ? "RELEASED" : status.Trim().ToUpperInvariant();
            ReleasedBy = releasedBy;
            Remarks = string.IsNullOrWhiteSpace(remarks) ? "-" : remarks.Trim();
        }

        public long PayslipReleaseId { get; }
        public long PayrollRunId { get; }
        public string PeriodCode { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public DateTime ReleasedAt { get; }
        public string Status { get; }
        public string ReleasedBy { get; }
        public string Remarks { get; }
        public string ReleasedAtText => ReleasedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
    }
}
