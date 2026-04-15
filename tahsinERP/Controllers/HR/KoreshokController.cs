using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels.HR;

namespace tahsinERP.Controllers.HR
{
    public class KoreshokController : Controller
    {
        private readonly DBTHSNEntities db = new DBTHSNEntities();

        [AllowAnonymous]
        public async Task<ActionResult> Index()
        {
            var vm = new KoreshokFilterViewModel
            {
                DateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                DateTo = DateTime.Today
            };

            ViewBag.EmployeeID = new SelectList(
                await db.HR_EMPLOYEES
                    .Where(e => e.IsDeleted == null || e.IsDeleted == false)
                    .OrderBy(e => e.FullName)
                    .Select(e => new { e.ID, Name = (e.TabNo ?? "-") + " - " + e.FullName })
                    .ToListAsync(),
                "ID", "Name");

            return View(vm);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Calculate(KoreshokFilterViewModel vm)
        {
            if (vm.EmployeeID <= 0)
            {
                TempData["Error"] = "Xodim tanlanmagan";
                return RedirectToAction("Index");
            }
            if (vm.DateTo < vm.DateFrom)
            {
                TempData["Error"] = "Sana oralig'i noto'g'ri";
                return RedirectToAction("Index");
            }
            return RedirectToAction("Result", new
            {
                employeeId = vm.EmployeeID,
                from = vm.DateFrom.ToString("yyyy-MM-dd"),
                to = vm.DateTo.ToString("yyyy-MM-dd")
            });
        }

        [AllowAnonymous]
        public async Task<ActionResult> Result(int employeeId, DateTime from, DateTime to)
        {
            if (to < from) return RedirectToAction("Index");
            var toExclusive = to.Date.AddDays(1);

            var employee = await db.HR_EMPLOYEES.FirstOrDefaultAsync(e => e.ID == employeeId && (e.IsDeleted == null || e.IsDeleted == false));
            if (employee == null) return HttpNotFound();

            var result = new KoreshokResultViewModel
            {
                EmployeeID = employeeId,
                EmployeeName = employee.FullName,
                TabNo = employee.TabNo,
                DateFrom = from.Date,
                DateTo = to.Date
            };

            // Soatbay / ishbay "Hisoblangan": avval ish haqi (HR_PAYROLL_LINES), bo'lmasa tabel + tarif bo'yicha baholash
            int fromMonthKey = from.Year * 100 + from.Month;
            int toMonthKey = to.Year * 100 + to.Month;

            var hourlyFromPayroll = await db.HR_PAYROLL_LINES
                .Where(l => l.EmployeeID == employeeId &&
                            (l.IsDeleted == null || l.IsDeleted == false) &&
                            (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                            l.HR_PAYROLL_HEADERS.PayrollType == "Hourly" &&
                            ((l.HR_PAYROLL_HEADERS.Year * 100) + l.HR_PAYROLL_HEADERS.Month) >= fromMonthKey &&
                            ((l.HR_PAYROLL_HEADERS.Year * 100) + l.HR_PAYROLL_HEADERS.Month) <= toMonthKey)
                .SumAsync(l => (decimal?)l.NetSalary) ?? 0m;

            // Ishlangan kun/soat (timesheet lines bo'yicha kunma-kun kesim)
            var timesheetLines = await db.HR_TIMESHEET_LINES
                .Where(l => l.EmployeeID == employeeId &&
                            (l.IsDeleted == null || l.IsDeleted == false) &&
                            (l.HR_TIMESHEET_HEADERS.IsDeleted == null || l.HR_TIMESHEET_HEADERS.IsDeleted == false))
                .Select(l => new
                {
                    l,
                    l.HR_TIMESHEET_HEADERS.Year,
                    l.HR_TIMESHEET_HEADERS.Month
                })
                .ToListAsync();

            decimal workedHours = 0m;
            decimal workedDays = 0m;
            foreach (var item in timesheetLines)
            {
                int daysInMonth = DateTime.DaysInMonth(item.Year, item.Month);
                for (int d = 1; d <= daysInMonth; d++)
                {
                    var date = new DateTime(item.Year, item.Month, d);
                    if (date < from.Date || date > to.Date) continue;
                    var prop = typeof(HR_TIMESHEET_LINES).GetProperty($"Day{d:D2}Hours");
                    var h = prop?.GetValue(item.l) as decimal? ?? 0m;
                    if (h > 0)
                    {
                        workedHours += h;
                        workedDays += 1m;
                    }
                }
            }
            result.HourlyWorkedHours = workedHours;
            result.HourlyWorkedDays = workedDays;

            result.HourlyCalculated = hourlyFromPayroll;
            if (result.HourlyCalculated == 0m && workedHours > 0)
                result.HourlyCalculated = await EstimateHourlyGrossFromTimesheetsAsync(employee, from, to);

            var pieceFromPayroll = await db.HR_PAYROLL_LINES
                .Where(l => l.EmployeeID == employeeId &&
                            (l.IsDeleted == null || l.IsDeleted == false) &&
                            (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                            l.HR_PAYROLL_HEADERS.PayrollType == "PieceRate" &&
                            ((l.HR_PAYROLL_HEADERS.Year * 100) + l.HR_PAYROLL_HEADERS.Month) >= fromMonthKey &&
                            ((l.HR_PAYROLL_HEADERS.Year * 100) + l.HR_PAYROLL_HEADERS.Month) <= toMonthKey)
                .SumAsync(l => (decimal?)l.NetSalary) ?? 0m;

            result.PieceCalculated = pieceFromPayroll;
            if (result.PieceCalculated == 0m)
            {
                var pieceEntries = await db.HR_PIECE_RATE_WORK_ENTRIES
                    .Include(e => e.HR_EMPLOYEES)
                    .Where(e => e.EmployeeID == employeeId &&
                                e.WorkDate >= from.Date && e.WorkDate <= to.Date &&
                                (e.IsDeleted == null || e.IsDeleted == false))
                    .ToListAsync();
                if (pieceEntries.Count > 0)
                    result.PieceCalculated = CalculatePieceRateAmountForEntries(pieceEntries);
            }

            // To'langan summa (xodimga bog'langan moliyaviy chiqimlardan)
            var paidTotal = await db.FIN_TRANSACTIONS
                .Where(t => (t.IsDeleted == null || t.IsDeleted == false) &&
                            t.Direction != 1 && // real bazada ba'zi chiqimlar 2 dan boshqa qiymatlarda ham uchrashi mumkin
                            ((t.CounterpartyID.HasValue && t.CounterpartyID == employeeId) ||
                             (!t.CounterpartyID.HasValue && t.CounterpartyName != null && t.CounterpartyName.Contains(employee.FullName))) &&
                            t.TxnDate >= from.Date && t.TxnDate < toExclusive)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            if (result.TotalCalculated > 0)
            {
                var hourlyShare = result.HourlyCalculated / result.TotalCalculated;
                result.HourlyPaid = Math.Round(paidTotal * hourlyShare, 2);
                result.PiecePaid = paidTotal - result.HourlyPaid;
            }
            else
            {
                // Hisoblangan summa topilmasa ham xodimga to'langan real chiqim ko'rinib turishi kerak.
                result.HourlyPaid = paidTotal;
                result.PiecePaid = 0m;
            }

            result.HourlyBalance = result.HourlyCalculated - result.HourlyPaid;
            result.PieceBalance = result.PieceCalculated - result.PiecePaid;

            result.Notes.Add("Hisoblangan: avvalo ish haqi (payroll) bo'yicha; payroll bo'lmasa soatbay tabel + shtat rejasi (HrPayroll bilan bir xil formula), ishbay esa ishbay yozuvlar va tariflar bo'yicha.");
            result.Notes.Add("Eslatma: to'langan summa xodimga bog'langan moliyaviy chiqimlardan olinadi.");
            result.Notes.Add("Agar oyliklar umumiy provodka bilan (xodim IDsiz) o'tkazilgan bo'lsa, to'lovlar to'liq chiqmasligi mumkin.");

            return View(result);
        }

        /// <summary>
        /// HrPayroll GenerateHourlyPayrollLines bilan mos: soatbay / kunlik proporsiya.
        /// </summary>
        private async Task<decimal> EstimateHourlyGrossFromTimesheetsAsync(HR_EMPLOYEES employee, DateTime from, DateTime to)
        {
            var lines = await db.HR_TIMESHEET_LINES
                .Include(l => l.HR_TIMESHEET_HEADERS)
                .Where(l => l.EmployeeID == employee.ID && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();

            decimal totalGross = 0;

            foreach (var line in lines)
            {
                var h = line.HR_TIMESHEET_HEADERS;
                if (h == null || (h.IsDeleted ?? false)) continue;

                int year = h.Year;
                int month = h.Month;
                int daysInMonth = DateTime.DaysInMonth(year, month);
                decimal hoursInRange = 0;
                decimal workedDaysInRange = 0;
                for (int d = 1; d <= daysInMonth; d++)
                {
                    var date = new DateTime(year, month, d);
                    if (date < from.Date || date > to.Date) continue;
                    var prop = typeof(HR_TIMESHEET_LINES).GetProperty($"Day{d:D2}Hours");
                    var hh = prop?.GetValue(line) as decimal? ?? 0m;
                    if (hh > 0)
                    {
                        hoursInRange += hh;
                        workedDaysInRange += 1m;
                    }
                }
                if (hoursInRange <= 0) continue;

                var refDate = new DateTime(year, month, 15);
                var staffingPlan = await db.HR_STAFFING_PLAN
                    .FirstOrDefaultAsync(sp => sp.OrgUnitID == employee.OrgUnitID &&
                        sp.PositionID == employee.PositionID &&
                        sp.IsActive && (sp.IsDeleted == null || sp.IsDeleted == false) && sp.IsPieceRate == false &&
                        sp.EffectiveFrom <= refDate && (sp.EffectiveTo == null || sp.EffectiveTo >= refDate));

                decimal baseSalary = employee.BaseSalary ?? 0;
                decimal gross = 0;
                decimal hourlyRate = 0;

                if (staffingPlan != null)
                {
                    if (staffingPlan.IsFixedSalary)
                        gross = baseSalary * (workedDaysInRange / daysInMonth);
                    else if (staffingPlan.IsHourly)
                    {
                        hourlyRate = baseSalary / 160m;
                        gross = hourlyRate * hoursInRange;
                    }
                }
                else
                    gross = baseSalary * (workedDaysInRange / daysInMonth);

                totalGross += gross;
            }

            return Math.Round(totalGross, 2);
        }

        private decimal CalculatePieceRateAmountForEntries(List<HR_PIECE_RATE_WORK_ENTRIES> workEntries)
        {
            decimal totalAmount = 0;

            foreach (var entry in workEntries)
            {
                int? positionId = entry.HR_EMPLOYEES?.PositionID;

                var tariff = db.HR_PIECE_RATE_TARIFFS
                    .Where(t => t.ProductID == entry.ProductID &&
                               t.OperationID == entry.OperationID &&
                               t.IsActive &&
                               t.EffectiveFrom <= entry.WorkDate &&
                               (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate) &&
                               (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                    .OrderByDescending(t => t.PositionID)
                    .FirstOrDefault();

                if (tariff == null)
                {
                    tariff = db.HR_PIECE_RATE_TARIFFS
                        .Where(t => t.ProductID == entry.ProductID &&
                                   t.OperationID == entry.OperationID &&
                                   t.PositionID == null &&
                                   t.IsActive &&
                                   t.EffectiveFrom <= entry.WorkDate &&
                                   (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate))
                        .FirstOrDefault();
                }

                if (tariff != null)
                    totalAmount += entry.Quantity * tariff.RatePerUnit;
            }

            return totalAmount;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
