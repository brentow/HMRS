namespace HRMS.Model
{
    public class DashboardStats
    {
        // Organization-level metrics (Admin / HR view)
        public long TotalEmployees { get; set; }
        public long ActiveEmployees { get; set; }
        public long Departments { get; set; }
        public long Positions { get; set; }
        public long PresentToday { get; set; }
        public long PendingLeaves { get; set; }
        public long PendingAdjustments { get; set; }
        public long OpenJobs { get; set; }
        public long ActiveCourses { get; set; }
        public long PendingTrainingEnrollments { get; set; }
        public long OpenPayrollPeriods { get; set; }
        public long PayrollReleaseQueue { get; set; }
        public long OpenPerformanceCycles { get; set; }
        public long ActiveUsers { get; set; }
        public long ApplicantsInPipeline { get; set; }

        // Personal metrics (Employee view)
        public string TodayInText { get; set; } = "-";
        public string TodayOutText { get; set; } = "-";
        public long MyPendingAdjustments { get; set; }
        public long MyPendingLeaves { get; set; }
        public long MyActiveEnrollments { get; set; }
        public long MyOpenReviews { get; set; }
        public decimal MyLatestNetPay { get; set; }
        public string MyNextShiftText { get; set; } = "No shift assigned";
        public string MyPendingRequestsText { get; set; } = "No pending requests.";
        public string MyLatestDecisionText { get; set; } = "No recent approval/rejection update.";
        public string MyLatestDecisionModule { get; set; } = "NONE";
    }
}
