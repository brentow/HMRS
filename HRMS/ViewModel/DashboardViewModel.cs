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
        private readonly CompanyProfileDataService _companyProfileDataService;
        private readonly AuthenticatedUser? _authenticatedUser;
        private DashboardStats _stats = new DashboardStats();
        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private DateTime _lastUpdated;
        private string _companyName = CompanyProfile.Default.CompanyName;
        private string _companyAddress = CompanyProfile.Default.Address;
        private string _companyOwner = CompanyProfile.Default.OwnerName;
        private string _serialNumber = CompanyProfile.Default.SerialNumber;
        private string _companyLogoPath = CompanyProfile.Default.LogoPath;
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
        private bool _isTransactionsVisible = false;
        private bool _isReportsVisible = false;
        private bool _isDocumentsVisible = false;
        private bool _isDocumentVerificationVisible = false;
        private bool _isBeneficiariesVisible = false;

        public DashboardViewModel(AuthenticatedUser? authenticatedUser = null)
        {
            _authenticatedUser = authenticatedUser;
            _dataService = new DashboardDataService(DbConfig.ConnectionString);
            _companyProfileDataService = new CompanyProfileDataService(DbConfig.ConnectionString);
            RefreshCommand = new AsyncRelayCommand(_ => LoadStatsAsync());

            _ = LoadCompanyProfileAsync();

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

        public string CompanyName
        {
            get => _companyName;
            private set
            {
                if (_companyName != value)
                {
                    _companyName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CompanyShortName));
                    OnPropertyChanged(nameof(CompanyAbbreviation));
                }
            }
        }

        public string CompanyAddress
        {
            get => _companyAddress;
            private set
            {
                if (_companyAddress != value)
                {
                    _companyAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CompanyOwner
        {
            get => _companyOwner;
            private set
            {
                if (_companyOwner != value)
                {
                    _companyOwner = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SerialNumber
        {
            get => _serialNumber;
            private set
            {
                if (_serialNumber != value)
                {
                    _serialNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CompanyLogoPath
        {
            get => _companyLogoPath;
            private set
            {
                if (_companyLogoPath != value)
                {
                    _companyLogoPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CompanyShortName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CompanyName))
                {
                    return "HRMS";
                }

                return CompanyName.Length <= 18
                    ? CompanyName
                    : CompanyName.Substring(0, 18) + "...";
            }
        }

        public string CompanyAbbreviation
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CompanyName))
                {
                    return "HRMS";
                }

                var parts = CompanyName
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length <= 1)
                {
                    return parts[0].Length <= 6
                        ? parts[0].ToUpperInvariant()
                        : parts[0].Substring(0, 6).ToUpperInvariant();
                }

                var abbreviation = string.Concat(parts.Select(part => char.ToUpperInvariant(part[0])));
                return abbreviation.Length <= 6 ? abbreviation : abbreviation.Substring(0, 6);
            }
        }

        public string SnapshotTitle => ShowEmployeeDashboard ? "My Workday Snapshot" : "Organization Snapshot";

        public string SnapshotSubtitle => ShowEmployeeDashboard
            ? "Your attendance, leave, payroll, and development status"
            : "Live HR operations and approval workload";

        public string CurrentUserDisplay =>
            string.IsNullOrWhiteSpace(_authenticatedUser?.FullName)
                ? string.IsNullOrWhiteSpace(_authenticatedUser?.Username) ? "System User" : _authenticatedUser!.Username.Trim()
                : _authenticatedUser!.FullName.Trim();

        public string CurrentRoleDisplay =>
            string.IsNullOrWhiteSpace(_authenticatedUser?.RoleName)
                ? "User"
                : _authenticatedUser!.RoleName.Trim();

        public string TodayDayDisplay => DateTime.Now.ToString("dddd");

        public string TodayDateDisplay => DateTime.Now.ToString("MMMM dd, yyyy");

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

        public bool IsTransactionsVisible
        {
            get => _isTransactionsVisible;
            private set { if (_isTransactionsVisible != value) { _isTransactionsVisible = value; OnPropertyChanged(); } }
        }

        public bool IsReportsVisible
        {
            get => _isReportsVisible;
            private set { if (_isReportsVisible != value) { _isReportsVisible = value; OnPropertyChanged(); } }
        }

        public bool IsDocumentsVisible
        {
            get => _isDocumentsVisible;
            private set { if (_isDocumentsVisible != value) { _isDocumentsVisible = value; OnPropertyChanged(); } }
        }

        public bool IsDocumentVerificationVisible
        {
            get => _isDocumentVerificationVisible;
            private set { if (_isDocumentVerificationVisible != value) { _isDocumentVerificationVisible = value; OnPropertyChanged(); } }
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
            IsTransactionsVisible = false;
            IsReportsVisible = false;
            IsDocumentsVisible = false;
            IsDocumentVerificationVisible = false;
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

        public void ShowTransactions()
        {
            HideAllModules();
            IsTransactionsVisible = true;
        }

        public void ShowReports()
        {
            HideAllModules();
            IsReportsVisible = true;
        }

        public void ShowDocuments()
        {
            HideAllModules();
            IsDocumentsVisible = true;
        }

        public void ShowDocumentVerification()
        {
            HideAllModules();
            IsDocumentVerificationVisible = true;
        }

        public void ShowBeneficiaries()
        {
            HideAllModules();
            IsBeneficiariesVisible = true;
        }

        private async Task LoadCompanyProfileAsync()
        {
            var profile = await _companyProfileDataService.GetCompanyProfileAsync();
            CompanyName = profile.CompanyName;
            CompanyAddress = profile.Address;
            CompanyOwner = profile.OwnerName;
            SerialNumber = profile.SerialNumber;
            CompanyLogoPath = NormalizeLogoPath(profile.LogoPath);
        }

        private static string NormalizeLogoPath(string? rawPath)
        {
            const string packagedLogoPath = "/Images/ePRIME_logo.png";

            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return packagedLogoPath;
            }

            var path = rawPath.Trim().Replace('\\', '/');
            if (path.Equals("HRMS/Images/ePRIME_logo.png", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("HRMS/Images/ERPMS_logo.png", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("HRMS/Images/HRMS_logo_cropped.png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/Images/ePRIME_logo.png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("Images/ePRIME_logo.png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/Images/ERPMS_logo.png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("Images/ERPMS_logo.png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/Images/HRMS_logo_cropped.png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("Images/HRMS_logo_cropped.png", StringComparison.OrdinalIgnoreCase))
            {
                return packagedLogoPath;
            }

            return path;
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
