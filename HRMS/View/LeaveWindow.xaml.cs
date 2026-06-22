using HRMS.ViewModel;
using HRMS.Model;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class LeaveWindow : UserControl
    {
        public LeaveWindow()
        {
            InitializeComponent();
            DataContext = new LeaveViewModel();
        }

        public async Task RefreshAsync()
        {
            if (DataContext is LeaveViewModel vm)
            {
                await vm.RefreshAsync();
            }
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (DataContext is LeaveViewModel vm)
            {
                vm.SetCurrentUser(user?.UserId ?? 0, user?.Username ?? "-", user?.RoleName);
            }
        }

        private void OpenFileLeavePopup_OnClick(object sender, RoutedEventArgs e)
        {
            FileLeavePopup.IsOpen = true;
        }

        private void CloseFileLeavePopup_OnClick(object sender, RoutedEventArgs e)
        {
            FileLeavePopup.IsOpen = false;
        }
    }
}
