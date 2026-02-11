using HRMS.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HRMS.ViewModel
{
    public class AttendanceViewModel : INotifyPropertyChanged
    {
        private readonly AttendanceDataService _dataService = new(DbConfig.ConnectionString);

        private int _totalLogs;
        private int _todayLogs;
        private int _presentToday;
        private int _incompleteLogs;

        public int TotalLogs { get => _totalLogs; set { _totalLogs = value; OnPropertyChanged(); } }
        public int TodayLogs { get => _todayLogs; set { _todayLogs = value; OnPropertyChanged(); } }
        public int PresentToday { get => _presentToday; set { _presentToday = value; OnPropertyChanged(); } }
        public int IncompleteLogs { get => _incompleteLogs; set { _incompleteLogs = value; OnPropertyChanged(); } }

        public ObservableCollection<AttendanceLogVm> Logs { get; } = new();

        public AttendanceViewModel()
        {
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var stats = await _dataService.GetStatsAsync();
            TotalLogs = stats.TotalLogs;
            TodayLogs = stats.TodayLogs;
            PresentToday = stats.PresentToday;
            IncompleteLogs = stats.IncompleteLogs;

            Logs.Clear();
            var logs = await _dataService.GetRecentLogsAsync();
            foreach (var l in logs)
            {
                Logs.Add(new AttendanceLogVm(l.Employee, l.LogDate, l.TimeIn, l.TimeOut, l.Source));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public record AttendanceLogVm(string Employee, System.DateTime LogDate, System.TimeSpan? TimeIn, System.TimeSpan? TimeOut, string Source);
}
