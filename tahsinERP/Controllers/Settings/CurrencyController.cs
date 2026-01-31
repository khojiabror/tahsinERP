using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers.Settings
{
    public class CurrencyController : Controller
    {
        // GET: Currency
        public async Task<ActionResult> Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = await db.CURRENCIES
                    .Where(c => c.IsDeleted == false || c.IsDeleted == null)
                    .ToListAsync();
                return View(list);
            }
        }
        public async Task<ActionResult> Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(CURRENCy currency)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        CURRENCy currency2 = new CURRENCy();
                        currency2.Longname = currency.Longname;
                        currency2.Shortname = currency.Shortname;
                        currency2.Description = currency.Description;

                        db.CURRENCIES.Add(currency2);

                        await db.SaveChangesAsync();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "Valyutalar", $"{currency.Shortname} - valyuta birligini kiritdi tizimga");

                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex);
                }
            }
            return View(currency);
        }
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var currency = await db.CURRENCIES
                    .Where(c => c.ID == id && (c.IsDeleted == false || c.IsDeleted == null))
                    .FirstOrDefaultAsync();
                if (currency == null)
                    return HttpNotFound();
                return View(currency);
            }
        }
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var currency = await db.CURRENCIES.FindAsync(id);
                return View(currency);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? id, FormCollection gfs)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var currencyToDelete = await db.CURRENCIES.FindAsync(id);
                if (currencyToDelete != null)
                {
                    try
                    {
                        currencyToDelete.IsDeleted = true;
                        db.Entry(currencyToDelete).State = System.Data.Entity.EntityState.Modified;
                        await db.SaveChangesAsync();
                        LogHelper.LogToDatabase(User.Identity.Name, "Valyutalar", $"{currencyToDelete.Shortname} - o'lchov birligini o'chirdi");

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
                var currency = await db.CURRENCIES.FindAsync(id);
                return View(currency);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(CURRENCy currency)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var currencyToUpdate = await db.CURRENCIES.FindAsync(currency.ID);

                if (ModelState.IsValid)
                {
                    currencyToUpdate.Longname = currency.Longname;
                    currencyToUpdate.Shortname = currency.Shortname;
                    currencyToUpdate.Description = currency.Description;
                    if (TryUpdateModel(currencyToUpdate, "", new string[] { "Longname", "Shortname", "Description" }))
                    {
                        try
                        {
                            await db.SaveChangesAsync();

                            var userEmail = User.Identity.Name;
                            LogHelper.LogToDatabase(userEmail, "Valyutalar", $"{currency.Shortname} - valyutasini tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                }

                return View(currencyToUpdate);
            }
        }
    }
}