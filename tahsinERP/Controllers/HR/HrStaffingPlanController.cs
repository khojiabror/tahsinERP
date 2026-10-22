using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;
using tahsinERP.I18n;
using System.Collections.Generic;
using ClosedXML.Excel;
using System.IO;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrStaffingPlanController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrStaffingPlanController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // Olish so'rovi: HR/StaffingPlan
        public async Task<ActionResult> Index(int? orgUnitId, int? positionId, bool? isActive)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_STAFFING_PLAN> query = db.HR_STAFFING_PLAN.AsNoTracking()
                .Where(sp => sp.IsDeleted != true)
                .Include(sp => sp.HR_ORG_UNITS)
                .Include(sp => sp.HR_POSITIONS)
                .Include(sp => sp.HR_SALARY_GRADES);

            if (orgUnitId.HasValue)
            {
                query = query.Where(sp => sp.OrgUnitID == orgUnitId.Value);
            }

            if (positionId.HasValue)
            {
                query = query.Where(sp => sp.PositionID == positionId.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(sp => sp.IsActive == isActive.Value);
            }
            else
            {
                query = query.Where(sp => sp.IsActive);
            }

            var staffingPlans = await query.OrderBy(sp => sp.HR_ORG_UNITS.Name).ThenBy(sp => sp.HR_POSITIONS.Name).ToListAsync();

            // Olish barcha employee counts in one query ga avoid N+1 problem
            var employeeCounts = (await db.HR_EMPLOYEES
                .Where(e => e.IsActive && e.IsDeleted != true && e.OrgUnitID.HasValue && e.PositionID.HasValue)
                .GroupBy(e => new { e.OrgUnitID, e.PositionID })
                .Select(g => new { g.Key.OrgUnitID, g.Key.PositionID, Count = g.Count() })
                .ToListAsync())
                .ToDictionary(c => Tuple.Create(c.OrgUnitID.Value, c.PositionID.Value), c => c.Count);

            var viewModels = staffingPlans.Select(sp =>
            {
                var vm = new HrStaffingPlanViewModel
                {
                    ID = sp.ID,
                    OrgUnitID = sp.OrgUnitID,
                    OrgUnitName = sp.HR_ORG_UNITS?.Name,
                    PositionID = sp.PositionID,
                    PositionName = sp.HR_POSITIONS?.Name,
                    SalaryGradeID = sp.SalaryGradeID,
                    SalaryGradeName = sp.HR_SALARY_GRADES?.Name,
                    PlannedHeadcount = sp.PlannedHeadcount,
                    IsPieceRate = sp.IsPieceRate,
                    IsHourly = sp.IsHourly,
                    IsFixedSalary = sp.IsFixedSalary,
                    EffectiveFrom = sp.EffectiveFrom,
                    EffectiveTo = sp.EffectiveTo,
                    IsActive = sp.IsActive
                };

                // Olish actual headcount dan pre-loaded employee counts
                int count;
                vm.ActualHeadcount = employeeCounts.TryGetValue(Tuple.Create(sp.OrgUnitID, sp.PositionID), out count) ? count : 0;

                return vm;
            }).ToList();

            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, orgUnitId);
            ViewBag.PositionID = HrLookups.SelectList(HrLookups.Positions, positionId);

            logger.Log("HrStaffingPlan", "Index");
            return View(viewModels);
        }

        // Olish so'rovi: HR/StaffingPlan/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var staffingPlan = await db.HR_STAFFING_PLAN.AsNoTracking()
                .Where(sp => sp.ID == id && sp.IsDeleted != true)
                .Include(sp => sp.HR_ORG_UNITS)
                .Include(sp => sp.HR_POSITIONS)
                .Include(sp => sp.HR_SALARY_GRADES)
                .FirstOrDefaultAsync();

            if (staffingPlan == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrStaffingPlanViewModel
            {
                ID = staffingPlan.ID,
                OrgUnitID = staffingPlan.OrgUnitID,
                OrgUnitName = staffingPlan.HR_ORG_UNITS?.Name,
                PositionID = staffingPlan.PositionID,
                PositionName = staffingPlan.HR_POSITIONS?.Name,
                SalaryGradeID = staffingPlan.SalaryGradeID,
                SalaryGradeName = staffingPlan.HR_SALARY_GRADES?.Name,
                PlannedHeadcount = staffingPlan.PlannedHeadcount,
                IsPieceRate = staffingPlan.IsPieceRate,
                IsHourly = staffingPlan.IsHourly,
                IsFixedSalary = staffingPlan.IsFixedSalary,
                EffectiveFrom = staffingPlan.EffectiveFrom,
                EffectiveTo = staffingPlan.EffectiveTo,
                IsActive = staffingPlan.IsActive,
                ActualHeadcount = await db.HR_EMPLOYEES.CountAsync(e => 
                    e.OrgUnitID == staffingPlan.OrgUnitID && 
                    e.PositionID == staffingPlan.PositionID &&
                    e.IsActive && e.IsDeleted != true)
            };

            logger.Log("HrStaffingPlan", "Details", $"Bo'lim: {viewModel.OrgUnitName}, Lavozim: {viewModel.PositionName}");
            return View(viewModel);
        }

        // Olish so'rovi: HR/StaffingPlan/yaratish
        public ActionResult Create(int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            PopulateViewBags(null, orgUnitId);
            return View();
        }

        // POST so'rovi: HR/StaffingPlan/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HrStaffingPlanViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                var staffingPlan = new HR_STAFFING_PLAN
                {
                    OrgUnitID = viewModel.OrgUnitID,
                    PositionID = viewModel.PositionID,
                    SalaryGradeID = viewModel.SalaryGradeID,
                    PlannedHeadcount = viewModel.PlannedHeadcount,
                    IsPieceRate = viewModel.IsPieceRate,
                    IsHourly = viewModel.IsHourly,
                    IsFixedSalary = viewModel.IsFixedSalary,
                    EffectiveFrom = viewModel.EffectiveFrom,
                    EffectiveTo = viewModel.EffectiveTo,
                    IsActive = viewModel.IsActive
                };

                db.HR_STAFFING_PLAN.Add(staffingPlan);
                await db.SaveChangesAsync();

                var orgUnitName = HrLookups.NameOf(HrLookups.OrgUnits, staffingPlan.OrgUnitID) ?? "-";
                var positionName = HrLookups.NameOf(HrLookups.Positions, staffingPlan.PositionID) ?? "-";
                logger.Log("HrStaffingPlan", "Create", $"Bo'lim: {orgUnitName}, Lavozim: {positionName}, RejaSoni: {staffingPlan.PlannedHeadcount}");
                return RedirectToAction("Index");
            }

            PopulateViewBags(viewModel, null);
            return View(viewModel);
        }

        // Olish so'rovi: HR/StaffingPlan/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var staffingPlan = await db.HR_STAFFING_PLAN.FindAsync(id);
            if (staffingPlan == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrStaffingPlanViewModel
            {
                ID = staffingPlan.ID,
                OrgUnitID = staffingPlan.OrgUnitID,
                PositionID = staffingPlan.PositionID,
                SalaryGradeID = staffingPlan.SalaryGradeID,
                PlannedHeadcount = staffingPlan.PlannedHeadcount,
                IsPieceRate = staffingPlan.IsPieceRate,
                IsHourly = staffingPlan.IsHourly,
                IsFixedSalary = staffingPlan.IsFixedSalary,
                EffectiveFrom = staffingPlan.EffectiveFrom,
                EffectiveTo = staffingPlan.EffectiveTo,
                IsActive = staffingPlan.IsActive
            };

            PopulateViewBags(viewModel, null);
            return View(viewModel);
        }

        // POST so'rovi: HR/StaffingPlan/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HrStaffingPlanViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                var staffingPlan = await db.HR_STAFFING_PLAN.FindAsync(viewModel.ID);
                if (staffingPlan == null)
                {
                    return HttpNotFound();
                }

                staffingPlan.OrgUnitID = viewModel.OrgUnitID;
                staffingPlan.PositionID = viewModel.PositionID;
                staffingPlan.SalaryGradeID = viewModel.SalaryGradeID;
                staffingPlan.PlannedHeadcount = viewModel.PlannedHeadcount;
                staffingPlan.IsPieceRate = viewModel.IsPieceRate;
                staffingPlan.IsHourly = viewModel.IsHourly;
                staffingPlan.IsFixedSalary = viewModel.IsFixedSalary;
                staffingPlan.EffectiveFrom = viewModel.EffectiveFrom;
                staffingPlan.EffectiveTo = viewModel.EffectiveTo;
                staffingPlan.IsActive = viewModel.IsActive;

                db.Entry(staffingPlan).State = EntityState.Modified;
                await db.SaveChangesAsync();

                var orgUnitName = HrLookups.NameOf(HrLookups.OrgUnits, staffingPlan.OrgUnitID) ?? "-";
                var positionName = HrLookups.NameOf(HrLookups.Positions, staffingPlan.PositionID) ?? "-";
                logger.Log("HrStaffingPlan", "Edit", $"Bo'lim: {orgUnitName}, Lavozim: {positionName}, RejaSoni: {staffingPlan.PlannedHeadcount}");
                return RedirectToAction("Index");
            }

            PopulateViewBags(viewModel, null);
            return View(viewModel);
        }

        // POST so'rovi: HR/StaffingPlan/o'chirish/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var staffingPlan = await db.HR_STAFFING_PLAN.FindAsync(id);
            if (staffingPlan == null)
            {
                return HttpNotFound();
            }

            staffingPlan.IsDeleted = true;
            db.Entry(staffingPlan).State = System.Data.Entity.EntityState.Modified;
            await db.SaveChangesAsync();

            var orgUnitName = HrLookups.NameOf(HrLookups.OrgUnits, staffingPlan.OrgUnitID) ?? "-";
            var positionName = HrLookups.NameOf(HrLookups.Positions, staffingPlan.PositionID) ?? "-";
            logger.Log("HrStaffingPlan", "Delete", $"Bo'lim: {orgUnitName}, Lavozim: {positionName}");
            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/StaffingPlan/GenerateFromEmployees
        public async Task<ActionResult> GenerateFromEmployees()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Bitta so'rov: faol xodimlar (bo'lim, lavozim) bo'yicha guruhlanadi, faol reja yo'q juftliklar.
            // Avval har xodimga 1 so'rov + 2 lazy-load, va har xodimga alohida (PlannedHeadcount = 1) taklif — saqlansa N ta dublikat reja (HRM-B13/P14)
            var groups = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => e.IsActive && e.IsDeleted != true && e.OrgUnitID.HasValue && e.PositionID.HasValue)
                .Where(e => !db.HR_STAFFING_PLAN.Any(sp => sp.OrgUnitID == e.OrgUnitID.Value &&
                                                          sp.PositionID == e.PositionID.Value &&
                                                          sp.IsActive && sp.IsDeleted != true))
                .GroupBy(e => new { OrgUnitID = e.OrgUnitID.Value, PositionID = e.PositionID.Value })
                .Select(g => new
                {
                    g.Key.OrgUnitID,
                    g.Key.PositionID,
                    OrgUnitName = g.Select(e => e.HR_ORG_UNITS.Name).FirstOrDefault(),
                    PositionName = g.Select(e => e.HR_POSITIONS.Name).FirstOrDefault(),
                    SalaryGradeID = g.Where(e => e.SalaryGradeID != null).Select(e => e.SalaryGradeID).FirstOrDefault(),
                    EarliestEmployment = g.Min(e => e.EmploymentDate),
                    Count = g.Count()
                })
                .ToListAsync();

            var proposals = groups
                .OrderBy(g => g.OrgUnitName).ThenBy(g => g.PositionName)
                .Select(g => new HrStaffingPlanViewModel
                {
                    OrgUnitID = g.OrgUnitID,
                    OrgUnitName = g.OrgUnitName,
                    PositionID = g.PositionID,
                    PositionName = g.PositionName,
                    SalaryGradeID = g.SalaryGradeID,
                    PlannedHeadcount = g.Count,
                    IsFixedSalary = true,
                    EffectiveFrom = g.EarliestEmployment ?? DateTime.Now,
                    IsActive = true
                })
                .ToList();

            ViewBag.Proposals = proposals;
            logger.Log("HrStaffingPlan", "GenerateFromEmployees");
            return View(proposals);
        }

        // POST so'rovi: HR/StaffingPlan/CreateFromProposals
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateFromProposals(List<HrStaffingPlanViewModel> proposals)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (proposals != null && proposals.Any())
            {
                var existingKeys = new HashSet<Tuple<int, int>>((await db.HR_STAFFING_PLAN.AsNoTracking()
                    .Where(sp => sp.IsActive && sp.IsDeleted != true)
                    .Select(sp => new { sp.OrgUnitID, sp.PositionID })
                    .ToListAsync()).Select(x => Tuple.Create(x.OrgUnitID, x.PositionID)));
                int created = 0;

                foreach (var proposal in proposals.Where(p => p.IsActive))
                {
                    if (!existingKeys.Add(Tuple.Create(proposal.OrgUnitID, proposal.PositionID)))
                    {
                        continue;
                    }

                    var staffingPlan = new HR_STAFFING_PLAN
                    {
                        OrgUnitID = proposal.OrgUnitID,
                        PositionID = proposal.PositionID,
                        SalaryGradeID = proposal.SalaryGradeID,
                        PlannedHeadcount = proposal.PlannedHeadcount,
                        IsPieceRate = proposal.IsPieceRate,
                        IsHourly = proposal.IsHourly,
                        IsFixedSalary = proposal.IsFixedSalary,
                        EffectiveFrom = proposal.EffectiveFrom,
                        EffectiveTo = proposal.EffectiveTo,
                        IsActive = true
                    };

                    db.HR_STAFFING_PLAN.Add(staffingPlan);
                    created++;
                }

                await db.SaveChangesAsync();
                logger.Log("HrStaffingPlan", "CreateFromProposals", $"YaratilganRejaSoni: {created}");
            }

            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/StaffingPlan/Import
        public ActionResult Import()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            return View();
        }

        // POST so'rovi: HR/StaffingPlan/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Import(HttpPostedFileBase file)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (file == null || file.ContentLength == 0)
            {
                TempData["Error"] = L.T("Hr_FileNotSelected");
                return View();
            }

            try
            {
                using (var workbook = new XLWorkbook(file.InputStream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rowCount = worksheet.LastRowNumber();

                    var importedData = new List<Dictionary<string, object>>();

                    // Skip header qator, start dan qator 2
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var rowData = new Dictionary<string, object>();
                        rowData["RowNumber"] = row;
                        rowData["TabNo"] = worksheet.Cell(row, 3).GetRawValue()?.ToString(); // ustun C - Таб.№
                        rowData["FullName"] = worksheet.Cell(row, 2).GetRawValue()?.ToString(); // ustun B - F.I.Sh
                        rowData["Position"] = worksheet.Cell(row, 4).GetRawValue()?.ToString(); // ustun D - Lavozim
                        rowData["BaseSalary"] = worksheet.Cell(row, 5).GetRawValue()?.ToString(); // ustun E - Начисление

                        if (!string.IsNullOrWhiteSpace(rowData["TabNo"]?.ToString()))
                        {
                            importedData.Add(rowData);
                        }
                    }

                    TempData["ImportedData"] = importedData;
                    logger.Log("HrStaffingPlan", "Import", $"ImportQatorSoni: {importedData.Count}");
                    return View("ImportPreview", importedData);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = L.T("Hr_ImportError", ex.Message);
                return View();
            }
        }

        // POST so'rovi: HR/StaffingPlan/ImportConfirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ImportConfirm()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var importedData = TempData["ImportedData"] as List<Dictionary<string, object>>;
            
            if (importedData == null || !importedData.Any())
            {
                TempData["Error"] = L.T("Hr_DataNotFound");
                return RedirectToAction("Import");
            }

            try
            {
                int successCount = 0;
                int errorCount = 0;

                var employeeByTabNo = (await db.HR_EMPLOYEES.AsNoTracking()
                    .Where(e => e.IsActive && e.IsDeleted != true && e.TabNo != null)
                    .Select(e => new { e.ID, e.TabNo, e.OrgUnitID })
                    .ToListAsync())
                    .GroupBy(e => e.TabNo)
                    .ToDictionary(g => g.Key, g => g.First());
                var positionByName = (await db.HR_POSITIONS.AsNoTracking()
                    .Where(p => p.IsActive && p.IsDeleted != true)
                    .Select(p => new { p.ID, p.Name })
                    .ToListAsync())
                    .GroupBy(p => p.Name)
                    .ToDictionary(g => g.Key, g => g.First().ID);
                var existingKeys = new HashSet<Tuple<int, int>>((await db.HR_STAFFING_PLAN.AsNoTracking()
                    .Where(sp => sp.IsActive && sp.IsDeleted != true)
                    .Select(sp => new { sp.OrgUnitID, sp.PositionID })
                    .ToListAsync()).Select(x => Tuple.Create(x.OrgUnitID, x.PositionID)));
                var newPlans = new Dictionary<Tuple<int, int>, HR_STAFFING_PLAN>();

                foreach (var row in importedData)
                {
                    var tabNo = row["TabNo"]?.ToString()?.Trim();
                    var positionName = row["Position"]?.ToString()?.Trim();

                    if (string.IsNullOrWhiteSpace(tabNo) || string.IsNullOrWhiteSpace(positionName))
                    {
                        errorCount++;
                        continue;
                    }

                    var employee = employeeByTabNo.TryGetValue(tabNo, out var emp) ? emp : null;
                    int positionId;
                    if (employee == null || !employee.OrgUnitID.HasValue || !positionByName.TryGetValue(positionName, out positionId))
                    {
                        errorCount++;
                        continue;
                    }

                    var key = Tuple.Create(employee.OrgUnitID.Value, positionId);
                    if (existingKeys.Contains(key))
                    {
                        continue;
                    }

                    HR_STAFFING_PLAN plan;
                    if (newPlans.TryGetValue(key, out plan))
                    {
                        // Bir (bo'lim, lavozim) uchun bitta reja, xodimlar soni bo'yicha
                        plan.PlannedHeadcount += 1;
                        continue;
                    }

                    plan = new HR_STAFFING_PLAN
                    {
                        OrgUnitID = key.Item1,
                        PositionID = key.Item2,
                        PlannedHeadcount = 1,
                        IsFixedSalary = true,
                        IsHourly = false,
                        IsPieceRate = false,
                        EffectiveFrom = DateTime.Now,
                        IsActive = true
                    };
                    newPlans[key] = plan;
                    db.HR_STAFFING_PLAN.Add(plan);
                    successCount++;
                }

                await db.SaveChangesAsync();
                logger.Log("HrStaffingPlan", "ImportConfirm", $"MuvaffaqiyatliSoni: {successCount}, XatolarSoni: {errorCount}");

                if (successCount > 0)
                {
                    TempData["Success"] = L.T("Hr_SpImportedCount", successCount);
                }
                if (errorCount > 0)
                {
                    TempData["Warning"] = L.T("Hr_RowsWithErrors", errorCount);
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrStaffingPlan.ImportConfirm] " + ex);
                TempData["Error"] = L.T("Hr_SaveError");
                return RedirectToAction("Import");
            }
        }

        private void PopulateViewBags(HrStaffingPlanViewModel viewModel, int? orgUnitId)
        {
            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, viewModel?.OrgUnitID ?? orgUnitId);
            ViewBag.PositionID = HrLookups.SelectList(HrLookups.Positions, viewModel?.PositionID);
            ViewBag.SalaryGradeID = HrLookups.SelectList(HrLookups.SalaryGrades, viewModel?.SalaryGradeID);
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

