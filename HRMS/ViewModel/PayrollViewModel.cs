using HRMS.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HRMS.ViewModel
{
    public class PayrollViewModel : INotifyPropertyChanged
    {
        private readonly PayrollDataService _dataService = new(DbConfig.ConnectionString);

        private int _totalPeriods;
        private int _openPeriods;
        private int _totalItems;
        private decimal _totalNetPay;

        public int TotalPeriods { get => _totalPeriods; set { _totalPeriods = value; OnPropertyChanged(); } }
        public int OpenPeriods { get => _openPeriods; set { _openPeriods = value; OnPropertyChanged(); } }
        public int TotalItems { get => _totalItems; set { _totalItems = value; OnPropertyChanged(); } }
        public decimal TotalNetPay { get => _totalNetPay; set { _totalNetPay = value; OnPropertyChanged(); } }

        public ObservableCollection<PayrollPeriodVm> Periods { get; } = new();
        public ObservableCollection<PayrollRunVm> Runs { get; } = new();

        public PayrollViewModel()
        {
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var stats = await _dataService.GetStatsAsync();
            TotalPeriods = stats.TotalPeriods;
            OpenPeriods = stats.OpenPeriods;
            TotalItems = stats.TotalItems;
            TotalNetPay = stats.TotalNetPay;

            Periods.Clear();
            var periods = await _dataService.GetRecentPeriodsAsync();
            foreach (var p in periods)
            {
                Periods.Add(new PayrollPeriodVm(p.Name, p.From, p.To, p.Status));
            }

            Runs.Clear();
            var runs = await _dataService.GetRecentPayrollRunsAsync();
            foreach (var r in runs)
            {
                Runs.Add(new PayrollRunVm(r.Employee, r.Period, r.NetPay, r.GeneratedAt));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public record PayrollPeriodVm(string Name, System.DateTime From, System.DateTime To, string Status);
    public record PayrollRunVm(string Employee, string Period, decimal NetPay, System.DateTime GeneratedAt);
}
