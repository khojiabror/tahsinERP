using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
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
    public class PContractController : Controller
    {
        private string supplierName, contractNo, partNo = "";
        private int contractDocMaxLength = Convert.ToInt32(ConfigurationManager.AppSettings["photoMaxSize"]);
        private List<string> missingSuppliers = new List<string>();
        private List<string> missingParts = new List<string>();

        private string[] sources;
        public PContractController()
        {
            sources = ConfigurationManager.AppSettings["partTypes"].Split(',');
            sources = sources.Where(x => !x.Equals("InHouse", StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        // GET: Contracts
        public ActionResult Index(string type, int? supplierID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var suppliers = db.SUPPLIERS.Where(s => s.IsDeleted == false).ToList();
                if (!string.IsNullOrEmpty(type))
                {
                    if (supplierID.HasValue)
                    {
                        ViewBag.partList = db.P_CONTRACTS.Include(x => x.SUPPLIER).Where(s => s.SupplierID == supplierID && (s.SUPPLIER.Type.CompareTo(type) == 0)).ToList();
                        ViewBag.SourceList = new SelectList(sources, type);
                        ViewBag.SupplierList = new SelectList(suppliers.Where(x => x.IsDeleted == false && x.Type.CompareTo(type) == 0), "ID", "Name");
                    }
                    else
                    {
                        ViewBag.partList = db.P_CONTRACTS.Include(x => x.SUPPLIER).Where(s => s.IsDeleted == false && (s.SUPPLIER.Type.CompareTo(type) == 0)).ToList();
                        ViewBag.SourceList = new SelectList(sources, type);
                        ViewBag.SupplierList = new SelectList(suppliers.Where(x => x.IsDeleted == false && x.Type.CompareTo(type) == 0), "ID", "Name");
                    }
                }
                else
                {
                    if (supplierID.HasValue)
                    {
                        ViewBag.partList = db.P_CONTRACTS.Include(x => x.SUPPLIER).Where(s => s.IsDeleted == false && s.SupplierID == supplierID).ToList();
                        ViewBag.SourceList = new SelectList(sources, type);
                        ViewBag.SupplierList = new SelectList(suppliers.Where(x => x.IsDeleted == false), "ID", "Name");
                    }
                    else
                    {
                        ViewBag.partList = db.P_CONTRACTS.Include(x => x.SUPPLIER).Where(s => s.IsDeleted == false).ToList();
                        ViewBag.SourceList = new SelectList(sources);
                        ViewBag.SupplierList = new SelectList(suppliers.Where(x => x.IsDeleted == false), "ID", "Name");
                    }
                }
                return View();
            }
        }
        public async Task<ActionResult> Download()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SAMPLE_FILES contract = await db.SAMPLE_FILES.Where(s => s.FileName.CompareTo("shartnoma.xlsx") == 0).FirstOrDefaultAsync();
                if (contract != null)
                    return File(contract.File, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

                return View();
            }
        }
        public async Task<ActionResult> DownloadSteel()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SAMPLE_FILES contract = await db.SAMPLE_FILES.Where(s => s.FileName.CompareTo("shartnoma-steel.xlsx") == 0).FirstOrDefaultAsync();
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
                            var colCount = 15; // ma'lumot uchun qo'shilgan columnlarni olmasligi uchun qo'lda yozildi

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
                        if (CheckForExistenceOfSuppliers(dataTable))
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
                                        contractNo = row["ContractNo"].ToString();
                                        supplierName = row["Supplier Name"].ToString();
                                        partNo = row["Part Number"].ToString();

                                        SUPPLIER supplier = db.SUPPLIERS.Where(s => s.Name.CompareTo(supplierName) == 0 && s.IsDeleted == false).FirstOrDefault();
                                        PART part = db.PARTS.Where(p => p.PNo.CompareTo(partNo) == 0 && p.IsDeleted == false).FirstOrDefault();
                                        P_CONTRACTS contract = db.P_CONTRACTS.Where(pc => pc.ContractNo.CompareTo(contractNo) == 0 && pc.SupplierID == supplier.ID && pc.IsDeleted == false).FirstOrDefault();

                                        if (contract != null)
                                        {
                                            if (part != null)
                                            {
                                                P_CONTRACT_PARTS contractPart = db.P_CONTRACT_PARTS.Where(pcp => pcp.PartID == part.ID && pcp.ContractID == contract.ID).FirstOrDefault();
                                                if (contractPart != null)
                                                    ViewBag.ExistingRecordsCount = 1;
                                            }
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
                                ViewBag.Message = "Ushbu shartnomalar faylida kiritilgan ehtiyot qismlar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Ehtiyot qismlar bazasiga kiritib keyin qayta urining.";
                                return View("UploadWithExcel");
                            }
                        }
                        else
                        {
                            var message = "";
                            foreach (var word in missingSuppliers)
                            {
                                message += word + ", ";
                            }
                            ViewBag.Message = "Ushbu shartnomalar faylda kiritilgan ta'minotchilar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Ta'minotchilar bazasiga kiritib keyin qayta urining.";
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
        public ActionResult UploadWithExcelSteel()
        {
            ViewBag.IsFileUploaded = false;
            return View();
        }
        [HttpPost]
        public ActionResult UploadWithExcelSteel(HttpPostedFileBase file)
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
                            var colCount = 20; // ma'lumot uchun qo'shilgan columnlarni olmasligi uchun qo'lda yozildi

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
                        if (CheckForExistenceOfSuppliers(dataTable))
                        {
                            ViewBag.DataTable = dataTable;
                            ViewBag.DataTableModel = JsonConvert.SerializeObject(dataTable);
                            ViewBag.IsFileUploaded = true;
                            if (!CheckForExistenceOfSteelCoils(dataTable))
                            {
                                var message = "";
                                foreach (var word in missingParts)
                                {
                                    message += word + ", ";
                                }
                                ViewBag.Message = "Ushbu shartnomalar faylida kiritilgan ehtiyot qismlar: " + message + " shartnoma: " + contractNo + " da mavjud emas.";
                                return View("UploadWithExcelSteel");
                            }
                        }
                        else
                        {
                            var message = "";
                            foreach (var word in missingSuppliers)
                            {
                                message += word + ", ";
                            }
                            ViewBag.Message = "Ushbu shartnomalar faylda kiritilgan ta'minotchilar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Ta'minotchilar bazasiga kiritib keyin qayta urining.";
                            return View("UploadWithExcelSteel");
                        }
                    }
                    catch (Exception ex)
                    {
                        ViewBag.Message = $"Faylni yuklashda quyidagicha muammo tug'ildi: {ex.Message}";
                        return View("UploadWithExcelSteel");
                    }
                }
                else
                {
                    ViewBag.Message = "Format noto'g'ri. Faqat .xlsx fayllarni yuklash mumkin.";
                    return View("UploadWithExcelSteel");
                }
            }
            else
            {
                ViewBag.Message = "Fayl bo'm-bo'sh yoki yuklanmadi!";
                return View("UploadWithExcelSteel");
            }
            return View("UploadWithExcelSteel");
        }
        private bool CheckForExistenceOfSteelCoils(DataTable dataTable)
        {
            bool flag = false;
            string partPNo = "";
            if (dataTable != null)
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    foreach (DataRow row in dataTable.Rows)
                    {

                        supplierName = row["Supplier Name"].ToString();
                        contractNo = row["ContractNo"].ToString();
                        partPNo = row["Steel Grade"].ToString() + " " + Convert.ToDouble(row["Thickness"].ToString()) + "x" + Convert.ToDouble(row["Width"].ToString());

                        SUPPLIER supplier = db.SUPPLIERS.Where(s => s.Name.CompareTo(supplierName) == 0 && s.IsDeleted == false).FirstOrDefault();
                        P_CONTRACTS contract = db.P_CONTRACTS.Where(pc => pc.ContractNo.CompareTo(contractNo) == 0 && pc.SupplierID == supplier.ID && pc.IsDeleted == false).FirstOrDefault();
                        PART part = db.PARTS.Where(p => p.PNo.CompareTo(partPNo) == 0 && p.IsDeleted == false).FirstOrDefault();
                        if (part != null)
                        {
                            P_CONTRACT_PARTS contractPart = db.P_CONTRACT_PARTS.Where(pcp => pcp.PartID == part.ID && pcp.ContractID == contract.ID).FirstOrDefault();
                            if (contractPart != null)
                                flag = true;
                        }
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
        private bool CheckForExistenceOfParts(DataTable dataTable)
        {
            bool flag = false;
            if (dataTable != null)
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        string partPNo = row["Part Number"].ToString();
                        if (db.PARTS.Where(p => p.PNo.CompareTo(partPNo) == 0).Any())
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
        private bool CheckForExistenceOfSuppliers(DataTable dataTable)
        {
            bool flag = false;
            if (dataTable != null)
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        string supplierName = row["Supplier Name"].ToString();
                        if (db.SUPPLIERS.Where(s => s.Name.CompareTo(supplierName) == 0).Any())
                            flag = true;
                        else
                        {
                            if (!missingSuppliers.Contains(supplierName))
                                missingSuppliers.Add(supplierName);
                        }
                    }
                    if (missingSuppliers.Count > 0)
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
        public ActionResult ClearDataTableSteel()
        {
            // Clear the DataTable and related ViewBag properties
            ViewBag.DataTable = null;
            ViewBag.DataTableModel = null;
            ViewBag.IsFileUploaded = false;
            ViewBag.Message = "Jadval ma'lumotlari o'chirib yuborildi.";
            // Return the UploadWithExcel view
            return View("UploadWithExcelSteel");
        }
        [HttpPost]
        public ActionResult Save(string dataTableModel)
        {
            if (!string.IsNullOrEmpty(dataTableModel))
            {
                var tableModel = JsonConvert.DeserializeObject<System.Data.DataTable>(dataTableModel);

                try
                {
                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        foreach (DataRow row in tableModel.Rows)
                        {
                            contractNo = row["ContractNo"].ToString();
                            supplierName = row["Supplier Name"].ToString();
                            partNo = row["Part Number"].ToString();

                            SUPPLIER supplier = db.SUPPLIERS.Where(s => s.Name.CompareTo(supplierName) == 0 && s.IsDeleted == false).FirstOrDefault();
                            PART part = db.PARTS.Where(p => p.PNo.CompareTo(partNo) == 0 && p.IsDeleted == false).FirstOrDefault();

                            P_CONTRACTS contract = db.P_CONTRACTS.Where(pc => pc.ContractNo.CompareTo(contractNo) == 0 && pc.IsDeleted == false).FirstOrDefault();

                            if (contract == null)
                            {
                                P_CONTRACTS new_contract = new P_CONTRACTS();
                                new_contract.ContractNo = contractNo;
                                new_contract.SupplierID = supplier.ID;
                                new_contract.IssuedDate = DateTime.Parse(row["IssuedDate"].ToString());
                                new_contract.DueDate = DateTime.Parse(row["DueDate"].ToString());
                                new_contract.Incoterms = row["Incoterms"].ToString();
                                new_contract.PaymentTerms = row["PaymentTerms"].ToString();
                                new_contract.CurrencyID = db.CURRENCIES.Where(x => x.Shortname.CompareTo(row["Currency"].ToString()) == 0).Select(x => x.ID).FirstOrDefault();
                                new_contract.CompanyID = Convert.ToInt32(ConfigurationManager.AppSettings["companyID"]);
                                new_contract.IsDeleted = false;

                                db.P_CONTRACTS.Add(new_contract);
                                db.SaveChanges();

                                LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{new_contract.ContractNo} - shartnomani Excel orqali kiritdi");

                                //var unitname = db.UNITS.Where(x => x.ShortName.CompareTo(row["Unit"].ToString()) == 0).FirstOrDefault();
                                P_CONTRACT_PARTS contractPart = db.P_CONTRACT_PARTS.Where(pcp => pcp.ContractID == new_contract.ID && pcp.PartID == part.ID).FirstOrDefault();
                                string unitName = row["Unit"].ToString();
                                if (contractPart == null)
                                {
                                    P_CONTRACT_PARTS new_contractPart = new P_CONTRACT_PARTS();
                                    new_contractPart.PartID = part.ID;
                                    new_contractPart.ContractID = new_contract.ID;
                                    new_contractPart.Price = Convert.ToDouble(row["Price"].ToString());
                                    UNIT unit = db.UNITS.Where(u => u.ShortName.CompareTo(unitName) == 0).FirstOrDefault();
                                    if (unit != null)
                                        new_contractPart.UnitID = unit.ID;
                                    else
                                        new_contractPart.UnitID = 1;
                                    new_contractPart.MOQ = Convert.ToDouble(row["MOQ"].ToString());
                                    new_contractPart.Quantity = Convert.ToDouble(row["Amount"].ToString());
                                    new_contractPart.ActivePart = true;

                                    db.P_CONTRACT_PARTS.Add(new_contractPart);

                                    db.SaveChanges();
                                    LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{new_contractPart.PART.PNo} - ushbu qismni Excel orqali {new_contract.ContractNo} shartnomasiga biriktirdi");
                                }
                            }
                            else
                            {
                                P_CONTRACT_PARTS contractPart = db.P_CONTRACT_PARTS.Where(pcp => pcp.ContractID == contract.ID && pcp.PartID == part.ID).FirstOrDefault();
                                string unitName = row["Unit"].ToString();
                                if (contractPart == null)
                                {
                                    P_CONTRACT_PARTS new_contractPart = new P_CONTRACT_PARTS();
                                    new_contractPart.PartID = part.ID;
                                    new_contractPart.ContractID = contract.ID;
                                    new_contractPart.Price = Convert.ToDouble(row["Price"].ToString());
                                    UNIT unit = db.UNITS.Where(u => u.ShortName.CompareTo(unitName) == 0).FirstOrDefault();
                                    if (unit != null)
                                        new_contractPart.UNIT = unit;
                                    else
                                        new_contractPart.UnitID = 1;
                                    new_contractPart.Quantity = Convert.ToDouble(row["Amount"].ToString());
                                    new_contractPart.MOQ = Convert.ToDouble(row["MOQ"].ToString());
                                    new_contractPart.ActivePart = true;

                                    db.P_CONTRACT_PARTS.Add(new_contractPart);
                                    int noOfRowUpdated = db.Database.ExecuteSqlCommand("UPDATE P_CONTRACT_PARTS SET ActivePart =" + 0 + " WHERE ContractID !=" + contract.ID + " AND PartID =" + part.ID + "");

                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{new_contractPart.PART.PNo} ushbu qismni Excel orqali {contract.ContractNo} shartnomasiga biriktirdi");
                                }
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
        [HttpPost]
        public ActionResult SaveSteel(string dataTableModel)
        {
            if (!string.IsNullOrEmpty(dataTableModel))
            {
                var tableModel = JsonConvert.DeserializeObject<System.Data.DataTable>(dataTableModel);
                string currency = "";
                try
                {
                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        foreach (DataRow row in tableModel.Rows)
                        {
                            contractNo = row["ContractNo"].ToString();
                            supplierName = row["Supplier Name"].ToString();
                            //partNo = row["Part Number"].ToString();
                            currency = row["Currency"].ToString();


                            SUPPLIER supplier = db.SUPPLIERS.Where(s => s.Name.CompareTo(supplierName) == 0 && s.IsDeleted == false).FirstOrDefault();
                            PART part = GetPartAsPerSteelInformationExcel(row["Steel Grade"].ToString(), row["Coating type"].ToString(), row["Thickness"].ToString(), row["Width"].ToString(), row["HSCODE"].ToString());

                            P_CONTRACTS contract = db.P_CONTRACTS.Where(pc => pc.ContractNo.CompareTo(contractNo) == 0 && pc.IsDeleted == false).FirstOrDefault();

                            if (contract == null)
                            {
                                P_CONTRACTS new_contract = new P_CONTRACTS();
                                new_contract.ContractNo = contractNo;
                                new_contract.SupplierID = supplier.ID;
                                new_contract.IssuedDate = DateTime.Parse(row["IssuedDate"].ToString());
                                new_contract.DueDate = DateTime.Parse(row["DueDate"].ToString());
                                new_contract.Incoterms = row["Incoterms"].ToString();
                                new_contract.PaymentTerms = row["PaymentTerms"].ToString();
                                new_contract.CurrencyID = db.CURRENCIES.Where(x => x.Shortname.CompareTo(currency) == 0).Select(x => x.ID).FirstOrDefault(); ;
                                new_contract.CompanyID = Convert.ToInt32(ConfigurationManager.AppSettings["companyID"]);
                                new_contract.IsDeleted = false;

                                db.P_CONTRACTS.Add(new_contract);
                                db.SaveChanges();

                                LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{new_contract.ContractNo} - shartnomani Excel orqali kiritdi");


                                P_CONTRACT_PARTS contractPart = db.P_CONTRACT_PARTS.Where(pcp => pcp.ContractID == new_contract.ID && pcp.PartID == part.ID).FirstOrDefault();
                                string unitName = row["Unit"].ToString();
                                if (contractPart == null)
                                {
                                    P_CONTRACT_PARTS new_contractPart = new P_CONTRACT_PARTS();
                                    new_contractPart.PartID = part.ID;
                                    new_contractPart.ContractID = new_contract.ID;
                                    new_contractPart.Price = Convert.ToDouble(row["Price"].ToString());
                                    UNIT unit = db.UNITS.Where(u => u.ShortName.CompareTo(unitName) == 0).FirstOrDefault();
                                    if (unit != null)
                                        new_contractPart.UnitID = unit.ID;
                                    else
                                        new_contractPart.UnitID = 1;
                                    if (!string.IsNullOrEmpty(row["MOQ"].ToString()))
                                        new_contractPart.MOQ = Convert.ToDouble(row["MOQ"].ToString());
                                    new_contractPart.Quantity = Convert.ToDouble(row["Volume"].ToString());
                                    new_contractPart.HSCodeID = part.HSCodeID;
                                    new_contractPart.ActivePart = true;

                                    db.P_CONTRACT_PARTS.Add(new_contractPart);

                                    db.SaveChanges();
                                    LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{part.PNo} - ushbu qismni Excel orqali {new_contract.ContractNo} shartnomasiga biriktirdi");
                                }
                            }
                            else
                            {
                                P_CONTRACT_PARTS contractPart = db.P_CONTRACT_PARTS.Where(pcp => pcp.ContractID == contract.ID && pcp.PartID == part.ID).FirstOrDefault();
                                string unitName = row["Unit"].ToString();
                                if (contractPart == null)
                                {
                                    P_CONTRACT_PARTS new_contractPart = new P_CONTRACT_PARTS();
                                    new_contractPart.PartID = part.ID;
                                    new_contractPart.ContractID = contract.ID;
                                    new_contractPart.Price = Convert.ToDouble(row["Price"].ToString());
                                    UNIT unit = db.UNITS.Where(u => u.ShortName.CompareTo(unitName) == 0).FirstOrDefault();
                                    if (unit != null)
                                        new_contractPart.UNIT = unit;
                                    else
                                        new_contractPart.UnitID = 1;
                                    new_contractPart.Quantity = Convert.ToDouble(row["Volume"].ToString());
                                    if (!string.IsNullOrEmpty(row["MOQ"].ToString()))
                                        new_contractPart.MOQ = Convert.ToDouble(row["MOQ"].ToString());
                                    new_contractPart.ActivePart = true;
                                    new_contractPart.HSCodeID = part.HSCodeID;

                                    db.P_CONTRACT_PARTS.Add(new_contractPart);
                                    int noOfRowUpdated = db.Database.ExecuteSqlCommand("UPDATE P_CONTRACT_PARTS SET ActivePart =" + 0 + " WHERE ContractID !=" + contract.ID + " AND PartID =" + part.ID + "");

                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{part.PNo} ushbu qismni Excel orqali {contract.ContractNo} shartnomasiga biriktirdi");
                                }
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
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Supplier = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                ViewBag.partList = new SelectList(db.PARTS.Where(c => c.IsDeleted == false).ToList(), "ID", "PNo");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PContractViewModel model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                //if (ModelState.IsValid)
                //{
                try
                {
                    var newContract = new P_CONTRACTS()
                    {
                        ContractNo = model.ContractNo,
                        IssuedDate = model.IssuedDate,
                        CompanyID = Convert.ToInt32(ConfigurationManager.AppSettings["companyID"]),
                        SupplierID = model.SupplierID,
                        CurrencyID = model.CurrencyID,
                        Amount = model.Amount,
                        Incoterms = model.Incoterms,
                        PaymentTerms = model.PaymentTerms,
                        DueDate = model.DueDate,
                        IsDeleted = false,
                        IDN = model.IDN,
                    };
                    db.P_CONTRACTS.Add(newContract);
                    db.SaveChanges();
                    LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{newContract.ContractNo} - shartnomasini tizimga kiritdi.");

                    int newContractID = newContract.ID;

                    foreach (var part in model.Parts)
                    {
                        var newPart = new P_CONTRACT_PARTS
                        {
                            ContractID = newContractID,
                            PartID = part.PartID,
                            UnitID = part.UnitID,
                            Price = double.Parse(part.Price.ToString(), System.Globalization.CultureInfo.InvariantCulture),
                            Quantity = double.Parse(part.Quantity.ToString(), System.Globalization.CultureInfo.InvariantCulture),
                            ActivePart = true,
                            MOQ = double.Parse(part.MOQ.ToString(), System.Globalization.CultureInfo.InvariantCulture)
                        };
                        db.P_CONTRACT_PARTS.Add(newPart);
                        PART paart = db.PARTS.Where(p => p.ID == part.PartID).FirstOrDefault();
                        if (paart != null)
                            LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{newContract.ContractNo} - shartnomasiga {paart.PNo} ni biriktirdi.");
                    }
                    db.SaveChanges();

                    newContract.Amount = (double)db.P_CONTRACT_PARTS.Where(x => x.ContractID == newContract.ID).Sum(x => x.Amount);
                    db.Entry(newContract).State = EntityState.Modified;
                    db.SaveChanges();

                    if (model.File != null && model.File.ContentLength > 0)
                    {
                        if (model.File.ContentLength < 5242880)
                        {
                            if (Path.GetExtension(model.File.FileName).ToLower() == ".pdf")
                            {
                                P_CONTRACT_DOCS contractDoc = new P_CONTRACT_DOCS();
                                byte[] fileData = new byte[model.File.InputStream.Length];
                                model.File.InputStream.Read(fileData, 0, fileData.Length);

                                contractDoc.ContractID = newContract.ID;
                                contractDoc.Doc = fileData;

                                db.P_CONTRACT_DOCS.Add(contractDoc);
                                db.SaveChanges();

                                LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{contractDoc.P_CONTRACTS.ContractNo} - uchun shartnoma uchun fayl biriktirdi");
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
                    else
                    {
                        ModelState.AddModelError("", "No file uploaded. Please try again.");
                    }

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error: {ex.Message}");
                }
                //}

                ViewBag.Supplier = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                ViewBag.partList = new SelectList(db.PARTS.Where(c => c.IsDeleted == false).ToList(), "ID", "PNo");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                return View(model);
            }
        }
        public ActionResult CreateSteel()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Supplier = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                ViewBag.partList = new SelectList(db.PARTS.Where(c => c.IsDeleted == false).ToList(), "ID", "PNo");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                ViewBag.hscodes = new SelectList(db.HSCODES.Where(h => h.ID >= 7647 && h.ID <= 8521).ToList(), "ID", "HSCODE1");

                var steelCoils = db.STEEL_COILS.ToList();

                var distinctMarka = steelCoils.Select(s => s.Marka).Distinct();
                var distinctStandart = steelCoils.Select(s => s.Standart).Distinct();
                var distinctCoating = steelCoils.Select(s => s.Coating).Distinct();
                var distinctThickness = steelCoils.Select(s => s.Thickness).Distinct();

                ViewBag.steelMarka = new SelectList(distinctMarka);
                ViewBag.steelStandart = new SelectList(distinctStandart);
                ViewBag.steelCoating = new SelectList(distinctCoating);
                ViewBag.steelThickness = new SelectList(distinctThickness);

                return View();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateSteel(PContractSViewModel model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                //if (ModelState.IsValid)
                //{
                try
                {
                    var newContract = new P_CONTRACTS()
                    {
                        ContractNo = model.ContractNo,
                        IssuedDate = model.IssuedDate,
                        CompanyID = Convert.ToInt32(ConfigurationManager.AppSettings["companyID"]),
                        SupplierID = model.SupplierID,
                        CurrencyID = model.CurrencyID,
                        Amount = model.Amount,
                        Incoterms = model.Incoterms,
                        PaymentTerms = model.PaymentTerms,
                        DueDate = model.DueDate,
                        IsDeleted = false,
                        IDN = model.IDN,
                    };
                    db.P_CONTRACTS.Add(newContract);
                    db.SaveChanges();
                    LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{newContract.ContractNo} - shartnomasini tizimga kiritdi.");

                    int newContractID = newContract.ID;

                    foreach (var part in model.Parts)
                    {
                        var newPart = new P_CONTRACT_PARTS
                        {
                            ContractID = newContractID,
                            PartID = GetPartAsPerSteelInformation(part),
                            UnitID = part.UnitID,
                            Price = double.Parse(part.Price.ToString(), System.Globalization.CultureInfo.InvariantCulture),
                            Quantity = double.Parse(part.Quantity.ToString(), System.Globalization.CultureInfo.InvariantCulture),
                            ActivePart = true
                            //HSCodeID = part.HSCodeID
                        };
                        db.P_CONTRACT_PARTS.Add(newPart);
                        db.SaveChanges();
                        PART paart = db.PARTS.Where(p => p.ID == newPart.PartID).FirstOrDefault();
                        if (paart != null)
                            LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{newContract.ContractNo} - shartnomasiga {paart.PNo} ni biriktirdi.");
                    }

                    newContract.Amount = (double)db.P_CONTRACT_PARTS.Where(x => x.ContractID == newContract.ID).Sum(x => x.Amount);
                    db.Entry(newContract).State = EntityState.Modified;
                    db.SaveChanges();

                    if (model.File != null && model.File.ContentLength > 0)
                    {
                        if (model.File.ContentLength < 5242880)
                        {
                            if (Path.GetExtension(model.File.FileName).ToLower() == ".pdf")
                            {
                                P_CONTRACT_DOCS contractDoc = new P_CONTRACT_DOCS();
                                byte[] fileData = new byte[model.File.InputStream.Length];
                                model.File.InputStream.Read(fileData, 0, fileData.Length);

                                contractDoc.ContractID = newContract.ID;
                                contractDoc.Doc = fileData;

                                db.P_CONTRACT_DOCS.Add(contractDoc);
                                db.SaveChanges();

                                LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{contractDoc.P_CONTRACTS.ContractNo} - uchun shartnoma uchun fayl biriktirdi");
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
                    else
                    {
                        ModelState.AddModelError("", "No file uploaded. Please try again.");
                    }

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error: {ex.Message}");
                }
                //}

                ViewBag.Supplier = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                ViewBag.partList = new SelectList(db.PARTS.Where(c => c.IsDeleted == false).ToList(), "ID", "PNo");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                return View(model);
            }
        }
        private int GetPartAsPerSteelInformation(PContractSPart steel)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PART part = db.PARTS.Where(p => p.Marka == steel.Marka && p.Coating == steel.Coating && p.Standart == steel.Standart && p.Gauge == steel.Thickness && p.PWidth == steel.Width).FirstOrDefault();
                if (part != null)
                    return part.ID;
                else
                {
                    PART newPart = new PART();
                    newPart.Marka = steel.Marka;
                    newPart.Coating = steel.Coating;
                    newPart.Standart = steel.Standart;
                    newPart.Gauge = steel.Thickness;
                    newPart.PWidth = steel.Width;
                    newPart.PNo = steel.Standart + " " + steel.Thickness + "x" + steel.Width;
                    newPart.PName = "steel coil " + steel.Standart + " " + steel.Thickness + "x" + steel.Width;
                    newPart.PWeight = 0;
                    newPart.PLength = 0;
                    newPart.PHeight = 0;
                    newPart.Pitch = 0;
                    newPart.IsDeleted = false;
                    newPart.IsInHouse = false;
                    newPart.HSCodeID = steel.HsCodeID;
                    newPart.UnitID = steel.UnitID;

                    db.PARTS.Add(newPart);
                    db.SaveChanges();

                    return newPart.ID;
                }
            }
        }
        private PART GetPartAsPerSteelInformationExcel(string v1, string v2, string v3, string v4, string v5)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                double gauge = Convert.ToDouble(v3);
                double width = Convert.ToDouble(v4);
                string hscode = v5.Replace(",", "");
                PART part = db.PARTS.Where(p => p.Standart.CompareTo(v1) == 0 && p.Coating.CompareTo(v2) == 0 && p.Gauge == gauge && p.PWidth == width).FirstOrDefault();
                if (part != null)
                    return part;
                else
                {
                    PART newPart = new PART();
                    newPart.Marka = "";
                    newPart.Coating = v2;
                    newPart.Standart = v1;
                    newPart.Gauge = Convert.ToDouble(v3);
                    newPart.PWidth = Convert.ToDouble(v4);
                    newPart.PNo = v1 + " " + Convert.ToDouble(v3) + "x" + Convert.ToDouble(v4);
                    newPart.PName = "steel coil " + v1 + " " + Convert.ToDouble(v3) + "x" + Convert.ToDouble(v4);
                    newPart.PWeight = 0;
                    newPart.PLength = 0;
                    newPart.PHeight = 0;
                    newPart.Pitch = 0;
                    newPart.IsDeleted = false;
                    newPart.IsInHouse = false;
                    newPart.HSCodeID = db.HSCODES.Where(x => x.HSCODE1.CompareTo(hscode) == 0).Select(x => x.ID).FirstOrDefault();
                    newPart.UnitID = 3;

                    db.PARTS.Add(newPart);
                    db.SaveChanges();

                    return newPart;
                }
            }
        }
        public ActionResult DownloadDoc(int? contractID)
        {
            if (contractID == null)
            {
                return Json(new { success = false, message = "Shartnoma ID ko'rsatilmagan." }, JsonRequestBehavior.AllowGet);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractDoc = db.P_CONTRACT_DOCS
                                .Include(x => x.P_CONTRACTS)
                                .FirstOrDefault(pi => pi.ContractID == contractID);
                if (contractDoc != null)
                {
                    var contractNo = contractDoc.P_CONTRACTS.ContractNo;
                    // Returning the file for download via AJAX can be handled differently, so return success here
                    return File(contractDoc.Doc, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", contractNo + "_ContractDoc.pdf");
                }
                else
                {
                    return Json(new { success = false, message = "Fayl yuklanmagan." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        public ActionResult Details(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.P_CONTRACTS
                                  .Include(p => p.SUPPLIER)
                                  .Include(p => p.CURRENCy)
                                  .FirstOrDefault(p => p.ID == ID && p.IsDeleted == false);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                var partList = db.P_CONTRACT_PARTS
                                  .Include(pc => pc.PART)
                                  .Include(pc => pc.UNIT)
                                  .Where(pc => pc.ContractID == contract.ID)
                                  .ToList();

                ViewBag.PartList = partList;
                var partImage = db.P_CONTRACT_DOCS.FirstOrDefault(pi => pi.ContractID == ID);
                if (partImage != null)
                {
                    ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(partImage.Doc);
                }
                return View(contract);
            }
        }
        public ActionResult GetPartsBySupplier(int supplierId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var supplier = db.SUPPLIERS.Where(x => x.IsDeleted == false && x.ID == supplierId).FirstOrDefault();
                var parts = db.PARTS
                    .Where(p => p.Type.CompareTo(supplier.Type) == 0 && p.IsDeleted == false)
                    .Select(p => new { p.ID, p.PNo })
                    .ToList();

                return Json(parts, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult Edit(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.P_CONTRACTS
                    .Include(c => c.SUPPLIER)
                    .Include(p => p.CURRENCy)
                    .Include(c => c.P_CONTRACT_PARTS.Select(p => p.PART))
                    .FirstOrDefault(c => c.ID == ID && c.IsDeleted == false);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                var model = new PContractViewModel
                {
                    ID = contract.ID,
                    ContractNo = contract.ContractNo,
                    IssuedDate = contract.IssuedDate,
                    DueDate = contract.DueDate,
                    SupplierID = contract.SupplierID,
                    Incoterms = contract.Incoterms,
                    PaymentTerms = contract.PaymentTerms,
                    CurrencyID = (int)contract.CurrencyID,
                    Amount = (double)contract.Amount,
                    IDN = contract.IDN,
                    Parts = contract.P_CONTRACT_PARTS.Select(x => new ViewModels.PContractPart
                    {
                        ID = x.ID,
                        Part = x.PART,
                        PartID = (int)x.PartID,
                        UnitID = (int)x.UnitID,
                        Price = (double)x.Price,
                        Quantity = (double)x.Quantity,
                        MOQ = (double)x.MOQ
                    }).ToList()
                };
                ViewBag.Supplier = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name", contract.SupplierID);
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(PContractViewModel contract)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                    P_CONTRACTS contractToUpdate = db.P_CONTRACTS.Find(contract.ID);
                    if (contractToUpdate != null)
                    {
                        contractToUpdate.ContractNo = contract.ContractNo;
                        contractToUpdate.SupplierID = contract.SupplierID;
                        contractToUpdate.IssuedDate = contract.IssuedDate;
                        contractToUpdate.DueDate = contract.DueDate;
                        contractToUpdate.CurrencyID = contract.CurrencyID;
                        contractToUpdate.Incoterms = contract.Incoterms;
                        contractToUpdate.PaymentTerms = contract.PaymentTerms;
                        contractToUpdate.IDN = contract.IDN;
                        contractToUpdate.IsDeleted = false;

                        double summ = 0;
                        foreach (var part in contract.Parts)
                        {
                            var existingPart = db.P_CONTRACT_PARTS.Include(pcp => pcp.PART).Where(pcp => pcp.PartID == part.ID).FirstOrDefault();
                            if (existingPart != null)
                            {
                                existingPart.Price = part.Price;
                                existingPart.UnitID = part.UnitID;
                                existingPart.Quantity = part.Quantity;
                                existingPart.MOQ = part.MOQ;
                                existingPart.Amount = part.Price * part.Quantity;
                                summ += ((double)existingPart.Quantity * (double)existingPart.Price);
                                db.Entry(existingPart).State = EntityState.Modified;
                                try
                                {
                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{contractToUpdate.ContractNo} - shartnomasida {existingPart.PART.PNo} - li ehtiyot qismni tahrirladi");
                                }
                                catch (RetryLimitExceededException)
                                {
                                    ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                                }
                            }
                        }

                        //Shartnoma miqdori o'zgargani uchun yozildi
                        //contractToUpdate.Amount = db.P_CONTRACT_PARTS.Where(x => x.ContractID == contractToUpdate.ID).Sum(x => x.Amount);
                        contractToUpdate.Amount = summ;
                        db.SaveChanges();

                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{contractToUpdate.ContractNo} - shartnomasini tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    return View(contractToUpdate);
                }
            }
            return View(contract);
        }
        public ActionResult EditPart(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractPart = db.P_CONTRACT_PARTS
                                    .Include(p => p.P_CONTRACTS)
                                    .Include(p => p.PART)
                                    .FirstOrDefault(p => p.ID == ID);
                if (contractPart == null)
                {
                    return HttpNotFound();
                }
                var allParts = db.PARTS
                                .Include(p => p.P_CONTRACT_PARTS)
                                .Include(p => p.UNIT)
                                .Where(p => p.IsDeleted == false)
                                .ToList();

                ViewBag.PartList = new SelectList(allParts, "ID", "PNo");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

                return View(contractPart);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPart(P_CONTRACT_PARTS contractPart)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    P_CONTRACT_PARTS contractPartToUpdate = db.P_CONTRACT_PARTS.Include(pcp => pcp.PART).Include(pcp => pcp.P_CONTRACTS).Where(pcp => pcp.PartID == contractPart.ID).FirstOrDefault();
                    if (contractPartToUpdate != null)
                    {
                        contractPartToUpdate.PartID = contractPart.PartID;
                        contractPartToUpdate.Price = contractPart.Price;
                        contractPartToUpdate.Quantity = contractPart.Quantity;
                        contractPartToUpdate.UnitID = contractPart.UnitID;
                        contractPartToUpdate.MOQ = contractPart.MOQ;
                        contractPartToUpdate.ActivePart = contractPart.ActivePart;
                        //contractPartToUpdate.Amount = contractPart.Quantity * contractPart.Price; SQL o'zi chiqarib beradi
                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{contractPartToUpdate.P_CONTRACTS.ContractNo} - shartnomasida {contractPartToUpdate.PART.PNo} - ehtiyot qismni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }

                    }
                    return RedirectToAction("Edit");
                }
            }
            return View();
        }
        public ActionResult Delete(int? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.P_CONTRACTS.Find(Id);
                if (contract == null)
                {
                    return HttpNotFound();
                }
                else
                    ViewBag.partList = db.P_CONTRACT_PARTS
                        .Include(pc => pc.PART)
                        .Include(pc => pc.UNIT)
                        .Where(pc => pc.ContractID == contract.ID).ToList();

                db.Entry(contract).Reference(i => i.SUPPLIER).Load();
                db.Entry(contract).Reference(i => i.CURRENCy).Load();
                return View(contract);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int? ID, FormCollection gfs)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    P_CONTRACTS contractToDelete = db.P_CONTRACTS.Find(ID);
                    if (contractToDelete != null)
                        try
                        {
                            contractToDelete.IsDeleted = true;
                            db.SaveChanges();
                            LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{contractToDelete.ContractNo} - shartnomani o'chirib yubordi");

                            db.Entry(contractToDelete).State = EntityState.Modified;
                            var contractParts = db.P_CONTRACT_PARTS.Where(pc => pc.ContractID == contractToDelete.ID).ToList();

                            foreach (var contractPart in contractParts)
                            {
                                db.P_CONTRACT_PARTS.Remove(contractPart);
                                //LogHelper.LogToDatabase(User.Identity.Name, "PContractController", $"{contractPart.PART.PNo} - PContractPartni o'chirdi");
                            }
                            db.SaveChanges();
                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    else
                        ModelState.AddModelError("", "Bunday shartnoma topilmadi.");
                }
            }
            return View();
        }
        public ActionResult DeletePart(int? id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                P_CONTRACT_PARTS contractPartToDelete = db.P_CONTRACT_PARTS.Include(pcp => pcp.PART).Include(pcp => pcp.P_CONTRACTS).Where(pcp => pcp.PartID == id).FirstOrDefault();
                if (ModelState.IsValid)
                {
                    if (contractPartToDelete != null)
                    {
                        try
                        {
                            db.P_CONTRACT_PARTS.Remove(contractPartToDelete);
                            db.SaveChanges();

                            //Shartnoma miqdori o'zgargani uchun yozildi
                            var contractToUpdate = db.PURCHASING_CONTRACTS.Find(contractPartToDelete.ContractID);
                            contractToUpdate.Amount = (double)db.P_CONTRACT_PARTS.Where(x => x.ContractID == contractToUpdate.ID).Sum(x => x.Amount);

                            db.SaveChanges();
                            LogHelper.LogToDatabase(User.Identity.Name, "Shartnomalar", $"{contractPartToDelete.P_CONTRACTS.ContractNo} - shartnomasida {contractPartToDelete.PART.PNo} - ehtiyot qismni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "ContractPart not found.");
                    }
                }
                return View(contractPartToDelete);
            }
        }
    }
}