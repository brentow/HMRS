using System;
using System.Collections.Generic;
using System.Linq;

namespace HRMS.Model
{
    public record DocumentDefinition(
        string Code,
        string Name,
        int Tier,
        int? ExpiryMonths);

    public static class DocumentChecklistDefinitions
    {
        public static readonly IReadOnlyList<DocumentDefinition> Tier1 = new[]
        {
            new DocumentDefinition("PDS", "Personal Data Sheet (CSC Form 212, Revised 2017)", 1, null),
            new DocumentDefinition("WORK_EXP", "Work Experience Sheet (PDS attachment, if applicable)", 1, null),
            new DocumentDefinition("NBI", "NBI Clearance (valid, not expired)", 1, 12),
            new DocumentDefinition("POLICE_CLR", "Police Clearance (from place of residence)", 1, 6),
            new DocumentDefinition("BRGY_CLR", "Barangay Clearance / Certificate of Residency", 1, 6),
            new DocumentDefinition("PSA_BIRTH", "PSA-Authenticated Birth Certificate", 1, null),
            new DocumentDefinition("PSA_MARR", "PSA-Authenticated Marriage Certificate (if married)", 1, null),
            new DocumentDefinition("TOR", "Transcript of Records (original or certified true copy)", 1, null),
            new DocumentDefinition("DIPLOMA", "Diploma (original or certified true copy)", 1, null),
            new DocumentDefinition("CS_ELIG", "Certificate of Eligibility / Civil Service Eligibility (CSC / RA 1080)", 1, null),
            new DocumentDefinition("MED_CERT", "Medical Certificate from Government Physician (with X-ray, CBC, UA)", 1, 6),
            new DocumentDefinition("DRUG_TEST", "Drug Test Result (PDEA-accredited laboratory)", 1, 6),
            new DocumentDefinition("ID_PHOTOS", "2x2 ID Photos - 4 copies, recently taken", 1, null),
            new DocumentDefinition("GOV_ID", "Valid government-issued ID (UMID / PhilSys / Voter / Driver / Passport)", 1, null),
        };

        public static readonly IReadOnlyList<DocumentDefinition> Tier2 = new[]
        {
            new DocumentDefinition("PRC_ID", "PRC ID (valid, not expired)", 2, 36),
            new DocumentDefinition("PRC_CERT", "PRC board certificate / certificate of registration", 2, null),
            new DocumentDefinition("COG_STAND", "Certificate of good standing", 2, 12),
            new DocumentDefinition("OATH", "Oath of allegiance / oath of office", 2, null),
        };

        public static readonly IReadOnlyList<DocumentDefinition> Tier3 = new[]
        {
            new DocumentDefinition("SALN", "Statement of Assets, Liabilities, and Net Worth (current year)", 3, 12),
            new DocumentDefinition("NO_CASE", "Certificate of no pending administrative/criminal case", 3, 6),
            new DocumentDefinition("PERF_RATE", "Performance rating - last 2 periods", 3, null),
            new DocumentDefinition("LEADERSHIP", "Supervisory or leadership training certificate", 3, null),
            new DocumentDefinition("SVC_RECORD", "Service record from previous employers (if applicable)", 3, null),
        };

        public static readonly IReadOnlyList<DocumentDefinition> Tier4 = new[]
        {
            new DocumentDefinition("APP_LETTER", "Application letter", 4, null),
            new DocumentDefinition("CV", "Resume / Curriculum Vitae", 4, null),
            new DocumentDefinition("PDS", "Personal Data Sheet (CSC Form 212)", 4, null),
            new DocumentDefinition("NBI", "NBI Clearance (valid)", 4, 12),
            new DocumentDefinition("BRGY_CLR", "Barangay Clearance", 4, 6),
            new DocumentDefinition("PSA_BIRTH", "PSA Birth Certificate", 4, null),
            new DocumentDefinition("TOR", "Transcript of Records / Diploma (certified copy)", 4, null),
            new DocumentDefinition("DRUG_TEST", "Drug Test Result", 4, 6),
            new DocumentDefinition("MED_CERT", "Medical Certificate", 4, 6),
            new DocumentDefinition("ID_PHOTOS", "2x2 ID Photos (2 copies)", 4, null),
            new DocumentDefinition("PRC_ID", "PRC ID (waive if not required for the role)", 4, 36),
        };

        private static readonly IReadOnlyDictionary<string, DocumentDefinition> DefinitionsByCode =
            Tier1.Concat(Tier2).Concat(Tier3).Concat(Tier4)
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        public static bool TryGetDefinition(string? code, out DocumentDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                definition = default!;
                return false;
            }

            return DefinitionsByCode.TryGetValue(code.Trim(), out definition!);
        }

        public static int GetTierForPosition(string? positionName, string? employmentTypeName)
        {
            var employment = employmentTypeName?.Trim().ToLowerInvariant() ?? string.Empty;
            var position = positionName?.Trim().ToLowerInvariant() ?? string.Empty;

            if (employment.Contains("job order", StringComparison.Ordinal) ||
                employment.Contains("contractual", StringComparison.Ordinal) ||
                employment.Contains("cos", StringComparison.Ordinal))
            {
                return 4;
            }

            if (position.Contains("department head", StringComparison.Ordinal) ||
                position.Contains("head", StringComparison.Ordinal) ||
                position.Contains("chief", StringComparison.Ordinal) ||
                position.Contains("manager", StringComparison.Ordinal) ||
                position.Contains("supervisor", StringComparison.Ordinal) ||
                position.Contains("officer", StringComparison.Ordinal))
            {
                return 3;
            }

            if (position.Contains("nurse", StringComparison.Ordinal) ||
                position.Contains("midwife", StringComparison.Ordinal) ||
                position.Contains("physician", StringComparison.Ordinal) ||
                position.Contains("doctor", StringComparison.Ordinal) ||
                position.Contains("dentist", StringComparison.Ordinal) ||
                position.Contains("engineer", StringComparison.Ordinal) ||
                position.Contains("architect", StringComparison.Ordinal) ||
                position.Contains("accountant", StringComparison.Ordinal) ||
                position.Contains("teacher", StringComparison.Ordinal) ||
                position.Contains("social worker", StringComparison.Ordinal))
            {
                return 2;
            }

            return 1;
        }

        public static List<DocumentDefinition> GetDocumentsForEmployee(string? positionName, string? employmentTypeName)
        {
            var tier = GetTierForPosition(positionName, employmentTypeName);
            if (tier == 4)
            {
                return new List<DocumentDefinition>(Tier4);
            }

            var docs = new List<DocumentDefinition>(Tier1);
            if (tier >= 2)
            {
                docs.AddRange(Tier2);
            }

            if (tier >= 3)
            {
                docs.AddRange(Tier3);
            }

            return docs;
        }
    }
}
