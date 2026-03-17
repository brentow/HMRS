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
        private readonly AuthenticatedUser? _authenticatedUser;
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
        private bool _isAttendanceLogsVisible = false;
        private bool _isAdjustmentsVisible = false;
        private bool _isLeaveVisible = false;
        private bool _isPayrollVisible = false;
        private bool _isDocumentsVisible = false;
        private bool _isBeneficiariesVisible = false;

        public DashboardViewModel(AuthenticatedUser? authenticatedUser = null)
        {
            _authenticatedUser = authenticatedUser;
            _dataService = new DashboardDataService(DbConfig.ConnectionString);
            RefreshCommand = new AsyncRelayCommand(_ => LoadStatsAsync());

            // Eagerly kick off the first load
            RefreshCommand.Execute(null);
        }

        public ICommand RefreshCommand { get; }
        public Task RefreshAsync() => LoadStatsAsync();

        public DashboardStats Stats
        {
            get => _stats;
            private set
            {
                _stats = value;
                OnPropertyChanged();
            }
        }

        public bool ShowAdminHrDashboard => IsAdminAccess || IsHrAccess;
        public bool ShowEmployeeDashboard => IsEmployeeAccess;
        public string DocumentsNavLabel => ShowEmployeeDashboard ? "My Documents" : "Documents";

        public string SnapshotTitle => ShowEmployeeDashboard ? "My Workday Snapshot" : "Organization Snapshot";

        public string SnapshotSubtitle => ShowEmployeeDashboard
            ? "Your attendance, leave, payroll, and development status"
            : "Live HR operations and approval workload";

        private bool IsAdminAccess =>
            string.Equals(_authenticatedUser?.RoleName?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase);

        private bool IsHrAccess =>
            string.Equals(_authenticatedUser?.RoleName?.Trim(), "HR Manager", StringComparison.OrdinalIgnoreCase);

        private bool IsEmployeeAccess =>
            string.Equals(_authenticatedUser?.RoleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);

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

        public bool IsAttendanceLogsVisible
        {
            get => _isAttendanceLogsVisible;
            private set { if (_isAttendanceLogsVisible != value) { _isAttendanceLogsVisible = value; OnPropertyChanged(); } }
        }

        public bool IsAdjustmentsVisible
        {
            get => _isAdjustmentsVisible;
            private set { if (_isAdjustmentsVisible != value) { _isAdjustmentsVisible = value; OnPropertyChanged(); } }
        }

        public bool IsPayrollVisible
        {
            get => _isPayrollVisible;
            private set { if (_isPayrollVisible != value) { _isPayrollVisible = value; OnPropertyChanged(); } }
        }

        public bool IsDocumentsVisible
        {
            get => _isDocumentsVisible;
            private set { if (_isDocumentsVisible != value) { _isDocumentsVisible = value; OnPropertyChanged(); } }
        }

        public bool IsBeneficiariesVisible
        {
            get => _isBeneficiariesVisible;
            private set { if (_isBeneficiariesVisible != value) { _isBeneficiariesVisible = value; OnPropertyChanged(); } }
        }

        private void HideAllModules()
        {
            IsDashboardVisible = false;
            IsTrainingVisible = false;
            IsRecruitmentVisible = false;
            IsPerformanceVisible = false;
            IsUsersVisible = false;
            IsEmployeesVisible = false;
            IsDepartmentsVisible = false;
            IsAttendanceVisible = false;
            IsAttendanceLogsVisible = false;
            IsAdjustmentsVisible = false;
            IsLeaveVisible = false;
            IsPayrollVisible = false;
            IsDocumentsVisible = false;
            IsBeneficiariesVisible = false;
        }

        public void ShowDashboard()
        {
            HideAllModules();
            IsDashboardVisible = true;
        }

        public void ShowTraining()
        {
            HideAllModules();
            IsTrainingVisible = true;
        }

        public void ShowRecruitment()
        {
            HideAllModules();
            IsRecruitmentVisible = true;
        }

        public void ShowPerformance()
        {
            HideAllModules();
            IsPerformanceVisible = true;
        }

        public void ShowUsers()
        {
            HideAllModules();
            IsUsersVisible = true;
        }

        public void ShowEmployees()
        {
            HideAllModules();
            IsEmployeesVisible = true;
        }

        public void ShowDepartments()
        {
            HideAllModules();
            IsDepartmentsVisible = true;
        }

        public void ShowAttendance()
        {
            HideAllModules();
            IsAttendanceVisible = true;
        }

        public void ShowAttendanceLogs()
        {
            HideAllModules();
            IsAttendanceLogsVisible = true;
        }

        public void ShowAdjustments()
        {
            HideAllModules();
            IsAdjustmentsVisible = true;
        }

        public void ShowLeave()
        {
            HideAllModules();
            IsLeaveVisible = true;
        }

        public void ShowPayroll()
        {
            HideAllModules();
            IsPayrollVisible = true;
        }

        public void ShowDocuments()
        {
            HideAllModules();
            IsDocumentsVisible = true;
        }

        public void ShowBeneficiaries()
        {
            HideAllModules();
            IsBeneficiariesVisible = true;
        }

        private async Task LoadStatsAsync()
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            try
            {
                if (IsEmployeeAccess && _authenticatedUser?.EmployeeId is int employeeId && employeeId > 0)
                {
                    Stats = await _dataService.GetEmployeeDashboardStatsAsync(employeeId);
                }
                else
                {
                    Stats = await _dataService.GetDashboardStatsAsync();
                }

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
