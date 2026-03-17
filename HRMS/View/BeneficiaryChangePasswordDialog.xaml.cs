using HRMS.ViewModel;
using System;
using System.Windows;

namespace HRMS.View
{
    /// <summary>
    /// Interaction logic for BeneficiaryChangePasswordDialog.xaml
    /// </summary>
    public partial class BeneficiaryChangePasswordDialog : Window
    {
        private readonly BeneficiaryChangePasswordViewModel _viewModel;

        public BeneficiaryChangePasswordDialog(int userId, string temporaryPassword)
        {
            InitializeComponent();

            _viewModel = new BeneficiaryChangePasswordViewModel(userId);
            DataContext = _viewModel;

            TemporaryPasswordText.Text = temporaryPassword;

            _viewModel.PasswordChangeSucceeded += (s, e) => 
            { 
                DialogResult = true;
                Close();
            };
            _viewModel.PasswordChangeCancelled += (s, e) => 
            { 
                DialogResult = false;
                Close();
            };

            NewPasswordBox.PasswordChanged += (s, e) =>
            {
                _viewModel.NewPassword = NewPasswordBox.Password ?? string.Empty;
            };

            ConfirmPasswordBox.PasswordChanged += (s, e) =>
            {
                _viewModel.ConfirmPassword = ConfirmPasswordBox.Password ?? string.Empty;
            };

            ChangePasswordButton.Click += (s, e) =>
            {
                if (_viewModel.ChangePasswordCommand.CanExecute(null))
                {
                    _viewModel.ChangePasswordCommand.Execute(null);
                }
            };

            CancelButton.Click += (s, e) => _viewModel.CancelPasswordChange();
        }
    }
}
