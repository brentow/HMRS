using System;
using System.Windows.Controls;
using System.Windows;
using HRMS.Model;
using HRMS.ViewModel;
using System.Threading.Tasks;

namespace HRMS.View
{
    public partial class AttendanceWindow : UserControl
    {
        public AttendanceWindow()
        {
            InitializeComponent();
            DataContext = new AttendanceViewModel();
        }

        public async Task RefreshAsync()
        {
            if (DataContext is AttendanceViewModel vm)
            {
                await vm.RefreshAsync();
            }
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (DataContext is AttendanceViewModel vm)
            {
                vm.SetCurrentUser(user?.UserId ?? 0, user?.Username ?? "-", user?.RoleName);

                var isEmployee = string.Equals(user?.RoleName, "Employee", StringComparison.OrdinalIgnoreCase);
                ShiftAssignmentActionsColumn.Visibility = isEmployee ? Visibility.Collapsed : Visibility.Visible;

                if (isEmployee)
                {
                    AttendanceTabControl.SelectedItem = DtrTab;
                }
            }
        }
    }
}
