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
        private string controllerName = "GTDController";
        public ActionResult Index()
        {
            using ( DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.GTDS.Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.invoices = new SelectList(db.P_INVOICES.Where(x => x.IsDeleted == false), "ID", "InvoiceNo");
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

                                    LogHelper.LogToDatabase(User.Identity.Name, "GTDController", $"{gtdDoc.GTD.GTD_No} - uchun gtdDOC yaratdi");
                                }
                                else
                                {
                                    ModelState.AddModelError("", "Format noto'g'ri. Faqat .pdf fayllarni yuklash mumkin.");
                                }
                            }
                            else
                            {
                                ModelState.AddModelError("", "The file is too large. Please upload a file smaller than 5MB.");
                                throw new RetryLimitExceededException();
                            }
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, controllerName, $"{gtd.GTD_No} - gtd yaratdi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", ex.Message);
                    }
                }

                var roles = db.ROLES.ToList();
                ViewBag.invoices = new SelectList(db.P_INVOICES.Where(x => x.IsDeleted == false), "ID", "InvoiceNo");

                return View(model);
            }
        }
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var gtd = db.GTDS.Where(x => x.IsDeleted == false && x.ID == id).FirstOrDefault();

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

                        LogHelper.LogToDatabase(User.Identity.Name, controllerName, $"{id} ID ga ega GTD ni o'chirdi");

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
                var gtd = db.GTDS.Where(x => x.IsDeleted == false && x.ID == id).FirstOrDefault();

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
                var gtd = db.GTDS.Where(x => x.IsDeleted == false && x.ID == id).FirstOrDefault();

                if (gtd != null)
                    return View(gtd);
                else
                    return HttpNotFound();
            }
        }
        [HttpPost]
        public ActionResult Edit(GTD model)
        {
            if (ModelState.IsValid)
            {
                using(DBTHSNEntities db = new DBTHSNEntities())
                {
                    var gtdToUpdate = db.GTDS.Find(model.ID);
                    if (gtdToUpdate != null)
                    {
                        gtdToUpdate.Sum = model.Sum;
                        gtdToUpdate.GTD_No = model.GTD_No;

                        db.SaveChanges();
                        LogHelper.LogToDatabase(User.Identity.Name, controllerName, $"{model.ID} ID ga ega GTD ni tahrirladi");
                        
                        return RedirectToAction("Index");
                    }
                }
            }
            return View(model); 
        }
    }
}