using HRMS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Input;

namespace HRMS.ViewModel
{
    public class TrainingViewModel : INotifyPropertyChanged
    {
        private readonly TrainingDataService _dataService = new(DbConfig.ConnectionString);
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private readonly List<TrainingCourseSummary> _allCourses = new();
        private readonly List<TrainingEnrollment> _allEnrollments = new();
        private int _currentUserId;
        private bool _isEmployeeMode;
        private int? _currentEmployeeId;

        public ObservableCollection<TrainingCourseSummary> Courses { get; } = new();
        public ObservableCollection<TrainingEnrollment> Enrollments { get; } = new();

        private int _totalCourses;
        private int _activeCourses;
        private int _enrollmentsToday;
        private int _completions;
        private string _selectedFilter = "All";
        private bool _isScopedUserLinked = true;

        public int TotalCourses
        {
            get => _totalCourses;
            set { _totalCourses = value; OnPropertyChanged(); }
        }

        public int ActiveCourses
        {
            get => _activeCourses;
            set { _activeCourses = value; OnPropertyChanged(); }
        }

        public int EnrollmentsToday
        {
            get => _enrollmentsToday;
            set { _enrollmentsToday = value; OnPropertyChanged(); }
        }

        public int Completions
        {
            get => _completions;
            set { _completions = value; OnPropertyChanged(); }
        }

        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (_selectedFilter != value)
                {
                    _selectedFilter = value;
                    OnPropertyChanged();
                }
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
                OnPropertyChanged(nameof(CanRequestEnrollment));
            }
        }

        public bool IsAdminOrHrMode => !IsEmployeeMode;
        public bool CanRequestEnrollment => IsEmployeeMode && IsScopedUserLinked && _currentEmployeeId.HasValue && _currentEmployeeId.Value > 0;

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
                OnPropertyChanged(nameof(CanRequestEnrollment));
            }
        }

        public ICommand FilterCommand { get; }

        public TrainingViewModel()
        {
            FilterCommand = new AsyncRelayCommand(p =>
            {
                SelectedFilter = p?.ToString() ?? "All";
                ApplyFilter();
                return Task.CompletedTask;
            });

            _ = LoadAsync();
        }

        public Task RefreshAsync() => LoadAsync();

        public void SetCurrentUser(int userId, string? roleName)
        {
            _currentUserId = userId;
            IsEmployeeMode = string.Equals(roleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);
            _currentEmployeeId = null;
            OnPropertyChanged(nameof(CanRequestEnrollment));
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            await _loadLock.WaitAsync();
            try
            {
                if (IsEmployeeMode && _currentUserId > 0 && (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0))
                {
                    _currentEmployeeId = await _dataService.GetEmployeeIdByUserIdAsync(_currentUserId);
                }

                IsScopedUserLinked = !IsEmployeeMode || (_currentEmployeeId.HasValue && _currentEmployeeId.Value > 0);

                await LoadCoursesAsync();
                await LoadEnrollmentsAsync(IsEmployeeMode ? _currentEmployeeId : null);
                ComputeStats();
                OnPropertyChanged(nameof(CanRequestEnrollment));
            }
            catch
            {
                // swallow for now; UI could show error if desired
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task LoadCoursesAsync()
        {
            Courses.Clear();
            _allCourses.Clear();
            var courses = await _dataService.GetCoursesAsync();
            var seenCourseIds = new HashSet<int>();
            foreach (var c in courses)
            {
                if (!seenCourseIds.Add(c.Id))
                {
                    continue;
                }

                var course = new TrainingCourseSummary
                {
                    Id = c.Id,
                    Title = c.Title,
                    Provider = c.Provider,
                    Description = c.Description,
                    Hours = c.Hours,
                    Status = c.Status,
                    StatusColor = GetStatusBrush(c.Status)
                };

                _allCourses.Add(course);
            }

            ApplyFilter();
        }

        private async Task LoadEnrollmentsAsync(int? scopedEmployeeId)
        {
            Enrollments.Clear();
            _allEnrollments.Clear();
            var enrollments = await _dataService.GetEnrollmentsAsync(scopedEmployeeId);
            foreach (var e in enrollments)
            {
                var enrollment = new TrainingEnrollment
                {
                    EnrollmentId = e.EnrollmentId,
                    EmployeeId = e.EmployeeId,
                    Employee = e.Employee,
                    Course = e.Course,
                    ScheduleDate = e.ScheduleDate,
                    Status = e.Status,
                    StatusColor = GetStatusBrush(e.Status)
                };

                _allEnrollments.Add(enrollment);
            }

            ApplyFilter();
        }

        private void ComputeStats()
        {
            // Use full datasets so stats stay stable regardless of filters
            TotalCourses = _allCourses.Count;
            ActiveCourses = _allCourses.Count(c => string.Equals(c.Status, "Active", StringComparison.OrdinalIgnoreCase));
            Completions = _allEnrollments.Count(e => string.Equals(e.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            EnrollmentsToday = _allEnrollments.Count;
        }

        internal static Brush GetStatusBrush(string status)
        {
            return status?.ToLower() switch
            {
                "active" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50")),
                "inactive" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935")),
                "scheduled" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#FDBD55")),
                "requested" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B")),
                "enrolled" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E4368")),
                "pending" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E4368")),
                "completed" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50")),
                "rejected" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935")),
                "failed" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935")),
                _ => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#9E9E9E")),
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void RemoveCourse(int id)
        {
            var toRemove = _allCourses.FirstOrDefault(c => c.Id == id);
            if (toRemove != null) _allCourses.Remove(toRemove);

            var toRemoveView = Courses.FirstOrDefault(c => c.Id == id);
            if (toRemoveView != null) Courses.Remove(toRemoveView);

            ComputeStats();
        }

        public void UpdateCourseStatusColor(TrainingCourseSummary course)
        {
            course.StatusColor = GetStatusBrush(course.Status);
        }

        public void AddCourse(TrainingCourseSummary course)
        {
            _allCourses.Add(course);
            ApplyFilter();
        }

        public void AddEnrollment(TrainingEnrollment enrollment)
        {
            _allEnrollments.Add(enrollment);
            ApplyFilter();
        }

        public async Task ApplyEnrollmentActionAsync(TrainingEnrollment enrollment, string? action)
        {
            if (enrollment is null || enrollment.EnrollmentId <= 0 || string.IsNullOrWhiteSpace(action))
            {
                return;
            }

            if (string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase))
            {
                await _dataService.DeleteEnrollmentAsync(enrollment.EnrollmentId);

                _allEnrollments.Remove(enrollment);
                Enrollments.Remove(enrollment);
                ComputeStats();
                return;
            }

            var normalizedStatus = string.Equals(action, "Complete", StringComparison.OrdinalIgnoreCase)
                ? "Completed"
                : action;

            await _dataService.UpdateEnrollmentStatusAsync(enrollment.EnrollmentId, normalizedStatus);

            enrollment.Status = normalizedStatus;
            enrollment.StatusColor = GetStatusBrush(normalizedStatus);
            ComputeStats();
        }

        public async Task<(bool Success, string Message)> RequestEnrollmentAsync(TrainingCourseSummary course)
        {
            if (course is null || course.Id <= 0)
            {
                return (false, "Invalid course selection.");
            }

            if (!CanRequestEnrollment || !_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0)
            {
                return (false, "Your employee profile is not linked to this account.");
            }

            if (!string.Equals(course.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Only active courses can be requested.");
            }

            var existing = _allEnrollments.FirstOrDefault(e =>
                string.Equals(e.Course, course.Title, StringComparison.OrdinalIgnoreCase));

            if (existing != null &&
                !string.Equals(existing.Status, "Rejected", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(existing.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"You already have a {existing.Status.ToLowerInvariant()} record for this course.");
            }

            await _dataService.AddEnrollmentAsync(_currentEmployeeId.Value, course.Id, DateTime.Today, "Requested");
            await LoadAsync();
            return (true, "Enrollment request submitted. HR/Admin will review it.");
        }

        public void RefreshFilter()
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filter = SelectedFilter?.ToLowerInvariant() ?? "all";

            IEnumerable<TrainingCourseSummary> courseQuery = _allCourses;
            if (filter != "all")
            {
                courseQuery = _allCourses.Where(c => string.Equals(c.Status, filter, StringComparison.OrdinalIgnoreCase));
            }

            Courses.Clear();
            foreach (var c in courseQuery)
                Courses.Add(c);

            IEnumerable<TrainingEnrollment> enrollmentQuery = _allEnrollments;
            if (filter != "all")
            {
                enrollmentQuery = _allEnrollments.Where(e =>
                    filter == "active"
                        ? string.Equals(e.Status, "Enrolled", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(e.Status, "Requested", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase)
                        : string.Equals(e.Status, filter, StringComparison.OrdinalIgnoreCase));
            }

            Enrollments.Clear();
            foreach (var e in enrollmentQuery)
                Enrollments.Add(e);

            ComputeStats();
        }
    }

    public class TrainingCourseSummary : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        private string _provider = string.Empty;
        public string Provider
        {
            get => _provider;
            set { _provider = value; OnPropertyChanged(); }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        private double _hours;
        public double Hours
        {
            get => _hours;
            set { _hours = value; OnPropertyChanged(); }
        }

        private string _status = string.Empty;
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private Brush? _statusColor;
        public Brush? StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TrainingEnrollment : INotifyPropertyChanged
    {
        public long EnrollmentId { get; set; }
        public int EmployeeId { get; set; }

        public string Employee { get; set; } = string.Empty;
        public string Course { get; set; } = string.Empty;
        public DateTime? ScheduleDate { get; set; }

        private string _status = string.Empty;
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private Brush? _statusColor;
        public Brush? StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
