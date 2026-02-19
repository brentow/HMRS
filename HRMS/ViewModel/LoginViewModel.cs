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
        public event EventHandler? LoginSucceeded;
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
                var isValid = await ValidateCredentialsAsync(Username, Password);

                if (isValid)
                {
                    LoginSucceeded?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    LoginFailed?.Invoke(this, "Invalid username or password.");
                }
            }
            catch (Exception ex)
            {
                LoginError?.Invoke(this, $"Unexpected error: {ex.Message}");
            }
        }

        private static async Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            await Task.CompletedTask;
            return !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
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
