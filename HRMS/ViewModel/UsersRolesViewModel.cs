using HRMS.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows;

namespace HRMS.ViewModel
{
    public class UsersRolesViewModel : INotifyPropertyChanged
    {
        private readonly UsersRolesDataService _dataService = new(DbConfig.ConnectionString);

        public ObservableCollection<UserDisplay> Users { get; } = new();
        public ObservableCollection<string> Roles { get; } = new();
        public ICommand SaveRoleCommand { get; }

        private int _totalUsers;
        private int _activeUsers;
        private int _managerUsers;
        private int _totalRoles;
        private int _adminUsers;

        public int TotalUsers { get => _totalUsers; set { _totalUsers = value; OnPropertyChanged(); } }
        public int ActiveUsers { get => _activeUsers; set { _activeUsers = value; OnPropertyChanged(); } }
        public int ManagerUsers { get => _managerUsers; set { _managerUsers = value; OnPropertyChanged(); } }
        public int TotalRoles { get => _totalRoles; set { _totalRoles = value; OnPropertyChanged(); } }
        public int AdminUsers { get => _adminUsers; set { _adminUsers = value; OnPropertyChanged(); } }

        public UsersRolesViewModel()
        {
            SaveRoleCommand = new AsyncRelayCommand(async p =>
            {
                if (p is UserDisplay user && !string.IsNullOrWhiteSpace(user.CurrentRole))
                {
                    try
                    {
                        await _dataService.UpdateUserRoleAsync(user.Id, user.CurrentRole);
                        user.Roles = user.CurrentRole;
                        // Reload to reflect all derived stats and role text immediately
                        await LoadAsync();
                        MessageBox.Show("Role saved successfully.", "Users & Roles", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch
                    {
                        MessageBox.Show("Failed to save role. Please try again.", "Users & Roles", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var users = await _dataService.GetUsersAsync();
            var roleCount = await _dataService.GetRoleCountAsync();
            var roles = await _dataService.GetRolesAsync();

            Users.Clear();
            Roles.Clear();
            foreach (var r in roles) Roles.Add(r);

            foreach (var u in users)
            {
                var firstRole = u.Roles?.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).FirstOrDefault() ?? "None";
                Users.Add(new UserDisplay
                {
                    Id = u.Id,
                    Username = u.Username,
                    IsActive = u.IsActive,
                    Roles = string.IsNullOrWhiteSpace(u.Roles) ? "None" : u.Roles,
                    CurrentRole = firstRole,
                    StatusColor = u.IsActive ? new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E4368"))
                                             : new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935"))
                });
            }

            TotalRoles = roleCount;
            UpdateAggregates();
        }

        private void UpdateAggregates()
        {
            TotalUsers = Users.Count;
            ActiveUsers = Users.Count(x => x.IsActive);
            AdminUsers = Users.Count(x => x.Roles?.Split(',').Any(r => r.Trim().ToLower() == "admin") == true);
            ManagerUsers = Users.Count(x => x.Roles?.Split(',').Any(r => r.Trim().ToLower() == "manager") == true);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class UserDisplay : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        private string _roles = string.Empty;
        public string Roles { get => _roles; set { _roles = value; OnPropertyChanged(); } }
        private string _currentRole = string.Empty;
        public string CurrentRole
        {
            get => _currentRole;
            set { _currentRole = value; OnPropertyChanged(); }
        }
        public bool IsActive { get; set; }
        public Brush? StatusColor { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
