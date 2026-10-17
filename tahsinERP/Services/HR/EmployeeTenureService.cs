using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.HR
{
    /// <summary>
    /// Xodim staji (HRM-B16).
    ///  - Jami staj: ish davri (EmploymentDate .. bugun/bo'shatish) va barcha tarix davrlari BIRLASHTIRILADI
    ///    (saralanib, ustma-ust va tutash davrlar qo'shiladi) — avval tarix qatorlari orasidagi ustma-ustlik ikki marta sanalardi.
    ///  - Yil/oy/kun — kalendar bo'yicha (avval /365 va /30 taxmini).
    ///  - Xodim va tarix oldindan yuklangan bo'lsa — <see cref="CalculateTotalTenure"/> (so'rovsiz, HRM-P03).
    /// </summary>
    public class EmployeeTenureService
    {
        private readonly DBTHSNEntities db;

        public EmployeeTenureService(DBTHSNEntities context)
        {
            db = context;
        }

        public async Task<TenureResult> CalculateTotalTenureAsync(int employeeId)
        {
            var employee = await db.HR_EMPLOYEES.AsNoTracking().FirstOrDefaultAsync(e => e.ID == employeeId);
            if (employee == null)
            {
                return TenureResult.Zero;
            }

            var records = await db.HR_EMPLOYEE_HISTORY.AsNoTracking()
                .Where(h => h.EmployeeID == employeeId)
                .ToListAsync();
            return CalculateTotalTenure(employee, records);
        }

        /// <summary>Jami staj (xotirada). <paramref name="historyRecords"/> — shu xodimning tarix qatorlari.</summary>
        public static TenureResult CalculateTotalTenure(HR_EMPLOYEES employee, IEnumerable<HR_EMPLOYEE_HISTORY> historyRecords, DateTime? asOf = null)
        {
            var today = (asOf ?? DateTime.Today).Date;
            var openEnd = employee.IsActive ? today : (employee.DismissalDate?.Date ?? today);

            var intervals = new List<Tuple<DateTime, DateTime>>();
            if (employee.EmploymentDate.HasValue)
            {
                var end = employee.IsActive ? today : (employee.DismissalDate?.Date ?? today);
                intervals.Add(Tuple.Create(employee.EmploymentDate.Value.Date, end));
            }
            foreach (var h in historyRecords ?? Enumerable.Empty<HR_EMPLOYEE_HISTORY>())
            {
                intervals.Add(Tuple.Create(h.EffectiveDateFrom.Date, (h.EffectiveDateTo?.Date ?? openEnd)));
            }

            int totalDays = 0;
            DateTime? curStart = null, curEnd = null;
            foreach (var iv in intervals.Where(i => i.Item2 >= i.Item1).OrderBy(i => i.Item1))
            {
                if (curStart == null)
                {
                    curStart = iv.Item1;
                    curEnd = iv.Item2;
                }
                else if (iv.Item1 <= curEnd.Value.AddDays(1))
                {
                    if (iv.Item2 > curEnd.Value) curEnd = iv.Item2;
                }
                else
                {
                    totalDays += (curEnd.Value - curStart.Value).Days;
                    curStart = iv.Item1;
                    curEnd = iv.Item2;
                }
            }
            if (curStart != null)
            {
                totalDays += (curEnd.Value - curStart.Value).Days;
            }

            return TenureResult.FromDays(totalDays);
        }

        /// <summary>Bitta davr staji (kalendar yil/oy/kun).</summary>
        public TenureResult CalculateTenure(DateTime effectiveDateFrom, DateTime? effectiveDateTo)
        {
            var startDate = effectiveDateFrom.Date;
            var endDate = (effectiveDateTo ?? DateTime.Today).Date;
            if (endDate < startDate)
            {
                return TenureResult.Zero;
            }
            return TenureResult.Between(startDate, endDate);
        }
    }

    public class TenureResult
    {
        public int TotalDays { get; set; }
        public int Years { get; set; }
        public int Months { get; set; }
        public int Days { get; set; }

        public static TenureResult Zero => new TenureResult();

        /// <summary>Kalendar farqi: to'liq yillar, keyin to'liq oylar, qolgan kunlar.</summary>
        public static TenureResult Between(DateTime start, DateTime end)
        {
            int years = end.Year - start.Year;
            if (start.AddYears(years) > end) years--;
            var afterYears = start.AddYears(years);

            int months = (end.Year - afterYears.Year) * 12 + end.Month - afterYears.Month;
            if (afterYears.AddMonths(months) > end) months--;
            var afterMonths = afterYears.AddMonths(months);

            return new TenureResult
            {
                TotalDays = (end - start).Days,
                Years = years,
                Months = months,
                Days = (end - afterMonths).Days
            };
        }

        /// <summary>Birlashtirilgan davrlar yig'indisi uchun: kunlar sonini kalendar yil/oy/kunga o'giradi.</summary>
        public static TenureResult FromDays(int totalDays)
        {
            if (totalDays <= 0) return Zero;
            var baseDate = new DateTime(2000, 1, 1);
            return Between(baseDate, baseDate.AddDays(totalDays));
        }
    }
}
