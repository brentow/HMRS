using HRMS.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public class LeaveViewModel : INotifyPropertyChanged
    {
        private readonly LeaveDataService _dataService = new(DbConfig.ConnectionString);

        private int _totalRequests;
        private int _pendingRequests;
        private int _approvedRequests;
        private int _rejectedRequests;
        private int _leaveTypes;

        public int TotalRequests { get => _totalRequests; set { _totalRequests = value; OnPropertyChanged(); } }
        public int PendingRequests { get => _pendingRequests; set { _pendingRequests = value; OnPropertyChanged(); } }
        public int ApprovedRequests { get => _approvedRequests; set { _approvedRequests = value; OnPropertyChanged(); } }
        public int RejectedRequests { get => _rejectedRequests; set { _rejectedRequests = value; OnPropertyChanged(); } }
        public int LeaveTypes { get => _leaveTypes; set { _leaveTypes = value; OnPropertyChanged(); } }

        public ObservableCollection<LeaveRequestVm> Requests { get; } = new();

        public LeaveViewModel()
        {
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var stats = await _dataService.GetStatsAsync();
            TotalRequests = stats.TotalRequests;
            PendingRequests = stats.PendingRequests;
            ApprovedRequests = stats.ApprovedRequests;
            RejectedRequests = stats.RejectedRequests;
            LeaveTypes = stats.LeaveTypes;

            Requests.Clear();
            var list = await _dataService.GetRecentRequestsAsync();
            foreach (var r in list)
            {
                Requests.Add(new LeaveRequestVm(r.Employee, r.LeaveType, r.StartDate, r.EndDate, r.Status, r.RequestedAt)
                {
                    StatusColor = GetStatusBrush(r.Status)
                });
            }
        }

        private static Brush GetStatusBrush(string status)
        {
            return status?.ToLowerInvariant() switch
            {
                "approved" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E4368")),
                "rejected" => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935")),
                _ => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#FDBD55")),
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LeaveRequestVm
    {
        public LeaveRequestVm(string employee, string leaveType, System.DateTime start, System.DateTime end, string status, System.DateTime requestedAt)
        {
            Employee = employee;
            LeaveType = leaveType;
            StartDate = start;
            EndDate = end;
            Status = status;
            RequestedAt = requestedAt;
        }

        public string Employee { get; }
        public string LeaveType { get; }
        public System.DateTime StartDate { get; }
        public System.DateTime EndDate { get; }
        public string Status { get; }
        public System.DateTime RequestedAt { get; }
        public Brush? StatusColor { get; set; }
    }
}
