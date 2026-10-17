using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.HR
{
    /// <summary>
    /// Ish haqi formulalari — HrPayroll (hisob yaratish) va Koreshok (taxminiy hisob) uchun yagona manba (HRM-D02, HRM-B12).
    ///
    /// Soatbay: oylik baza = xodim asosiy maoshi, bo'sh bo'lsa shtat rejasi yoki xodim tarif tarmog'i (min+max)/2;
    /// ustama % (moddiy + maxsus) qo'shiladi; soat stavkasi = (baza + ustama) / fond soati;
    /// hisoblangan = oddiy soat × stavka + qo'shimcha soat × stavka × <see cref="OvertimeMultiplier"/>.
    /// Daromad solig'i faqat hisoblangan summadan olinadi; to'lovga = hisoblangan + mukofot − jarima − soliq.
    /// </summary>
    public static class HrPayrollCalculator
    {
        public const decimal DefaultIncomeTaxPercent = 12m;
        public const decimal OvertimeMultiplier = 1.5m;

        public sealed class HourlyResult
        {
            public decimal MonthlyBase { get; set; }
            public decimal AllowancePercent { get; set; }
            public decimal HourlyRate { get; set; }
            public decimal GrossSalary { get; set; }
        }

        /// <summary>Oylik bazaviy summa (yuqoridagi qoida bo'yicha). Grade navigatsiyalari yuklangan bo'lishi kerak.</summary>
        public static decimal ResolveMonthlyBase(HR_EMPLOYEES employee, HR_STAFFING_PLAN staffingPlan, DateTime refDate)
        {
            if (employee.BaseSalary.HasValue && employee.BaseSalary.Value > 0)
                return employee.BaseSalary.Value;

            HR_SALARY_GRADES grade = null;
            if (staffingPlan?.SalaryGradeID != null)
                grade = staffingPlan.HR_SALARY_GRADES;
            if (grade == null && employee.SalaryGradeID != null)
                grade = employee.HR_SALARY_GRADES;

            if (grade != null && grade.IsDeleted != true &&
                grade.EffectiveFrom.Date <= refDate.Date &&
                (grade.EffectiveTo == null || grade.EffectiveTo.Value.Date >= refDate.Date))
            {
                return (grade.MinSalary + grade.MaxSalary) / 2m;
            }

            return 0m;
        }

        public static decimal AllowancePercent(HrEmployeeAllowanceService.EmployeeAllowanceRow allowanceRow)
        {
            return allowanceRow?.TotalPercent ?? 0m;
        }

        /// <summary>Soatbay hisob. <paramref name="overtimeHours"/> [0, hoursWorked] oralig'iga keltiriladi.</summary>
        public static HourlyResult CalculateHourly(decimal monthlyBase, decimal allowancePercent, decimal fundHours,
            decimal hoursWorked, decimal overtimeHours)
        {
            if (hoursWorked < 0) hoursWorked = 0;
            if (overtimeHours < 0) overtimeHours = 0;
            if (overtimeHours > hoursWorked) overtimeHours = hoursWorked;

            var monthlyCompensation = monthlyBase + monthlyBase * allowancePercent / 100m;
            var hourlyRate = fundHours > 0 ? monthlyCompensation / fundHours : 0m;
            var normalHours = hoursWorked - overtimeHours;

            return new HourlyResult
            {
                MonthlyBase = monthlyBase,
                AllowancePercent = allowancePercent,
                HourlyRate = hourlyRate,
                GrossSalary = normalHours * hourlyRate + overtimeHours * hourlyRate * OvertimeMultiplier
            };
        }

        /// <summary>Ishbay: tariflar bo'yicha summa + ustama %.</summary>
        public static decimal ApplyAllowance(decimal gross, decimal allowancePercent)
        {
            return gross + gross * allowancePercent / 100m;
        }

        public static decimal NormalizeTaxPercent(decimal percent)
        {
            if (percent < 0) return 0m;
            return percent > 100 ? 100m : percent;
        }

        public static decimal IncomeTax(decimal grossSalary, decimal taxPercent)
        {
            if (grossSalary <= 0 || taxPercent <= 0) return 0m;
            return Math.Round(grossSalary * taxPercent / 100m, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal NetSalary(decimal grossSalary, decimal bonuses, decimal penalties, decimal taxPercent)
        {
            return grossSalary + bonuses - penalties - IncomeTax(grossSalary, taxPercent);
        }

        /// <summary>Saqlangan qatordan soliq summasi: hisoblangan + mukofot − jarima − to'lovga (manfiy bo'lsa 0).</summary>
        public static decimal StoredIncomeTax(HR_PAYROLL_LINES line)
        {
            var tax = (line.GrossSalary ?? 0) + (line.Bonuses ?? 0) - (line.Penalties ?? 0) - (line.NetSalary ?? 0);
            return tax > 0 ? tax : 0m;
        }

        /// <summary>Saqlangan qatordan soliq foizi (hisoblangan 0 bo'lsa null).</summary>
        public static decimal? StoredIncomeTaxPercent(HR_PAYROLL_LINES line)
        {
            var gross = line.GrossSalary ?? 0;
            if (gross <= 0) return null;
            return Math.Round(StoredIncomeTax(line) / gross * 100m, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Davr uchun faol shtat rejalari (bitta so'rov; avval har tabel qatoriga alohida so'rov, HRM-P08).
        /// Kalit — (OrgUnitID, PositionID); bir nechta bo'lsa eng so'nggi EffectiveFrom.
        /// </summary>
        public static async Task<Dictionary<Tuple<int, int>, HR_STAFFING_PLAN>> LoadStaffingPlansAsync(
            DBTHSNEntities db, DateTime periodStart, DateTime periodEnd)
        {
            var plans = await db.HR_STAFFING_PLAN.AsNoTracking()
                .Include(sp => sp.HR_SALARY_GRADES)
                .Where(sp => sp.IsActive &&
                             sp.IsDeleted != true &&
                             sp.EffectiveFrom <= periodEnd &&
                             (sp.EffectiveTo == null || sp.EffectiveTo >= periodStart))
                .ToListAsync();

            return plans
                .GroupBy(sp => Tuple.Create(sp.OrgUnitID, sp.PositionID))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(sp => sp.EffectiveFrom).ThenByDescending(sp => sp.ID).First());
        }

        public static HR_STAFFING_PLAN FindPlan(Dictionary<Tuple<int, int>, HR_STAFFING_PLAN> plans, int? orgUnitId, int? positionId)
        {
            if (!orgUnitId.HasValue || !positionId.HasValue) return null;
            HR_STAFFING_PLAN plan;
            return plans.TryGetValue(Tuple.Create(orgUnitId.Value, positionId.Value), out plan) ? plan : null;
        }
    }
}
