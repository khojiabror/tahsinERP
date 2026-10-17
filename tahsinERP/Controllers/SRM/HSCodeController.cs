using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    [Permission]
    public class HSCodeController : Controller
    {
        // Olish so'rovi: HSCode
        public async Task<ActionResult> Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = await db.HSCODES.ToListAsync();
                return View(list);
            }
        }

        public ActionResult Create()
        {
            return  View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HSCODE hsCode)
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        db.HSCODES.Add(hsCode);

                        await db.SaveChangesAsync();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "ТНВЭД kodlar", $"{hsCode.HSCODE1} - ТНВЭД ni biriktirdi");

                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex);
                }
            }
            return View(hsCode);
        }

        public async Task<ActionResult> Details(int? id)
        {
            if(id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                var hscode = await db.HSCODES.FindAsync(id);
                if (hscode == null)
                    return HttpNotFound();

                return View(hscode);
            }
        }

        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var hscode = await db.HSCODES.FindAsync(id);
                return View(hscode);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? id, FormCollection gfs)
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                var hscodeToDelete = await db.HSCODES.FindAsync(id);
                if(hscodeToDelete != null)
                {
                    try
                    {
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "ТНВЭД kodlar", $"{hscodeToDelete.HSCODE1} - HSCodeni o'chirdi");

                        return RedirectToAction("Index");
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError(string.Empty, "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Bunday ТНВЭД kod topilmadi.");
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
                var hscode = await db.HSCODES.FindAsync(id);
                return View(hscode);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HSCODE hsCode)
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                var hscodeToUpdate = await db.HSCODES.FindAsync(hsCode.ID);

                if (ModelState.IsValid)
                {
                    hscodeToUpdate.Description = hsCode.Description;
                    hscodeToUpdate.HSCODE1 = hsCode.HSCODE1;

                    try
                    {
                        await db.SaveChangesAsync();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "ТНВЭД kodlar", $"{hsCode.HSCODE1} - HSCodeni tahrirladi");

                        return RedirectToAction("Index");
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                    }
                }
                
                return View(hscodeToUpdate);
            }
        }
    }
}
