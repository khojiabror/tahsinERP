using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.HR;

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

        // GET: HR/OrgUnits
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var orgUnits = await db.HR_ORG_UNITS
                .Where(o => o.IsActive)
                .OrderBy(o => o.LevelNo)
                .ThenBy(o => o.SortOrder)
                .ToListAsync();

            var tree = BuildOrgTree(orgUnits, null);
            
            logger.Log("HrOrgUnits", "Index");
            return View(tree);
        }

        // GET: HR/OrgUnits/Details/5
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

            var orgUnit = await db.HR_ORG_UNITS
                .Include(o => o.HR_STAFFING_PLAN.Select(sp => sp.HR_POSITIONS))
                .Include(o => o.HR_STAFFING_PLAN.Select(sp => sp.HR_SALARY_GRADES))
                .FirstOrDefaultAsync(o => o.ID == id);

            if (orgUnit == null)
            {
                return HttpNotFound();
            }

            // Get staffing plans for this org unit
            var staffingPlans = await db.HR_STAFFING_PLAN
                .Include(sp => sp.HR_POSITIONS)
                .Where(sp => sp.OrgUnitID == id && sp.IsActive)
                .ToListAsync();

            // Get employees for this org unit
            var employees = await db.HR_EMPLOYEES
                .Include(e => e.HR_POSITIONS)
                .Where(e => e.OrgUnitID == id && e.IsActive && e.DismissalDate == null)
                .ToListAsync();

            // Create staffing plan view models with employee assignments
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
                var parent = await db.HR_ORG_UNITS.FindAsync(orgUnit.ParentID.Value);
                viewModel.ParentName = parent?.Name;
            }

            logger.Log("HrOrgUnits", "Details", $"ID: {id}");
            
            // Return partial view for AJAX calls, full view for direct access
            if (Request.IsAjaxRequest())
            {
                return PartialView("_OrgUnitDetails", viewModel);
            }
            return View(viewModel);
        }

        // GET: HR/OrgUnits/Create
        public async Task<ActionResult> Create(int? parentId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.ParentID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", parentId);

            return View();
        }

        // POST: HR/OrgUnits/Create
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
                // Validate no circular reference
                if (orgUnit.ParentID.HasValue && await HasCircularReference(orgUnit.ParentID.Value, null))
                {
                    ModelState.AddModelError("ParentID", "Циклическая ссылка не допускается");
                    ViewBag.ParentID = new SelectList(
                        await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                        "ID", "Name", orgUnit.ParentID);
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

                logger.Log("HrOrgUnits", "Create", $"ID: {orgUnit.ID}, Name: {orgUnit.Name}");
                return RedirectToAction("Index");
            }

            ViewBag.ParentID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnit.ParentID);
            return View(orgUnit);
        }

        // GET: HR/OrgUnits/Edit/5
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

            ViewBag.ParentID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive && o.ID != id).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnit.ParentID);

            return View(orgUnit);
        }

        // POST: HR/OrgUnits/Edit/5
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
                // Validate no circular reference
                if (orgUnit.ParentID.HasValue && await HasCircularReference(orgUnit.ParentID.Value, orgUnit.ID))
                {
                    ModelState.AddModelError("ParentID", "Циклическая ссылка не допускается");
                    ViewBag.ParentID = new SelectList(
                        await db.HR_ORG_UNITS.Where(o => o.IsActive && o.ID != orgUnit.ID).OrderBy(o => o.Name).ToListAsync(),
                        "ID", "Name", orgUnit.ParentID);
                    return View(orgUnit);
                }

                // Recalculate LevelNo if parent changed
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

                    // Update children levels
                    await UpdateChildrenLevels(orgUnit.ID, orgUnit.LevelNo);
                }

                // Update existing entity with values from orgUnit
                existing.Name = orgUnit.Name;
                existing.ParentID = orgUnit.ParentID;
                existing.LevelNo = orgUnit.LevelNo;
                existing.IsActive = orgUnit.IsActive;

                await db.SaveChangesAsync();

                logger.Log("HrOrgUnits", "Edit", $"ID: {orgUnit.ID}");
                return RedirectToAction("Index");
            }

            ViewBag.ParentID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive && o.ID != orgUnit.ID).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnit.ParentID);
            return View(orgUnit);
        }

        // GET: HR/OrgUnits/Delete/5
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

            // Check if has children
            var hasChildren = await db.HR_ORG_UNITS.AnyAsync(o => o.ParentID == id);
            if (hasChildren)
            {
                TempData["Error"] = "Bo'limda quyi bo'limlar mavjud bo'lsa, uni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            // Check if has employees
            var hasEmployees = await db.HR_EMPLOYEES.AnyAsync(e => e.OrgUnitID == id && e.IsActive);
            if (hasEmployees)
            {
                TempData["Error"] = "Bo'limda xodimlar mavjud bo'lsa, uni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            return View(orgUnit);
        }

        // POST: HR/OrgUnits/Delete/5
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

            // Check if has children
            var hasChildren = await db.HR_ORG_UNITS.AnyAsync(o => o.ParentID == id);
            if (hasChildren)
            {
                TempData["Error"] = "Bo'limda quyi bo'limlar mavjud bo'lsa, uni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            // Check if has employees
            var hasEmployees = await db.HR_EMPLOYEES.AnyAsync(e => e.OrgUnitID == id && e.IsActive);
            if (hasEmployees)
            {
                TempData["Error"] = "Bo'limda xodimlar mavjud bo'lsa, uni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            db.HR_ORG_UNITS.Remove(orgUnit);
            await db.SaveChangesAsync();

            logger.Log("HrOrgUnits", "Delete", $"ID: {id}");
            return RedirectToAction("Index");
        }

        // AJAX: Update parent (drag & drop)
        [HttpPost]
        public async Task<JsonResult> UpdateParent(int id, int? newParentId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return Json(new { success = false, message = "Нет прав доступа" });
            }

            try
            {
                var orgUnit = await db.HR_ORG_UNITS.FindAsync(id);
                if (orgUnit == null)
                {
                    return Json(new { success = false, message = "Подразделение не найдено" });
                }

                // Validate no circular reference
                if (newParentId.HasValue && await HasCircularReference(newParentId.Value, id))
                {
                    return Json(new { success = false, message = "Циклическая ссылка не допускается" });
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

                // Update children levels
                await UpdateChildrenLevels(id, orgUnit.LevelNo);

                await db.SaveChangesAsync();

                logger.Log("HrOrgUnits", "UpdateParent", $"ID: {id}, NewParentID: {newParentId}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // AJAX: Get tree data
        [HttpGet]
        public async Task<JsonResult> GetTreeData()
        {
            var orgUnits = await db.HR_ORG_UNITS
                .Where(o => o.IsActive)
                .OrderBy(o => o.LevelNo)
                .ThenBy(o => o.SortOrder)
                .ToListAsync();

            var tree = BuildOrgTree(orgUnits, null);
            return Json(tree, JsonRequestBehavior.AllowGet);
        }

        // GET: HR/OrgUnits/OrgChart - Visual org chart
        public async Task<ActionResult> OrgChart()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var orgUnits = await db.HR_ORG_UNITS
                .Include(o => o.HR_EMPLOYEES)
                .Include(o => o.HR_STAFFING_PLAN.Select(sp => sp.HR_POSITIONS))
                .Where(o => o.IsActive)
                .OrderBy(o => o.LevelNo)
                .ThenBy(o => o.SortOrder)
                .ToListAsync();

            // Build org chart data with positions and employees
            var rootUnits = orgUnits.Where(o => !o.ParentID.HasValue).ToList();
            var orgChartData = new List<OrgChartNodeViewModel>();

            foreach (var unit in rootUnits)
            {
                orgChartData.Add(BuildOrgChartNode(unit, orgUnits));
            }

            ViewBag.OrgChartData = orgChartData;
            logger.Log("HrOrgUnits", "OrgChart");
            return View();
        }

        private OrgChartNodeViewModel BuildOrgChartNode(HR_ORG_UNITS unit, List<HR_ORG_UNITS> allUnits)
        {
            var node = new OrgChartNodeViewModel
            {
                ID = unit.ID,
                Code = unit.Code,
                Name = unit.Name,
                LevelNo = unit.LevelNo,
                Employees = unit.HR_EMPLOYEES.Where(e => e.IsActive).Select(e => new OrgChartEmployeeViewModel
                {
                    FullName = e.FullName,
                    PositionName = e.HR_POSITIONS?.Name ?? "",
                    TabNo = e.TabNo
                }).ToList(),
                Positions = unit.HR_STAFFING_PLAN.Where(sp => sp.IsActive).Select(sp => new OrgChartPositionViewModel
                {
                    PositionName = sp.HR_POSITIONS?.Name ?? "",
                    PlannedHeadcount = (int)sp.PlannedHeadcount,
                    ActualHeadcount = unit.HR_EMPLOYEES.Count(e => e.IsActive && e.PositionID == sp.PositionID)
                }).ToList()
            };

            // Add children
            var children = allUnits.Where(o => o.ParentID == unit.ID).ToList();
            foreach (var child in children)
            {
                node.Children.Add(BuildOrgChartNode(child, allUnits));
            }

            return node;
        }

        private List<HrOrgUnitViewModel> BuildOrgTree(List<HR_ORG_UNITS> allUnits, int? parentId)
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

                // Get headcount
                viewModel.PlannedHeadcount = (int)(db.HR_STAFFING_PLAN
                    .Where(sp => sp.OrgUnitID == unit.ID && sp.IsActive)
                    .Sum(sp => (decimal?)sp.PlannedHeadcount) ?? 0);
                viewModel.ActualHeadcount = db.HR_EMPLOYEES
                    .Count(e => e.OrgUnitID == unit.ID && e.IsActive);

                viewModel.Children = BuildOrgTree(allUnits, unit.ID);
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

