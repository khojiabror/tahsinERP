using DocumentFormat.OpenXml.EMMA;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers
{
    public class FContractController : Controller
    {
        // Olish so'rovi: FContract
        public async Task<ActionResult> Index(int? forwarderID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Forwarder = new SelectList(await db.FORWARDERS.Where(fc => fc.IsDeleted == false).ToListAsync(), "ID", "ForwarderName");
                ViewBag.currencies = new SelectList(await db.CURRENCIES.ToListAsync(), "ID", "Shortname");
                return View();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(F_CONTRACTS contract)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            try
            {
                using(DBTHSNEntities db = new DBTHSNEntities()) 
                {
                    if (ModelState.IsValid)
                    {
                        contract.IsDeleted = false;
                        contract.CompanyID = 1;
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
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db1 = new DBTHSNEntities())
            {
                var contract = await db1.F_CONTRACTS
                    .Include(c => c.FORWARDER)
                    .Include (c => c.CURRENCy)
                    .FirstOrDefaultAsync(c => c.ID == ID);

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
        public async Task<ActionResult> Edit(F_CONTRACTS contract)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var contractToUpdate = await db.F_CONTRACTS.FindAsync(contract.ID);
                    if (contractToUpdate != null)
                    {
                        contractToUpdate.ContractNo = contract.ContractNo;
                        contractToUpdate.ForwarderID = contract.ForwarderID;
                        contractToUpdate.IssueDate = contract.IssueDate;
                        contractToUpdate.DueDate = contract.DueDate;
                        contractToUpdate.CurrencyID = contract.CurrencyID;
                        contractToUpdate.Amount = contract.Amount;
                        contractToUpdate.Description = contract.Description;
                        contractToUpdate.IsDeleted = false;

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = await db.F_CONTRACTS.FindAsync(Id);
                if (contract == null)
                {
                    return HttpNotFound();
                }

                await db.Entry(contract).Reference(i => i.FORWARDER).LoadAsync();
                await db.Entry(contract).Reference(i => i.CURRENCy).LoadAsync();
                return View(contract);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? ID, FormCollection gfs)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var contractToDelete = await db.F_CONTRACTS.FindAsync(ID);
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

            return View();
        }
    }
}
