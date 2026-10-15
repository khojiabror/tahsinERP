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
    public class HrSalaryGradesController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrSalaryGradesController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // Olish so'rovi: HR/SalaryGrades
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrSalaryGrades"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var grades = await db.HR_SALARY_GRADES
                .Where(sg => sg.IsDeleted == false || sg.IsDeleted == null)
                .OrderBy(sg => sg.GradeCode)
                .ToListAsync();

            var viewModels = grades.Select(sg => new HrSalaryGradeViewModel
            {
                ID = sg.ID,
                GradeCode = sg.GradeCode,
                Name = sg.Name,
                MinSalary = sg.MinSalary,
                MaxSalary = sg.MaxSalary,
                CurrencyCode = sg.CurrencyCode,
                EffectiveFrom = sg.EffectiveFrom,
                EffectiveTo = sg.EffectiveTo
            }).ToList();

            logger.Log("HrSalaryGrades", "Index");
            return View(viewModels);
        }

        // Olish so'rovi: HR/SalaryGrades/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrSalaryGrades"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var grade = await db.HR_SALARY_GRADES
                .Where(sg => sg.ID == id && (sg.IsDeleted == false || sg.IsDeleted == null))
                .FirstOrDefaultAsync();
            if (grade == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrSalaryGradeViewModel
            {
                ID = grade.ID,
                GradeCode = grade.GradeCode,
                Name = grade.Name,
                MinSalary = grade.MinSalary,
                MaxSalary = grade.MaxSalary,
                CurrencyCode = grade.CurrencyCode,
                EffectiveFrom = grade.EffectiveFrom,
                EffectiveTo = grade.EffectiveTo
            };

            logger.Log("HrSalaryGrades", "Details", $"Daraja: {viewModel.Name}, Kod: {viewModel.GradeCode}");
            return View(viewModel);
        }

        // Olish so'rovi: HR/SalaryGrades/yaratish
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrSalaryGrades"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.CurrencyCode = new SelectList(
                db.CURRENCIES.OrderBy(c => c.Shortname).ToList(),
                "Shortname", "Shortname");

            return View();
        }

        // POST so'rovi: HR/SalaryGrades/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HrSalaryGradeViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrSalaryGrades"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Tekshirish Min <= Max
                if (viewModel.MinSalary > viewModel.MaxSalary)
                {
                    ModelState.AddModelError("MaxSalary", "Максимальная зарплата должна быть больше или равна минимальной");
                    ViewBag.CurrencyCode = new SelectList(
                        db.CURRENCIES.OrderBy(c => c.Shortname).ToList(),
                        "Shortname", "Shortname", viewModel.CurrencyCode);
                    return View(viewModel);
                }

                // Tekshirish uchun overlapping date ranges
                var overlapping = await db.HR_SALARY_GRADES
                    .Where(sg => sg.GradeCode == viewModel.GradeCode &&
                        (sg.IsDeleted == false || sg.IsDeleted == null) &&
                        ((sg.EffectiveFrom <= viewModel.EffectiveFrom && (!sg.EffectiveTo.HasValue || sg.EffectiveTo >= viewModel.EffectiveFrom)) ||
                         (viewModel.EffectiveFrom <= sg.EffectiveFrom && (!viewModel.EffectiveTo.HasValue || viewModel.EffectiveTo >= sg.EffectiveFrom))))
                    .FirstOrDefaultAsync();

                if (overlapping != null)
                {
                    ModelState.AddModelError("EffectiveFrom", "Пересекающиеся периоды действия для этого кода тарифной сетки не допускаются");
                    ViewBag.CurrencyCode = new SelectList(
                        db.CURRENCIES.OrderBy(c => c.Shortname).ToList(),
                        "Shortname", "Shortname", viewModel.CurrencyCode);
                    return View(viewModel);
                }

                var grade = new HR_SALARY_GRADES
                {
                    GradeCode = viewModel.GradeCode,
                    Name = viewModel.Name,
                    MinSalary = viewModel.MinSalary,
                    MaxSalary = viewModel.MaxSalary,
                    CurrencyCode = viewModel.CurrencyCode,
                    EffectiveFrom = viewModel.EffectiveFrom,
                    EffectiveTo = viewModel.EffectiveTo,
                    IsDeleted = false
                };

                db.HR_SALARY_GRADES.Add(grade);
                await db.SaveChangesAsync();

                logger.Log("HrSalaryGrades", "Create", $"Daraja: {grade.Name}, Kod: {grade.GradeCode}");
                return RedirectToAction("Index");
            }

            ViewBag.CurrencyCode = new SelectList(
                db.CURRENCIES.OrderBy(c => c.Shortname).ToList(),
                "Shortname", "Shortname", viewModel.CurrencyCode);
            return View(viewModel);
        }

        // Olish so'rovi: HR/SalaryGrades/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrSalaryGrades"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var grade = await db.HR_SALARY_GRADES.FindAsync(id);
            if (grade == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrSalaryGradeViewModel
            {
                ID = grade.ID,
                GradeCode = grade.GradeCode,
                Name = grade.Name,
                MinSalary = grade.MinSalary,
                MaxSalary = grade.MaxSalary,
                CurrencyCode = grade.CurrencyCode,
                EffectiveFrom = grade.EffectiveFrom,
                EffectiveTo = grade.EffectiveTo
            };

            ViewBag.CurrencyCode = new SelectList(
                db.CURRENCIES.OrderBy(c => c.Shortname).ToList(),
                "Shortname", "Shortname", viewModel.CurrencyCode);
            return View(viewModel);
        }

        // POST so'rovi: HR/SalaryGrades/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HrSalaryGradeViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrSalaryGrades"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Tekshirish Min <= Max
                if (viewModel.MinSalary > viewModel.MaxSalary)
                {
                    ModelState.AddModelError("MaxSalary", "Максимальная зарплата должна быть больше или равна минимальной");
                    ViewBag.CurrencyCode = new SelectList(
                        db.CURRENCIES.OrderBy(c => c.Shortname).ToList(),
                        "Shortname", "Shortname", viewModel.CurrencyCode);
                    return View(viewModel);
                }

                // Tekshirish uchun overlapping date ranges (excluding current record)
                var overlapping = await db.HR_SALARY_GRADES
                    .Where(sg => sg.ID != viewModel.ID &&
                        sg.GradeCode == viewModel.GradeCode &&
                        (sg.IsDeleted == false || sg.IsDeleted == null) &&
                        ((sg.EffectiveFrom <= viewModel.EffectiveFrom && (!sg.EffectiveTo.HasValue || sg.EffectiveTo >= viewModel.EffectiveFrom)) ||
                         (viewModel.EffectiveFrom <= sg.EffectiveFrom && (!viewModel.EffectiveTo.HasValue || viewModel.EffectiveTo >= sg.EffectiveFrom))))
                    .FirstOrDefaultAsync();

                if (overlapping != null)
                {
                    ModelState.AddModelError("EffectiveFrom", "Пересекающиеся периоды действия для этого кода тарифной сетки не допускаются");
                    ViewBag.CurrencyCode = new SelectList(
                        db.CURRENCIES.OrderBy(c => c.Shortname).ToList(),
                        "Shortname", "Shortname", viewModel.CurrencyCode);
                    return View(viewModel);
                }

                var grade = await db.HR_SALARY_GRADES.FindAsync(viewModel.ID);
                if (grade == null)
                {
                    return HttpNotFound();
                }

                grade.GradeCode = viewModel.GradeCode;
                grade.Name = viewModel.Name;
                grade.MinSalary = viewModel.MinSalary;
                grade.MaxSalary = viewModel.MaxSalary;
                grade.CurrencyCode = viewModel.CurrencyCode;
                grade.EffectiveFrom = viewModel.EffectiveFrom;
                grade.EffectiveTo = viewModel.EffectiveTo;

                db.Entry(grade).State = EntityState.Modified;
                await db.SaveChangesAsync();

                logger.Log("HrSalaryGrades", "Edit", $"Daraja: {viewModel.Name}, Kod: {viewModel.GradeCode}");
                return RedirectToAction("Index");
            }

            ViewBag.CurrencyCode = new SelectList(
                db.CURRENCIES.OrderBy(c => c.Shortname).ToList(),
                "Shortname", "Shortname", viewModel.CurrencyCode);
            return View(viewModel);
        }

        // POST so'rovi: HR/SalaryGrades/o'chirish/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrSalaryGrades"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var grade = await db.HR_SALARY_GRADES.FindAsync(id);
            if (grade == null)
            {
                return HttpNotFound();
            }

            // Tekshirish agar used in staffing plan
            var usedInStaffing = await db.HR_STAFFING_PLAN.AnyAsync(sp => sp.SalaryGradeID == id);
            if (usedInStaffing)
            {
                TempData["Error"] = "Нельзя удалить тарифную сетку, которая используется в штатном расписании";
                return RedirectToAction("Index");
            }

            grade.IsDeleted = true;
            db.Entry(grade).State = System.Data.Entity.EntityState.Modified;
            await SoftDeleteReferenceService.AfterSalaryGradeSoftDeletedAsync(db, id);
            await db.SaveChangesAsync();

            logger.Log("HrSalaryGrades", "Delete", $"Daraja: {grade.Name}, Kod: {grade.GradeCode}");
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

