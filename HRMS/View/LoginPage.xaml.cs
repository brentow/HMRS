using HRMS.ViewModel;
using HRMS;
using MaterialDesignThemes.Wpf;
using System;
using System.Windows;
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
            _viewModel.Password = PasswordBox.Password;
        }
        #endregion

        #region Login callbacks
        private void OnLoginSucceeded(object? sender, EventArgs e)
        {
            MessageBox.Show("Login successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            var dashboard = new DashboardWindow();
            Application.Current.MainWindow = dashboard;
            dashboard.Show();
            Close();
        }

        private void OnLoginFailed(object? sender, string message)
        {
            MessageBox.Show(message, "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void OnLoginError(object? sender, string message)
        {
            MessageBox.Show(message, "Connection error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        #endregion
    }
}
