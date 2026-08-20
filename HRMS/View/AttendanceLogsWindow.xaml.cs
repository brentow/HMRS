using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HRMS.Model;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class AttendanceLogsWindow : UserControl
    {
        public AttendanceLogsWindow()
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
            }
        }

        private void ViewEmployeeLogsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AttendanceViewModel vm || sender is not FrameworkElement element)
            {
                return;
            }

            if (element.DataContext is not AttendanceEmployeeLogSummaryVm summary)
            {
                return;
            }

            var rows = vm.GetEmployeeDailyLogs(summary.EmployeeNo);
            var owner = Window.GetWindow(this);
            var window = new EmployeeAttendanceLogsWindow(summary, rows)
            {
                Owner = owner
            };

            window.ShowDialog();
        }
    }
}
