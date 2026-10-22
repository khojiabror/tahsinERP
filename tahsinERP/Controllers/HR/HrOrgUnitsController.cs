using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;
using tahsinERP.Filters;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrOrgUnitsController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrOrgUnitsController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // Olish so'rovi: HR/OrgUnits
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var orgUnits = await db.HR_ORG_UNITS.AsNoTracking()
                .Where(o => o.IsActive && o.IsDeleted != true)
                .OrderBy(o => o.LevelNo)
                .ThenBy(o => o.SortOrder)
                .ToListAsync();

            var tree = BuildOrgTree(orgUnits, null, await LoadPlannedByOrgAsync(), await LoadActualByOrgAsync());

            logger.Log("HrOrgUnits", "Index");
            return View(tree);
        }

        // Olish so'rovi: HR/OrgUnits/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var orgUnit = await db.HR_ORG_UNITS.AsNoTracking()
                .Where(o => o.ID == id && o.IsDeleted != true)
                .FirstOrDefaultAsync();

            if (orgUnit == null)
            {
                return HttpNotFound();
            }

            // Olish staffing plans uchun this org unit
            var staffingPlans = await db.HR_STAFFING_PLAN.AsNoTracking()
                .Include(sp => sp.HR_POSITIONS)
                .Where(sp => sp.OrgUnitID == id && sp.IsActive && sp.IsDeleted != true)
                .ToListAsync();

            // Olish employees uchun this org unit
            var employees = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => e.OrgUnitID == id && e.IsActive && e.IsDeleted != true && e.DismissalDate == null)
                .Select(e => new { e.FullName, e.PositionID })
                .ToListAsync();

            // Yaratish staffing plan view models bilan employee assignments
            var staffingPlanViewModels = staffingPlans.Select(sp => {
                var assignedEmployees = employees.Where(e => e.PositionID == sp.PositionID).ToList();
                return new HrOrgUnitStaffingPlanViewModel
                {
                    PositionID = sp.PositionID,
                    PositionName = sp.HR_POSITIONS?.Name ?? "-",
                    PlannedHeadcount = (int)sp.PlannedHeadcount,
                    AssignedEmployees = assignedEmployees.Select(e => e.FullName).ToList(),
                    AssignedCount = assignedEmployees.Count
                };
            }).ToList();

            var viewModel = new HrOrgUnitViewModel
            {
                ID = orgUnit.ID,
                ParentID = orgUnit.ParentID,
                Code = orgUnit.Code,
                Name = orgUnit.Name,
                LevelNo = orgUnit.LevelNo,
                SortOrder = orgUnit.SortOrder,
                IsActive = orgUnit.IsActive,
                CreatedDT = orgUnit.CreatedDT,
                PlannedHeadcount = (int)(staffingPlans.Sum(sp => (decimal?)sp.PlannedHeadcount) ?? 0),
                ActualHeadcount = employees.Count,
                StaffingPlans = staffingPlanViewModels
            };

            if (orgUnit.ParentID.HasValue)
            {
                viewModel.ParentName = await db.HR_ORG_UNITS.AsNoTracking()
                    .Where(o => o.ID == orgUnit.ParentID.Value).Select(o => o.Name).FirstOrDefaultAsync();
            }

            logger.Log("HrOrgUnits", "Details", $"Bo'lim: {viewModel.Name}");
            
            // Qaytarish partial view uchun AJAX calls, full view uchun direct access
            if (Request.IsAjaxRequest())
            {
                return PartialView("_OrgUnitDetails", viewModel);
            }
            return View(viewModel);
        }

        // Olish so'rovi: HR/OrgUnits/yaratish
        public ActionResult Create(int? parentId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            PopulateParents(parentId, null);
            return View();
        }

        private void PopulateParents(int? selectedParentId, int? excludeId)
        {
            ViewBag.ParentID = new SelectList(
                HrLookups.Get(HrLookups.OrgUnits).Where(o => o.ID != excludeId),
                "ID", "Name", selectedParentId);
        }

        private async Task<string> ParentNameAsync(int? parentId)
        {
            if (!parentId.HasValue) return "-";
            return await db.HR_ORG_UNITS.AsNoTracking().Where(o => o.ID == parentId.Value).Select(o => o.Name).FirstOrDefaultAsync() ?? "-";
        }

        // POST so'rovi: HR/OrgUnits/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "ID,ParentID,Code,Name,LevelNo,SortOrder,IsActive")] HR_ORG_UNITS orgUnit)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Tekshirish no circular reference
                if (orgUnit.ParentID.HasValue && await HasCircularReference(orgUnit.ParentID.Value, null))
                {
                    ModelState.AddModelError("ParentID", L.T("Hr_OrgCycle"));
                    PopulateParents(orgUnit.ParentID, null);
                    return View(orgUnit);
                }

                // Calculate LevelNo
                if (orgUnit.ParentID.HasValue)
                {
                    var parent = await db.HR_ORG_UNITS.FindAsync(orgUnit.ParentID.Value);
                    orgUnit.LevelNo = parent.LevelNo + 1;
                }
                else
                {
                    orgUnit.LevelNo = 1;
                }

                // Calculate SortOrder
                var maxSort = await db.HR_ORG_UNITS
                    .Where(o => o.ParentID == orgUnit.ParentID)
                    .Select(o => (int?)o.SortOrder)
                    .DefaultIfEmpty(0)
                    .MaxAsync();
                orgUnit.SortOrder = (maxSort ?? 0) + 1;

                orgUnit.CreatedDT = DateTime.Now;
                orgUnit.IsActive = true;

                db.HR_ORG_UNITS.Add(orgUnit);
                await db.SaveChangesAsync();
                HrLookups.Invalidate(HrLookups.OrgUnits);

                logger.Log("HrOrgUnits", "Create", $"Bo'lim: {orgUnit.Name}, OtaBo'lim: {await ParentNameAsync(orgUnit.ParentID)}");
                return RedirectToAction("Index");
            }

            PopulateParents(orgUnit.ParentID, null);
            return View(orgUnit);
        }

        // Olish so'rovi: HR/OrgUnits/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var orgUnit = await db.HR_ORG_UNITS.FindAsync(id);
            if (orgUnit == null)
            {
                return HttpNotFound();
            }

            PopulateParents(orgUnit.ParentID, orgUnit.ID);

            return View(orgUnit);
        }

        // POST so'rovi: HR/OrgUnits/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "ID,ParentID,Code,Name,LevelNo,SortOrder,IsActive,CreatedDT")] HR_ORG_UNITS orgUnit)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Tekshirish no circular reference
                if (orgUnit.ParentID.HasValue && await HasCircularReference(orgUnit.ParentID.Value, orgUnit.ID))
                {
                    ModelState.AddModelError("ParentID", L.T("Hr_OrgCycle"));
                    PopulateParents(orgUnit.ParentID, orgUnit.ID);
                    return View(orgUnit);
                }

                // Recalculate LevelNo agar parent changed
                var existing = await db.HR_ORG_UNITS.FindAsync(orgUnit.ID);
                if (existing == null)
                {
                    return HttpNotFound();
                }

                if (existing.ParentID != orgUnit.ParentID)
                {
                    if (orgUnit.ParentID.HasValue)
                    {
                        var parent = await db.HR_ORG_UNITS.FindAsync(orgUnit.ParentID.Value);
                        orgUnit.LevelNo = parent.LevelNo + 1;
                    }
                    else
                    {
                        orgUnit.LevelNo = 1;
                    }

                    // Yangilash children levels
                    await UpdateChildrenLevels(orgUnit.ID, orgUnit.LevelNo);
                }

                // Yangilash existing entity bilan values dan orgUnit
                existing.Name = orgUnit.Name;
                existing.Code = orgUnit.Code; // avval forma yuborsa ham saqlanmasdi (HRM-B19)
                existing.SortOrder = orgUnit.SortOrder;
                existing.ParentID = orgUnit.ParentID;
                existing.LevelNo = orgUnit.LevelNo;
                existing.IsActive = orgUnit.IsActive;

                await db.SaveChangesAsync();
                HrLookups.Invalidate(HrLookups.OrgUnits);

                logger.Log("HrOrgUnits", "Edit", $"Bo'lim: {orgUnit.Name}, OtaBo'lim: {await ParentNameAsync(orgUnit.ParentID)}");
                return RedirectToAction("Index");
            }

            PopulateParents(orgUnit.ParentID, orgUnit.ID);
            return View(orgUnit);
        }

        // Olish so'rovi: HR/OrgUnits/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var orgUnit = await db.HR_ORG_UNITS.FindAsync(id);
            if (orgUnit == null)
            {
                return HttpNotFound();
            }

            // Tekshirish agar has children
            var hasChildren = await db.HR_ORG_UNITS.AnyAsync(o => o.ParentID == id && o.IsDeleted != true);
            if (hasChildren)
            {
                TempData["Error"] = L.T("Hr_OrgHasChildren");
                return RedirectToAction("Index");
            }

            // Tekshirish agar has employees
            var hasEmployees = await db.HR_EMPLOYEES.AnyAsync(e => e.OrgUnitID == id && e.IsActive && e.IsDeleted != true);
            if (hasEmployees)
            {
                TempData["Error"] = L.T("Hr_OrgHasEmployees");
                return RedirectToAction("Index");
            }

            return View(orgUnit);
        }

        // POST so'rovi: HR/OrgUnits/o'chirish/5
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var orgUnit = await db.HR_ORG_UNITS.FindAsync(id);
            if (orgUnit == null)
            {
                return HttpNotFound();
            }

            // Tekshirish agar has children
            var hasChildren = await db.HR_ORG_UNITS.AnyAsync(o => o.ParentID == id && o.IsDeleted != true);
            if (hasChildren)
            {
                TempData["Error"] = L.T("Hr_OrgHasChildren");
                return RedirectToAction("Index");
            }

            // Tekshirish agar has employees
            var hasEmployees = await db.HR_EMPLOYEES.AnyAsync(e => e.OrgUnitID == id && e.IsActive && e.IsDeleted != true);
            if (hasEmployees)
            {
                TempData["Error"] = L.T("Hr_OrgHasEmployees");
                return RedirectToAction("Index");
            }

            orgUnit.IsDeleted = true;
            db.Entry(orgUnit).State = System.Data.Entity.EntityState.Modified;
            await SoftDeleteReferenceService.AfterOrgUnitSoftDeletedAsync(db, id);
            await db.SaveChangesAsync();
            HrLookups.Invalidate(HrLookups.OrgUnits);

            logger.Log("HrOrgUnits", "Delete", $"Bo'lim: {orgUnit.Name}");
            return RedirectToAction("Index");
        }

        // AJAX so'rovi: yangilash parent (drag & drop)
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> UpdateParent(int id, int? newParentId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return Json(new { success = false, message = L.T("Hr_NoAccess") });
            }

            try
            {
                var orgUnit = await db.HR_ORG_UNITS.FindAsync(id);
                if (orgUnit == null)
                {
                    return Json(new { success = false, message = L.T("Hr_OrgNotFound") });
                }

                // Tekshirish no circular reference
                if (newParentId.HasValue && await HasCircularReference(newParentId.Value, id))
                {
                    return Json(new { success = false, message = L.T("Hr_OrgCycle") });
                }

                orgUnit.ParentID = newParentId;
                if (newParentId.HasValue)
                {
                    var parent = await db.HR_ORG_UNITS.FindAsync(newParentId.Value);
                    orgUnit.LevelNo = parent.LevelNo + 1;
                }
                else
                {
                    orgUnit.LevelNo = 1;
                }

                // Yangilash children levels
                await UpdateChildrenLevels(id, orgUnit.LevelNo);

                await db.SaveChangesAsync();
                HrLookups.Invalidate(HrLookups.OrgUnits);

                logger.Log("HrOrgUnits", "UpdateParent", $"Bo'lim: {orgUnit.Name ?? "-"}, YangiOtaBo'lim: {await ParentNameAsync(newParentId)}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrOrgUnits.UpdateParent] " + ex);
                return Json(new { success = false, message = L.T("Hr_SaveError") });
            }
        }

        // Olish so'rovi: HR/OrgUnits/OrgChart - Visual org chart
        public async Task<ActionResult> OrgChart()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var orgUnits = await db.HR_ORG_UNITS.AsNoTracking()
                .Where(o => o.IsActive && o.IsDeleted != true)
                .OrderBy(o => o.LevelNo)
                .ThenBy(o => o.SortOrder)
                .Select(o => new OrgChartUnitRow { ID = o.ID, ParentID = o.ParentID, Code = o.Code, Name = o.Name, LevelNo = o.LevelNo })
                .ToListAsync();

            // Xodimlar va shtat rejasi bitta so'rovdan, lavozim nomi bilan (avval har xodimga lazy-load, HRM-P10)
            var employees = (await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => e.IsActive && e.IsDeleted != true && e.OrgUnitID != null)
                .Select(e => new { OrgUnitID = e.OrgUnitID.Value, e.FullName, e.TabNo, e.PositionID, PositionName = e.HR_POSITIONS.Name })
                .ToListAsync())
                .ToLookup(e => e.OrgUnitID);
            var plans = (await db.HR_STAFFING_PLAN.AsNoTracking()
                .Where(sp => sp.IsActive && sp.IsDeleted != true)
                .Select(sp => new { sp.OrgUnitID, sp.PositionID, sp.PlannedHeadcount, PositionName = sp.HR_POSITIONS.Name })
                .ToListAsync())
                .ToLookup(sp => sp.OrgUnitID);

            var childrenByParent = orgUnits.ToLookup(o => o.ParentID);
            Func<OrgChartUnitRow, OrgChartNodeViewModel> build = null;
            build = unit =>
            {
                var unitEmployees = employees[unit.ID].ToList();
                var node = new OrgChartNodeViewModel
                {
                    ID = unit.ID,
                    Code = unit.Code,
                    Name = unit.Name,
                    LevelNo = unit.LevelNo,
                    Employees = unitEmployees.Select(e => new OrgChartEmployeeViewModel
                    {
                        FullName = e.FullName,
                        PositionName = e.PositionName ?? "",
                        TabNo = e.TabNo
                    }).ToList(),
                    Positions = plans[unit.ID].Select(sp => new OrgChartPositionViewModel
                    {
                        PositionName = sp.PositionName ?? "",
                        PlannedHeadcount = (int)sp.PlannedHeadcount,
                        ActualHeadcount = unitEmployees.Count(e => e.PositionID == sp.PositionID)
                    }).ToList()
                };
                foreach (var child in childrenByParent[unit.ID])
                {
                    node.Children.Add(build(child));
                }
                return node;
            };

            ViewBag.OrgChartData = childrenByParent[null].Select(build).ToList();
            logger.Log("HrOrgUnits", "OrgChart");
            return View();
        }

        private sealed class OrgChartUnitRow
        {
            public int ID { get; set; }
            public int? ParentID { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public int LevelNo { get; set; }
        }

        private Task<Dictionary<int, decimal>> LoadPlannedByOrgAsync()
        {
            return db.HR_STAFFING_PLAN.AsNoTracking()
                .Where(sp => sp.IsActive && sp.IsDeleted != true)
                .GroupBy(sp => sp.OrgUnitID)
                .Select(g => new { g.Key, Planned = g.Sum(sp => sp.PlannedHeadcount) })
                .ToDictionaryAsync(x => x.Key, x => x.Planned);
        }

        private Task<Dictionary<int, int>> LoadActualByOrgAsync()
        {
            return db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => e.IsActive && e.IsDeleted != true && e.OrgUnitID != null)
                .GroupBy(e => e.OrgUnitID.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
        }

        private static List<HrOrgUnitViewModel> BuildOrgTree(List<HR_ORG_UNITS> allUnits, int? parentId,
            Dictionary<int, decimal> plannedByOrg, Dictionary<int, int> actualByOrg)
        {
            var result = new List<HrOrgUnitViewModel>();

            var children = allUnits.Where(u => u.ParentID == parentId).ToList();
            foreach (var unit in children)
            {
                var viewModel = new HrOrgUnitViewModel
                {
                    ID = unit.ID,
                    ParentID = unit.ParentID,
                    Code = unit.Code,
                    Name = unit.Name,
                    LevelNo = unit.LevelNo,
                    SortOrder = unit.SortOrder,
                    IsActive = unit.IsActive,
                    CreatedDT = unit.CreatedDT
                };

                decimal planned;
                int actual;
                plannedByOrg.TryGetValue(unit.ID, out planned);
                actualByOrg.TryGetValue(unit.ID, out actual);
                viewModel.PlannedHeadcount = (int)planned;
                viewModel.ActualHeadcount = actual;

                viewModel.Children = BuildOrgTree(allUnits, unit.ID, plannedByOrg, actualByOrg);
                result.Add(viewModel);
            }

            return result;
        }

        private async Task<bool> HasCircularReference(int parentId, int? excludeId)
        {
            var visited = new HashSet<int>();
            if (excludeId.HasValue)
            {
                visited.Add(excludeId.Value);
            }

            int currentId = parentId;
            while (currentId > 0)
            {
                if (visited.Contains(currentId))
                {
                    return true; // Circular reference found
                }

                visited.Add(currentId);

                var parent = await db.HR_ORG_UNITS.FindAsync(currentId);
                if (parent == null || !parent.ParentID.HasValue)
                {
                    break;
                }

                currentId = parent.ParentID.Value;
            }

            return false;
        }

        private async Task UpdateChildrenLevels(int parentId, int parentLevel)
        {
            var children = await db.HR_ORG_UNITS
                .Where(o => o.ParentID == parentId)
                .ToListAsync();

            foreach (var child in children)
            {
                child.LevelNo = parentLevel + 1;
                await UpdateChildrenLevels(child.ID, child.LevelNo);
            }
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

