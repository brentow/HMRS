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

        private const string LocalHostDefault = "127.0.0.1";
        private const string LocalPortDefault = "3306";
        private const string LocalDbDefault = "hrms_db";
        private const string LocalUserDefault = "hrms_app";
        private const string LocalPasswordDefault = "15248130";

        private const string NetworkHostDefault = "192.168.137.108";
        private const string NetworkPortDefault = "3306";

        private const string RemoteHostDefault = "194.59.164.58";
        private const string RemotePortDefault = "3306";
        private const string RemoteDbDefault = "u621755393_hrms3b";
        private const string RemoteUserDefault = "u621755393_hrms3b_user";
        private const string RemotePasswordDefault = "Hrms3b@2026";

        private const string SulopLocalHostDefault = "127.0.0.1";
        private const string SulopLocalPortDefault = "3306";
        private const string SulopLocalDbDefault = "ggms_db";
        private const string SulopLocalUserDefault = "hrms_app";
        private const string SulopLocalPasswordDefault = "15248130";

        private const string SulopNetworkHostDefault = "192.168.137.108";
        private const string SulopNetworkPortDefault = "3306";
        private const string SulopNetworkDbDefault = "ggms_db";
        private const string SulopNetworkUserDefault = "root";
        private const string SulopNetworkPasswordDefault = "";

        private const string SulopRemoteHostDefault = "194.59.164.58";
        private const string SulopRemotePortDefault = "3306";
        private const string SulopRemoteDbDefault = "u621755393_ggms";
        private const string SulopRemoteUserDefault = "u621755393_ggms_user";
        private const string SulopRemotePasswordDefault = "Ggms@2026";

        private const string CrsLocalHostDefault = "127.0.0.1";
        private const string CrsLocalPortDefault = "3306";
        private const string CrsLocalDbDefault = "crs_db";
        private const string CrsLocalUserDefault = "hrms_app";
        private const string CrsLocalPasswordDefault = "15248130";

        private const string CrsNetworkHostDefault = "192.168.137.108";
        private const string CrsNetworkPortDefault = "3306";
        private const string CrsNetworkDbDefault = "crs_db";
        private const string CrsNetworkUserDefault = "root";
        private const string CrsNetworkPasswordDefault = "";

        private const string CrsRemoteHostDefault = "194.59.164.58";
        private const string CrsRemotePortDefault = "3306";
        private const string CrsRemoteDbDefault = "u621755393_crs";
        private const string CrsRemoteUserDefault = "u621755393_crs_user";
        private const string CrsRemotePasswordDefault = "Crs@2026";

        private readonly LoginViewModel _viewModel;
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();
        private readonly SetupOtpChallengeService _setupOtpChallengeService;
        private bool _ignoreDatabaseModeChange;

        public LoginPage()
        {
            InitializeComponent();

            _viewModel = new LoginViewModel();
            _setupOtpChallengeService = new SetupOtpChallengeService();
            DataContext = _viewModel;

            _viewModel.LoginSucceeded += OnLoginSucceeded;
            _viewModel.LoginFailed += OnLoginFailed;
            _viewModel.LoginError += OnLoginError;

            InitializeDatabaseForm();
            HookDatabaseFieldEvents();
        }

        private void LoginPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void LoginPage_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            var isLandscape = ActualWidth >= ActualHeight;

            IdentityPanel.Visibility = isLandscape ? Visibility.Visible : Visibility.Collapsed;
            IdentityColumn.Width = isLandscape ? new GridLength(1.02, GridUnitType.Star) : new GridLength(0);
            LoginColumn.Width = isLandscape ? new GridLength(0.98, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
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
            if (!_setupOtpChallengeService.HasVerifiedSetupAccess)
            {
                var otpWindow = new SetupOtpWindow(_setupOtpChallengeService)
                {
                    Owner = this
                };

                var verified = otpWindow.ShowDialog();
                if (verified != true)
                {
                    return;
                }
            }

            InitializeDatabaseForm();
            DialogHost.IsOpen = true;
            SetDbStatus("OTP verified. Database setup is temporarily unlocked.", DbSuccessBrush);
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
            CrsHostTextBox.TextChanged += DatabaseField_OnTextChanged;
            CrsPortTextBox.TextChanged += DatabaseField_OnTextChanged;
            CrsNameTextBox.TextChanged += DatabaseField_OnTextChanged;
            CrsUsernameTextBox.TextChanged += DatabaseField_OnTextChanged;
        }

        private void DatabaseField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDatabaseEndpointText();
        }

        private void InitializeDatabaseForm()
        {
            var settings = DbConfig.GetSettings();
            var ggmsSettings = GgmsConfig.GetSettings();
            var crsSettings = CrsConfig.GetSettings();
            DbHostTextBox.Text = settings.Host;
            DbPortTextBox.Text = settings.Port;
            DbNameTextBox.Text = settings.Database;
            DbUsernameTextBox.Text = settings.Username;
            DbPasswordTextBox.Text = settings.Password;

            SulopHostTextBox.Text = ggmsSettings.Host;
            SulopPortTextBox.Text = ggmsSettings.Port;
            SulopNameTextBox.Text = ggmsSettings.Database;
            SulopUsernameTextBox.Text = ggmsSettings.Username;
            SulopPasswordTextBox.Text = ggmsSettings.Password;

            CrsHostTextBox.Text = crsSettings.Host;
            CrsPortTextBox.Text = crsSettings.Port;
            CrsNameTextBox.Text = crsSettings.Database;
            CrsUsernameTextBox.Text = crsSettings.Username;
            CrsPasswordTextBox.Text = crsSettings.Password;

            var isLocal = string.Equals(settings.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(settings.Host, LocalHostDefault, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(settings.Host, "::1", StringComparison.OrdinalIgnoreCase);

            var isNetwork = string.Equals(settings.Host, NetworkHostDefault, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(ggmsSettings.Host, SulopNetworkHostDefault, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(crsSettings.Host, CrsNetworkHostDefault, StringComparison.OrdinalIgnoreCase);

            var isRemote = string.Equals(settings.Host, RemoteHostDefault, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(ggmsSettings.Host, SulopRemoteHostDefault, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(crsSettings.Host, CrsRemoteHostDefault, StringComparison.OrdinalIgnoreCase);

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
                        CrsHostTextBox.Text = CrsLocalHostDefault;
                        CrsPortTextBox.Text = CrsLocalPortDefault;
                        CrsNameTextBox.Text = CrsLocalDbDefault;
                        CrsUsernameTextBox.Text = CrsLocalUserDefault;
                        CrsPasswordTextBox.Text = CrsLocalPasswordDefault;
                        break;
                    case DatabaseMode.Network:
                        DbHostTextBox.Text = NetworkHostDefault;
                        DbPortTextBox.Text = NetworkPortDefault;
                        DbNameTextBox.Text = LocalDbDefault;
                        DbUsernameTextBox.Text = LocalUserDefault;
                        DbPasswordTextBox.Text = LocalPasswordDefault;
                        SulopHostTextBox.Text = SulopNetworkHostDefault;
                        SulopPortTextBox.Text = SulopNetworkPortDefault;
                        SulopNameTextBox.Text = SulopNetworkDbDefault;
                        SulopUsernameTextBox.Text = SulopNetworkUserDefault;
                        SulopPasswordTextBox.Text = SulopNetworkPasswordDefault;
                        CrsHostTextBox.Text = CrsNetworkHostDefault;
                        CrsPortTextBox.Text = CrsNetworkPortDefault;
                        CrsNameTextBox.Text = CrsNetworkDbDefault;
                        CrsUsernameTextBox.Text = CrsNetworkUserDefault;
                        CrsPasswordTextBox.Text = CrsNetworkPasswordDefault;
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
                        CrsHostTextBox.Text = CrsRemoteHostDefault;
                        CrsPortTextBox.Text = CrsRemotePortDefault;
                        CrsNameTextBox.Text = CrsRemoteDbDefault;
                        CrsUsernameTextBox.Text = CrsRemoteUserDefault;
                        CrsPasswordTextBox.Text = CrsRemotePasswordDefault;
                        break;
                }
            }

            DbModeHintText.Text = mode switch
            {
                DatabaseMode.Local => "Local preset: HRMS, GGMS, and CRS point to your local offline databases.",
                DatabaseMode.Network => "Network preset: all hosts point to LAN IP. Keep host fields editable for current demo server.",
                _ => "Remote preset: all hosts point to 194.59.164.58."
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
            var crsHost = CrsHostTextBox.Text?.Trim() ?? string.Empty;
            var crsPort = CrsPortTextBox.Text?.Trim() ?? string.Empty;
            var crsDb = CrsNameTextBox.Text?.Trim() ?? string.Empty;

            DbEndpointText.Text =
                $"HRMS: {hrmsHost}:{hrmsPort}/{hrmsDb}\n" +
                $"GGMS: {sulopHost}:{sulopPort}/{sulopDb}\n" +
                $"CRS: {crsHost}:{crsPort}/{crsDb}";
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

        private GgmsConnectionSettings BuildCurrentGgmsSettings() =>
            new()
            {
                Host = SulopHostTextBox.Text?.Trim() ?? string.Empty,
                Port = SulopPortTextBox.Text?.Trim() ?? string.Empty,
                Database = SulopNameTextBox.Text?.Trim() ?? string.Empty,
                Username = SulopUsernameTextBox.Text?.Trim() ?? string.Empty,
                Password = SulopPasswordTextBox.Text ?? string.Empty
            };

        private CrsConnectionSettings BuildCurrentCrsSettings() =>
            new()
            {
                Host = CrsHostTextBox.Text?.Trim() ?? string.Empty,
                Port = CrsPortTextBox.Text?.Trim() ?? string.Empty,
                Database = CrsNameTextBox.Text?.Trim() ?? string.Empty,
                Username = CrsUsernameTextBox.Text?.Trim() ?? string.Empty,
                Password = CrsPasswordTextBox.Text ?? string.Empty
            };

        private bool HasRequiredFields(bool requireDatabaseName)
        {
            var current = BuildCurrentConnectionSettings();
            return !string.IsNullOrWhiteSpace(current.Host)
                   && !string.IsNullOrWhiteSpace(current.Port)
                   && !string.IsNullOrWhiteSpace(current.Username)
                   && (!requireDatabaseName || !string.IsNullOrWhiteSpace(current.Database));
        }

        private bool HasGgmsRequiredFields(bool requireDatabaseName)
        {
            var current = BuildCurrentGgmsSettings();
            return !string.IsNullOrWhiteSpace(current.Host)
                   && !string.IsNullOrWhiteSpace(current.Port)
                   && !string.IsNullOrWhiteSpace(current.Username)
                   && (!requireDatabaseName || !string.IsNullOrWhiteSpace(current.Database));
        }

        private bool HasCrsRequiredFields(bool requireDatabaseName)
        {
            var current = BuildCurrentCrsSettings();
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
            if (!HasRequiredFields(requireDatabaseName: true)
                || !HasGgmsRequiredFields(requireDatabaseName: true)
                || !HasCrsRequiredFields(requireDatabaseName: true))
            {
                SetDbStatus("HRMS, GGMS, and CRS host/port/database/username are required.", DbErrorBrush);
                return;
            }

            try
            {
                var hrmsSettings = BuildCurrentConnectionSettings();
                var ggmsSettings = BuildCurrentGgmsSettings();
                var crsSettings = BuildCurrentCrsSettings();

                var hrmsOk = await TestConnectionAsync(DbConfig.BuildConnectionString(hrmsSettings));
                var ggmsOk = await TestConnectionAsync(GgmsConfig.BuildConnectionString(ggmsSettings));
                var crsOk = await TestConnectionAsync(CrsConfig.BuildConnectionString(crsSettings));

                if (hrmsOk && ggmsOk && crsOk)
                {
                    SetDbStatus("All connections passed (HRMS, GGMS, CRS).", DbSuccessBrush);
                }
                else
                {
                    SetDbStatus(
                        $"Test result: HRMS={(hrmsOk ? "OK" : "FAILED")}, GGMS={(ggmsOk ? "OK" : "FAILED")}, CRS={(crsOk ? "OK" : "FAILED")}",
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
            if (!_setupOtpChallengeService.HasVerifiedSetupAccess)
            {
                SetDbStatus("OTP verification expired. Request a new OTP before saving database setup changes.", DbErrorBrush);
                return;
            }

            if (!HasRequiredFields(requireDatabaseName: true)
                || !HasGgmsRequiredFields(requireDatabaseName: true)
                || !HasCrsRequiredFields(requireDatabaseName: true))
            {
                SetDbStatus("HRMS, GGMS, and CRS host/port/database/username are required.", DbErrorBrush);
                return;
            }

            try
            {
                DbConfig.SaveSettings(BuildCurrentConnectionSettings());
                GgmsConfig.SaveSettings(BuildCurrentGgmsSettings());
                CrsConfig.SaveSettings(BuildCurrentCrsSettings());
                SetDbStatus("Settings saved for HRMS, GGMS, and CRS. Testing all...", DbInfoBrush);
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
