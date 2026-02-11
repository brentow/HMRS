using HRMS.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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

        public int TotalEmployees { get => _totalEmployees; set { _totalEmployees = value; OnPropertyChanged(); } }
        public int ActiveEmployees { get => _activeEmployees; set { _activeEmployees = value; OnPropertyChanged(); } }
        public int Departments { get => _departments; set { _departments = value; OnPropertyChanged(); } }
        public int Positions { get => _positions; set { _positions = value; OnPropertyChanged(); } }

        public ObservableCollection<EmployeeRowVm> Employees { get; } = new();

        public EmployeesViewModel()
        {
            _ = LoadAsync();
        }

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
                    e.Status
                )
                {
                    StatusColor = e.Status.ToLower() == "active"
                        ? new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E4368"))
                        : new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935"))
                });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class EmployeeRowVm
    {
        public EmployeeRowVm(string employeeNo, string name, string department, string position, System.DateTime hireDate, string status)
        {
            EmployeeNo = employeeNo;
            Name = name;
            Department = department;
            Position = position;
            HireDate = hireDate;
            Status = status;
        }

        public string EmployeeNo { get; }
        public string Name { get; }
        public string Department { get; }
        public string Position { get; }
        public System.DateTime HireDate { get; }
        public string Status { get; }
        public Brush? StatusColor { get; set; }
    }
}
