using System;
using System.Linq;

namespace HRMS.Model
{
    public enum BeneficiaryVerificationStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public record BeneficiaryStagingDto(
        int StagingID,
        string BeneficiaryID,
        string CivilRegistryID,
        string FirstName,
        string LastName,
        string? MiddleName,
        string? FullName,
        string? Address,
        string? Sex,
        string? Age,
        bool IsPwd,
        bool IsSenior,
        BeneficiaryVerificationStatus VerificationStatus,
        DateTime ImportedAt,
        string? Remarks,
        DateTime? ApprovedRejectedAt,
        int? MasterID)
    {
        public string FullNameDisplay =>
            !string.IsNullOrWhiteSpace(FullName)
                ? FullName
                : string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

        public bool IsPending => VerificationStatus == BeneficiaryVerificationStatus.Pending;
    }
}
