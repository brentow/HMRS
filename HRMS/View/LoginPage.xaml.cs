using HRMS.ViewModel;
using HRMS;
using HRMS.Model;
using MaterialDesignThemes.Wpf;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HRMS.View
{
    /// <summary>
    /// Interaction logic for LoginPage.xaml
    /// </summary>
    public partial class LoginPage : Window
    {
        private readonly LoginViewModel _viewModel;
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();

        public LoginPage()
        {
            InitializeComponent();

            _viewModel = new LoginViewModel();
            DataContext = _viewModel;

            _viewModel.LoginSucceeded += OnLoginSucceeded;
            _viewModel.LoginFailed += OnLoginFailed;
            _viewModel.LoginError += OnLoginError;
        }

        #region Theme & Window chrome
        public bool IsDarkTheme { get; set; }

        private void toggleTheme(object sender, RoutedEventArgs e)
        {
            var theme = _paletteHelper.GetTheme();

            if (IsDarkTheme = theme.GetBaseTheme() == BaseTheme.Dark)
            {
                IsDarkTheme = false;
                theme.SetBaseTheme(BaseTheme.Light);
            }
            else
            {
                IsDarkTheme = true;
                theme.SetBaseTheme(BaseTheme.Dark);
            }
            _paletteHelper.SetTheme(theme);
        }

        private void exitApp(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }

        private void UsernameTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PasswordBox.Focus();
                e.Handled = true;
            }
        }

        private void UsernameTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            ClearLoginError();
        }

        private void PasswordBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_viewModel.LoginCommand.CanExecute(null))
                {
                    _viewModel.LoginCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
        #endregion

        #region Password handling
        private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            // PasswordBox.Password cannot be bound directly, so we relay it to the ViewModel here.
            ClearLoginError();
            _viewModel.Password = PasswordBox.Password;
        }
        #endregion

        #region Login callbacks
        private void OnLoginSucceeded(object? sender, AuthenticatedUser user)
        {
            var dashboard = new DashboardWindow(user);
            Application.Current.MainWindow = dashboard;
            dashboard.Show();
            if (user.MustChangePassword)
            {
                MessageBox.Show(
                    "Your password was reset. Please change it now in Users & Roles > Profile.",
                    "Password Update Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            Close();
        }

        private void OnLoginFailed(object? sender, string message)
        {
            ShowLoginError(message);
        }

        private void OnLoginError(object? sender, string message)
        {
            ShowLoginError(message);
        }
        #endregion

        private void ShowLoginError(string message)
        {
            LoginErrorText.Text = message;
            LoginErrorText.Visibility = Visibility.Visible;
        }

        private void ClearLoginError()
        {
            LoginErrorText.Text = string.Empty;
            LoginErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
