namespace HRMS.Model
{
    public sealed class CompanyProfile
    {
        public string CompanyName { get; init; } = "Human Resources Management System";
        public string Address { get; init; } = "Human Resource Management Office";
        public string OwnerName { get; init; } = "HRMS Control Center";
        public string SerialNumber { get; init; } = "Office ID 18 / OFF-2026-0007";
        public string LogoPath { get; init; } = "HRMS/Images/ePRIME_logo.png";

        public static CompanyProfile Default => new();
    }
}
