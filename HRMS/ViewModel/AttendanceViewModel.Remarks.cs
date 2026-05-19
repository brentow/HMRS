using HRMS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HRMS.ViewModel
{
    public partial class AttendanceViewModel
    {
        private readonly List<AttendanceRemarkVm> _allAttendanceRemarks = new();
        private int? _selectedRemarkEmployeeId;
        private DateTime _newAttendanceRemarkDate = DateTime.Today;
        private string _selectedRemarkType = "TO";
        private string _newAttendanceRemarkDetails = string.Empty;
        private string _attendanceRemarkSearchText = string.Empty;
        private AttendanceRemarkVm? _selectedAttendanceRemark;

        public ObservableCollection<AttendanceRemarkVm> AttendanceRemarks { get; } = new();
        public ObservableCollection<string> AttendanceRemarkTypeOptions { get; } = new()
        {
            "OB",
            "TO",
            "HOLIDAY",
            "WFH",
            "CTO",
            "OTHER"
        };

        public ICommand SaveAttendanceRemarkCommand { get; private set; } = null!;
        public ICommand DeleteAttendanceRemarkCommand { get; private set; } = null!;

        public int? SelectedRemarkEmployeeId
        {
            get => _selectedRemarkEmployeeId;
            set
            {
                if (_selectedRemarkEmployeeId == value)
                {
                    return;
                }

                _selectedRemarkEmployeeId = value;
                OnPropertyChanged();
            }
        }

        public DateTime NewAttendanceRemarkDate
        {
            get => _newAttendanceRemarkDate;
            set
            {
                if (_newAttendanceRemarkDate == value)
                {
                    return;
                }

                _newAttendanceRemarkDate = value;
                OnPropertyChanged();
            }
        }

        public string SelectedRemarkType
        {
            get => _selectedRemarkType;
            set
            {
                if (_selectedRemarkType == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _selectedRemarkType = value.Trim().ToUpperInvariant();
                OnPropertyChanged();
            }
        }

        public string NewAttendanceRemarkDetails
        {
            get => _newAttendanceRemarkDetails;
            set
            {
                if (_newAttendanceRemarkDetails == value)
                {
                    return;
                }

                _newAttendanceRemarkDetails = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string AttendanceRemarkSearchText
        {
            get => _attendanceRemarkSearchText;
            set
            {
                if (_attendanceRemarkSearchText == value)
                {
                    return;
                }

                _attendanceRemarkSearchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyAttendanceRemarkFilters();
            }
        }

        public AttendanceRemarkVm? SelectedAttendanceRemark
        {
            get => _selectedAttendanceRemark;
            set
            {
                if (_selectedAttendanceRemark == value)
                {
                    return;
                }

                _selectedAttendanceRemark = value;
                OnPropertyChanged();

                if (_selectedAttendanceRemark != null)
                {
                    SelectedRemarkEmployeeId = _selectedAttendanceRemark.EmployeeId;
                    NewAttendanceRemarkDate = _selectedAttendanceRemark.WorkDate;
                    SelectedRemarkType = _selectedAttendanceRemark.RemarkType;
                    NewAttendanceRemarkDetails = _selectedAttendanceRemark.Details;
                }
            }
        }

        private void InitializeAttendanceRemarks()
        {
            SaveAttendanceRemarkCommand = new AsyncRelayCommand(_ => SaveAttendanceRemarkAsync());
            DeleteAttendanceRemarkCommand = new AsyncRelayCommand(DeleteAttendanceRemarkAsync);
        }

        private void RebuildAttendanceRemarks(IReadOnlyList<AttendanceRemarkDto> remarks)
        {
            _allAttendanceRemarks.Clear();
            foreach (var remark in remarks)
            {
                _allAttendanceRemarks.Add(new AttendanceRemarkVm(
                    remark.RemarkId,
                    remark.EmployeeId,
                    remark.EmployeeNo,
                    remark.EmployeeName,
                    remark.WorkDate,
                    remark.RemarkType,
                    remark.Details,
                    remark.CreatedAt));
            }

            ApplyAttendanceRemarkFilters();
        }

        private void ApplyAttendanceRemarkFilters()
        {
            IEnumerable<AttendanceRemarkVm> query = _allAttendanceRemarks;
            var search = (AttendanceRemarkSearchText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    Contains(x.EmployeeNo, search) ||
                    Contains(x.EmployeeName, search) ||
                    Contains(x.RemarkType, search) ||
                    Contains(x.Details, search) ||
                    Contains(x.WorkDateText, search));
            }

            var selectedId = SelectedAttendanceRemark?.RemarkId;
            AttendanceRemarks.Clear();
            foreach (var item in query)
            {
                AttendanceRemarks.Add(item);
            }

            if (selectedId.HasValue)
            {
                SelectedAttendanceRemark = AttendanceRemarks.FirstOrDefault(x => x.RemarkId == selectedId.Value);
            }
        }

        private async Task SaveAttendanceRemarkAsync()
        {
            var employeeId = IsEmployeeMode
                ? (_currentEmployeeId.HasValue && _currentEmployeeId.Value > 0 ? _currentEmployeeId.Value : 0)
                : (SelectedRemarkEmployeeId ?? 0);

            if (employeeId <= 0)
            {
                SetMessage("Select an employee for the travel/OB remark.", ErrorBrush);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedRemarkType))
            {
                SetMessage("Select a remark type.", ErrorBrush);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewAttendanceRemarkDetails))
            {
                SetMessage("Enter remark details first.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.UpsertAttendanceRemarkAsync(
                    employeeId,
                    NewAttendanceRemarkDate.Date,
                    SelectedRemarkType,
                    NewAttendanceRemarkDetails);

                await RefreshAsync();
                SetMessage("Travel/OB remark saved.", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceRemarkSaved");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save travel/OB remark: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DeleteAttendanceRemarkAsync(object? parameter)
        {
            var remark = parameter as AttendanceRemarkVm ?? SelectedAttendanceRemark;
            if (remark == null)
            {
                SetMessage("Select a travel/OB remark first.", ErrorBrush);
                return;
            }

            if (IsEmployeeMode && (!_currentEmployeeId.HasValue || remark.EmployeeId != _currentEmployeeId.Value))
            {
                SetMessage("You can only delete your own travel/OB remarks.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.DeleteAttendanceRemarkAsync(
                    remark.RemarkId,
                    IsEmployeeMode ? _currentEmployeeId : null);

                await RefreshAsync();
                SetMessage("Travel/OB remark deleted.", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceRemarkDeleted");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to delete travel/OB remark: {ex.Message}", ErrorBrush);
            }
        }
    }

    public class AttendanceRemarkVm
    {
        public AttendanceRemarkVm(
            long remarkId,
            int employeeId,
            string employeeNo,
            string employeeName,
            DateTime workDate,
            string remarkType,
            string details,
            DateTime createdAt)
        {
            RemarkId = remarkId;
            EmployeeId = employeeId;
            EmployeeNo = employeeNo;
            EmployeeName = employeeName;
            WorkDate = workDate;
            RemarkType = string.IsNullOrWhiteSpace(remarkType) ? "OTHER" : remarkType.Trim().ToUpperInvariant();
            Details = string.IsNullOrWhiteSpace(details) ? "-" : details.Trim();
            CreatedAt = createdAt;
        }

        public long RemarkId { get; }
        public int EmployeeId { get; }
        public string EmployeeNo { get; }
        public string EmployeeName { get; }
        public DateTime WorkDate { get; }
        public string RemarkType { get; }
        public string Details { get; }
        public DateTime CreatedAt { get; }
        public string WorkDateText => WorkDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string CreatedAtText => CreatedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
    }
}
