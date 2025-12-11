using System;
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
    public class HrEmployeesController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrEmployeesController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // GET: HR/Employees
        public async Task<ActionResult> Index(int? orgUnitId, int? positionId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_EMPLOYEES> query = db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .Include(e => e.HR_SALARY_GRADES);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.OrgUnitID == orgUnitId.Value);
            }

            if (positionId.HasValue)
            {
                query = query.Where(e => e.PositionID == positionId.Value);
            }

            if (status == "InActive")
            {
                query = query.Where(e => !e.IsActive);
            }
            else
            {
                query = query.Where(e => e.IsActive);
            }

            var employees = await query.OrderBy(e => e.FullName).ToListAsync();

            var viewModels = employees.Select(e => new HrEmployeeViewModel
            {
                ID = e.ID,
                TabNo = e.TabNo,
                FullName = e.FullName,
                OrgUnitID = e.OrgUnitID,
                OrgUnitName = e.HR_ORG_UNITS?.Name,
                PositionID = e.PositionID,
                PositionName = e.HR_POSITIONS?.Name,
                BaseSalary = e.BaseSalary,
                SalaryGradeID = e.SalaryGradeID,
                SalaryGradeName = e.HR_SALARY_GRADES?.Name,
                EmploymentDate = e.EmploymentDate,
                DismissalDate = e.DismissalDate,
                IsActive = e.IsActive
            }).ToList();

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", positionId);

            logger.Log("HrEmployees", "Index");
            return View(viewModels);
        }

        // GET: HR/Employees/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var employee = await db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .Include(e => e.HR_SALARY_GRADES)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrEmployeeViewModel
            {
                ID = employee.ID,
                TabNo = employee.TabNo,
                FullName = employee.FullName,
                OrgUnitID = employee.OrgUnitID,
                OrgUnitName = employee.HR_ORG_UNITS?.Name,
                PositionID = employee.PositionID,
                PositionName = employee.HR_POSITIONS?.Name,
                BaseSalary = employee.BaseSalary,
                SalaryGradeID = employee.SalaryGradeID,
                SalaryGradeName = employee.HR_SALARY_GRADES?.Name,
                EmploymentDate = employee.EmploymentDate,
                DismissalDate = employee.DismissalDate,
                IsActive = employee.IsActive
            };

            logger.Log("HrEmployees", "Details", $"ID: {id}");
            return View(viewModel);
        }

        // GET: HR/Employees/Create
        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name");

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name");

            ViewBag.SalaryGradeID = new SelectList(
                await db.HR_SALARY_GRADES.OrderBy(sg => sg.GradeCode).ToListAsync(),
                "ID", "Name");

            return View();
        }

        // POST: HR/Employees/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HrEmployeeViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Check if TabNo already exists
                var existing = await db.HR_EMPLOYEES
                    .FirstOrDefaultAsync(e => e.TabNo == viewModel.TabNo && e.IsActive);
                if (existing != null)
                {
                    ModelState.AddModelError("TabNo", "Табельный номер уже существует");
                    await PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                var employee = new HR_EMPLOYEES
                {
                    TabNo = viewModel.TabNo,
                    FullName = viewModel.FullName,
                    OrgUnitID = viewModel.OrgUnitID,
                    PositionID = viewModel.PositionID,
                    BaseSalary = viewModel.BaseSalary,
                    SalaryGradeID = viewModel.SalaryGradeID,
                    EmploymentDate = viewModel.EmploymentDate,
                    DismissalDate = viewModel.DismissalDate,
                    IsActive = viewModel.IsActive,
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                };

                db.HR_EMPLOYEES.Add(employee);
                await db.SaveChangesAsync();

                logger.Log("HrEmployees", "Create", $"ID: {employee.ID}, TabNo: {employee.TabNo}");
                return RedirectToAction("Index");
            }

            await PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // GET: HR/Employees/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var employee = await db.HR_EMPLOYEES.FindAsync(id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrEmployeeViewModel
            {
                ID = employee.ID,
                TabNo = employee.TabNo,
                FullName = employee.FullName,
                OrgUnitID = employee.OrgUnitID,
                PositionID = employee.PositionID,
                BaseSalary = employee.BaseSalary,
                SalaryGradeID = employee.SalaryGradeID,
                EmploymentDate = employee.EmploymentDate,
                DismissalDate = employee.DismissalDate,
                IsActive = employee.IsActive
            };

            await PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // POST: HR/Employees/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HrEmployeeViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Check if TabNo already exists for another employee
                var existing = await db.HR_EMPLOYEES
                    .FirstOrDefaultAsync(e => e.TabNo == viewModel.TabNo && e.ID != viewModel.ID && e.IsActive);
                if (existing != null)
                {
                    ModelState.AddModelError("TabNo", "Табельный номер уже существует");
                    await PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                var employee = await db.HR_EMPLOYEES.FindAsync(viewModel.ID);
                if (employee == null)
                {
                    return HttpNotFound();
                }

                employee.TabNo = viewModel.TabNo;
                employee.FullName = viewModel.FullName;
                employee.OrgUnitID = viewModel.OrgUnitID;
                employee.PositionID = viewModel.PositionID;
                employee.BaseSalary = viewModel.BaseSalary;
                employee.SalaryGradeID = viewModel.SalaryGradeID;
                employee.EmploymentDate = viewModel.EmploymentDate;
                employee.DismissalDate = viewModel.DismissalDate;
                employee.IsActive = viewModel.IsActive;
                employee.UpdatedDT = DateTime.Now;

                db.Entry(employee).State = EntityState.Modified;
                await db.SaveChangesAsync();

                logger.Log("HrEmployees", "Edit", $"ID: {viewModel.ID}");
                return RedirectToAction("Index");
            }

            await PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // POST: HR/Employees/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var employee = await db.HR_EMPLOYEES.FindAsync(id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            // Soft delete - set IsActive to false
            employee.IsActive = false;
            employee.DismissalDate = DateTime.Now;
            employee.UpdatedDT = DateTime.Now;

            db.Entry(employee).State = EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrEmployees", "Delete", $"ID: {id}");
            return RedirectToAction("Index");
        }

        private async Task PopulateViewBags(HrEmployeeViewModel viewModel)
        {
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", viewModel.OrgUnitID);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", viewModel.PositionID);

            ViewBag.SalaryGradeID = new SelectList(
                await db.HR_SALARY_GRADES.OrderBy(sg => sg.GradeCode).ToListAsync(),
                "ID", "Name", viewModel.SalaryGradeID);
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

