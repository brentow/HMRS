using HRMS.Model;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public class BeneficiaryChangePasswordViewModel : INotifyPropertyChanged
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5B6C"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private readonly LoginDataService _loginDataService = new(DbConfig.ConnectionString);
        private readonly int _userId;

        private bool _isBusy;
        private string _messageText = "Minimum 8 characters required.";
        private Brush _messageBrush = InfoBrush;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;

        public ICommand ChangePasswordCommand { get; }

        public int UserId => _userId;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public string MessageText
        {
            get => _messageText;
            private set
            {
                if (_messageText == value) return;
                _messageText = value;
                OnPropertyChanged();
            }
        }

        public Brush MessageBrush
        {
            get => _messageBrush;
            private set
            {
                if (_messageBrush == value) return;
                _messageBrush = value;
                OnPropertyChanged();
            }
        }

        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (_newPassword == value) return;
                _newPassword = value;
                OnPropertyChanged();
            }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (_confirmPassword == value) return;
                _confirmPassword = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Event raised when password change succeeds.
        /// </summary>
        public event EventHandler? PasswordChangeSucceeded;

        /// <summary>
        /// Event raised when user cancels the password change.
        /// </summary>
        public event EventHandler? PasswordChangeCancelled;

        public BeneficiaryChangePasswordViewModel(int userId)
        {
            _userId = userId;
            ChangePasswordCommand = new AsyncRelayCommand(_ => ChangePasswordAsync());
        }

        private async Task ChangePasswordAsync()
        {
            if (IsBusy)
            {
                return;
            }

            // Validate inputs
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                SetMessage("New password cannot be empty.", ErrorBrush);
                return;
            }

            if (string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                SetMessage("Please confirm your new password.", ErrorBrush);
                return;
            }

            if (NewPassword.Length < 8)
            {
                SetMessage("Password must be at least 8 characters long.", ErrorBrush);
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                SetMessage("Passwords do not match.", ErrorBrush);
                return;
            }

            IsBusy = true;
            SetMessage("Changing password...", InfoBrush);

            try
            {
                await _loginDataService.ChangePasswordAsync(_userId, NewPassword.Trim());
                SetMessage("✓ Password changed successfully!", SuccessBrush);
                
                // Raise success event after a brief delay to show the success message
                await Task.Delay(800);
                PasswordChangeSucceeded?.Invoke(this, EventArgs.Empty);
            }
            catch (InvalidOperationException ex)
            {
                SetMessage($"Error: {ex.Message}", ErrorBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Failed to change password: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void CancelPasswordChange()
        {
            PasswordChangeCancelled?.Invoke(this, EventArgs.Empty);
        }

        private void SetMessage(string message, Brush brush)
        {
            MessageText = message;
            MessageBrush = brush;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
