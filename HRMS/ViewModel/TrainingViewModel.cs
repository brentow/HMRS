using HRMS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Input;

namespace HRMS.ViewModel
{
    public class TrainingViewModel : INotifyPropertyChanged
    {
        private readonly TrainingDataService _dataService = new(DbConfig.ConnectionString);

        private readonly List<TrainingCourseSummary> _allCourses = new();
        private readonly List<TrainingEnrollment> _allEnrollments = new();

        public ObservableCollection<TrainingCourseSummary> Courses { get; } = new();
        public ObservableCollection<TrainingEnrollment> Enrollments { get; } = new();

        private int _totalCourses;
        private int _activeCourses;
        private int _enrollmentsToday;
        private int _completions;
        private string _selectedFilter = "All";

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

        private async Task LoadAsync()
        {
            try
            {
                await LoadCoursesAsync();
                await LoadEnrollmentsAsync();
                ComputeStats();
            }
            catch
            {
                // swallow for now; UI could show error if desired
            }
        }

        private async Task LoadCoursesAsync()
        {
            Courses.Clear();
            _allCourses.Clear();
            var courses = await _dataService.GetCoursesAsync();
            foreach (var c in courses)
            {
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

        private async Task LoadEnrollmentsAsync()
        {
            Enrollments.Clear();
            _allEnrollments.Clear();
            var enrollments = await _dataService.GetEnrollmentsAsync();
            foreach (var e in enrollments)
            {
                var enrollment = new TrainingEnrollment
                {
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
                "enrolled" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E4368")),
                "completed" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50")),
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
