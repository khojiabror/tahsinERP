using DocumentFormat.OpenXml.Office2010.Excel;
using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls.WebParts;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class PurchasingItemController : Controller
    {
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.PURCHASING_ITEMS.Include(x => x.UNIT).ToList();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                return View();
            }
        }
        [HttpPost]
        public ActionResult Create(PURCHASING_ITEMS model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        model.IsDeleted = false;
                        db.PURCHASING_ITEMS.Add(model);
                        LogHelper.LogToDatabase(User.Identity.Name, "PurchasingItemController", $"{model.ItemName} - yaratdi");
                        db.SaveChanges();

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                        ModelState.AddModelError("", "Ma'lumotlarni saqlashni iloji bo'lmadi. Qayta urinib ko'ring, agar muammo yana qaytarilsa, tizim administratoriga murojaat qiling.");
                        return View(model);
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ModelState.AddModelError(ex.Message, "Ma'lumotlarni saqlashni iloji bo'lmadi. Qayta urinib ko'ring, agar muammo yana qaytarilsa, tizim administratoriga murojaat qiling.");
                    return View(model);
                }
            }
        }
        public ActionResult Edit(int? id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                var model = db.PURCHASING_ITEMS.Find(id);
                if (model == null)
                {
                    return HttpNotFound();
                }
                else
                {
                    ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                    return View(model);
                }
            }
        }
        [HttpPost]
        public ActionResult Edit(PURCHASING_ITEMS model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {

                if (TryUpdateModel(model, "", new string[] { "ItemCode", "ItemName", "Description", "UnitID" }))
                {
                    try
                    {
                        db.Entry(model).State = EntityState.Modified;
                        db.SaveChanges();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "PurchasingItemController", $"{model.ItemName} - buyumini tahrirladi");

                        // Update the many-to-many table Prod_Shop_Parts

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError(ex.Message, "Ma'lumotlarni saqlashni iloji bo'lmadi. Qayta urinib ko'ring, agar muammo yana qaytarilsa, tizim administratoriga murojaat qiling.");
                    }
                }
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

                return View(model);
            }
        }
        public ActionResult Delete(int? ID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ID == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                var model = db.PURCHASING_ITEMS.Include(x => x.UNIT).FirstOrDefault();
                if (model == null)
                {
                    ModelState.AddModelError("", "bu ID bilan ma'lumot topilmadi.");
                    return View();
                }

                return View(model);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int? ID, FormCollection gfc)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_ITEMS purchasingItemToDelete = db.PURCHASING_ITEMS.Find(ID);
                    if (purchasingItemToDelete != null)
                    {
                        try
                        {
                            db.PURCHASING_ITEMS.Remove(purchasingItemToDelete);
                            db.SaveChanges();

                            var userEmail = User.Identity.Name;
                            LogHelper.LogToDatabase(userEmail, "PurchasingItemController", $"{purchasingItemToDelete.ItemName} - Purchasingni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Bunday PurchasingItem topilmadi.");
                    }
                }
            }
            return View();
        }
        public ActionResult Details(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var model = db.PURCHASING_ITEMS.Include(x => x.UNIT).FirstOrDefault();
                if (model == null)
                    return HttpNotFound();

                return View(model);
            }
        }

        //upload with excel
        public ActionResult UploadWithExcel()
        {
            ViewBag.IsFileUploaded = false;
            return View();
        }
        public ActionResult Download()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SAMPLE_FILES zakup_detal = db.SAMPLE_FILES.Where(s => s.FileName.CompareTo("zakup_detallar.xlsx") == 0).FirstOrDefault();
                if (zakup_detal != null)
                    return File(zakup_detal.File, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                return View();
            }
        }
        [HttpPost]
        public ActionResult UploadWithExcel(HttpPostedFileBase file)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (file != null && file.ContentLength > 0)
                {
                    if (Path.GetExtension(file.FileName).ToLower() == ".xlsx")
                    {
                        try
                        {
                            var dataTable = new DataTable();
                            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                            using (var package = new ExcelPackage(file.InputStream))
                            {
                                var worksheet = package.Workbook.Worksheets[0];
                                var rowCount = worksheet.Dimension.Rows;
                                var colCount = worksheet.Dimension.Columns;

                                for (int col = 1; col <= colCount; col++)
                                {
                                    dataTable.Columns.Add(worksheet.Cells[1, col].Text);
                                }

                                for (int row = 2; row <= rowCount; row++)
                                {
                                    var dataRow = dataTable.NewRow();
                                    for (int col = 1; col <= colCount; col++)
                                    {
                                        var cellText = worksheet.Cells[row, col].Text;

                                        // Assuming 'PartName' is the name of the column that may contain en-dashes or em-dashes
                                        if (dataTable.Columns[col - 1].ColumnName == "Itemname")
                                        {
                                            // Replace en-dash and em-dash with a standard hyphen
                                            cellText = cellText.Replace('–', '-').Replace('—', '-');
                                        }
                                        cellText = new string(cellText.Where(c => !char.IsControl(c)).ToArray()).Trim();

                                        dataRow[col - 1] = cellText;
                                    }
                                    dataTable.Rows.Add(dataRow);
                                }
                            }

                            ViewBag.DataTable = dataTable;
                            ViewBag.DataTableModel = JsonConvert.SerializeObject(dataTable);
                            ViewBag.IsFileUploaded = true;

                            foreach (DataRow row in dataTable.Rows)
                            {
                                var itemCode = row["Itemcode"].ToString();

                                PURCHASING_ITEMS item = db.PURCHASING_ITEMS.Where(p => p.ItemCode.CompareTo(itemCode) == 0 && p.IsDeleted == false).FirstOrDefault();
                                if (item != null)
                                {
                                    ViewBag.ExistingRecordsCount = 1;
                                }

                            }
                        }
                        catch (Exception ex)
                        {
                            ViewBag.Message = $"Faylni yuklashda quyidagicha muammo tug'ildi: {ex.Message}";
                            return View("UploadWithExcel");
                        }
                    }
                    else
                    {
                        ViewBag.Message = "Format noto'g'ri. Faqat .xlsx fayllarni yuklash mumkin.";
                        return View("UploadWithExcel");
                    }
                }
                else
                {
                    ViewBag.Message = "Fayl bo'm-bo'sh yoki yuklanmadi!";
                    return View("UploadWithExcel");
                }
                return View("UploadWithExcel");
            }
        }
        public ActionResult ClearDataTable()
        {
            // Clear the DataTable and related ViewBag properties
            ViewBag.DataTable = null;
            ViewBag.DataTableModel = null;
            ViewBag.IsFileUploaded = false;
            ViewBag.Message = "Jadval ma'lumotlari o'chirib yuborildi.";
            // Return the UploadWithExcel view
            return View("UploadWithExcel");
        }
        [HttpPost]
        public ActionResult Save(string dataTableModel)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (!string.IsNullOrEmpty(dataTableModel))
                {
                    var tableModel = JsonConvert.DeserializeObject<DataTable>(dataTableModel);
                    try
                    {
                        foreach (DataRow row in tableModel.Rows)
                        {
                            var itemCode = row["Itemcode"].ToString();
                            var itemName = row["Itemname"].ToString();
                            var description = row["Description"].ToString();
                            string unitName = row["Unit"].ToString();

                            PURCHASING_ITEMS item = db.PURCHASING_ITEMS.Where(p => p.ItemCode.CompareTo(itemCode) == 0 && p.IsDeleted == false).FirstOrDefault();

                            if (item == null)
                            {
                                PURCHASING_ITEMS newItem = new PURCHASING_ITEMS();

                                newItem.ItemCode = itemCode;
                                newItem.IsDeleted = false;
                                newItem.ItemName = itemName;
                                UNIT unit = db.UNITS.Where(u => u.ShortName.CompareTo(unitName) == 0).FirstOrDefault();
                                if (unit != null)
                                    newItem.UnitID = unit.ID;
                                else
                                    newItem.UnitID = 1;

                                db.PURCHASING_ITEMS.Add(newItem);
                                db.SaveChanges();

                                var userEmail = User.Identity.Name;
                                LogHelper.LogToDatabase(userEmail, "PurchasingItemController", $"{newItem.ItemCode} - predemetni Excell orqali yaratdi");
                            }
                            else
                            {
                                ViewBag.Message = "Muammo!. Yuklangan faylda ayni vaqtda ma'lumotlar bazasida bor ma'lumot kiritilishga harakat bo'lmoqda.";
                            }
                        }
                    }
                    catch (JsonReaderException jex)
                    {
                        // Log the detailed exception message and stack trace
                        Console.WriteLine("JSON Reader Exception: " + jex.Message);
                        Console.WriteLine(jex.StackTrace);
                        // Handle the exception
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", ex.Message);
                    }
                }

                return RedirectToAction("Index");
            }
        }
    }
}