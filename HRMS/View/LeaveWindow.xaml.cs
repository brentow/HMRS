using HRMS.ViewModel;
using HRMS.Model;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System;

namespace HRMS.View
{
    public partial class LeaveWindow : UserControl
    {
        public LeaveWindow()
        {
            InitializeComponent();
            var viewModel = new LeaveViewModel();
            viewModel.LeaveRequestSaved += LeaveViewModel_OnLeaveRequestSaved;
            DataContext = viewModel;
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

            var isEmployee = string.Equals(user?.RoleName, "Employee", StringComparison.OrdinalIgnoreCase);
            var employeeVisibility = isEmployee ? Visibility.Collapsed : Visibility.Visible;

            if (FileLeaveEmployeeLabel != null)
            {
                FileLeaveEmployeeLabel.Visibility = employeeVisibility;
            }

            if (FileLeaveEmployeeComboBox != null)
            {
                FileLeaveEmployeeComboBox.Visibility = employeeVisibility;
            }

            if (LeaveRequestEmployeeColumn != null)
            {
                LeaveRequestEmployeeColumn.Visibility = employeeVisibility;
            }

            if (LeaveBalanceEmpNoColumn != null)
            {
                LeaveBalanceEmpNoColumn.Visibility = employeeVisibility;
            }

            if (LeaveBalanceEmployeeColumn != null)
            {
                LeaveBalanceEmployeeColumn.Visibility = employeeVisibility;
            }

            if (LeaveAttachmentEmpNoColumn != null)
            {
                LeaveAttachmentEmpNoColumn.Visibility = employeeVisibility;
            }

            if (LeaveAttachmentEmployeeColumn != null)
            {
                LeaveAttachmentEmployeeColumn.Visibility = employeeVisibility;
            }
        }

        private void OpenFileLeavePopup_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is LeaveViewModel vm)
            {
                vm.BeginNewLeaveRequest();
            }

            FileLeavePopup.IsOpen = true;
        }

        private void EditLeaveRequest_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is LeaveViewModel vm && vm.BeginEditSelectedRequest())
            {
                FileLeavePopup.IsOpen = true;
            }
        }

        private void CloseFileLeavePopup_OnClick(object sender, RoutedEventArgs e)
        {
            FileLeavePopup.IsOpen = false;
        }

        private void LeaveViewModel_OnLeaveRequestSaved(object? sender, EventArgs e)
        {
            FileLeavePopup.IsOpen = false;
        }
    }
}
