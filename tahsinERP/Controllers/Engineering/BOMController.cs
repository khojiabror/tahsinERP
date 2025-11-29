using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using tahsinERP.Models;
using tahsinERP.ViewModels.BOM;
using static tahsinERP.ViewModels.BOM.BOMCreateProductViewModel;
namespace tahsinERP.Controllers
{
    public class BOMController : Controller
    {
        List<string> missingParts = new List<string>();
        List<string> missingProducts = new List<string>();
        public ActionResult Index(IndexViewModel viewModel)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (viewModel.IsParentProduct == true)
                {
                    //var results = db.BOMS.Where(bom => bom.IsParentProduct == true).GroupBy(bom => bom.ParentPNo).Select(group => group.FirstOrDefault()).ToList();
                    List<IndexViewModel> result = db.BOMS
                                                    .Where(x => x.IsDeleted == false)
                                                    .Join(db.PRODUCTS, bom => bom.ParentPNo, product => product.PNo, (bom, product) =>
                    new IndexViewModel
                    {
                        ParentPNo = bom.ParentPNo,
                        ProductName = product.Name,
                        ProductID = product.ID,
                        IsParentProduct = true,
                        ID = product.ID
                    }).GroupBy(item => new { item.ParentPNo, item.ID })
                      .Select(group => group.FirstOrDefault())
                      .ToList();

                    ViewBag.IsParentProduct = true;
                    return View(result);
                }
                else
                {
                    List<IndexViewModel> result = db.BOMS
                                                    .Where(x => x.IsDeleted == false)
                                                    .Join(db.PRODUCTS, bom => bom.ParentPNo, product => product.PNo, (bom, product) =>
                    new IndexViewModel
                    {
                        ParentPNo = bom.ParentPNo,
                        ProductName = product.Name,
                        ProductID = product.ID,
                        IsParentProduct = false,
                        ID = product.ID
                    }).GroupBy(item => new { item.ParentPNo, item.ID })
                      .Select(group => group.FirstOrDefault())
                      .ToList();

                    ViewBag.IsParentProduct = false;
                    return View(result);
                }
            }
        }
        public ActionResult Details(int ID, bool isParent)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PRODUCT product = db.PRODUCTS
                                    .Where(p => p.ID == ID && p.IsDeleted == false)
                                    .FirstOrDefault();
                PRODUCT part = db.PRODUCTS
                              .Where(p => p.ID == ID && p.IsDeleted == false)
                              .FirstOrDefault();

                try
                {
                    if (product != null)
                    {
                        var rootItem = GetBomTree(product.PNo);
                        return View(rootItem);
                    }
                    else if (part != null)
                    {
                        var rootItem = GetBomTree(part.PNo);
                        return View(rootItem);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex);
                }
                return View();
            }
        }
        private BoomViewModel GetBomTree(string parentPno)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var parentItems = db.BOMS.Where(b => b.ParentPNo == parentPno && b.IsDeleted == false).ToList();

                if (!parentItems.Any())
                {
                    return null;
                }

                var root = new BoomViewModel()
                {
                    ParentPNo = parentPno,
                    ParentImageBase64 = GetParentImage(parentPno),
                    Children = parentItems.Select(b => new BoomViewModel
                    {
                        ParentPNo = b.ParentPNo,
                        ChildPNo = b.ChildPNo,
                        ChildImageBase64 = GetChildImage(b.ChildPNo),
                        Consumption = b.Consumption,
                        ConsumptionUnit = "",
                        Children = GetBomTree(b.ChildPNo)?.Children
                    }).ToList()
                };

                return root;
            }
        }
        private string GetParentImage(string PNo)
        {
            string Base64String = "";
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var product = db.PRODUCTS.Where(p => p.PNo.CompareTo(PNo) == 0).FirstOrDefault();
                var part = db.PRODUCTS.Where(p => p.PNo.CompareTo(PNo) == 0 && p.IsRawMaterial == true).FirstOrDefault();
                if (product != null)
                {
                    var productImage = db.PRODUCTIMAGES.FirstOrDefault(pi => pi.ProdID == product.ID);
                    if (productImage != null)
                    {
                        Base64String = "data:image/jpeg;base64," + Convert.ToBase64String(productImage.Image);
                    }
                }
                else
                {
                    var partImage = db.PRODUCTIMAGES.FirstOrDefault(pi => pi.ProdID == part.ID);
                    if (partImage != null)
                    {
                        Base64String = "data:image/jpeg;base64," + Convert.ToBase64String(partImage.Image);
                    }
                }
                return Base64String;
            }
        }
        private string GetChildImage(string PNo)
        {

            string Base64String = "";
            using (DBTHSNEntities db = new DBTHSNEntities())
            {

                var part = db.PRODUCTS.Where(p => p.PNo.CompareTo(PNo) == 0).FirstOrDefault();
                var partImage = db.PRODUCTIMAGES.FirstOrDefault(pi => pi.ProdID == part.ID);
                if (partImage != null)
                {
                    Base64String = "data:image/jpeg;base64," + Convert.ToBase64String(partImage.Image);
                }
                return Base64String;
            }
        }
        public ActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(BOMCreateProductViewModel model, BoomViewModel vmodel)
        {
            return RedirectToAction("CompletionStatus", vmodel);
        }
        private int? GetUserID(string email)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                USER currentUser = db.USERS
                                     .Where(u => u.Email.CompareTo(email) == 0 && u.IsDeleted == false && u.IsActive == true)
                                     .FirstOrDefault();

                return currentUser?.ID;
            }
        }
        private void DeleteBomTree(string parentPno)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var parentItems = db.BOMS.Where(b => b.ParentPNo == parentPno && b.IsDeleted == false).ToList();

                foreach (var item in parentItems)
                {
                    DeleteBomTree(item.ChildPNo);
                    item.IsDeleted = true;
                }
                db.SaveChanges();
            }
        }
        [HttpPost]
        public ActionResult Delete(int ID, FormCollection fmc)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PRODUCT product = db.PRODUCTS.Where(p => p.ID == ID && p.IsDeleted == false).FirstOrDefault();
                PRODUCT part = db.PRODUCTS.Where(p => p.ID == ID && p.IsDeleted == false && p.IsRawMaterial == true).FirstOrDefault();

                try
                {
                    if (product != null)
                    {
                        DeleteBomTree(product.PNo);

                        product.IsDeleted = true;
                        db.SaveChanges();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "BOMController", $"{ID} ID ga ega Maxsulotni o'chirdi");
                    }
                    else if (part != null)
                    {
                        DeleteBomTree(part.PNo);

                        part.IsDeleted = true;
                        db.SaveChanges();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "BOMController", $"{ID} ID ga ega Qismni o'chirdi");
                    }

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex);
                }

                return View();
            }
        }
        public ActionResult Download()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SAMPLE_FILES detal = db.SAMPLE_FILES.Where(s => s.FileName.CompareTo("boms.xlsx") == 0).FirstOrDefault();
                if (detal != null)
                    return File(detal.File, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
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
                                    dataRow[col - 1] = worksheet.Cells[row, col].Text;
                                }
                                dataTable.Rows.Add(dataRow);
                            }
                        }
                        if (CheckForExistenceOfProducts(dataTable))
                        {
                            if (CheckForExistenceOfParts(dataTable))
                            {
                                ViewBag.DataTable = dataTable;
                                ViewBag.DataTableModel = JsonConvert.SerializeObject(dataTable);
                                ViewBag.IsFileUploaded = true;

                                using (DBTHSNEntities db = new DBTHSNEntities())
                                {
                                    foreach (DataRow row in dataTable.Rows)
                                    {
                                        string ParentPNo = row["ParentPNo"].ToString();
                                        string ChildPNo = row["ChildPNo"].ToString();
                                        string ConsumptionUnit = row["ConsumptionUnit"].ToString();
                                        string Consumption = row["Consumption"].ToString();
                                        string IsParentProduct = row["IsParentProduct?"].ToString();

                                        BOM existBom = db.BOMS.Where(p => p.ParentPNo.CompareTo(ParentPNo) == 0 && p.ChildPNo.CompareTo(ChildPNo) == 0 && p.IsDeleted == false).FirstOrDefault();

                                        if (existBom != null)
                                        {
                                            ViewBag.ExistingRecordsCount = 1;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var message = "";
                                foreach (var word in missingParts)
                                {
                                    message += word + ", ";
                                }
                                ViewBag.Message = "Ushbu BOM faylda kiritilgan ehtiyot qismlar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Ehtiyot qismlar bazasiga kiritib keyin qayta urining.";
                                return View("UploadWithExcel");
                            }
                        }
                        else
                        {
                            var message = "";
                            foreach (var word in missingProducts)
                            {
                                message += word + ", ";
                            }
                            ViewBag.Message = "Ushbu BOM faylda kiritilgan maxsulotlar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Maxsulotlar bazasiga kiritib keyin qayta urining.";
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
        private bool CheckForExistenceOfParts(DataTable dataTable)
        {
            bool flag = false;
            if (dataTable != null)
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        string childPNo = row["ChildPNo"].ToString();
                        if (db.PRODUCTS.Where(p => p.PNo.CompareTo(childPNo) == 0).Any())
                            flag = true;
                        else
                        {
                            if (!missingParts.Contains(childPNo))
                                missingParts.Add(childPNo);
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
        private bool CheckForExistenceOfProducts(DataTable dataTable)
        {
            bool flag = false;
            if (dataTable != null)
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        string parentPNo = row["ParentPNo"].ToString();
                        if (db.PRODUCTS.Where(p => p.PNo.CompareTo(parentPNo) == 0).Any())
                            flag = true;
                        else
                        {
                            if (!missingProducts.Contains(parentPNo))
                                missingProducts.Add(parentPNo);
                        }
                    }
                    if (missingProducts.Count > 0)
                        return false;
                    else
                        return flag;
                }
            else
                return flag;

        }
        [HttpPost]
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
        public async Task<ActionResult> Save(string dataTableModel)
        {
            if (!string.IsNullOrEmpty(dataTableModel))
            {
                await Task.Run(() =>
                {
                    var tableModel = JsonConvert.DeserializeObject<DataTable>(dataTableModel);

                    try
                    {
                        using (DBTHSNEntities db = new DBTHSNEntities())
                        {
                            foreach (DataRow row in tableModel.Rows)
                            {
                                string ParentPNo = row["ParentPNo"].ToString();
                                string ChildPNo = row["ChildPNo"].ToString();
                                string ConsumptionUnit = row["ConsumptionUnit"].ToString();
                                string Consumption = row["Consumption"].ToString();
                                string IsParentProduct = row["IsParentProduct?"].ToString();

                                BOM bom = new BOM();
                                bom.Consumption = Convert.ToDouble(Consumption);
                                bom.ParentPNo = ParentPNo;
                                bom.ChildPNo = ChildPNo;
                                bom.IsDeleted = false;
                                if (IsParentProduct.CompareTo("Yes") == 0)
                                    bom.IsParentProduct = true;
                                else
                                    bom.IsParentProduct = false;

                                db.BOMS.Add(bom);
                                db.SaveChanges();

                                var userEmail = User.Identity.Name;
                                LogHelper.LogToDatabase(userEmail, "BOMController", $"{bom.ID} ID ga ega BOM ni Excell orqali qo'shdi");
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", ex.Message);
                    }
                });
            }

            return RedirectToAction("Index");
        }
        public ActionResult Edit(int ID, BoomViewModel model)
        {
            return View();
        }
        [HttpPost]
        public ActionResult Edit(BomEditViewModels model)
        {
            return RedirectToAction("Index");
        }
    }
}

