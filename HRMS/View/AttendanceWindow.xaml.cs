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
        private static readonly GridLength AdminDtrDailyHeight = new(1.1, GridUnitType.Star);
        private static readonly GridLength AdminDtrCertificationHeight = new(0.9, GridUnitType.Star);
        private static readonly GridLength EmployeeDtrDailyHeight = new(1, GridUnitType.Star);
        private static readonly GridLength HiddenDtrCertificationHeight = new(0);

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
                DtrDailyRowDefinition.Height = isEmployee ? EmployeeDtrDailyHeight : AdminDtrDailyHeight;
                DtrCertificationRowDefinition.Height = isEmployee ? HiddenDtrCertificationHeight : AdminDtrCertificationHeight;

                if (isEmployee)
                {
                    AttendanceTabControl.SelectedItem = DtrTab;
                }
            }
        }
    }
}
