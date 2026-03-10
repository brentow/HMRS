using HRMS.Model;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.RegularExpressions;

namespace HRMS.ViewModel
{
    public class UsersRolesViewModel : INotifyPropertyChanged
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5B6C"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private readonly UsersRolesDataService _dataService = new(DbConfig.ConnectionString);
        private readonly List<UserAccessRow> _allUsers = new();

        private bool _isBusy;
        private string _searchText = string.Empty;
        private string _selectedStatusFilter = "All";
        private string _actionMessage = "Ready";
        private Brush _actionMessageBrush = InfoBrush;

        private int _totalUsers;
        private int _activeUsers;
        private int _lockedUsers;
        private int _inactiveUsers;
        private int _adminUsers;
        private int _totalRoles;
        private int _totalPermissions;
        private int _currentUserId;
        private string _currentUsername = "-";
        private string _currentFullName = "Current User";
        private string _currentRole = "-";
        private string _currentStatus = "Inactive";
        private string _profileUsername = string.Empty;
        private string _profileFullName = string.Empty;
        private string _profileEmail = string.Empty;
        private string _profileRole = string.Empty;
        private string _profileStatus = string.Empty;
        private string _profileLastLogin = "Never";
        private string _profileNewPassword = string.Empty;
        private string _profileConfirmPassword = string.Empty;
        private string _addUserUsername = string.Empty;
        private string _addUserFullName = string.Empty;
        private string _addUserEmail = string.Empty;
        private string _addUserPassword = string.Empty;
        private string _addUserConfirmPassword = string.Empty;
        private string _addUserRole = string.Empty;

        private bool _useLocalDatabase;
        private bool _useRemoteDatabase;
        private string _dbHost = "127.0.0.1";
        private string _dbPort = "3306";
        private string _dbName = "hrms_db";
        private string _dbUsername = "root";
        private string _dbPassword = string.Empty;
        private string _dbConnectionStatus = "Not tested";
        private Brush _dbConnectionStatusBrush = InfoBrush;
        private string _storageLocation = string.Empty;
        private string _tempFolderPath = string.Empty;
        private string _tempFilesSizeText = "0.00 MB";
        private bool _confirmResetAndSeed;

        public ObservableCollection<UserAccessRow> Users { get; } = new();
        public ObservableCollection<string> Roles { get; } = new();
        public ObservableCollection<string> AddUserRoles { get; } = new();
        public ObservableCollection<string> StatusFilters { get; } = new() { "All", "ACTIVE", "INACTIVE", "LOCKED" };
        public ObservableCollection<string> StatusOptions { get; } = new() { "ACTIVE", "INACTIVE", "LOCKED" };
        public ObservableCollection<RoleSummaryRow> RoleSummaries { get; } = new();
        public ObservableCollection<PermissionSummaryRow> PermissionSummaries { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand AddUserCommand { get; }
        public ICommand SaveUserCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand TestDbConnectionCommand { get; }
        public ICommand CreateDatabaseCommand { get; }
        public ICommand SaveDbConnectionCommand { get; }
        public ICommand RefreshSystemInfoCommand { get; }
        public ICommand OpenStorageFolderCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand ClearTempFilesCommand { get; }
        public ICommand SeedDatabaseCommand { get; }
        public ICommand ResetAndSeedCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                {
                    return;
                }

                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _selectedStatusFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string ActionMessage
        {
            get => _actionMessage;
            private set
            {
                if (_actionMessage == value)
                {
                    return;
                }

                _actionMessage = value;
                OnPropertyChanged();
            }
        }

        public Brush ActionMessageBrush
        {
            get => _actionMessageBrush;
            private set
            {
                if (_actionMessageBrush == value)
                {
                    return;
                }

                _actionMessageBrush = value;
                OnPropertyChanged();
            }
        }

        public int TotalUsers { get => _totalUsers; private set { _totalUsers = value; OnPropertyChanged(); } }
        public int ActiveUsers { get => _activeUsers; private set { _activeUsers = value; OnPropertyChanged(); } }
        public int LockedUsers { get => _lockedUsers; private set { _lockedUsers = value; OnPropertyChanged(); } }
        public int InactiveUsers { get => _inactiveUsers; private set { _inactiveUsers = value; OnPropertyChanged(); } }
        public int AdminUsers { get => _adminUsers; private set { _adminUsers = value; OnPropertyChanged(); } }
        public int TotalRoles { get => _totalRoles; private set { _totalRoles = value; OnPropertyChanged(); } }
        public int TotalPermissions { get => _totalPermissions; private set { _totalPermissions = value; OnPropertyChanged(); } }
        public int CurrentUserId { get => _currentUserId; private set { _currentUserId = value; OnPropertyChanged(); } }
        public string CurrentUsername { get => _currentUsername; private set { _currentUsername = value; OnPropertyChanged(); } }
        public string CurrentFullName { get => _currentFullName; private set { _currentFullName = value; OnPropertyChanged(); } }
        public string CurrentRole { get => _currentRole; private set { _currentRole = value; OnPropertyChanged(); } }
        public string CurrentStatus { get => _currentStatus; private set { _currentStatus = value; OnPropertyChanged(); } }
        public string ProfileUsername { get => _profileUsername; set { _profileUsername = value ?? string.Empty; OnPropertyChanged(); } }
        public string ProfileFullName { get => _profileFullName; set { _profileFullName = value ?? string.Empty; OnPropertyChanged(); } }
        public string ProfileEmail { get => _profileEmail; set { _profileEmail = value ?? string.Empty; OnPropertyChanged(); } }
        public string ProfileRole { get => _profileRole; private set { _profileRole = value ?? string.Empty; OnPropertyChanged(); } }
        public string ProfileStatus { get => _profileStatus; private set { _profileStatus = value ?? string.Empty; OnPropertyChanged(); } }
        public string ProfileLastLogin { get => _profileLastLogin; private set { _profileLastLogin = value ?? "Never"; OnPropertyChanged(); } }
        public string ProfileNewPassword { get => _profileNewPassword; set { _profileNewPassword = value ?? string.Empty; OnPropertyChanged(); } }
        public string ProfileConfirmPassword { get => _profileConfirmPassword; set { _profileConfirmPassword = value ?? string.Empty; OnPropertyChanged(); } }
        public string AddUserUsername { get => _addUserUsername; set { _addUserUsername = value ?? string.Empty; OnPropertyChanged(); } }
        public string AddUserFullName { get => _addUserFullName; set { _addUserFullName = value ?? string.Empty; OnPropertyChanged(); } }
        public string AddUserEmail { get => _addUserEmail; set { _addUserEmail = value ?? string.Empty; OnPropertyChanged(); } }
        public string AddUserPassword { get => _addUserPassword; set { _addUserPassword = value ?? string.Empty; OnPropertyChanged(); } }
        public string AddUserConfirmPassword { get => _addUserConfirmPassword; set { _addUserConfirmPassword = value ?? string.Empty; OnPropertyChanged(); } }
        public string AddUserRole { get => _addUserRole; set { _addUserRole = value ?? string.Empty; OnPropertyChanged(); } }
        public Brush CurrentStatusBrush =>
            string.Equals(CurrentStatus, "Active", StringComparison.OrdinalIgnoreCase)
                ? SuccessBrush
                : string.Equals(CurrentStatus, "Locked", StringComparison.OrdinalIgnoreCase)
                    ? ErrorBrush
                    : InfoBrush;

        public bool UseLocalDatabase
        {
            get => _useLocalDatabase;
            set
            {
                if (_useLocalDatabase == value)
                {
                    return;
                }

                SetConnectionMode(value);
            }
        }

        public bool UseRemoteDatabase
        {
            get => _useRemoteDatabase;
            set
            {
                if (_useRemoteDatabase == value)
                {
                    return;
                }

                SetConnectionMode(!value);
            }
        }

        public string DbHost
        {
            get => _dbHost;
            set
            {
                var next = value?.Trim() ?? string.Empty;
                if (_dbHost == next)
                {
                    return;
                }

                _dbHost = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DatabaseEndpointText));
            }
        }

        public string DbPort
        {
            get => _dbPort;
            set
            {
                var next = value?.Trim() ?? string.Empty;
                if (_dbPort == next)
                {
                    return;
                }

                _dbPort = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DatabaseEndpointText));
            }
        }

        public string DbName
        {
            get => _dbName;
            set
            {
                var next = value?.Trim() ?? string.Empty;
                if (_dbName == next)
                {
                    return;
                }

                _dbName = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DatabaseEndpointText));
            }
        }

        public string DbUsername
        {
            get => _dbUsername;
            set
            {
                var next = value?.Trim() ?? string.Empty;
                if (_dbUsername == next)
                {
                    return;
                }

                _dbUsername = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DatabaseEndpointText));
            }
        }

        public string DbPassword
        {
            get => _dbPassword;
            set
            {
                var next = value ?? string.Empty;
                if (_dbPassword == next)
                {
                    return;
                }

                _dbPassword = next;
                OnPropertyChanged();
            }
        }

        public string DbConnectionStatus
        {
            get => _dbConnectionStatus;
            private set
            {
                if (_dbConnectionStatus == value)
                {
                    return;
                }

                _dbConnectionStatus = value;
                OnPropertyChanged();
            }
        }

        public Brush DbConnectionStatusBrush
        {
            get => _dbConnectionStatusBrush;
            private set
            {
                if (_dbConnectionStatusBrush == value)
                {
                    return;
                }

                _dbConnectionStatusBrush = value;
                OnPropertyChanged();
            }
        }

        public string DatabaseEndpointText => $"{DbHost}:{DbPort} / {DbName} ({DbUsername})";

        public string StorageLocation
        {
            get => _storageLocation;
            private set
            {
                if (_storageLocation == value)
                {
                    return;
                }

                _storageLocation = value;
                OnPropertyChanged();
            }
        }

        public string TempFolderPath
        {
            get => _tempFolderPath;
            private set
            {
                if (_tempFolderPath == value)
                {
                    return;
                }

                _tempFolderPath = value;
                OnPropertyChanged();
            }
        }

        public string TempFilesSizeText
        {
            get => _tempFilesSizeText;
            private set
            {
                if (_tempFilesSizeText == value)
                {
                    return;
                }

                _tempFilesSizeText = value;
                OnPropertyChanged();
            }
        }

        public bool ConfirmResetAndSeed
        {
            get => _confirmResetAndSeed;
            set
            {
                if (_confirmResetAndSeed == value)
                {
                    return;
                }

                _confirmResetAndSeed = value;
                OnPropertyChanged();
            }
        }

        public UsersRolesViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
            AddUserCommand = new AsyncRelayCommand(_ => AddUserAsync());
            SaveUserCommand = new AsyncRelayCommand(SaveUserAsync);
            ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync);
            DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync);
            SaveProfileCommand = new AsyncRelayCommand(_ => SaveProfileAsync());
            TestDbConnectionCommand = new AsyncRelayCommand(_ => TestDbConnectionAsync());
            CreateDatabaseCommand = new AsyncRelayCommand(_ => CreateDatabaseAsync());
            SaveDbConnectionCommand = new AsyncRelayCommand(_ => SaveDbConnectionAsync());
            RefreshSystemInfoCommand = new AsyncRelayCommand(_ => RefreshSystemInfoAsync());
            OpenStorageFolderCommand = new AsyncRelayCommand(_ => OpenStorageFolderAsync());
            CreateBackupCommand = new AsyncRelayCommand(_ => CreateBackupAsync());
            ClearTempFilesCommand = new AsyncRelayCommand(_ => ClearTempFilesAsync());
            SeedDatabaseCommand = new AsyncRelayCommand(_ => SeedDatabaseAsync());
            ResetAndSeedCommand = new AsyncRelayCommand(_ => ResetAndSeedAsync());

            InitializeConnectionSettings();
            InitializeStoragePaths();

            _ = LoadAsync();
        }

        public Task RefreshNowAsync() => LoadAsync();

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetMessage("Loading users, roles, and permissions...", InfoBrush);

            try
            {
                var usersTask = _dataService.GetUsersAsync();
                var rolesTask = _dataService.GetRolesAsync();
                var statsTask = _dataService.GetStatsAsync();
                var roleSummariesTask = _dataService.GetRoleSummariesAsync();
                var permissionSummariesTask = _dataService.GetPermissionSummariesAsync();

                var users = await usersTask;
                var roles = await rolesTask;
                var stats = await statsTask;
                var roleSummaries = await roleSummariesTask;
                var permissionSummaries = await permissionSummariesTask;

                Roles.Clear();
                foreach (var role in roles)
                {
                    Roles.Add(role);
                }
                LoadAddUserRoles(roles);

                _allUsers.Clear();
                foreach (var user in users)
                {
                    var row = new UserAccessRow
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        FullName = string.IsNullOrWhiteSpace(user.FullName) ? "-" : user.FullName,
                        Email = string.IsNullOrWhiteSpace(user.Email) ? "-" : user.Email,
                        LastLoginText = user.LastLoginAt.HasValue
                            ? user.LastLoginAt.Value.ToString("MMM dd, yyyy h:mm tt", CultureInfo.InvariantCulture)
                            : "Never",
                        SelectedRole = string.IsNullOrWhiteSpace(user.RoleName) ? "Employee" : user.RoleName.Trim(),
                        SelectedStatus = NormalizeStatus(user.Status)
                    };
                    row.MarkPersisted();
                    _allUsers.Add(row);
                }

                RoleSummaries.Clear();
                foreach (var role in roleSummaries)
                {
                    RoleSummaries.Add(new RoleSummaryRow
                    {
                        RoleName = role.RoleName,
                        Users = role.UserCount,
                        Permissions = role.PermissionCount
                    });
                }

                PermissionSummaries.Clear();
                foreach (var permission in permissionSummaries)
                {
                    PermissionSummaries.Add(new PermissionSummaryRow
                    {
                        PermissionCode = permission.PermissionCode,
                        Description = string.IsNullOrWhiteSpace(permission.Description) ? "-" : permission.Description,
                        AssignedRoles = permission.AssignedRoleCount
                    });
                }

                TotalUsers = stats.TotalUsers;
                ActiveUsers = stats.ActiveUsers;
                LockedUsers = stats.LockedUsers;
                AdminUsers = stats.AdminUsers;
                TotalRoles = stats.TotalRoles;
                TotalPermissions = stats.TotalPermissions;
                InactiveUsers = Math.Max(0, TotalUsers - ActiveUsers - LockedUsers);

                ApplyFilters();
                SetMessage("Users & Roles loaded.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to load Users & Roles: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LoadAddUserRoles(IEnumerable<string> availableRoles)
        {
            var orderedPreferred = new[] { "Admin", "HR Manager", "Dept Head" };
            var source = availableRoles?
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .ToList() ?? new List<string>();

            AddUserRoles.Clear();
            foreach (var role in orderedPreferred.Where(r => source.Any(x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase))))
            {
                AddUserRoles.Add(role);
            }

            if (AddUserRoles.Count == 0)
            {
                foreach (var role in source.Where(r => !string.Equals(r, "Employee", StringComparison.OrdinalIgnoreCase)))
                {
                    AddUserRoles.Add(role);
                }
            }

            if (AddUserRoles.Count > 0 &&
                !AddUserRoles.Any(r => string.Equals(r, AddUserRole, StringComparison.OrdinalIgnoreCase)))
            {
                AddUserRole = AddUserRoles[0];
            }
        }

        private async Task AddUserAsync()
        {
            var username = AddUserUsername.Trim();
            var fullName = AddUserFullName.Trim();
            var email = AddUserEmail.Trim();
            var password = AddUserPassword;
            var confirmPassword = AddUserConfirmPassword;
            var role = AddUserRole.Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword) ||
                string.IsNullOrWhiteSpace(role))
            {
                SetMessage("Username, full name, role, password, and confirm password are required.", ErrorBrush);
                return;
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                SetMessage("Password and retype password do not match.", ErrorBrush);
                return;
            }
            if (password.Trim().Length < 8)
            {
                SetMessage("Password must be at least 8 characters.", ErrorBrush);
                return;
            }

            if (!string.IsNullOrWhiteSpace(email) &&
                !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
            {
                SetMessage("Invalid email format.", ErrorBrush);
                return;
            }

            try
            {
                var createdId = await _dataService.CreateUserAsync(new CreateUserAccountDto(
                    Username: username,
                    Password: password.Trim(),
                    FullName: fullName,
                    Email: email,
                    RoleName: role),
                    CurrentUserId);

                if (createdId <= 0)
                {
                    throw new InvalidOperationException("Insert failed. No row was created.");
                }

                var createdUsername = username;
                AddUserUsername = string.Empty;
                AddUserFullName = string.Empty;
                AddUserEmail = string.Empty;
                AddUserPassword = string.Empty;
                AddUserConfirmPassword = string.Empty;
                SearchText = string.Empty;
                SelectedStatusFilter = "All";

                await LoadAsync();
                SetMessage($"User '{createdUsername}' was created successfully.", SuccessBrush);
                SystemRefreshBus.Raise("UserAccountCreated");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to add user: {ex.Message}", ErrorBrush);
            }
        }

        private void ApplyFilters()
        {
            var query = SearchText.Trim();
            var filter = SelectedStatusFilter?.Trim() ?? "All";

            var filtered = _allUsers.Where(user =>
            {
                var matchesStatus = string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(user.SelectedStatus, filter, StringComparison.OrdinalIgnoreCase);

                if (!matchesStatus)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(query))
                {
                    return true;
                }

                return Contains(user.Username, query) ||
                       Contains(user.FullName, query) ||
                       Contains(user.Email, query) ||
                       Contains(user.SelectedRole, query) ||
                       Contains(user.SelectedStatus, query);
            }).ToList();

            Users.Clear();
            foreach (var row in filtered)
            {
                Users.Add(row);
            }
        }

        private async Task SaveUserAsync(object? parameter)
        {
            if (parameter is not UserAccessRow row)
            {
                return;
            }

            if (!row.IsDirty)
            {
                SetMessage($"No changes to save for {row.Username}.", InfoBrush);
                return;
            }

            try
            {
                await _dataService.UpdateUserAccessAsync(row.UserId, row.SelectedRole, row.SelectedStatus, CurrentUserId);
                row.MarkPersisted();
                SetMessage($"Saved: {row.Username} ({row.SelectedRole}, {row.SelectedStatus}).", SuccessBrush);
                await LoadAsync();
                SystemRefreshBus.Raise("UserAccessUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Save failed for {row.Username}: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ResetPasswordAsync(object? parameter)
        {
            if (parameter is not UserAccessRow row)
            {
                return;
            }

            try
            {
                var temporaryPassword = await _dataService.ResetPasswordAsync(row.UserId, CurrentUserId);
                SetMessage($"Password reset for {row.Username}. Temporary password: {temporaryPassword}", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Password reset failed for {row.Username}: {ex.Message}", ErrorBrush);
            }
        }

        private async Task DeleteUserAsync(object? parameter)
        {
            if (parameter is not UserAccessRow row)
            {
                return;
            }

            if (row.UserId == CurrentUserId)
            {
                SetMessage("You cannot delete your own current account.", ErrorBrush);
                return;
            }

            try
            {
                await _dataService.DeleteUserAsync(row.UserId, CurrentUserId);
                SetMessage($"Deleted user account and linked employee: {row.Username}.", SuccessBrush);
                await LoadAsync();
                SystemRefreshBus.Raise("UserDeleted");
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                SetMessage(
                    $"Cannot delete {row.Username} because related records exist. Set status to INACTIVE instead.",
                    ErrorBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Delete failed for {row.Username}: {ex.Message}", ErrorBrush);
            }
        }

        private void SetConnectionMode(bool useLocal)
        {
            _useLocalDatabase = useLocal;
            _useRemoteDatabase = !useLocal;

            if (useLocal &&
                !string.Equals(DbHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(DbHost, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                DbHost = "127.0.0.1";
            }

            OnPropertyChanged(nameof(UseLocalDatabase));
            OnPropertyChanged(nameof(UseRemoteDatabase));
        }

        private void InitializeConnectionSettings()
        {
            var settings = DbConfig.GetSettings();
            DbHost = settings.Host;
            DbPort = settings.Port;
            DbName = settings.Database;
            DbUsername = settings.Username;
            DbPassword = settings.Password;

            var local = string.Equals(settings.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(settings.Host, "localhost", StringComparison.OrdinalIgnoreCase);
            _useLocalDatabase = local;
            _useRemoteDatabase = !local;
            OnPropertyChanged(nameof(UseLocalDatabase));
            OnPropertyChanged(nameof(UseRemoteDatabase));
        }

        private void InitializeStoragePaths()
        {
            StorageLocation = Path.Combine(AppContext.BaseDirectory, "Storage");
            TempFolderPath = Path.Combine(Path.GetTempPath(), "HRMS");

            Directory.CreateDirectory(StorageLocation);
            Directory.CreateDirectory(TempFolderPath);
            UpdateTempFilesSize();
        }

        private DbConnectionSettings GetCurrentConnectionSettings() =>
            new()
            {
                Host = DbHost,
                Port = DbPort,
                Database = DbName,
                Username = DbUsername,
                Password = DbPassword
            };

        private string BuildCurrentConnectionString() =>
            DbConfig.BuildConnectionString(GetCurrentConnectionSettings());

        private string BuildServerConnectionString()
        {
            var builder = new MySqlConnectionStringBuilder(BuildCurrentConnectionString())
            {
                Database = string.Empty
            };
            return builder.ConnectionString;
        }

        private async Task TestDbConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(DbHost) ||
                string.IsNullOrWhiteSpace(DbPort) ||
                string.IsNullOrWhiteSpace(DbName) ||
                string.IsNullOrWhiteSpace(DbUsername))
            {
                SetMessage("Database connection fields are required.", ErrorBrush);
                SetDatabaseConnectionState("Invalid settings", ErrorBrush);
                return;
            }

            try
            {
                await using var connection = new MySqlConnection(BuildCurrentConnectionString());
                await connection.OpenAsync();
                await connection.CloseAsync();

                SetDatabaseConnectionState($"Connected ({DatabaseEndpointText})", SuccessBrush);
                SetMessage("Database connection test successful.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetDatabaseConnectionState("Connection failed", ErrorBrush);
                SetMessage($"Connection test failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task CreateDatabaseAsync()
        {
            if (string.IsNullOrWhiteSpace(DbHost) ||
                string.IsNullOrWhiteSpace(DbPort) ||
                string.IsNullOrWhiteSpace(DbUsername) ||
                string.IsNullOrWhiteSpace(DbName))
            {
                SetMessage("Host, port, username, and database name are required.", ErrorBrush);
                return;
            }

            try
            {
                var targetDbName = DbName.Trim();
                var safeDbName = targetDbName.Replace("`", "``");
                var sql =
                    $"CREATE DATABASE IF NOT EXISTS `{safeDbName}` " +
                    "DEFAULT CHARACTER SET utf8mb4 " +
                    "DEFAULT COLLATE utf8mb4_unicode_ci;";

                await using var connection = new MySqlConnection(BuildServerConnectionString());
                await connection.OpenAsync();

                var exists = false;
                await using (var existsCommand = new MySqlCommand(
                                 "SELECT COUNT(*) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @dbName;",
                                 connection))
                {
                    existsCommand.Parameters.AddWithValue("@dbName", targetDbName);
                    var countObj = await existsCommand.ExecuteScalarAsync();
                    var count = Convert.ToInt32(countObj ?? 0);
                    exists = count > 0;
                }

                if (!exists)
                {
                    await using var command = new MySqlCommand(sql, connection);
                    await command.ExecuteNonQueryAsync();
                }

                await using (var verifyConnection = new MySqlConnection(BuildCurrentConnectionString()))
                {
                    await verifyConnection.OpenAsync();
                    await verifyConnection.CloseAsync();
                }

                SetDatabaseConnectionState($"Connected ({DatabaseEndpointText})", SuccessBrush);
                SetMessage(
                    exists
                        ? $"Database '{targetDbName}' already exists. You can click Seed Database."
                        : $"Database '{targetDbName}' created successfully. You can now click Seed Database.",
                    exists ? InfoBrush : SuccessBrush);
            }
            catch (Exception ex)
            {
                SetDatabaseConnectionState("Connection failed", ErrorBrush);
                SetMessage($"Create database failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SaveDbConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(DbHost) ||
                string.IsNullOrWhiteSpace(DbPort) ||
                string.IsNullOrWhiteSpace(DbName) ||
                string.IsNullOrWhiteSpace(DbUsername))
            {
                SetMessage("Host, port, database, and username are required.", ErrorBrush);
                return;
            }

            try
            {
                DbConfig.SaveSettings(GetCurrentConnectionSettings());
                await TestDbConnectionAsync();
                SetMessage(
                    "Database settings saved. Restart the app to ensure all open modules use the new connection.",
                    SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Save settings failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task RefreshSystemInfoAsync()
        {
            UpdateTempFilesSize();
            await TestDbConnectionAsync();
        }

        private async Task OpenStorageFolderAsync()
        {
            try
            {
                Directory.CreateDirectory(StorageLocation);
                Process.Start(new ProcessStartInfo
                {
                    FileName = StorageLocation,
                    UseShellExecute = true
                });
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to open storage folder: {ex.Message}", ErrorBrush);
            }
        }

        private async Task CreateBackupAsync()
        {
            try
            {
                var backupsFolder = Path.Combine(StorageLocation, "Backups");
                Directory.CreateDirectory(backupsFolder);

                var backupFile = Path.Combine(
                    backupsFolder,
                    $"{DbName}_{DateTime.Now:yyyyMMdd_HHmmss}.sql");

                var args =
                    $"--host=\"{DbHost}\" " +
                    $"--port=\"{DbPort}\" " +
                    $"--user=\"{DbUsername}\" " +
                    $"--password=\"{DbPassword}\" " +
                    $"--single-transaction --routines --events --databases \"{DbName}\" " +
                    $"--result-file=\"{backupFile}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "mysqldump",
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var errorOutput = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    SetMessage(
                        $"Backup failed. Ensure mysqldump is installed and in PATH. {errorOutput}".Trim(),
                        ErrorBrush);
                    return;
                }

                SetMessage($"Backup created: {backupFile}", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Backup failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ClearTempFilesAsync()
        {
            try
            {
                Directory.CreateDirectory(TempFolderPath);
                var tempDir = new DirectoryInfo(TempFolderPath);
                var beforeBytes = GetDirectorySize(tempDir);

                foreach (var file in tempDir.GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        file.IsReadOnly = false;
                        file.Delete();
                    }
                    catch
                    {
                        // Skip locked files.
                    }
                }

                foreach (var directory in tempDir.GetDirectories("*", SearchOption.AllDirectories).OrderByDescending(x => x.FullName.Length))
                {
                    try
                    {
                        directory.Delete();
                    }
                    catch
                    {
                        // Skip non-empty or locked folders.
                    }
                }

                UpdateTempFilesSize();
                SetMessage($"Temporary files cleared. Freed approximately {FormatBytes(beforeBytes)}.", SuccessBrush);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                SetMessage($"Clear temp files failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SeedDatabaseAsync()
        {
            try
            {
                var migrationRunner = new DbMigrationService(BuildCurrentConnectionString());
                var result = await migrationRunner.ApplyPendingMigrationsAsync();

                if (result.TotalMigrationFiles == 0)
                {
                    SetMessage("No migration files found in Database/Migrations. Nothing to seed.", ErrorBrush);
                    return;
                }

                if (result.AppliedCount == 0)
                {
                    SetMessage(
                        $"Seed skipped: database '{DbName}' is already up to date (no pending migrations).",
                        InfoBrush);
                }
                else
                {
                    SetMessage(
                        $"Seed complete. Applied {result.AppliedCount} migration(s) of {result.TotalMigrationFiles}.",
                        SuccessBrush);
                    SystemRefreshBus.Raise("DatabaseSeeded");
                }

                await LoadAsync();
            }
            catch (Exception ex)
            {
                SetMessage($"Seed failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ResetAndSeedAsync()
        {
            if (!ConfirmResetAndSeed)
            {
                SetMessage("Enable 'I understand this will delete all data' before Reset and Seed.", ErrorBrush);
                return;
            }

            try
            {
                await ResetDatabaseSchemaAsync();
                var migrationRunner = new DbMigrationService(BuildCurrentConnectionString());
                var result = await migrationRunner.ApplyPendingMigrationsAsync();

                ConfirmResetAndSeed = false;
                SetMessage(
                    $"Database reset complete. Re-applied {result.AppliedCount} migration(s).",
                    SuccessBrush);

                await LoadAsync();
                SystemRefreshBus.Raise("DatabaseResetAndSeeded");
            }
            catch (Exception ex)
            {
                SetMessage($"Reset and Seed failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ResetDatabaseSchemaAsync()
        {
            var resetSql = @"
CREATE TABLE IF NOT EXISTS schema_migrations (
    migration_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    migration_key VARCHAR(190) NOT NULL UNIQUE,
    applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

SET FOREIGN_KEY_CHECKS = 0;

DROP VIEW IF EXISTS v_dtr_daily_effective;
DROP VIEW IF EXISTS v_dtr_daily_raw;

SET @drop_tables_sql = (
  SELECT IFNULL(
    CONCAT(
      'DROP TABLE IF EXISTS ',
      GROUP_CONCAT(CONCAT('`', table_name, '`') ORDER BY table_name SEPARATOR ',')
    ),
    'SELECT 1'
  )
  FROM INFORMATION_SCHEMA.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_TYPE = 'BASE TABLE'
    AND TABLE_NAME <> 'schema_migrations'
);

PREPARE stmt FROM @drop_tables_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET FOREIGN_KEY_CHECKS = 1;
DELETE FROM schema_migrations;";

            await using var connection = new MySqlConnection(BuildCurrentConnectionString());
            await connection.OpenAsync();
            await using var command = new MySqlCommand(resetSql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private void UpdateTempFilesSize()
        {
            try
            {
                Directory.CreateDirectory(TempFolderPath);
                var bytes = GetDirectorySize(new DirectoryInfo(TempFolderPath));
                TempFilesSizeText = FormatBytes(bytes);
            }
            catch
            {
                TempFilesSizeText = "0.00 MB";
            }
        }

        private static long GetDirectorySize(DirectoryInfo directory)
        {
            if (!directory.Exists)
            {
                return 0;
            }

            long total = 0;
            foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    total += file.Length;
                }
                catch
                {
                    // Skip inaccessible files.
                }
            }

            return total;
        }

        private static string FormatBytes(long bytes)
        {
            const double kb = 1024d;
            const double mb = kb * 1024d;
            const double gb = mb * 1024d;

            if (bytes >= gb)
            {
                return $"{bytes / gb:0.00} GB";
            }

            if (bytes >= mb)
            {
                return $"{bytes / mb:0.00} MB";
            }

            if (bytes >= kb)
            {
                return $"{bytes / kb:0.00} KB";
            }

            return $"{bytes:0} B";
        }

        private void SetDatabaseConnectionState(string status, Brush brush)
        {
            DbConnectionStatus = status;
            DbConnectionStatusBrush = brush;
        }

        private static bool Contains(string source, string query) =>
            !string.IsNullOrWhiteSpace(source) &&
            source.Contains(query, StringComparison.OrdinalIgnoreCase);

        private static string NormalizeStatus(string? status)
        {
            var value = status?.Trim().ToUpperInvariant();
            return value switch
            {
                "ACTIVE" => "ACTIVE",
                "INACTIVE" => "INACTIVE",
                "LOCKED" => "LOCKED",
                _ => "ACTIVE"
            };
        }

        public void SetCurrentUser(int userId, string username, string fullName, string roleName, string status)
        {
            CurrentUserId = userId;
            CurrentUsername = string.IsNullOrWhiteSpace(username) ? "-" : username.Trim();
            CurrentFullName = string.IsNullOrWhiteSpace(fullName) ? "Current User" : fullName.Trim();
            CurrentRole = string.IsNullOrWhiteSpace(roleName) ? "-" : roleName.Trim();
            CurrentStatus = NormalizeStatus(status) switch
            {
                "ACTIVE" => "Active",
                "LOCKED" => "Locked",
                _ => "Inactive"
            };
            OnPropertyChanged(nameof(CurrentStatusBrush));
            _ = LoadCurrentUserProfileAsync(userId);
        }

        private async Task LoadCurrentUserProfileAsync(int userId)
        {
            if (userId <= 0)
            {
                return;
            }

            try
            {
                var profile = await _dataService.GetCurrentUserProfileAsync(userId);
                if (profile == null)
                {
                    return;
                }

                ProfileUsername = profile.Username;
                ProfileFullName = profile.FullName;
                ProfileEmail = profile.Email;
                ProfileRole = profile.RoleName;
                ProfileStatus = NormalizeStatus(profile.Status) switch
                {
                    "ACTIVE" => "Active",
                    "LOCKED" => "Locked",
                    _ => "Inactive"
                };
                ProfileLastLogin = profile.LastLoginAt.HasValue
                    ? profile.LastLoginAt.Value.ToString("MMM dd, yyyy h:mm tt", CultureInfo.InvariantCulture)
                    : "Never";

                CurrentUsername = ProfileUsername;
                CurrentFullName = string.IsNullOrWhiteSpace(ProfileFullName) ? "Current User" : ProfileFullName;
                CurrentRole = ProfileRole;
                CurrentStatus = ProfileStatus;
                OnPropertyChanged(nameof(CurrentStatusBrush));
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to load profile: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SaveProfileAsync()
        {
            if (CurrentUserId <= 0)
            {
                SetMessage("No current user session loaded.", ErrorBrush);
                return;
            }

            if (string.IsNullOrWhiteSpace(ProfileUsername))
            {
                SetMessage("Username is required.", ErrorBrush);
                return;
            }

            if (!string.IsNullOrWhiteSpace(ProfileEmail) &&
                !Regex.IsMatch(ProfileEmail.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
            {
                SetMessage("Invalid email format.", ErrorBrush);
                return;
            }

            if (!string.IsNullOrWhiteSpace(ProfileNewPassword) || !string.IsNullOrWhiteSpace(ProfileConfirmPassword))
            {
                if (string.IsNullOrWhiteSpace(ProfileNewPassword))
                {
                    SetMessage("New password is required.", ErrorBrush);
                    return;
                }

                if (!string.Equals(ProfileNewPassword, ProfileConfirmPassword, StringComparison.Ordinal))
                {
                    SetMessage("Password confirmation does not match.", ErrorBrush);
                    return;
                }
                if (ProfileNewPassword.Trim().Length < 8)
                {
                    SetMessage("New password must be at least 8 characters.", ErrorBrush);
                    return;
                }
            }

            try
            {
                await _dataService.UpdateCurrentUserProfileAsync(new UpdateCurrentUserProfileDto(
                    UserId: CurrentUserId,
                    Username: ProfileUsername.Trim(),
                    FullName: ProfileFullName.Trim(),
                    Email: ProfileEmail.Trim(),
                    NewPassword: string.IsNullOrWhiteSpace(ProfileNewPassword) ? null : ProfileNewPassword));

                ProfileNewPassword = string.Empty;
                ProfileConfirmPassword = string.Empty;
                SetMessage("Profile updated successfully.", SuccessBrush);

                await LoadCurrentUserProfileAsync(CurrentUserId);
                await LoadAsync();
                SystemRefreshBus.Raise("UserProfileUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Profile update failed: {ex.Message}", ErrorBrush);
            }
        }

        private void SetMessage(string message, Brush brush)
        {
            ActionMessage = message;
            ActionMessageBrush = brush;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class UserAccessRow : INotifyPropertyChanged
    {
        private static readonly Brush ActiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush InactiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B95A1"));
        private static readonly Brush LockedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private string _selectedRole = string.Empty;
        private string _selectedStatus = "ACTIVE";
        private string _originalRole = string.Empty;
        private string _originalStatus = "ACTIVE";
        private bool _isDirty;
        private Brush _statusBrush = ActiveBrush;

        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string LastLoginText { get; set; } = "Never";

        public string SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (_selectedRole == value)
                {
                    return;
                }

                _selectedRole = value ?? string.Empty;
                OnPropertyChanged();
                UpdateDirty();
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (_selectedStatus == value)
                {
                    return;
                }

                _selectedStatus = string.IsNullOrWhiteSpace(value) ? "ACTIVE" : value.Trim().ToUpperInvariant();
                OnPropertyChanged();
                UpdateStatusVisual();
                UpdateDirty();
            }
        }

        public string StatusLabel =>
            SelectedStatus switch
            {
                "ACTIVE" => "Active",
                "INACTIVE" => "Inactive",
                "LOCKED" => "Locked",
                _ => "Active"
            };

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set
            {
                if (_statusBrush == value)
                {
                    return;
                }

                _statusBrush = value;
                OnPropertyChanged();
            }
        }

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value)
                {
                    return;
                }

                _isDirty = value;
                OnPropertyChanged();
            }
        }

        public void MarkPersisted()
        {
            _originalRole = SelectedRole;
            _originalStatus = SelectedStatus;
            UpdateStatusVisual();
            UpdateDirty();
        }

        private void UpdateStatusVisual()
        {
            StatusBrush = SelectedStatus switch
            {
                "ACTIVE" => ActiveBrush,
                "INACTIVE" => InactiveBrush,
                "LOCKED" => LockedBrush,
                _ => ActiveBrush
            };

            OnPropertyChanged(nameof(StatusLabel));
        }

        private void UpdateDirty()
        {
            IsDirty = !string.Equals(SelectedRole, _originalRole, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(SelectedStatus, _originalStatus, StringComparison.OrdinalIgnoreCase);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class RoleSummaryRow
    {
        public string RoleName { get; set; } = string.Empty;
        public int Users { get; set; }
        public int Permissions { get; set; }
    }

    public class PermissionSummaryRow
    {
        public string PermissionCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int AssignedRoles { get; set; }
    }
}
