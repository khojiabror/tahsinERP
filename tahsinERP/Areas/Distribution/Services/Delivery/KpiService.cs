using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Reports;
using tahsinERP.Services.HR;

namespace tahsinERP.Areas.Distribution.Services.Delivery
{
    /// <summary>KPI hisobi uchun oylik ko'rsatkichlar.</summary>
    public sealed class KpiInputs
    {
        public decimal NetSales { get; set; }
        public decimal? CoveragePct { get; set; }
        public decimal OverdueDebt { get; set; }
        public decimal ShortageCost { get; set; }
    }

    public sealed class KpiAmounts
    {
        public decimal? PlanPct { get; set; }
        public decimal Commission { get; set; }
        public decimal Bonus { get; set; }
        public decimal Penalty { get; set; }
    }

    /// <summary>
    /// Agent KPI ish haqi: % savdo (sof tushum: jo'natilgan − qaytarilgan, S5 hisobotlari bilan bir xil), reja bajarilsa bonus
    /// (+ rejadan ortig'iga qo'shimcha %), jarimalar — tashrif rejasi bajarilmasa (qat'iy summa), muddati o'tgan qarzdan %,
    /// reys kamomadidan % (ekspeditor). Natija HR ish haqi qatoriga mukofot va jarima sifatida qo'shiladi (HrPayrollCalculator).
    /// </summary>
    public static class KpiMath
    {
        public static KpiAmounts Compute(KpiScheme s, KpiInputs x)
        {
            var r = new KpiAmounts
            {
                PlanPct = s.MonthlyPlan > 0 ? Math.Round(x.NetSales * 100m / s.MonthlyPlan, 2) : (decimal?)null,
                Commission = Round(Math.Max(0, x.NetSales) * s.SalesPct / 100m)
            };
            if (s.MonthlyPlan > 0 && x.NetSales >= s.MonthlyPlan)
                r.Bonus = Round(s.PlanBonus + (x.NetSales - s.MonthlyPlan) * s.OverPlanPct / 100m);
            decimal penalty = 0;
            if (s.MinCoveragePct > 0 && s.CoveragePenalty > 0 && x.CoveragePct.HasValue && x.CoveragePct.Value < s.MinCoveragePct)
                penalty += s.CoveragePenalty;
            penalty += Math.Max(0, x.OverdueDebt) * s.OverduePenaltyPct / 100m;
            penalty += Math.Max(0, x.ShortageCost) * s.ShortagePenaltyPct / 100m;
            r.Penalty = Round(penalty);
            return r;
        }

        private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
    }

    public static class KpiService
    {
        public static DateTime MonthStart(int year, int month) => new DateTime(year, month, 1);

        public static KpiScheme SaveScheme(DistributionDbContext db, KpiScheme input, int userId, DateTime now)
        {
            if (!db.SalesAgents.Any(a => a.UserID == input.AgentUserID)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            foreach (var pct in new[] { input.SalesPct, input.OverPlanPct, input.MinCoveragePct, input.OverduePenaltyPct, input.ShortagePenaltyPct })
                DirectoryRules.ValidateDiscountPercent(pct);
            if (input.MonthlyPlan < 0 || input.PlanBonus < 0 || input.CoveragePenalty < 0)
                throw new DstValidationException(DstLocalization.T("Error_PriceNegative"));
            if (input.EmployeeID.HasValue && db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM dbo.HR_EMPLOYEES WHERE ID = @p0 AND ISNULL(IsDeleted, 0) = 0", input.EmployeeID.Value).First() == 0)
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var row = db.KpiSchemes.Find(input.AgentUserID);
            if (row == null) db.KpiSchemes.Add(row = new KpiScheme { AgentUserID = input.AgentUserID });
            row.EmployeeID = input.EmployeeID;
            row.SalesPct = input.SalesPct;
            row.MonthlyPlan = input.MonthlyPlan;
            row.PlanBonus = input.PlanBonus;
            row.OverPlanPct = input.OverPlanPct;
            row.MinCoveragePct = input.MinCoveragePct;
            row.CoveragePenalty = input.CoveragePenalty;
            row.OverduePenaltyPct = input.OverduePenaltyPct;
            row.ShortagePenaltyPct = input.ShortagePenaltyPct;
            row.IsActive = input.IsActive;
            row.UpdatedBy = userId;
            row.UpdatedAt = now;
            db.SaveChanges();
            return row;
        }

        /// <summary>Oylik ko'rsatkichlar: sof savdo, tashrif rejasining bajarilishi, oy oxirida 30 kundan ortiq kechikkan qarz, reys kamomadi.</summary>
        public static KpiInputs Inputs(DistributionDbContext db, int agentUserId, int year, int month, DateTime now)
        {
            var from = MonthStart(year, month);
            var to = from.AddMonths(1).AddDays(-1);
            var end = to < now.Date ? to : now.Date;
            var filter = new ReportFilter { From = from, To = to, AgentId = agentUserId };
            var totals = SalesReportService.Totals(SalesReportService.Facts(db, filter, ReportScope.Unrestricted));
            var visits = VisitReportService.ByAgent(db, new ReportFilter { From = from, To = end < from ? from : end, AgentId = agentUserId }, ReportScope.Unrestricted)
                .FirstOrDefault(r => r.AgentUserID == agentUserId);

            // muddati 30 kundan ortiq o'tgan qarz (01_PHASE1_MVP qoidasi) — debet yozilgan paytdagi agent bo'yicha, mijozning hozirgi agenti emas
            var customers = db.CustomerLedger.Where(l => l.AgentUserID == agentUserId && l.Debit > 0).Select(l => l.CustomerID).Distinct().ToList();
            var debts = DebtService.Load(db, customers);
            decimal overdue = 0;
            foreach (var d in debts.Values)
                overdue += DebtMath.OverdueFor(d.Debits, d.Credits, end < from ? from : end, 30, agentUserId);
            var next = from.AddMonths(1);
            var shortage = db.Trips.Where(t => t.ExpeditorUserID == agentUserId && t.Status == DocumentStatus.Posted && t.PostedAt >= from && t.PostedAt < next)
                .Select(t => (decimal?)t.ShortageCost).Sum() ?? 0;
            return new KpiInputs { NetSales = totals.Net, CoveragePct = visits?.CoveragePct, OverdueDebt = overdue, ShortageCost = shortage };
        }

        /// <summary>Oy bo'yicha hisoblash (faol sxemali har agent uchun). Ish haqiga o'tkazilgani qayta hisoblanmaydi.</summary>
        public static List<KpiResult> Calculate(DistributionDbContext db, int year, int month, int userId, DateTime now)
        {
            if (month < 1 || month > 12 || year < 2000 || year > 2100) throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));
            var schemes = db.KpiSchemes.Where(s => s.IsActive).ToList();
            var existing = db.KpiResults.Where(r => r.Year == year && r.Month == month).ToList();
            foreach (var s in schemes)
            {
                var row = existing.FirstOrDefault(r => r.AgentUserID == s.AgentUserID);
                if (row != null && row.Status == KpiResult.Applied) continue;
                var x = Inputs(db, s.AgentUserID, year, month, now);
                var k = KpiMath.Compute(s, x);
                if (row == null)
                {
                    row = new KpiResult { Year = year, Month = month, AgentUserID = s.AgentUserID };
                    db.KpiResults.Add(row);
                    existing.Add(row);
                }
                row.EmployeeID = s.EmployeeID;
                row.NetSales = Math.Round(x.NetSales, 2);
                row.PlanAmount = s.MonthlyPlan;
                row.PlanPct = k.PlanPct;
                row.CoveragePct = x.CoveragePct;
                row.OverdueDebt = Math.Round(x.OverdueDebt, 2);
                row.ShortageCost = Math.Round(x.ShortageCost, 2);
                row.Commission = k.Commission;
                row.Bonus = k.Bonus;
                row.Penalty = k.Penalty;
                row.CalculatedBy = userId;
                row.CalculatedAt = now;
            }
            db.SaveChanges();
            return existing;
        }

        /// <summary>Qo'lda bonus/jarima va izoh (faqat o'tkazilmagan natija).</summary>
        public static void Adjust(DistributionDbContext db, int resultId, decimal manualBonus, decimal manualPenalty, string note)
        {
            var r = db.KpiResults.Find(resultId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (r.Status == KpiResult.Applied) throw new DstValidationException(DstLocalization.T("Kpi_AlreadyApplied"));
            if (manualBonus < 0 || manualPenalty < 0) throw new DstValidationException(DstLocalization.T("Error_PriceNegative"));
            r.ManualBonus = Math.Round(manualBonus, 2);
            r.ManualPenalty = Math.Round(manualPenalty, 2);
            r.Note = string.IsNullOrWhiteSpace(note) ? null : (note.Trim().Length > 300 ? note.Trim().Substring(0, 300) : note.Trim());
            db.SaveChanges();
        }

        /// <summary>
        /// HR ish haqiga o'tkazish: shu oyning QORALAMA ish haqi hisobidagi xodim qatoriga mukofot (% savdo + bonus) va jarima qo'shiladi,
        /// "to'lovga" qayta hisoblanadi (soliq foizi qatorning amaldagi foizi). Hisob tasdiqlangan bo'lsa — rad.
        /// </summary>
        public static void Apply(DistributionUnitOfWork uow, int resultId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var erp = uow.Erp;
            var r = db.KpiResults.Find(resultId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (r.Status == KpiResult.Applied) throw new DstValidationException(DstLocalization.T("Kpi_AlreadyApplied"));
            int employeeId = r.EmployeeID ?? db.KpiSchemes.Where(s => s.AgentUserID == r.AgentUserID).Select(s => s.EmployeeID).FirstOrDefault()
                             ?? throw new DstValidationException(DstLocalization.T("Kpi_NoEmployee"));
            var line = erp.HR_PAYROLL_LINES
                .Where(l => l.EmployeeID == employeeId && l.IsDeleted != true && l.HR_PAYROLL_HEADERS.Year == r.Year && l.HR_PAYROLL_HEADERS.Month == r.Month
                            && l.HR_PAYROLL_HEADERS.IsDeleted != true && l.HR_PAYROLL_HEADERS.Status == "Draft")
                .OrderByDescending(l => l.ID).FirstOrDefault()
                ?? throw new DstValidationException(DstLocalization.T("Kpi_NoPayroll", r.Year + "-" + r.Month.ToString("00")));

            decimal bonus = r.TotalBonus, penalty = r.TotalPenalty;
            ApplyToLine(line, bonus, penalty, "KPI " + r.Year + "-" + r.Month.ToString("00"));
            erp.SaveChanges();

            r.EmployeeID = employeeId;
            r.PayrollLineID = line.ID;
            r.AppliedBonus = bonus;
            r.AppliedPenalty = penalty;
            r.Status = KpiResult.Applied;
            r.AppliedBy = userId;
            r.AppliedAt = now;
            db.SaveChanges();
        }

        /// <summary>O'tkazishni qaytarish (ish haqi hisobi hali qoralama bo'lsa) — qatordan o'sha summalar ayriladi.</summary>
        public static void Revert(DistributionUnitOfWork uow, int resultId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var r = db.KpiResults.Find(resultId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (r.Status != KpiResult.Applied || !r.PayrollLineID.HasValue) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var line = uow.Erp.HR_PAYROLL_LINES.Find(r.PayrollLineID.Value);
            if (line == null || line.IsDeleted == true || line.HR_PAYROLL_HEADERS.Status != "Draft")
                throw new DstValidationException(DstLocalization.T("Kpi_PayrollLocked"));
            ApplyToLine(line, -(r.AppliedBonus ?? 0), -(r.AppliedPenalty ?? 0), null);
            uow.Erp.SaveChanges();
            r.Status = KpiResult.Calculated;
            r.PayrollLineID = null;
            r.AppliedBonus = null;
            r.AppliedPenalty = null;
            r.AppliedBy = null;
            r.AppliedAt = null;
            db.SaveChanges();
        }

        /// <summary>Qatorga mukofot/jarima qo'shish va to'lovga summani HR formulasi bilan qayta hisoblash.</summary>
        public static void ApplyToLine(tahsinERP.Models.HR_PAYROLL_LINES line, decimal bonus, decimal penalty, string note)
        {
            var taxPercent = HrPayrollCalculator.StoredIncomeTaxPercent(line) ?? HrPayrollCalculator.DefaultIncomeTaxPercent;
            line.Bonuses = Math.Max(0, (line.Bonuses ?? 0) + bonus);
            line.Penalties = Math.Max(0, (line.Penalties ?? 0) + penalty);
            line.NetSalary = HrPayrollCalculator.NetSalary(line.GrossSalary ?? 0, line.Bonuses ?? 0, line.Penalties ?? 0, taxPercent);
            if (note != null)
            {
                var notes = string.IsNullOrWhiteSpace(line.Notes) ? note : line.Notes + "; " + note;
                line.Notes = notes.Length > 500 ? notes.Substring(0, 500) : notes;
            }
        }
    }
}
