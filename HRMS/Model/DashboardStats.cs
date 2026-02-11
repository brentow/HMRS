namespace HRMS.Model
{
    public class DashboardStats
    {
        public long TotalEmployees { get; set; }
        public long ActiveEmployees { get; set; }
        public long Departments { get; set; }
        public long Positions { get; set; }
        public long PresentToday { get; set; }
        public long PendingLeaves { get; set; }
        public long OpenJobs { get; set; }
        public long ActiveCourses { get; set; }
        public long OpenPayrollPeriods { get; set; }
        public long OpenPerformanceCycles { get; set; }
        public long ActiveUsers { get; set; }
        public long ApplicantsInPipeline { get; set; }
    }
}
