using System;

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
        string CivilRegistryID,
        string FirstName,
        string LastName,
        string? MiddleName,
        string? Address,
        BeneficiaryVerificationStatus VerificationStatus,
        DateTime ImportedAt,
        string? Remarks,
        DateTime? ApprovedRejectedAt,
        int? MasterID);
}
