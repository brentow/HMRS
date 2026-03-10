using HRMS.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public class EmployeesViewModel : INotifyPropertyChanged
    {
        private readonly EmployeeDataService _dataService = new(DbConfig.ConnectionString);

        private int _totalEmployees;
        private int _activeEmployees;
        private int _departments;
        private int _positions;
        private string _searchText = string.Empty;
        private EmployeeRowVm? _selectedEmployee;

        public int TotalEmployees { get => _totalEmployees; set { _totalEmployees = value; OnPropertyChanged(); } }
        public int ActiveEmployees { get => _activeEmployees; set { _activeEmployees = value; OnPropertyChanged(); } }
        public int Departments { get => _departments; set { _departments = value; OnPropertyChanged(); } }
        public int Positions { get => _positions; set { _positions = value; OnPropertyChanged(); } }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                _searchText = value;
                OnPropertyChanged();
                EmployeesView.Refresh();
                EnsureSelectedEmployeeVisible();
            }
        }

        public EmployeeRowVm? SelectedEmployee { get => _selectedEmployee; set { _selectedEmployee = value; OnPropertyChanged(); } }

        public ObservableCollection<EmployeeRowVm> Employees { get; } = new();
        public ICollectionView EmployeesView { get; }

        public EmployeesViewModel()
        {
            EmployeesView = CollectionViewSource.GetDefaultView(Employees);
            EmployeesView.Filter = FilterEmployee;
            _ = RefreshAsync();
        }

        public Task RefreshAsync() => LoadAsync();

        private async Task LoadAsync()
        {
            var stats = await _dataService.GetStatsAsync();
            TotalEmployees = stats.TotalEmployees;
            ActiveEmployees = stats.ActiveEmployees;
            Departments = stats.Departments;
            Positions = stats.Positions;

            Employees.Clear();
            var list = await _dataService.GetRecentEmployeesAsync();
            foreach (var e in list)
            {
                Employees.Add(new EmployeeRowVm(
                    e.EmployeeNo,
                    e.Name,
                    e.Department,
                    e.Position,
                    e.HireDate,
                    e.Status,
                    e.AppointmentType,
                    e.SalaryGrade,
                    e.SalaryStep,
                    e.MonthlySalary,
                    e.TinNo,
                    e.GsisBpNo,
                    e.PhilHealthNo,
                    e.PagibigMidNo,
                    e.LastDtrDate,
                    e.LastTimeIn,
                    e.LastTimeOut,
                    e.LastWorkedMinutes
                )
                {
                    StatusColor = e.Status.ToLower() == "active"
                        ? new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E4368"))
                        : new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935"))
                });
            }

            EmployeesView.Refresh();
            SelectedEmployee = EmployeesView.Cast<EmployeeRowVm>().FirstOrDefault();
        }

        private bool FilterEmployee(object obj)
        {
            if (obj is not EmployeeRowVm employee)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            var term = SearchText.Trim();

            return Contains(employee.EmployeeNo, term)
                || Contains(employee.Name, term)
                || Contains(employee.Department, term)
                || Contains(employee.Position, term)
                || Contains(employee.Status, term)
                || Contains(employee.AppointmentType, term)
                || Contains(employee.SalaryGrade, term)
                || Contains(employee.SalaryStep, term)
                || Contains(employee.TinNo, term)
                || Contains(employee.GsisBpNo, term)
                || Contains(employee.PhilHealthNo, term)
                || Contains(employee.PagibigMidNo, term)
                || employee.HireDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)
                    .Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureSelectedEmployeeVisible()
        {
            if (SelectedEmployee != null && EmployeesView.Cast<EmployeeRowVm>().Any(e => ReferenceEquals(e, SelectedEmployee)))
            {
                return;
            }

            SelectedEmployee = EmployeesView.Cast<EmployeeRowVm>().FirstOrDefault();
        }

        private static bool Contains(string? source, string term) =>
            !string.IsNullOrWhiteSpace(source) && source.Contains(term, StringComparison.OrdinalIgnoreCase);

        public void SelectEmployeeByNumber(string? employeeNo)
        {
            if (string.IsNullOrWhiteSpace(employeeNo))
            {
                return;
            }

            var match = Employees.FirstOrDefault(e =>
                string.Equals(e.EmployeeNo, employeeNo, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                return;
            }

            SelectedEmployee = match;
            EmployeesView.MoveCurrentTo(match);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class EmployeeRowVm
    {
        public EmployeeRowVm(
            string employeeNo,
            string name,
            string department,
            string position,
            DateTime hireDate,
            string status,
            string appointmentType,
            string salaryGrade,
            string salaryStep,
            decimal monthlySalary,
            string tinNo,
            string gsisBpNo,
            string philHealthNo,
            string pagibigMidNo,
            DateTime? lastDtrDate,
            TimeSpan? lastTimeIn,
            TimeSpan? lastTimeOut,
            int lastWorkedMinutes)
        {
            EmployeeNo = employeeNo;
            Name = name;
            Department = department;
            Position = position;
            HireDate = hireDate;
            Status = status;
            AppointmentType = string.IsNullOrWhiteSpace(appointmentType) ? "-" : appointmentType;
            SalaryGrade = string.IsNullOrWhiteSpace(salaryGrade) ? "-" : salaryGrade;
            SalaryStep = string.IsNullOrWhiteSpace(salaryStep) ? "-" : salaryStep;
            MonthlySalary = monthlySalary.ToString("N2", CultureInfo.InvariantCulture);
            TinNo = string.IsNullOrWhiteSpace(tinNo) ? "-" : tinNo;
            GsisBpNo = string.IsNullOrWhiteSpace(gsisBpNo) ? "-" : gsisBpNo;
            PhilHealthNo = string.IsNullOrWhiteSpace(philHealthNo) ? "-" : philHealthNo;
            PagibigMidNo = string.IsNullOrWhiteSpace(pagibigMidNo) ? "-" : pagibigMidNo;
            LastDtrDate = lastDtrDate;
            LastTimeIn = lastTimeIn;
            LastTimeOut = lastTimeOut;
            LastWorkedMinutes = lastWorkedMinutes;
        }

        public string EmployeeNo { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }
        public System.DateTime HireDate { get; set; }
        public string Status { get; set; }
        public string AppointmentType { get; set; }
        public string SalaryGrade { get; set; }
        public string SalaryStep { get; set; }
        public string MonthlySalary { get; set; }
        public string TinNo { get; set; }
        public string GsisBpNo { get; set; }
        public string PhilHealthNo { get; set; }
        public string PagibigMidNo { get; set; }
        public DateTime? LastDtrDate { get; set; }
        public TimeSpan? LastTimeIn { get; set; }
        public TimeSpan? LastTimeOut { get; set; }
        public int LastWorkedMinutes { get; set; }
        public string LastDtrDateText => LastDtrDate?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "No DTR yet";
        public string LastTimeInText => LastTimeIn.HasValue ? DateTime.Today.Add(LastTimeIn.Value).ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string LastTimeOutText => LastTimeOut.HasValue ? DateTime.Today.Add(LastTimeOut.Value).ToString("hh:mm tt", CultureInfo.InvariantCulture) : "--";
        public string LastWorkedHoursText => LastWorkedMinutes > 0 ? $"{(LastWorkedMinutes / 60d):0.##} hrs" : "--";
        public Brush? StatusColor { get; set; }
    }
}
