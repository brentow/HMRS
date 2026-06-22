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

        public void ShowAttendanceTab()
        {
            AttendanceTabControl.SelectedItem = DtrTab;
        }

        public void ShowTravelOrderTab()
        {
            AttendanceTabControl.SelectedItem = AttendanceRemarksTab;
            if (DataContext is AttendanceViewModel vm)
            {
                vm.SelectedRemarkType = "TO";
            }
        }

        public void ShowHolidaysTab()
        {
            AttendanceTabControl.SelectedItem = AttendanceRemarksTab;
            if (DataContext is AttendanceViewModel vm)
            {
                vm.SelectedRemarkType = "HOLIDAY";
            }
        }

        private void OpenShiftPopup_OnClick(object sender, RoutedEventArgs e)
        {
            ShiftPopup.IsOpen = true;
        }

        private void CloseShiftPopup_OnClick(object sender, RoutedEventArgs e)
        {
            ShiftPopup.IsOpen = false;
        }

        private void OpenAssignmentPopup_OnClick(object sender, RoutedEventArgs e)
        {
            AssignmentPopup.IsOpen = true;
        }

        private void CloseAssignmentPopup_OnClick(object sender, RoutedEventArgs e)
        {
            AssignmentPopup.IsOpen = false;
        }

        private void OpenRemarkPopup_OnClick(object sender, RoutedEventArgs e)
        {
            RemarkPopup.IsOpen = true;
        }

        private void CloseRemarkPopup_OnClick(object sender, RoutedEventArgs e)
        {
            RemarkPopup.IsOpen = false;
        }
    }
}
