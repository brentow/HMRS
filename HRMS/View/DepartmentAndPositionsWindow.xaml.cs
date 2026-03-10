using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HRMS.Model;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class DepartmentAndPositionsWindow : UserControl
    {
        private DepartmentsViewModel Vm => (DepartmentsViewModel)DataContext;

        public DepartmentAndPositionsWindow()
        {
            InitializeComponent();
            DataContext = new DepartmentsViewModel();
            Loaded += DepartmentAndPositionsWindow_Loaded;
            Unloaded += DepartmentAndPositionsWindow_Unloaded;
            Vm.PropertyChanged += Vm_PropertyChanged;
        }

        private void DepartmentAndPositionsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshSelectors();
        }

        private void DepartmentAndPositionsWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            Vm.PropertyChanged -= Vm_PropertyChanged;
        }

        public async Task RefreshAsync()
        {
            await Vm.RefreshAsync();
            RefreshSelectors();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DepartmentsViewModel.LastRefreshed))
            {
                Dispatcher.Invoke(RefreshSelectors);
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Vm.RefreshAsync();
                RefreshSelectors();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to refresh departments and positions data: {ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void AddDepartment_Click(object sender, RoutedEventArgs e)
        {
            var departmentName = DepartmentNameTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                MessageBox.Show("Enter a department name first.", "Department", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await Vm.AddDepartmentAsync(departmentName);
                DepartmentNameTextBox.Clear();
                RefreshSelectors();
                SystemRefreshBus.Raise("DepartmentAdded");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to add department: {ex.Message}", "Department", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteDepartment_Click(object sender, RoutedEventArgs e)
        {
            var departmentName = DeleteDepartmentComboBox.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                MessageBox.Show("Select a department to delete.", "Department", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                    $"Delete department '{departmentName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                await Vm.DeleteDepartmentAsync(departmentName);
                RefreshSelectors();
                SystemRefreshBus.Raise("DepartmentDeleted");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to delete department: {ex.Message}", "Department", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddPosition_Click(object sender, RoutedEventArgs e)
        {
            var departmentName = PositionDepartmentComboBox.SelectedValue as string;
            var positionName = PositionNameTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(departmentName))
            {
                MessageBox.Show("Select a department first.", "Position", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(positionName))
            {
                MessageBox.Show("Enter a position name.", "Position", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await Vm.AddPositionAsync(departmentName, positionName);
                PositionNameTextBox.Clear();
                ExistingPositionDepartmentComboBox.SelectedValue = departmentName;
                RefreshDeletePositionOptions();
                SystemRefreshBus.Raise("PositionAdded");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to add position: {ex.Message}", "Position", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeletePosition_Click(object sender, RoutedEventArgs e)
        {
            var departmentName = ExistingPositionDepartmentComboBox.SelectedValue as string;
            var positionName = DeletePositionComboBox.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(departmentName))
            {
                MessageBox.Show("Select a department first.", "Position", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(positionName))
            {
                MessageBox.Show("Select a position to delete.", "Position", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                    $"Delete position '{positionName}' from '{departmentName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                await Vm.DeletePositionAsync(departmentName, positionName);
                RefreshDeletePositionOptions();
                SystemRefreshBus.Raise("PositionDeleted");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to delete position: {ex.Message}", "Position", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PositionDepartmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Keep department selections independent across actions.
        }

        private void ExistingPositionDepartmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshDeletePositionOptions();
        }

        private void RefreshSelectors()
        {
            if (Vm.DepartmentRows.Count == 0)
            {
                DeleteDepartmentComboBox.SelectedIndex = -1;
                PositionDepartmentComboBox.SelectedIndex = -1;
                ExistingPositionDepartmentComboBox.SelectedIndex = -1;
                DeletePositionComboBox.ItemsSource = null;
                DeletePositionComboBox.SelectedIndex = -1;
                return;
            }

            var deleteDept = DeleteDepartmentComboBox.SelectedValue as string;
            if (!Vm.DepartmentRows.Any(d => string.Equals(d.Name, deleteDept, StringComparison.OrdinalIgnoreCase)))
            {
                DeleteDepartmentComboBox.SelectedIndex = -1;
            }

            var addPosDept = PositionDepartmentComboBox.SelectedValue as string;
            if (!Vm.DepartmentRows.Any(d => string.Equals(d.Name, addPosDept, StringComparison.OrdinalIgnoreCase)))
            {
                PositionDepartmentComboBox.SelectedIndex = -1;
            }

            var deletePosDept = ExistingPositionDepartmentComboBox.SelectedValue as string;
            if (!Vm.DepartmentRows.Any(d => string.Equals(d.Name, deletePosDept, StringComparison.OrdinalIgnoreCase)))
            {
                ExistingPositionDepartmentComboBox.SelectedIndex = -1;
            }

            RefreshDeletePositionOptions();
        }

        private void RefreshDeletePositionOptions()
        {
            var selectedDepartment = ExistingPositionDepartmentComboBox.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(selectedDepartment))
            {
                DeletePositionComboBox.ItemsSource = null;
                DeletePositionComboBox.SelectedIndex = -1;
                return;
            }

            var positions = Vm.PositionRows
                .Where(p => string.Equals(p.Department, selectedDepartment, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            DeletePositionComboBox.ItemsSource = positions;
            DeletePositionComboBox.SelectedIndex = positions.Count > 0 ? 0 : -1;
        }

        private void ClearDeleteDepartmentSelection_Click(object sender, RoutedEventArgs e)
        {
            DeleteDepartmentComboBox.SelectedIndex = -1;
        }

        private void ClearAddPositionDepartmentSelection_Click(object sender, RoutedEventArgs e)
        {
            PositionDepartmentComboBox.SelectedIndex = -1;
        }

        private void ClearDeletePositionDepartmentSelection_Click(object sender, RoutedEventArgs e)
        {
            ExistingPositionDepartmentComboBox.SelectedIndex = -1;
            RefreshDeletePositionOptions();
        }

        private void ClearDeletePositionSelection_Click(object sender, RoutedEventArgs e)
        {
            DeletePositionComboBox.SelectedIndex = -1;
        }
    }
}
