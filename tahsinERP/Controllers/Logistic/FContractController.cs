using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers
{
    /// <summary>Ruxsat: [Permission] (GET — ko'rish, POST/Create/Edit/Delete — o'zgartirish; AJAX endpointlar ham, LOG-S02).</summary>
    // LOG-S03: POST entity binding faqat ruxsat etilgan maydonlar bilan ([Bind(Include)]) — over-posting yo'q
    [Permission]
    public class FContractController : BaseController
    {
        // Olish so'rovi: FContract
        public async Task<ActionResult> Index(int? forwarderID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var q = db.F_CONTRACTS
                    .Include(fc => fc.FORWARDER)
                    .Include(fc => fc.CURRENCy)
                    .Where(fc => fc.IsDeleted == false);

                if (forwarderID.HasValue && forwarderID.Value > 0)
                {
                    q = q.Where(fc => fc.ForwarderID == forwarderID.Value);
                }

                var list = await q.ToListAsync();
                ViewBag.ForwarderID = forwarderID;
                return View(list);
            }
        }
        public async Task<ActionResult> Create()
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Forwarder = new SelectList(await db.FORWARDERS.Where(fc => fc.IsDeleted == false).ToListAsync(), "ID", "ForwarderName");
                ViewBag.currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname");
                return View();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "ContractNo,ForwarderID,IssueDate,DueDate,Amount,CurrencyID,Description")] F_CONTRACTS contract)
        {
            try
            {
                using(DBTHSNEntities db = new DBTHSNEntities()) 
                {
                    if (ModelState.IsValid)
                    {
                        contract.IsDeleted = false;
                        // LOG-S01: qattiq "1" o'rniga joriy bazadagi kompaniya (DB-per-tenant)
                        contract.CompanyID = await db.COMPANIES.OrderBy(c => c.ID).Select(c => c.ID).FirstOrDefaultAsync();
                        db.F_CONTRACTS.Add(contract);
                        await db.SaveChangesAsync();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "FContractController", "Create", $"{contract.ContractNo} - shartnoma yaratildi.");

                        return RedirectToAction("Index"); 
                    }
                    else
                    {
                        ModelState.AddModelError("", "Iltimos, majburiy maydonlarni to'ldiring.");
                        ViewBag.currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname");
                        ViewBag.Forwarder = new SelectList(await db.FORWARDERS.Where(fc => fc.IsDeleted == false).ToListAsync(), "ID", "ForwarderName");
                    }
                }
            }
            catch (Exception ex)
            {
                using(DBTHSNEntities db = new DBTHSNEntities())
                {
                    ModelState.AddModelError("", "Ma'lumotni saqlashda xatolik yuz berdi. Qayta urinib ko'ring.");
                    LogHelper.LogToDatabase(User.Identity.Name, "FContractController", "Create", $"Xatolik: {ex.Message}");
                    ViewBag.Forwarder = new SelectList(await db.FORWARDERS.Where(fc => fc.IsDeleted == false).ToListAsync(), "ID", "ForwarderName");
                    ViewBag.currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname");
                }
            }
            return View(contract);
        }
        public async Task<ActionResult> Details(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db1 = new DBTHSNEntities())
            {
                var contract = await db1.F_CONTRACTS
                                  .Include(p => p.FORWARDER)
                                  .Include(p => p.CURRENCy)
                                  .Include(p => p.F_CONTRACT_TARIFFS.Select(t => t.F_TRANSPORT_TYPES))
                                  .Include(p => p.F_CONTRACT_TARIFFS.Select(t => t.CURRENCy))
                                  .FirstOrDefaultAsync(p => p.ID == ID);

                if (contract == null)
                {
                    return HttpNotFound();
                }
                
                return View(contract);
            }
        }
        public async Task<ActionResult> Edit(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db1 = new DBTHSNEntities())
            {
                var contract = await db1.F_CONTRACTS
                    .Include(c => c.FORWARDER)
                    .Include (c => c.CURRENCy)
                    .FirstOrDefaultAsync(c => c.ID == ID && !c.IsDeleted);

                if (contract == null)
                {
                    return HttpNotFound();
                }
                ViewBag.Forwarder = new SelectList( await db1.FORWARDERS.Where(f => f.IsDeleted == false).ToListAsync(), "ID", "ForwarderName");
                ViewBag.currencies = new SelectList(await db1.CURRENCIES.ToListAsync(), "ID", "Shortname");
                return View(contract);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "ID,ContractNo,ForwarderID,IssueDate,DueDate,Amount,CurrencyID,Description")] F_CONTRACTS contract)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // LOG-B16: o'chirilgan shartnoma Edit orqali tiklanmaydi
                    var contractToUpdate = await db.F_CONTRACTS.FirstOrDefaultAsync(c => c.ID == contract.ID && !c.IsDeleted);
                    if (contractToUpdate != null)
                    {
                        contractToUpdate.ContractNo = contract.ContractNo;
                        contractToUpdate.ForwarderID = contract.ForwarderID;
                        contractToUpdate.IssueDate = contract.IssueDate;
                        contractToUpdate.DueDate = contract.DueDate;
                        contractToUpdate.CurrencyID = contract.CurrencyID;
                        contractToUpdate.Amount = contract.Amount;
                        contractToUpdate.Description = contract.Description;

                        try
                        {
                            await db.SaveChangesAsync();

                            var userEmail = User.Identity.Name;
                            LogHelper.LogToDatabase(userEmail, "FContractController", "Edit", $"{contract.ContractNo} - shartnoma tahrirlandi.");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError("", "Ma'lumotni saqlashda xatolik yuz berdi. Qayta urinib ko'ring.");
                            LogHelper.LogToDatabase(User.Identity.Name, "FContractController", "Edit", $"Xatolik: {ex.Message}");
                        }
                    }

                    ViewBag.Forwarder = new SelectList(await db.FORWARDERS.Where(f => f.IsDeleted == false).ToListAsync(), "ID", "ForwarderName", contract.ForwarderID);
                    ViewBag.currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname", contract.CurrencyID);
                    return View(contractToUpdate ?? contract);
                }
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Forwarder = new SelectList(await db.FORWARDERS.Where(f => f.IsDeleted == false).ToListAsync(), "ID", "ForwarderName", contract.ForwarderID);
                ViewBag.currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname", contract.CurrencyID);
            }
            ModelState.AddModelError("", "Iltimos, majburiy maydonlarni to'ldiring.");
            return View(contract);
        }
        public async Task<ActionResult> Delete(int? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = await LoadContractForDeleteAsync(db, Id.Value);
                if (contract == null)
                {
                    return HttpNotFound();
                }
                return View(contract);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<ActionResult> DeleteConfirmed(int? ID)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var contractToDelete = await db.F_CONTRACTS.FirstOrDefaultAsync(c => c.ID == ID && !c.IsDeleted);
                    if (contractToDelete != null)
                    {
                        contractToDelete.IsDeleted = true;
                        try
                        {
                            db.Entry(contractToDelete).State = EntityState.Modified;
                            await SoftDeleteReferenceService.AfterFContractSoftDeletedAsync(db, contractToDelete.ID);
                            await db.SaveChangesAsync();

                            var userEmail = User.Identity.Name;
                            LogHelper.LogToDatabase(userEmail, "FContractController", "Delete", $"{contractToDelete.ContractNo} - shartnoma o'chirildi.");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError("", "Ma'lumotni o'chirishda xatolik yuz berdi. Qayta urinib ko'ring.");
                            LogHelper.LogToDatabase(User.Identity.Name, "FContractController", "Delete", $"Xatolik: {ex.Message}");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Bunday shartnoma topilmadi.");
                    }
                }
            }
            else
            {
                ModelState.AddModelError("", "Ma'lumotlar noto'g'ri yuborildi. Qayta urinib ko'ring.");
            }

            // LOG-B09: view null model bilan ochilmasin
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var model = ID.HasValue ? await LoadContractForDeleteAsync(db, ID.Value) : null;
                if (model == null)
                {
                    return HttpNotFound();
                }
                return View(model);
            }
        }

        private static Task<F_CONTRACTS> LoadContractForDeleteAsync(DBTHSNEntities db, int id)
        {
            return db.F_CONTRACTS.AsNoTracking()
                .Include(c => c.FORWARDER)
                .Include(c => c.CURRENCy)
                .FirstOrDefaultAsync(c => c.ID == id && !c.IsDeleted);
        }
    }
}
