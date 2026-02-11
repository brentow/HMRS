using HRMS.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HRMS.ViewModel
{
    public class DepartmentsViewModel : INotifyPropertyChanged
    {
        private readonly DepartmentsDataService _dataService = new(DbConfig.ConnectionString);

        private int _departments;
        private int _positions;
        private int _employees;

        public int Departments { get => _departments; set { _departments = value; OnPropertyChanged(); } }
        public int Positions { get => _positions; set { _positions = value; OnPropertyChanged(); } }
        public int Employees { get => _employees; set { _employees = value; OnPropertyChanged(); } }

        public ObservableCollection<DepartmentRowVm> DepartmentRows { get; } = new();
        public ObservableCollection<PositionRowVm> PositionRows { get; } = new();

        public DepartmentsViewModel()
        {
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var stats = await _dataService.GetStatsAsync();
            Departments = stats.Departments;
            Positions = stats.Positions;
            Employees = stats.Employees;

            DepartmentRows.Clear();
            var deps = await _dataService.GetDepartmentsAsync();
            foreach (var d in deps)
            {
                DepartmentRows.Add(new DepartmentRowVm(d.Name, d.Positions, d.Employees));
            }

            PositionRows.Clear();
            var pos = await _dataService.GetPositionsAsync();
            foreach (var p in pos)
            {
                PositionRows.Add(new PositionRowVm(p.Name, p.Department));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public record DepartmentRowVm(string Name, int Positions, int Employees);
    public record PositionRowVm(string Name, string Department);
}
