using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;
using tahsinERP.Filters;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.HR
{
    /// <summary>
    /// Ishbay ish hajmi (HR_PIECE_RATE_WORK_ENTRIES) va oy/bo'lim tasdiqlari.
    /// Eski HR_TIMESHEET_HEADERS asosidagi ishbay oqimi (Edit, UpdateQuantity, ImportFromProduction, Delete, Grids,
    /// BulkDeleteGrid va yordamchilari) o'lik kod sifatida olib tashlandi.
    /// </summary>
    [Authorize]
    public class HrPieceRateTimesheetsController : Controller
    {
        private const string Approved = "Approved";
        private const string Draft = "Draft";

        private static readonly JsonSerializerSettings HtmlSafeJson = new JsonSerializerSettings
        {
            // <script> ichida xavfsiz: < > & ' belgilar \uXXXX ko'rinishida (HRM-S04)
            StringEscapeHandling = StringEscapeHandling.EscapeHtml
        };

        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrPieceRateTimesheetsController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // Olish so'rovi: HR/PieceRateTimesheets
        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Standart — joriy yil (avval filtrsiz butun tarix yuklanardi, HRM-P01)
            year = year ?? DateTime.Now.Year;
            var periodStart = new DateTime(year.Value, month.HasValue && month.Value > 0 ? month.Value : 1, 1);
            var periodEnd = month.HasValue && month.Value > 0 ? periodStart.AddMonths(1).AddDays(-1) : new DateTime(year.Value, 12, 31);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES.AsNoTracking()
                .Where(e => e.IsDeleted != true && e.WorkDate >= periodStart && e.WorkDate <= periodEnd);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query
                .Select(e => new
                {
                    e.ProductID,
                    e.OperationID,
                    e.WorkDate,
                    e.Quantity,
                    e.CreatedDT,
                    e.HR_EMPLOYEES.OrgUnitID,
                    e.HR_EMPLOYEES.PositionID
                })
                .ToListAsync();

            var approvals = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS.AsNoTracking()
                .Where(a => a.IsDeleted != true && a.Year == year.Value)
                .ToListAsync();

            var orgNames = await LoadOrgUnitNamesAsync();
            var resolver = await PieceRateTariffResolver.LoadAsync(db, periodStart, periodEnd);

            var groupedData = workEntries
                .GroupBy(e => new { e.WorkDate.Year, e.WorkDate.Month, e.OrgUnitID })
                .Select(g =>
                {
                    var approval = FindEffectiveApproval(approvals, g.Key.Year, g.Key.Month, g.Key.OrgUnitID);
                    return new HrPieceRateTimesheetIndexViewModel
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                        OrgUnitID = g.Key.OrgUnitID,
                        OrgUnitName = g.Key.OrgUnitID.HasValue && orgNames.TryGetValue(g.Key.OrgUnitID.Value, out var name) ? name : L.T("Hr_WholeCompany"),
                        CalculatedAmount = g.Sum(e => resolver.Amount(e.ProductID, e.OperationID, e.PositionID, e.WorkDate, e.Quantity)),
                        CreatedDT = g.Min(e => e.CreatedDT),
                        EntryCount = g.Count(),
                        Status = approval?.Status ?? Draft,
                        ApprovedDT = approval?.ApprovedDT,
                        ApprovedByUserID = approval?.ApprovedByUserID
                    };
                })
                .Where(x => string.IsNullOrEmpty(status) || x.Status == status)
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToList();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, orgUnitId);

            logger.Log("HrPieceRateTimesheets", "Index");
            return View(groupedData);
        }

        /// <summary>
        /// Guruh uchun amaldagi tasdiq: butun kompaniya bo'yicha tasdiqlangan bo'lsa — u (tahrir baribir bloklangan),
        /// aks holda shu bo'limning yozuvi (HRM-B20).
        /// </summary>
        private static HR_PIECE_RATE_TIMESHEET_APPROVALS FindEffectiveApproval(
            List<HR_PIECE_RATE_TIMESHEET_APPROVALS> approvals, int year, int month, int? orgUnitId)
        {
            var companyWide = approvals.FirstOrDefault(a => a.Year == year && a.Month == month && a.OrgUnitID == null);
            if (companyWide != null && companyWide.Status == Approved) return companyWide;
            return approvals.FirstOrDefault(a => a.Year == year && a.Month == month && a.OrgUnitID == orgUnitId) ?? companyWide;
        }

        private async Task<Dictionary<int, string>> LoadOrgUnitNamesAsync()
        {
            return await db.HR_ORG_UNITS.AsNoTracking()
                .Select(o => new { o.ID, o.Name })
                .ToDictionaryAsync(o => o.ID, o => o.Name);
        }

        // Olish so'rovi: HR/PieceRateTimesheets/yaratish
        public ActionResult Create()
        {
            return RedirectToAction("CreateSimple");
        }

        // Olish so'rovi: HR/PieceRateTimesheets/Details
        public async Task<ActionResult> Details(int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES.AsNoTracking()
                .Include(e => e.HR_EMPLOYEES.HR_ORG_UNITS)
                .Include(e => e.HR_EMPLOYEES.HR_POSITIONS)
                .Include(e => e.PRODUCT)
                .Include(e => e.HR_PIECE_RATE_OPERATIONS)
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.OrderBy(e => e.WorkDate).ThenBy(e => e.HR_EMPLOYEES.FullName).ToListAsync();

            // Tariflar bir marta (avval har yozuvga 1-2 so'rov)
            var resolver = await PieceRateTariffResolver.LoadAsync(db, startDate, endDate,
                workEntries.Select(e => e.ProductID).Distinct().ToList(), includeCurrency: true);

            var viewModel = workEntries.Select(entry =>
            {
                var tariff = resolver.Resolve(entry.ProductID, entry.OperationID, entry.HR_EMPLOYEES?.PositionID, entry.WorkDate);
                return new HrPieceRateTimesheetDetailsViewModel
                {
                    Entry = entry,
                    Tariff = tariff,
                    EntryAmount = tariff == null ? 0m : entry.Quantity * tariff.RatePerUnit
                };
            }).ToList();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
            ViewBag.OrgUnitName = orgUnitId.HasValue
                ? (await db.HR_ORG_UNITS.AsNoTracking().Where(o => o.ID == orgUnitId.Value).Select(o => o.Name).FirstOrDefaultAsync())
                : L.T("Hr_WholeCompany");

            return View(viewModel);
        }

        // POST so'rovi: HR/PieceRateTimesheets/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Approve(int year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets", "Approve"))
            {
                TempData["Error"] = L.T("Hr_NoApproveRight");
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            if (!month.HasValue || month.Value < 1 || month.Value > 12)
            {
                TempData["Error"] = L.T("Hr_BadMonthParam");
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            var startDate = new DateTime(year, month.Value, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            if (!await query.AnyAsync())
            {
                TempData["Error"] = L.T("Hr_PrNoDataForMonth");
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            var userId = UserHelper.GetUserId(User.Identity.Name);
            var approval = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .FirstOrDefaultAsync(a => a.Year == year && a.Month == month.Value && a.OrgUnitID == orgUnitId && a.IsDeleted != true);

            if (approval == null)
            {
                approval = new HR_PIECE_RATE_TIMESHEET_APPROVALS
                {
                    Year = year,
                    Month = month.Value,
                    OrgUnitID = orgUnitId,
                    Status = Draft,
                    CreatedByUserID = userId,
                    CreatedDT = DateTime.Now
                };
                db.HR_PIECE_RATE_TIMESHEET_APPROVALS.Add(approval);
            }
            else if (approval.Status == Approved)
            {
                TempData["Error"] = L.T("Hr_TsAlreadyApproved");
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            approval.Status = Approved;
            approval.ApprovedByUserID = userId;
            approval.ApprovedDT = DateTime.Now;
            approval.ModifiedByUserID = userId;
            approval.ModifiedDT = DateTime.Now;

            await db.SaveChangesAsync();

            var orgUnitName = HrLookups.NameOf(HrLookups.OrgUnits, orgUnitId) ?? "Butun kompaniya";
            logger.Log("HrPieceRateTimesheets", "Approve", $"Tabel: {year}-{month.Value:D2}, Bo'lim: {orgUnitName}");
            TempData["Success"] = L.T("Hr_TsApproved");
            return RedirectToAction("Index", new { year, month = month.Value, orgUnitId });
        }

        // Qo'shimcha grid-kiritish
        public async Task<ActionResult> GridCreate(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var vm = new HrPieceRateGridEntryViewModel
            {
                Year = year ?? DateTime.Now.Year,
                Month = month ?? DateTime.Now.Month,
                OrgUnitID = orgUnitId
            };

            vm.Rows.Add(new HrPieceRateGridRowViewModel { WorkDate = new DateTime(vm.Year, vm.Month, 1) });

            await PopulateGridClientDataAsync(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GridCreate(HrPieceRateGridEntryViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                var validRows = (model.Rows ?? new List<HrPieceRateGridRowViewModel>()).Where(IsCompleteRow).ToList();
                if (!validRows.Any())
                {
                    TempData["Error"] = L.T("Hr_NoRowsToSave");
                    await PopulateGridClientDataAsync(model);
                    return View(model);
                }

                var futureError = FutureMonthError(validRows.Select(r => r.WorkDate.Value));
                if (futureError != null)
                {
                    TempData["Error"] = futureError;
                    await PopulateGridClientDataAsync(model);
                    return View(model);
                }

                // Har qator o'z sanasi bo'yicha tekshiriladi (avval forma oyi bo'yicha)
                var blockMsg = await GetPieceRateEditBlockedMessageAsync(validRows.Select(r => Tuple.Create(r.WorkDate.Value, r.EmployeeID.Value)));
                if (blockMsg != null)
                {
                    TempData["Error"] = blockMsg;
                    await PopulateGridClientDataAsync(model);
                    return View(model);
                }

                int created = 0;
                int duplicates = 0;
                var userId = UserHelper.GetUserId(User.Identity.Name);
                var touched = new List<Tuple<DateTime, int>>();
                var existing = await LoadEntriesByKeyAsync(validRows);
                // Bir partiya ichidagi dublikatlar ham ushlanadi (HRM-B13)
                var batchKeys = new HashSet<string>();

                foreach (var row in validRows)
                {
                    var key = EntryKey(row.EmployeeID.Value, row.ProductID.Value, row.OperationID.Value, row.WorkDate.Value);
                    if (!batchKeys.Add(key))
                    {
                        duplicates++;
                        continue;
                    }

                    List<HR_PIECE_RATE_WORK_ENTRIES> sameKey;
                    existing.TryGetValue(key, out sameKey);
                    if (sameKey != null && sameKey.Any(e => e.IsDeleted != true))
                    {
                        duplicates++;
                        continue;
                    }

                    var deletedDuplicate = sameKey?.FirstOrDefault(e => e.IsDeleted == true);
                    if (deletedDuplicate != null)
                    {
                        deletedDuplicate.Quantity = row.Quantity.Value;
                        deletedDuplicate.IsDeleted = false;
                        deletedDuplicate.ModifiedByUserID = userId;
                        deletedDuplicate.ModifiedDT = DateTime.Now;
                    }
                    else
                    {
                        db.HR_PIECE_RATE_WORK_ENTRIES.Add(NewEntry(row, userId));
                    }
                    touched.Add(Tuple.Create(row.WorkDate.Value, row.EmployeeID.Value));
                    created++;
                }

                await db.SaveChangesAsync();
                if (created > 0)
                    await EnsurePieceRateDraftApprovalsAsync(touched);
                TempData["Success"] = L.T("Hr_PrEntriesSaved", created);
                if (duplicates > 0) TempData["Error"] = L.T("Hr_PrDuplicatesSkipped", duplicates);
                logger.Log("HrPieceRateTimesheets", "GridCreate", $"Davr: {model.Year}-{model.Month:D2}, Yangi: {created}, Dublikat: {duplicates}");
                return RedirectToAction("GridCreate", new { year = model.Year, month = model.Month, orgUnitId = model.OrgUnitID });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrPieceRateTimesheets.GridCreate] " + ex);
                TempData["Error"] = L.T("Hr_SaveError");
                await PopulateGridClientDataAsync(model);
                return View(model);
            }
        }

        // Qo'shimcha grid-tahrirlash (mavjud yozuvlarni jadvalda tahrirlash)
        public async Task<ActionResult> GridEdit(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var y = year ?? DateTime.Now.Year;
            var m = month ?? DateTime.Now.Month;
            var startDate = new DateTime(y, m, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES.AsNoTracking()
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);
            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var vm = new HrPieceRateGridEntryViewModel
            {
                Year = y,
                Month = m,
                OrgUnitID = orgUnitId,
                Rows = await query.OrderBy(e => e.WorkDate).ThenBy(e => e.ID)
                    .Select(e => new HrPieceRateGridRowViewModel
                    {
                        EntryID = e.ID,
                        EmployeeID = e.EmployeeID,
                        ProductID = e.ProductID,
                        OperationID = e.OperationID,
                        WorkDate = e.WorkDate,
                        Quantity = e.Quantity
                    }).ToListAsync()
            };

            await PopulateGridClientDataAsync(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GridEdit(HrPieceRateGridEntryViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                if (model.Rows == null)
                {
                    TempData["Error"] = L.T("Hr_TsNoDataToSave");
                    return RedirectToAction("GridEdit", new { year = model.Year, month = model.Month, orgUnitId = model.OrgUnitID });
                }

                var validRows = model.Rows.Where(IsCompleteRow).ToList();
                var futureError = FutureMonthError(validRows.Select(r => r.WorkDate.Value));
                if (futureError != null)
                {
                    TempData["Error"] = futureError;
                    await PopulateGridClientDataAsync(model);
                    return View(model);
                }

                var entryIds = validRows.Where(r => r.EntryID.HasValue).Select(r => r.EntryID.Value).Distinct().ToList();
                var originals = await db.HR_PIECE_RATE_WORK_ENTRIES
                    .Where(e => entryIds.Contains(e.ID) && e.IsDeleted != true)
                    .ToDictionaryAsync(e => e.ID);

                // Tasdiq qulfi YANGI va ESKI (sana, xodim) bo'yicha: tasdiqlangan oydan yozuvni ko'chirib bo'lmaydi (HRM-B07)
                var lockKeys = validRows.Select(r => Tuple.Create(r.WorkDate.Value, r.EmployeeID.Value))
                    .Concat(originals.Values.Select(e => Tuple.Create(e.WorkDate, e.EmployeeID)));
                var blockEdit = await GetPieceRateEditBlockedMessageAsync(lockKeys);
                if (blockEdit != null)
                {
                    TempData["Error"] = blockEdit;
                    return RedirectToAction("GridEdit", new { year = model.Year, month = model.Month, orgUnitId = model.OrgUnitID });
                }

                int updated = 0;
                int inserted = 0;
                var userId = UserHelper.GetUserId(User.Identity.Name);
                var now = DateTime.Now;
                var touched = new List<Tuple<DateTime, int>>();
                var deletedByKey = await LoadEntriesByKeyAsync(validRows.Where(r => !r.EntryID.HasValue).ToList());

                foreach (var row in validRows)
                {
                    if (row.EntryID.HasValue)
                    {
                        HR_PIECE_RATE_WORK_ENTRIES entry;
                        if (!originals.TryGetValue(row.EntryID.Value, out entry)) continue;

                        bool changed = entry.EmployeeID != row.EmployeeID.Value || entry.ProductID != row.ProductID.Value ||
                                       entry.OperationID != row.OperationID.Value || entry.WorkDate != row.WorkDate.Value ||
                                       entry.Quantity != row.Quantity.Value;
                        if (!changed) continue;

                        entry.EmployeeID = row.EmployeeID.Value;
                        entry.ProductID = row.ProductID.Value;
                        entry.OperationID = row.OperationID.Value;
                        entry.WorkDate = row.WorkDate.Value;
                        entry.Quantity = row.Quantity.Value;
                        // Tahrir Modified* ga yoziladi, Created* (audit izi) o'zgarmaydi (HRM-B07)
                        entry.ModifiedByUserID = userId;
                        entry.ModifiedDT = now;
                        touched.Add(Tuple.Create(row.WorkDate.Value, row.EmployeeID.Value));
                        updated++;
                    }
                    else
                    {
                        var key = EntryKey(row.EmployeeID.Value, row.ProductID.Value, row.OperationID.Value, row.WorkDate.Value);
                        List<HR_PIECE_RATE_WORK_ENTRIES> sameKey;
                        deletedByKey.TryGetValue(key, out sameKey);
                        var deletedDuplicate = sameKey?.FirstOrDefault(e => e.IsDeleted == true);
                        if (deletedDuplicate != null)
                        {
                            deletedDuplicate.Quantity = row.Quantity.Value;
                            deletedDuplicate.IsDeleted = false;
                            deletedDuplicate.ModifiedByUserID = userId;
                            deletedDuplicate.ModifiedDT = now;
                            sameKey.Remove(deletedDuplicate);
                        }
                        else
                        {
                            db.HR_PIECE_RATE_WORK_ENTRIES.Add(NewEntry(row, userId));
                        }
                        touched.Add(Tuple.Create(row.WorkDate.Value, row.EmployeeID.Value));
                        inserted++;
                    }
                }

                await db.SaveChangesAsync();
                if (updated + inserted > 0)
                    await EnsurePieceRateDraftApprovalsAsync(touched);
                TempData["Success"] = L.T("Hr_PrUpdatedInserted", updated, inserted);
                logger.Log("HrPieceRateTimesheets", "GridEdit", $"Davr: {model.Year}-{model.Month:D2}, Yangilandi: {updated}, Qo'shildi: {inserted}");
                return RedirectToAction("GridEdit", new { year = model.Year, month = model.Month, orgUnitId = model.OrgUnitID });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrPieceRateTimesheets.GridEdit] " + ex);
                TempData["Error"] = L.T("Hr_SaveError");
                await PopulateGridClientDataAsync(model);
                return View(model);
            }
        }

        #region Grid yordamchilari

        private static bool IsCompleteRow(HrPieceRateGridRowViewModel r) =>
            r != null && r.EmployeeID.HasValue && r.ProductID.HasValue && r.OperationID.HasValue &&
            r.WorkDate.HasValue && r.Quantity.HasValue && r.Quantity.Value > 0;

        private static string EntryKey(int employeeId, int productId, int operationId, DateTime workDate) =>
            employeeId + "|" + productId + "|" + operationId + "|" + workDate.Date.ToString("yyyyMMdd");

        private static string FutureMonthError(IEnumerable<DateTime> dates)
        {
            var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return dates.Any(d => new DateTime(d.Year, d.Month, 1) > currentMonth) ? L.T("Hr_TsNoFuture") : null;
        }

        private static HR_PIECE_RATE_WORK_ENTRIES NewEntry(HrPieceRateGridRowViewModel row, int? userId)
        {
            return new HR_PIECE_RATE_WORK_ENTRIES
            {
                EmployeeID = row.EmployeeID.Value,
                ProductID = row.ProductID.Value,
                OperationID = row.OperationID.Value,
                WorkDate = row.WorkDate.Value,
                Quantity = row.Quantity.Value,
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now,
                IsDeleted = false
            };
        }

        /// <summary>Qatorlar kalitlari bo'yicha mavjud (o'chirilganlari ham) yozuvlar — bitta so'rov (avval har qatorga 2 ta).</summary>
        private async Task<Dictionary<string, List<HR_PIECE_RATE_WORK_ENTRIES>>> LoadEntriesByKeyAsync(List<HrPieceRateGridRowViewModel> rows)
        {
            if (rows.Count == 0) return new Dictionary<string, List<HR_PIECE_RATE_WORK_ENTRIES>>();
            var employeeIds = rows.Select(r => r.EmployeeID.Value).Distinct().ToList();
            var minDate = rows.Min(r => r.WorkDate.Value).Date;
            var maxDate = rows.Max(r => r.WorkDate.Value).Date.AddDays(1);

            var entries = await db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => employeeIds.Contains(e.EmployeeID) && e.WorkDate >= minDate && e.WorkDate < maxDate)
                .ToListAsync();
            return entries
                .GroupBy(e => EntryKey(e.EmployeeID, e.ProductID, e.OperationID, e.WorkDate))
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Tasdiqlangan oy/bo'lim uchun ma'lumot kiritish/o'zgartirishni bloklaydi (avtomatik tasdiqni bekor qilmaymiz).
        /// Kalitlar — (sana, xodim); bo'lim xodimning joriy bo'limi. Tasdiqlar bitta so'rov bilan (HRM-D10).
        /// </summary>
        private async Task<string> GetPieceRateEditBlockedMessageAsync(IEnumerable<Tuple<DateTime, int>> dateEmployeePairs)
        {
            var pairs = dateEmployeePairs?.Where(p => p.Item2 > 0).ToList();
            if (pairs == null || pairs.Count == 0) return null;

            var periods = pairs.Select(p => p.Item1.Year * 100 + p.Item1.Month).Distinct().ToList();
            var employeeIds = pairs.Select(p => p.Item2).Distinct().ToList();

            var approved = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS.AsNoTracking()
                .Where(a => a.IsDeleted != true && a.Status == Approved && periods.Contains(a.Year * 100 + a.Month))
                .Select(a => new { a.Year, a.Month, a.OrgUnitID })
                .ToListAsync();
            if (approved.Count == 0) return null;

            var orgByEmployee = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => employeeIds.Contains(e.ID))
                .Select(e => new { e.ID, e.OrgUnitID })
                .ToDictionaryAsync(e => e.ID, e => e.OrgUnitID);

            foreach (var p in pairs)
            {
                int y = p.Item1.Year, m = p.Item1.Month;
                if (approved.Any(a => a.Year == y && a.Month == m && a.OrgUnitID == null))
                    return L.T("Hr_PrCompanyTsApproved", $"{y}-{m:D2}");

                int? org;
                orgByEmployee.TryGetValue(p.Item2, out org);
                if (org.HasValue && approved.Any(a => a.Year == y && a.Month == m && a.OrgUnitID == org))
                    return L.T("Hr_PrOrgTsApproved", $"{y}-{m:D2}");
            }
            return null;
        }

        private Task<string> GetPieceRateEditBlockedMessageAsync(DateTime workDate, int employeeId)
        {
            return GetPieceRateEditBlockedMessageAsync(new[] { Tuple.Create(workDate, employeeId) });
        }

        /// <summary>
        /// Yangi yoki o'zgargan yozuvlar uchun HR_PIECE_RATE_TIMESHEET_APPROVALS da Draft yozuv bor-yo'qligini ta'minlaydi (Index: Draft ko'rinishi).
        /// </summary>
        private async Task EnsurePieceRateDraftApprovalsAsync(IEnumerable<Tuple<DateTime, int>> dateEmployeePairs)
        {
            var pairs = dateEmployeePairs?.Where(p => p.Item2 > 0).ToList();
            if (pairs == null || pairs.Count == 0) return;

            var employeeIds = pairs.Select(p => p.Item2).Distinct().ToList();
            var orgByEmployee = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => employeeIds.Contains(e.ID))
                .Select(e => new { e.ID, e.OrgUnitID })
                .ToDictionaryAsync(e => e.ID, e => e.OrgUnitID);

            var groups = pairs
                .Select(p => new { p.Item1.Year, p.Item1.Month, OrgUnitID = orgByEmployee.TryGetValue(p.Item2, out var o) ? o : null })
                .Distinct()
                .ToList();
            var periods = groups.Select(g => g.Year * 100 + g.Month).Distinct().ToList();

            var existing = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .Where(a => periods.Contains(a.Year * 100 + a.Month))
                .ToListAsync();

            var uid = UserHelper.GetUserId(User.Identity.Name);
            var now = DateTime.Now;
            foreach (var g in groups)
            {
                var row = existing.FirstOrDefault(a => a.Year == g.Year && a.Month == g.Month && a.OrgUnitID == g.OrgUnitID);
                if (row == null)
                {
                    row = new HR_PIECE_RATE_TIMESHEET_APPROVALS
                    {
                        Year = g.Year,
                        Month = g.Month,
                        OrgUnitID = g.OrgUnitID,
                        Status = Draft,
                        CreatedByUserID = uid,
                        CreatedDT = now
                    };
                    db.HR_PIECE_RATE_TIMESHEET_APPROVALS.Add(row);
                    existing.Add(row);
                }
                else if (row.IsDeleted == true)
                {
                    row.IsDeleted = false;
                    row.Status = Draft;
                    row.ModifiedByUserID = uid;
                    row.ModifiedDT = now;
                }
            }
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Grid uchun mijoz ma'lumotlari: xodim/mahsulot/operatsiya ro'yxatlari va oy tariflari — bir marta JSON (HRM-P06).
        /// </summary>
        private async Task PopulateGridClientDataAsync(HrPieceRateGridEntryViewModel model)
        {
            var rows = model.Rows ?? new List<HrPieceRateGridRowViewModel>();
            var rowEmployeeIds = rows.Where(r => r.EmployeeID.HasValue).Select(r => r.EmployeeID.Value).Distinct().ToList();
            var rowProductIds = rows.Where(r => r.ProductID.HasValue).Select(r => r.ProductID.Value).Distinct().ToList();
            var rowOperationIds = rows.Where(r => r.OperationID.HasValue).Select(r => r.OperationID.Value).Distinct().ToList();

            var orgUnitId = model.OrgUnitID;
            var employees = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => (e.IsDeleted != true && (orgUnitId == null || e.OrgUnitID == orgUnitId)) || rowEmployeeIds.Contains(e.ID))
                .OrderBy(e => e.FullName)
                .Select(e => new { id = e.ID, name = e.FullName, positionId = e.PositionID })
                .ToListAsync();

            var products = await db.PRODUCTS.AsNoTracking()
                .Where(p => (p.IsFinishedGood == true && p.IsDeleted != true) || rowProductIds.Contains(p.ID))
                .OrderBy(p => p.PNo)
                .Select(p => new { id = p.ID, name = p.PNo })
                .ToListAsync();

            var operations = await db.HR_PIECE_RATE_OPERATIONS.AsNoTracking()
                .Where(o => o.IsActive || rowOperationIds.Contains(o.ID))
                .OrderBy(o => o.SortOrder).ThenBy(o => o.Name)
                .Select(o => new { id = o.ID, name = o.Name })
                .ToListAsync();

            var monthStart = new DateTime(model.Year, model.Month, 1);
            var dates = rows.Where(r => r.WorkDate.HasValue).Select(r => r.WorkDate.Value.Date).ToList();
            var from = dates.Count > 0 && dates.Min() < monthStart ? dates.Min() : monthStart;
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var to = dates.Count > 0 && dates.Max() > monthEnd ? dates.Max() : monthEnd;

            var tariffs = await db.HR_PIECE_RATE_TARIFFS.AsNoTracking()
                .Where(t => t.IsActive && t.IsDeleted != true && t.OperationID != null &&
                            t.EffectiveFrom <= to && (t.EffectiveTo == null || t.EffectiveTo >= from))
                .Select(t => new { t.ID, t.ProductID, t.OperationID, t.PositionID, t.RatePerUnit, t.EffectiveFrom, t.EffectiveTo })
                .ToListAsync();

            var data = new
            {
                employees,
                products,
                operations,
                tariffs = tariffs.Select(t => new
                {
                    id = t.ID,
                    p = t.ProductID,
                    o = t.OperationID,
                    pos = t.PositionID,
                    rate = t.RatePerUnit,
                    from = t.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    to = t.EffectiveTo.HasValue ? t.EffectiveTo.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null
                })
            };

            var clientRows = rows.Select(r => new
            {
                entryId = r.EntryID,
                employeeId = r.EmployeeID,
                productId = r.ProductID,
                operationId = r.OperationID,
                workDate = r.WorkDate.HasValue ? r.WorkDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
                quantity = r.Quantity
            });

            ViewBag.GridDataJson = JsonConvert.SerializeObject(data, HtmlSafeJson);
            ViewBag.GridRowsJson = JsonConvert.SerializeObject(clientRows, HtmlSafeJson);
        }

        #endregion

        // Olish so'rovi: HR/PieceRateTimesheets/CreateSimple
        public async Task<ActionResult> CreateSimple()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            await PopulateCreateSimpleViewBags();
            return View();
        }

        // Olish so'rovi: HR/PieceRateTimesheets/EditWorkEntry/5 (grid yoki ro'yxatdan bitta yozuv)
        public async Task<ActionResult> EditWorkEntry(int? id, int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var entry = await db.HR_PIECE_RATE_WORK_ENTRIES.AsNoTracking()
                .FirstOrDefaultAsync(e => e.ID == id.Value && e.IsDeleted != true);
            if (entry == null)
            {
                return HttpNotFound();
            }

            var blockEdit = await GetPieceRateEditBlockedMessageAsync(entry.WorkDate, entry.EmployeeID);
            if (blockEdit != null)
            {
                TempData["Error"] = blockEdit;
                return RedirectToAction("GridEdit", new { year = year ?? entry.WorkDate.Year, month = month ?? entry.WorkDate.Month, orgUnitId });
            }

            await PopulateCreateSimpleViewBags(entry.EmployeeID, entry.ProductID, entry.OperationID);
            SetEditEntryViewBags(entry.ID, entry.WorkDate, entry.Quantity,
                Url.Action("GridEdit", "HrPieceRateTimesheets", new { year = year ?? entry.WorkDate.Year, month = month ?? entry.WorkDate.Month, orgUnitId }));
            return View("CreateSimple");
        }

        private void SetEditEntryViewBags(int entryId, DateTime workDate, decimal quantity, string returnUrl)
        {
            ViewBag.EntryID = entryId;
            ViewBag.PrefillWorkDate = workDate.ToString("yyyy-MM-dd");
            ViewBag.PrefillQuantity = quantity;
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.PageTitle = L.T("Hr_PrEntryEdit");
        }

        // POST so'rovi: HR/PieceRateTimesheets/CreateSimple
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateSimple(int? entryId, int EmployeeID, int ProductID, int OperationID, DateTime WorkDate, decimal Quantity, string returnUrl = null)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                var userId = UserHelper.GetUserId(User.Identity.Name);
                var futureError = FutureMonthError(new[] { WorkDate });

                if (entryId.HasValue)
                {
                    var entry = await db.HR_PIECE_RATE_WORK_ENTRIES
                        .FirstOrDefaultAsync(e => e.ID == entryId.Value && e.IsDeleted != true);
                    if (entry == null)
                    {
                        TempData["Error"] = L.T("Hr_EntryNotFound");
                        await PopulateCreateSimpleViewBags();
                        return View();
                    }

                    // Eski va yangi (sana, xodim) bo'yicha tasdiq qulfi (HRM-B07)
                    var editBlockedMessage = futureError ?? await GetPieceRateEditBlockedMessageAsync(new[]
                    {
                        Tuple.Create(WorkDate, EmployeeID),
                        Tuple.Create(entry.WorkDate, entry.EmployeeID)
                    });
                    if (editBlockedMessage != null)
                    {
                        TempData["Error"] = editBlockedMessage;
                        await PopulateCreateSimpleViewBags(EmployeeID, ProductID, OperationID);
                        SetEditEntryViewBags(entry.ID, WorkDate, Quantity, returnUrl);
                        return View();
                    }

                    entry.EmployeeID = EmployeeID;
                    entry.ProductID = ProductID;
                    entry.OperationID = OperationID;
                    entry.WorkDate = WorkDate;
                    entry.Quantity = Quantity;
                    entry.ModifiedByUserID = userId;
                    entry.ModifiedDT = DateTime.Now;
                    await db.SaveChangesAsync();
                    await EnsurePieceRateDraftApprovalsAsync(new[] { Tuple.Create(WorkDate, EmployeeID) });
                    logger.Log("HrPieceRateTimesheets", "EditWorkEntry", $"YozuvID: {entry.ID}, Sana: {WorkDate:dd.MM.yyyy}, Miqdor: {Quantity}");
                    TempData["Success"] = L.T("Hr_EntryUpdated");
                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("CreateSimple");
                }

                var blockSimple = futureError ?? await GetPieceRateEditBlockedMessageAsync(WorkDate, EmployeeID);
                if (blockSimple != null)
                {
                    TempData["Error"] = blockSimple;
                    await PopulateCreateSimpleViewBags();
                    return View();
                }

                var existing = await db.HR_PIECE_RATE_WORK_ENTRIES
                    .FirstOrDefaultAsync(e => e.EmployeeID == EmployeeID &&
                                             e.ProductID == ProductID &&
                                             e.OperationID == OperationID &&
                                             e.WorkDate == WorkDate &&
                                             e.IsDeleted != true);

                if (existing != null)
                {
                    existing.Quantity = Quantity;
                    existing.ModifiedByUserID = userId;
                    existing.ModifiedDT = DateTime.Now;
                }
                else
                {
                    db.HR_PIECE_RATE_WORK_ENTRIES.Add(new HR_PIECE_RATE_WORK_ENTRIES
                    {
                        EmployeeID = EmployeeID,
                        ProductID = ProductID,
                        OperationID = OperationID,
                        WorkDate = WorkDate,
                        Quantity = Quantity,
                        CreatedByUserID = userId,
                        CreatedDT = DateTime.Now,
                        IsDeleted = false
                    });
                }

                await db.SaveChangesAsync();
                await EnsurePieceRateDraftApprovalsAsync(new[] { Tuple.Create(WorkDate, EmployeeID) });

                var labels = await db.HR_EMPLOYEES.AsNoTracking().Where(e => e.ID == EmployeeID)
                    .Select(e => new
                    {
                        Employee = e.FullName + " (" + (e.TabNo ?? "-") + ")",
                        Product = db.PRODUCTS.Where(p => p.ID == ProductID).Select(p => p.Name ?? p.PNo).FirstOrDefault(),
                        Operation = db.HR_PIECE_RATE_OPERATIONS.Where(o => o.ID == OperationID).Select(o => o.Name).FirstOrDefault()
                    }).FirstOrDefaultAsync();
                logger.Log("HrPieceRateTimesheets", "CreateSimple", $"Xodim: {labels?.Employee ?? "-"}, Mahsulot: {labels?.Product ?? "-"}, Operatsiya: {labels?.Operation ?? "-"}, Sana: {WorkDate:dd.MM.yyyy}, Miqdor: {Quantity}");
                TempData["Success"] = L.T("Hr_PrEntrySaved");
                return RedirectToAction("CreateSimple");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrPieceRateTimesheets.CreateSimple] " + ex);
                TempData["Error"] = L.T("Hr_SaveError");
                await PopulateCreateSimpleViewBags();
                return View();
            }
        }

        /// <summary>
        /// GridEdit jadvalidan bitta yozuvni o'chirish (soft delete).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteGridEntry(int id, int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var entry = await db.HR_PIECE_RATE_WORK_ENTRIES.FirstOrDefaultAsync(e => e.ID == id && e.IsDeleted != true);
            if (entry == null)
            {
                TempData["Error"] = L.T("Hr_EntryNotFound");
                return RedirectToAction("GridEdit", new { year, month, orgUnitId });
            }

            var blockEdit = await GetPieceRateEditBlockedMessageAsync(entry.WorkDate, entry.EmployeeID);
            if (blockEdit != null)
            {
                TempData["Error"] = blockEdit;
                return RedirectToAction("GridEdit", new { year, month, orgUnitId });
            }

            entry.IsDeleted = true;
            entry.ModifiedByUserID = UserHelper.GetUserId(User.Identity.Name);
            entry.ModifiedDT = DateTime.Now;
            await db.SaveChangesAsync();
            logger.Log("HrPieceRateTimesheets", "DeleteGridEntry", $"YozuvID: {id}");
            TempData["Success"] = L.T("Hr_EntryDeleted");
            return RedirectToAction("GridEdit", new { year, month, orgUnitId });
        }

        // Olish so'rovi: HR/PieceRateTimesheets/GetTariff (CreateSimple sahifasi uchun)
        [Permission(Controller = "HrPieceRateTimesheets")]
        public async Task<JsonResult> GetTariff(int productId, int operationId, int? employeeId = null, DateTime? workDate = null)
        {
            try
            {
                var tariffDate = (workDate ?? DateTime.Now).Date;
                int? positionId = null;
                if (employeeId.HasValue)
                {
                    positionId = await db.HR_EMPLOYEES.AsNoTracking()
                        .Where(e => e.ID == employeeId.Value)
                        .Select(e => e.PositionID)
                        .FirstOrDefaultAsync();
                }

                var resolver = await PieceRateTariffResolver.LoadAsync(db, tariffDate, tariffDate, new[] { productId }, includeCurrency: true);
                var tariff = resolver.Resolve(productId, operationId, positionId, tariffDate);

                if (tariff != null)
                {
                    return Json(new
                    {
                        success = true,
                        tariff = new
                        {
                            ratePerUnit = tariff.RatePerUnit,
                            currencyShortname = tariff.CURRENCy?.Shortname ?? ""
                        }
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = false, message = L.T("Hr_PrTariffNotFound") }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrPieceRateTimesheets.GetTariff] " + ex);
                return Json(new { success = false, message = L.T("Hr_PrTariffLoadError") }, JsonRequestBehavior.AllowGet);
            }
        }

        private async Task PopulateCreateSimpleViewBags(int? selectedEmployeeId = null, int? selectedProductId = null, int? selectedOperationId = null)
        {
            // Ishbay lavozimlaridagi faol xodimlar; ishbay lavozim sozlanmagan bo'lsa — barcha faol xodimlar
            var pieceRatePositionIds = db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == true && sp.IsDeleted != true)
                .Select(sp => sp.PositionID);
            bool hasPieceRatePositions = await pieceRatePositionIds.AnyAsync();

            var employees = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => (e.IsActive && e.IsDeleted != true &&
                             (!hasPieceRatePositions || (e.PositionID.HasValue && pieceRatePositionIds.Contains(e.PositionID.Value))))
                            || e.ID == selectedEmployeeId)
                .OrderBy(e => e.FullName)
                .Select(e => new { e.ID, e.FullName })
                .ToListAsync();

            var products = await db.PRODUCTS.AsNoTracking()
                .Where(p => (p.IsFinishedGood == true && p.IsDeleted != true) || p.ID == selectedProductId)
                .OrderBy(p => p.PNo)
                .Select(p => new { p.ID, p.PNo })
                .ToListAsync();

            ViewBag.EmployeeID = new SelectList(employees, "ID", "FullName", selectedEmployeeId);
            ViewBag.ProductID = new SelectList(products, "ID", "PNo", selectedProductId);
            ViewBag.OperationID = HrLookups.SelectList(HrLookups.Operations, selectedOperationId);
        }

        // Olish so'rovi: HR/PieceRateTimesheets/DeleteEntries
        public async Task<ActionResult> DeleteEntries(int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.OrgUnitId = orgUnitId;
            ViewBag.EntryCount = await query.CountAsync();
            ViewBag.MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
            ViewBag.OrgUnitName = orgUnitId.HasValue
                ? (await db.HR_ORG_UNITS.AsNoTracking().Where(o => o.ID == orgUnitId.Value).Select(o => o.Name).FirstOrDefaultAsync()) ?? L.T("Hr_WholeCompany")
                : L.T("Hr_WholeCompany");

            return View();
        }

        // POST so'rovi: HR/PieceRateTimesheets/DeleteEntries
        [HttpPost, ActionName("DeleteEntries")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteEntriesConfirmed(int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.ToListAsync();

            // O'chirilgan ish haqi hisobi bloklamaydi; butun kompaniya rejimida bo'lim hisoblari ham tekshiriladi (HRM-B08)
            var hasPayrolls = await db.HR_PAYROLL_HEADERS
                .AnyAsync(p => p.Year == year && p.Month == month &&
                    p.PayrollType == "PieceRate" &&
                    p.IsDeleted != true &&
                    (orgUnitId == null || p.OrgUnitID == null || p.OrgUnitID == orgUnitId.Value));

            if (hasPayrolls)
            {
                TempData["Error"] = L.T("Hr_PrPayrollExists");
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Tasdiq qulfi har yozuv xodimining bo'limi bo'yicha (avval faqat aynan shu OrgUnitID tasdig'i tekshirilardi, HRM-B08)
            var blockMsg = await GetPieceRateEditBlockedMessageAsync(workEntries.Select(e => Tuple.Create(e.WorkDate, e.EmployeeID)));
            if (blockMsg != null)
            {
                TempData["Error"] = L.T("Hr_PrCannotDeleteApproved") + " " + blockMsg;
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            var userId = UserHelper.GetUserId(User.Identity.Name);
            var now = DateTime.Now;
            foreach (var entry in workEntries)
            {
                entry.IsDeleted = true;
                entry.ModifiedByUserID = userId;
                entry.ModifiedDT = now;
            }

            var approval = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .FirstOrDefaultAsync(a => a.Year == year && a.Month == month && a.OrgUnitID == orgUnitId && a.IsDeleted != true);
            if (approval != null)
            {
                approval.IsDeleted = true;
                approval.ModifiedByUserID = userId;
                approval.ModifiedDT = now;
            }

            await db.SaveChangesAsync();

            var orgUnitName = HrLookups.NameOf(HrLookups.OrgUnits, orgUnitId) ?? "Butun kompaniya";
            logger.Log("HrPieceRateTimesheets", "DeleteEntries", $"Tabel: {year}-{month:D2}, Bo'lim: {orgUnitName}, YozuvlarSoni: {workEntries.Count}");
            TempData["Success"] = L.T("Hr_PrEntriesDeleted", workEntries.Count);
            return RedirectToAction("Index", new { year, month, orgUnitId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
