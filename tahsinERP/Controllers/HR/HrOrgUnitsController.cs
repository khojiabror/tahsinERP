using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.HR;
using tahsinERP.Filters;

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

            var orgUnits = await db.HR_ORG_UNITS
                .Where(o => o.IsActive && o.IsDeleted == false)
                .OrderBy(o => o.LevelNo)
                .ThenBy(o => o.SortOrder)
                .ToListAsync();

            var tree = BuildOrgTree(orgUnits, null);
            
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

            var orgUnit = await db.HR_ORG_UNITS
                .Include(o => o.HR_STAFFING_PLAN.Select(sp => sp.HR_POSITIONS))
                .Include(o => o.HR_STAFFING_PLAN.Select(sp => sp.HR_SALARY_GRADES))
                .Where(o => o.ID == id && o.IsDeleted == false)
                .FirstOrDefaultAsync();

            if (orgUnit == null)
            {
                return HttpNotFound();
            }

            // Olish staffing plans uchun this org unit
            var staffingPlans = await db.HR_STAFFING_PLAN
                .Include(sp => sp.HR_POSITIONS)
                .Where(sp => sp.OrgUnitID == id && sp.IsActive)
                .ToListAsync();

            // Olish employees uchun this org unit
            var employees = await db.HR_EMPLOYEES
                .Include(e => e.HR_POSITIONS)
                .Where(e => e.OrgUnitID == id && e.IsActive && e.DismissalDate == null)
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
                var parent = await db.HR_ORG_UNITS.FindAsync(orgUnit.ParentID.Value);
                viewModel.ParentName = parent?.Name;
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

                var parentName = "-";
                if (orgUnit.ParentID.HasValue)
                {
                    parentName = (await db.HR_ORG_UNITS.FindAsync(orgUnit.ParentID.Value))?.Name ?? "-";
                }
                logger.Log("HrOrgUnits", "Create", $"Bo'lim: {orgUnit.Name}, OtaBo'lim: {parentName}");
                return RedirectToAction("Index");
            }

            ViewBag.ParentID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnit.ParentID);
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

            ViewBag.ParentID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive && o.ID != id).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnit.ParentID);

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
                    ModelState.AddModelError("ParentID", "Циклическая ссылка не допускается");
                    ViewBag.ParentID = new SelectList(
                        await db.HR_ORG_UNITS.Where(o => o.IsActive && o.ID != orgUnit.ID).OrderBy(o => o.Name).ToListAsync(),
                        "ID", "Name", orgUnit.ParentID);
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
                existing.ParentID = orgUnit.ParentID;
                existing.LevelNo = orgUnit.LevelNo;
                existing.IsActive = orgUnit.IsActive;

                await db.SaveChangesAsync();

                var parentName = "-";
                if (orgUnit.ParentID.HasValue)
                {
                    parentName = (await db.HR_ORG_UNITS.FindAsync(orgUnit.ParentID.Value))?.Name ?? "-";
                }
                logger.Log("HrOrgUnits", "Edit", $"Bo'lim: {orgUnit.Name}, OtaBo'lim: {parentName}");
                return RedirectToAction("Index");
            }

            ViewBag.ParentID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive && o.ID != orgUnit.ID).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnit.ParentID);
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
            var hasChildren = await db.HR_ORG_UNITS.AnyAsync(o => o.ParentID == id);
            if (hasChildren)
            {
                TempData["Error"] = "Bo'limda quyi bo'limlar mavjud bo'lsa, uni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            // Tekshirish agar has employees
            var hasEmployees = await db.HR_EMPLOYEES.AnyAsync(e => e.OrgUnitID == id && e.IsActive);
            if (hasEmployees)
            {
                TempData["Error"] = "Bo'limda xodimlar mavjud bo'lsa, uni o'chirib bo'lmaydi";
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
            var hasChildren = await db.HR_ORG_UNITS.AnyAsync(o => o.ParentID == id);
            if (hasChildren)
            {
                TempData["Error"] = "Bo'limda quyi bo'limlar mavjud bo'lsa, uni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            // Tekshirish agar has employees
            var hasEmployees = await db.HR_EMPLOYEES.AnyAsync(e => e.OrgUnitID == id && e.IsActive);
            if (hasEmployees)
            {
                TempData["Error"] = "Bo'limda xodimlar mavjud bo'lsa, uni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            orgUnit.IsDeleted = true;
            db.Entry(orgUnit).State = System.Data.Entity.EntityState.Modified;
            await SoftDeleteReferenceService.AfterOrgUnitSoftDeletedAsync(db, id);
            await db.SaveChangesAsync();

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
                return Json(new { success = false, message = "Нет прав доступа" });
            }

            try
            {
                var orgUnit = await db.HR_ORG_UNITS.FindAsync(id);
                if (orgUnit == null)
                {
                    return Json(new { success = false, message = "Подразделение не найдено" });
                }

                // Tekshirish no circular reference
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

                // Yangilash children levels
                await UpdateChildrenLevels(id, orgUnit.LevelNo);

                await db.SaveChangesAsync();

                var unitName = orgUnit.Name ?? "-";
                var parentName = "-";
                if (newParentId.HasValue)
                {
                    parentName = (await db.HR_ORG_UNITS.FindAsync(newParentId.Value))?.Name ?? "-";
                }
                logger.Log("HrOrgUnits", "UpdateParent", $"Bo'lim: {unitName}, YangiOtaBo'lim: {parentName}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // AJAX so'rovi: Get tree ma'lumot
        [HttpGet]
        public async Task<JsonResult> GetTreeData()
        {
            var orgUnits = await db.HR_ORG_UNITS
                .Where(o => o.IsActive && o.IsDeleted == false)
                .OrderBy(o => o.LevelNo)
                .ThenBy(o => o.SortOrder)
                .ToListAsync();

            var tree = BuildOrgTree(orgUnits, null);
            return Json(tree, JsonRequestBehavior.AllowGet);
        }

        // Olish so'rovi: HR/OrgUnits/OrgChart - Visual org chart
        public async Task<ActionResult> OrgChart()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrOrgUnits"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var orgUnits = await db.HR_ORG_UNITS
                .Where(o => o.IsActive && o.IsDeleted == false)
                .Include(o => o.HR_EMPLOYEES)
                .Include(o => o.HR_STAFFING_PLAN.Select(sp => sp.HR_POSITIONS))
                .OrderBy(o => o.LevelNo)
                .ThenBy(o => o.SortOrder)
                .ToListAsync();

            // Build org chart ma'lumot bilan positions va employees
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

            // Qo'shish children
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

                // Olish headcount
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

