using System;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
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
                var employeeVisibility = isEmployee ? Visibility.Collapsed : Visibility.Visible;

                if (DtrEmployeeLabel != null)
                {
                    DtrEmployeeLabel.Visibility = employeeVisibility;
                }

                if (DtrEmployeeComboBox != null)
                {
                    DtrEmployeeComboBox.Visibility = employeeVisibility;
                }

                if (DtrEmployeeNoColumn != null)
                {
                    DtrEmployeeNoColumn.Visibility = employeeVisibility;
                }

                if (DtrEmployeeNameColumn != null)
                {
                    DtrEmployeeNameColumn.Visibility = employeeVisibility;
                }

                if (AttendanceRemarkEmployeeColumn != null)
                {
                    AttendanceRemarkEmployeeColumn.Visibility = employeeVisibility;
                }

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
                vm.SelectedAttendanceRemarkFilter = "TO";
            }
        }

        public void ShowHolidaysTab()
        {
            AttendanceTabControl.SelectedItem = AttendanceRemarksTab;
            if (DataContext is AttendanceViewModel vm)
            {
                vm.SelectedRemarkType = "HOLIDAY";
                vm.SelectedAttendanceRemarkFilter = "HOLIDAY";
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

        private void NewRemarkPopup_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is AttendanceViewModel vm)
            {
                vm.BeginNewAttendanceRemark();
            }

            RemarkPopup.IsOpen = true;
        }

        private void EditRemarkButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is AttendanceViewModel vm &&
                sender is FrameworkElement element &&
                element.DataContext is AttendanceRemarkVm remark)
            {
                vm.BeginEditAttendanceRemark(remark);
                RemarkPopup.IsOpen = true;
            }

            e.Handled = true;
        }

        private void AttendanceRemarksGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AttendanceViewModel vm && vm.SelectedAttendanceRemark != null)
            {
                vm.BeginEditAttendanceRemark(vm.SelectedAttendanceRemark);
                RemarkPopup.IsOpen = true;
                e.Handled = true;
            }
        }

        private void CloseRemarkPopup_OnClick(object sender, RoutedEventArgs e)
        {
            RemarkPopup.IsOpen = false;
        }

        private async void ShowDtrButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AttendanceViewModel vm || sender is not FrameworkElement element)
            {
                return;
            }

            if (element.DataContext is not DtrEmployeeSummaryVm summary)
            {
                return;
            }

            try
            {
                vm.SelectedDtrEmployeeSummary = summary;
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                var rows = await vm.GetEmployeeDtrRowsForViewAsync(summary.EmployeeId);
                var form = new EmployeeDtrWindow(summary, vm.SelectedDtrYear, vm.SelectedDtrMonth, rows);
                form.OpenPdfPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    $"Unable to open DTR: {ex.Message}",
                    "Employee DTR",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
            }
        }
    }
}
