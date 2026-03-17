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

        public LoginViewModel()
        {
            LoginCommand = new AsyncRelayCommand(_ => LoginAsync(), _ => CanLogin);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<AuthenticatedUser>? LoginSucceeded;
        public event EventHandler<string>? LoginFailed;
        public event EventHandler<string>? LoginError;

        public ICommand LoginCommand { get; }

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
