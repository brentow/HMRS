using HRMS.Model;
using Microsoft.Win32;
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
    public class LeaveViewModel : INotifyPropertyChanged
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5B6C"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private readonly LeaveDataService _dataService = new(DbConfig.ConnectionString);
        private readonly List<LeaveRequestVm> _allRequests = new();
        private readonly List<LeaveBalanceVm> _allBalances = new();
        private readonly List<LeaveAttachmentVm> _allAttachments = new();

        private int _currentUserId;
        private string _currentUsername = "-";
        private bool _isEmployeeMode;
        private int? _currentEmployeeId;

        private int _totalRequests;
        private int _pendingRequests;
        private int _approvedRequests;
        private int _rejectedRequests;
        private int _leaveTypes;
        private int _employeesWithBalances;
        private int _attachmentsCount;

        private string _requestSearchText = string.Empty;
        private string _selectedRequestStatusFilter = "All";
        private LeaveRequestVm? _selectedRequest;
        private string _decisionRemarks = string.Empty;

        private int? _selectedFileEmployeeId;
        private int? _selectedFileLeaveTypeId;
        private DateTime _fileDateFrom = DateTime.Today;
        private DateTime _fileDateTo = DateTime.Today;
        private decimal _fileDaysRequested = 1m;
        private string _fileReason = string.Empty;
        private string _fileBalancePreviewText = "Select employee and leave type.";
        private string _fileValidationText = "No blocking issue detected.";
        private Brush _fileValidationBrush = SuccessBrush;
        private bool _hasBlockingFileConflict;
        private string _selectedRequestTimeline = "No request selected.";

        private string _balanceSearchText = string.Empty;
        private int _selectedBalanceYearFilter;
        private int? _selectedBalanceEmployeeId;
        private int? _selectedBalanceLeaveTypeId;
        private int _balanceYear = DateTime.Today.Year;
        private decimal _balanceOpeningCredits;
        private decimal _balanceEarned;
        private decimal _balanceUsed;
        private decimal _balanceAdjustments;
        private DateTime _balanceAsOfDate = DateTime.Today;

        private string _attachmentSearchText = string.Empty;
        private long? _selectedAttachmentRequestId;
        private string _selectedAttachmentFilePath = string.Empty;
        private LeaveAttachmentVm? _selectedAttachment;

        private string _actionMessage = "Ready.";
        private Brush _actionMessageBrush = InfoBrush;
        private bool _isBusy;
        private bool _refreshQueued;

        public int TotalRequests { get => _totalRequests; private set { _totalRequests = value; OnPropertyChanged(); } }
        public int PendingRequests { get => _pendingRequests; private set { _pendingRequests = value; OnPropertyChanged(); } }
        public int ApprovedRequests { get => _approvedRequests; private set { _approvedRequests = value; OnPropertyChanged(); } }
        public int RejectedRequests { get => _rejectedRequests; private set { _rejectedRequests = value; OnPropertyChanged(); } }
        public int LeaveTypes { get => _leaveTypes; private set { _leaveTypes = value; OnPropertyChanged(); } }
        public int EmployeesWithBalances { get => _employeesWithBalances; private set { _employeesWithBalances = value; OnPropertyChanged(); } }
        public int AttachmentsCount { get => _attachmentsCount; private set { _attachmentsCount = value; OnPropertyChanged(); } }

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
                OnPropertyChanged(nameof(CanCancelSelectedRequest));
            }
        }

        public bool IsAdminOrHrMode => !IsEmployeeMode;

        public string RequestSearchText
        {
            get => _requestSearchText;
            set
            {
                if (_requestSearchText == value)
                {
                    return;
                }

                _requestSearchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyRequestFilters();
            }
        }

        public string SelectedRequestStatusFilter
        {
            get => _selectedRequestStatusFilter;
            set
            {
                if (_selectedRequestStatusFilter == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _selectedRequestStatusFilter = value;
                OnPropertyChanged();
                ApplyRequestFilters();
            }
        }

        public LeaveRequestVm? SelectedRequest
        {
            get => _selectedRequest;
            set
            {
                if (_selectedRequest == value)
                {
                    return;
                }

                _selectedRequest = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCancelSelectedRequest));
                if (_selectedRequest != null)
                {
                    DecisionRemarks = _selectedRequest.DecisionRemarksRaw;
                    SelectedAttachmentRequestId = _selectedRequest.LeaveApplicationId;
                }

                UpdateSelectedRequestContext();
            }
        }

        public string DecisionRemarks
        {
            get => _decisionRemarks;
            set
            {
                if (_decisionRemarks == value)
                {
                    return;
                }

                _decisionRemarks = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public int? SelectedFileEmployeeId
        {
            get => _selectedFileEmployeeId;
            set
            {
                if (_selectedFileEmployeeId == value)
                {
                    return;
                }

                _selectedFileEmployeeId = value;
                OnPropertyChanged();
                UpdateFileLeavePreview();
            }
        }

        public int? SelectedFileLeaveTypeId
        {
            get => _selectedFileLeaveTypeId;
            set
            {
                if (_selectedFileLeaveTypeId == value)
                {
                    return;
                }

                _selectedFileLeaveTypeId = value;
                OnPropertyChanged();
                UpdateFileLeavePreview();
            }
        }

        public DateTime FileDateFrom
        {
            get => _fileDateFrom;
            set
            {
                if (_fileDateFrom == value)
                {
                    return;
                }

                _fileDateFrom = value;
                OnPropertyChanged();
                RecalculateRequestedDays();
            }
        }

        public DateTime FileDateTo
        {
            get => _fileDateTo;
            set
            {
                if (_fileDateTo == value)
                {
                    return;
                }

                _fileDateTo = value;
                OnPropertyChanged();
                RecalculateRequestedDays();
            }
        }

        public decimal FileDaysRequested
        {
            get => _fileDaysRequested;
            set
            {
                if (_fileDaysRequested == value)
                {
                    return;
                }

                _fileDaysRequested = value;
                OnPropertyChanged();
                UpdateFileLeavePreview();
            }
        }

        public string FileReason
        {
            get => _fileReason;
            set { if (_fileReason != value) { _fileReason = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string FileBalancePreviewText
        {
            get => _fileBalancePreviewText;
            private set
            {
                if (_fileBalancePreviewText == value)
                {
                    return;
                }

                _fileBalancePreviewText = value;
                OnPropertyChanged();
            }
        }

        public string FileValidationText
        {
            get => _fileValidationText;
            private set
            {
                if (_fileValidationText == value)
                {
                    return;
                }

                _fileValidationText = value;
                OnPropertyChanged();
            }
        }

        public Brush FileValidationBrush
        {
            get => _fileValidationBrush;
            private set
            {
                if (_fileValidationBrush == value)
                {
                    return;
                }

                _fileValidationBrush = value;
                OnPropertyChanged();
            }
        }

        public bool CanFileLeaveRequest => !_hasBlockingFileConflict;

        public string SelectedRequestTimeline
        {
            get => _selectedRequestTimeline;
            private set
            {
                if (_selectedRequestTimeline == value)
                {
                    return;
                }

                _selectedRequestTimeline = value;
                OnPropertyChanged();
            }
        }

        public bool CanCancelSelectedRequest =>
            IsEmployeeMode &&
            SelectedRequest != null &&
            SelectedRequest.EmployeeId > 0 &&
            _currentEmployeeId.HasValue &&
            SelectedRequest.EmployeeId == _currentEmployeeId.Value &&
            (string.Equals(SelectedRequest.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(SelectedRequest.Status, "RECOMMENDED", StringComparison.OrdinalIgnoreCase));

        public string BalanceSearchText
        {
            get => _balanceSearchText;
            set
            {
                if (_balanceSearchText == value)
                {
                    return;
                }

                _balanceSearchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyBalanceFilters();
            }
        }

        public int SelectedBalanceYearFilter
        {
            get => _selectedBalanceYearFilter;
            set
            {
                if (_selectedBalanceYearFilter == value)
                {
                    return;
                }

                _selectedBalanceYearFilter = value;
                OnPropertyChanged();
                ApplyBalanceFilters();
            }
        }

        public int? SelectedBalanceEmployeeId
        {
            get => _selectedBalanceEmployeeId;
            set
            {
                if (_selectedBalanceEmployeeId == value)
                {
                    return;
                }

                _selectedBalanceEmployeeId = value;
                OnPropertyChanged();
                ApplyBalanceEditorFromSelection();
            }
        }

        public int? SelectedBalanceLeaveTypeId
        {
            get => _selectedBalanceLeaveTypeId;
            set
            {
                if (_selectedBalanceLeaveTypeId == value)
                {
                    return;
                }

                _selectedBalanceLeaveTypeId = value;
                OnPropertyChanged();
                ApplyBalanceEditorFromSelection();
            }
        }

        public int BalanceYear
        {
            get => _balanceYear;
            set
            {
                if (_balanceYear == value)
                {
                    return;
                }

                _balanceYear = value;
                OnPropertyChanged();
                ApplyBalanceEditorFromSelection();
            }
        }

        public decimal BalanceOpeningCredits
        {
            get => _balanceOpeningCredits;
            set { if (_balanceOpeningCredits != value) { _balanceOpeningCredits = value; OnPropertyChanged(); } }
        }

        public decimal BalanceEarned
        {
            get => _balanceEarned;
            set { if (_balanceEarned != value) { _balanceEarned = value; OnPropertyChanged(); } }
        }

        public decimal BalanceUsed
        {
            get => _balanceUsed;
            set { if (_balanceUsed != value) { _balanceUsed = value; OnPropertyChanged(); } }
        }

        public decimal BalanceAdjustments
        {
            get => _balanceAdjustments;
            set { if (_balanceAdjustments != value) { _balanceAdjustments = value; OnPropertyChanged(); } }
        }

        public DateTime BalanceAsOfDate
        {
            get => _balanceAsOfDate;
            set { if (_balanceAsOfDate != value) { _balanceAsOfDate = value; OnPropertyChanged(); } }
        }

        public string AttachmentSearchText
        {
            get => _attachmentSearchText;
            set
            {
                if (_attachmentSearchText == value)
                {
                    return;
                }

                _attachmentSearchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyAttachmentFilters();
            }
        }

        public long? SelectedAttachmentRequestId
        {
            get => _selectedAttachmentRequestId;
            set { if (_selectedAttachmentRequestId != value) { _selectedAttachmentRequestId = value; OnPropertyChanged(); } }
        }

        public string SelectedAttachmentFilePath
        {
            get => _selectedAttachmentFilePath;
            set { if (_selectedAttachmentFilePath != value) { _selectedAttachmentFilePath = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public LeaveAttachmentVm? SelectedAttachment
        {
            get => _selectedAttachment;
            set
            {
                if (_selectedAttachment == value)
                {
                    return;
                }

                _selectedAttachment = value;
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

        public ObservableCollection<string> RequestStatusFilters { get; } = new()
        {
            "All",
            "SUBMITTED",
            "RECOMMENDED",
            "APPROVED",
            "REJECTED",
            "CANCELLED"
        };

        public ObservableCollection<LeaveRequestVm> LeaveRequests { get; } = new();
        public ObservableCollection<LeaveBalanceVm> LeaveBalances { get; } = new();
        public ObservableCollection<LeaveAttachmentVm> LeaveAttachments { get; } = new();

        public ObservableCollection<LeaveEmployeeOptionVm> EmployeeOptions { get; } = new();
        public ObservableCollection<LeaveTypeOptionVm> LeaveTypeOptions { get; } = new();
        public ObservableCollection<LeaveRequestOptionVm> LeaveRequestOptions { get; } = new();
        public ObservableCollection<int> BalanceYearOptions { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand FileLeaveCommand { get; }
        public ICommand ApproveRequestCommand { get; }
        public ICommand RejectRequestCommand { get; }
        public ICommand RecommendRequestCommand { get; }
        public ICommand CancelMyRequestCommand { get; }
        public ICommand SaveBalanceCommand { get; }
        public ICommand BrowseAttachmentCommand { get; }
        public ICommand UploadAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }

        public LeaveViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
            FileLeaveCommand = new AsyncRelayCommand(_ => FileLeaveAsync());
            ApproveRequestCommand = new AsyncRelayCommand(ApproveRequestAsync);
            RejectRequestCommand = new AsyncRelayCommand(RejectRequestAsync);
            RecommendRequestCommand = new AsyncRelayCommand(RecommendRequestAsync);
            CancelMyRequestCommand = new AsyncRelayCommand(CancelMyRequestAsync);
            SaveBalanceCommand = new AsyncRelayCommand(_ => SaveBalanceAsync());
            BrowseAttachmentCommand = new AsyncRelayCommand(_ => BrowseAttachmentAsync());
            UploadAttachmentCommand = new AsyncRelayCommand(_ => UploadAttachmentAsync());
            DeleteAttachmentCommand = new AsyncRelayCommand(DeleteAttachmentAsync);

            _selectedBalanceYearFilter = 0;
            QueueRefresh();
        }

        public void SetCurrentUser(int userId, string username, string? roleName)
        {
            _currentUserId = userId;
            _currentUsername = string.IsNullOrWhiteSpace(username) ? "-" : username.Trim();
            IsEmployeeMode = string.Equals(roleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);
            _currentEmployeeId = null;
            QueueRefresh();
        }

        public async Task RefreshAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetMessage("Loading leave module...", InfoBrush);

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
                var leaveTypesTask = _dataService.GetLeaveTypesAsync(activeOnly: true);
                var employeesTask = _dataService.GetEmployeesAsync(scopedEmployeeId);
                var requestsTask = _dataService.GetLeaveRequestsAsync(limit: 400, employeeId: scopedEmployeeId);
                var balancesTask = _dataService.GetLeaveBalancesAsync(employeeId: scopedEmployeeId);
                var attachmentsTask = _dataService.GetLeaveAttachmentsAsync(limit: 400, employeeId: scopedEmployeeId);

                var stats = await statsTask;
                var leaveTypes = await leaveTypesTask;
                var employees = await employeesTask;
                var requests = await requestsTask;
                var balances = await balancesTask;
                var attachments = await attachmentsTask;

                TotalRequests = stats.TotalRequests;
                PendingRequests = stats.PendingRequests;
                ApprovedRequests = stats.ApprovedRequests;
                RejectedRequests = stats.RejectedRequests;
                LeaveTypes = stats.LeaveTypes;
                EmployeesWithBalances = stats.EmployeesWithBalances;
                AttachmentsCount = stats.AttachmentsCount;

                RebuildOptions(employees, leaveTypes, requests);
                RebuildRequests(requests);
                RebuildBalances(balances);
                RebuildAttachments(attachments);

                if (IsEmployeeMode && _currentEmployeeId.HasValue && _currentEmployeeId.Value > 0)
                {
                    SelectedFileEmployeeId = _currentEmployeeId.Value;
                    SelectedBalanceEmployeeId = _currentEmployeeId.Value;
                }
                else
                {
                    if (!SelectedFileEmployeeId.HasValue || !EmployeeOptions.Any(x => x.EmployeeId == SelectedFileEmployeeId.Value))
                    {
                        SelectedFileEmployeeId = EmployeeOptions.Count > 0 ? EmployeeOptions[0].EmployeeId : null;
                    }

                    if (!SelectedBalanceEmployeeId.HasValue || !EmployeeOptions.Any(x => x.EmployeeId == SelectedBalanceEmployeeId.Value))
                    {
                        SelectedBalanceEmployeeId = EmployeeOptions.Count > 0 ? EmployeeOptions[0].EmployeeId : null;
                    }
                }

                if (!SelectedFileLeaveTypeId.HasValue || !LeaveTypeOptions.Any(x => x.LeaveTypeId == SelectedFileLeaveTypeId.Value))
                {
                    SelectedFileLeaveTypeId = LeaveTypeOptions.Count > 0 ? LeaveTypeOptions[0].LeaveTypeId : null;
                }

                if (!SelectedBalanceLeaveTypeId.HasValue || !LeaveTypeOptions.Any(x => x.LeaveTypeId == SelectedBalanceLeaveTypeId.Value))
                {
                    SelectedBalanceLeaveTypeId = LeaveTypeOptions.Count > 0 ? LeaveTypeOptions[0].LeaveTypeId : null;
                }

                if (!SelectedAttachmentRequestId.HasValue || !LeaveRequestOptions.Any(x => x.LeaveApplicationId == SelectedAttachmentRequestId.Value))
                {
                    SelectedAttachmentRequestId = LeaveRequestOptions.Count > 0 ? LeaveRequestOptions[0].LeaveApplicationId : null;
                }

                ApplyBalanceEditorFromSelection();
                UpdateFileLeavePreview();
                UpdateSelectedRequestContext();
                SetMessage("Leave module refreshed.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to refresh leave module: {ex.Message}", ErrorBrush);
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

        private void QueueRefresh()
        {
            if (IsBusy)
            {
                _refreshQueued = true;
                return;
            }

            _ = RefreshAsync();
        }

        private void ClearForUnlinkedEmployee()
        {
            TotalRequests = 0;
            PendingRequests = 0;
            ApprovedRequests = 0;
            RejectedRequests = 0;
            LeaveTypes = 0;
            EmployeesWithBalances = 0;
            AttachmentsCount = 0;

            _allRequests.Clear();
            _allBalances.Clear();
            _allAttachments.Clear();
            LeaveRequests.Clear();
            LeaveBalances.Clear();
            LeaveAttachments.Clear();

            EmployeeOptions.Clear();
            LeaveTypeOptions.Clear();
            LeaveRequestOptions.Clear();
            BalanceYearOptions.Clear();
            BalanceYearOptions.Add(0);

            SelectedFileEmployeeId = null;
            SelectedFileLeaveTypeId = null;
            SelectedBalanceEmployeeId = null;
            SelectedBalanceLeaveTypeId = null;
            SelectedAttachmentRequestId = null;
            SelectedAttachment = null;
            SelectedRequest = null;
            FileBalancePreviewText = "Select employee and leave type.";
            FileValidationText = "No blocking issue detected.";
            FileValidationBrush = SuccessBrush;
            _hasBlockingFileConflict = false;
            OnPropertyChanged(nameof(CanFileLeaveRequest));
            SelectedRequestTimeline = "No request selected.";
        }

        private async Task FileLeaveAsync()
        {
            if (IsEmployeeMode)
            {
                if (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0)
                {
                    SetMessage("Unable to resolve your employee profile.", ErrorBrush);
                    return;
                }

                SelectedFileEmployeeId = _currentEmployeeId.Value;
            }

            if (!SelectedFileEmployeeId.HasValue || SelectedFileEmployeeId.Value <= 0)
            {
                SetMessage("Select employee for leave request.", ErrorBrush);
                return;
            }

            if (!SelectedFileLeaveTypeId.HasValue || SelectedFileLeaveTypeId.Value <= 0)
            {
                SetMessage("Select leave type.", ErrorBrush);
                return;
            }

            if (!CanFileLeaveRequest)
            {
                SetMessage(string.IsNullOrWhiteSpace(FileValidationText)
                    ? "Resolve leave conflicts before filing."
                    : FileValidationText, ErrorBrush);
                return;
            }

            try
            {
                var leaveApplicationId = await _dataService.AddLeaveRequestAsync(
                    SelectedFileEmployeeId.Value,
                    SelectedFileLeaveTypeId.Value,
                    FileDateFrom,
                    FileDateTo,
                    FileDaysRequested,
                    FileReason);

                FileReason = string.Empty;
                RecalculateRequestedDays();
                await RefreshAsync();
                SelectedRequest = LeaveRequests.FirstOrDefault(x => x.LeaveApplicationId == leaveApplicationId);
                SetMessage($"Leave request #{leaveApplicationId} filed successfully.", SuccessBrush);
                SystemRefreshBus.Raise("LeaveRequestFiled");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to file leave request: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ApproveRequestAsync(object? parameter)
        {
            await UpdateRequestStatusAsync(parameter, "APPROVED", requireReason: false);
        }

        private async Task RejectRequestAsync(object? parameter)
        {
            await UpdateRequestStatusAsync(parameter, "REJECTED", requireReason: true);
        }

        private async Task RecommendRequestAsync(object? parameter)
        {
            await UpdateRequestStatusAsync(parameter, "RECOMMENDED", requireReason: false);
        }

        private async Task CancelMyRequestAsync(object? parameter)
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

            var request = parameter as LeaveRequestVm ?? SelectedRequest;
            if (request == null)
            {
                SetMessage("Select a leave request first.", ErrorBrush);
                return;
            }

            if (request.EmployeeId != _currentEmployeeId.Value)
            {
                SetMessage("You can only cancel your own leave request.", ErrorBrush);
                return;
            }

            if (!request.CanCancelPending)
            {
                SetMessage("Only submitted or recommended requests can be cancelled.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.CancelOwnPendingLeaveRequestAsync(
                    request.LeaveApplicationId,
                    _currentEmployeeId.Value,
                    _currentUserId > 0 ? _currentUserId : null);

                await RefreshAsync();
                SelectedRequest = LeaveRequests.FirstOrDefault(x => x.LeaveApplicationId == request.LeaveApplicationId);
                SetMessage($"Leave request #{request.LeaveApplicationId} was cancelled.", SuccessBrush);
                SystemRefreshBus.Raise("LeaveRequestCancelled");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to cancel leave request: {ex.Message}", ErrorBrush);
            }
        }

        private async Task UpdateRequestStatusAsync(object? parameter, string status, bool requireReason)
        {
            if (!IsAdminOrHrMode)
            {
                SetMessage("You do not have permission to change request status.", ErrorBrush);
                return;
            }

            var request = parameter as LeaveRequestVm ?? SelectedRequest;
            if (request == null)
            {
                SetMessage("Select a leave request first.", ErrorBrush);
                return;
            }

            var remarks = (DecisionRemarks ?? string.Empty).Trim();
            if (requireReason && string.IsNullOrWhiteSpace(remarks))
            {
                SetMessage("Decision reason is required for rejection.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.UpdateLeaveRequestStatusAsync(
                    request.LeaveApplicationId,
                    status,
                    remarks,
                    _currentUserId > 0 ? _currentUserId : null);

                DecisionRemarks = string.Empty;
                await RefreshAsync();
                SelectedRequest = LeaveRequests.FirstOrDefault(x => x.LeaveApplicationId == request.LeaveApplicationId);
                SetMessage($"Leave request #{request.LeaveApplicationId} marked as {status}.", SuccessBrush);
                SystemRefreshBus.Raise("LeaveRequestStatusUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to update leave request: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SaveBalanceAsync()
        {
            if (!IsAdminOrHrMode)
            {
                SetMessage("You do not have permission to edit leave balances.", ErrorBrush);
                return;
            }

            if (!SelectedBalanceEmployeeId.HasValue || SelectedBalanceEmployeeId.Value <= 0)
            {
                SetMessage("Select employee for leave balance.", ErrorBrush);
                return;
            }

            if (!SelectedBalanceLeaveTypeId.HasValue || SelectedBalanceLeaveTypeId.Value <= 0)
            {
                SetMessage("Select leave type for balance.", ErrorBrush);
                return;
            }

            if (BalanceYear <= 0)
            {
                SetMessage("Year is required for leave balance.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.UpsertLeaveBalanceAsync(
                    SelectedBalanceEmployeeId.Value,
                    SelectedBalanceLeaveTypeId.Value,
                    BalanceYear,
                    BalanceOpeningCredits,
                    BalanceEarned,
                    BalanceUsed,
                    BalanceAdjustments,
                    BalanceAsOfDate);

                await RefreshAsync();
                SetMessage("Leave balance saved.", SuccessBrush);
                SystemRefreshBus.Raise("LeaveBalanceSaved");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save leave balance: {ex.Message}", ErrorBrush);
            }
        }

        private Task BrowseAttachmentAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Leave Attachment",
                Filter = "All files (*.*)|*.*"
            };

            var result = dialog.ShowDialog();
            if (result == true && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                SelectedAttachmentFilePath = dialog.FileName;
                SetMessage("Attachment selected.", InfoBrush);
            }

            return Task.CompletedTask;
        }

        private async Task UploadAttachmentAsync()
        {
            if (IsEmployeeMode)
            {
                if (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0)
                {
                    SetMessage("Unable to resolve your employee profile.", ErrorBrush);
                    return;
                }
            }

            if (!SelectedAttachmentRequestId.HasValue || SelectedAttachmentRequestId.Value <= 0)
            {
                SetMessage("Select leave request to attach file.", ErrorBrush);
                return;
            }

            if (IsEmployeeMode)
            {
                var request = _allRequests.FirstOrDefault(x => x.LeaveApplicationId == SelectedAttachmentRequestId.Value);
                if (request == null || request.EmployeeId != _currentEmployeeId!.Value)
                {
                    SetMessage("You can only upload attachments to your own leave requests.", ErrorBrush);
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(SelectedAttachmentFilePath))
            {
                SetMessage("Select a file first.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.AddLeaveAttachmentAsync(
                    SelectedAttachmentRequestId.Value,
                    SelectedAttachmentFilePath,
                    _currentUserId > 0 ? _currentUserId : null);

                SelectedAttachmentFilePath = string.Empty;
                await RefreshAsync();
                SetMessage("Attachment uploaded.", SuccessBrush);
                SystemRefreshBus.Raise("LeaveAttachmentAdded");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to upload attachment: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DeleteAttachmentAsync(object? parameter)
        {
            if (!IsAdminOrHrMode)
            {
                SetMessage("You do not have permission to delete attachments.", ErrorBrush);
                return;
            }

            var attachment = parameter as LeaveAttachmentVm ?? SelectedAttachment;
            if (attachment == null)
            {
                SetMessage("Select an attachment to delete.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.DeleteLeaveAttachmentAsync(attachment.LeaveDocumentId);
                await RefreshAsync();
                SetMessage("Attachment deleted.", SuccessBrush);
                SystemRefreshBus.Raise("LeaveAttachmentDeleted");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to delete attachment: {ex.Message}", ErrorBrush);
            }
        }

        private void RebuildOptions(
            IReadOnlyList<LeaveEmployeeOptionDto> employees,
            IReadOnlyList<LeaveTypeDto> leaveTypes,
            IReadOnlyList<LeaveRequestDto> requests)
        {
            EmployeeOptions.Clear();
            foreach (var employee in employees)
            {
                EmployeeOptions.Add(new LeaveEmployeeOptionVm(employee.EmployeeId, employee.EmployeeNo, employee.EmployeeName));
            }

            LeaveTypeOptions.Clear();
            foreach (var leaveType in leaveTypes)
            {
                LeaveTypeOptions.Add(new LeaveTypeOptionVm(
                    leaveType.LeaveTypeId,
                    leaveType.Code,
                    leaveType.Name,
                    leaveType.IsPaid,
                    leaveType.DefaultCreditsPerYear));
            }

            LeaveRequestOptions.Clear();
            foreach (var request in requests.OrderByDescending(x => x.FiledAt))
            {
                LeaveRequestOptions.Add(new LeaveRequestOptionVm(
                    request.LeaveApplicationId,
                    request.EmployeeNo,
                    request.EmployeeName,
                    request.LeaveTypeName,
                    request.DateFrom));
            }
        }

        private void RebuildRequests(IReadOnlyList<LeaveRequestDto> requests)
        {
            _allRequests.Clear();
            foreach (var request in requests)
            {
                _allRequests.Add(new LeaveRequestVm(
                    request.LeaveApplicationId,
                    request.EmployeeId,
                    request.EmployeeNo,
                    request.EmployeeName,
                    request.LeaveTypeId,
                    request.LeaveTypeCode,
                    request.LeaveTypeName,
                    request.DateFrom,
                    request.DateTo,
                    request.DaysRequested,
                    request.Reason,
                    request.Status,
                    request.FiledAt,
                    request.DecisionAt,
                    request.DecisionRemarks,
                    request.AttachmentCount));
            }

            ApplyRequestFilters();
        }

        private void RebuildBalances(IReadOnlyList<LeaveBalanceDto> balances)
        {
            _allBalances.Clear();
            foreach (var balance in balances)
            {
                _allBalances.Add(new LeaveBalanceVm(
                    balance.LeaveBalanceId,
                    balance.EmployeeId,
                    balance.EmployeeNo,
                    balance.EmployeeName,
                    balance.LeaveTypeId,
                    balance.LeaveTypeName,
                    balance.Year,
                    balance.OpeningCredits,
                    balance.Earned,
                    balance.Used,
                    balance.Adjustments,
                    balance.AsOfDate));
            }

            RebuildBalanceYearOptions();
            ApplyBalanceFilters();
        }

        private void RebuildAttachments(IReadOnlyList<LeaveAttachmentDto> attachments)
        {
            _allAttachments.Clear();
            foreach (var attachment in attachments)
            {
                _allAttachments.Add(new LeaveAttachmentVm(
                    attachment.LeaveDocumentId,
                    attachment.LeaveApplicationId,
                    attachment.EmployeeNo,
                    attachment.EmployeeName,
                    attachment.FileName,
                    attachment.FilePath,
                    attachment.UploadedAt));
            }

            ApplyAttachmentFilters();
        }

        private void RebuildBalanceYearOptions()
        {
            var selected = SelectedBalanceYearFilter;
            var years = _allBalances.Select(x => x.Year).Distinct().OrderByDescending(x => x).ToList();
            var currentYear = DateTime.Today.Year;

            if (!years.Contains(currentYear))
            {
                years.Insert(0, currentYear);
            }

            BalanceYearOptions.Clear();
            BalanceYearOptions.Add(0);
            foreach (var year in years)
            {
                BalanceYearOptions.Add(year);
            }

            if (!BalanceYearOptions.Contains(selected))
            {
                SelectedBalanceYearFilter = 0;
            }
        }

        private void ApplyRequestFilters()
        {
            IEnumerable<LeaveRequestVm> query = _allRequests;

            if (!string.Equals(SelectedRequestStatusFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals(x.Status, SelectedRequestStatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            var search = (RequestSearchText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    Contains(x.EmployeeNo, search) ||
                    Contains(x.EmployeeName, search) ||
                    Contains(x.LeaveTypeName, search) ||
                    Contains(x.Status, search) ||
                    Contains(x.Reason, search));
            }

            var selectedId = SelectedRequest?.LeaveApplicationId;
            LeaveRequests.Clear();
            foreach (var request in query)
            {
                LeaveRequests.Add(request);
            }

            if (selectedId.HasValue)
            {
                SelectedRequest = LeaveRequests.FirstOrDefault(x => x.LeaveApplicationId == selectedId.Value);
            }

            if (!selectedId.HasValue || SelectedRequest == null)
            {
                UpdateSelectedRequestContext();
            }
        }

        private void ApplyBalanceFilters()
        {
            IEnumerable<LeaveBalanceVm> query = _allBalances;

            if (SelectedBalanceYearFilter > 0)
            {
                query = query.Where(x => x.Year == SelectedBalanceYearFilter);
            }

            var search = (BalanceSearchText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    Contains(x.EmployeeNo, search) ||
                    Contains(x.EmployeeName, search) ||
                    Contains(x.LeaveTypeName, search));
            }

            LeaveBalances.Clear();
            foreach (var balance in query)
            {
                LeaveBalances.Add(balance);
            }
        }

        private void ApplyAttachmentFilters()
        {
            IEnumerable<LeaveAttachmentVm> query = _allAttachments;

            var search = (AttachmentSearchText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    Contains(x.EmployeeNo, search) ||
                    Contains(x.EmployeeName, search) ||
                    Contains(x.FileName, search) ||
                    Contains(x.FilePath, search));
            }

            LeaveAttachments.Clear();
            foreach (var attachment in query)
            {
                LeaveAttachments.Add(attachment);
            }
        }

        private void ApplyBalanceEditorFromSelection()
        {
            if (!SelectedBalanceEmployeeId.HasValue || SelectedBalanceEmployeeId.Value <= 0 ||
                !SelectedBalanceLeaveTypeId.HasValue || SelectedBalanceLeaveTypeId.Value <= 0 ||
                BalanceYear <= 0)
            {
                return;
            }

            var existing = _allBalances.FirstOrDefault(x =>
                x.EmployeeId == SelectedBalanceEmployeeId.Value &&
                x.LeaveTypeId == SelectedBalanceLeaveTypeId.Value &&
                x.Year == BalanceYear);

            if (existing != null)
            {
                BalanceOpeningCredits = existing.OpeningCredits;
                BalanceEarned = existing.Earned;
                BalanceUsed = existing.Used;
                BalanceAdjustments = existing.Adjustments;
                BalanceAsOfDate = existing.AsOfDate == DateTime.MinValue ? DateTime.Today : existing.AsOfDate;
                return;
            }

            var leaveType = LeaveTypeOptions.FirstOrDefault(x => x.LeaveTypeId == SelectedBalanceLeaveTypeId.Value);
            BalanceOpeningCredits = leaveType?.DefaultCredits ?? 0m;
            BalanceEarned = 0m;
            BalanceUsed = 0m;
            BalanceAdjustments = 0m;
            BalanceAsOfDate = DateTime.Today;
        }

        private void UpdateSelectedRequestContext()
        {
            if (SelectedRequest == null)
            {
                SelectedRequestTimeline = "No request selected.";
                OnPropertyChanged(nameof(CanCancelSelectedRequest));
                return;
            }

            var timeline = $"Submitted: {SelectedRequest.FiledAtText}";
            switch (SelectedRequest.Status)
            {
                case "RECOMMENDED":
                    timeline += SelectedRequest.DecisionAt.HasValue
                        ? $" | Recommended: {SelectedRequest.DecisionAtText}"
                        : " | Recommended";
                    break;
                case "APPROVED":
                    timeline += SelectedRequest.DecisionAt.HasValue
                        ? $" | Approved: {SelectedRequest.DecisionAtText}"
                        : " | Approved";
                    break;
                case "REJECTED":
                    timeline += SelectedRequest.DecisionAt.HasValue
                        ? $" | Rejected: {SelectedRequest.DecisionAtText}"
                        : " | Rejected";
                    break;
                case "CANCELLED":
                    timeline += SelectedRequest.DecisionAt.HasValue
                        ? $" | Cancelled: {SelectedRequest.DecisionAtText}"
                        : " | Cancelled";
                    break;
            }

            SelectedRequestTimeline = timeline;
            OnPropertyChanged(nameof(CanCancelSelectedRequest));
        }

        private void UpdateFileLeavePreview()
        {
            var employeeId = SelectedFileEmployeeId.GetValueOrDefault();
            var leaveTypeId = SelectedFileLeaveTypeId.GetValueOrDefault();
            var from = FileDateFrom.Date;
            var to = FileDateTo.Date < from ? from : FileDateTo.Date;
            var requestedDays = Math.Max(1m, FileDaysRequested);
            var year = from.Year;

            if (employeeId <= 0 || leaveTypeId <= 0)
            {
                FileBalancePreviewText = "Select employee and leave type.";
                FileValidationText = "No blocking issue detected.";
                FileValidationBrush = SuccessBrush;
                _hasBlockingFileConflict = false;
                OnPropertyChanged(nameof(CanFileLeaveRequest));
                return;
            }

            var leaveType = LeaveTypeOptions.FirstOrDefault(x => x.LeaveTypeId == leaveTypeId);
            var balance = _allBalances.FirstOrDefault(x =>
                x.EmployeeId == employeeId &&
                x.LeaveTypeId == leaveTypeId &&
                x.Year == year);

            var availableCredits = balance?.AvailableCredits ?? (leaveType?.DefaultCredits ?? 0m);
            var remainingCredits = availableCredits - requestedDays;
            var usedFallback = balance == null
                ? " (no balance record yet; using default credits)"
                : string.Empty;

            FileBalancePreviewText =
                $"Balance {year}: {availableCredits:0.##} day(s) | Requested: {requestedDays:0.##} day(s) | Remaining: {remainingCredits:0.##} day(s){usedFallback}";

            var hasOverlap = _allRequests.Any(x =>
                x.EmployeeId == employeeId &&
                !string.Equals(x.Status, "REJECTED", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase) &&
                from <= x.DateTo.Date &&
                to >= x.DateFrom.Date);

            var weekendDays = 0;
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    weekendDays++;
                }
            }

            var errors = new List<string>();
            var warnings = new List<string>();

            if (hasOverlap)
            {
                errors.Add("Date range overlaps an existing non-cancelled leave request.");
            }

            if (leaveType is { IsPaid: true } && remainingCredits < 0m)
            {
                errors.Add("Insufficient leave credits for this request.");
            }

            if (weekendDays > 0)
            {
                warnings.Add($"{weekendDays} weekend day(s) detected in the selected range.");
            }

            if (errors.Count > 0)
            {
                _hasBlockingFileConflict = true;
                FileValidationText = string.Join(" ", errors);
                FileValidationBrush = ErrorBrush;
            }
            else if (warnings.Count > 0)
            {
                _hasBlockingFileConflict = false;
                FileValidationText = string.Join(" ", warnings);
                FileValidationBrush = InfoBrush;
            }
            else
            {
                _hasBlockingFileConflict = false;
                FileValidationText = "No blocking issue detected.";
                FileValidationBrush = SuccessBrush;
            }

            OnPropertyChanged(nameof(CanFileLeaveRequest));
        }

        private void RecalculateRequestedDays()
        {
            var from = FileDateFrom.Date;
            var to = FileDateTo.Date;
            if (to < from)
            {
                to = from;
                FileDateTo = from;
            }

            FileDaysRequested = Math.Max(1m, Convert.ToDecimal((to - from).TotalDays + 1, CultureInfo.InvariantCulture));
        }

        private static bool Contains(string source, string search) =>
            !string.IsNullOrWhiteSpace(source) &&
            source.Contains(search, StringComparison.OrdinalIgnoreCase);

        private void SetMessage(string message, Brush brush)
        {
            ActionMessage = message;
            ActionMessageBrush = brush;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class LeaveEmployeeOptionVm
    {
        public LeaveEmployeeOptionVm(int employeeId, string employeeNo, string employeeName)
        {
            EmployeeId = employeeId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
        }

        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public string DisplayText => $"{EmployeeNo} - {EmployeeName}";
    }

    public class LeaveTypeOptionVm
    {
        public LeaveTypeOptionVm(int leaveTypeId, string code, string name, bool isPaid, decimal defaultCredits)
        {
            LeaveTypeId = leaveTypeId;
            Code = string.IsNullOrWhiteSpace(code) ? "-" : code.Trim();
            Name = string.IsNullOrWhiteSpace(name) ? "-" : name.Trim();
            IsPaid = isPaid;
            DefaultCredits = defaultCredits;
        }

        public int LeaveTypeId { get; }
        public string Code { get; }
        public string Name { get; }
        public bool IsPaid { get; }
        public decimal DefaultCredits { get; }
        public string DisplayText => $"{Code} - {Name}";
    }

    public class LeaveRequestOptionVm
    {
        public LeaveRequestOptionVm(long leaveApplicationId, string employeeNo, string employeeName, string leaveTypeName, DateTime dateFrom)
        {
            LeaveApplicationId = leaveApplicationId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            LeaveTypeName = string.IsNullOrWhiteSpace(leaveTypeName) ? "-" : leaveTypeName.Trim();
            DateFrom = dateFrom;
        }

        public long LeaveApplicationId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public string LeaveTypeName { get; }
        public DateTime DateFrom { get; }
        public string DisplayText => $"#{LeaveApplicationId} - {EmployeeNo} - {LeaveTypeName} ({DateFrom:MMM dd})";
    }

    public class LeaveRequestVm
    {
        private static readonly Brush PendingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B9831A"));
        private static readonly Brush RecommendedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5B8DEF"));
        private static readonly Brush ApprovedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush RejectedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));
        private static readonly Brush CancelledBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A98AA"));
        private static readonly Brush DefaultBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4368"));

        public LeaveRequestVm(
            long leaveApplicationId,
            int employeeId,
            string employeeNo,
            string employeeName,
            int leaveTypeId,
            string leaveTypeCode,
            string leaveTypeName,
            DateTime dateFrom,
            DateTime dateTo,
            decimal daysRequested,
            string reason,
            string status,
            DateTime filedAt,
            DateTime? decisionAt,
            string decisionRemarks,
            int attachmentCount)
        {
            LeaveApplicationId = leaveApplicationId;
            EmployeeId = employeeId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            LeaveTypeId = leaveTypeId;
            LeaveTypeCode = string.IsNullOrWhiteSpace(leaveTypeCode) ? "-" : leaveTypeCode.Trim();
            LeaveTypeName = string.IsNullOrWhiteSpace(leaveTypeName) ? "-" : leaveTypeName.Trim();
            DateFrom = dateFrom;
            DateTo = dateTo;
            DaysRequested = daysRequested;
            Reason = string.IsNullOrWhiteSpace(reason) ? "-" : reason.Trim();
            Status = string.IsNullOrWhiteSpace(status) ? "SUBMITTED" : status.Trim().ToUpperInvariant();
            FiledAt = filedAt;
            DecisionAt = decisionAt;
            DecisionRemarksRaw = string.IsNullOrWhiteSpace(decisionRemarks) ? string.Empty : decisionRemarks.Trim();
            AttachmentCount = attachmentCount;
        }

        public long LeaveApplicationId { get; }
        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public int LeaveTypeId { get; }
        public string LeaveTypeCode { get; }
        public string LeaveTypeName { get; }
        public DateTime DateFrom { get; }
        public DateTime DateTo { get; }
        public decimal DaysRequested { get; }
        public string Reason { get; }
        public string Status { get; }
        public DateTime FiledAt { get; }
        public DateTime? DecisionAt { get; }
        public string DecisionRemarksRaw { get; }
        public int AttachmentCount { get; }

        public string DateFromText => DateFrom == DateTime.MinValue ? "-" : DateFrom.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string DateToText => DateTo == DateTime.MinValue ? "-" : DateTo.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string FiledAtText => FiledAt == DateTime.MinValue ? "-" : FiledAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
        public string DecisionAtText => DecisionAt.HasValue ? DecisionAt.Value.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture) : "-";
        public string DaysRequestedText => DaysRequested.ToString("0.##", CultureInfo.InvariantCulture);
        public string DecisionRemarks => string.IsNullOrWhiteSpace(DecisionRemarksRaw) ? "-" : DecisionRemarksRaw;
        public bool CanCancelPending =>
            string.Equals(Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Status, "RECOMMENDED", StringComparison.OrdinalIgnoreCase);

        public Brush StatusBrush => Status switch
        {
            "SUBMITTED" => PendingBrush,
            "RECOMMENDED" => RecommendedBrush,
            "APPROVED" => ApprovedBrush,
            "REJECTED" => RejectedBrush,
            "CANCELLED" => CancelledBrush,
            _ => DefaultBrush
        };
    }

    public class LeaveBalanceVm
    {
        public LeaveBalanceVm(
            long leaveBalanceId,
            int employeeId,
            string employeeNo,
            string employeeName,
            int leaveTypeId,
            string leaveTypeName,
            int year,
            decimal openingCredits,
            decimal earned,
            decimal used,
            decimal adjustments,
            DateTime asOfDate)
        {
            LeaveBalanceId = leaveBalanceId;
            EmployeeId = employeeId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            LeaveTypeId = leaveTypeId;
            LeaveTypeName = string.IsNullOrWhiteSpace(leaveTypeName) ? "-" : leaveTypeName.Trim();
            Year = year;
            OpeningCredits = openingCredits;
            Earned = earned;
            Used = used;
            Adjustments = adjustments;
            AsOfDate = asOfDate;
        }

        public long LeaveBalanceId { get; }
        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public int LeaveTypeId { get; }
        public string LeaveTypeName { get; }
        public int Year { get; }
        public decimal OpeningCredits { get; }
        public decimal Earned { get; }
        public decimal Used { get; }
        public decimal Adjustments { get; }
        public DateTime AsOfDate { get; }
        public decimal AvailableCredits => OpeningCredits + Earned + Adjustments - Used;
        public string AsOfDateText => AsOfDate == DateTime.MinValue ? "-" : AsOfDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
    }

    public class LeaveAttachmentVm
    {
        public LeaveAttachmentVm(
            long leaveDocumentId,
            long leaveApplicationId,
            string employeeNo,
            string employeeName,
            string fileName,
            string filePath,
            DateTime uploadedAt)
        {
            LeaveDocumentId = leaveDocumentId;
            LeaveApplicationId = leaveApplicationId;
            EmployeeNo = string.IsNullOrWhiteSpace(employeeNo) ? "-" : employeeNo.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "-" : employeeName.Trim();
            FileName = string.IsNullOrWhiteSpace(fileName) ? "-" : fileName.Trim();
            FilePath = string.IsNullOrWhiteSpace(filePath) ? string.Empty : filePath.Trim();
            UploadedAt = uploadedAt;
        }

        public long LeaveDocumentId { get; }
        public long LeaveApplicationId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public string FileName { get; }
        public string FilePath { get; }
        public DateTime UploadedAt { get; }
        public string UploadedAtText => UploadedAt == DateTime.MinValue ? "-" : UploadedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
    }
}
