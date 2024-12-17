using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2010.Excel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class GTDController : Controller
    {
        public ActionResult Index()
        {
            using ( DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.GTDS
                    .Include(x => x.CURRENCy)
                    .Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                ViewBag.invoices = new SelectList(db.P_INVOICES.Where(x => x.IsDeleted == false).ToList(), "ID", "InvoiceNo");
            }
            return View();
        }
        [HttpPost]
        public ActionResult Create(GTDViewModel model, int[] invoiceID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        GTD gtd = new GTD()
                        {
                            GTD_No = model.GTDNo,
                            Sum = model.Sum,
                            CurrencyID = model.CurrencyID,
                            IsDeleted = false
                        };
                        db.GTDS.Add(gtd);
                        db.SaveChanges();

                        if (model.File != null && model.File.ContentLength > 0)
                        {
                            if (model.File.ContentLength < 5242880)
                            {
                                if (Path.GetExtension(model.File.FileName).ToLower() == ".pdf")
                                {
                                    GTD_DOCS gtdDoc = new GTD_DOCS();
                                    byte[] fileData = new byte[model.File.InputStream.Length];
                                    model.File.InputStream.Read(fileData, 0, fileData.Length);

                                    gtdDoc.GtdID = gtd.ID;
                                    gtdDoc.Doc = fileData;

                                    db.GTD_DOCS.Add(gtdDoc);
                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "GTD lar", $"{gtdDoc.GTD.GTD_No} - uchun fayl biriktirdi.");
                                }
                                else
                                {
                                    ModelState.AddModelError("", "Format noto'g'ri. Faqat .pdf fayllarni yuklash mumkin.");
                                }
                            }
                            else
                            {
                                ModelState.AddModelError("", "Fayl o'ta katta. 5MB dan kichik bo'lgan faylni yuklang.");
                                throw new RetryLimitExceededException();
                            }
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "GTD lar", $"{gtd.GTD_No} - GTD ni tizimga kiritdi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", ex.Message);
                    }
                }

                var roles = db.ROLES.ToList();
                ViewBag.invoices = new SelectList(db.P_INVOICES.Where(x => x.IsDeleted == false).ToList(), "ID", "InvoiceNo");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View(model);
            }
        }
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var gtd = db.GTDS
                    .Include(x => x.CURRENCy)
                    .Where(x => x.IsDeleted == false && x.ID == id).FirstOrDefault();

                if (gtd != null)
                    return View(gtd);
                else
                    return HttpNotFound();
            }
        }
        [HttpPost]
        public ActionResult Delete(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var gtdToDelete =  db.GTDS.Find(id);
                if (gtdToDelete != null)
                {
                    gtdToDelete.IsDeleted = true;
                    try
                    {
                         db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "GTD lar", $"{gtdToDelete.GTD_No} GTD ni o'chirdi");

                        return RedirectToAction("Index");
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Bunday invoice topilmadi.");
                }
            }
            return View();
        }
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var gtd = db.GTDS
                    .Include(x => x.CURRENCy)
                    .Where(x => x.IsDeleted == false && x.ID == id).FirstOrDefault();

                if (gtd != null)
                    return View(gtd);
                else
                    return HttpNotFound();
            }
        }
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var gtd = db.GTDS
                    .Include(x => x.CURRENCy)
                    .Where(x => x.IsDeleted == false && x.ID == id).FirstOrDefault();
                ViewBag.invoices = new SelectList(db.P_INVOICES.Where(x => x.IsDeleted == false).ToList(), "ID", "InvoiceNo");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                if (gtd != null)
                    return View(gtd);
                else
                    return HttpNotFound();
            }
        }
        [HttpPost]
        public ActionResult Edit(GTD model)
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var gtdToUpdate = db.GTDS.Find(model.ID);
                    if (gtdToUpdate != null)
                    {
                        gtdToUpdate.Sum = model.Sum;
                        gtdToUpdate.GTD_No = model.GTD_No;
                        gtdToUpdate.CurrencyID = model.CurrencyID;

                        db.SaveChanges();
                        LogHelper.LogToDatabase(User.Identity.Name, "GTD lar", $"{gtdToUpdate.GTD_No}  ni tahrirladi");
                        
                        return RedirectToAction("Index");
                    }
                }
                ViewBag.invoices = new SelectList(db.P_INVOICES.Where(x => x.IsDeleted == false).ToList(), "ID", "InvoiceNo");
            }

            return View(model); 
        }
    }
}