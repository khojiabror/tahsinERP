using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Filters;

namespace tahsinERP.Controllers.Settings
{
    [Permission]
    public class UnitController : Controller
    {
        // Olish so'rovi: Unit
        public async Task<ActionResult> Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = await db.UNITS
                    .Where(u => u.IsDeleted == false || u.IsDeleted == null)
                    .ToListAsync();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(UNIT unit)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        db.UNITS.Add(unit);

                        await db.SaveChangesAsync();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "O'lchov birliklar", $"{unit.ShortName} - birligini kiritdi tizimga");

                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex);
                }
            }
            return View(unit);
        }
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var unit = await db.UNITS
                    .Where(u => u.ID == id && (u.IsDeleted == false || u.IsDeleted == null))
                    .FirstOrDefaultAsync();
                if (unit == null)
                    return HttpNotFound();
                return View(unit);
            }
        }

        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var unit = await db.UNITS.FindAsync(id);
                return View(unit);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? id, FormCollection gfs)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var unitToDelete = await db.UNITS.FindAsync(id);
                if (unitToDelete != null)
                {
                    try
                    {
                        unitToDelete.IsDeleted = true;
                        db.Entry(unitToDelete).State = System.Data.Entity.EntityState.Modified;
                        await SoftDeleteReferenceService.AfterUnitSoftDeletedAsync(db, unitToDelete.ID);
                        await db.SaveChangesAsync();
                        LogHelper.LogToDatabase(User.Identity.Name, "O'lchov birliklar", $"{unitToDelete.UnitName} - o'lchov birligini o'chirdi");

                        return RedirectToAction("Index");
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError(string.Empty, "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Bunday o'lchov birligi topilmadi.");
                }
            }
            return View();
        }
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var unit = await db.UNITS.FindAsync(id);
                return View(unit);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(UNIT unit)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var unitToUpdate = await db.UNITS.FindAsync(unit.ID);

                if (ModelState.IsValid)
                {
                    unitToUpdate.UnitName = unit.UnitName;
                    unitToUpdate.ShortName = unit.ShortName;
                    unitToUpdate.Code = unit.Code;
                    if (TryUpdateModel(unitToUpdate, "", new string[] { "UnitName", "ShortName", "Code"}))
                    {
                        try
                        {
                            await db.SaveChangesAsync();

                            var userEmail = User.Identity.Name;
                            LogHelper.LogToDatabase(userEmail, "O'lchov birliklar", $"{unit.UnitName} - ni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                }

                return View(unitToUpdate);
            }
        }
    }
}
