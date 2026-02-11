using HRMS.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HRMS.ViewModel
{
    public class RecruitmentViewModel : INotifyPropertyChanged
    {
        private readonly RecruitmentDataService _dataService = new(DbConfig.ConnectionString);

        private int _totalPosts;
        private int _openPosts;
        private int _applicants;
        private int _interviews;

        public int TotalPosts { get => _totalPosts; set { _totalPosts = value; OnPropertyChanged(); } }
        public int OpenPosts { get => _openPosts; set { _openPosts = value; OnPropertyChanged(); } }
        public int Applicants { get => _applicants; set { _applicants = value; OnPropertyChanged(); } }
        public int Interviews { get => _interviews; set { _interviews = value; OnPropertyChanged(); } }

        public ObservableCollection<JobPostVm> JobPosts { get; } = new();
        public ObservableCollection<ApplicantVm> RecentApplicants { get; } = new();

        public RecruitmentViewModel()
        {
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var stats = await _dataService.GetStatsAsync();
            TotalPosts = stats.TotalPosts;
            OpenPosts = stats.OpenPosts;
            Applicants = stats.Applicants;
            Interviews = stats.Interviews;

            JobPosts.Clear();
            var jobs = await _dataService.GetRecentJobPostsAsync();
            foreach (var j in jobs)
            {
                JobPosts.Add(new JobPostVm(j.Title, j.Department, j.Status, j.PostedAt));
            }

            RecentApplicants.Clear();
            var appl = await _dataService.GetRecentApplicantsAsync();
            foreach (var a in appl)
            {
                RecentApplicants.Add(new ApplicantVm(a.Name, a.JobTitle, a.Status, a.AppliedAt));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public record JobPostVm(string Title, string Department, string Status, System.DateTime PostedAt);
    public record ApplicantVm(string Name, string JobTitle, string Status, System.DateTime AppliedAt);
}
