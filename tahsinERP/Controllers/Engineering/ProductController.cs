using Newtonsoft.Json;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using System.Data.Entity;
using tahsinERP.Filters;
using tahsinERP.Services.Common;
using tahsinERP.I18n;

namespace tahsinERP.Controllers
{
    [Permission]
    public class ProductController : BaseController
    {
        // Formadan bog'lanadigan maydonlar (avval butun PRODUCT entity bog'lanardi — over-posting, MES-B17)
        private const string ProductFormFields = "ID,PNo,Name,Description,Weight,Length,Width,Height,StandardLengthMm,UnitID,PNo2,PNo3,PNo4,ShopID,CategoryID,HSCodeID";

        private int productPhotoMaxLength = Convert.ToInt32(ConfigurationManager.AppSettings["photoMaxSize"]);
        private DBTHSNEntities db = new DBTHSNEntities();

        // Product type constants
        private const string PRODUCT_TYPE_ALL = "ALL";
        private const string PRODUCT_TYPE_FINISHED_GOOD = "MAXSULOT";
        private const string PRODUCT_TYPE_COMPONENT = "YARIM_TAYYOR";
        private const string PRODUCT_TYPE_RAW_MATERIAL = "XOMASHYO";

        #region Helpers

        private static SelectList GetUnitSelectList(object selected = null)
        {
            // Birliklar keshdan (Extra = ShortName)
            var units = ReferenceDataCache.Get(ReferenceDataCache.Units)
                .Select(u => new { u.ID, ShortName = u.Extra ?? u.Name })
                .OrderBy(u => u.ShortName)
                .ToList();
            return new SelectList(units, "ID", "ShortName", selected);
        }

        /// <summary>Create/Edit formasi dropdownlari (avval 5 joyda nusxalangan edi — dublikat #26).</summary>
        private async Task PopulateFormLookupsAsync(int? unitId, int? shopId, int? categoryId, string productType)
        {
            ViewBag.UNIT = GetUnitSelectList(unitId);
            ViewBag.TypeList = GetProductTypeFormSelectList(productType);
            ViewBag.ShopList = ReferenceDataCache.SelectList(ReferenceDataCache.Shops, shopId);
            var categories = await db.PRODUCT_CATEGORIES.AsNoTracking()
                .Where(c => c.IsDeleted != true)
                .OrderBy(c => c.Name)
                .Select(c => new { c.ID, c.Name })
                .ToListAsync();
            ViewBag.CategoryList = new SelectList(categories, "ID", "Name", categoryId);
        }

        // INDEX filtri uchun Type list
        private SelectList GetProductTypeFilterSelectList(string selected = "ALL")
        {
            var items = new List<SelectListItem>
            {
                new SelectListItem { Value = PRODUCT_TYPE_ALL, Text = L.T("All") },
                new SelectListItem { Value = PRODUCT_TYPE_FINISHED_GOOD, Text = L.T("Prd_TypeFinished") },
                new SelectListItem { Value = PRODUCT_TYPE_COMPONENT, Text = L.T("Prd_SemiFinishedProduct") },
                new SelectListItem { Value = PRODUCT_TYPE_RAW_MATERIAL, Text = L.T("Prd_TypeRaw") }
            };

            return new SelectList(items, "Value", "Text", selected);
        }

        // Yaratish/Edit form uchun Type list
        private SelectList GetProductTypeFormSelectList(string selected = null)
        {
            var items = new List<SelectListItem>
            {
                new SelectListItem { Value = PRODUCT_TYPE_FINISHED_GOOD, Text = L.T("Prd_TypeFinished") },
                new SelectListItem { Value = PRODUCT_TYPE_COMPONENT, Text = L.T("Prd_SemiFinishedProduct") },
                new SelectListItem { Value = PRODUCT_TYPE_RAW_MATERIAL, Text = L.T("Prd_TypeRaw") }
            };

            return new SelectList(items, "Value", "Text", selected ?? PRODUCT_TYPE_FINISHED_GOOD);
        }

        // Olish product type string dan boolean flags
        private string GetProductTypeFromFlags(PRODUCT product)
        {
            if (product.IsFinishedGood == true)
                return PRODUCT_TYPE_FINISHED_GOOD;
            if (product.IsComponent == true)
                return PRODUCT_TYPE_COMPONENT;
            if (product.IsRawMaterial == true)
                return PRODUCT_TYPE_RAW_MATERIAL;

            return PRODUCT_TYPE_FINISHED_GOOD; // standart
        }

        // Belgilash boolean flags based on product type string
        private void SetProductTypeFlags(PRODUCT product, string productType)
        {
            // Reset barcha flags
            product.IsFinishedGood = false;
            product.IsComponent = false;
            product.IsRawMaterial = false;

            // Belgilash appropriate flag
            switch (productType)
            {
                case PRODUCT_TYPE_FINISHED_GOOD:
                    product.IsFinishedGood = true;
                    product.Type = "MAXSULOT";
                    break;
                case PRODUCT_TYPE_COMPONENT:
                    product.IsComponent = true;
                    product.Type = "YARIM TAYYOR MAXSULOT";
                    break;
                case PRODUCT_TYPE_RAW_MATERIAL:
                    product.IsRawMaterial = true;
                    product.Type = "XOMASHYO";
                    break;
                default:
                    product.IsFinishedGood = true;
                    product.Type = "MAXSULOT";
                    break;
            }
        }

        #endregion

        // Olish so'rovi: Product
        public async Task<ActionResult> Index()
        {
            var list = await db.PRODUCTS.AsNoTracking()
                .Include(p => p.UNIT)
                .Include(p => p.SHOP)
                .Where(p => p.IsDeleted == false)
                .OrderBy(p => p.PNo)
                .ToListAsync();

            ViewBag.TypeFilterList = GetProductTypeFilterSelectList();
            ViewBag.ChangePermit = RoleHelper.IsChangePermitted(User.Identity.Name, "Product", "Index");

            return View(list);
        }

        // Olish so'rovi: Product/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var product = await db.PRODUCTS
                            .Include(p => p.UNIT)
                            .Include(p => p.SHOP)
                            .Include(p => p.PRODUCT_CATEGORIES)
                            .FirstOrDefaultAsync(p => p.ID == id && p.IsDeleted == false);

            if (product == null)
                return HttpNotFound();

            SetProductImageUrl(id);

            return View(product);
        }

        // Olish so'rovi: Product/yaratish
        public async Task<ActionResult> Create()
        {
            await PopulateFormLookupsAsync(null, null, null, PRODUCT_TYPE_FINISHED_GOOD);
            return View();
        }

        // POST so'rovi: Product/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = ProductFormFields)] PRODUCT product, string ProductType)
        {
            if (ModelState.IsValid)
            {
                product.IsDeleted = false;

                // Belgilash product type flags based on selection
                SetProductTypeFlags(product, ProductType);

                db.PRODUCTS.Add(product);
                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "ProductController",
                    $"{product.PNo} - maxsulotni yaratdi");

                // Handle image upload
                await HandleImageUploadAsync(product.ID, product.PNo);

                return RedirectToAction("Index");
            }

            await PopulateFormLookupsAsync(product.UnitID, product.ShopID, product.CategoryID, ProductType);
            return View(product);
        }

        // yordamchi method ga handle image upload
        private async Task<bool> HandleImageUploadAsync(int productId, string productPNo, bool isUpdate = false)
        {
            var imageFile = Request.Files["productPhotoUpload"];
            if (imageFile == null || imageFile.ContentLength == 0)
                return true;

            if (imageFile.ContentLength >= productPhotoMaxLength)
            {
                ModelState.AddModelError("", L.T("Prd_ImageTooLarge"));
                return false;
            }

            // agar updating, mark existing image as deleted
            if (isUpdate)
            {
                var existingImage = await db.PRODUCTIMAGES
                    .Where(pi => pi.ProdID == productId && pi.IsDeleted != true)
                    .FirstOrDefaultAsync();

                if (existingImage != null)
                {
                    existingImage.IsDeleted = true;
                }
            }

            // O'qish image ma'lumot
            byte[] imageData;
            using (var binaryReader = new BinaryReader(imageFile.InputStream))
            {
                imageData = binaryReader.ReadBytes(imageFile.ContentLength);
            }

            var photoImage = new PRODUCTIMAGE
            {
                ProdID = productId,
                Image = imageData,
                IsDeleted = false
            };

            db.PRODUCTIMAGES.Add(photoImage);
            await db.SaveChangesAsync();

            var action = isUpdate ? "yangiladi" : "yaratdi";
            LogHelper.LogToDatabase(User.Identity.Name, "ProductController", $"{productPNo} - uchun ProductImageni {action}");

            return true;
        }

        // Olish so'rovi: Product/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var product = await db.PRODUCTS.FindAsync(id);
            if (product == null || product.IsDeleted)
                return HttpNotFound();

            // Olish current product type dan flags
            var currentProductType = GetProductTypeFromFlags(product);

            await PopulateFormLookupsAsync(product.UnitID, product.ShopID, product.CategoryID, currentProductType);
            ViewBag.CurrentProductType = currentProductType;

            SetProductImageUrl(id);

            return View(product);
        }

        // POST so'rovi: Product/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = ProductFormFields)] PRODUCT product, string ProductType)
        {
            if (!ModelState.IsValid)
            {
                await PopulateFormLookupsAsync(product.UnitID, product.ShopID, product.CategoryID, ProductType);
                return View(product);
            }

            var entity = await db.PRODUCTS.FindAsync(product.ID);
            if (entity == null || entity.IsDeleted)
                return HttpNotFound();

            // Yangilash fields
            entity.PNo = product.PNo;
            entity.Name = product.Name;
            entity.Description = product.Description;
            entity.Weight = product.Weight;
            entity.Length = product.Length;
            entity.Width = product.Width;
            entity.Height = product.Height;
            // Kesish hisoblari shu maydonga tayanadi, lekin u avval UI'da umuman tahrirlanmasdi (MES-B21)
            entity.StandardLengthMm = product.StandardLengthMm;
            entity.UnitID = product.UnitID;
            entity.PNo2 = product.PNo2;
            entity.PNo3 = product.PNo3;
            entity.PNo4 = product.PNo4;
            entity.ShopID = product.ShopID;
            entity.CategoryID = product.CategoryID;

            // Belgilash product type flags
            SetProductTypeFlags(entity, ProductType);

            db.Entry(entity).State = EntityState.Modified;
            await db.SaveChangesAsync();

            LogHelper.LogToDatabase(User.Identity.Name, "ProductController",
                $"{entity.PNo} - Productni tahrirladi");

            // Handle image upload on Edit
            if (!await HandleImageUploadAsync(product.ID, entity.PNo, isUpdate: true))
            {
                await PopulateFormLookupsAsync(product.UnitID, product.ShopID, product.CategoryID, ProductType);
                return View(product);
            }

            return RedirectToAction("Index");
        }

        // Olish so'rovi: Product/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var product = await db.PRODUCTS
                            .Include(p => p.UNIT)
                            .Include(p => p.SHOP)
                            .FirstOrDefaultAsync(p => p.ID == id && p.IsDeleted == false);

            if (product == null)
                return HttpNotFound();

            SetProductImageUrl(id);

            return View(product);
        }

        // POST so'rovi: Product/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            var product = await db.PRODUCTS.FindAsync(id);
            if (product == null)
                return HttpNotFound();

            product.IsDeleted = true;
            await SoftDeleteReferenceService.AfterProductSoftDeletedAsync(db, id);
            await db.SaveChangesAsync();

            LogHelper.LogToDatabase(User.Identity.Name, "ProductController",
                $"{product.PNo} - maxsulotni o'chirdi");

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Rasm sahifaga base64 bo'lib qo'shilmaydi — ProductImage/Image URL'i orqali keshlanib yuklanadi (MES-P23, dublikat #24).
        /// </summary>
        private void SetProductImageUrl(int? productId)
        {
            var imageId = productId.HasValue ? ProductImageController.CurrentImageId(db, productId.Value) : null;
            ViewBag.ProductImageUrl = imageId.HasValue ? ProductImageController.UrlFor(productId.Value, imageId.Value, 600) : null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        // Import shabloni kodda yasaladi (SAMPLE_FILES'da qator yo'q edi — 404)
        public ActionResult Download()
        {
            return File(ProductImportTemplate.Build(), ExcelHelper.XlsxContentType, ProductImportTemplate.FileName);
        }

        public ActionResult UploadWithExcel()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<ActionResult> UploadWithExcel(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                ViewBag.Message = L.T("Prd_FileEmptyOrNotUploaded");
                return View();
            }

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Message = L.T("Prd_OnlyXlsx");
                return View();
            }

            try
            {
                var dataTable = new DataTable();

                using (var workbook = new XLWorkbook(file.InputStream))
                {
                    var worksheet = workbook.Worksheet(1);
                    if (worksheet.LastRowNumber() == 0)
                    {
                        ViewBag.Message = L.T("Prd_ExcelEmpty");
                        return View();
                    }

                    int rowCount = worksheet.LastRowNumber();
                    int colCount = worksheet.LastColumnNumber();

                    // Qo'shish ustunlar dan header qator
                    for (int col = 1; col <= colCount; col++)
                    {
                        dataTable.Columns.Add(worksheet.Cell(1, col).GetFormattedString());
                    }

                    // Qo'shish ma'lumot qatorlar
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

                // Tekshirish uchun existing records
                int existingCount = 0;
                if (dataTable.Columns.Contains("Partnumber"))
                {
                    var pnos = dataTable.AsEnumerable().Select(r => r["Partnumber"].ToString()).ToList();
                    existingCount = await db.PRODUCTS.CountAsync(p => pnos.Contains(p.PNo) && p.IsDeleted == false);
                }

                ViewBag.DataTable = dataTable;
                ViewBag.DataTableModel = JsonConvert.SerializeObject(dataTable);
                ViewBag.ExistingRecordsCount = existingCount;
            }
            catch (Exception ex)
            {
                ViewBag.Message = L.T("Prd_ErrorWithMessage", ex.Message);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<ActionResult> Save(string dataTableModel)
        {
            if (string.IsNullOrWhiteSpace(dataTableModel))
            {
                TempData["Error"] = L.T("Prd_DataNotFound");
                return RedirectToAction("Index");
            }

            DataTable tableModel;
            try
            {
                tableModel = JsonConvert.DeserializeObject<DataTable>(dataTableModel);
                if (tableModel == null || tableModel.Rows.Count == 0)
                {
                    TempData["Error"] = L.T("Prd_ExcelNoData");
                    return RedirectToAction("Index");
                }
            }
            catch (JsonException)
            {
                TempData["Error"] = L.T("Prd_DataReadError");
                return RedirectToAction("Index");
            }

            try
            {
                // Olish standart ShopID (birinchi faol shop)
                var defaultShop = await db.SHOPS
                    .FirstOrDefaultAsync(s => s.IsDeleted != true);

                int defaultShopId = defaultShop?.ID ?? 1;
                // Import qilingan mahsulot birligi — "dona" (UNITS.SystemCode = PCS)
                int piecesUnitId = SystemRefs.UnitId(db, SystemCodes.Unit.Pieces) ?? 0;
                if (piecesUnitId == 0)
                    throw new InvalidOperationException(L.T("Prd_PiecesUnitMissing"));

                // Mavjud PNo'lar bir martada; yangi qatorlar birga qo'shiladi, bitta SaveChanges va bitta log
                // (avval har qatorga AnyAsync + SaveChanges + log, MES-P24). Fayl ichidagi takror PNo ham o'tkazib yuboriladi.
                var existingPNos = new HashSet<string>(
                    await db.PRODUCTS.Where(p => p.IsDeleted == false && p.PNo != null).Select(p => p.PNo).ToListAsync(),
                    StringComparer.OrdinalIgnoreCase);
                var newProducts = new List<PRODUCT>();

                foreach (DataRow row in tableModel.Rows)
                {
                    var pno = row.Table.Columns.Contains("Partnumber") ? row["Partnumber"].ToString().Trim() : "";
                    if (string.IsNullOrEmpty(pno) || !existingPNos.Add(pno)) continue;

                    var newProduct = new PRODUCT
                    {
                        PNo = pno,
                        Name = row.Table.Columns.Contains("Name") ? row["Name"].ToString() : "",
                        Weight = (decimal?)(ParseDoubleOrNull(row, "Weight")),
                        Length = (decimal?)(ParseDoubleOrNull(row, "Length")),
                        Width = (decimal?)(ParseDoubleOrNull(row, "Width")),
                        Height = (decimal?)(ParseDoubleOrNull(row, "Height")),
                        PNo2 = row.Table.Columns.Contains("PNo2") ? row["PNo2"].ToString() : null,
                        PNo3 = row.Table.Columns.Contains("PNo3") ? row["PNo3"].ToString() : null,
                        PNo4 = row.Table.Columns.Contains("PNo4") ? row["PNo4"].ToString() : null,
                        UnitID = piecesUnitId,
                        ShopID = defaultShopId,
                        IsDeleted = false
                    };

                    // Handle product type dan Excel
                    var typeFromExcel = row.Table.Columns.Contains("Type") ? row["Type"].ToString().Trim().ToUpper() : "";
                    SetProductTypeFlags(newProduct, ProductTypeFromExcel(typeFromExcel));
                    newProducts.Add(newProduct);
                }

                db.PRODUCTS.AddRange(newProducts);
                await db.SaveChangesAsync();
                var savedCount = newProducts.Count;

                if (savedCount > 0)
                {
                    LogHelper.LogToDatabase(User.Identity.Name, "ProductController",
                        $"Excel orqali {savedCount} ta maxsulot yaratildi: {string.Join(", ", newProducts.Take(20).Select(p => p.PNo))}{(savedCount > 20 ? " ..." : "")}");
                }

                TempData["Success"] = L.T("Prd_ProductsSaved", savedCount);
            }
            catch (Exception ex)
            {
                TempData["Error"] = L.T("Prd_ErrorWithMessage", ex.Message);
            }

            return RedirectToAction("Index");
        }

        // yordamchi method ga tahlil double dan DataRow
        private double? ParseDoubleOrNull(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName))
                return null;

            var value = row[columnName].ToString();
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (double.TryParse(value, out double result))
                return result;

            return null;
        }

        // Excel "Type" ustuni matni -> mahsulot turi kodi; bayroqlarni SetProductTypeFlags qo'yadi
        // (avval bayroq/Type qo'yish ikki joyda nusxalangan edi, dublikat #25)
        private static string ProductTypeFromExcel(string typeValue)
        {
            typeValue = typeValue ?? "";
            if (typeValue.Contains("XOMASHYO") || typeValue.Contains("RAW") || typeValue.Contains("MATERIAL"))
                return PRODUCT_TYPE_RAW_MATERIAL;
            if (typeValue.Contains("YARIM") || typeValue.Contains("TAYYOR") || typeValue.Contains("COMPONENT") || typeValue.Contains("PART"))
                return PRODUCT_TYPE_COMPONENT;
            return PRODUCT_TYPE_FINISHED_GOOD;
        }
    }
}
