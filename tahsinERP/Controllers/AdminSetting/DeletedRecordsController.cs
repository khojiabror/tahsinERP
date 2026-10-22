using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// O'chirilgan ma'lumotlarni tiklash - Admin panel
    /// User role permission orqali kirish tartibi qo'llaniladi
    /// </summary>
    public class DeletedRecordsController : Controller
    {
        // Modullar ro'yxati: Key -> DisplayName (resx kaliti; ko'rsatishda L.T bilan tarjima qilinadi)
        private static readonly List<DeletedModuleInfo> Modules = new List<DeletedModuleInfo>
        {
            new DeletedModuleInfo { Key = "F_WAYBILLS", DisplayName = "Core_DelMod_FWaybills" },
            new DeletedModuleInfo { Key = "F_WAYBILL_PAYMENTS", DisplayName = "Core_DelMod_FWaybillPayments" },
            new DeletedModuleInfo { Key = "F_CONTRACTS", DisplayName = "Core_DelMod_FContracts" },
            new DeletedModuleInfo { Key = "FORWARDER", DisplayName = "Core_DelMod_Forwarder" },
            new DeletedModuleInfo { Key = "CUSTOMER", DisplayName = "Core_DelMod_Customer" },
            new DeletedModuleInfo { Key = "CUSTOMER_BASE", DisplayName = "Core_DelMod_CustomerBase" },
            new DeletedModuleInfo { Key = "S_ORDERS", DisplayName = "Core_DelMod_SOrders" },
            new DeletedModuleInfo { Key = "S_CONTRACTS", DisplayName = "Core_DelMod_SContracts" },
            new DeletedModuleInfo { Key = "SUPPLIER", DisplayName = "Core_DelMod_Supplier" },
            new DeletedModuleInfo { Key = "SUPPLIER_BASE", DisplayName = "Core_DelMod_SupplierBase" },
            new DeletedModuleInfo { Key = "P_ORDERS", DisplayName = "Core_DelMod_POrders" },
            new DeletedModuleInfo { Key = "P_CONTRACTS", DisplayName = "Core_DelMod_PContracts" },
            new DeletedModuleInfo { Key = "P_INVOICES", DisplayName = "Core_DelMod_PInvoices" },
            new DeletedModuleInfo { Key = "P_REQUISITIONS", DisplayName = "Core_DelMod_PRequisitions" },
            new DeletedModuleInfo { Key = "PRODUCT", DisplayName = "Core_DelMod_Product" },
            new DeletedModuleInfo { Key = "PRODUCTPLAN", DisplayName = "Core_DelMod_Productplan" },
            new DeletedModuleInfo { Key = "PRODUCTIONPROCESS", DisplayName = "Core_DelMod_Productionprocess" },
            new DeletedModuleInfo { Key = "DAMAGED_PRODUCTS", DisplayName = "Core_DelMod_DamagedProducts" },
            new DeletedModuleInfo { Key = "DEFECT_TYPES", DisplayName = "Core_DelMod_DefectTypes" },
            new DeletedModuleInfo { Key = "SHOP", DisplayName = "Core_DelMod_Shop" },
            new DeletedModuleInfo { Key = "TANNARX", DisplayName = "Core_DelMod_Tannarx" },
            new DeletedModuleInfo { Key = "FIN_TRANSACTIONS", DisplayName = "Core_DelMod_FinTransactions" },
            new DeletedModuleInfo { Key = "FIN_ACCOUNTS", DisplayName = "Core_DelMod_FinAccounts" },
            new DeletedModuleInfo { Key = "FIN_CATEGORIES", DisplayName = "Core_DelMod_FinCategories" },
            new DeletedModuleInfo { Key = "FIN_BALANCE_CATEGORIES", DisplayName = "Core_DelMod_FinBalanceCategories" },
            new DeletedModuleInfo { Key = "FIN_PL_PLANS", DisplayName = "Core_DelMod_FinPlPlans" },
            new DeletedModuleInfo { Key = "FIN_PL_ITEMS", DisplayName = "Core_DelMod_FinPlItems" },
            new DeletedModuleInfo { Key = "SHAREHOLDER", DisplayName = "Core_DelMod_Shareholder" },
            new DeletedModuleInfo { Key = "UNIT", DisplayName = "Core_DelMod_Unit" },
            new DeletedModuleInfo { Key = "CURRENCY", DisplayName = "Core_DelMod_Currency" },
            new DeletedModuleInfo { Key = "WAREHOUSE", DisplayName = "Core_DelMod_Warehouse" },
            new DeletedModuleInfo { Key = "PRODUCT_CATEGORIES", DisplayName = "Core_DelMod_ProductCategories" },
            new DeletedModuleInfo { Key = "USER", DisplayName = "Core_DelMod_User" },
            new DeletedModuleInfo { Key = "ROLE", DisplayName = "Core_DelMod_Role" },
            new DeletedModuleInfo { Key = "APP_TASKS", DisplayName = "Core_DelMod_AppTasks" },
            new DeletedModuleInfo { Key = "APP_TASK_PROJECTS", DisplayName = "Core_DelMod_AppTaskProjects" },
            new DeletedModuleInfo { Key = "APP_TASK_STATUS_REF", DisplayName = "Core_DelMod_AppTaskStatusRef" },
            new DeletedModuleInfo { Key = "APP_TASK_PRIORITY_REF", DisplayName = "Core_DelMod_AppTaskPriorityRef" },
            new DeletedModuleInfo { Key = "STD_COMPANY_STANDARDS", DisplayName = "Core_DelMod_StdCompanyStandards" },
            new DeletedModuleInfo { Key = "JOB_DESC", DisplayName = "Core_DelMod_JobDesc" },
            new DeletedModuleInfo { Key = "ADM_COMPANY_ORDERS", DisplayName = "Core_DelMod_AdmCompanyOrders" },
            new DeletedModuleInfo { Key = "COM_OFFICIAL_LETTERS", DisplayName = "Core_DelMod_ComOfficialLetters" },
            new DeletedModuleInfo { Key = "NORMVERSION", DisplayName = "Core_DelMod_Normversion" },
            new DeletedModuleInfo { Key = "WASTE", DisplayName = "Core_DelMod_Waste" },
            new DeletedModuleInfo { Key = "HR_EMPLOYEES", DisplayName = "Core_DelMod_HrEmployees" },
            new DeletedModuleInfo { Key = "HR_POSITIONS", DisplayName = "Core_DelMod_HrPositions" },
            new DeletedModuleInfo { Key = "HR_ORG_UNITS", DisplayName = "Core_DelMod_HrOrgUnits" },
            new DeletedModuleInfo { Key = "HR_SALARY_GRADES", DisplayName = "Core_DelMod_HrSalaryGrades" },
            new DeletedModuleInfo { Key = "HR_STAFFING_PLAN", DisplayName = "Core_DelMod_HrStaffingPlan" },
            new DeletedModuleInfo { Key = "HR_PIECE_RATE_TARIFFS", DisplayName = "Core_DelMod_HrPieceRateTariffs" },
            new DeletedModuleInfo { Key = "F_TRANSPORT_TYPES", DisplayName = "Core_DelMod_FTransportTypes" },
            new DeletedModuleInfo { Key = "EXPENSES", DisplayName = "Core_DelMod_Expenses" },
            new DeletedModuleInfo { Key = "INCOMES", DisplayName = "Core_DelMod_Incomes" },
            new DeletedModuleInfo { Key = "PRODUCT_PRODUCTION", DisplayName = "Core_DelMod_ProductProduction" }
        };

        // Olish so'rovi: DeletedRecords - Admin panel permission (AdminPanel moduli orqali kirish)
        public ActionResult Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "AdminPanel"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.Modules = Modules
                .Select(m => new DeletedModuleInfo { Key = m.Key, DisplayName = L.T(m.DisplayName) })
                .OrderBy(m => m.DisplayName).ToList();
            return View();
        }

        // Olish so'rovi: DeletedRecords/GetDeletedData?moduleKey=...&searchTerm=...
        [HttpGet]
        public async Task<JsonResult> GetDeletedData(string moduleKey, string searchTerm)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "AdminPanel"))
            {
                return Json(new { error = L.T("Error_NotAuthorized") }, JsonRequestBehavior.AllowGet);
            }

            if (string.IsNullOrEmpty(moduleKey))
            {
                return Json(new { data = new List<DeletedRecordItem>(), columns = new string[0] }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var result = await GetDeletedRecordsAsync(moduleKey, searchTerm);
                // Ustun nomlari (o'zbekcha ichki nom) joriy tilga — data kalitlari ham shu nom bilan
                foreach (var item in result.Items)
                {
                    if (item.Columns != null)
                        item.Columns = item.Columns.ToDictionary(kv => ColumnTitle(kv.Key), kv => kv.Value);
                }
                var columns = result.ColumnNames.Select(ColumnTitle).ToArray();
                return Json(new { data = result.Items, columns }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST so'rovi: DeletedRecords/Recover
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Recover(string moduleKey, int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "AdminPanel"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrEmpty(moduleKey))
            {
                TempData["Error"] = L.T("Core_ModuleNotFound");
                return RedirectToAction("Index");
            }

            // Ombor qoldig'i / moliyaviy proводkasi bor hujjatlarni oddiy IsDeleted=false bilan tiklab
            // bo'lmaydi — qoldiq va moliya qayta hisoblanmaydi (CORE-B02). Bunday hujjat qaytadan kiritiladi.
            if (NonRecoverableModules.Contains(moduleKey))
            {
                TempData["Error"] = L.T("Core_DelNotRecoverable");
                return RedirectToAction("Index", new { selectedModule = moduleKey });
            }

            try
            {
                var success = await RecoverRecordAsync(moduleKey, id);
                if (success)
                {
                    TempData["Success"] = L.T("Core_DelRecovered");
                    LogHelper.LogToDatabase(User.Identity.Name, "DeletedRecords", $"Tiklandi: {moduleKey} ID={id}");
                }
                else
                {
                    TempData["Error"] = L.T("Core_DelRecoverError");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = L.T("Core_ErrorShort", ex.Message);
            }

            return RedirectToAction("Index", new { selectedModule = moduleKey });
        }

        // Ichki ustun nomi (o'zbekcha) -> resx kaliti
        private static readonly Dictionary<string, string> ColumnKeys = new Dictionary<string, string>
        {
            ["Asosiy"] = "Core_DelCol_Main",
            ["Ariza No"] = "Core_DelCol_RequisitionNo",
            ["Buyruq No"] = "Core_DelCol_DecreeNo",
            ["Buyurtma No"] = "Core_DelCol_OrderNo",
            ["Hujjat No"] = "Core_DelCol_DocNo",
            ["Invoys No"] = "Core_DelCol_InvoiceNo",
            ["Ism"] = "Core_DelCol_PersonName",
            ["Izoh"] = "Comment",
            ["Kod"] = "Code",
            ["Kompaniya"] = "Core_DelCol_Company",
            ["Login"] = "Username",
            ["Mahsulot kodi"] = "Core_DelCol_ProductCode",
            ["Manzil"] = "Address",
            ["Maqsad"] = "Core_DelCol_Purpose",
            ["Mavzu"] = "Core_DelCol_Topic",
            ["Maxsulot"] = "Core_DelCol_Product",
            ["Nomi"] = "Name",
            ["Nukson turi"] = "Core_DelCol_DefectType",
            ["Qayerdan"] = "Core_DelCol_From",
            ["Qayerga"] = "Core_DelCol_To",
            ["Qisqa"] = "Core_DelCol_Short",
            ["Qisqacha"] = "Core_DelCol_ShortName",
            ["Rol nomi"] = "Core_RoleName",
            ["Sana"] = "Date",
            ["Sarlavha"] = "Core_DelCol_Title",
            ["Shartnoma No"] = "Core_DelCol_ContractNo",
            ["Summa"] = "Amount",
            ["Tabel No"] = "Core_DelCol_TabNo",
            ["Tavsif"] = "Description",
            ["Telefon"] = "Phone",
            ["To'liq"] = "Core_DelCol_Full",
            ["Transport turi"] = "Core_DelCol_TransportType",
            ["Yo'l varaqasi"] = "Core_DelCol_Waybill",
        };

        private static string ColumnTitle(string name)
        {
            string key;
            return name != null && ColumnKeys.TryGetValue(name, out key) ? L.T(key) : name;
        }

        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedRecordsAsync(string moduleKey, string searchTerm)
        {
            using (var db = new DBTHSNEntities())
            {
                if (!Registry.TryGetValue(moduleKey ?? "", out var def))
                    return (new List<DeletedRecordItem>(), new[] { "ID", "Asosiy" });
                return await def.Load(this, db, searchTerm);
            }
        }

        #region GetDeleted helpers
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedWaybills(DBTHSNEntities db, string search)
        {
            var query = db.F_WAYBILLS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(w => (w.WaybillNo != null && w.WaybillNo.Contains(search)) || (w.FromLocation != null && w.FromLocation.Contains(search)) || (w.ToLocation != null && w.ToLocation.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem
            {
                Id = x.ID,
                DisplayText = x.WaybillNo ?? x.ID.ToString(),
                Columns = new Dictionary<string, object> { ["Yo'l varaqasi"] = x.WaybillNo, ["Sana"] = x.IssueDate, ["Qayerdan"] = x.FromLocation, ["Qayerga"] = x.ToLocation }
            }).ToList();
            return (items, new[] { "Yo'l varaqasi", "Sana", "Qayerdan", "Qayerga" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedCustomers(DBTHSNEntities db, string search)
        {
            var query = db.CUSTOMERS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => (c.Name != null && c.Name.Contains(search)) || (c.Address != null && c.Address.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name, ["Manzil"] = x.Address, ["Telefon"] = x.Telephone } }).ToList();
            return (items, new[] { "Nomi", "Manzil", "Telefon" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedCustomerBase(DBTHSNEntities db, string search)
        {
            var query = db.CUSTOMER_BASE.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => (c.CompanyName != null && c.CompanyName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.CompanyName ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Kompaniya"] = x.CompanyName } }).ToList();
            return (items, new[] { "Kompaniya" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedSuppliers(DBTHSNEntities db, string search)
        {
            var query = db.SUPPLIERS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => (s.Name != null && s.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name, ["Manzil"] = x.Address } }).ToList();
            return (items, new[] { "Nomi", "Manzil" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedSupplierBase(DBTHSNEntities db, string search)
        {
            var query = db.SUPPLIER_BASE.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => (s.CompanyName != null && s.CompanyName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.CompanyName ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Kompaniya"] = x.CompanyName } }).ToList();
            return (items, new[] { "Kompaniya" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedSOrders(DBTHSNEntities db, string search)
        {
            var query = db.S_ORDERS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => (o.OrderNo != null && o.OrderNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.OrderNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Buyurtma No"] = x.OrderNo, ["Sana"] = x.IssuedDate } }).ToList();
            return (items, new[] { "Buyurtma No", "Sana" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedSContracts(DBTHSNEntities db, string search)
        {
            var query = db.S_CONTRACTS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => (c.ContractNo != null && c.ContractNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ContractNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Shartnoma No"] = x.ContractNo } }).ToList();
            return (items, new[] { "Shartnoma No" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedPOrders(DBTHSNEntities db, string search)
        {
            var query = db.P_ORDERS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => (o.OrderNo != null && o.OrderNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.OrderNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Buyurtma No"] = x.OrderNo } }).ToList();
            return (items, new[] { "Buyurtma No" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedPContracts(DBTHSNEntities db, string search)
        {
            var query = db.P_CONTRACTS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => (c.ContractNo != null && c.ContractNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ContractNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Shartnoma No"] = x.ContractNo } }).ToList();
            return (items, new[] { "Shartnoma No" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedPInvoices(DBTHSNEntities db, string search)
        {
            var query = db.P_INVOICES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(i => (i.InvoiceNo != null && i.InvoiceNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.InvoiceNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Invoys No"] = x.InvoiceNo } }).ToList();
            return (items, new[] { "Invoys No" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedPRequisitions(DBTHSNEntities db, string search)
        {
            var query = db.P_REQUISITIONS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r => (r.RequisitionNo != null && r.RequisitionNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.RequisitionNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Ariza No"] = x.RequisitionNo } }).ToList();
            return (items, new[] { "Ariza No" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedProducts(DBTHSNEntities db, string search)
        {
            var query = db.PRODUCTS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => (p.PNo != null && p.PNo.Contains(search)) || (p.Name != null && p.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = (x.PNo ?? "") + " " + (x.Name ?? ""), Columns = new Dictionary<string, object> { ["Mahsulot kodi"] = x.PNo, ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Mahsulot kodi", "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedForwarders(DBTHSNEntities db, string search)
        {
            var query = db.FORWARDERS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(f => (f.ForwarderName != null && f.ForwarderName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ForwarderName ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.ForwarderName } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedFContracts(DBTHSNEntities db, string search)
        {
            var query = db.F_CONTRACTS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => (c.ContractNo != null && c.ContractNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ContractNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Shartnoma No"] = x.ContractNo } }).ToList();
            return (items, new[] { "Shartnoma No" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedWaybillPayments(DBTHSNEntities db, string search)
        {
            var query = db.F_WAYBILL_PAYMENTS.Where(x => x.IsDeleted == true).Include(x => x.F_WAYBILLS);
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.F_WAYBILLS?.WaybillNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Yo'l varaqasi"] = x.F_WAYBILLS?.WaybillNo, ["Summa"] = x.Payment_Amount } }).ToList();
            return (items, new[] { "Yo'l varaqasi", "Summa" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedWarehouses(DBTHSNEntities db, string search)
        {
            var query = db.WAREHOUSES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(w => (w.WHName != null && w.WHName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.WHName ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.WHName } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedUnits(DBTHSNEntities db, string search)
        {
            var query = db.UNITS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => (u.UnitName != null && u.UnitName.Contains(search)) || (u.ShortName != null && u.ShortName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.UnitName ?? x.ShortName ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.UnitName, ["Qisqacha"] = x.ShortName } }).ToList();
            return (items, new[] { "Nomi", "Qisqacha" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedCurrencies(DBTHSNEntities db, string search)
        {
            var query = db.CURRENCIES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => (c.Shortname != null && c.Shortname.Contains(search)) || (c.Longname != null && c.Longname.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Shortname ?? x.Longname ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Qisqa"] = x.Shortname, ["To'liq"] = x.Longname } }).ToList();
            return (items, new[] { "Qisqa", "To'liq" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedProductCategories(DBTHSNEntities db, string search)
        {
            var query = db.PRODUCT_CATEGORIES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => (c.Name != null && c.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedFinAccounts(DBTHSNEntities db, string search)
        {
            var query = db.FIN_ACCOUNTS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a => (a.Code != null && a.Code.Contains(search)) || (a.Name != null && a.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Code ?? x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Kod"] = x.Code, ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Kod", "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedFinCategories(DBTHSNEntities db, string search)
        {
            var query = db.FIN_CATEGORIES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => (c.Name != null && c.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedFinTransactions(DBTHSNEntities db, string search)
        {
            var query = db.FIN_TRANSACTIONS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => (t.Purpose != null && t.Purpose.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Purpose ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Maqsad"] = x.Purpose, ["Summa"] = x.Amount, ["Sana"] = x.TxnDate } }).ToList();
            return (items, new[] { "Maqsad", "Summa", "Sana" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedFinBalanceCategories(DBTHSNEntities db, string search)
        {
            var query = db.FIN_BALANCE_CATEGORIES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => (c.Name != null && c.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedFinPlPlans(DBTHSNEntities db, string search)
        {
            var query = db.FIN_PL_PLANS.Where(x => x.IsDeleted == true);
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ID.ToString(), Columns = new Dictionary<string, object> { ["ID"] = x.ID } }).ToList();
            return (items, new[] { "ID" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedFinPlItems(DBTHSNEntities db, string search)
        {
            var query = db.FIN_PL_ITEMS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(i => (i.Code != null && i.Code.Contains(search)) || (i.Name != null && i.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Code ?? x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Kod"] = x.Code, ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Kod", "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedShareholders(DBTHSNEntities db, string search)
        {
            var query = db.SHAREHOLDERS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => (s.FullName != null && s.FullName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.FullName ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Ism"] = x.FullName } }).ToList();
            return (items, new[] { "Ism" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedProductPlans(DBTHSNEntities db, string search)
        {
            var query = db.PRODUCTPLANS.Where(x => x.IsDeleted == true).Include(p => p.PRODUCT);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => (p.PRODUCT != null && p.PRODUCT.PNo != null && p.PRODUCT.PNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.PRODUCT?.PNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Maxsulot"] = x.PRODUCT?.PNo } }).ToList();
            return (items, new[] { "Maxsulot" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedProductionProcesses(DBTHSNEntities db, string search)
        {
            var query = db.PRODUCTIONPROCESSES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => (p.ProcessName != null && p.ProcessName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ProcessName ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.ProcessName } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedTannarx(DBTHSNEntities db, string search)
        {
            var query = db.TANNARXes.Where(x => x.IsDeleted == true).Include(t => t.PRODUCT);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => (t.PRODUCT != null && t.PRODUCT.PNo != null && t.PRODUCT.PNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.PRODUCT?.PNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Maxsulot"] = x.PRODUCT?.PNo } }).ToList();
            return (items, new[] { "Maxsulot" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedShops(DBTHSNEntities db, string search)
        {
            var query = db.SHOPS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => (s.ShopName != null && s.ShopName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ShopName ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.ShopName } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedDamagedProducts(DBTHSNEntities db, string search)
        {
            var query = db.DAMAGED_PRODUCTS.Where(x => x.IsDeleted == true);
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ID.ToString(), Columns = new Dictionary<string, object> { ["ID"] = x.ID } }).ToList();
            return (items, new[] { "ID" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedDefectTypes(DBTHSNEntities db, string search)
        {
            var query = db.DEFECT_TYPES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(d => (d.DefectType != null && d.DefectType.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.DefectType ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nukson turi"] = x.DefectType } }).ToList();
            return (items, new[] { "Nukson turi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedUsers(DBTHSNEntities db, string search)
        {
            var query = db.USERS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => (u.Uname != null && u.Uname.Contains(search)) || (u.FullName != null && u.FullName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.FullName ?? x.Uname ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Login"] = x.Uname, ["Ism"] = x.FullName } }).ToList();
            return (items, new[] { "Login", "Ism" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedRoles(DBTHSNEntities db, string search)
        {
            var query = db.ROLES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r => (r.RName != null && r.RName.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.RName ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Rol nomi"] = x.RName } }).ToList();
            return (items, new[] { "Rol nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedAppTasks(DBTHSNEntities db, string search)
        {
            var query = db.APP_TASKS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => (t.Title != null && t.Title.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = (int)x.ID, DisplayText = x.Title ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Sarlavha"] = x.Title } }).ToList();
            return (items, new[] { "Sarlavha" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedAppTaskProjects(DBTHSNEntities db, string search)
        {
            var query = db.APP_TASK_PROJECTS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => (p.Name != null && p.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedAppTaskStatusRef(DBTHSNEntities db, string search)
        {
            var query = db.APP_TASK_STATUS_REF.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => (s.Name != null && s.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedAppTaskPriorityRef(DBTHSNEntities db, string search)
        {
            var query = db.APP_TASK_PRIORITY_REF.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => (p.Name != null && p.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedStdCompanyStandards(DBTHSNEntities db, string search)
        {
            var query = db.STD_COMPANY_STANDARDS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => (s.Notes != null && s.Notes.Contains(search)) || (s.StandardCode != null && s.StandardCode.Contains(search)));
            var list = await query.OrderByDescending(x => x.DocumentID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = (int)x.DocumentID, DisplayText = x.StandardCode ?? x.Notes ?? x.DocumentID.ToString(), Columns = new Dictionary<string, object> { ["Kod"] = x.StandardCode, ["Izoh"] = x.Notes } }).ToList();
            return (items, new[] { "Kod", "Izoh" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedJobDesc(DBTHSNEntities db, string search)
        {
            var query = db.JOB_DESC.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(j => (j.JobTitle != null && j.JobTitle.Contains(search)));
            var list = await query.OrderByDescending(x => x.DocumentID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = (int)x.DocumentID, DisplayText = x.JobTitle ?? x.DocumentID.ToString(), Columns = new Dictionary<string, object> { ["Sarlavha"] = x.JobTitle } }).ToList();
            return (items, new[] { "Sarlavha" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedAdmCompanyOrders(DBTHSNEntities db, string search)
        {
            var query = db.ADM_COMPANY_ORDERS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => (o.OrderNo != null && o.OrderNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.DocumentID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = (int)x.DocumentID, DisplayText = x.OrderNo ?? x.DocumentID.ToString(), Columns = new Dictionary<string, object> { ["Buyruq No"] = x.OrderNo } }).ToList();
            return (items, new[] { "Buyruq No" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedComOfficialLetters(DBTHSNEntities db, string search)
        {
            var query = db.COM_OFFICIAL_LETTERS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(l => (l.Topic != null && l.Topic.Contains(search)));
            var list = await query.OrderByDescending(x => x.DocumentID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = (int)x.DocumentID, DisplayText = x.Topic ?? x.DocumentID.ToString(), Columns = new Dictionary<string, object> { ["Mavzu"] = x.Topic } }).ToList();
            return (items, new[] { "Mavzu" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedNormVersions(DBTHSNEntities db, string search)
        {
            var query = db.NORMVERSIONS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(n => (n.Comment != null && n.Comment.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Comment ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Izoh"] = x.Comment } }).ToList();
            return (items, new[] { "Izoh" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedWastes(DBTHSNEntities db, string search)
        {
            var query = db.WASTES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(w => (w.Description != null && w.Description.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Description ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Tavsif"] = x.Description } }).ToList();
            return (items, new[] { "Tavsif" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedHrEmployees(DBTHSNEntities db, string search)
        {
            var query = db.HR_EMPLOYEES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e => (e.FullName != null && e.FullName.Contains(search)) || (e.TabNo != null && e.TabNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.FullName ?? x.TabNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Ism"] = x.FullName, ["Tabel No"] = x.TabNo } }).ToList();
            return (items, new[] { "Ism", "Tabel No" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedHrPositions(DBTHSNEntities db, string search)
        {
            var query = db.HR_POSITIONS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => (p.Name != null && p.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedHrOrgUnits(DBTHSNEntities db, string search)
        {
            var query = db.HR_ORG_UNITS.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => (o.Name != null && o.Name.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.Name ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Nomi"] = x.Name } }).ToList();
            return (items, new[] { "Nomi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedHrSalaryGrades(DBTHSNEntities db, string search)
        {
            var query = db.HR_SALARY_GRADES.Where(x => x.IsDeleted == true);
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ID.ToString(), Columns = new Dictionary<string, object> { ["ID"] = x.ID } }).ToList();
            return (items, new[] { "ID" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedHrStaffingPlan(DBTHSNEntities db, string search)
        {
            var query = db.HR_STAFFING_PLAN.Where(x => x.IsDeleted == true);
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ID.ToString(), Columns = new Dictionary<string, object> { ["ID"] = x.ID } }).ToList();
            return (items, new[] { "ID" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedHrPieceRateTariffs(DBTHSNEntities db, string search)
        {
            var query = db.HR_PIECE_RATE_TARIFFS.Where(x => x.IsDeleted == true);
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.ID.ToString(), Columns = new Dictionary<string, object> { ["ID"] = x.ID } }).ToList();
            return (items, new[] { "ID" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedFTransportTypes(DBTHSNEntities db, string search)
        {
            var query = db.F_TRANSPORT_TYPES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => (t.TransportType != null && t.TransportType.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.TransportType ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Transport turi"] = x.TransportType } }).ToList();
            return (items, new[] { "Transport turi" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedExpenses(DBTHSNEntities db, string search)
        {
            var query = db.EXPENSES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e => (e.DocNo != null && e.DocNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.DocNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Hujjat No"] = x.DocNo, ["Sana"] = x.IssueDateTime } }).ToList();
            return (items, new[] { "Hujjat No", "Sana" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedIncomes(DBTHSNEntities db, string search)
        {
            var query = db.INCOMES.Where(x => x.IsDeleted == true);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(i => (i.DocNo != null && i.DocNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.DocNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Hujjat No"] = x.DocNo, ["Sana"] = x.IssueDateTime } }).ToList();
            return (items, new[] { "Hujjat No", "Sana" });
        }
        private async Task<(List<DeletedRecordItem> Items, string[] ColumnNames)> GetDeletedProductProduction(DBTHSNEntities db, string search)
        {
            var query = db.PRODUCT_PRODUCTION.Where(x => x.IsDeleted == true).Include(p => p.PRODUCT);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => (p.PRODUCT != null && p.PRODUCT.PNo != null && p.PRODUCT.PNo.Contains(search)));
            var list = await query.OrderByDescending(x => x.ID).Take(500).ToListAsync();
            var items = list.Select(x => new DeletedRecordItem { Id = x.ID, DisplayText = x.PRODUCT?.PNo ?? x.ID.ToString(), Columns = new Dictionary<string, object> { ["Maxsulot"] = x.PRODUCT?.PNo, ["Sana"] = x.Date } }).ToList();
            return (items, new[] { "Maxsulot", "Sana" });
        }
        #endregion

        // Modul kaliti -> entity turi + o'chirilganlarni yuklovchi (CORE dublikat: avval ikki katta switch —
        // ro'yxat uchun va tiklash uchun — har yangi jadvalda ikki joyga qo'shilardi)
        private sealed class DeletedModuleDef
        {
            public DeletedModuleDef(Type entityType, Func<DeletedRecordsController, DBTHSNEntities, string, Task<(List<DeletedRecordItem> Items, string[] ColumnNames)>> load, bool longKey = false)
            {
                EntityType = entityType;
                Load = load;
                LongKey = longKey;
            }

            /// <summary>Kalit bigint (Tasks/Docs jadvallari) — FindAsync to'g'ri turdagi qiymat talab qiladi.</summary>
            public bool LongKey { get; }

            public Type EntityType { get; }
            public Func<DeletedRecordsController, DBTHSNEntities, string, Task<(List<DeletedRecordItem> Items, string[] ColumnNames)>> Load { get; }
        }

        private static readonly Dictionary<string, DeletedModuleDef> Registry = new Dictionary<string, DeletedModuleDef>
        {
            ["F_WAYBILLS"] = new DeletedModuleDef(typeof(F_WAYBILLS), (c, db, q) => c.GetDeletedWaybills(db, q)),
            ["CUSTOMER"] = new DeletedModuleDef(typeof(CUSTOMER), (c, db, q) => c.GetDeletedCustomers(db, q)),
            ["CUSTOMER_BASE"] = new DeletedModuleDef(typeof(CUSTOMER_BASE), (c, db, q) => c.GetDeletedCustomerBase(db, q)),
            ["SUPPLIER"] = new DeletedModuleDef(typeof(SUPPLIER), (c, db, q) => c.GetDeletedSuppliers(db, q)),
            ["SUPPLIER_BASE"] = new DeletedModuleDef(typeof(SUPPLIER_BASE), (c, db, q) => c.GetDeletedSupplierBase(db, q)),
            ["S_ORDERS"] = new DeletedModuleDef(typeof(S_ORDERS), (c, db, q) => c.GetDeletedSOrders(db, q)),
            ["S_CONTRACTS"] = new DeletedModuleDef(typeof(S_CONTRACTS), (c, db, q) => c.GetDeletedSContracts(db, q)),
            ["P_ORDERS"] = new DeletedModuleDef(typeof(P_ORDERS), (c, db, q) => c.GetDeletedPOrders(db, q)),
            ["P_CONTRACTS"] = new DeletedModuleDef(typeof(P_CONTRACTS), (c, db, q) => c.GetDeletedPContracts(db, q)),
            ["P_INVOICES"] = new DeletedModuleDef(typeof(P_INVOICES), (c, db, q) => c.GetDeletedPInvoices(db, q)),
            ["P_REQUISITIONS"] = new DeletedModuleDef(typeof(P_REQUISITIONS), (c, db, q) => c.GetDeletedPRequisitions(db, q)),
            ["PRODUCT"] = new DeletedModuleDef(typeof(PRODUCT), (c, db, q) => c.GetDeletedProducts(db, q)),
            ["FORWARDER"] = new DeletedModuleDef(typeof(FORWARDER), (c, db, q) => c.GetDeletedForwarders(db, q)),
            ["F_CONTRACTS"] = new DeletedModuleDef(typeof(F_CONTRACTS), (c, db, q) => c.GetDeletedFContracts(db, q)),
            ["F_WAYBILL_PAYMENTS"] = new DeletedModuleDef(typeof(F_WAYBILL_PAYMENTS), (c, db, q) => c.GetDeletedWaybillPayments(db, q)),
            ["WAREHOUSE"] = new DeletedModuleDef(typeof(Warehouse), (c, db, q) => c.GetDeletedWarehouses(db, q)),
            ["UNIT"] = new DeletedModuleDef(typeof(UNIT), (c, db, q) => c.GetDeletedUnits(db, q)),
            ["CURRENCY"] = new DeletedModuleDef(typeof(CURRENCy), (c, db, q) => c.GetDeletedCurrencies(db, q)),
            ["PRODUCT_CATEGORIES"] = new DeletedModuleDef(typeof(PRODUCT_CATEGORIES), (c, db, q) => c.GetDeletedProductCategories(db, q)),
            ["FIN_ACCOUNTS"] = new DeletedModuleDef(typeof(FIN_ACCOUNTS), (c, db, q) => c.GetDeletedFinAccounts(db, q)),
            ["FIN_CATEGORIES"] = new DeletedModuleDef(typeof(FIN_CATEGORIES), (c, db, q) => c.GetDeletedFinCategories(db, q)),
            ["FIN_TRANSACTIONS"] = new DeletedModuleDef(typeof(FIN_TRANSACTIONS), (c, db, q) => c.GetDeletedFinTransactions(db, q)),
            ["FIN_BALANCE_CATEGORIES"] = new DeletedModuleDef(typeof(FIN_BALANCE_CATEGORIES), (c, db, q) => c.GetDeletedFinBalanceCategories(db, q)),
            ["FIN_PL_PLANS"] = new DeletedModuleDef(typeof(FIN_PL_PLANS), (c, db, q) => c.GetDeletedFinPlPlans(db, q)),
            ["FIN_PL_ITEMS"] = new DeletedModuleDef(typeof(FIN_PL_ITEMS), (c, db, q) => c.GetDeletedFinPlItems(db, q)),
            ["SHAREHOLDER"] = new DeletedModuleDef(typeof(SHAREHOLDER), (c, db, q) => c.GetDeletedShareholders(db, q)),
            ["PRODUCTPLAN"] = new DeletedModuleDef(typeof(PRODUCTPLAN), (c, db, q) => c.GetDeletedProductPlans(db, q)),
            ["PRODUCTIONPROCESS"] = new DeletedModuleDef(typeof(PRODUCTIONPROCESS), (c, db, q) => c.GetDeletedProductionProcesses(db, q)),
            ["TANNARX"] = new DeletedModuleDef(typeof(TANNARX), (c, db, q) => c.GetDeletedTannarx(db, q)),
            ["SHOP"] = new DeletedModuleDef(typeof(SHOP), (c, db, q) => c.GetDeletedShops(db, q)),
            ["DAMAGED_PRODUCTS"] = new DeletedModuleDef(typeof(DAMAGED_PRODUCTS), (c, db, q) => c.GetDeletedDamagedProducts(db, q)),
            ["DEFECT_TYPES"] = new DeletedModuleDef(typeof(DEFECT_TYPES), (c, db, q) => c.GetDeletedDefectTypes(db, q)),
            ["USER"] = new DeletedModuleDef(typeof(USER), (c, db, q) => c.GetDeletedUsers(db, q)),
            ["ROLE"] = new DeletedModuleDef(typeof(ROLE), (c, db, q) => c.GetDeletedRoles(db, q)),
            ["APP_TASKS"] = new DeletedModuleDef(typeof(APP_TASKS), (c, db, q) => c.GetDeletedAppTasks(db, q), longKey: true),
            ["APP_TASK_PROJECTS"] = new DeletedModuleDef(typeof(APP_TASK_PROJECTS), (c, db, q) => c.GetDeletedAppTaskProjects(db, q)),
            ["APP_TASK_STATUS_REF"] = new DeletedModuleDef(typeof(APP_TASK_STATUS_REF), (c, db, q) => c.GetDeletedAppTaskStatusRef(db, q)),
            ["APP_TASK_PRIORITY_REF"] = new DeletedModuleDef(typeof(APP_TASK_PRIORITY_REF), (c, db, q) => c.GetDeletedAppTaskPriorityRef(db, q)),
            ["STD_COMPANY_STANDARDS"] = new DeletedModuleDef(typeof(STD_COMPANY_STANDARDS), (c, db, q) => c.GetDeletedStdCompanyStandards(db, q), longKey: true),
            ["JOB_DESC"] = new DeletedModuleDef(typeof(JOB_DESC), (c, db, q) => c.GetDeletedJobDesc(db, q), longKey: true),
            ["ADM_COMPANY_ORDERS"] = new DeletedModuleDef(typeof(ADM_COMPANY_ORDERS), (c, db, q) => c.GetDeletedAdmCompanyOrders(db, q), longKey: true),
            ["COM_OFFICIAL_LETTERS"] = new DeletedModuleDef(typeof(COM_OFFICIAL_LETTERS), (c, db, q) => c.GetDeletedComOfficialLetters(db, q), longKey: true),
            ["NORMVERSION"] = new DeletedModuleDef(typeof(NORMVERSION), (c, db, q) => c.GetDeletedNormVersions(db, q)),
            ["WASTE"] = new DeletedModuleDef(typeof(WASTE), (c, db, q) => c.GetDeletedWastes(db, q)),
            ["HR_EMPLOYEES"] = new DeletedModuleDef(typeof(HR_EMPLOYEES), (c, db, q) => c.GetDeletedHrEmployees(db, q)),
            ["HR_POSITIONS"] = new DeletedModuleDef(typeof(HR_POSITIONS), (c, db, q) => c.GetDeletedHrPositions(db, q)),
            ["HR_ORG_UNITS"] = new DeletedModuleDef(typeof(HR_ORG_UNITS), (c, db, q) => c.GetDeletedHrOrgUnits(db, q)),
            ["HR_SALARY_GRADES"] = new DeletedModuleDef(typeof(HR_SALARY_GRADES), (c, db, q) => c.GetDeletedHrSalaryGrades(db, q)),
            ["HR_STAFFING_PLAN"] = new DeletedModuleDef(typeof(HR_STAFFING_PLAN), (c, db, q) => c.GetDeletedHrStaffingPlan(db, q)),
            ["HR_PIECE_RATE_TARIFFS"] = new DeletedModuleDef(typeof(HR_PIECE_RATE_TARIFFS), (c, db, q) => c.GetDeletedHrPieceRateTariffs(db, q)),
            ["F_TRANSPORT_TYPES"] = new DeletedModuleDef(typeof(F_TRANSPORT_TYPES), (c, db, q) => c.GetDeletedFTransportTypes(db, q)),
            ["EXPENSES"] = new DeletedModuleDef(typeof(EXPENS), (c, db, q) => c.GetDeletedExpenses(db, q)),
            ["INCOMES"] = new DeletedModuleDef(typeof(INCOME), (c, db, q) => c.GetDeletedIncomes(db, q)),
            ["PRODUCT_PRODUCTION"] = new DeletedModuleDef(typeof(PRODUCT_PRODUCTION), (c, db, q) => c.GetDeletedProductProduction(db, q)),
        };

        private static readonly HashSet<string> NonRecoverableModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EXPENSES", "INCOMES", "PRODUCT_PRODUCTION", "FIN_TRANSACTIONS", "P_INVOICES", "F_WAYBILL_PAYMENTS"
        };

        private async Task<bool> RecoverRecordAsync(string moduleKey, int id)
        {
            using (var db = new DBTHSNEntities())
            {
                return await RecoverEntity(db, moduleKey, id);
            }
        }

        private async Task<bool> RecoverEntity(DBTHSNEntities db, string moduleKey, int id)
        {
            dynamic entity = null;
            if (!Registry.TryGetValue(moduleKey ?? "", out var def))
                return false;
            entity = await db.Set(def.EntityType).FindAsync(def.LongKey ? (object)(long)id : id);
            if (entity == null) return false;

            // Foydalanuvchi: shu nom bilan yangi faol foydalanuvchi bo'lsa — tiklanmaydi
            if (moduleKey == "USER")
            {
                string uname = entity.Uname;
                int uid = entity.ID;
                if (await db.USERS.AnyAsync(u => u.Uname == uname && u.ID != uid && u.IsDeleted != true))
                    return false;
            }

            // Faqat IsDeleted yoziladi (avval butun qator Modified qilinardi, CORE-B02)
            try { entity.IsDeleted = false; } catch { return false; }
            await db.SaveChangesAsync();
            return true;
        }
    }
}
