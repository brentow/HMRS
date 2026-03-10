using HRMS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HRMS.ViewModel
{
    public partial class AttendanceViewModel
    {
        private readonly List<AttendanceAdjustmentVm> _allAdjustments = new();
        private string _adjustmentSearchText = string.Empty;
        private string _selectedAdjustmentStatusFilter = "All";
        private string _adjustmentDecisionReason = string.Empty;
        private AttendanceAdjustmentVm? _selectedAdjustment;
        private int _approvedAdjustments;
        private int _rejectedAdjustments;

        public ObservableCollection<string> AdjustmentStatusFilters { get; } = new()
        {
            "All",
            "PENDING",
            "APPROVED",
            "REJECTED"
        };

        public string AdjustmentSearchText
        {
            get => _adjustmentSearchText;
            set
            {
                if (_adjustmentSearchText == value)
                {
                    return;
                }

                _adjustmentSearchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyAdjustmentFilters();
            }
        }

        public string SelectedAdjustmentStatusFilter
        {
            get => _selectedAdjustmentStatusFilter;
            set
            {
                if (_selectedAdjustmentStatusFilter == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _selectedAdjustmentStatusFilter = value;
                OnPropertyChanged();
                ApplyAdjustmentFilters();
            }
        }

        public string AdjustmentDecisionReason
        {
            get => _adjustmentDecisionReason;
            set
            {
                if (_adjustmentDecisionReason == value)
                {
                    return;
                }

                _adjustmentDecisionReason = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public AttendanceAdjustmentVm? SelectedAdjustment
        {
            get => _selectedAdjustment;
            set
            {
                if (_selectedAdjustment == value)
                {
                    return;
                }

                _selectedAdjustment = value;
                OnPropertyChanged();

                if (_selectedAdjustment != null && !string.IsNullOrWhiteSpace(_selectedAdjustment.DecisionRemarks))
                {
                    AdjustmentDecisionReason = _selectedAdjustment.DecisionRemarks;
                }
            }
        }

        public int ApprovedAdjustments
        {
            get => _approvedAdjustments;
            private set
            {
                if (_approvedAdjustments == value)
                {
                    return;
                }

                _approvedAdjustments = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAdjustmentRequests));
                OnPropertyChanged(nameof(FourthAdjustmentsCardValue));
            }
        }

        public int RejectedAdjustments
        {
            get => _rejectedAdjustments;
            private set
            {
                if (_rejectedAdjustments == value)
                {
                    return;
                }

                _rejectedAdjustments = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAdjustmentRequests));
                OnPropertyChanged(nameof(FourthAdjustmentsCardValue));
            }
        }

        private void InitializeAdjustmentsAdmin()
        {
            SelectedAdjustmentStatusFilter = "All";
        }

        private void RebuildAdjustments(IReadOnlyList<AttendanceAdjustmentDto> adjustments, AttendanceAdjustmentCountsDto counts)
        {
            _allAdjustments.Clear();
            foreach (var adjustment in adjustments)
            {
                _allAdjustments.Add(new AttendanceAdjustmentVm(
                    adjustment.AdjustmentId,
                    adjustment.EmployeeNo,
                    adjustment.EmployeeName,
                    adjustment.WorkDate,
                    adjustment.RequestedIn,
                    adjustment.RequestedOut,
                    adjustment.Reason,
                    adjustment.Status,
                    adjustment.RequestedAt,
                    adjustment.DecisionRemarks,
                    adjustment.DecidedAt));
            }

            PendingAdjustments = counts.Pending;
            ApprovedAdjustments = counts.Approved;
            RejectedAdjustments = counts.Rejected;
            ApplyAdjustmentFilters();
        }

        private void ApplyAdjustmentFilters()
        {
            IEnumerable<AttendanceAdjustmentVm> query = _allAdjustments;

            if (!string.Equals(SelectedAdjustmentStatusFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(a => string.Equals(a.Status, SelectedAdjustmentStatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            var search = (AdjustmentSearchText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a =>
                    Contains(a.EmployeeNo, search) ||
                    Contains(a.EmployeeName, search) ||
                    Contains(a.Reason, search) ||
                    Contains(a.Status, search) ||
                    Contains(a.DecisionRemarks, search));
            }

            var selectedId = SelectedAdjustment?.AdjustmentId;

            Adjustments.Clear();
            foreach (var item in query)
            {
                Adjustments.Add(item);
            }

            if (selectedId.HasValue)
            {
                SelectedAdjustment = Adjustments.FirstOrDefault(x => x.AdjustmentId == selectedId.Value);
            }
        }
    }
}
