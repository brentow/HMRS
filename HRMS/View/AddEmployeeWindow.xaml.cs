using HRMS.Model;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class AddEmployeeWindow : UserControl
    {
        private readonly EmployeeDataService _employeeDataService = new(DbConfig.ConnectionString);
        private IReadOnlyList<PositionLookupDto> _allPositions = Array.Empty<PositionLookupDto>();
        private bool _lookupsLoaded;

        public event EventHandler? EmployeeSaved;
        public event EventHandler? Cancelled;

        public AddEmployeeWindow()
        {
            InitializeComponent();
            Loaded += AddEmployeeWindow_Loaded;
        }

        private async void AddEmployeeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadLookupsAsync(forceReload: false);
            if (string.IsNullOrWhiteSpace(EmployeeNoBox.Text))
            {
                await SetDefaultEmployeeNoAsync();
            }
        }

        public async System.Threading.Tasks.Task RefreshReferenceDataAsync()
        {
            await LoadLookupsAsync(forceReload: true);
        }

        public async Task PrepareForCreateAsync()
        {
            await LoadLookupsAsync(forceReload: true);
            await ResetFormAsync();
        }

        private async System.Threading.Tasks.Task LoadLookupsAsync(bool forceReload)
        {
            if (_lookupsLoaded && !forceReload)
            {
                return;
            }

            try
            {
                var selectedDepartmentId = ToNullableInt(DepartmentBox.SelectedValue);
                var selectedPositionId = ToNullableInt(PositionBox.SelectedValue);
                var selectedAppointmentTypeId = ToNullableInt(AppointmentTypeBox.SelectedValue);
                var selectedSalaryGrade = ParseNullableIntFromObject(SalaryGradeBox.SelectedItem, SalaryGradeBox.Text);
                var selectedStepNo = ParseNullableIntFromObject(StepNoBox.SelectedItem, StepNoBox.Text);

                var departmentsTask = _employeeDataService.GetDepartmentsLookupAsync();
                var positionsTask = _employeeDataService.GetPositionsLookupAsync();
                var appointmentTypesTask = _employeeDataService.GetAppointmentTypesLookupAsync();
                var salaryGradesTask = _employeeDataService.GetSalaryGradesAsync();

                var departments = await departmentsTask;
                _allPositions = await positionsTask;
                var appointmentTypes = await appointmentTypesTask;
                var salaryGrades = await salaryGradesTask;

                DepartmentBox.ItemsSource = departments;
                AppointmentTypeBox.ItemsSource = appointmentTypes;
                SalaryGradeBox.ItemsSource = salaryGrades;
                StepNoBox.ItemsSource = Enumerable.Range(1, 8).ToArray();

                if (departments.Count > 0)
                {
                    if (selectedDepartmentId.HasValue && departments.Any(d => d.Id == selectedDepartmentId.Value))
                    {
                        DepartmentBox.SelectedValue = selectedDepartmentId.Value;
                    }
                    else
                    {
                        DepartmentBox.SelectedIndex = 0;
                    }
                }

                RefreshPositionItems(selectedPositionId);

                if (appointmentTypes.Count > 0)
                {
                    if (selectedAppointmentTypeId.HasValue && appointmentTypes.Any(a => a.Id == selectedAppointmentTypeId.Value))
                    {
                        AppointmentTypeBox.SelectedValue = selectedAppointmentTypeId.Value;
                    }
                    else
                    {
                        AppointmentTypeBox.SelectedIndex = 0;
                    }
                }

                if (salaryGrades.Count > 0)
                {
                    if (selectedSalaryGrade.HasValue && salaryGrades.Contains(selectedSalaryGrade.Value))
                    {
                        SalaryGradeBox.SelectedItem = selectedSalaryGrade.Value;
                    }
                    else
                    {
                        SalaryGradeBox.SelectedIndex = 0;
                    }
                }

                if (StepNoBox.Items.Count > 0)
                {
                    if (selectedStepNo.HasValue && selectedStepNo.Value >= 1 && selectedStepNo.Value <= StepNoBox.Items.Count)
                    {
                        StepNoBox.SelectedItem = selectedStepNo.Value;
                    }
                    else
                    {
                        StepNoBox.SelectedIndex = 0;
                    }
                }

                if (SexBox.SelectedIndex < 0 && SexBox.Items.Count > 0)
                {
                    SexBox.SelectedIndex = 0;
                }

                if (CivilStatusBox.SelectedIndex < 0 && CivilStatusBox.Items.Count > 0)
                {
                    CivilStatusBox.SelectedIndex = 0;
                }

                if (!HireDatePicker.SelectedDate.HasValue)
                {
                    HireDatePicker.SelectedDate = DateTime.Today;
                }

                _lookupsLoaded = true;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load reference data: {ex.Message}");
            }
        }

        private void DepartmentBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshPositionItems(preferredPositionId: null);
        }

        private void RefreshPositionItems(int? preferredPositionId)
        {
            if (_allPositions.Count == 0)
            {
                PositionBox.ItemsSource = null;
                return;
            }

            var departmentId = ToNullableInt(DepartmentBox.SelectedValue);
            IEnumerable<PositionLookupDto> filtered = _allPositions;

            if (departmentId.HasValue)
            {
                filtered = _allPositions.Where(p => !p.DepartmentId.HasValue || p.DepartmentId.Value == departmentId.Value);
            }

            var items = filtered.ToList();
            PositionBox.ItemsSource = items;

            if (preferredPositionId.HasValue)
            {
                var match = items.FirstOrDefault(p => p.PositionId == preferredPositionId.Value);
                if (match != null)
                {
                    PositionBox.SelectedValue = match.PositionId;
                    return;
                }
            }

            PositionBox.SelectedIndex = items.Count > 0 ? 0 : -1;
        }

        private async void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            ShowError(string.Empty);

            var employeeNo = (EmployeeNoBox.Text ?? string.Empty).Trim();
            var lastName = (LastNameBox.Text ?? string.Empty).Trim();
            var firstName = (FirstNameBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(employeeNo))
            {
                ShowError("Employee No is required.");
                EmployeeNoBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
            {
                ShowError("First Name and Last Name are required.");
                FirstNameBox.Focus();
                return;
            }

            var departmentId = ToNullableInt(DepartmentBox.SelectedValue);
            var positionId = ToNullableInt(PositionBox.SelectedValue);
            var appointmentTypeId = ToNullableInt(AppointmentTypeBox.SelectedValue);

            if (!departmentId.HasValue || !positionId.HasValue || !appointmentTypeId.HasValue)
            {
                ShowError("Department, Position, and Appointment Type are required.");
                return;
            }

            if (!HireDatePicker.SelectedDate.HasValue)
            {
                ShowError("Hire Date is required.");
                HireDatePicker.Focus();
                return;
            }

            var sex = (SexBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var civilStatus = (CivilStatusBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? CivilStatusBox.Text;

            var dto = new NewEmployeeDto(
                EmployeeNo: employeeNo,
                LastName: lastName,
                FirstName: firstName,
                MiddleName: MiddleNameBox.Text,
                Sex: sex,
                BirthDate: BirthDatePicker.SelectedDate,
                CivilStatus: civilStatus,
                Email: EmailBox.Text,
                ContactNumber: ContactNumberBox.Text,
                Address: AddressBox.Text,
                DepartmentId: departmentId,
                PositionId: positionId,
                AppointmentTypeId: appointmentTypeId,
                SalaryGrade: ParseNullableIntFromObject(SalaryGradeBox.SelectedItem, SalaryGradeBox.Text),
                StepNo: ParseNullableIntFromObject(StepNoBox.SelectedItem, StepNoBox.Text),
                HireDate: HireDatePicker.SelectedDate,
                TinNo: TinBox.Text,
                GsisBpNo: GsisBox.Text,
                PhilHealthNo: PhilHealthBox.Text,
                PagibigMidNo: PagibigBox.Text,
                EmergencyContact: EmergencyContactBox.Text,
                EmergencyPhone: EmergencyPhoneBox.Text);

            try
            {
                var employeeId = await _employeeDataService.AddEmployeeAsync(dto);
                await _employeeDataService.CreateDefaultUserAccountForEmployeeAsync(
                    employeeId,
                    dto.EmployeeNo,
                    dto.FirstName,
                    dto.LastName,
                    dto.Email);
                SystemRefreshBus.Raise("EmployeeAdded");
                EmployeeSaved?.Invoke(this, EventArgs.Empty);
                await ResetFormAsync();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                ShowError("Employee No already exists. Please use a unique Employee No.");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to add employee: {ex.Message}");
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        public void ResetForm()
        {
            _ = ResetFormAsync();
        }

        public async Task ResetFormAsync()
        {
            EmployeeNoBox.Clear();
            LastNameBox.Clear();
            FirstNameBox.Clear();
            MiddleNameBox.Clear();
            EmailBox.Clear();
            ContactNumberBox.Clear();
            AddressBox.Clear();
            TinBox.Clear();
            GsisBox.Clear();
            PhilHealthBox.Clear();
            PagibigBox.Clear();
            EmergencyContactBox.Clear();
            EmergencyPhoneBox.Clear();
            BirthDatePicker.SelectedDate = null;
            HireDatePicker.SelectedDate = DateTime.Today;

            if (DepartmentBox.Items.Count > 0)
            {
                DepartmentBox.SelectedIndex = 0;
            }

            if (AppointmentTypeBox.Items.Count > 0)
            {
                AppointmentTypeBox.SelectedIndex = 0;
            }

            if (SalaryGradeBox.Items.Count > 0)
            {
                SalaryGradeBox.SelectedIndex = 0;
            }

            if (StepNoBox.Items.Count > 0)
            {
                StepNoBox.SelectedIndex = 0;
            }

            if (SexBox.Items.Count > 0)
            {
                SexBox.SelectedIndex = 0;
            }

            if (CivilStatusBox.Items.Count > 0)
            {
                CivilStatusBox.SelectedIndex = 0;
            }

            ShowError(string.Empty);
            await SetDefaultEmployeeNoAsync();
        }

        private async Task SetDefaultEmployeeNoAsync()
        {
            try
            {
                EmployeeNoBox.Text = await _employeeDataService.GetNextEmployeeNoAsync();
            }
            catch
            {
                // keep manual entry possible if sequence cannot be read
            }
        }

        private static int? ToNullableInt(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int?)null;
        }

        private static int? ParseNullableIntFromObject(object? selectedItem, string? fallbackText)
        {
            var selected = ToNullableInt(selectedItem);
            if (selected.HasValue)
            {
                return selected;
            }

            if (string.IsNullOrWhiteSpace(fallbackText))
            {
                return null;
            }

            return int.TryParse(fallbackText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int?)null;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
        }
    }
}
