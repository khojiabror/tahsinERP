using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Finance;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;
using tahsinERP.Filters;

namespace tahsinERP.Controllers.HR
{
    // Xodimlar maoshi ko'rsatiladi — login va ruxsat talab qilinadi (avval anonim edi)
    [Permission]
    public class KoreshokController : Controller
    {
        private readonly DBTHSNEntities db = new DBTHSNEntities();
        private readonly HrEmployeeAllowanceService allowanceService;
        private readonly HrTimesheetSettingsService timesheetSettingsService;

        public KoreshokController()
        {
            allowanceService = new HrEmployeeAllowanceService(db);
            timesheetSettingsService = new HrTimesheetSettingsService(db);
        }

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

            // Xodimning oylik ochilish qoldig'i — Finance qoidasi bilan (avval xom SQL + try/catch, HRM-D15)
            var balances = new CounterpartyBalanceService(db);
            var latestOpeningDate = balances.LatestOpeningDate(to.Date, CounterpartyBalanceService.BalanceTypePayroll);
            var openingRow = latestOpeningDate.HasValue
                ? balances.OpeningContractBalances(latestOpeningDate.Value, CounterpartyBalanceService.BalanceTypePayroll)
                    .FirstOrDefault(x => x.CounterpartyID == employeeId)
                : null;
            decimal openingCalculated = openingRow?.TotalAmount ?? 0m;
            decimal openingPaid = openingRow?.PaidAmount ?? 0m;

            // Soatbay / ishbay "Hisoblangan": avval ish haqi (HR_PAYROLL_LINES), bo'lmasa tabel + tarif bo'yicha baholash
            int fromMonthKey = from.Year * 100 + from.Month;
            int toMonthKey = to.Year * 100 + to.Month;

            var hourlyFromPayroll = await SumPayrollNetAsync(employeeId, "Hourly", fromMonthKey, toMonthKey);

            // Faqat davr oylaridagi tabel qatorlari, bir marta (avval xodimning butun tarixi ikki marta yuklanardi, HRM-P12)
            var timesheetLines = await db.HR_TIMESHEET_LINES.AsNoTracking()
                .Where(l => l.EmployeeID == employeeId &&
                            (l.IsDeleted == null || l.IsDeleted == false) &&
                            (l.HR_TIMESHEET_HEADERS.IsDeleted == null || l.HR_TIMESHEET_HEADERS.IsDeleted == false) &&
                            (l.HR_TIMESHEET_HEADERS.Year * 100 + l.HR_TIMESHEET_HEADERS.Month) >= fromMonthKey &&
                            (l.HR_TIMESHEET_HEADERS.Year * 100 + l.HR_TIMESHEET_HEADERS.Month) <= toMonthKey)
                .Select(l => new TimesheetLineInPeriod
                {
                    Line = l,
                    TimesheetID = l.TimesheetID,
                    Year = l.HR_TIMESHEET_HEADERS.Year,
                    Month = l.HR_TIMESHEET_HEADERS.Month
                })
                .ToListAsync();

            foreach (var item in timesheetLines)
            {
                CountHoursInRange(item, from.Date, to.Date);
            }
            result.HourlyWorkedHours = timesheetLines.Sum(x => x.HoursInRange);
            result.HourlyWorkedDays = timesheetLines.Sum(x => x.DaysInRange);

            result.HourlyCalculated = hourlyFromPayroll;
            if (result.HourlyCalculated == 0m && result.HourlyWorkedHours > 0)
                result.HourlyCalculated = await EstimateHourlyGrossFromTimesheetsAsync(employee, timesheetLines);

            var pieceFromPayroll = await SumPayrollNetAsync(employeeId, "PieceRate", fromMonthKey, toMonthKey);

            result.PieceCalculated = pieceFromPayroll;
            if (result.PieceCalculated == 0m)
            {
                var pieceEntries = await db.HR_PIECE_RATE_WORK_ENTRIES.AsNoTracking()
                    .Where(e => e.EmployeeID == employeeId &&
                                e.WorkDate >= from.Date && e.WorkDate <= to.Date &&
                                (e.IsDeleted == null || e.IsDeleted == false))
                    .Select(e => new { e.ProductID, e.OperationID, e.WorkDate, e.Quantity })
                    .ToListAsync();
                if (pieceEntries.Count > 0)
                {
                    // Umumiy tarif qoidasi (avvalgi nusxada o'chirilgan tariflar ham olinardi, HRM-B12)
                    var resolver = await PieceRateTariffResolver.LoadAsync(db, from.Date, to.Date,
                        pieceEntries.Select(e => e.ProductID).Distinct().ToList());
                    var gross = pieceEntries.Sum(e => resolver.Amount(e.ProductID, e.OperationID, employee.PositionID, e.WorkDate, e.Quantity));
                    var allowancePercent = HrPayrollCalculator.AllowancePercent(allowanceService.GetByEmployeeId(employee.ID));
                    result.PieceCalculated = Math.Round(HrPayrollCalculator.ApplyAllowance(gross, allowancePercent), 2);
                }
            }

            // To'langan summa (xodimga bog'langan moliyaviy chiqimlardan)
            var employeeFullName = (employee.FullName ?? "").Trim();
            // Chiqim — Direction == 2 (Finance va HR dashboard bilan bir xil; avval "!= 1", HRM-D16)
            var directPaidTotal = await FinQueries.Active(db)
                .Where(t => t.Direction == 2 &&
                            // ID bo'yicha; ID'siz eski yozuvlar — faqat to'liq ism mosligi (avval Contains o'xshash ismlarni ham olardi, HRM-B12)
                            ((t.CounterpartyID.HasValue && t.CounterpartyID == employeeId) ||
                             (!t.CounterpartyID.HasValue && t.CounterpartyName != null && t.CounterpartyName.Trim() == employeeFullName)) &&
                            t.TxnDate >= from.Date && t.TxnDate < toExclusive)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            // Payroll postlari ko'pincha "Xodimlar" kontragenti bilan umumiy o'tadi.
            // Bunday holatda FinTransactions orqali payroll ID ni topib, xodim kesimidagi net summani olamiz.
            var payrollTxnIds = await FinQueries.Active(db)
                .Where(t => t.Direction == 2 &&
                            t.SourceDocType == "HR_PAYROLL" &&
                            t.SourceDocID.HasValue &&
                            t.TxnDate >= from.Date && t.TxnDate < toExclusive)
                .Select(t => t.SourceDocID.Value)
                .Distinct()
                .ToListAsync();

            decimal postedPayrollPaid = 0m;
            if (payrollTxnIds.Any())
            {
                postedPayrollPaid = await db.HR_PAYROLL_LINES
                    .Where(l => payrollTxnIds.Contains(l.PayrollID) &&
                                l.EmployeeID == employeeId &&
                                (l.IsDeleted == null || l.IsDeleted == false))
                    .SumAsync(l => (decimal?)l.NetSalary) ?? 0m;
            }

            var paidTotal = directPaidTotal + postedPayrollPaid;
            paidTotal += openingPaid;
            result.HourlyCalculated += openingCalculated;

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

            result.Notes.Add("Hisoblangan: avvalo ish haqi (payroll) bo'yicha (to'lovga summa); payroll bo'lmasa soatbay tabel + shtat rejasi va ishbay yozuvlar + tariflar bo'yicha HrPayroll bilan bir xil formulada taxminiy (soliqdan oldingi) summa.");
            result.Notes.Add("Eslatma: to'langan summa FinTransactions'dagi xodimga bog'langan chiqimlar + HR_PAYROLL postlari bo'yicha xodim net summasidan olinadi.");
            result.Notes.Add("Agar oyliklar umumiy provodka bilan (xodim IDsiz) o'tkazilgan bo'lsa, to'lovlar to'liq chiqmasligi mumkin.");

            return View(result);
        }

        private sealed class TimesheetLineInPeriod
        {
            public HR_TIMESHEET_LINES Line { get; set; }
            public int TimesheetID { get; set; }
            public int Year { get; set; }
            public int Month { get; set; }
            public decimal HoursInRange { get; set; }
            public decimal DaysInRange { get; set; }
        }

        private static void CountHoursInRange(TimesheetLineInPeriod item, DateTime from, DateTime to)
        {
            int daysInMonth = DateTime.DaysInMonth(item.Year, item.Month);
            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(item.Year, item.Month, d);
                if (date < from || date > to) continue;
                var h = TimesheetLineAccessor.GetHours(item.Line, d) ?? 0m;
                if (h > 0)
                {
                    item.HoursInRange += h;
                    item.DaysInRange += 1m;
                }
            }
        }

        private async Task<decimal> SumPayrollNetAsync(int employeeId, string payrollType, int fromMonthKey, int toMonthKey)
        {
            return await db.HR_PAYROLL_LINES
                .Where(l => l.EmployeeID == employeeId &&
                            (l.IsDeleted == null || l.IsDeleted == false) &&
                            (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                            l.HR_PAYROLL_HEADERS.PayrollType == payrollType &&
                            ((l.HR_PAYROLL_HEADERS.Year * 100) + l.HR_PAYROLL_HEADERS.Month) >= fromMonthKey &&
                            ((l.HR_PAYROLL_HEADERS.Year * 100) + l.HR_PAYROLL_HEADERS.Month) <= toMonthKey)
                .SumAsync(l => (decimal?)l.NetSalary) ?? 0m;
        }

        /// <summary>
        /// HrPayroll soatbay hisobi bilan bir xil formula (Services/HR/HrPayrollCalculator, HRM-D02):
        /// baza (maosh, bo'lmasa shtat rejasi/tarif tarmog'i), ustama, tabel fond soati, qo'shimcha soat koeffitsienti.
        /// Davr oy o'rtasidan boshlansa — oydagi soatlarning davrga tushgan qismi olinadi.
        /// </summary>
        private async Task<decimal> EstimateHourlyGrossFromTimesheetsAsync(HR_EMPLOYEES employee, List<TimesheetLineInPeriod> lines)
        {
            var withHours = lines.Where(l => l.HoursInRange > 0).ToList();
            if (withHours.Count == 0) return 0m;

            var periodStart = withHours.Min(l => new DateTime(l.Year, l.Month, 1));
            var periodEnd = withHours.Max(l => new DateTime(l.Year, l.Month, 1)).AddMonths(1).AddDays(-1);
            var staffingPlans = await HrPayrollCalculator.LoadStaffingPlansAsync(db, periodStart, periodEnd);
            var fundHoursByTimesheet = timesheetSettingsService.GetFundHoursOrDefault(withHours.Select(l => l.TimesheetID));
            var allowancePercent = HrPayrollCalculator.AllowancePercent(allowanceService.GetByEmployeeId(employee.ID));

            decimal totalGross = 0;
            foreach (var item in withHours)
            {
                var monthStart = new DateTime(item.Year, item.Month, 1);
                var plan = HrPayrollCalculator.FindPlan(staffingPlans, employee.OrgUnitID, employee.PositionID);
                var monthlyBase = HrPayrollCalculator.ResolveMonthlyBase(employee, plan, monthStart);
                var hourly = HrPayrollCalculator.CalculateHourly(monthlyBase, allowancePercent,
                    fundHoursByTimesheet[item.TimesheetID], item.HoursInRange, item.Line.OvertimeHours ?? 0);
                totalGross += hourly.GrossSalary;
            }

            return Math.Round(totalGross, 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
