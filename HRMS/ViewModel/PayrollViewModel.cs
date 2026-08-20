using HRMS.Model;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
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
using System.Text;
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
        private readonly List<PayrollGovernmentContributionSourceDto> _allGovernmentContributionSources = new();
        private const long AllPeriodsOptionId = 0;
        private const int GgmsOfficeId = 18;
        private const string GgmsOfficeCode = "OFF-2026-0007";

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
        private long _ggmsAllocationId;
        private string _ggmsProgram = "-";
        private decimal _ggmsAllocatedAmount;
        private decimal _ggmsUsedAmount;
        private decimal _ggmsRemainingAmount;
        private string _ggmsSyncStatus = "GGMS allocation not synced yet.";
        private Brush _ggmsSyncStatusBrush = InfoBrush;

        private string _periodSearchText = string.Empty;
        private string _selectedPeriodStatusFilter = "All";
        private string _runSearchText = string.Empty;
        private long? _selectedRunPeriodFilterId;
        private string _selectedRunStatusFilter = "All";
        private string _releaseSearchText = string.Empty;
        private long? _selectedGovernmentPeriodId;
        private long? _selectedGovernmentEmployeeId;
        private string _selectedGovernmentContributionType = "ALL";

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
        private string _runEmploymentTypeName = string.Empty;
        private string _runPositionName = string.Empty;
        private string _runStatus = "GENERATED";
        private int _runEditorLoadVersion;
        private bool _isApplyingRunEditorValues;
        private int _runDtrMinusMinutes;
        private decimal _runAbsentDays;
        private PayrollDeductionResult _runDeductionPreview = new();

        private PayrollRunVm? _selectedRun;
        private long? _selectedReleaseRunId;
        private string _releaseRemarks = string.Empty;
        private string _payrollConcernDetails = string.Empty;
        private PayrollConcernVm? _selectedPayrollConcern;
        private string _concernResolutionNotes = string.Empty;
        private decimal _governmentReportEmployeeShareTotal;
        private decimal _governmentReportEmployerShareTotal;
        private decimal _governmentReportRemittanceTotal;

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
        public int VisiblePayrollRunCount => PayrollRuns
            .Select(x => x.EmployeeId)
            .Distinct()
            .Count();
        public int SelectedPayrollRunCount => PayrollRuns.Count(x => x.IsSelectedForBulk);
        public string SelectedPayrollRunCountText => $"{SelectedPayrollRunCount} selected";
        public string VisiblePayrollGrossText => $"PHP {PayrollRuns.Sum(x => x.GrossPay):N2}";
        public string VisiblePayrollDeductionsText => $"PHP {PayrollRuns.Sum(x => x.DeductionsTotal):N2}";
        public string VisiblePayrollNetText => $"PHP {PayrollRuns.Sum(x => x.NetPay):N2}";
        public long GgmsAllocationId
        {
            get => _ggmsAllocationId;
            private set
            {
                if (_ggmsAllocationId == value)
                {
                    return;
                }

                _ggmsAllocationId = value;
                OnPropertyChanged();
            }
        }
        public string GgmsProgram
        {
            get => _ggmsProgram;
            private set
            {
                if (string.Equals(_ggmsProgram, value, StringComparison.Ordinal))
                {
                    return;
                }

                _ggmsProgram = value;
                OnPropertyChanged();
            }
        }
        public decimal GgmsAllocatedAmount
        {
            get => _ggmsAllocatedAmount;
            private set
            {
                if (_ggmsAllocatedAmount == value)
                {
                    return;
                }

                _ggmsAllocatedAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GgmsAllocatedAmountText));
            }
        }
        public decimal GgmsUsedAmount
        {
            get => _ggmsUsedAmount;
            private set
            {
                if (_ggmsUsedAmount == value)
                {
                    return;
                }

                _ggmsUsedAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GgmsUsedAmountText));
            }
        }
        public decimal GgmsRemainingAmount
        {
            get => _ggmsRemainingAmount;
            private set
            {
                if (_ggmsRemainingAmount == value)
                {
                    return;
                }

                _ggmsRemainingAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GgmsRemainingAmountText));
            }
        }
        public string GgmsAllocatedAmountText => $"PHP {GgmsAllocatedAmount:N2}";
        public string GgmsUsedAmountText => $"PHP {GgmsUsedAmount:N2}";
        public string GgmsRemainingAmountText => $"PHP {GgmsRemainingAmount:N2}";
        public string GgmsSyncStatus
        {
            get => _ggmsSyncStatus;
            private set
            {
                if (string.Equals(_ggmsSyncStatus, value, StringComparison.Ordinal))
                {
                    return;
                }

                _ggmsSyncStatus = value;
                OnPropertyChanged();
            }
        }
        public Brush GgmsSyncStatusBrush
        {
            get => _ggmsSyncStatusBrush;
            private set
            {
                if (Equals(_ggmsSyncStatusBrush, value))
                {
                    return;
                }

                _ggmsSyncStatusBrush = value;
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
                OnPropertyChanged(nameof(DeductionsTabLabel));
                OnPropertyChanged(nameof(ConcernsTabLabel));
                OnPropertyChanged(nameof(RunActionsHeader));
                OnPropertyChanged(nameof(ReleaseActionsHeader));
                OnPropertyChanged(nameof(CanReportPayrollConcern));
            }
        }
        public bool IsAdminOrHrMode => !IsEmployeeMode;
        public string PageHeaderTitle => IsEmployeeMode ? "My Payroll" : "Payroll";
        public string PageHeaderSubtitle => IsEmployeeMode
            ? "Review your released payroll and download official payslips."
            : "Generate payroll, review net pay, and release payslips.";
        public string RunsCardLabel => IsEmployeeMode ? "My Released Payroll" : "Payroll Runs";
        public string ReleasesCardLabel => IsEmployeeMode ? "My Released Payslips" : "Released Payslips";
        public string NetPayCardLabel => IsEmployeeMode ? "My Total Net Pay" : "Total Net Pay";
        public string PeriodsTabLabel => IsEmployeeMode ? "Payroll Periods" : "Payroll Periods";
        public string RunsTabLabel => IsEmployeeMode ? "My Released Payroll" : "Payroll Runs";
        public string ReleaseTabLabel => IsEmployeeMode ? "My Payslip Release Logs" : "Payslip Release Logs";
        public string DeductionsTabLabel => IsEmployeeMode ? "My Deductions" : "Deductions";
        public string ConcernsTabLabel => IsEmployeeMode ? "My Payroll Concerns" : "Payroll Concerns";
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

        public PayrollConcernVm? SelectedPayrollConcern
        {
            get => _selectedPayrollConcern;
            set
            {
                if (_selectedPayrollConcern == value) return;
                _selectedPayrollConcern = value;
                OnPropertyChanged();
            }
        }

        public string ConcernResolutionNotes
        {
            get => _concernResolutionNotes;
            set
            {
                if (_concernResolutionNotes == value) return;
                _concernResolutionNotes = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
        public string SelectedRunPeriodLabel => SelectedRun?.PeriodCode ?? "-";
        public string SelectedRunGeneratedLabel => SelectedRun?.GeneratedAtText ?? "-";
        public string SelectedRunStatusLabel => SelectedRun?.Status ?? "-";
        public string GovernmentContributionTitle => IsAllDeductionsReport
            ? "All Deductions Report"
            : $"{GetGovernmentContributionLabel(SelectedGovernmentContributionType)} Contribution Report";
        public bool IsAllDeductionsReport =>
            string.Equals(SelectedGovernmentContributionType, "ALL", StringComparison.OrdinalIgnoreCase);
        public string GovernmentReportEmployeeShareLabel => IsAllDeductionsReport ? "Employee Deduction Total" : "Employee Share Total";
        public string GovernmentReportEmployerShareLabel => "Employer Share Total";
        public string GovernmentReportRemittanceLabel => IsAllDeductionsReport ? "Combined Total" : "Total Remittance";
        public string GovernmentReportPeriodLabel =>
            SelectedGovernmentPeriodId.GetValueOrDefault() > 0
                ? PeriodOptions.FirstOrDefault(x => x.Id == SelectedGovernmentPeriodId.GetValueOrDefault())?.Label ?? "Selected period"
                : "All payroll periods";
        public string GovernmentReportEmployeeLabel =>
            SelectedGovernmentEmployeeId.GetValueOrDefault() > 0
                ? DeductionEmployeeOptions.FirstOrDefault(x => x.Id == SelectedGovernmentEmployeeId.GetValueOrDefault())?.Label ?? "Selected employee"
                : "All employees";
        public decimal GovernmentReportEmployeeShareTotal
        {
            get => _governmentReportEmployeeShareTotal;
            private set
            {
                if (_governmentReportEmployeeShareTotal == value)
                {
                    return;
                }

                _governmentReportEmployeeShareTotal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GovernmentReportEmployeeShareTotalText));
            }
        }
        public decimal GovernmentReportEmployerShareTotal
        {
            get => _governmentReportEmployerShareTotal;
            private set
            {
                if (_governmentReportEmployerShareTotal == value)
                {
                    return;
                }

                _governmentReportEmployerShareTotal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GovernmentReportEmployerShareTotalText));
            }
        }
        public decimal GovernmentReportRemittanceTotal
        {
            get => _governmentReportRemittanceTotal;
            private set
            {
                if (_governmentReportRemittanceTotal == value)
                {
                    return;
                }

                _governmentReportRemittanceTotal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GovernmentReportRemittanceTotalText));
            }
        }
        public string GovernmentReportEmployeeShareTotalText => $"PHP {GovernmentReportEmployeeShareTotal:N2}";
        public string GovernmentReportEmployerShareTotalText => $"PHP {GovernmentReportEmployerShareTotal:N2}";
        public string GovernmentReportRemittanceTotalText => $"PHP {GovernmentReportRemittanceTotal:N2}";
        public bool HasGovernmentContributionRows => GovernmentContributionRows.Count > 0;

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

        public long? SelectedRunPeriodFilterId
        {
            get => _selectedRunPeriodFilterId;
            set
            {
                var normalized = value.GetValueOrDefault(AllPeriodsOptionId);
                if (_selectedRunPeriodFilterId == normalized) return;
                _selectedRunPeriodFilterId = normalized;
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

        public long? SelectedGovernmentPeriodId
        {
            get => _selectedGovernmentPeriodId;
            set
            {
                var normalized = value.GetValueOrDefault(AllPeriodsOptionId);
                if (_selectedGovernmentPeriodId == normalized)
                {
                    return;
                }

                _selectedGovernmentPeriodId = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GovernmentReportPeriodLabel));
                ApplyGovernmentContributionRows();
            }
        }

        public long? SelectedGovernmentEmployeeId
        {
            get => _selectedGovernmentEmployeeId;
            set
            {
                var normalized = value.GetValueOrDefault();
                if (_selectedGovernmentEmployeeId == normalized)
                {
                    return;
                }

                _selectedGovernmentEmployeeId = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GovernmentReportEmployeeLabel));
                ApplyGovernmentContributionRows();
            }
        }

        public string SelectedGovernmentContributionType
        {
            get => _selectedGovernmentContributionType;
            set
            {
                if (string.Equals(_selectedGovernmentContributionType, value, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _selectedGovernmentContributionType = value.Trim().ToUpperInvariant();
                OnPropertyChanged();
                OnPropertyChanged(nameof(GovernmentContributionTitle));
                OnPropertyChanged(nameof(IsAllDeductionsReport));
                OnPropertyChanged(nameof(GovernmentReportEmployeeShareLabel));
                OnPropertyChanged(nameof(GovernmentReportEmployerShareLabel));
                OnPropertyChanged(nameof(GovernmentReportRemittanceLabel));
                ApplyGovernmentContributionRows();
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
        public decimal RunBasicPay { get => _runBasicPay; set { if (_runBasicPay != value) { _runBasicPay = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunBasicPayText)); OnPropertyChanged(nameof(RunGrossPreview)); OnPropertyChanged(nameof(RunNetPreview)); if (!_isApplyingRunEditorValues) RecalculateRunEditorDeductions(); } } }
        public decimal RunAllowances { get => _runAllowances; set { if (_runAllowances != value) { _runAllowances = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunAllowancesText)); OnPropertyChanged(nameof(RunGrossPreview)); OnPropertyChanged(nameof(RunNetPreview)); if (!_isApplyingRunEditorValues) RecalculateRunEditorDeductions(); } } }
        public decimal RunOvertimePay { get => _runOvertimePay; set { if (_runOvertimePay != value) { _runOvertimePay = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunOvertimePayText)); OnPropertyChanged(nameof(RunGrossPreview)); OnPropertyChanged(nameof(RunNetPreview)); if (!_isApplyingRunEditorValues) RecalculateRunEditorDeductions(); } } }
        public decimal RunOtherEarnings { get => _runOtherEarnings; set { if (_runOtherEarnings != value) { _runOtherEarnings = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunOtherEarningsText)); OnPropertyChanged(nameof(RunGrossPreview)); OnPropertyChanged(nameof(RunNetPreview)); if (!_isApplyingRunEditorValues) RecalculateRunEditorDeductions(); } } }
        public decimal RunDeductions { get => _runDeductions; set { if (_runDeductions != value) { _runDeductions = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunNetPreview)); } } }
        public string RunBasicPayText { get => FormatEditableAmount(RunBasicPay); set => UpdateEditableAmount(value, amount => RunBasicPay = amount); }
        public string RunAllowancesText { get => FormatEditableAmount(RunAllowances); set => UpdateEditableAmount(value, amount => RunAllowances = amount); }
        public string RunOvertimePayText { get => FormatEditableAmount(RunOvertimePay); set => UpdateEditableAmount(value, amount => RunOvertimePay = amount); }
        public string RunOtherEarningsText { get => FormatEditableAmount(RunOtherEarnings); set => UpdateEditableAmount(value, amount => RunOtherEarnings = amount); }
        public string RunStatus { get => _runStatus; set { if (_runStatus != value && !string.IsNullOrWhiteSpace(value)) { _runStatus = value; OnPropertyChanged(); } } }
        public decimal RunGrossPreview => RunBasicPay + RunAllowances + RunOvertimePay + RunOtherEarnings;
        public decimal RunNetPreview => RunGrossPreview - RunDeductions;
        public string RunEmployeeProfileText => string.IsNullOrWhiteSpace(_runEmploymentTypeName)
            ? "Select an employee to compute deductions."
            : $"{_runPositionName} | {_runEmploymentTypeName}";
        public string RunDeductionBreakdownText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_runEmploymentTypeName))
                {
                    return "Deductions will be computed from the employee's appointment type and current run earnings.";
                }

                var retirementLabel = _runDeductionPreview.GsisContribution > 0m ? "GSIS" : "SSS";
                var retirementAmount = _runDeductionPreview.GsisContribution + _runDeductionPreview.SssContribution;
                var dtrMinusText = _runDtrMinusMinutes > 0
                    ? $" | DTR Minus: PHP {_runDeductionPreview.LateDeduction:N2} ({_runDtrMinusMinutes} min)"
                    : string.Empty;
                var absenceText = _runAbsentDays > 0m
                    ? $" | Absence: PHP {_runDeductionPreview.AbsenceDeduction:N2} ({_runAbsentDays:0.##} day(s))"
                    : string.Empty;
                return $"{retirementLabel}: PHP {retirementAmount:N2} | PhilHealth: PHP {_runDeductionPreview.PhilHealthContribution:N2} | Pag-IBIG: PHP {_runDeductionPreview.PagIBIGContribution:N2} | Tax: PHP {_runDeductionPreview.TaxWithheld:N2}{absenceText}{dtrMinusText}";
            }
        }

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
        public ObservableCollection<string> EditableRunStatuses { get; } = new() { "DRAFT", "GENERATED", "APPROVED", "VOID" };
        public ObservableCollection<string> GovernmentContributionTypeOptions { get; } = new() { "ALL", "SSS", "GSIS", "PHILHEALTH", "PAGIBIG" };

        public ObservableCollection<PayrollLookupOptionVm> PeriodOptions { get; } = new();
        public ObservableCollection<PayrollLookupOptionVm> PayrollPeriodOptions { get; } = new();
        public ObservableCollection<PayrollLookupOptionVm> EmployeeOptions { get; } = new();
        public ObservableCollection<PayrollLookupOptionVm> DeductionEmployeeOptions { get; } = new();
        public ObservableCollection<PayrollLookupOptionVm> RunOptions { get; } = new();
        public ObservableCollection<PayrollPeriodVm> PayrollPeriods { get; } = new();
        public ObservableCollection<PayrollRunVm> PayrollRuns { get; } = new();
        public ObservableCollection<PayrollReleaseLogVm> PayslipReleases { get; } = new();
        public ObservableCollection<PayrollConcernVm> PayrollConcerns { get; } = new();
        public ObservableCollection<PayrollGovernmentContributionRowVm> GovernmentContributionRows { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand AddPeriodCommand { get; }
        public ICommand SavePeriodCommand { get; }
        public ICommand DeletePeriodCommand { get; }
        public ICommand UpsertRunCommand { get; }
        public ICommand GenerateAllRunsCommand { get; }
        public ICommand ApproveRunCommand { get; }
        public ICommand ApproveSelectedRunsCommand { get; }
        public ICommand ReleaseSelectedRunsCommand { get; }
        public ICommand ToggleSelectAllRunsCommand { get; }
        public ICommand SelectRunCommand { get; }
        public ICommand SaveRunStatusCommand { get; }
        public ICommand DeleteRunCommand { get; }
        public ICommand ReleasePayslipCommand { get; }
        public ICommand DownloadPayslipCommand { get; }
        public ICommand PrintPayslipCommand { get; }
        public ICommand ReportPayrollConcernCommand { get; }
        public ICommand ReviewPayrollConcernCommand { get; }
        public ICommand ResolvePayrollConcernCommand { get; }
        public ICommand RejectPayrollConcernCommand { get; }
        public ICommand ExportGovernmentReportCommand { get; }
        public ICommand SaveGovernmentReportPdfCommand { get; }
        public ICommand ViewAllDeductionsCommand { get; }
        public ICommand ViewSssCommand { get; }
        public ICommand ViewGsisCommand { get; }
        public ICommand ViewPhilHealthCommand { get; }
        public ICommand ViewPagIbigCommand { get; }

        public PayrollViewModel()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
            AddPeriodCommand = new AsyncRelayCommand(_ => AddPeriodAsync());
            SavePeriodCommand = new AsyncRelayCommand(SavePeriodAsync);
            DeletePeriodCommand = new AsyncRelayCommand(DeletePeriodAsync);
            UpsertRunCommand = new AsyncRelayCommand(_ => UpsertRunAsync());
            GenerateAllRunsCommand = new AsyncRelayCommand(_ => GenerateAllRunsAsync());
            ApproveRunCommand = new AsyncRelayCommand(ApproveRunAsync);
            ApproveSelectedRunsCommand = new AsyncRelayCommand(_ => ApproveSelectedRunsAsync());
            ReleaseSelectedRunsCommand = new AsyncRelayCommand(_ => ReleaseSelectedRunsAsync());
            ToggleSelectAllRunsCommand = new AsyncRelayCommand(_ => ToggleSelectAllRunsAsync());
            SelectRunCommand = new AsyncRelayCommand(SelectRunAsync);
            SaveRunStatusCommand = new AsyncRelayCommand(SaveRunStatusAsync);
            DeleteRunCommand = new AsyncRelayCommand(DeleteRunAsync);
            ReleasePayslipCommand = new AsyncRelayCommand(ReleasePayslipAsync);
            DownloadPayslipCommand = new AsyncRelayCommand(DownloadPayslipAsync);
            PrintPayslipCommand = new AsyncRelayCommand(PrintPayslipAsync);
            ReportPayrollConcernCommand = new AsyncRelayCommand(ReportPayrollConcernAsync);
            ReviewPayrollConcernCommand = new AsyncRelayCommand(p => UpdatePayrollConcernAsync(p, "IN_REVIEW"));
            ResolvePayrollConcernCommand = new AsyncRelayCommand(p => UpdatePayrollConcernAsync(p, "RESOLVED"));
            RejectPayrollConcernCommand = new AsyncRelayCommand(p => UpdatePayrollConcernAsync(p, "REJECTED"));
            ExportGovernmentReportCommand = new AsyncRelayCommand(_ => ExportGovernmentReportAsync());
            SaveGovernmentReportPdfCommand = new AsyncRelayCommand(_ => SaveGovernmentReportPdfAsync());
            ViewAllDeductionsCommand = new AsyncRelayCommand(_ => SwitchGovernmentContributionType("ALL"));
            ViewSssCommand = new AsyncRelayCommand(_ => SwitchGovernmentContributionType("SSS"));
            ViewGsisCommand = new AsyncRelayCommand(_ => SwitchGovernmentContributionType("GSIS"));
            ViewPhilHealthCommand = new AsyncRelayCommand(_ => SwitchGovernmentContributionType("PHILHEALTH"));
            ViewPagIbigCommand = new AsyncRelayCommand(_ => SwitchGovernmentContributionType("PAGIBIG"));

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

                var statsTask = _dataService.GetStatsAsync(scopedEmployeeId, releasedOnly: IsEmployeeMode);
                var periodsTask = _dataService.GetPeriodsAsync(limit: 400, employeeId: scopedEmployeeId, releasedOnly: IsEmployeeMode);
                var employeesTask = _dataService.GetEmployeesAsync(scopedEmployeeId);
                var runsTask = _dataService.GetRunsAsync(limit: 700, employeeId: scopedEmployeeId, releasedOnly: IsEmployeeMode);
                var releasesTask = _dataService.GetReleaseLogsAsync(limit: 700, employeeId: scopedEmployeeId);
                var ggmsAllocationTask = LoadGgmsAllocationAsync();
                var governmentContributionTask = _dataService.GetGovernmentContributionSourcesAsync(
                    limit: 1500,
                    employeeId: scopedEmployeeId,
                    releasedOnly: IsEmployeeMode);
                var concernsTask = _dataService.GetPayrollConcernsAsync(scopedEmployeeId, limit: 500);

                var stats = await statsTask;
                var periods = await periodsTask;
                var employees = await employeesTask;
                var runs = await runsTask;
                var releases = await releasesTask;
                var governmentContributions = await governmentContributionTask;
                var concerns = await concernsTask;
                await ggmsAllocationTask;

                TotalPeriods = stats.TotalPeriods;
                OpenPeriods = stats.OpenPeriods;
                TotalRuns = stats.TotalRuns;
                ReleasedPayslips = stats.ReleasedPayslips;
                TotalNetPay = stats.TotalNetPay;

                RebuildPeriodRows(periods);
                RebuildRunRows(runs);
                RebuildReleaseRows(releases);
                RebuildOptions(periods, employees, runs);
                RebuildGovernmentContributionSources(governmentContributions);
                PayrollConcerns.Clear();
                foreach (var concern in concerns)
                {
                    PayrollConcerns.Add(new PayrollConcernVm(concern));
                }
                SelectedPayrollConcern = PayrollConcerns.FirstOrDefault();
                RecalculateYtdTotals();

                if (IsEmployeeMode && _currentEmployeeId.HasValue && _currentEmployeeId.Value > 0)
                {
                    SelectedRunEmployeeId = _currentEmployeeId.Value;
                }

                if (!SelectedRunPeriodId.HasValue)
                {
                    SelectedRunPeriodId = AllPeriodsOptionId;
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

        private async Task LoadGgmsAllocationAsync()
        {
            try
            {
                var ggmsService = new GgmsFundAllocationService(GgmsConfig.ConnectionString, GgmsOfficeId, GgmsOfficeCode);
                var allocation = await ggmsService.GetActiveAllocationAsync();
                if (allocation == null)
                {
                    GgmsAllocationId = 0;
                    GgmsProgram = "-";
                    GgmsAllocatedAmount = 0m;
                    GgmsUsedAmount = 0m;
                    GgmsRemainingAmount = 0m;
                    GgmsSyncStatus = "No active GGMS allocation found for Office ID 18 (OFF-2026-0007).";
                    GgmsSyncStatusBrush = ErrorBrush;
                    return;
                }

                GgmsAllocationId = allocation.AllocationId;
                GgmsProgram = allocation.Program;
                GgmsAllocatedAmount = allocation.AllocatedAmount;
                GgmsUsedAmount = allocation.UsedAmount;
                GgmsRemainingAmount = allocation.RemainingAmount;
                GgmsSyncStatus = $"GGMS allocation synced (ID #{allocation.AllocationId}, Office: {GgmsOfficeCode}).";
                GgmsSyncStatusBrush = SuccessBrush;
            }
            catch (Exception ex)
            {
                GgmsSyncStatus = $"GGMS sync failed: {ex.Message}";
                GgmsSyncStatusBrush = ErrorBrush;
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
                SelectedRunPeriodId = id;
                await RefreshAsync();
                SelectedRunPeriodId = id;
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

        private async Task GenerateAllRunsAsync()
        {
            if (!EnsureAdminOrHrAction("generate payroll for all employees"))
            {
                return;
            }

            if (!SelectedRunPeriodId.HasValue || SelectedRunPeriodId.Value <= 0)
            {
                SetMessage("Select a payroll period first before generating.", ErrorBrush);
                return;
            }

            try
            {
                IsBusy = true;
                SetMessage("Generating payroll for all active employees...", InfoBrush);

                var result = await _dataService.GenerateAllRunsAsync(SelectedRunPeriodId.Value);

                await RefreshAsync();
                if (result.FailedCount == 0)
                {
                    SetMessage($"Payroll generated for {result.GeneratedCount} employee(s). No failures.", SuccessBrush);
                }
                else
                {
                    var sample = string.Join(" | ", result.FailureDetails.Take(3));
                    var more = result.FailedCount > 3 ? $" | +{result.FailedCount - 3} more" : string.Empty;
                    SetMessage(
                        $"Generated: {result.GeneratedCount}. Failed: {result.FailedCount}. {sample}{more}",
                        ErrorBrush);
                }
                SystemRefreshBus.Raise("PayrollBatchGenerated");
            }
            catch (Exception ex)
            {
                SetMessage($"Batch generation failed: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private Task SwitchGovernmentContributionType(string type)
        {
            SelectedGovernmentContributionType = type;
            return Task.CompletedTask;
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

        private Task SelectRunAsync(object? parameter)
        {
            if (!TryResolveRunId(parameter, out var runId))
            {
                SetMessage("Select payroll run row first.", ErrorBrush);
                return Task.CompletedTask;
            }

            var row = _allRuns.FirstOrDefault(x => x.PayrollRunId == runId)
                      ?? PayrollRuns.FirstOrDefault(x => x.PayrollRunId == runId);

            if (row is null)
            {
                SetMessage($"Run #{runId} is not loaded. Refresh payroll runs and try again.", ErrorBrush);
                return Task.CompletedTask;
            }

            if (!PayrollRuns.Any(x => x.PayrollRunId == row.PayrollRunId))
            {
                RunSearchText = string.Empty;
                SelectedRunStatusFilter = "All";
                SelectedRunPeriodFilterId = AllPeriodsOptionId;
            }

            SelectedRun = PayrollRuns.FirstOrDefault(x => x.PayrollRunId == row.PayrollRunId) ?? row;
            SelectedRunPeriodId = row.PayrollPeriodId;
            SelectedRunEmployeeId = row.EmployeeId;
            SelectedReleaseRunId = row.PayrollRunId;

            SetMessage($"Run #{row.PayrollRunId} selected for {row.EmployeeName}.", InfoBrush);
            return Task.CompletedTask;
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

        private async Task ApproveRunAsync(object? parameter)
        {
            if (!EnsureAdminOrHrAction("approve payroll runs"))
            {
                return;
            }

            if (parameter is not PayrollRunVm row)
            {
                SetMessage("Select a generated payroll run first.", ErrorBrush);
                return;
            }

            if (!string.Equals(row.Status, "GENERATED", StringComparison.OrdinalIgnoreCase))
            {
                SetMessage("Only a GENERATED payroll run can be approved.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.UpdateRunStatusAsync(
                    row.PayrollRunId,
                    "APPROVED",
                    _currentUserId > 0 ? _currentUserId : null);
                await RefreshAsync();
                SelectedRun = PayrollRuns.FirstOrDefault(x => x.PayrollRunId == row.PayrollRunId);
                SetMessage($"Run #{row.PayrollRunId} approved. It is now ready for payslip release.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollRunApproved");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to approve payroll run: {ex.Message}", ErrorBrush);
            }
        }

        private Task ToggleSelectAllRunsAsync()
        {
            var shouldSelect = PayrollRuns.Any(x => !x.IsSelectedForBulk);
            foreach (var row in PayrollRuns)
            {
                row.IsSelectedForBulk = shouldSelect;
            }

            SetMessage(shouldSelect
                ? $"Selected {PayrollRuns.Count} visible payroll run(s)."
                : "Payroll run selection cleared.", InfoBrush);
            return Task.CompletedTask;
        }

        private async Task ApproveSelectedRunsAsync()
        {
            if (!EnsureAdminOrHrAction("approve payroll runs"))
            {
                return;
            }

            var selected = PayrollRuns
                .Where(x => x.IsSelectedForBulk && x.CanApprovePayroll)
                .ToList();
            if (selected.Count == 0)
            {
                SetMessage("Select at least one GENERATED payroll run to approve.", ErrorBrush);
                return;
            }

            try
            {
                foreach (var row in selected)
                {
                    await _dataService.UpdateRunStatusAsync(
                        row.PayrollRunId,
                        "APPROVED",
                        _currentUserId > 0 ? _currentUserId : null);
                }

                await RefreshAsync();
                SetMessage($"Approved {selected.Count} payroll run(s). They are ready for release.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollRunsBulkApproved");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to approve selected payroll runs: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ReleaseSelectedRunsAsync()
        {
            if (!EnsureAdminOrHrAction("release payslips"))
            {
                return;
            }

            var selected = PayrollRuns
                .Where(x => x.IsSelectedForBulk && x.CanReleasePayslip)
                .ToList();
            if (selected.Count == 0)
            {
                SetMessage("Select at least one APPROVED payroll run to release.", ErrorBrush);
                return;
            }

            if (GgmsAllocationId <= 0)
            {
                SetMessage("GGMS allocation is unavailable. Refresh and verify the connection before releasing payslips.", ErrorBrush);
                return;
            }

            var released = 0;
            var failures = new List<string>();
            var ggmsService = new GgmsFundAllocationService(GgmsConfig.ConnectionString, GgmsOfficeId, GgmsOfficeCode);
            foreach (var row in selected)
            {
                try
                {
                    var description = string.IsNullOrWhiteSpace(ReleaseRemarks)
                        ? $"HRMS payroll release for {row.EmployeeNo} - {row.EmployeeName}."
                        : ReleaseRemarks.Trim();
                    await ggmsService.RecordPayrollDisbursementAsync(
                        allocationId: GgmsAllocationId,
                        amount: row.NetPay,
                        recipientName: row.EmployeeName,
                        purpose: $"Payroll disbursement ({row.PeriodCode})",
                        description: description,
                        idempotencyReference: $"PAYROLL-RUN-{row.PayrollRunId}");
                    await _dataService.ReleasePayslipAsync(
                        row.PayrollRunId,
                        _currentUserId > 0 ? _currentUserId : null,
                        ReleaseRemarks);
                    released++;
                }
                catch (Exception ex)
                {
                    failures.Add($"#{row.PayrollRunId}: {ex.Message}");
                }
            }

            ReleaseRemarks = string.Empty;
            await RefreshAsync();
            if (failures.Count == 0)
            {
                SetMessage($"Released {released} payslip(s) successfully.", SuccessBrush);
                SystemRefreshBus.Raise("PayslipsBulkReleased");
            }
            else
            {
                SetMessage($"Released {released} payslip(s); {failures.Count} failed. {failures[0]}", ErrorBrush);
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

            if (!string.Equals(targetRun.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                SetMessage("Approve the payroll run before releasing its payslip.", ErrorBrush);
                return;
            }

            var disbursementAmount = targetRun.NetPay;
            if (disbursementAmount <= 0)
            {
                SetMessage("Cannot release payslip with zero or negative net pay.", ErrorBrush);
                return;
            }

            if (GgmsAllocationId <= 0)
            {
                SetMessage("GGMS allocation is not available for Office ID 18 (OFF-2026-0007). Refresh and verify connection first.", ErrorBrush);
                return;
            }

            try
            {
                var ggmsService = new GgmsFundAllocationService(GgmsConfig.ConnectionString, GgmsOfficeId, GgmsOfficeCode);
                var purpose = $"Payroll disbursement ({targetRun.PeriodCode})";
                var description = string.IsNullOrWhiteSpace(ReleaseRemarks)
                    ? $"HRMS payroll release for {targetRun.EmployeeNo} - {targetRun.EmployeeName}."
                    : ReleaseRemarks.Trim();

                var ggmsResult = await ggmsService.RecordPayrollDisbursementAsync(
                    allocationId: GgmsAllocationId,
                    amount: disbursementAmount,
                    recipientName: targetRun.EmployeeName,
                    purpose: purpose,
                    description: description,
                    idempotencyReference: $"PAYROLL-RUN-{runId}");

                await _dataService.ReleasePayslipAsync(runId, _currentUserId > 0 ? _currentUserId : null, ReleaseRemarks);
                ReleaseRemarks = string.Empty;
                await RefreshAsync();
                SelectedRun = PayrollRuns.FirstOrDefault(x => x.PayrollRunId == runId);
                var ggmsAction = ggmsResult.AlreadyRecorded ? "GGMS post reused safely" : "GGMS posted";
                SetMessage(
                    $"Payslip released and {ggmsAction}. Run #{runId}, Txn #{ggmsResult.TransactionId}, Remaining: PHP {ggmsResult.RemainingAfter:N2}.",
                    SuccessBrush);
                SystemRefreshBus.Raise("PayslipReleased");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to release payslip: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DownloadPayslipAsync(object? parameter)
        {
            if (!TryResolvePayslipRow(parameter, out var row))
            {
                SetMessage("Select a payroll run or release log first.", ErrorBrush);
                return;
            }

            if (!row.CanOpenPayslip)
            {
                SetMessage("Payslip is not yet released for this run.", ErrorBrush);
                return;
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
                    return;
                }

                var items = await _dataService.GetRunItemsAsync(row.PayrollRunId);
                var profile = await _dataService.GetPayslipProfileAsync(row.PayrollRunId);
                var company = await new CompanyProfileDataService(DbConfig.ConnectionString).GetCompanyProfileAsync();
                BuildPayslipPdf(row, profile, company, items).GeneratePdf(dialog.FileName);
                SetMessage($"Payslip downloaded: {Path.GetFileName(dialog.FileName)}", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to download payslip: {ex.Message}", ErrorBrush);
            }
        }

        private async Task PrintPayslipAsync(object? parameter)
        {
            if (!TryResolvePayslipRow(parameter, out var row))
            {
                SetMessage("Select a payroll run or release log first.", ErrorBrush);
                return;
            }

            if (!row.CanOpenPayslip)
            {
                SetMessage("Payslip is not yet released for this run.", ErrorBrush);
                return;
            }

            try
            {
                var safePeriod = string.IsNullOrWhiteSpace(row.PeriodCode) ? "PERIOD" : row.PeriodCode.Replace("/", "-");
                var safeEmpNo = string.IsNullOrWhiteSpace(row.EmployeeNo) ? "EMP" : row.EmployeeNo.Replace("/", "-");
                var tempPdf = Path.Combine(
                    Path.GetTempPath(),
                    $"HRMS-Payslip-{safePeriod}-{safeEmpNo}-{DateTime.Now:yyyyMMddHHmmss}.pdf");

                var items = await _dataService.GetRunItemsAsync(row.PayrollRunId);
                var profile = await _dataService.GetPayslipProfileAsync(row.PayrollRunId);
                var company = await new CompanyProfileDataService(DbConfig.ConnectionString).GetCompanyProfileAsync();
                BuildPayslipPdf(row, profile, company, items).GeneratePdf(tempPdf);

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
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to report payroll concern: {ex.Message}", ErrorBrush);
            }
        }

        private async Task UpdatePayrollConcernAsync(object? parameter, string nextStatus)
        {
            if (!EnsureAdminOrHrAction("review payroll concerns"))
            {
                return;
            }

            var concern = parameter as PayrollConcernVm ?? SelectedPayrollConcern;
            if (concern == null)
            {
                SetMessage("Select a payroll concern first.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.UpdatePayrollConcernAsync(
                    concern.PayrollConcernId,
                    nextStatus,
                    ConcernResolutionNotes,
                    _currentUserId > 0 ? _currentUserId : null);
                ConcernResolutionNotes = string.Empty;
                await RefreshAsync();
                SetMessage($"Payroll concern #{concern.PayrollConcernId} updated to {nextStatus.Replace('_', ' ')}.", SuccessBrush);
                SystemRefreshBus.Raise("PayrollConcernUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to update payroll concern: {ex.Message}", ErrorBrush);
            }
        }

        private static QuestPDF.Infrastructure.IDocument BuildPayslipPdf(
            PayrollRunVm row,
            PayrollPayslipProfileDto? profile,
            CompanyProfile company,
            IReadOnlyList<PayrollRunItemDto> items)
        {
            var generatedAt = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
            var earnings = items
                .Where(x => string.Equals(x.ItemType, "EARNING", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var deductions = items
                .Where(x => string.Equals(x.ItemType, "DEDUCTION", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (earnings.Count == 0)
            {
                earnings = BuildFallbackEarnings(row);
            }

            if (deductions.Count == 0 && row.DeductionsTotal > 0m)
            {
                deductions = BuildFallbackDeductions(row, profile);
            }

            var employerContributions = BuildEmployerContributionLines(row, profile);
            var companyName = string.IsNullOrWhiteSpace(company.CompanyName) ? CompanyProfile.Default.CompanyName : company.CompanyName.Trim();
            var companyAddress = string.IsNullOrWhiteSpace(company.Address) ? CompanyProfile.Default.Address : company.Address.Trim();
            var employeeName = string.IsNullOrWhiteSpace(profile?.EmployeeName) ? row.EmployeeName : profile!.EmployeeName;
            var employeeNo = string.IsNullOrWhiteSpace(profile?.EmployeeNo) ? row.EmployeeNo : profile!.EmployeeNo;
            var periodText = FormatPayslipPeriod(row, profile);
            var payrollCycleText = FormatPayrollCycle(profile);
            var payDateText = profile?.PayDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "-";
            var grossIncome = row.GrossPay > 0m ? row.GrossPay : earnings.Sum(x => x.Amount);
            var deductionsTotal = row.DeductionsTotal > 0m ? row.DeductionsTotal : deductions.Sum(x => x.Amount);
            var netPay = row.NetPay != 0m ? row.NetPay : grossIncome - deductionsTotal;
            var earningLines = earnings
                .Select(item => new PayslipAmountLine(NormalizePayslipItemLabel(item), item.Amount))
                .ToList();
            var deductionLines = deductions
                .Select(item => new PayslipAmountLine(NormalizePayslipItemLabel(item), item.Amount))
                .ToList();
            var logoBytes = TryResolvePayslipLogoBytes(company.LogoPath);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.MarginHorizontal(34);
                    page.MarginVertical(28);
                    page.DefaultTextStyle(x => x.FontSize(9f).FontFamily("Segoe UI").FontColor("#17283A"));

                    page.Header().Column(column =>
                    {
                        column.Item().Height(72).Border(1).BorderColor("#C9D2DC").Row(header =>
                        {
                            var logo = header.ConstantItem(78).Background("#F5F7FA").Padding(8);
                            if (logoBytes is { Length: > 0 })
                            {
                                logo.Image(logoBytes).FitArea();
                            }
                            else
                            {
                                logo.AlignCenter().AlignMiddle().Text("HR").FontSize(20).Bold().FontColor("#164A73");
                            }

                            header.RelativeItem().Background("#143F63").PaddingHorizontal(16).PaddingVertical(10).Column(identity =>
                            {
                                identity.Item().Text(companyName.ToUpperInvariant()).FontSize(11).Bold().FontColor(QColors.White);
                                identity.Item().PaddingTop(3).Text(companyAddress).FontSize(8).FontColor("#DDEAF4");
                                identity.Item().PaddingTop(5).Text(company.SerialNumber).FontSize(7.5f).SemiBold().FontColor("#DDEAF4");
                            });

                            header.ConstantItem(172).Background("#E9EDF1").Padding(12).AlignMiddle().Column(mark =>
                            {
                                mark.Item().AlignRight().Text("OFFICIAL PAYROLL").FontSize(13).Bold().FontColor("#697886");
                                mark.Item().PaddingTop(4).AlignRight().Text("Human Resource Office").FontSize(7.5f).FontColor("#7A8793");
                            });
                        });
                    });

                    page.Content().Column(column =>
                    {
                        column.Item().PaddingTop(17).AlignCenter().Text("Payslip").FontSize(25).Bold().FontColor("#111820");

                        column.Item().PaddingTop(12).BorderTop(1).BorderBottom(1).BorderColor("#AEB8C2").PaddingVertical(8).Row(meta =>
                        {
                            meta.RelativeItem().AlignCenter().Column(section =>
                            {
                                section.Item().AlignCenter().Text("PAY RUN").FontSize(8).Bold().FontColor("#556574");
                                section.Item().AlignCenter().Text($"#{row.PayrollRunId:N0}").FontSize(11).Bold();
                            });
                            meta.RelativeItem().AlignCenter().Column(section =>
                            {
                                section.Item().AlignCenter().Text("PAY PERIOD").FontSize(8).Bold().FontColor("#556574");
                                section.Item().AlignCenter().Text(periodText).FontSize(11).Bold();
                            });
                            meta.RelativeItem().AlignCenter().Column(section =>
                            {
                                section.Item().AlignCenter().Text("PAY DATE / CYCLE").FontSize(8).Bold().FontColor("#556574");
                                section.Item().AlignCenter().Text($"{payDateText}  •  {payrollCycleText}").FontSize(10).Bold();
                            });
                        });

                        column.Item().MinHeight(550).BorderBottom(1).BorderColor("#AEB8C2").Row(body =>
                        {
                            body.ConstantItem(255).BorderRight(1).BorderColor("#AEB8C2").PaddingTop(18).PaddingRight(28).Column(employee =>
                            {
                                employee.Spacing(5);
                                employee.Item().AlignCenter().Text(employeeName).FontSize(13).Bold();
                                employee.Item().AlignCenter().Text(employeeNo).FontSize(8).FontColor("#657482");

                                employee.Item().PaddingTop(12).Column(profileSection =>
                                {
                                    profileSection.Spacing(5);
                                    AddProfileLine(profileSection, "Agency ID", employeeNo);
                                    AddProfileLine(profileSection, "Salary Grade", FormatSalaryGrade(profile));
                                    AddProfileLine(profileSection, "Date Hired", profile?.HireDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-");
                                    AddProfileLine(profileSection, "Position", profile?.PositionName ?? "-");
                                    AddProfileLine(profileSection, "Department", profile?.DepartmentName ?? "-");
                                    AddProfileLine(profileSection, "Designation", profile?.PositionName ?? "-");
                                    AddProfileLine(profileSection, "Payroll Cycle", payrollCycleText);
                                    AddProfileLine(profileSection, "Fund Source", company.SerialNumber);
                                });

                                employee.Item().PaddingTop(9).Column(government =>
                                {
                                    government.Spacing(5);
                                    AddProfileLine(government, "TIN", profile?.TinNo ?? "-");
                                    AddProfileLine(government, "GSIS / SSS", profile?.GsisNo ?? "-");
                                    AddProfileLine(government, "Pag-IBIG", profile?.PagIbigNo ?? "-");
                                    AddProfileLine(government, "PhilHealth", profile?.PhilHealthNo ?? "-");
                                });

                                employee.Item().PaddingTop(15).BorderBottom(1).BorderColor("#8795A2").PaddingBottom(4)
                                    .Text("Gross Income").FontSize(11).Bold();
                                employee.Item().AlignRight().Text(FormatMoney(grossIncome)).FontSize(16).Bold();
                                employee.Item().Text($"Monthly rate: {FormatMoney(ResolveMonthlySalaryRate(row, profile))}")
                                    .FontSize(7.5f).FontColor("#657482");

                                employee.Item().PaddingTop(18).BorderBottom(1).BorderColor("#8795A2").PaddingBottom(4)
                                    .Text("Employer Contribution").FontSize(11).Bold();
                                employee.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.ConstantColumn(82);
                                    });

                                    foreach (var contribution in employerContributions)
                                    {
                                        table.Cell().PaddingVertical(2).Text(contribution.Label);
                                        table.Cell().PaddingVertical(2).AlignRight().Text(FormatMoney(contribution.Amount)).Bold();
                                    }

                                    table.Cell().PaddingTop(5).Text("Total").Bold();
                                    table.Cell().PaddingTop(5).AlignRight().Text(FormatMoney(employerContributions.Sum(x => x.Amount))).Bold();
                                });
                            });

                            body.RelativeItem().PaddingTop(18).PaddingLeft(18).Column(itemsSection =>
                            {
                                itemsSection.Spacing(10);
                                itemsSection.Item().Text("ITEMS").FontSize(11).Bold();
                                itemsSection.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.ConstantColumn(72);
                                        columns.ConstantColumn(72);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().BorderBottom(1).BorderColor("#8795A2").PaddingBottom(5).Text("Description").Bold();
                                        header.Cell().BorderBottom(1).BorderColor("#8795A2").PaddingBottom(5).AlignRight().Text("Earnings").Bold();
                                        header.Cell().BorderBottom(1).BorderColor("#8795A2").PaddingBottom(5).AlignRight().Text("Deductions").Bold();
                                    });

                                    foreach (var earning in earningLines)
                                    {
                                        table.Cell().PaddingVertical(3).Text(earning.Label);
                                        table.Cell().PaddingVertical(3).AlignRight().Text(FormatMoney(earning.Amount)).SemiBold();
                                        table.Cell().PaddingVertical(3).Text(string.Empty);
                                    }

                                    foreach (var deduction in deductionLines)
                                    {
                                        table.Cell().PaddingVertical(3).Text(deduction.Label);
                                        table.Cell().PaddingVertical(3).Text(string.Empty);
                                        table.Cell().PaddingVertical(3).AlignRight().Text(FormatDeduction(deduction.Amount)).SemiBold();
                                    }

                                    table.Cell().BorderTop(1).BorderColor("#8795A2").PaddingTop(7).Text("TOTAL").Bold();
                                    table.Cell().BorderTop(1).BorderColor("#8795A2").PaddingTop(7).AlignRight().Text(FormatMoney(grossIncome)).Bold();
                                    table.Cell().BorderTop(1).BorderColor("#8795A2").PaddingTop(7).AlignRight().Text(FormatDeduction(deductionsTotal)).Bold();
                                });

                                itemsSection.Item().PaddingTop(9).BorderTop(1).BorderBottom(1).BorderColor("#143F63").PaddingVertical(10).Row(net =>
                                {
                                    net.RelativeItem().Text("NET PAY").FontSize(14).Bold();
                                    net.ConstantItem(150).AlignRight().Text(FormatMoney(netPay)).FontSize(17).Bold().FontColor("#143F63");
                                });

                                itemsSection.Item().PaddingTop(22).Column(cert =>
                                {
                                    cert.Item().Text("Certified By:").FontSize(8).FontColor("#657482");
                                    cert.Item().PaddingTop(18).Width(240).AlignCenter().Text(company.OwnerName.ToUpperInvariant()).FontSize(11).Bold();
                                    cert.Item().Width(240).BorderTop(1).BorderColor("#8795A2").PaddingTop(3).AlignCenter().Text("HRMO").FontSize(8).FontColor("#657482");
                                });
                            });
                        });
                    });

                    page.Footer().BorderTop(1).BorderColor("#D0D7DE").PaddingTop(5).Row(footer =>
                    {
                        footer.RelativeItem().Text("This is a system-generated official payslip.").FontSize(7.5f).FontColor("#657482");
                        footer.RelativeItem().AlignRight().Text($"Generated {generatedAt}").FontSize(7.5f).FontColor("#657482");
                    });
                });
            });
        }

        private sealed record PayslipAmountLine(string Label, decimal Amount);

        private static byte[]? TryResolvePayslipLogoBytes(string? rawLogoPath)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(rawLogoPath))
            {
                var normalized = rawLogoPath.Trim()
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                candidates.Add(Path.IsPathFullyQualified(normalized)
                    ? normalized
                    : Path.Combine(AppContext.BaseDirectory, normalized));
                candidates.Add(Path.Combine(AppContext.BaseDirectory, "Images", Path.GetFileName(normalized)));
            }

            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Images", "ePRIME_logo.png"));

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        return File.ReadAllBytes(candidate);
                    }
                }
                catch
                {
                    // Continue to the packaged application resource.
                }
            }

            foreach (var uri in new[]
                     {
                         "pack://application:,,,/Images/ePRIME_logo.png",
                         "pack://application:,,,/HRMS;component/Images/ePRIME_logo.png"
                     })
            {
                try
                {
                    var resource = System.Windows.Application.GetResourceStream(new Uri(uri, UriKind.Absolute));
                    if (resource?.Stream == null)
                    {
                        continue;
                    }

                    using var stream = resource.Stream;
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
                catch
                {
                    // Use the text fallback when no configured logo can be read.
                }
            }

            return null;
        }

        private static void AddPayslipAmountTable(
            ColumnDescriptor column,
            string title,
            IReadOnlyList<PayslipAmountLine> lines,
            string totalLabel,
            decimal totalAmount,
            bool showAsDeduction)
        {
            column.Item().Border(1).BorderColor(QColors.Grey.Lighten2).Padding(10).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text(title.ToUpperInvariant()).FontSize(10.5f).Bold().FontColor(QColors.Blue.Darken3);
                section.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(100);
                    });

                    if (lines.Count == 0)
                    {
                        table.Cell().PaddingVertical(4).Text("-").FontColor(QColors.Grey.Darken1);
                        table.Cell().PaddingVertical(4).AlignRight().Text(showAsDeduction ? "(0.00)" : "0.00").FontColor(QColors.Grey.Darken1);
                    }

                    foreach (var line in lines)
                    {
                        table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingVertical(4).Text(line.Label);
                        table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingVertical(4).AlignRight()
                            .Text(showAsDeduction ? FormatDeduction(line.Amount) : FormatMoney(line.Amount));
                    }

                    table.Cell().PaddingTop(7).Text(totalLabel).Bold();
                    table.Cell().PaddingTop(7).AlignRight().Text(showAsDeduction ? FormatDeduction(totalAmount) : FormatMoney(totalAmount)).Bold();
                });
            });
        }

        private static List<PayrollRunItemDto> BuildFallbackDeductions(PayrollRunVm row, PayrollPayslipProfileDto? profile)
        {
            var items = new List<PayrollRunItemDto>();
            if (row.DeductionsTotal <= 0m)
            {
                return items;
            }

            var deductions = PhilippinePayrollDeductions.ComputeAll(
                basicMonthlySalary: row.BasicPay,
                employmentTypeName: profile?.EmploymentTypeName,
                allowances: row.Allowances,
                overtimePay: row.OvertimePay,
                otherEarnings: row.OtherEarnings);

            var retirementAmount = deductions.GsisContribution + deductions.SssContribution;
            if (retirementAmount > 0m)
            {
                var code = deductions.GsisContribution > 0m ? "GSIS" : "SSS";
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "DEDUCTION", code, $"{code} Contribution", retirementAmount));
            }

            if (deductions.PhilHealthContribution > 0m)
            {
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "DEDUCTION", "PHILHEALTH", "PhilHealth", deductions.PhilHealthContribution));
            }

            if (deductions.PagIBIGContribution > 0m)
            {
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "DEDUCTION", "PAGIBIG", "HDMF / Pag-IBIG", deductions.PagIBIGContribution));
            }

            if (deductions.TaxWithheld > 0m)
            {
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "DEDUCTION", "TAX", "Taxes", deductions.TaxWithheld));
            }

            var listedTotal = items.Sum(x => x.Amount);
            var remaining = Math.Round(row.DeductionsTotal - listedTotal, 2, MidpointRounding.AwayFromZero);
            if (remaining > 0m)
            {
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "DEDUCTION", "OTHER_DEDUCTION", "Other Deductions", remaining));
            }

            if (items.Count == 0)
            {
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "DEDUCTION", "DEDUCTIONS", "Deductions", row.DeductionsTotal));
            }

            return items;
        }

        private static List<PayslipAmountLine> BuildEmployerContributionLines(PayrollRunVm row, PayrollPayslipProfileDto? profile)
        {
            var lines = new List<PayslipAmountLine>();
            if (row.BasicPay <= 0m)
            {
                return lines;
            }

            var deductions = PhilippinePayrollDeductions.ComputeAll(
                basicMonthlySalary: row.BasicPay,
                employmentTypeName: profile?.EmploymentTypeName,
                allowances: row.Allowances,
                overtimePay: row.OvertimePay,
                otherEarnings: row.OtherEarnings);

            var retirementAmount = deductions.GsisEmployerShare + deductions.SssEmployerShare;
            if (retirementAmount > 0m)
            {
                lines.Add(new PayslipAmountLine(deductions.GsisEmployerShare > 0m ? "GSIS" : "SSS", retirementAmount));
            }

            if (deductions.PhilHealthEmployerShare > 0m)
            {
                lines.Add(new PayslipAmountLine("PhilHealth", deductions.PhilHealthEmployerShare));
            }

            if (deductions.PagIBIGEmployerShare > 0m)
            {
                lines.Add(new PayslipAmountLine("HDMF / Pag-IBIG", deductions.PagIBIGEmployerShare));
            }

            return lines;
        }

        private static void AddProfileLine(ColumnDescriptor column, string label, string? value)
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(76).Text(label);
                row.RelativeItem().AlignRight().Text(NormalizePayslipValue(value)).Bold();
            });
        }

        private static void AddSummaryLine(ColumnDescriptor column, string label, string value)
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(label);
                row.ConstantItem(120).AlignRight().Text(value).SemiBold();
            });
        }

        private static void AddDetailLine(ColumnDescriptor column, string label, string value)
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(126).Text(label).FontColor(QColors.Grey.Darken1);
                row.RelativeItem().Text(value).SemiBold();
            });
        }

        private static void AddAmountLine(ColumnDescriptor column, string label, decimal amount, bool bold = false)
        {
            column.Item().Row(row =>
            {
                var labelText = row.RelativeItem().Text(label);
                var amountText = row.ConstantItem(92).AlignRight().Text(FormatMoney(amount));
                if (bold)
                {
                    labelText.Bold();
                    amountText.Bold();
                }
            });
        }

        private static void AddItemLine(
            ColumnDescriptor column,
            string label,
            decimal amount,
            bool isDeduction,
            bool bold = false,
            decimal? secondaryAmount = null)
        {
            column.Item().Row(row =>
            {
                var labelText = row.RelativeItem().Text(label);
                var firstAmount = row.ConstantItem(95).AlignRight().Text(isDeduction ? string.Empty : FormatMoney(amount));
                var secondAmountText = isDeduction
                    ? FormatDeduction(amount)
                    : secondaryAmount.HasValue
                        ? FormatDeduction(secondaryAmount.Value)
                        : string.Empty;
                var secondAmount = row.ConstantItem(105).AlignRight().Text(secondAmountText);

                if (bold)
                {
                    labelText.Bold();
                    firstAmount.Bold();
                    secondAmount.Bold();
                }
            });
        }

        private static string NormalizePayslipItemLabel(PayrollRunItemDto item)
        {
            var code = item.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            var description = string.IsNullOrWhiteSpace(item.Description) ? item.Code : item.Description.Trim();

            return code switch
            {
                "BASIC" => "Salary",
                "ALLOW" => "Allowances",
                "OVERTIME" => "Overtime Pay",
                "OTHER" => "Other Earnings",
                "PAGIBIG" or "HDMF" => "HDMF / Pag-IBIG",
                "PHILHEALTH" => "PhilHealth",
                "DTR_MINUS" or "LATE" => "Late/Tardiness Deduction",
                "TAX" => "Taxes",
                "GSIS" => "GSIS",
                "SSS" => "SSS",
                _ => string.IsNullOrWhiteSpace(description) ? "Item" : description
            };
        }

        private static string FormatPayslipPeriod(PayrollRunVm row, PayrollPayslipProfileDto? profile)
        {
            if (profile == null)
            {
                return row.PeriodCode;
            }

            if (profile.DateFrom.Year == profile.DateTo.Year && profile.DateFrom.Month == profile.DateTo.Month)
            {
                return $"{profile.DateFrom:MMM d} - {profile.DateTo:MMM d, yyyy}";
            }

            return $"{profile.DateFrom:MMM d, yyyy} - {profile.DateTo:MMM d, yyyy}";
        }

        private static string FormatPayrollCycle(PayrollPayslipProfileDto? profile)
        {
            if (profile == null)
            {
                return "Monthly";
            }

            var sameMonth = profile.DateFrom.Year == profile.DateTo.Year &&
                            profile.DateFrom.Month == profile.DateTo.Month;
            if (!sameMonth)
            {
                return "Custom period";
            }

            var lastDay = DateTime.DaysInMonth(profile.DateTo.Year, profile.DateTo.Month);
            if (profile.DateFrom.Day == 1 && profile.DateTo.Day == 15)
            {
                return "15th cutoff";
            }

            if (profile.DateFrom.Day == 16 && profile.DateTo.Day == lastDay)
            {
                return "30th cutoff";
            }

            if (profile.DateFrom.Day == 1 && profile.DateTo.Day == lastDay)
            {
                return "Monthly";
            }

            return "Custom period";
        }

        private static decimal ResolveMonthlySalaryRate(PayrollRunVm row, PayrollPayslipProfileDto? profile)
        {
            var cycle = FormatPayrollCycle(profile);
            return cycle is "15th cutoff" or "30th cutoff"
                ? Math.Round(row.BasicPay * 2m, 2, MidpointRounding.AwayFromZero)
                : row.BasicPay;
        }

        private static string FormatSalaryGrade(PayrollPayslipProfileDto? profile)
        {
            if (profile == null || profile.SalaryGrade <= 0)
            {
                return "-";
            }

            return profile.StepNo > 0
                ? $"{profile.SalaryGrade}-{profile.StepNo}"
                : profile.SalaryGrade.ToString(CultureInfo.InvariantCulture);
        }

        private static string NormalizePayslipValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            var normalized = value.Trim();
            if (normalized == "0" ||
                normalized.StartsWith("enc:v1:", StringComparison.OrdinalIgnoreCase))
            {
                return "-";
            }

            return normalized;
        }

        private static string FormatMoney(decimal amount) =>
            amount.ToString("N2", CultureInfo.InvariantCulture);

        private static string FormatDeduction(decimal amount) =>
            amount <= 0m ? "(0.00)" : $"({amount:N2})";

        private static bool IsDtrMinusDeduction(PayrollRunItemDto item)
        {
            var code = item.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            var description = item.Description?.Trim().ToUpperInvariant() ?? string.Empty;
            return code is "DTR_MINUS" or "LATE" ||
                   description.Contains("DTR MINUS", StringComparison.Ordinal) ||
                   description.Contains("LATE", StringComparison.Ordinal) ||
                   description.Contains("TARDINESS", StringComparison.Ordinal);
        }

        private static bool IsLoanDeduction(PayrollRunItemDto item)
        {
            var code = item.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            var description = item.Description?.Trim().ToUpperInvariant() ?? string.Empty;
            return code is "LOAN" or "LOAN_DEDUCTION" ||
                   description.Contains("LOAN", StringComparison.Ordinal);
        }

        private static string FormatPayslipMinusTime(int minutes)
        {
            if (minutes <= 0)
            {
                return "-";
            }

            var hours = minutes / 60;
            var remainingMinutes = minutes % 60;
            return hours > 0
                ? $"{hours}h {remainingMinutes:00}m"
                : $"{remainingMinutes} min";
        }

        private static List<PayrollRunItemDto> BuildFallbackEarnings(PayrollRunVm row)
        {
            var items = new List<PayrollRunItemDto>();
            if (row.BasicPay > 0m)
            {
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "EARNING", "BASIC", "Basic Pay", row.BasicPay));
            }

            if (row.Allowances > 0m)
            {
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "EARNING", "ALLOW", "Allowances", row.Allowances));
            }

            if (row.OvertimePay > 0m)
            {
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "EARNING", "OVERTIME", "Overtime Pay", row.OvertimePay));
            }

            if (row.OtherEarnings > 0m)
            {
                items.Add(new PayrollRunItemDto(0, row.PayrollRunId, "EARNING", "OTHER", "Other Earnings", row.OtherEarnings));
            }

            return items;
        }

        private static bool TryResolveRunId(object? parameter, out long runId)
        {
            switch (parameter)
            {
                case PayrollRunVm runRow:
                    runId = runRow.PayrollRunId;
                    return runId > 0;
                case PayrollReleaseLogVm releaseRow:
                    runId = releaseRow.PayrollRunId;
                    return runId > 0;
                case PayrollGovernmentContributionRowVm governmentRow:
                    runId = governmentRow.PayrollRunId;
                    return runId > 0;
                case long longId:
                    runId = longId;
                    return runId > 0;
                case int intId:
                    runId = intId;
                    return runId > 0;
                case string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId):
                    runId = parsedId;
                    return runId > 0;
                default:
                    runId = 0;
                    return false;
            }
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

                ApplyRunEditorValues(0m, 0m, 0m, 0m, 0m, "GENERATED", false, string.Empty, string.Empty, 0, 0m);
                return;
            }

            var periodId = SelectedRunPeriodId.Value;
            var employeeId = (int)SelectedRunEmployeeId.Value;

            try
            {
                var defaults = await _dataService.GetRunEditorDefaultsAsync(periodId, employeeId);

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
                    defaults.Status,
                    defaults.FromExistingRun,
                    defaults.EmploymentTypeName,
                    defaults.PositionName,
                    defaults.DtrMinusMinutes,
                    defaults.AbsentDays);
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
            string? status,
            bool fromExistingRun,
            string employmentTypeName,
            string positionName,
            int dtrMinusMinutes,
            decimal absentDays)
        {
            _isApplyingRunEditorValues = true;
            try
            {
                _runEmploymentTypeName = employmentTypeName ?? string.Empty;
                _runPositionName = positionName ?? string.Empty;
                _runDtrMinusMinutes = Math.Max(0, dtrMinusMinutes);
                _runAbsentDays = Math.Max(0m, absentDays);
                RunBasicPay = basicPay;
                RunAllowances = allowances;
                RunOvertimePay = overtimePay;
                RunOtherEarnings = otherEarnings;
                RunDeductions = deductions;
                RunStatus = NormalizeEditableRunStatus(status);
                if (!fromExistingRun)
                {
                    RecalculateRunEditorDeductions();
                }
                else
                {
                    _runDeductionPreview = PhilippinePayrollDeductions.ComputeAll(
                        basicMonthlySalary: RunBasicPay,
                        employmentTypeName: _runEmploymentTypeName,
                        allowances: RunAllowances,
                        overtimePay: RunOvertimePay,
                        otherEarnings: RunOtherEarnings,
                        absentDays: _runAbsentDays,
                        lateMinutes: _runDtrMinusMinutes);
                    RunDeductions = deductions;
                }
            }
            finally
            {
                _isApplyingRunEditorValues = false;
            }

            OnPropertyChanged(nameof(RunEmployeeProfileText));
            OnPropertyChanged(nameof(RunDeductionBreakdownText));
        }

        private void RecalculateRunEditorDeductions()
        {
            if (string.IsNullOrWhiteSpace(_runEmploymentTypeName))
            {
                _runDeductionPreview = new PayrollDeductionResult();
                RunDeductions = 0m;
                OnPropertyChanged(nameof(RunDeductionBreakdownText));
                return;
            }

            _runDeductionPreview = PhilippinePayrollDeductions.ComputeAll(
                basicMonthlySalary: RunBasicPay,
                employmentTypeName: _runEmploymentTypeName,
                allowances: RunAllowances,
                overtimePay: RunOvertimePay,
                otherEarnings: RunOtherEarnings,
                absentDays: _runAbsentDays,
                lateMinutes: _runDtrMinusMinutes);
            RunDeductions = _runDeductionPreview.TotalDeductions;
            OnPropertyChanged(nameof(RunDeductionBreakdownText));
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
                var row = new PayrollRunVm(
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
                    run.ReleaseCount,
                    run.DtrMinusMinutes);
                row.PropertyChanged += PayrollRunRow_OnPropertyChanged;
                _allRuns.Add(row);
            }

            ApplyRunFilters();
        }

        private void PayrollRunRow_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PayrollRunVm.IsSelectedForBulk))
            {
                OnPropertyChanged(nameof(SelectedPayrollRunCount));
                OnPropertyChanged(nameof(SelectedPayrollRunCountText));
            }
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
            var selectedFilter = SelectedRunPeriodFilterId.GetValueOrDefault(AllPeriodsOptionId);
            var selectedRunPeriod = SelectedRunPeriodId;
            var selectedEmployee = SelectedRunEmployeeId;
            var selectedReleaseRun = SelectedReleaseRunId;
            var selectedGovernmentPeriod = SelectedGovernmentPeriodId.GetValueOrDefault(AllPeriodsOptionId);
            var selectedGovernmentEmployee = SelectedGovernmentEmployeeId.GetValueOrDefault();

            PeriodOptions.Clear();
            PeriodOptions.Add(new PayrollLookupOptionVm(AllPeriodsOptionId, "All periods"));
            foreach (var period in periods)
            {
                PeriodOptions.Add(new PayrollLookupOptionVm(period.PayrollPeriodId, period.PeriodCode));
            }

            if (!PeriodOptions.Any(x => x.Id == selectedFilter))
            {
                SelectedRunPeriodFilterId = AllPeriodsOptionId;
            }

            PayrollPeriodOptions.Clear();
            foreach (var period in periods)
            {
                PayrollPeriodOptions.Add(new PayrollLookupOptionVm(period.PayrollPeriodId, period.PeriodCode));
            }

            if (!PayrollPeriodOptions.Any(x => x.Id == selectedRunPeriod))
            {
                SelectedRunPeriodId = PayrollPeriodOptions.Count > 0 ? PayrollPeriodOptions[0].Id : null;
            }

            if (!PeriodOptions.Any(x => x.Id == selectedGovernmentPeriod))
            {
                SelectedGovernmentPeriodId = AllPeriodsOptionId;
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

            DeductionEmployeeOptions.Clear();
            if (IsAdminOrHrMode)
            {
                DeductionEmployeeOptions.Add(new PayrollLookupOptionVm(0, "All employees"));
            }

            foreach (var employee in employees)
            {
                DeductionEmployeeOptions.Add(new PayrollLookupOptionVm(employee.EmployeeId, $"{employee.EmployeeNo} - {employee.EmployeeName}"));
            }
            OnPropertyChanged(nameof(GovernmentReportEmployeeLabel));

            var requiredEmployeeId = IsEmployeeMode
                ? _currentEmployeeId.GetValueOrDefault()
                : selectedGovernmentEmployee;
            if (!DeductionEmployeeOptions.Any(x => x.Id == requiredEmployeeId))
            {
                requiredEmployeeId = IsAdminOrHrMode
                    ? 0
                    : DeductionEmployeeOptions.FirstOrDefault()?.Id ?? 0;
            }

            SelectedGovernmentEmployeeId = requiredEmployeeId;

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

        private void RebuildGovernmentContributionSources(IReadOnlyList<PayrollGovernmentContributionSourceDto> sources)
        {
            _allGovernmentContributionSources.Clear();
            _allGovernmentContributionSources.AddRange(sources);
            EnsureGovernmentContributionSelection();
            ApplyGovernmentContributionRows();
        }

        private void ApplyGovernmentContributionRows()
        {
            GovernmentContributionRows.Clear();

            IEnumerable<PayrollGovernmentContributionSourceDto> query = _allGovernmentContributionSources;
            var selectedGovernmentPeriodId = SelectedGovernmentPeriodId.GetValueOrDefault(AllPeriodsOptionId);
            if (selectedGovernmentPeriodId > 0)
            {
                query = query.Where(x => x.PayrollPeriodId == selectedGovernmentPeriodId);
            }

            var selectedGovernmentEmployeeId = SelectedGovernmentEmployeeId.GetValueOrDefault();
            if (selectedGovernmentEmployeeId > 0)
            {
                query = query.Where(x => x.EmployeeId == selectedGovernmentEmployeeId);
            }

            decimal employeeShareTotal = 0m;
            decimal employerShareTotal = 0m;
            decimal remittanceTotal = 0m;

            foreach (var source in query
                .OrderByDescending(x => x.PayDate)
                .ThenBy(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase))
            {
                var deductions = PhilippinePayrollDeductions.ComputeAll(
                    source.BasicPay,
                    source.EmploymentTypeName,
                    absentDays: source.BasicPay <= 0m ? 0m : source.AbsenceDeduction * 22m / source.BasicPay,
                    lateMinutes: source.DtrMinusMinutes,
                    loanDeduction: source.LoanDeduction,
                    otherDeductions: source.OtherDeductions);

                foreach (var row in BuildDeductionRows(source, deductions, SelectedGovernmentContributionType))
                {
                    GovernmentContributionRows.Add(row);
                    employeeShareTotal += row.EmployeeShare;
                    employerShareTotal += row.EmployerShare;
                    remittanceTotal += row.TotalRemittance;
                }
            }

            GovernmentReportEmployeeShareTotal = employeeShareTotal;
            GovernmentReportEmployerShareTotal = employerShareTotal;
            GovernmentReportRemittanceTotal = remittanceTotal;
            OnPropertyChanged(nameof(HasGovernmentContributionRows));
        }

        private static IEnumerable<PayrollGovernmentContributionRowVm> BuildDeductionRows(
            PayrollGovernmentContributionSourceDto source,
            PayrollDeductionResult deductions,
            string contributionType)
        {
            var selectedType = string.IsNullOrWhiteSpace(contributionType)
                ? "ALL"
                : contributionType.Trim().ToUpperInvariant();

            if (!string.Equals(selectedType, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var (employeeShare, employerShare) = GetGovernmentContributionAmounts(deductions, selectedType);
                var totalRemittance = employeeShare + employerShare;
                if (totalRemittance <= 0m)
                {
                    yield break;
                }

                yield return CreateDeductionRow(
                    source,
                    GetGovernmentContributionLabel(selectedType),
                    0,
                    employeeShare,
                    employerShare,
                    totalRemittance);
                yield break;
            }

            var retirementType = deductions.GsisContribution > 0m ? "GSIS" : "SSS";
            var retirementEmployeeShare = deductions.GsisContribution + deductions.SssContribution;
            var retirementEmployerShare = deductions.GsisEmployerShare + deductions.SssEmployerShare;
            foreach (var row in CreateDeductionRowIfPositive(source, retirementType, retirementEmployeeShare, retirementEmployerShare))
            {
                yield return row;
            }

            foreach (var row in CreateDeductionRowIfPositive(source, "PhilHealth", deductions.PhilHealthContribution, deductions.PhilHealthEmployerShare))
            {
                yield return row;
            }

            foreach (var row in CreateDeductionRowIfPositive(source, "Pag-IBIG", deductions.PagIBIGContribution, deductions.PagIBIGEmployerShare))
            {
                yield return row;
            }

            foreach (var row in CreateDeductionRowIfPositive(source, "Withholding Tax", deductions.TaxWithheld, 0m))
            {
                yield return row;
            }

            foreach (var row in CreateDeductionRowIfPositive(source, "Absence Deduction", deductions.AbsenceDeduction, 0m))
            {
                yield return row;
            }

            foreach (var row in CreateDeductionRowIfPositive(source, "DTR Minus", deductions.LateDeduction, 0m, source.DtrMinusMinutes))
            {
                yield return row;
            }

            foreach (var row in CreateDeductionRowIfPositive(source, "Loan Deduction", deductions.LoanDeduction, 0m))
            {
                yield return row;
            }

            foreach (var row in CreateDeductionRowIfPositive(source, "Other Deduction", deductions.OtherDeductions, 0m))
            {
                yield return row;
            }
        }

        private static IEnumerable<PayrollGovernmentContributionRowVm> CreateDeductionRowIfPositive(
            PayrollGovernmentContributionSourceDto source,
            string deductionType,
            decimal employeeShare,
            decimal employerShare,
            int dtrMinusMinutes = 0)
        {
            var totalRemittance = employeeShare + employerShare;
            if (totalRemittance <= 0m)
            {
                yield break;
            }

            yield return CreateDeductionRow(source, deductionType, dtrMinusMinutes, employeeShare, employerShare, totalRemittance);
        }

        private static PayrollGovernmentContributionRowVm CreateDeductionRow(
            PayrollGovernmentContributionSourceDto source,
            string deductionType,
            int dtrMinusMinutes,
            decimal employeeShare,
            decimal employerShare,
            decimal totalRemittance)
        {
            return new PayrollGovernmentContributionRowVm(
                source.PayrollRunId,
                source.PayrollPeriodId,
                source.PeriodCode,
                source.PayDate,
                source.EmployeeNo,
                source.EmployeeName,
                source.EmploymentTypeName,
                source.BasicPay,
                deductionType,
                dtrMinusMinutes,
                employeeShare,
                employerShare,
                totalRemittance,
                source.RunStatus);
        }

        private void EnsureGovernmentContributionSelection()
        {
            if (HasGovernmentContributionRowsForType(SelectedGovernmentContributionType))
            {
                return;
            }

            var preferredType = GovernmentContributionTypeOptions
                .FirstOrDefault(HasGovernmentContributionRowsForType);

            if (string.IsNullOrWhiteSpace(preferredType) ||
                string.Equals(preferredType, SelectedGovernmentContributionType, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedGovernmentContributionType = preferredType.Trim().ToUpperInvariant();
            OnPropertyChanged(nameof(SelectedGovernmentContributionType));
            OnPropertyChanged(nameof(GovernmentContributionTitle));
            OnPropertyChanged(nameof(IsAllDeductionsReport));
            OnPropertyChanged(nameof(GovernmentReportEmployeeShareLabel));
            OnPropertyChanged(nameof(GovernmentReportEmployerShareLabel));
            OnPropertyChanged(nameof(GovernmentReportRemittanceLabel));
        }

        private bool HasGovernmentContributionRowsForType(string contributionType)
        {
            if (string.IsNullOrWhiteSpace(contributionType))
            {
                return false;
            }

            IEnumerable<PayrollGovernmentContributionSourceDto> query = _allGovernmentContributionSources;
            var selectedGovernmentPeriodId = SelectedGovernmentPeriodId.GetValueOrDefault(AllPeriodsOptionId);
            if (selectedGovernmentPeriodId > 0)
            {
                query = query.Where(x => x.PayrollPeriodId == selectedGovernmentPeriodId);
            }

            foreach (var source in query)
            {
                var selectedType = contributionType.Trim().ToUpperInvariant();
                var deductions = PhilippinePayrollDeductions.ComputeAll(
                    source.BasicPay,
                    source.EmploymentTypeName,
                    absentDays: source.BasicPay <= 0m ? 0m : source.AbsenceDeduction * 22m / source.BasicPay,
                    lateMinutes: source.DtrMinusMinutes,
                    loanDeduction: source.LoanDeduction,
                    otherDeductions: source.OtherDeductions);

                if (string.Equals(selectedType, "ALL", StringComparison.OrdinalIgnoreCase))
                {
                    if (BuildDeductionRows(source, deductions, selectedType).Any())
                    {
                        return true;
                    }

                    continue;
                }

                var (employeeShare, employerShare) = GetGovernmentContributionAmounts(deductions, selectedType);
                if ((employeeShare + employerShare) > 0m)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task ExportGovernmentReportAsync()
        {
            if (IsEmployeeMode && (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0))
            {
                SetMessage("Your employee profile is not linked to this account.", ErrorBrush);
                return;
            }

            if (GovernmentContributionRows.Count == 0)
            {
                SetMessage("There are no deduction rows to export for the selected period.", ErrorBrush);
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export Deduction Report",
                    Filter = "CSV File (*.csv)|*.csv",
                    DefaultExt = ".csv",
                    AddExtension = true,
                    FileName = BuildGovernmentReportFileName("csv")
                };

                if (dialog.ShowDialog() != true)
                {
                    SetMessage("Deduction export cancelled.", InfoBrush);
                    return;
                }

                await File.WriteAllTextAsync(dialog.FileName, BuildGovernmentReportCsv(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                SetMessage($"Deduction CSV saved to {Path.GetFileName(dialog.FileName)}.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to export deduction report: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SaveGovernmentReportPdfAsync()
        {
            if (IsEmployeeMode && (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0))
            {
                SetMessage("Your employee profile is not linked to this account.", ErrorBrush);
                return;
            }

            if (GovernmentContributionRows.Count == 0)
            {
                SetMessage("There are no deduction rows to save for the selected period.", ErrorBrush);
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save Deduction PDF",
                    Filter = "PDF File (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    FileName = BuildGovernmentReportFileName("pdf")
                };

                if (dialog.ShowDialog() != true)
                {
                    SetMessage("Deduction PDF save cancelled.", InfoBrush);
                    return;
                }

                BuildGovernmentContributionPdf().GeneratePdf(dialog.FileName);
                SetMessage($"Deduction PDF saved to {Path.GetFileName(dialog.FileName)}.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save deduction PDF: {ex.Message}", ErrorBrush);
            }
        }

        private static (decimal EmployeeShare, decimal EmployerShare) GetGovernmentContributionAmounts(
            PayrollDeductionResult deductions,
            string contributionType)
        {
            return contributionType switch
            {
                "SSS" => (deductions.SssContribution, deductions.SssEmployerShare),
                "GSIS" => (deductions.GsisContribution, deductions.GsisEmployerShare),
                "PHILHEALTH" => (deductions.PhilHealthContribution, deductions.PhilHealthEmployerShare),
                "PAGIBIG" => (deductions.PagIBIGContribution, deductions.PagIBIGEmployerShare),
                _ => (0m, 0m)
            };
        }

        private string BuildGovernmentReportCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Payroll Run ID,Period,Pay Date,Employee No,Employee Name,Appointment Type,Basic Pay,Deduction,Minus Time,Employee Deduction,Employer Share,Total,Run Status");

            foreach (var row in GovernmentContributionRows)
            {
                builder.AppendLine(string.Join(",",
                    EscapeCsv(row.PayrollRunId.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(row.PeriodCode),
                    EscapeCsv(row.PayDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.EmployeeNo),
                    EscapeCsv(row.EmployeeName),
                    EscapeCsv(row.EmploymentTypeName),
                    EscapeCsv(row.BasicPay.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.DeductionType),
                    EscapeCsv(row.MinusTimeText),
                    EscapeCsv(row.EmployeeShare.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.EmployerShare.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.TotalRemittance.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.RunStatus)));
            }

            return builder.ToString();
        }

        private QuestPDF.Infrastructure.IDocument BuildGovernmentContributionPdf()
        {
            var reportTitle = GovernmentContributionTitle;
            var periodLabel = GovernmentReportPeriodLabel;
            var employeeLabel = GovernmentReportEmployeeLabel;
            var generatedAt = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Spacing(4);
                        column.Item().Text("HRMS PAYROLL DEDUCTION REPORT").FontSize(16).Bold().FontColor(QColors.Blue.Darken3);
                        column.Item().Text(reportTitle).FontSize(12).SemiBold().FontColor(QColors.Blue.Darken2);
                        column.Item().Text($"Payroll Period: {periodLabel}");
                        column.Item().Text($"Employee: {employeeLabel}");
                        column.Item().Text($"Generated: {generatedAt}").FontColor(QColors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(10).Column(column =>
                    {
                        column.Spacing(8);

                        column.Item().Row(summary =>
                        {
                            summary.RelativeItem().Text($"{GovernmentReportEmployeeShareLabel}: {GovernmentReportEmployeeShareTotal:N2}").SemiBold();
                            summary.RelativeItem().Text($"{GovernmentReportEmployerShareLabel}: {GovernmentReportEmployerShareTotal:N2}").SemiBold();
                            summary.RelativeItem().AlignRight().Text($"{GovernmentReportRemittanceLabel}: {GovernmentReportRemittanceTotal:N2}").SemiBold().FontColor(QColors.Green.Darken2);
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(52);
                                columns.ConstantColumn(76);
                                columns.ConstantColumn(64);
                                columns.RelativeColumn(1.7f);
                                columns.RelativeColumn(1.15f);
                                columns.RelativeColumn(1.15f);
                                columns.ConstantColumn(64);
                                columns.ConstantColumn(72);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(72);
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).Text("Run").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).Text("Period").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).Text("Emp No").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).Text("Employee").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).Text("Type").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).Text("Deduction").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).Text("Minus Time").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).AlignRight().Text("Basic").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).AlignRight().Text("Employee").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).AlignRight().Text("Employer").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingBottom(4).AlignRight().Text("Total").SemiBold();
                            });

                            foreach (var row in GovernmentContributionRows)
                            {
                                table.Cell().PaddingVertical(4).Text(row.PayrollRunId.ToString(CultureInfo.InvariantCulture));
                                table.Cell().PaddingVertical(4).Text(row.PeriodCode);
                                table.Cell().PaddingVertical(4).Text(row.EmployeeNo);
                                table.Cell().PaddingVertical(4).Text(row.EmployeeName);
                                table.Cell().PaddingVertical(4).Text(row.EmploymentTypeName);
                                table.Cell().PaddingVertical(4).Text(row.DeductionType);
                                table.Cell().PaddingVertical(4).Text(row.MinusTimeText);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{row.BasicPay:N2}");
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{row.EmployeeShare:N2}");
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{row.EmployerShare:N2}");
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{row.TotalRemittance:N2}");
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text("Generated by HRMS payroll reporting.");
                });
            });
        }

        private string BuildGovernmentReportFileName(string extension)
        {
            var contributionCode = IsAllDeductionsReport
                ? "Deductions"
                : SelectedGovernmentContributionType.Trim().ToUpperInvariant();
            var selectedGovernmentPeriodId = SelectedGovernmentPeriodId.GetValueOrDefault(AllPeriodsOptionId);
            var periodLabel = selectedGovernmentPeriodId > 0
                ? PeriodOptions.FirstOrDefault(x => x.Id == selectedGovernmentPeriodId)?.Label ?? "Period"
                : "AllPeriods";
            var selectedGovernmentEmployeeId = SelectedGovernmentEmployeeId.GetValueOrDefault();
            var employeeLabel = selectedGovernmentEmployeeId > 0
                ? GovernmentContributionRows.FirstOrDefault()?.EmployeeNo ?? "Employee"
                : "AllEmployees";
            var safePeriodLabel = periodLabel.Replace("/", "-").Replace(" ", string.Empty);
            var safeEmployeeLabel = new string(employeeLabel.Where(char.IsLetterOrDigit).ToArray());
            return $"{contributionCode}-Report-{safeEmployeeLabel}-{safePeriodLabel}-{DateTime.Now:yyyyMMdd-HHmm}.{extension}";
        }

        private static string GetGovernmentContributionLabel(string contributionType)
        {
            return contributionType switch
            {
                "ALL" => "All Deductions",
                "PHILHEALTH" => "PhilHealth",
                "PAGIBIG" => "Pag-IBIG",
                "GSIS" => "GSIS",
                _ => "SSS"
            };
        }

        private static string EscapeCsv(string? value)
        {
            var sanitized = value ?? string.Empty;
            if (sanitized.Contains('"'))
            {
                sanitized = sanitized.Replace("\"", "\"\"");
            }

            return sanitized.IndexOfAny([',', '"', '\r', '\n']) >= 0
                ? $"\"{sanitized}\""
                : sanitized;
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

            var selectedRunPeriodFilterId = SelectedRunPeriodFilterId.GetValueOrDefault(AllPeriodsOptionId);
            if (selectedRunPeriodFilterId > 0)
            {
                query = query.Where(x => x.PayrollPeriodId == selectedRunPeriodFilterId);
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

            OnPropertyChanged(nameof(VisiblePayrollRunCount));
            OnPropertyChanged(nameof(VisiblePayrollGrossText));
            OnPropertyChanged(nameof(VisiblePayrollDeductionsText));
            OnPropertyChanged(nameof(VisiblePayrollNetText));
            OnPropertyChanged(nameof(SelectedPayrollRunCount));
            OnPropertyChanged(nameof(SelectedPayrollRunCountText));

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

        private static string FormatEditableAmount(decimal amount) =>
            amount == 0m ? "0" : amount.ToString("0.##", CultureInfo.InvariantCulture);

        private static void UpdateEditableAmount(string? value, Action<decimal> apply)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                apply(0m);
                return;
            }

            if (decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out var currentCultureAmount) ||
                decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out currentCultureAmount))
            {
                apply(currentCultureAmount);
            }
        }

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
            _allGovernmentContributionSources.Clear();
            PayrollPeriods.Clear();
            PayrollRuns.Clear();
            PayslipReleases.Clear();
            GovernmentContributionRows.Clear();
            OnPropertyChanged(nameof(HasGovernmentContributionRows));

            PeriodOptions.Clear();
            PeriodOptions.Add(new PayrollLookupOptionVm(AllPeriodsOptionId, "All periods"));
            EmployeeOptions.Clear();
            DeductionEmployeeOptions.Clear();
            RunOptions.Clear();
            GovernmentReportEmployeeShareTotal = 0m;
            GovernmentReportEmployerShareTotal = 0m;
            GovernmentReportRemittanceTotal = 0m;
            SelectedGovernmentPeriodId = AllPeriodsOptionId;
            SelectedGovernmentEmployeeId = null;

            SelectedRun = null;
            SelectedRunPeriodId = AllPeriodsOptionId;
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
        public string PayrollCycleText => PeriodCode.Contains("-15-CUTOFF", StringComparison.OrdinalIgnoreCase)
            ? "15th cutoff"
            : PeriodCode.Contains("-30-CUTOFF", StringComparison.OrdinalIgnoreCase)
                ? "30th cutoff"
                : "Monthly";
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
        private static readonly Brush DraftBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B9831A"));
        private static readonly Brush GeneratedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F80ED"));
        private static readonly Brush ApprovedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4368"));
        private static readonly Brush ReleasedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush VoidBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private string _status;
        private bool _isSelectedForBulk;

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
            int releaseCount,
            int dtrMinusMinutes)
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
            DtrMinusMinutes = Math.Max(0, dtrMinusMinutes);
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
        public int DtrMinusMinutes { get; }
        public bool CanOpenPayslip => ReleaseCount > 0 || string.Equals(Status, "RELEASED", StringComparison.OrdinalIgnoreCase);
        public bool CanApprovePayroll => string.Equals(Status, "GENERATED", StringComparison.OrdinalIgnoreCase);
        public bool CanReleasePayslip => !CanOpenPayslip && NetPay > 0m && string.Equals(Status, "APPROVED", StringComparison.OrdinalIgnoreCase);
        public bool IsSelectedForBulk
        {
            get => _isSelectedForBulk;
            set
            {
                if (_isSelectedForBulk == value)
                {
                    return;
                }

                _isSelectedForBulk = value;
                OnPropertyChanged();
            }
        }

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
                OnPropertyChanged(nameof(CanApprovePayroll));
                OnPropertyChanged(nameof(CanReleasePayslip));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(ReleasedText));
            }
        }

        public string StatusText => Status switch
        {
            "DRAFT" => "Draft",
            "APPROVED" => "Approved",
            "RELEASED" => "Released",
            "VOID" => "Void",
            _ => "Generated"
        };

        public Brush StatusBrush => Status switch
        {
            "DRAFT" => DraftBrush,
            "APPROVED" => ApprovedBrush,
            "RELEASED" => ReleasedBrush,
            "VOID" => VoidBrush,
            _ => GeneratedBrush
        };

        public string GeneratedAtText => GeneratedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
        public string LastReleasedAtText => LastReleasedAt.HasValue
            ? LastReleasedAt.Value.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture)
            : "-";
        public string ReleasedText => LastReleasedAt.HasValue || string.Equals(Status, "RELEASED", StringComparison.OrdinalIgnoreCase)
            ? "Released"
            : "Not released";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class PayrollConcernVm
    {
        public PayrollConcernVm(PayrollConcernDto concern)
        {
            PayrollConcernId = concern.PayrollConcernId;
            PayrollRunId = concern.PayrollRunId;
            PeriodCode = concern.PeriodCode;
            EmployeeId = concern.EmployeeId;
            EmployeeNo = concern.EmployeeNo;
            EmployeeName = concern.EmployeeName;
            ConcernDetails = concern.ConcernDetails;
            Status = concern.Status;
            CreatedAt = concern.CreatedAt;
            ResolutionNotes = concern.ResolutionNotes;
        }

        public long PayrollConcernId { get; }
        public long PayrollRunId { get; }
        public string PeriodCode { get; }
        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public string ConcernDetails { get; }
        public string Status { get; }
        public DateTime CreatedAt { get; }
        public string ResolutionNotes { get; }
        public string CreatedAtText => CreatedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
        public string StatusText => Status switch
        {
            "IN_REVIEW" => "Reviewing",
            "RESOLVED" => "Resolved",
            "REJECTED" => "Rejected",
            _ => "Open"
        };
        public bool CanStartReview => string.Equals(Status, "OPEN", StringComparison.OrdinalIgnoreCase);
        public bool CanClose => string.Equals(Status, "IN_REVIEW", StringComparison.OrdinalIgnoreCase);
    }

    public class PayrollGovernmentContributionRowVm
    {
        public PayrollGovernmentContributionRowVm(
            long payrollRunId,
            long payrollPeriodId,
            string periodCode,
            DateTime payDate,
            string employeeNo,
            string employeeName,
            string employmentTypeName,
            decimal basicPay,
            string deductionType,
            int dtrMinusMinutes,
            decimal employeeShare,
            decimal employerShare,
            decimal totalRemittance,
            string runStatus)
        {
            PayrollRunId = payrollRunId;
            PayrollPeriodId = payrollPeriodId;
            PeriodCode = periodCode;
            PayDate = payDate;
            EmployeeNo = employeeNo;
            EmployeeName = employeeName;
            EmploymentTypeName = employmentTypeName;
            BasicPay = basicPay;
            DeductionType = string.IsNullOrWhiteSpace(deductionType) ? "Deduction" : deductionType.Trim();
            DtrMinusMinutes = Math.Max(0, dtrMinusMinutes);
            EmployeeShare = employeeShare;
            EmployerShare = employerShare;
            TotalRemittance = totalRemittance;
            RunStatus = string.IsNullOrWhiteSpace(runStatus) ? "GENERATED" : runStatus.Trim().ToUpperInvariant();
        }

        public long PayrollRunId { get; }
        public long PayrollPeriodId { get; }
        public string PeriodCode { get; }
        public DateTime PayDate { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public string EmploymentTypeName { get; }
        public decimal BasicPay { get; }
        public string DeductionType { get; }
        public int DtrMinusMinutes { get; }
        public string MinusTimeText => DtrMinusMinutes > 0 ? $"-{FormatMinusTime(DtrMinusMinutes)}" : "-";
        public decimal EmployeeShare { get; }
        public decimal EmployerShare { get; }
        public decimal TotalRemittance { get; }
        public string RunStatus { get; }
        public string PayDateText => PayDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

        private static string FormatMinusTime(int minutes)
        {
            if (minutes <= 0)
            {
                return "-";
            }

            var hours = minutes / 60;
            var remainingMinutes = minutes % 60;
            return hours > 0
                ? $"{hours}h {remainingMinutes:00}m"
                : $"{remainingMinutes} min";
        }
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
