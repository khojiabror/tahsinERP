using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrPositionsController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrPositionsController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // Olish so'rovi: HR/Positions
        public async Task<ActionResult> Index(bool? isActive, bool? isManager)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPositions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_POSITIONS> query = db.HR_POSITIONS
                .Where(p => p.IsDeleted == false);

            if (isActive.HasValue)
            {
                query = query.Where(p => p.IsActive == isActive.Value);
            }
            else
            {
                query = query.Where(p => p.IsActive);
            }

            if (isManager.HasValue)
            {
                query = query.Where(p => p.IsManager == isManager.Value);
            }

            var positions = await query.OrderBy(p => p.Name).ToListAsync();

            ViewBag.IsActive = isActive;
            ViewBag.IsManager = isManager;

            logger.Log("HrPositions", "Index");
            return View(positions);
        }

        // Olish so'rovi: HR/Positions/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPositions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var position = await db.HR_POSITIONS
                .Where(p => p.ID == id && p.IsDeleted == false)
                .FirstOrDefaultAsync();
            if (position == null)
            {
                return HttpNotFound();
            }

            // Olish related ma'lumot
            ViewBag.EmployeeCount = await db.HR_EMPLOYEES.CountAsync(e => e.PositionID == id && e.IsActive);
            ViewBag.StaffingPlanCount = await db.HR_STAFFING_PLAN.CountAsync(sp => sp.PositionID == id && sp.IsActive);

            logger.Log("HrPositions", "Details", $"Lavozim: {position.Name}");
            return View(position);
        }

        // Olish so'rovi: HR/Positions/yaratish
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPositions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            return View();
        }

        // POST so'rovi: HR/Positions/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "Code,Name,IsManager,IsActive")] HR_POSITIONS position)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPositions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                position.CreatedDT = DateTime.Now;
                db.HR_POSITIONS.Add(position);
                await db.SaveChangesAsync();

                logger.Log("HrPositions", "Create", $"Lavozim: {position.Name}");
                return RedirectToAction("Index");
            }

            return View(position);
        }

        // Olish so'rovi: HR/Positions/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPositions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var position = await db.HR_POSITIONS.FindAsync(id);
            if (position == null)
            {
                return HttpNotFound();
            }

            return View(position);
        }

        // POST so'rovi: HR/Positions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "ID,Code,Name,IsManager,IsActive,CreatedDT")] HR_POSITIONS position)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPositions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                db.Entry(position).State = EntityState.Modified;
                await db.SaveChangesAsync();

                logger.Log("HrPositions", "Edit", $"Lavozim: {position.Name}");
                return RedirectToAction("Index");
            }

            return View(position);
        }

        // Olish so'rovi: HR/Positions/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPositions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var position = await db.HR_POSITIONS.FindAsync(id);
            if (position == null)
            {
                return HttpNotFound();
            }

            // Tekshirish agar position is used
            var employeeCount = await db.HR_EMPLOYEES.CountAsync(e => e.PositionID == id);
            var staffingPlanCount = await db.HR_STAFFING_PLAN.CountAsync(sp => sp.PositionID == id);

            if (employeeCount > 0 || staffingPlanCount > 0)
            {
                TempData["Error"] = $"Bu lavozim {employeeCount} ta xodim va {staffingPlanCount} ta shtat jadvalida ishlatilmoqda. O'chirish mumkin emas.";
                return RedirectToAction("Index");
            }

            return View(position);
        }

        // POST so'rovi: HR/Positions/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPositions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var position = await db.HR_POSITIONS.FindAsync(id);
            if (position != null)
            {
                // Tekshirish agar position is used
                var employeeCount = await db.HR_EMPLOYEES.CountAsync(e => e.PositionID == id);
                var staffingPlanCount = await db.HR_STAFFING_PLAN.CountAsync(sp => sp.PositionID == id);

                if (employeeCount > 0 || staffingPlanCount > 0)
                {
                    TempData["Error"] = $"Bu lavozim {employeeCount} ta xodim va {staffingPlanCount} ta shtat jadvalida ishlatilmoqda. O'chirish mumkin emas.";
                    return RedirectToAction("Index");
                }

                position.IsDeleted = true;
                db.Entry(position).State = System.Data.Entity.EntityState.Modified;
                await SoftDeleteReferenceService.AfterPositionSoftDeletedAsync(db, id);
                await db.SaveChangesAsync();

                logger.Log("HrPositions", "Delete", $"Lavozim: {position.Name}");
            }

            return RedirectToAction("Index");
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

