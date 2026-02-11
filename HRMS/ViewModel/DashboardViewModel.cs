using HRMS.Model;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HRMS.ViewModel
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly DashboardDataService _dataService;
        private DashboardStats _stats = new DashboardStats();
        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private DateTime _lastUpdated;
        private bool _isDashboardVisible = true;
        private bool _isTrainingVisible = false;
        private bool _isRecruitmentVisible = false;
        private bool _isPerformanceVisible = false;
        private bool _isUsersVisible = false;
        private bool _isEmployeesVisible = false;
        private bool _isDepartmentsVisible = false;
        private bool _isAttendanceVisible = false;
        private bool _isLeaveVisible = false;
        private bool _isPayrollVisible = false;

        public DashboardViewModel()
        {
            _dataService = new DashboardDataService(DbConfig.ConnectionString);
            RefreshCommand = new AsyncRelayCommand(_ => LoadStatsAsync());

            // Eagerly kick off the first load
            RefreshCommand.Execute(null);
        }

        public ICommand RefreshCommand { get; }

        public DashboardStats Stats
        {
            get => _stats;
            private set
            {
                _stats = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            private set
            {
                if (_lastUpdated != value)
                {
                    _lastUpdated = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDashboardVisible
        {
            get => _isDashboardVisible;
            private set
            {
                if (_isDashboardVisible != value)
                {
                    _isDashboardVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsTrainingVisible
        {
            get => _isTrainingVisible;
            private set
            {
                if (_isTrainingVisible != value)
                {
                    _isTrainingVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsRecruitmentVisible
        {
            get => _isRecruitmentVisible;
            private set
            {
                if (_isRecruitmentVisible != value)
                {
                    _isRecruitmentVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsPerformanceVisible
        {
            get => _isPerformanceVisible;
            private set
            {
                if (_isPerformanceVisible != value)
                {
                    _isPerformanceVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsUsersVisible
        {
            get => _isUsersVisible;
            private set
            {
                if (_isUsersVisible != value)
                {
                    _isUsersVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEmployeesVisible
        {
            get => _isEmployeesVisible;
            private set { if (_isEmployeesVisible != value) { _isEmployeesVisible = value; OnPropertyChanged(); } }
        }

        public bool IsDepartmentsVisible
        {
            get => _isDepartmentsVisible;
            private set { if (_isDepartmentsVisible != value) { _isDepartmentsVisible = value; OnPropertyChanged(); } }
        }

        public bool IsAttendanceVisible
        {
            get => _isAttendanceVisible;
            private set { if (_isAttendanceVisible != value) { _isAttendanceVisible = value; OnPropertyChanged(); } }
        }

        public bool IsLeaveVisible
        {
            get => _isLeaveVisible;
            private set { if (_isLeaveVisible != value) { _isLeaveVisible = value; OnPropertyChanged(); } }
        }

        public bool IsPayrollVisible
        {
            get => _isPayrollVisible;
            private set { if (_isPayrollVisible != value) { _isPayrollVisible = value; OnPropertyChanged(); } }
        }

        public void ShowDashboard()
        {
            IsDashboardVisible = true;
            IsTrainingVisible = false;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = false;
            IsUsersVisible = false;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = false;
            IsLeaveVisible = false;
            IsPayrollVisible = false;
        }

        public void ShowTraining()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = true;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = false;
            IsUsersVisible = false;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = false;
            IsLeaveVisible = false;
            IsPayrollVisible = false;
        }

        public void ShowRecruitment()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = false;
            IsRecruitmentVisible = true;
            IsPerformanceVisible = false;
            IsUsersVisible = false;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = false;
            IsLeaveVisible = false;
            IsPayrollVisible = false;
        }

        public void ShowPerformance()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = false;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = true;
            IsUsersVisible = false;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = false;
            IsLeaveVisible = false;
            IsPayrollVisible = false;
        }

        public void ShowUsers()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = false;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = false;
            IsUsersVisible = true;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = false;
            IsLeaveVisible = false;
            IsPayrollVisible = false;
        }

        public void ShowEmployees()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = false;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = false;
            IsUsersVisible = false;
            IsEmployeesVisible = true;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = false;
            IsLeaveVisible = false;
            IsPayrollVisible = false;
        }

        public void ShowDepartments()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = false;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = false;
            IsUsersVisible = false;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = true;
            IsAttendanceVisible = false;
            IsLeaveVisible = false;
            IsPayrollVisible = false;
        }

        public void ShowAttendance()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = false;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = false;
            IsUsersVisible = false;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = true;
            IsLeaveVisible = false;
            IsPayrollVisible = false;
        }

        public void ShowLeave()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = false;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = false;
            IsUsersVisible = false;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = false;
            IsLeaveVisible = true;
            IsPayrollVisible = false;
        }

        public void ShowPayroll()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = false;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = false;
            IsUsersVisible = false;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = false;
            IsLeaveVisible = false;
            IsPayrollVisible = true;
        }

        private async Task LoadStatsAsync()
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            try
            {
                Stats = await _dataService.GetDashboardStatsAsync();
                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
