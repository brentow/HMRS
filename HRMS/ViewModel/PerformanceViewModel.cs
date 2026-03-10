using HRMS.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HRMS.ViewModel
{
    public class PerformanceViewModel : INotifyPropertyChanged
    {
        private readonly PerformanceDataService _dataService = new(DbConfig.ConnectionString);
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private int _currentUserId;
        private bool _isEmployeeMode;
        private int? _currentEmployeeId;
        private bool _isScopedUserLinked = true;

        private int _totalCycles;
        private int _openCycles;
        private int _totalReviews;
        private int _submittedReviews;
        private int _draftReviews;
        private double _avgRating;

        public int TotalCycles { get => _totalCycles; set { _totalCycles = value; OnPropertyChanged(); } }
        public int OpenCycles { get => _openCycles; set { _openCycles = value; OnPropertyChanged(); } }
        public int TotalReviews { get => _totalReviews; set { _totalReviews = value; OnPropertyChanged(); } }
        public int SubmittedReviews { get => _submittedReviews; set { _submittedReviews = value; OnPropertyChanged(); } }
        public int DraftReviews { get => _draftReviews; set { _draftReviews = value; OnPropertyChanged(); } }
        public double AvgRating { get => _avgRating; set { _avgRating = value; OnPropertyChanged(); } }

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
            }
        }

        public bool IsAdminOrHrMode => !IsEmployeeMode;

        public bool IsScopedUserLinked
        {
            get => _isScopedUserLinked;
            private set
            {
                if (_isScopedUserLinked == value)
                {
                    return;
                }

                _isScopedUserLinked = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ChartItem> ReviewChart { get; } = new();
        public ObservableCollection<TopPerformer> TopPerformers { get; } = new();
        public ObservableCollection<PerformanceCycleRowVm> Cycles { get; } = new();
        public ObservableCollection<PerformanceReviewRowVm> Reviews { get; } = new();

        public PerformanceViewModel()
        {
        }

        public void SetCurrentUser(int userId, string? roleName)
        {
            _currentUserId = userId;
            IsEmployeeMode = string.Equals(roleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);
            _currentEmployeeId = null;
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                if (IsEmployeeMode && _currentUserId > 0 && (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0))
                {
                    _currentEmployeeId = await _dataService.GetEmployeeIdByUserIdAsync(_currentUserId);
                }

                IsScopedUserLinked = !IsEmployeeMode || (_currentEmployeeId.HasValue && _currentEmployeeId.Value > 0);
                var scopedEmployeeId = IsEmployeeMode ? _currentEmployeeId : null;

                if (IsEmployeeMode && !IsScopedUserLinked)
                {
                    ClearForUnlinkedEmployee();
                    return;
                }

                var stats = await _dataService.GetStatsAsync(scopedEmployeeId);
                TotalCycles = stats.TotalCycles;
                OpenCycles = stats.OpenCycles;
                TotalReviews = stats.TotalReviews;
                SubmittedReviews = stats.SubmittedReviews;
                DraftReviews = stats.DraftReviews;
                AvgRating = stats.AverageRating;

                var remaining = Math.Max(0, TotalReviews - SubmittedReviews - DraftReviews);

                ReviewChart.Clear();
                ReviewChart.Add(new ChartItem("Submitted", SubmittedReviews, "#1E4368"));
                ReviewChart.Add(new ChartItem("Draft", DraftReviews, "#FDBD55"));
                ReviewChart.Add(new ChartItem("Remaining", remaining, "#CBE9FE"));

                TopPerformers.Clear();
                var top = await _dataService.GetTopPerformersAsync(5, scopedEmployeeId);
                foreach (var t in top)
                {
                    TopPerformers.Add(new TopPerformer(t.Employee, t.Rating));
                }

                Cycles.Clear();
                var cycles = await _dataService.GetCyclesAsync(scopedEmployeeId);
                foreach (var cycle in cycles)
                {
                    Cycles.Add(new PerformanceCycleRowVm
                    {
                        Id = cycle.Id,
                        CycleCode = cycle.CycleCode,
                        Name = cycle.Name,
                        StartDate = cycle.StartDate,
                        EndDate = cycle.EndDate,
                        Status = cycle.Status,
                        CreatedBy = cycle.CreatedBy
                    });
                }

                Reviews.Clear();
                var reviews = await _dataService.GetReviewsAsync(scopedEmployeeId);
                foreach (var review in reviews)
                {
                    Reviews.Add(new PerformanceReviewRowVm
                    {
                        Id = review.Id,
                        CycleCode = review.CycleCode,
                        Employee = review.Employee,
                        Reviewer = review.Reviewer,
                        Rating = review.Rating,
                        Status = review.Status,
                        Remarks = review.Remarks,
                        ItemsCount = review.ItemsCount
                    });
                }
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public async Task SaveCycleAsync(PerformanceCycleRowVm cycle)
        {
            if (!IsAdminOrHrMode)
            {
                throw new InvalidOperationException("You only have read-only access to your performance data.");
            }

            if (cycle is null)
            {
                return;
            }

            if (cycle.EndDate < cycle.StartDate)
            {
                throw new InvalidOperationException("Cycle end date cannot be earlier than start date.");
            }

            await _dataService.UpdateCycleAsync(cycle.Id, cycle.Name, cycle.StartDate, cycle.EndDate, cycle.Status);
            await RefreshAsync();
        }

        public async Task SaveReviewAsync(PerformanceReviewRowVm review)
        {
            if (!IsAdminOrHrMode)
            {
                throw new InvalidOperationException("You only have read-only access to your performance data.");
            }

            if (review is null)
            {
                return;
            }

            if (review.Rating.HasValue && (review.Rating.Value < 0 || review.Rating.Value > 5))
            {
                throw new InvalidOperationException("Rating must be between 0 and 5.");
            }

            await _dataService.UpdateReviewAsync(review.Id, review.Rating, review.Status, review.Remarks);
            await RefreshAsync();
        }

        private void ClearForUnlinkedEmployee()
        {
            TotalCycles = 0;
            OpenCycles = 0;
            TotalReviews = 0;
            SubmittedReviews = 0;
            DraftReviews = 0;
            AvgRating = 0;

            ReviewChart.Clear();
            TopPerformers.Clear();
            Cycles.Clear();
            Reviews.Clear();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public record ChartItem(string Label, int Value, string ColorHex);

    public record TopPerformer(string Name, double Rating);

    public class PerformanceCycleRowVm : INotifyPropertyChanged
    {
        public long Id { get; set; }

        private string _cycleCode = string.Empty;
        public string CycleCode { get => _cycleCode; set { _cycleCode = value; OnPropertyChanged(); } }

        private string _name = string.Empty;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private DateTime _startDate = DateTime.Today;
        public DateTime StartDate { get => _startDate; set { _startDate = value; OnPropertyChanged(); } }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate { get => _endDate; set { _endDate = value; OnPropertyChanged(); } }

        private string _status = "DRAFT";
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        private string _createdBy = "-";
        public string CreatedBy { get => _createdBy; set { _createdBy = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PerformanceReviewRowVm : INotifyPropertyChanged
    {
        public long Id { get; set; }

        private string _cycleCode = string.Empty;
        public string CycleCode { get => _cycleCode; set { _cycleCode = value; OnPropertyChanged(); } }

        private string _employee = string.Empty;
        public string Employee { get => _employee; set { _employee = value; OnPropertyChanged(); } }

        private string _reviewer = string.Empty;
        public string Reviewer { get => _reviewer; set { _reviewer = value; OnPropertyChanged(); } }

        private double? _rating;
        public double? Rating { get => _rating; set { _rating = value; OnPropertyChanged(); } }

        private string _status = "DRAFT";
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        private string _remarks = string.Empty;
        public string Remarks { get => _remarks; set { _remarks = value; OnPropertyChanged(); } }

        private int _itemsCount;
        public int ItemsCount { get => _itemsCount; set { _itemsCount = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
