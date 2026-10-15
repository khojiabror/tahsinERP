using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Irony.Parsing;
using Newtonsoft.Json;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers
{
    public class ProductStockController : Controller
    {
        private Warehouse warehouse = null;
        private List<string> missingParts = new List<string>();
        // Olish so'rovi: PartStock
        public async Task<ActionResult> Index(int? whid)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (RoleHelper.GetUserRoles(User.Identity.Name)[0].CompareTo("MRP") == 0)
                {
                    this.warehouse = GetWarehouseOfMRP(User.Identity.Name);
                    warehouse = GetWarehouseOfMRP(User.Identity.Name);
                    if (warehouse != null)
                        ViewBag.WHName = warehouse.WHName;

                    var userID = GetUserID(User.Identity.Name);
                    var list = await db.PRODUCT_STOCKS
                                            .Include(x => x.Warehouse)
                                            .Include(x => x.PRODUCT)
                                            .Include(x => x.UNIT)
                                            .Where(x => x.Warehouse.MRP == userID)
                                            .ToListAsync();
                    return View(list);
                }
                else
                {
                    ViewBag.warehouseList = new SelectList(db.WAREHOUSES.Where(wh => wh.IsDeleted == false).ToList(), "ID", "WHName", whid);
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";

                    var list = await db.PRODUCT_STOCKS
                                            .Include(x => x.Warehouse)
                                            .Include(x => x.PRODUCT)
                                            .Include(x => x.UNIT)
                                            .Where(x => x.Warehouse.ID == whid)
                                            .ToListAsync();
                    return View(list);
                }

            }
        }
        private Warehouse GetWarehouseOfMRP(string username)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                USER currentUser = db.USERS.FirstOrDefault(u => u.Uname == username && u.IsDeleted != true && u.IsActive);
                Warehouse this_warehouse = currentUser != null ? db.WAREHOUSES.FirstOrDefault(wrhs => wrhs.MRP == currentUser.ID) : null;

                return this_warehouse;
            }
        }
        private int? GetUserID(string username)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                USER currentUser = db.USERS
                                     .FirstOrDefault(u => u.Uname == username && u.IsDeleted != true && u.IsActive);

                return currentUser?.ID;
            }
        }
        public async Task<ActionResult> Download()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SAMPLE_FILES file = await db.SAMPLE_FILES.Where(s => s.FileName.CompareTo("inventarizatsiya.xlsx") == 0).FirstOrDefaultAsync();
                if (file != null)
                    return File(file.File, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

                return View();
            }
        }
        public ActionResult UploadWithExcel()
        {
            ViewBag.IsFileUploaded = false;
            using (DBTHSNEntities db = new DBTHSNEntities())
                ViewBag.warehouseList = new SelectList(db.WAREHOUSES.Where(wh => wh.IsDeleted == false).ToList(), "ID", "WHName");

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

                        using (var workbook = new XLWorkbook(file.InputStream))
                        {
                            var worksheet = workbook.Worksheet(1);
                            var rowCount = worksheet.LastRowNumber();
                            var colCount = 3;

                            for (int col = 1; col <= colCount; col++)
                            {
                                dataTable.Columns.Add(worksheet.Cell(1, col).GetFormattedString());
                            }

                            for (int row = 2; row <= rowCount; row++)
                            {
                                var dataRow = dataTable.NewRow();
                                for (int col = 1; col <= colCount; col++)
                                {
                                    dataRow[col - 1] = worksheet.Cell(row, col).GetFormattedString();
                                }
                                dataTable.Rows.Add(dataRow);
                            }
                        }

                        if (CheckForExistenceOfParts(dataTable))
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
                            using (DBTHSNEntities db = new DBTHSNEntities())
                                ViewBag.warehouseList = new SelectList(db.WAREHOUSES.Where(wh => wh.IsDeleted == false).ToList(), "ID", "WHName");
                            return View("UploadWithExcel");
                        }

                    }
                    catch (Exception ex)
                    {
                        ViewBag.Message = $"Faylni yuklashda quyidagicha muammo tug'ildi: {ex.Message}";
                        using (DBTHSNEntities db = new DBTHSNEntities())
                            ViewBag.warehouseList = new SelectList(db.WAREHOUSES.Where(wh => wh.IsDeleted == false).ToList(), "ID", "WHName");
                        return View("UploadWithExcel");
                    }
                }
                else
                {
                    ViewBag.Message = "Format noto'g'ri. Faqat .xlsx fayllarni yuklash mumkin.";
                    using (DBTHSNEntities db = new DBTHSNEntities())
                        ViewBag.warehouseList = new SelectList(db.WAREHOUSES.Where(wh => wh.IsDeleted == false).ToList(), "ID", "WHName");
                    return View("UploadWithExcel");
                }
            }
            else
            {
                ViewBag.Message = "Fayl bo'm-bo'sh yoki yuklanmadi!";
                using (DBTHSNEntities db = new DBTHSNEntities())
                    ViewBag.warehouseList = new SelectList(db.WAREHOUSES.Where(wh => wh.IsDeleted == false).ToList(), "ID", "WHName");
                return View("UploadWithExcel");
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
                ViewBag.warehouseList = new SelectList(db.WAREHOUSES.Where(wh => wh.IsDeleted == false).ToList(), "ID", "WHName");
            return View("UploadWithExcel");
        }
        private bool CheckForExistenceOfParts(DataTable dataTable)
        {
            bool flag = false;
            if (dataTable != null)
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        string partPNo = row["Detal Raqami"].ToString();
                        if (db.PRODUCTS.Where(p => p.PNo.CompareTo(partPNo) == 0 && p.IsRawMaterial == true).Any())
                            flag = true;
                        else
                        {
                            if (!missingParts.Contains(partPNo))
                                missingParts.Add(partPNo);
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
            // Clear the DataTable va related ViewBag properties
            ViewBag.DataTable = null;
            ViewBag.DataTableModel = null;
            ViewBag.IsFileUploaded = false;
            ViewBag.Message = "Jadval ma'lumotlari o'chirib yuborildi.";
            return View("UploadWithExcel");
        }
        [HttpPost]
        public ActionResult Save(string dataTableModel, int? WarehouseID)
        {
            if (WarehouseID > 0)
            {
                string pNo = "", amount = "", unitName = "";
                if (!string.IsNullOrEmpty(dataTableModel))
                {
                    var tableModel = JsonConvert.DeserializeObject<System.Data.DataTable>(dataTableModel);

                    try
                    {
                        using (DBTHSNEntities db = new DBTHSNEntities())
                        {
                            foreach (DataRow row in tableModel.Rows)
                            {
                                pNo = row["Detal Raqami"].ToString();
                                amount = row["Qoldiq"].ToString();
                                unitName = row["Birligi"].ToString();

                                UNIT unit = db.UNITS.Where(u => u.ShortName.CompareTo(unitName) == 0).FirstOrDefault();
                                PRODUCT part = db.PRODUCTS.Where(p => p.PNo.CompareTo(pNo) == 0 && p.IsDeleted == false).FirstOrDefault();

                                if (part != null)
                                {
                                    var whId = Convert.ToInt32(WarehouseID);
                                    var unitId = unit != null ? unit.ID : 1;
                                    var stock = WarehouseFlowService.GetOrCreateStockRow(db, whId, part.ID, unitId);
                                    stock.Amount = Convert.ToDouble(amount);
                                    stock.UnitID = unitId;
                                    db.SaveChanges();

                                    var userEmail1 = User.Identity.Name;
                                    LogHelper.LogToDatabase(userEmail1, "Inventarizatsiya", $"{pNo} ehtiyot qismi uchun {amount}  {unit.ShortName} bo'lgan qoldiqni {DateTime.Now} paytida kiritdi");

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
            else
            {
                ViewBag.Message = "Ombor tanlanmagan. Avval kirim qilinadigan omborni tanlab oling va keyin qaytadan urunib ko'ring.";
                using (DBTHSNEntities db = new DBTHSNEntities())
                    ViewBag.warehouseList = new SelectList(db.WAREHOUSES.Where(wh => wh.IsDeleted == false).ToList(), "ID", "WHName");

                return View("UploadWithExcel");
            }

        }
    }
}
