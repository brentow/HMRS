using System.Windows.Controls;
using HRMS.Model;
using HRMS.ViewModel;
using System.Threading.Tasks;
using System;
using System.Windows;

namespace HRMS.View
{
    public partial class UsersRolesWindow : UserControl
    {
        private readonly UsersRolesViewModel _viewModel;

        public UsersRolesWindow()
        {
            InitializeComponent();
            _viewModel = new UsersRolesViewModel();
            DataContext = _viewModel;
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (user == null)
            {
                _viewModel.SetCurrentUser(0, "-", "Current User", "-", "INACTIVE");
                ApplyAccessScope("-");
                OpenProfileTab();
                return;
            }

            ApplyAccessScope(user.RoleName);
            _viewModel.SetCurrentUser(
                user.UserId,
                user.Username,
                user.FullName,
                user.RoleName,
                user.Status);
        }

        public void OpenProfileTab()
        {
            if (UsersRolesTabControl != null && ProfileTabItem != null)
            {
                UsersRolesTabControl.SelectedItem = ProfileTabItem;
            }
        }

        public void OpenUsersAdminTab()
        {
            if (UsersRolesAdminTabItem == null || UsersRolesTabControl == null)
            {
                return;
            }

            if (UsersRolesAdminTabItem.Visibility == Visibility.Visible)
            {
                UsersRolesTabControl.SelectedItem = UsersRolesAdminTabItem;
            }
            else
            {
                OpenProfileTab();
            }
        }

        private void ApplyAccessScope(string? roleName)
        {
            var isAdmin = string.Equals(roleName?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase);

            if (UsersRolesAdminTabItem != null)
            {
                UsersRolesAdminTabItem.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            }

            if (SystemSettingsTabItem != null)
            {
                SystemSettingsTabItem.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Task RefreshAsync() => _viewModel.RefreshNowAsync();

        private void LogoutButton_OnClick(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Do you want to log out now?",
                "Log Out",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var login = new LoginPage();
            Application.Current.MainWindow = login;
            login.Show();

            var hostWindow = Window.GetWindow(this);
            hostWindow?.Close();
        }
    }
}
