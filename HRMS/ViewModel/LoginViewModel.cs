using HRMS.Model;
using MySqlConnector;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HRMS.ViewModel
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private readonly CompanyProfileDataService _companyProfileDataService;

        private string _companyName = CompanyProfile.Default.CompanyName;
        private string _companyAddress = CompanyProfile.Default.Address;
        private string _companyOwner = CompanyProfile.Default.OwnerName;
        private string _serialNumber = CompanyProfile.Default.SerialNumber;
        private string _companyLogoPath = CompanyProfile.Default.LogoPath;

        public LoginViewModel()
        {
            LoginCommand = new AsyncRelayCommand(_ => LoginAsync(), _ => CanLogin);
            _companyProfileDataService = new CompanyProfileDataService(DbConfig.ConnectionString);
            _ = LoadCompanyProfileAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<AuthenticatedUser>? LoginSucceeded;
        public event EventHandler<string>? LoginFailed;
        public event EventHandler<string>? LoginError;

        public ICommand LoginCommand { get; }

        public string CompanyName
        {
            get => _companyName;
            private set
            {
                if (_companyName != value)
                {
                    _companyName = value;
                    OnPropertyChanged();
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

        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged();
                    RaiseCanExecute();
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged();
                    RaiseCanExecute();
                }
            }
        }

        private bool CanLogin => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

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

        private async Task LoginAsync()
        {
            try
            {
                var loginDataService = new LoginDataService(DbConfig.ConnectionString);
                var result = await loginDataService.ValidateCredentialsAsync(Username.Trim(), Password);
                switch (result.Status)
                {
                    case LoginStatus.Success:
                        if (result.User == null)
                        {
                            LoginError?.Invoke(this, "Login succeeded but user profile was not loaded.");
                            return;
                        }

                        LoginSucceeded?.Invoke(this, result.User);
                        break;
                    case LoginStatus.Inactive:
                        LoginFailed?.Invoke(this, "This account is inactive. Please contact HR admin.");
                        break;
                    case LoginStatus.Locked:
                        LoginFailed?.Invoke(this, "This account is locked. Please contact HR admin.");
                        break;
                    default:
                        LoginFailed?.Invoke(this, "Invalid username or password.");
                        break;
                }
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1045 || ex.Message.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
                {
                    LoginError?.Invoke(this, "Database access denied. Set correct DB username/password in DbConfig or HRMS_DB_* environment variables.");
                    return;
                }

                LoginError?.Invoke(this, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoginError?.Invoke(this, $"Unexpected error: {ex.Message}");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void RaiseCanExecute()
        {
            if (LoginCommand is AsyncRelayCommand cmd)
            {
                cmd.RaiseCanExecuteChanged();
            }
        }
    }
}
