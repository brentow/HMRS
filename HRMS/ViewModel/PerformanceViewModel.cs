using HRMS.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HRMS.ViewModel
{
    public class PerformanceViewModel : INotifyPropertyChanged
    {
        private readonly PerformanceDataService _dataService = new(DbConfig.ConnectionString);

        private int _totalCycles;
        private int _openCycles;
        private int _totalReviews;
        private int _submittedReviews;
        private int _draftReviews;
        private double _avgRating;

        public int TotalCycles { get => _totalCycles; set { _totalCycles = value; OnPropertyChanged(); } }
        public int OpenCycles { get => _openCycles; set { _openCycles = value; OnPropertyChanged(); } }
        public int TotalReviews { get => _totalReviews; set { _totalReviews = value; OnPropertyChanged(); } }
        public int SubmittedReviews { get => _submittedReviews; set { _submittedReviews = value; OnPropertyChanged(); } }
        public int DraftReviews { get => _draftReviews; set { _draftReviews = value; OnPropertyChanged(); } }
        public double AvgRating { get => _avgRating; set { _avgRating = value; OnPropertyChanged(); } }

        public ObservableCollection<ChartItem> ReviewChart { get; } = new();
        public ObservableCollection<TopPerformer> TopPerformers { get; } = new();

        public PerformanceViewModel()
        {
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var stats = await _dataService.GetStatsAsync();
            TotalCycles = stats.TotalCycles;
            OpenCycles = stats.OpenCycles;
            TotalReviews = stats.TotalReviews;
            SubmittedReviews = stats.SubmittedReviews;
            DraftReviews = stats.DraftReviews;
            AvgRating = stats.AverageRating;

            ReviewChart.Clear();
            ReviewChart.Add(new ChartItem("Submitted", SubmittedReviews, "#1E4368"));
            ReviewChart.Add(new ChartItem("Draft", DraftReviews, "#FDBD55"));
            ReviewChart.Add(new ChartItem("Remaining", TotalReviews - SubmittedReviews - DraftReviews, "#CBE9FE"));

            TopPerformers.Clear();
            var top = await _dataService.GetTopPerformersAsync();
            foreach (var t in top)
            {
                TopPerformers.Add(new TopPerformer(t.Employee, t.Rating));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public record ChartItem(string Label, int Value, string ColorHex);

    public record TopPerformer(string Name, double Rating);
}
