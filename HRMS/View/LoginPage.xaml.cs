using HRMS.ViewModel;
using HRMS;
using HRMS.Model;
using MaterialDesignThemes.Wpf;
using MySqlConnector;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.View
{
    /// <summary>
    /// Interaction logic for LoginPage.xaml
    /// </summary>
    public partial class LoginPage : Window
    {
        private enum DatabaseMode
        {
            Local,
            Network,
            Remote
        }

        private static readonly Brush DbInfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5B6C"));
        private static readonly Brush DbSuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush DbErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private const string LocalHostDefault = "localhost";
        private const string LocalPortDefault = "3306";
        private const string LocalDbDefault = "u621755393_hrms3b";
        private const string LocalUserDefault = "root";
        private const string LocalPasswordDefault = "";

        private const string NetworkHostDefault = "192.168.137.108";
        private const string NetworkPortDefault = "3306";

        private const string RemoteHostDefault = "srv1237.hstgr.io";
        private const string RemotePortDefault = "3306";
        private const string RemoteDbDefault = "u621755393_hrms3b";
        private const string RemoteUserDefault = "u621755393_hrms3b_user";
        private const string RemotePasswordDefault = "Hrms3b@2026";

        private const string SulopLocalHostDefault = "localhost";
        private const string SulopLocalPortDefault = "3306";
        private const string SulopLocalDbDefault = "sulop";
        private const string SulopLocalUserDefault = "root";
        private const string SulopLocalPasswordDefault = "";

        private const string SulopNetworkHostDefault = "192.168.137.108";
        private const string SulopNetworkPortDefault = "3306";
        private const string SulopNetworkDbDefault = "sulop";
        private const string SulopNetworkUserDefault = "root";
        private const string SulopNetworkPasswordDefault = "";

        private const string SulopRemoteHostDefault = "srv1237.hstgr.io";
        private const string SulopRemotePortDefault = "3306";
        private const string SulopRemoteDbDefault = "u621755393_sulop";
        private const string SulopRemoteUserDefault = "u621755393_sulop_user";
        private const string SulopRemotePasswordDefault = "Sulop@2026";

        private readonly LoginViewModel _viewModel;
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();
        private bool _ignoreDatabaseModeChange;

        public LoginPage()
        {
            InitializeComponent();

            _viewModel = new LoginViewModel();
            DataContext = _viewModel;

            _viewModel.LoginSucceeded += OnLoginSucceeded;
            _viewModel.LoginFailed += OnLoginFailed;
            _viewModel.LoginError += OnLoginError;

            InitializeDatabaseForm();
            HookDatabaseFieldEvents();
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
            // For beneficiaries with temporary passwords, show password change dialog before dashboard
            if (user.MustChangePassword && user.RoleName == "Beneficiary")
            {
                var changePasswordDialog = new BeneficiaryChangePasswordDialog(user.UserId, "");
                var result = changePasswordDialog.ShowDialog();

                if (result != true)
                {
                    // User cancelled password change
                    MessageBox.Show(
                        "Password change is required to proceed. Please try logging in again.",
                        "Password Change Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            var dashboard = new DashboardWindow(user);
            Application.Current.MainWindow = dashboard;
            dashboard.Show();

            // For non-beneficiary users with reset passwords, show message box
            if (user.MustChangePassword && user.RoleName != "Beneficiary")
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

        private void RequestAccessButton_OnClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Account registration is managed by the HR Administrator.\n\n" +
                "Please contact your HR/Admin office to request access and role assignment.",
                "Request Access",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OpenDatabaseDialogButton_OnClick(object sender, RoutedEventArgs e)
        {
            InitializeDatabaseForm();
            DialogHost.IsOpen = true;
        }

        private void CloseDbDialogButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogHost.IsOpen = false;
        }

        private void HookDatabaseFieldEvents()
        {
            DbHostTextBox.TextChanged += DatabaseField_OnTextChanged;
            DbPortTextBox.TextChanged += DatabaseField_OnTextChanged;
            DbNameTextBox.TextChanged += DatabaseField_OnTextChanged;
            DbUsernameTextBox.TextChanged += DatabaseField_OnTextChanged;
            SulopHostTextBox.TextChanged += DatabaseField_OnTextChanged;
            SulopPortTextBox.TextChanged += DatabaseField_OnTextChanged;
            SulopNameTextBox.TextChanged += DatabaseField_OnTextChanged;
            SulopUsernameTextBox.TextChanged += DatabaseField_OnTextChanged;
        }

        private void DatabaseField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDatabaseEndpointText();
        }

        private void InitializeDatabaseForm()
        {
            var settings = DbConfig.GetSettings();
            var sulopSettings = SulopConfig.GetSettings();
            DbHostTextBox.Text = settings.Host;
            DbPortTextBox.Text = settings.Port;
            DbNameTextBox.Text = settings.Database;
            DbUsernameTextBox.Text = settings.Username;
            DbPasswordTextBox.Text = settings.Password;

            SulopHostTextBox.Text = sulopSettings.Host;
            SulopPortTextBox.Text = sulopSettings.Port;
            SulopNameTextBox.Text = sulopSettings.Database;
            SulopUsernameTextBox.Text = sulopSettings.Username;
            SulopPasswordTextBox.Text = sulopSettings.Password;

            var isLocal = string.Equals(settings.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(settings.Host, LocalHostDefault, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(settings.Host, "::1", StringComparison.OrdinalIgnoreCase);

            var isNetwork = string.Equals(settings.Host, NetworkHostDefault, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(sulopSettings.Host, SulopNetworkHostDefault, StringComparison.OrdinalIgnoreCase);

            var isRemote = string.Equals(settings.Host, RemoteHostDefault, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(sulopSettings.Host, SulopRemoteHostDefault, StringComparison.OrdinalIgnoreCase);

            if (isLocal)
            {
                SetDatabaseMode(DatabaseMode.Local, applyDefaults: false);
            }
            else if (isRemote)
            {
                SetDatabaseMode(DatabaseMode.Remote, applyDefaults: false);
            }
            else if (isNetwork)
            {
                SetDatabaseMode(DatabaseMode.Network, applyDefaults: false);
            }
            else
            {
                SetDatabaseMode(DatabaseMode.Remote, applyDefaults: false);
            }

            SetDbStatus("Not tested", DbInfoBrush);
            UpdateDatabaseEndpointText();
        }

        private void DatabaseModeRadio_OnChecked(object sender, RoutedEventArgs e)
        {
            if (_ignoreDatabaseModeChange)
            {
                return;
            }

            if (ReferenceEquals(sender, LocalDbRadio))
            {
                SetDatabaseMode(DatabaseMode.Local, applyDefaults: true);
            }
            else if (ReferenceEquals(sender, NetworkDbRadio))
            {
                SetDatabaseMode(DatabaseMode.Network, applyDefaults: true);
            }
            else if (ReferenceEquals(sender, RemoteDbRadio))
            {
                SetDatabaseMode(DatabaseMode.Remote, applyDefaults: true);
            }
        }

        private void SetDatabaseMode(DatabaseMode mode, bool applyDefaults)
        {
            _ignoreDatabaseModeChange = true;
            LocalDbRadio.IsChecked = mode == DatabaseMode.Local;
            NetworkDbRadio.IsChecked = mode == DatabaseMode.Network;
            RemoteDbRadio.IsChecked = mode == DatabaseMode.Remote;
            _ignoreDatabaseModeChange = false;

            if (applyDefaults)
            {
                switch (mode)
                {
                    case DatabaseMode.Local:
                        DbHostTextBox.Text = LocalHostDefault;
                        DbPortTextBox.Text = LocalPortDefault;
                        DbNameTextBox.Text = LocalDbDefault;
                        DbUsernameTextBox.Text = LocalUserDefault;
                        DbPasswordTextBox.Text = LocalPasswordDefault;
                        SulopHostTextBox.Text = SulopLocalHostDefault;
                        SulopPortTextBox.Text = SulopLocalPortDefault;
                        SulopNameTextBox.Text = SulopLocalDbDefault;
                        SulopUsernameTextBox.Text = SulopLocalUserDefault;
                        SulopPasswordTextBox.Text = SulopLocalPasswordDefault;
                        break;
                    case DatabaseMode.Network:
                        DbHostTextBox.Text = NetworkHostDefault;
                        DbPortTextBox.Text = NetworkPortDefault;
                        SulopHostTextBox.Text = SulopNetworkHostDefault;
                        SulopPortTextBox.Text = SulopNetworkPortDefault;
                        SulopNameTextBox.Text = SulopNetworkDbDefault;
                        SulopUsernameTextBox.Text = SulopNetworkUserDefault;
                        SulopPasswordTextBox.Text = SulopNetworkPasswordDefault;
                        break;
                    case DatabaseMode.Remote:
                        DbHostTextBox.Text = RemoteHostDefault;
                        DbPortTextBox.Text = RemotePortDefault;
                        DbNameTextBox.Text = RemoteDbDefault;
                        DbUsernameTextBox.Text = RemoteUserDefault;
                        DbPasswordTextBox.Text = RemotePasswordDefault;
                        SulopHostTextBox.Text = SulopRemoteHostDefault;
                        SulopPortTextBox.Text = SulopRemotePortDefault;
                        SulopNameTextBox.Text = SulopRemoteDbDefault;
                        SulopUsernameTextBox.Text = SulopRemoteUserDefault;
                        SulopPasswordTextBox.Text = SulopRemotePasswordDefault;
                        break;
                }
            }

            DbModeHintText.Text = mode switch
            {
                DatabaseMode.Local => "Local preset: both HRMS and Sulop point to localhost.",
                DatabaseMode.Network => "Network preset: both hosts point to LAN IP. Keep host editable for current demo server.",
                _ => "Remote preset: both hosts point to Hostinger."
            };

            UpdateDatabaseEndpointText();
        }

        private void UpdateDatabaseEndpointText()
        {
            var hrmsHost = DbHostTextBox.Text?.Trim() ?? string.Empty;
            var hrmsPort = DbPortTextBox.Text?.Trim() ?? string.Empty;
            var hrmsDb = DbNameTextBox.Text?.Trim() ?? string.Empty;

            var sulopHost = SulopHostTextBox.Text?.Trim() ?? string.Empty;
            var sulopPort = SulopPortTextBox.Text?.Trim() ?? string.Empty;
            var sulopDb = SulopNameTextBox.Text?.Trim() ?? string.Empty;

            DbEndpointText.Text =
                $"HRMS: {hrmsHost}:{hrmsPort}/{hrmsDb}\n" +
                $"Sulop: {sulopHost}:{sulopPort}/{sulopDb}";
        }

        private DbConnectionSettings BuildCurrentConnectionSettings() =>
            new()
            {
                Host = DbHostTextBox.Text?.Trim() ?? string.Empty,
                Port = DbPortTextBox.Text?.Trim() ?? string.Empty,
                Database = DbNameTextBox.Text?.Trim() ?? string.Empty,
                Username = DbUsernameTextBox.Text?.Trim() ?? string.Empty,
                Password = DbPasswordTextBox.Text ?? string.Empty
            };

        private SulopConnectionSettings BuildCurrentSulopSettings() =>
            new()
            {
                Host = SulopHostTextBox.Text?.Trim() ?? string.Empty,
                Port = SulopPortTextBox.Text?.Trim() ?? string.Empty,
                Database = SulopNameTextBox.Text?.Trim() ?? string.Empty,
                Username = SulopUsernameTextBox.Text?.Trim() ?? string.Empty,
                Password = SulopPasswordTextBox.Text ?? string.Empty
            };

        private bool HasRequiredFields(bool requireDatabaseName)
        {
            var current = BuildCurrentConnectionSettings();
            return !string.IsNullOrWhiteSpace(current.Host)
                   && !string.IsNullOrWhiteSpace(current.Port)
                   && !string.IsNullOrWhiteSpace(current.Username)
                   && (!requireDatabaseName || !string.IsNullOrWhiteSpace(current.Database));
        }

        private bool HasSulopRequiredFields(bool requireDatabaseName)
        {
            var current = BuildCurrentSulopSettings();
            return !string.IsNullOrWhiteSpace(current.Host)
                   && !string.IsNullOrWhiteSpace(current.Port)
                   && !string.IsNullOrWhiteSpace(current.Username)
                   && (!requireDatabaseName || !string.IsNullOrWhiteSpace(current.Database));
        }

        private void SetDbStatus(string text, Brush brush)
        {
            DbStatusText.Text = text;
            DbStatusText.Foreground = brush;
        }

        private async void TestDatabaseConnectionButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!HasRequiredFields(requireDatabaseName: true) || !HasSulopRequiredFields(requireDatabaseName: true))
            {
                SetDbStatus("HRMS and Sulop host, port, database, and username are required.", DbErrorBrush);
                return;
            }

            try
            {
                var hrmsSettings = BuildCurrentConnectionSettings();
                var sulopSettings = BuildCurrentSulopSettings();

                var hrmsOk = await TestConnectionAsync(DbConfig.BuildConnectionString(hrmsSettings));
                var sulopOk = await TestConnectionAsync(SulopConfig.BuildConnectionString(sulopSettings));

                if (hrmsOk && sulopOk)
                {
                    SetDbStatus("Both connections passed (HRMS and Sulop).", DbSuccessBrush);
                }
                else
                {
                    SetDbStatus(
                        $"Test failed: HRMS={(hrmsOk ? "OK" : "FAILED")}, Sulop={(sulopOk ? "OK" : "FAILED")}",
                        DbErrorBrush);
                }
            }
            catch (Exception ex)
            {
                SetDbStatus($"Connection failed: {ex.Message}", DbErrorBrush);
            }
        }

        private static async Task<bool> TestConnectionAsync(string connectionString)
        {
            try
            {
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                await connection.CloseAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async void CreateDatabaseButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!HasRequiredFields(requireDatabaseName: true))
            {
                SetDbStatus("Host, port, username, and database are required.", DbErrorBrush);
                return;
            }

            try
            {
                var settings = BuildCurrentConnectionSettings();
                var safeDbName = settings.Database.Replace("`", "``");
                var serverBuilder = new MySqlConnectionStringBuilder(DbConfig.BuildConnectionString(settings))
                {
                    Database = string.Empty
                };

                await using var connection = new MySqlConnection(serverBuilder.ConnectionString);
                await connection.OpenAsync();

                var exists = false;
                await using (var existsCommand = new MySqlCommand(
                                 "SELECT COUNT(*) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @dbName;",
                                 connection))
                {
                    existsCommand.Parameters.AddWithValue("@dbName", settings.Database);
                    var countObj = await existsCommand.ExecuteScalarAsync();
                    exists = Convert.ToInt32(countObj ?? 0) > 0;
                }

                if (!exists)
                {
                    var createSql =
                        $"CREATE DATABASE IF NOT EXISTS `{safeDbName}` DEFAULT CHARACTER SET utf8mb4 DEFAULT COLLATE utf8mb4_unicode_ci;";
                    await using var command = new MySqlCommand(createSql, connection);
                    await command.ExecuteNonQueryAsync();
                }

                SetDbStatus(
                    exists
                        ? $"Database '{settings.Database}' already exists."
                        : $"Database '{settings.Database}' created successfully.",
                    exists ? DbInfoBrush : DbSuccessBrush);
            }
            catch (Exception ex)
            {
                SetDbStatus($"Create database failed: {ex.Message}", DbErrorBrush);
            }
        }

        private async void SaveDatabaseSettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!HasRequiredFields(requireDatabaseName: true) || !HasSulopRequiredFields(requireDatabaseName: true))
            {
                SetDbStatus("HRMS and Sulop host, port, database, and username are required.", DbErrorBrush);
                return;
            }

            try
            {
                DbConfig.SaveSettings(BuildCurrentConnectionSettings());
                SulopConfig.SaveSettings(BuildCurrentSulopSettings());
                SetDbStatus("Settings saved for HRMS and Sulop. Testing both...", DbInfoBrush);
                await Task.Delay(100);
                TestDatabaseConnectionButton_OnClick(sender, e);
            }
            catch (Exception ex)
            {
                SetDbStatus($"Save settings failed: {ex.Message}", DbErrorBrush);
            }
        }
    }
}
