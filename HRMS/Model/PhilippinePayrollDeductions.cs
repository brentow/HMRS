using System;

namespace HRMS.Model
{
    public static class PhilippinePayrollDeductions
    {
        public static decimal ComputeGSIS(decimal basicMonthlySalary) =>
            Math.Round(basicMonthlySalary * 0.09m, 2, MidpointRounding.AwayFromZero);

        public static decimal ComputeGSISEmployerShare(decimal basicMonthlySalary) =>
            Math.Round(basicMonthlySalary * 0.12m, 2, MidpointRounding.AwayFromZero);

        public static bool UsesSssCoverage(string? employmentTypeName)
        {
            var normalizedType = employmentTypeName?.Trim().ToLowerInvariant() ?? string.Empty;
            return normalizedType.Contains("job order", StringComparison.Ordinal) ||
                   normalizedType.Contains("contractual", StringComparison.Ordinal) ||
                   normalizedType.Contains("cos", StringComparison.Ordinal);
        }

        public static (decimal GsisContribution, decimal SssContribution) ComputeRetirement(decimal basicMonthlySalary, string? employmentTypeName)
        {
            var isSss = UsesSssCoverage(employmentTypeName);

            if (isSss)
            {
                return (0m, ComputeSss(basicMonthlySalary));
            }

            return (ComputeGSIS(basicMonthlySalary), 0m);
        }

        public static (decimal GsisEmployerShare, decimal SssEmployerShare) ComputeRetirementEmployer(decimal basicMonthlySalary, string? employmentTypeName)
        {
            if (UsesSssCoverage(employmentTypeName))
            {
                return (0m, ComputeSssEmployerShare(basicMonthlySalary));
            }

            return (ComputeGSISEmployerShare(basicMonthlySalary), 0m);
        }

        public static decimal ComputeSSS(decimal basicMonthlySalary) =>
            ComputeSss(basicMonthlySalary);

        public static decimal ComputeSss(decimal basicMonthlySalary)
        {
            var salaryCredit = ComputeSssSalaryCredit(basicMonthlySalary);
            return Math.Round(salaryCredit * 0.05m, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal ComputeSSSEmployerShare(decimal basicMonthlySalary) =>
            ComputeSssEmployerShare(basicMonthlySalary);

        public static decimal ComputeSssEmployerShare(decimal basicMonthlySalary)
        {
            var salaryCredit = ComputeSssSalaryCredit(basicMonthlySalary);
            return Math.Round(salaryCredit * 0.10m, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal ComputePhilHealth(decimal basicMonthlySalary)
        {
            var share = basicMonthlySalary * 0.025m;
            return Math.Round(Math.Clamp(share, 250m, 2_500m), 2, MidpointRounding.AwayFromZero);
        }

        public static decimal ComputePhilHealthEmployerShare(decimal basicMonthlySalary) =>
            ComputePhilHealth(basicMonthlySalary);

        public static decimal ComputePagIBIG(decimal basicMonthlySalary)
        {
            var rate = basicMonthlySalary <= 1_500m ? 0.01m : 0.02m;
            return Math.Round(Math.Min(basicMonthlySalary * rate, 200m), 2, MidpointRounding.AwayFromZero);
        }

        public static decimal ComputePagIBIGEmployerShare(decimal basicMonthlySalary) =>
            Math.Round(Math.Min(basicMonthlySalary * 0.02m, 200m), 2, MidpointRounding.AwayFromZero);

        public static decimal ComputeWithholdingTax(decimal grossPay, decimal gsisOrSss, decimal philHealth, decimal pagIbig)
        {
            var taxable = grossPay - gsisOrSss - philHealth - pagIbig;
            if (taxable <= 20_833m)
            {
                return 0m;
            }

            decimal tax;
            if (taxable <= 33_332m)
            {
                tax = (taxable - 20_833m) * 0.20m;
            }
            else if (taxable <= 66_666m)
            {
                tax = 2_500m + ((taxable - 33_333m) * 0.25m);
            }
            else if (taxable <= 166_666m)
            {
                tax = 10_833m + ((taxable - 66_667m) * 0.30m);
            }
            else if (taxable <= 666_666m)
            {
                tax = 40_833m + ((taxable - 166_667m) * 0.32m);
            }
            else
            {
                tax = 200_833m + ((taxable - 666_667m) * 0.35m);
            }

            return Math.Round(tax, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal ComputeAbsenceDeduction(decimal basicMonthlySalary, decimal absentDays) =>
            Math.Round((basicMonthlySalary / 22m) * absentDays, 2, MidpointRounding.AwayFromZero);

        public static decimal ComputeAbsence(decimal basicMonthlySalary, decimal absentDays) =>
            ComputeAbsenceDeduction(basicMonthlySalary, absentDays);

        public static decimal ComputeLateDeduction(decimal basicMonthlySalary, int lateMinutes) =>
            Math.Round((basicMonthlySalary / 22m / 8m / 60m) * lateMinutes, 2, MidpointRounding.AwayFromZero);

        public static decimal ComputeLate(decimal basicMonthlySalary, int lateMinutes) =>
            ComputeLateDeduction(basicMonthlySalary, lateMinutes);

        public static PayrollDeductionResult ComputeAll(
            decimal basicMonthlySalary,
            string? employmentTypeName,
            decimal allowances = 0m,
            decimal overtimePay = 0m,
            decimal otherEarnings = 0m,
            decimal absentDays = 0m,
            int lateMinutes = 0,
            decimal loanDeduction = 0m,
            decimal otherDeductions = 0m)
        {
            var grossPay = basicMonthlySalary + allowances + overtimePay + otherEarnings;
            var (gsis, sss) = ComputeRetirement(basicMonthlySalary, employmentTypeName);
            var (gsisEmployer, sssEmployer) = ComputeRetirementEmployer(basicMonthlySalary, employmentTypeName);
            var philHealth = ComputePhilHealth(basicMonthlySalary);
            var philHealthEmployer = ComputePhilHealthEmployerShare(basicMonthlySalary);
            var pagIbig = ComputePagIBIG(basicMonthlySalary);
            var pagIbigEmployer = ComputePagIBIGEmployerShare(basicMonthlySalary);
            var tax = ComputeWithholdingTax(grossPay, gsis + sss, philHealth, pagIbig);
            var absence = ComputeAbsenceDeduction(basicMonthlySalary, absentDays);
            var late = ComputeLateDeduction(basicMonthlySalary, lateMinutes);

            var totalDeductions = gsis + sss + philHealth + pagIbig + tax + absence + late + loanDeduction + otherDeductions;
            return new PayrollDeductionResult
            {
                GrossPay = grossPay,
                GsisContribution = gsis,
                SssContribution = sss,
                GsisEmployerShare = gsisEmployer,
                SssEmployerShare = sssEmployer,
                PhilHealthContribution = philHealth,
                PhilHealthEmployerShare = philHealthEmployer,
                PagIBIGContribution = pagIbig,
                PagIBIGEmployerShare = pagIbigEmployer,
                TaxWithheld = tax,
                AbsenceDeduction = absence,
                LateDeduction = late,
                LoanDeduction = loanDeduction,
                OtherDeductions = otherDeductions,
                TotalDeductions = totalDeductions,
                NetPay = grossPay - totalDeductions,
                TotalEmployerShare = gsisEmployer + sssEmployer + philHealthEmployer + pagIbigEmployer
            };
        }

        private static decimal ComputeSssSalaryCredit(decimal basicMonthlySalary)
        {
            var salaryCredit = Math.Round(basicMonthlySalary / 500m, 0, MidpointRounding.AwayFromZero) * 500m;
            return Math.Clamp(salaryCredit, 4_000m, 20_000m);
        }

        /// <summary>
        /// Computes SSS contribution using DB-loaded bracket table.
        /// Falls back to formula-based computation if no brackets provided.
        /// </summary>
        public static (decimal EeShare, decimal ErShare) ComputeSssFromBrackets(
            decimal basicMonthlySalary,
            System.Collections.Generic.IReadOnlyList<SssBracketDto>? brackets)
        {
            if (brackets == null || brackets.Count == 0)
            {
                return (ComputeSss(basicMonthlySalary), ComputeSssEmployerShare(basicMonthlySalary));
            }

            foreach (var bracket in brackets)
            {
                if (basicMonthlySalary >= bracket.MinRange && basicMonthlySalary <= bracket.MaxRange)
                {
                    return (bracket.EeShare, bracket.ErShare);
                }
            }

            // If salary exceeds all brackets, use the last (highest) bracket
            var last = brackets[brackets.Count - 1];
            return (last.EeShare, last.ErShare);
        }

        /// <summary>
        /// Computes GSIS contribution using DB-loaded rates.
        /// Falls back to hardcoded 9%/12% if no bracket provided.
        /// </summary>
        public static (decimal EeShare, decimal ErShare) ComputeGsisFromBracket(
            decimal basicMonthlySalary,
            GsisBracketDto? bracket)
        {
            if (bracket == null)
            {
                return (ComputeGSIS(basicMonthlySalary), ComputeGSISEmployerShare(basicMonthlySalary));
            }

            var ee = Math.Round(basicMonthlySalary * bracket.EeRate, 2, MidpointRounding.AwayFromZero);
            var er = Math.Round(basicMonthlySalary * bracket.ErRate, 2, MidpointRounding.AwayFromZero);
            return (ee, er);
        }

        /// <summary>
        /// Computes PhilHealth contribution using DB-loaded parameters.
        /// Falls back to hardcoded formula if no bracket provided.
        /// </summary>
        public static (decimal EeShare, decimal ErShare) ComputePhilHealthFromBracket(
            decimal basicMonthlySalary,
            PhilHealthBracketDto? bracket)
        {
            if (bracket == null)
            {
                var share = ComputePhilHealth(basicMonthlySalary);
                return (share, share);
            }

            var totalPremium = basicMonthlySalary * bracket.PremiumRate;
            totalPremium = Math.Clamp(totalPremium, bracket.MinMonthlyPremium, bracket.MaxMonthlyPremium);
            var eeShare = Math.Round(totalPremium * bracket.EeSharePct, 2, MidpointRounding.AwayFromZero);
            var erShare = Math.Round(totalPremium * bracket.ErSharePct, 2, MidpointRounding.AwayFromZero);
            return (eeShare, erShare);
        }

        /// <summary>
        /// Computes Pag-IBIG contribution using DB-loaded brackets.
        /// Falls back to hardcoded formula if no brackets provided.
        /// </summary>
        public static (decimal EeShare, decimal ErShare) ComputePagIbigFromBrackets(
            decimal basicMonthlySalary,
            System.Collections.Generic.IReadOnlyList<PagIbigBracketDto>? brackets)
        {
            if (brackets == null || brackets.Count == 0)
            {
                return (ComputePagIBIG(basicMonthlySalary), ComputePagIBIGEmployerShare(basicMonthlySalary));
            }

            foreach (var bracket in brackets)
            {
                if (basicMonthlySalary >= bracket.MinSalary && basicMonthlySalary <= bracket.MaxSalary)
                {
                    var ee = Math.Round(
                        Math.Min(basicMonthlySalary * bracket.EeRate, bracket.MaxEeContribution),
                        2, MidpointRounding.AwayFromZero);
                    var er = Math.Round(
                        Math.Min(basicMonthlySalary * bracket.ErRate, bracket.MaxErContribution),
                        2, MidpointRounding.AwayFromZero);
                    return (ee, er);
                }
            }

            // Fallback to last bracket
            var last = brackets[brackets.Count - 1];
            var eeFallback = Math.Round(
                Math.Min(basicMonthlySalary * last.EeRate, last.MaxEeContribution),
                2, MidpointRounding.AwayFromZero);
            var erFallback = Math.Round(
                Math.Min(basicMonthlySalary * last.ErRate, last.MaxErContribution),
                2, MidpointRounding.AwayFromZero);
            return (eeFallback, erFallback);
        }
    }

    public class PayrollDeductionResult
    {
        public decimal GrossPay { get; set; }
        public decimal GsisContribution { get; set; }
        public decimal SssContribution { get; set; }
        public decimal GsisEmployerShare { get; set; }
        public decimal SssEmployerShare { get; set; }
        public decimal PhilHealthContribution { get; set; }
        public decimal PhilHealthEmployerShare { get; set; }
        public decimal PagIBIGContribution { get; set; }
        public decimal PagIBIGEmployerShare { get; set; }
        public decimal TaxWithheld { get; set; }
        public decimal AbsenceDeduction { get; set; }
        public decimal LateDeduction { get; set; }
        public decimal LoanDeduction { get; set; }
        public decimal OtherDeductions { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal TotalEmployerShare { get; set; }
        public decimal NetPay { get; set; }
    }
}
