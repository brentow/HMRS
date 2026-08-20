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
        private string _selectedRemarkType = "OTHER";
        private string _newAttendanceRemarkDetails = string.Empty;
        private string _attendanceRemarkSearchText = string.Empty;
        private string _selectedAttendanceRemarkFilter = "ALL";
        private AttendanceRemarkVm? _selectedAttendanceRemark;

        public ObservableCollection<AttendanceRemarkVm> AttendanceRemarks { get; } = new();
        public bool IsAttendanceRemarksEmpty => AttendanceRemarks.Count == 0;
        public ObservableCollection<AttendanceRemarkTypeOptionVm> AttendanceRemarkTypeOptions { get; } = new()
        {
            new("OB", "Official Business"),
            new("TO", "Travel Order"),
            new("HOLIDAY", "Holiday"),
            new("WFH", "Work From Home"),
            new("CTO", "Compensatory Time Off"),
            new("OTHER", "Other Attendance Note")
        };

        public ObservableCollection<AttendanceRemarkTypeOptionVm> AttendanceRemarkFilterOptions { get; } = new()
        {
            new("ALL", "All special entries"),
            new("HOLIDAY", "Holidays"),
            new("TO", "Travel Orders"),
            new("OB", "Official Business"),
            new("WFH", "Work From Home"),
            new("CTO", "Compensatory Time Off"),
            new("OTHER", "Other Attendance Notes")
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

        public string SelectedAttendanceRemarkFilter
        {
            get => _selectedAttendanceRemarkFilter;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value)
                    ? "ALL"
                    : value.Trim().ToUpperInvariant();

                if (_selectedAttendanceRemarkFilter == normalized)
                {
                    return;
                }

                _selectedAttendanceRemarkFilter = normalized;
                OnPropertyChanged();
                ApplyAttendanceRemarkFilters();
            }
        }

        public bool IsEditingAttendanceRemark => SelectedAttendanceRemark != null;
        public string AttendanceRemarkEditorTitle => IsEditingAttendanceRemark
            ? "Edit Special Entry"
            : "New Special Entry";
        public string AttendanceRemarkSaveButtonText => IsEditingAttendanceRemark
            ? "Save Changes"
            : "Create Entry";

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
                OnPropertyChanged(nameof(IsEditingAttendanceRemark));
                OnPropertyChanged(nameof(AttendanceRemarkEditorTitle));
                OnPropertyChanged(nameof(AttendanceRemarkSaveButtonText));

                if (_selectedAttendanceRemark != null)
                {
                    SelectedRemarkEmployeeId = _selectedAttendanceRemark.EmployeeId;
                    NewAttendanceRemarkDate = _selectedAttendanceRemark.WorkDate;
                    SelectedRemarkType = _selectedAttendanceRemark.RemarkType;
                    NewAttendanceRemarkDetails = _selectedAttendanceRemark.Details;
                }
            }
        }

        public void BeginNewAttendanceRemark()
        {
            SelectedAttendanceRemark = null;
            SelectedRemarkEmployeeId = IsEmployeeMode && _currentEmployeeId.HasValue
                ? _currentEmployeeId.Value
                : null;
            NewAttendanceRemarkDate = DateTime.Today;
            SelectedRemarkType = string.Equals(SelectedAttendanceRemarkFilter, "ALL", StringComparison.OrdinalIgnoreCase)
                ? "OTHER"
                : SelectedAttendanceRemarkFilter;
            NewAttendanceRemarkDetails = string.Empty;
        }

        public void BeginEditAttendanceRemark(AttendanceRemarkVm remark)
        {
            SelectedAttendanceRemark = remark;
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
            if (!string.Equals(SelectedAttendanceRemarkFilter, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    string.Equals(x.RemarkType, SelectedAttendanceRemarkFilter, StringComparison.OrdinalIgnoreCase));
            }

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
            OnPropertyChanged(nameof(IsAttendanceRemarksEmpty));

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
                SetMessage("Select an employee for this special entry.", ErrorBrush);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedRemarkType))
            {
                SetMessage("Select a special entry type.", ErrorBrush);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewAttendanceRemarkDetails))
            {
                SetMessage("Enter remark details first.", ErrorBrush);
                return;
            }

            try
            {
                var isEditing = SelectedAttendanceRemark != null;
                if (isEditing)
                {
                    await _dataService.UpdateAttendanceRemarkAsync(
                        SelectedAttendanceRemark!.RemarkId,
                        employeeId,
                        NewAttendanceRemarkDate.Date,
                        SelectedRemarkType,
                        NewAttendanceRemarkDetails,
                        IsEmployeeMode ? _currentEmployeeId : null);
                }
                else
                {
                    await _dataService.UpsertAttendanceRemarkAsync(
                        employeeId,
                        NewAttendanceRemarkDate.Date,
                        SelectedRemarkType,
                        NewAttendanceRemarkDetails);
                }

                await RefreshAsync();
                SetMessage(isEditing ? "Special entry updated." : "Special entry created.", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceRemarkSaved");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save special entry: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DeleteAttendanceRemarkAsync(object? parameter)
        {
            var remark = parameter as AttendanceRemarkVm ?? SelectedAttendanceRemark;
            if (remark == null)
            {
                SetMessage("Select a special entry first.", ErrorBrush);
                return;
            }

            if (IsEmployeeMode && (!_currentEmployeeId.HasValue || remark.EmployeeId != _currentEmployeeId.Value))
            {
                SetMessage("You can only delete your own special entries.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.DeleteAttendanceRemarkAsync(
                    remark.RemarkId,
                    IsEmployeeMode ? _currentEmployeeId : null);

                await RefreshAsync();
                SetMessage("Special entry deleted.", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceRemarkDeleted");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to delete special entry: {ex.Message}", ErrorBrush);
            }
        }
    }

    public sealed class AttendanceRemarkTypeOptionVm
    {
        public AttendanceRemarkTypeOptionVm(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public string Code { get; }
        public string Label { get; }
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
        public string RemarkTypeLabel => RemarkType switch
        {
            "HOLIDAY" => "Holiday",
            "TO" => "Travel Order",
            "OB" => "Official Business",
            "WFH" => "Work From Home",
            "CTO" => "Compensatory Time Off",
            _ => "Other Attendance Note"
        };
        public string Details { get; }
        public DateTime CreatedAt { get; }
        public string WorkDateText => WorkDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string CreatedAtText => CreatedAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
    }
}
