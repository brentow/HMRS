using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record RecruitmentStatsDto(int TotalPosts, int OpenPosts, int Applicants, int Interviews);
    public record JobPostDto(string Title, string Department, string Status, DateTime PostedAt);
    public record ApplicantDto(string Name, string JobTitle, string Status, DateTime AppliedAt);

    public class RecruitmentDataService
    {
        public RecruitmentDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<RecruitmentStatsDto> GetStatsAsync()
        {
            return Task.FromResult(new RecruitmentStatsDto(0, 0, 0, 0));
        }

        public Task<IReadOnlyList<JobPostDto>> GetRecentJobPostsAsync(int limit = 6)
        {
            IReadOnlyList<JobPostDto> list = Array.Empty<JobPostDto>();
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<ApplicantDto>> GetRecentApplicantsAsync(int limit = 8)
        {
            IReadOnlyList<ApplicantDto> list = Array.Empty<ApplicantDto>();
            return Task.FromResult(list);
        }
    }
}
