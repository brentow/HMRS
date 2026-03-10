using HRMS.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using System.Windows.Data;

namespace HRMS.ViewModel
{
    public class DepartmentsViewModel : INotifyPropertyChanged
    {
        private readonly DepartmentsDataService _dataService = new(DbConfig.ConnectionString);

        private int _departments;
        private int _positions;
        private int _employees;
        private int _departmentsWithoutPositions;
        private int _positionsWithoutEmployees;
        private decimal _avgPositionsPerDepartment;
        private decimal _avgEmployeesPerDepartment;
        private DateTime _lastRefreshed;
        private string _searchQuery = string.Empty;

        public int Departments { get => _departments; set { _departments = value; OnPropertyChanged(); } }
        public int Positions { get => _positions; set { _positions = value; OnPropertyChanged(); } }
        public int Employees { get => _employees; set { _employees = value; OnPropertyChanged(); } }
        public int DepartmentsWithoutPositions { get => _departmentsWithoutPositions; set { _departmentsWithoutPositions = value; OnPropertyChanged(); } }
        public int PositionsWithoutEmployees { get => _positionsWithoutEmployees; set { _positionsWithoutEmployees = value; OnPropertyChanged(); } }
        public decimal AvgPositionsPerDepartment { get => _avgPositionsPerDepartment; set { _avgPositionsPerDepartment = value; OnPropertyChanged(); } }
        public decimal AvgEmployeesPerDepartment { get => _avgEmployeesPerDepartment; set { _avgEmployeesPerDepartment = value; OnPropertyChanged(); } }
        public DateTime LastRefreshed { get => _lastRefreshed; set { _lastRefreshed = value; OnPropertyChanged(); } }

        public ObservableCollection<DepartmentRowVm> DepartmentRows { get; } = new();
        public ObservableCollection<PositionRowVm> PositionRows { get; } = new();
        public ICollectionView DepartmentView { get; }
        public ICollectionView PositionView { get; }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery == value)
                {
                    return;
                }

                _searchQuery = value ?? string.Empty;
                OnPropertyChanged();
                DepartmentView.Refresh();
                PositionView.Refresh();
            }
        }

        public DepartmentsViewModel()
        {
            DepartmentView = CollectionViewSource.GetDefaultView(DepartmentRows);
            PositionView = CollectionViewSource.GetDefaultView(PositionRows);
            DepartmentView.Filter = FilterDepartment;
            PositionView.Filter = FilterPosition;

            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            var stats = await _dataService.GetStatsAsync();
            Departments = stats.Departments;
            Positions = stats.Positions;
            Employees = stats.Employees;
            DepartmentsWithoutPositions = stats.DepartmentsWithoutPositions;
            PositionsWithoutEmployees = stats.PositionsWithoutEmployees;
            AvgPositionsPerDepartment = Departments == 0 ? 0 : Math.Round((decimal)Positions / Departments, 2);
            AvgEmployeesPerDepartment = Departments == 0 ? 0 : Math.Round((decimal)Employees / Departments, 2);
            LastRefreshed = DateTime.Now;

            DepartmentRows.Clear();
            var deps = await _dataService.GetDepartmentsAsync();
            foreach (var d in deps)
            {
                DepartmentRows.Add(new DepartmentRowVm(d.Name, d.Positions, d.Employees, d.EmployeesPerPosition, d.Health));
            }

            PositionRows.Clear();
            var pos = await _dataService.GetPositionsAsync();
            foreach (var p in pos)
            {
                PositionRows.Add(new PositionRowVm(p.Name, p.Department, p.Employees, p.Status));
            }

            DepartmentView.Refresh();
            PositionView.Refresh();
        }

        public async Task AddDepartmentAsync(string departmentName)
        {
            await _dataService.AddDepartmentAsync(departmentName);
            await RefreshAsync();
        }

        public async Task DeleteDepartmentAsync(string departmentName)
        {
            await _dataService.DeleteDepartmentAsync(departmentName);
            await RefreshAsync();
        }

        public async Task AddPositionAsync(string departmentName, string positionName)
        {
            await _dataService.AddPositionAsync(departmentName, positionName);
            await RefreshAsync();
        }

        public async Task DeletePositionAsync(string departmentName, string positionName)
        {
            await _dataService.DeletePositionAsync(departmentName, positionName);
            await RefreshAsync();
        }

        private bool FilterDepartment(object obj)
        {
            if (obj is not DepartmentRowVm row)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                return true;
            }

            return row.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
                   || row.Health.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
        }

        private bool FilterPosition(object obj)
        {
            if (obj is not PositionRowVm row)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                return true;
            }

            return row.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
                   || row.Department.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
                   || row.Status.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public record DepartmentRowVm(string Name, int Positions, int Employees, decimal EmployeesPerPosition, string Health);
    public record PositionRowVm(string Name, string Department, int Employees, string Status);
}
