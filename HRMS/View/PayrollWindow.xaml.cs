using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HRMS.Model;
using HRMS.ViewModel;
using System;

namespace HRMS.View
{
    public partial class PayrollWindow : UserControl
    {
        private readonly PayrollViewModel _viewModel;

        public PayrollWindow()
        {
            InitializeComponent();
            _viewModel = new PayrollViewModel();
            DataContext = _viewModel;
            PayrollTabControl.SelectedItem = PayrollRunsTab;
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (user == null)
            {
                _viewModel.SetCurrentUser(0, string.Empty, null);
                ApplyEmployeeVisibility(false);
                return;
            }

            _viewModel.SetCurrentUser(user.UserId, user.Username, user.RoleName);
            ApplyEmployeeVisibility(string.Equals(user.RoleName, "Employee", StringComparison.OrdinalIgnoreCase));
        }

        public async Task RefreshAsync()
        {
            await _viewModel.RefreshAsync();
        }

        public void ShowPayrollTab()
        {
            PayrollTabControl.SelectedItem = PayrollRunsTab;
        }

        public void ShowPayslipTab()
        {
            PayrollTabControl.SelectedItem = PayslipReleasesTab;
        }

        public void ShowDeductionsTab()
        {
            PayrollTabControl.SelectedItem = DeductionsTab;
        }

        private void PayrollRunActionsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: PayrollRunVm row } button)
            {
                return;
            }

            var menu = new ContextMenu
            {
                Style = (Style)FindResource("PayrollContextMenuStyle"),
                PlacementTarget = button,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Left,
                HorizontalOffset = -6
            };
            menu.Resources[typeof(MenuItem)] = FindResource("PayrollMenuItemStyle");

            if (_viewModel.IsAdminOrHrMode && row.CanApprovePayroll)
            {
                menu.Items.Add(CreatePayrollMenuItem("Approve Payroll", _viewModel.ApproveRunCommand, row));
            }

            if (_viewModel.IsAdminOrHrMode && row.CanReleasePayslip)
            {
                menu.Items.Add(CreatePayrollMenuItem("Release Payslip", _viewModel.ReleasePayslipCommand, row));
            }

            if (row.CanOpenPayslip)
            {
                menu.Items.Add(CreatePayrollMenuItem("Download PDF", _viewModel.DownloadPayslipCommand, row));
                menu.Items.Add(CreatePayrollMenuItem("Print Payslip", _viewModel.PrintPayslipCommand, row));
            }

            if (menu.Items.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "No actions available", IsEnabled = false });
            }

            button.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private static MenuItem CreatePayrollMenuItem(string header, System.Windows.Input.ICommand command, PayrollRunVm row) =>
            new()
            {
                Header = header,
                Command = command,
                CommandParameter = row
            };

        private void ApplyEmployeeVisibility(bool isEmployee)
        {
            var employeeVisibility = isEmployee ? Visibility.Collapsed : Visibility.Visible;

            if (PayrollRunEmployeeColumn != null)
            {
                PayrollRunEmployeeColumn.Visibility = employeeVisibility;
            }

            if (PayslipReleaseEmpNoColumn != null)
            {
                PayslipReleaseEmpNoColumn.Visibility = employeeVisibility;
            }

            if (PayslipReleaseEmployeeColumn != null)
            {
                PayslipReleaseEmployeeColumn.Visibility = employeeVisibility;
            }
        }

    }
}
