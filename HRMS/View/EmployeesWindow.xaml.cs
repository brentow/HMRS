using HRMS.Model;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class EmployeesWindow : UserControl
    {
        public static readonly DependencyProperty IsEditModeProperty =
            DependencyProperty.Register(
                nameof(IsEditMode),
                typeof(bool),
                typeof(EmployeesWindow),
                new PropertyMetadata(false));

        public bool IsEditMode
        {
            get => (bool)GetValue(IsEditModeProperty);
            set => SetValue(IsEditModeProperty, value);
        }

        private readonly EmployeeDataService _employeeDataService = new(DbConfig.ConnectionString);
        private bool _isEditMode;
        private string _editingEmployeeNo = string.Empty;
        private IReadOnlyList<LookupItemDto> _departmentLookups = Array.Empty<LookupItemDto>();
        private IReadOnlyList<PositionLookupDto> _positionLookups = Array.Empty<PositionLookupDto>();
        private IReadOnlyList<LookupItemDto> _appointmentTypeLookups = Array.Empty<LookupItemDto>();
        private IReadOnlyList<int> _salaryGradeLookups = Array.Empty<int>();
        private static readonly IReadOnlyList<int> SalaryStepLookups = Enumerable.Range(1, 8).ToArray();
        private bool _suppressProfileLookupEvents;

        public EmployeesWindow()
        {
            InitializeComponent();
            DataContext = new EmployeesViewModel();
            SetEditMode(false);
        }

        public async Task RefreshAsync()
        {
            if (DataContext is EmployeesViewModel vm)
            {
                await vm.RefreshAsync();
            }

            await AddEmployeeForm.RefreshReferenceDataAsync();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not EmployeesViewModel vm || vm.SelectedEmployee == null)
            {
                return;
            }

            _editingEmployeeNo = vm.SelectedEmployee.EmployeeNo;

            if (_isEditMode)
            {
                SyncProfileEditorsToSelectedEmployee();
            }
        }

        private async void AddEmployeeButton_OnClick(object sender, RoutedEventArgs e)
        {
            await AddEmployeeForm.PrepareForCreateAsync();
            AddEmployeeHost.Visibility = Visibility.Visible;
        }

        private async void AddEmployeeForm_EmployeeSaved(object sender, System.EventArgs e)
        {
            AddEmployeeHost.Visibility = Visibility.Collapsed;
            if (DataContext is EmployeesViewModel vm)
            {
                await vm.RefreshAsync();
            }
        }

        private void AddEmployeeForm_Cancelled(object sender, System.EventArgs e)
        {
            AddEmployeeHost.Visibility = Visibility.Collapsed;
        }

        private async void EditProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not EmployeesViewModel vm || vm.SelectedEmployee == null)
            {
                MessageBox.Show(
                    "Please select an employee first before editing profile.",
                    "Select Employee",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!_isEditMode)
            {
                _editingEmployeeNo = vm.SelectedEmployee.EmployeeNo;
                try
                {
                    await LoadProfileLookupsAsync();
                    SyncProfileEditorsToSelectedEmployee();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Unable to load departments and positions: {ex.Message}",
                        "Reference Data Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                SetEditMode(true);
                return;
            }

            SetEditMode(false);
            await vm.RefreshAsync();
        }

        private async void SaveProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not EmployeesViewModel vm || vm.SelectedEmployee == null)
            {
                MessageBox.Show(
                    "Please select an employee first before saving.",
                    "Select Employee",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            UpdateSelectedEmployeeFromProfileSelectors();
            UpdateSelectedEmployeeFromSalarySelectors();
            var selected = vm.SelectedEmployee;
            var update = new UpdateEmployeeProfileDto(
                OriginalEmployeeNo: _editingEmployeeNo,
                EmployeeNo: selected.EmployeeNo,
                FullName: selected.Name,
                DepartmentName: selected.Department,
                PositionName: selected.Position,
                Status: selected.Status,
                AppointmentTypeName: selected.AppointmentType,
                SalaryGradeText: selected.SalaryGrade,
                SalaryStepText: selected.SalaryStep,
                HireDate: selected.HireDate,
                TinNo: selected.TinNo,
                GsisBpNo: selected.GsisBpNo,
                PhilHealthNo: selected.PhilHealthNo,
                PagibigMidNo: selected.PagibigMidNo);

            try
            {
                await _employeeDataService.UpdateEmployeeProfileAsync(update);
                var newEmployeeNo = selected.EmployeeNo;

                await vm.RefreshAsync();
                vm.SelectEmployeeByNumber(newEmployeeNo);
                _editingEmployeeNo = newEmployeeNo;
                SetEditMode(false);
                SystemRefreshBus.Raise("EmployeeUpdated");
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                MessageBox.Show(
                    "Employee No already exists. Please use a unique value.",
                    "Duplicate Employee No",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to save profile: {ex.Message}",
                    "Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SetEditMode(bool enabled)
        {
            _isEditMode = enabled;
            IsEditMode = enabled;
            SaveProfileButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            ProfileDepartmentTextBox.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            ProfilePositionTextBox.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            ProfileDepartmentComboBox.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            ProfilePositionComboBox.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            SalaryAppointmentTypeTextBox.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            SalaryStepTextBox.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            SalaryGradeTextBox.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            SalaryAppointmentTypeComboBox.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            SalaryStepComboBox.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            SalaryGradeComboBox.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            EditProfileIcon.Kind = enabled
                ? MaterialDesignThemes.Wpf.PackIconKind.CloseCircleOutline
                : MaterialDesignThemes.Wpf.PackIconKind.AccountEditOutline;
            EditProfileText.Text = enabled ? "Cancel Edit" : "Edit Profile";

            if (enabled)
            {
                SyncProfileEditorsToSelectedEmployee();
            }

            ApplyEditModeToDetailsControls();
            Dispatcher.BeginInvoke(new Action(ApplyEditModeToDetailsControls), DispatcherPriority.Loaded);
        }

        private async Task LoadProfileLookupsAsync()
        {
            var departmentsTask = _employeeDataService.GetDepartmentsLookupAsync();
            var positionsTask = _employeeDataService.GetPositionsLookupAsync();
            var appointmentTypesTask = _employeeDataService.GetAppointmentTypesLookupAsync();
            var salaryGradesTask = _employeeDataService.GetSalaryGradesAsync();

            _departmentLookups = await departmentsTask;
            _positionLookups = await positionsTask;
            _appointmentTypeLookups = await appointmentTypesTask;
            _salaryGradeLookups = await salaryGradesTask;

            _suppressProfileLookupEvents = true;
            ProfileDepartmentComboBox.ItemsSource = _departmentLookups;
            SalaryAppointmentTypeComboBox.ItemsSource = _appointmentTypeLookups;
            SalaryGradeComboBox.ItemsSource = _salaryGradeLookups.Select(grade => $"SG-{grade}").ToList();
            SalaryStepComboBox.ItemsSource = SalaryStepLookups.Select(step => step.ToString(CultureInfo.InvariantCulture)).ToList();
            _suppressProfileLookupEvents = false;
        }

        private void SyncProfileEditorsToSelectedEmployee()
        {
            if (DataContext is not EmployeesViewModel vm || vm.SelectedEmployee == null)
            {
                ProfileDepartmentComboBox.SelectedIndex = -1;
                ProfilePositionComboBox.ItemsSource = null;
                ProfilePositionComboBox.SelectedIndex = -1;
                SalaryAppointmentTypeComboBox.SelectedIndex = -1;
                SalaryGradeComboBox.SelectedIndex = -1;
                SalaryStepComboBox.SelectedIndex = -1;
                return;
            }

            _suppressProfileLookupEvents = true;

            var selectedDepartment = NormalizeLookupText(vm.SelectedEmployee.Department);
            var selectedPosition = NormalizeLookupText(vm.SelectedEmployee.Position);
            var selectedAppointmentType = NormalizeLookupText(vm.SelectedEmployee.AppointmentType);
            var selectedSalaryGrade = NormalizeSalaryGradeText(vm.SelectedEmployee.SalaryGrade);
            var selectedSalaryStep = NormalizeStepText(vm.SelectedEmployee.SalaryStep);

            ProfileDepartmentComboBox.SelectedValue = selectedDepartment;
            RefreshProfilePositionItems(selectedPosition);
            SalaryAppointmentTypeComboBox.SelectedValue = selectedAppointmentType;
            SalaryGradeComboBox.SelectedItem = selectedSalaryGrade;
            SalaryStepComboBox.SelectedItem = selectedSalaryStep;

            _suppressProfileLookupEvents = false;
            UpdateSelectedEmployeeFromProfileSelectors();
            UpdateSelectedEmployeeFromSalarySelectors();
        }

        private void RefreshProfilePositionItems(string? preferredPosition = null)
        {
            var selectedDepartmentId = (ProfileDepartmentComboBox.SelectedItem as LookupItemDto)?.Id;
            IEnumerable<PositionLookupDto> filtered = _positionLookups;

            if (selectedDepartmentId.HasValue)
            {
                filtered = filtered.Where(p => !p.DepartmentId.HasValue || p.DepartmentId.Value == selectedDepartmentId.Value);
            }

            var list = filtered.OrderBy(p => p.Name).ToList();
            ProfilePositionComboBox.ItemsSource = list;

            var target = NormalizeLookupText(preferredPosition);
            if (string.IsNullOrWhiteSpace(target) && DataContext is EmployeesViewModel vm && vm.SelectedEmployee != null)
            {
                target = NormalizeLookupText(vm.SelectedEmployee.Position);
            }

            var matched = string.IsNullOrWhiteSpace(target)
                ? null
                : list.FirstOrDefault(p => string.Equals(p.Name, target, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                ProfilePositionComboBox.SelectedItem = matched;
                return;
            }

            ProfilePositionComboBox.SelectedIndex = list.Count > 0 ? 0 : -1;
        }

        private void ProfileDepartmentComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressProfileLookupEvents || !_isEditMode)
            {
                return;
            }

            _suppressProfileLookupEvents = true;
            RefreshProfilePositionItems();
            _suppressProfileLookupEvents = false;
            UpdateSelectedEmployeeFromProfileSelectors();
        }

        private void ProfilePositionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressProfileLookupEvents || !_isEditMode)
            {
                return;
            }

            UpdateSelectedEmployeeFromProfileSelectors();
        }

        private void SalaryAppointmentTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressProfileLookupEvents || !_isEditMode)
            {
                return;
            }

            UpdateSelectedEmployeeFromSalarySelectors();
        }

        private void SalaryGradeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressProfileLookupEvents || !_isEditMode)
            {
                return;
            }

            UpdateSelectedEmployeeFromSalarySelectors();
        }

        private void SalaryStepComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressProfileLookupEvents || !_isEditMode)
            {
                return;
            }

            UpdateSelectedEmployeeFromSalarySelectors();
        }

        private void UpdateSelectedEmployeeFromProfileSelectors()
        {
            if (DataContext is not EmployeesViewModel vm || vm.SelectedEmployee == null)
            {
                return;
            }

            vm.SelectedEmployee.Department = (ProfileDepartmentComboBox.SelectedItem as LookupItemDto)?.Name ?? "-";
            vm.SelectedEmployee.Position = (ProfilePositionComboBox.SelectedItem as PositionLookupDto)?.Name ?? "-";
        }

        private void UpdateSelectedEmployeeFromSalarySelectors()
        {
            if (DataContext is not EmployeesViewModel vm || vm.SelectedEmployee == null)
            {
                return;
            }

            vm.SelectedEmployee.AppointmentType = (SalaryAppointmentTypeComboBox.SelectedItem as LookupItemDto)?.Name ?? "-";
            vm.SelectedEmployee.SalaryGrade = SalaryGradeComboBox.SelectedItem?.ToString() ?? "-";
            vm.SelectedEmployee.SalaryStep = SalaryStepComboBox.SelectedItem?.ToString() ?? "-";
        }

        private void ApplyEditModeToDetailsControls()
        {
            foreach (var textBox in FindVisualChildren<TextBox>(EmployeeDetailsCard))
            {
                textBox.IsReadOnly = !_isEditMode;
                textBox.IsReadOnlyCaretVisible = _isEditMode;
                textBox.Focusable = _isEditMode;
                textBox.Cursor = _isEditMode ? Cursors.IBeam : Cursors.Arrow;
            }

            foreach (var datePicker in FindVisualChildren<DatePicker>(EmployeeDetailsCard))
            {
                datePicker.IsHitTestVisible = _isEditMode;
                datePicker.Focusable = _isEditMode;
            }
        }

        private void EmployeeDetailsTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, e.Source))
            {
                return;
            }

            ApplyEditModeToDetailsControls();
        }

        private static string? NormalizeLookupText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var value = text.Trim();
            return value == "-" ? null : value;
        }

        private static string? NormalizeSalaryGradeText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var digits = new string(text.Where(char.IsDigit).ToArray());
            return string.IsNullOrWhiteSpace(digits) ? null : $"SG-{digits}";
        }

        private static string? NormalizeStepText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var digits = new string(text.Where(char.IsDigit).ToArray());
            return string.IsNullOrWhiteSpace(digits) ? null : digits;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
            {
                yield break;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }
}
