using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing.Charts;
using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls.WebParts;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class ProductPlanController : Controller
    {
        // GET: ProductPlan
        private List<string> missingParts = new List<string>();
        private string[] sources;
        private string productNo = "", amount = "", startDate = "", dueDate = "";
        public ActionResult Index(int? shopID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var shops = db.SHOPS.Where(s => s.IsDeleted == false).ToList();
                if (shopID.HasValue)
                {
                    ViewBag.shopIsNotSelected = false;
                    ViewBag.productList = db.Database.SqlQuery<GetProductListPerShop_Result>("EXEC GetProductListPerShop @ShopID", new SqlParameter("@ShopID", shopID)).ToList();
                    ViewBag.ShopList = new SelectList(shops, "ID", "ShopName", shopID);
                }
                else
                {
                    ViewBag.shopIsNotSelected = true;
                    ViewBag.productList = db.PRODUCTPLANS.Include(pp => pp.PRODUCT).Where(x => x.IsDeleted == false).ToList();
                    ViewBag.ShopList = new SelectList(shops, "ID", "ShopName");
                }
                return View();
            }
        }
        public ActionResult Details(int? id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {

                if (id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                PRODUCTPLAN productPlan = db.PRODUCTPLANS
                                           .Include(pp => pp.PRODUCT)
                                           .FirstOrDefault(pp => pp.ID == id);

                if (productPlan == null)
                {
                    return HttpNotFound();
                }

                ViewBag.dailyPlan = db.PRODUCTPLANS_DAILY.Where(p => p.PlanID == productPlan.ID).ToList();

                return View(productPlan);

            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.ProductList = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");
                return View();
            }
        }
        [HttpPost]
        public ActionResult Create(ProductPlanVM productPlan)
        {
            int dayCount = 0;
            double dayPlanAmount;
            DateTime startDate;
            if (!CheckForPlanExistencePerProduct(productPlan))
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        if (ModelState.IsValid)
                        {
                            PRODUCTPLAN plan = new PRODUCTPLAN();
                            plan.ProductID = productPlan.ProductID;
                            plan.Amount = productPlan.Amount;
                            plan.StartDate = productPlan.StartDate;
                            plan.DueDate = productPlan.DueDate;
                            plan.IsDeleted = false;

                            db.PRODUCTPLANS.Add(plan);
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"{productPlan.ID} ID ga ega ProductPlanni yaratdi");

                            dayCount = productPlan.DueDate.Subtract(productPlan.StartDate).Days;
                            dayPlanAmount = Math.Ceiling(plan.Amount / dayCount);
                            startDate = productPlan.StartDate;
                            for (int i = 0; i < dayCount; i++)
                            {
                                PRODUCTPLANS_DAILY dailyPlan = new PRODUCTPLANS_DAILY();
                                if (!productPlan.IsTwoShiftPlan)
                                    dailyPlan.DayShift = dayPlanAmount;
                                else
                                {
                                    dailyPlan.DayShift = Math.Ceiling(dayPlanAmount / 2);
                                    dailyPlan.NightShift = dayPlanAmount - dailyPlan.DayShift;
                                }
                                dailyPlan.PlanID = plan.ID;
                                dailyPlan.Day = startDate;
                                startDate = startDate.AddDays(1);
                                db.PRODUCTPLANS_DAILY.Add(dailyPlan);

                                LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"{dailyPlan.ID} ID ga ega ProductPlanDailyni yaratdi");
                            }

                            db.SaveChanges();
                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Error: " + ex.Message);
                    }
                    ViewBag.ProductList = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "Name", productPlan.ProductID);

                    return View(productPlan);
                }
            }
            else
            {
                ModelState.AddModelError("", "Ushbu maxsulotga kiritlgan muddatlarda reja berilgan, iltimos tekshirib qaytadan urinib ko'ring!");
                return RedirectToAction("Create", productPlan);
            }
        }
        private bool CheckForPlanExistencePerProduct(ProductPlanVM productPlan)
        {
            DateTime startDate = productPlan.StartDate.Date;
            DateTime endDate = productPlan.DueDate.Date;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PRODUCTPLAN plan = db.PRODUCTPLANS.Where(p => ((p.StartDate <= productPlan.StartDate) || (p.StartDate <= productPlan.DueDate))
                                                                                                     && p.ProductID == productPlan.ProductID
                                                                                                     && p.IsDeleted == false).FirstOrDefault();
                if (plan != null)
                    return true;
                else
                    return false;
            }
        }
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PRODUCTPLAN productPlan = db.PRODUCTPLANS.Find(id);
                if (productPlan == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ProductList = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "Name", productPlan.ProductID);
                return View(productPlan);
            }
        }
        [HttpPost]
        public ActionResult Edit([Bind(Include = "ID,ProductID,PlannedQty,Label,IssueDate,DueDate,IsDeleted")] PRODUCTPLAN productPlan)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        var existingProductPlan = db.PRODUCTPLANS.Find(productPlan.ID);
                        if (existingProductPlan != null)
                        {
                            existingProductPlan.ProductID = productPlan.ProductID;
                            existingProductPlan.Amount = productPlan.Amount;
                            existingProductPlan.StartDate = productPlan.StartDate;
                            existingProductPlan.DueDate = productPlan.DueDate;
                            existingProductPlan.IsDeleted = false;

                            db.Entry(existingProductPlan).State = EntityState.Modified;
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"{productPlan.ID} ID ga ega ProductPlanni tahrirladi");
                        }

                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Tasdiqlanmadi: " + ex.Message);
                }
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.ProductList = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "Name", productPlan.ProductID);
            }
            return View(productPlan);
        }
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PRODUCTPLAN productPlan = db.PRODUCTPLANS.Find(id);
                if (productPlan == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ProductList = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "Name", productPlan.ProductID);
                return View(productPlan);
            }
        }
        [HttpPost]
        public ActionResult Delete(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    PRODUCTPLAN productPlan = db.PRODUCTPLANS.Find(id);
                    if (productPlan == null)
                    {
                        return HttpNotFound();
                    }

                    productPlan.IsDeleted = true;
                    db.Entry(productPlan).State = EntityState.Modified;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"{productPlan.ID} ID ga ega ProductPlanni o'chirdi");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    // Log the exception
                    ModelState.AddModelError("", ex.Message);
                    return View();
                }
            }
        }
        public async Task<ActionResult> Download()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SAMPLE_FILES contract = await db.SAMPLE_FILES.Where(s => s.FileName.CompareTo("productplan.xlsx") == 0).FirstOrDefaultAsync();
                if (contract != null)
                    return File(contract.File, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

                return View();
            }
        }
        public ActionResult UploadWithExcel()
        {
            ViewBag.IsFileUploaded = false;
            return View();
        }
        [HttpPost]
        public ActionResult UploadWithExcel(HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                if (Path.GetExtension(file.FileName).ToLower() == ".xlsx")
                {
                    try
                    {
                        var dataTable = new System.Data.DataTable();
                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                        using (var package = new ExcelPackage(file.InputStream))
                        {
                            var worksheet = package.Workbook.Worksheets[0];
                            var rowCount = worksheet.Dimension.Rows;
                            var colCount = 4; // ma'lumot uchun qo'shilgan columnlarni olmasligi uchun qo'lda yozildi

                            for (int col = 1; col <= colCount; col++)
                            {
                                dataTable.Columns.Add(worksheet.Cells[1, col].Text);
                            }
                            // Iterate over the rows and columns
                            for (int row = 2; row <= rowCount; row++)
                            {
                                var dataRow = dataTable.NewRow();
                                for (int col = 1; col <= colCount; col++)
                                {
                                    var cellValue = worksheet.Cells[row, col].Value;

                                    // Check if the cell contains a DateTime value
                                    if (cellValue is DateTime date)
                                    {
                                        // If it is a date, format it as "dd.MM.yyyy"
                                        dataRow[col - 1] = date.ToString("dd.MM.yyyy");
                                    }
                                    else
                                    {
                                        // Otherwise, store the value as is
                                        dataRow[col - 1] = cellValue?.ToString();
                                    }
                                }
                                dataTable.Rows.Add(dataRow);
                            }
                        }

                        if (CheckForExistenceOfProducts(dataTable))
                        {
                            ViewBag.DataTable = dataTable;
                            ViewBag.DataTableModel = JsonConvert.SerializeObject(dataTable);
                            ViewBag.IsFileUploaded = true;
                        }
                        else
                        {
                            var message = "";
                            foreach (var word in missingParts)
                            {
                                message += word + ", ";
                            }
                            ViewBag.Message = "Ushbu reja faylida kiritilgan maxsulotlar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Maxsulotlar bazasiga kiritib keyin qayta urining.";
                            return View("UploadWithExcel");
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
        private bool CheckForExistenceOfProducts(System.Data.DataTable dataTable)
        {
            bool flag = false;
            if (dataTable != null)
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        string prodPNo = row["Maxsulot raqami"].ToString();
                        if (db.PRODUCTS.Where(p => p.PNo.CompareTo(prodPNo) == 0).Any())
                            flag = true;
                        else
                        {
                            if (!missingParts.Contains(prodPNo))
                                missingParts.Add(prodPNo);
                        }
                    }
                    if (missingParts.Count > 0)
                        return false;
                    else
                        return flag;
                }
            else
                return flag;
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
            int dayCount = 0;
            double dayPlanAmount;
            DateTime startD;
            if (!string.IsNullOrEmpty(dataTableModel))
            {
                var tableModel = JsonConvert.DeserializeObject<System.Data.DataTable>(dataTableModel);

                try
                {
                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        foreach (DataRow row in tableModel.Rows)
                        {
                            productNo = row["Maxsulot raqami"].ToString();
                            amount = row["Reja"].ToString();

                            startDate = row["Boshlanish sanasi"].ToString();
                            dueDate = row["Tugash sanasi"].ToString();
                            DateTime d1 = DateTime.ParseExact(startDate, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
                            DateTime d2 = DateTime.ParseExact(dueDate, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);

                            PRODUCT product = db.PRODUCTS.Where(p => p.PNo.CompareTo(productNo) == 0 && p.IsDeleted == false).FirstOrDefault();
                            PRODUCTPLAN checkPlan = db.PRODUCTPLANS.Where(pp => pp.ProductID == product.ID &&
                                                    DbFunctions.TruncateTime(pp.StartDate) == DbFunctions.TruncateTime(d1) &&
                                                    DbFunctions.TruncateTime(pp.DueDate) == DbFunctions.TruncateTime(d2))
                                                    .FirstOrDefault();

                            if (checkPlan == null)
                            {
                                PRODUCTPLAN plan = new PRODUCTPLAN();
                                plan.ProductID = product.ID;
                                plan.Amount = Convert.ToDouble(amount);
                                plan.StartDate = d1;
                                plan.DueDate = d2;
                                plan.IsDeleted = false;

                                db.PRODUCTPLANS.Add(plan);
                                db.SaveChanges();

                                var userEmail1 = User.Identity.Name;
                                LogHelper.LogToDatabase(userEmail1, "Maxsulotga reja", $"{productNo} ga {amount} rejasini Maxsulotga reja berish oynasi orqali qo'shdi");

                                dayCount = (int)(plan.DueDate - plan.StartDate).TotalDays;
                                dayPlanAmount = Math.Ceiling(plan.Amount / dayCount);
                                startD = plan.StartDate;

                                for (int i = 0; i < dayCount; i++)
                                {
                                    PRODUCTPLANS_DAILY dailyPlan = new PRODUCTPLANS_DAILY();
                                    //if (!productPlan.IsTwoShiftPlan)
                                    dailyPlan.DayShift = dayPlanAmount;
                                    //else
                                    //{
                                    //    dailyPlan.DayShift = Math.Ceiling(dayPlanAmount / 2);
                                    //    dailyPlan.NightShift = dayPlanAmount - dailyPlan.DayShift;
                                    //}
                                    dailyPlan.PlanID = plan.ID;
                                    dailyPlan.Day = startD;
                                    startD = startD.AddDays(1);
                                    db.PRODUCTPLANS_DAILY.Add(dailyPlan);
                                }
                                db.SaveChanges();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.Message = $"Faylni yuklashda quyidagicha muammo tug'ildi: {ex.Message}";
                    ModelState.AddModelError("", ex.Message);
                }
            }

            return RedirectToAction("Index");
        }
    }
}