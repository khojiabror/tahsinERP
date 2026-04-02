using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Soft delete (IsDeleted) — standart yondashuv. Nullable FKlarni tozalash — yangi operatsiyalarda
    /// o‘chirilgan yozuvga tasodifiy bog‘lanishni kamaytiradi. Majburiy FKlar tarixiy hujjatlar uchun saqlanadi.
    /// </summary>
    public static class SoftDeleteReferenceService
    {
        public static async Task AfterProductSoftDeletedAsync(DBTHSNEntities db, int productId)
        {
            var bomLines = await db.BOM_LINES.Where(x => x.ComponentID == productId).ToListAsync();
            foreach (var row in bomLines)
                row.ComponentID = null;

            var bomHeaders = await db.BOM_HEADERS.Where(x => x.ProductID == productId).ToListAsync();
            foreach (var row in bomHeaders)
                row.ProductID = null;

            var norms = await db.NORMVERSIONS.Where(x => x.ProductID == productId).ToListAsync();
            foreach (var row in norms)
                row.ProductID = null;

            var tsLines = await db.HR_TIMESHEET_LINES.Where(x => x.ProductID == productId).ToListAsync();
            foreach (var row in tsLines)
                row.ProductID = null;

            var ttnkLines = await db.SALES_TTNK_LINES.Where(x => x.ProductID == productId).ToListAsync();
            foreach (var row in ttnkLines)
                row.ProductID = null;

            var sched = await db.SORDER_DELIVERY_SCHEDULE.Where(x => x.ProductID == productId).ToListAsync();
            foreach (var row in sched)
                row.ProductID = null;

            var images = await db.PRODUCTIMAGES.Where(x => x.ProdID == productId && x.IsDeleted != true).ToListAsync();
            foreach (var img in images)
                img.IsDeleted = true;
        }

        public static async Task AfterWarehouseSoftDeletedAsync(DBTHSNEntities db, int warehouseId)
        {
            var exp = await db.EXPENSES.Where(x => x.ReceiverWhID == warehouseId).ToListAsync();
            foreach (var row in exp)
                row.ReceiverWhID = null;

            var inc = await db.INCOMES.Where(x => x.SenderWHID == warehouseId).ToListAsync();
            foreach (var row in inc)
                row.SenderWHID = null;
        }

        public static async Task AfterShopSoftDeletedAsync(DBTHSNEntities db, int shopId)
        {
            var wh = await db.WAREHOUSES.Where(x => x.ShopID == shopId).ToListAsync();
            foreach (var row in wh)
                row.ShopID = null;
        }

        public static async Task AfterOrgUnitSoftDeletedAsync(DBTHSNEntities db, int orgUnitId)
        {
            var children = await db.HR_ORG_UNITS.Where(x => x.ParentID == orgUnitId).ToListAsync();
            foreach (var row in children)
                row.ParentID = null;

            var emp = await db.HR_EMPLOYEES.Where(x => x.OrgUnitID == orgUnitId).ToListAsync();
            foreach (var row in emp)
                row.OrgUnitID = null;

            var ph = await db.HR_PAYROLL_HEADERS.Where(x => x.OrgUnitID == orgUnitId).ToListAsync();
            foreach (var row in ph)
                row.OrgUnitID = null;

            var th = await db.HR_TIMESHEET_HEADERS.Where(x => x.OrgUnitID == orgUnitId).ToListAsync();
            foreach (var row in th)
                row.OrgUnitID = null;

            var appr = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS.Where(x => x.OrgUnitID == orgUnitId).ToListAsync();
            foreach (var row in appr)
                row.OrgUnitID = null;

            var adm = await db.ADM_COMPANY_ORDERS.Where(x => x.OrgUnitID == orgUnitId).ToListAsync();
            foreach (var row in adm)
                row.OrgUnitID = null;

            var docs = await db.DOC_DOCUMENTS.Where(x => x.OrgUnitID == orgUnitId).ToListAsync();
            foreach (var row in docs)
                row.OrgUnitID = null;

            var jd = await db.HR_JOB_DESCRIPTIONS.Where(x => x.OrgUnitID == orgUnitId).ToListAsync();
            foreach (var row in jd)
                row.OrgUnitID = null;
        }

        public static async Task AfterPositionSoftDeletedAsync(DBTHSNEntities db, int positionId)
        {
            var emp = await db.HR_EMPLOYEES.Where(x => x.PositionID == positionId).ToListAsync();
            foreach (var row in emp)
                row.PositionID = null;

            var t = await db.HR_PIECE_RATE_TARIFFS.Where(x => x.PositionID == positionId).ToListAsync();
            foreach (var row in t)
                row.PositionID = null;
        }

        public static async Task AfterSalaryGradeSoftDeletedAsync(DBTHSNEntities db, int gradeId)
        {
            var emp = await db.HR_EMPLOYEES.Where(x => x.SalaryGradeID == gradeId).ToListAsync();
            foreach (var row in emp)
                row.SalaryGradeID = null;

            var sp = await db.HR_STAFFING_PLAN.Where(x => x.SalaryGradeID == gradeId).ToListAsync();
            foreach (var row in sp)
                row.SalaryGradeID = null;
        }

        public static async Task AfterCurrencySoftDeletedAsync(DBTHSNEntities db, int currencyId)
        {
            var wp = await db.F_WAYBILL_PAYMENTS.Where(x => x.CurrencyID == currencyId).ToListAsync();
            foreach (var row in wp)
                row.CurrencyID = null;

            var splits = await db.F_WAYBILL_PAYMENT_SPLITS.Where(x => x.CurrencyID == currencyId).ToListAsync();
            foreach (var row in splits)
                row.CurrencyID = null;

            var t = await db.F_CONTRACT_TARIFFS.Where(x => x.CurrencyID == currencyId).ToListAsync();
            foreach (var row in t)
                row.CurrencyID = null;

            var pl = await db.HR_PAYROLL_LINES.Where(x => x.CurrencyID == currencyId).ToListAsync();
            foreach (var row in pl)
                row.CurrencyID = null;

            var prt = await db.HR_PIECE_RATE_TARIFFS.Where(x => x.CurrencyID == currencyId).ToListAsync();
            foreach (var row in prt)
                row.CurrencyID = null;
        }

        public static async Task AfterTransportTypeSoftDeletedAsync(DBTHSNEntities db, int transportTypeId)
        {
            var t = await db.F_CONTRACT_TARIFFS.Where(x => x.TransportTypeID == transportTypeId).ToListAsync();
            foreach (var row in t)
                row.TransportTypeID = null;

            var w = await db.F_WAYBILLS.Where(x => x.TransportTypeID == transportTypeId).ToListAsync();
            foreach (var row in w)
                row.TransportTypeID = null;
        }

        public static async Task AfterForwarderSoftDeletedAsync(DBTHSNEntities db, int forwarderId)
        {
            var w = await db.F_WAYBILLS.Where(x => x.ForwarderID == forwarderId).ToListAsync();
            foreach (var row in w)
                row.ForwarderID = null;
        }

        public static async Task AfterUserSoftDeletedAsync(DBTHSNEntities db, int userId)
        {
            var tasks = await db.APP_TASKS.Where(x => x.AssigneeUserID == userId || x.ReporterUserID == userId).ToListAsync();
            foreach (var row in tasks)
            {
                if (row.AssigneeUserID == userId)
                    row.AssigneeUserID = null;
                if (row.ReporterUserID == userId)
                    row.ReporterUserID = null;
            }

            var he = await db.HR_EMPLOYEES.Where(x => x.CreatedByUserID == userId).ToListAsync();
            foreach (var row in he)
                row.CreatedByUserID = null;

            var docs = await db.DOC_DOCUMENTS.Where(x => x.CreatedByUserID == userId || x.ApprovedByUserID == userId).ToListAsync();
            foreach (var row in docs)
            {
                if (row.CreatedByUserID == userId)
                    row.CreatedByUserID = null;
                if (row.ApprovedByUserID == userId)
                    row.ApprovedByUserID = null;
            }
        }

        public static async Task AfterFinCategorySoftDeletedAsync(DBTHSNEntities db, int categoryId)
        {
            var fc = await db.FIN_CATEGORIES.Where(x => x.ParentID == categoryId).ToListAsync();
            foreach (var row in fc)
                row.ParentID = null;
        }

        public static async Task AfterFinBalanceCategorySoftDeletedAsync(DBTHSNEntities db, int balanceCategoryId)
        {
            var children = await db.FIN_BALANCE_CATEGORIES.Where(x => x.ParentID == balanceCategoryId).ToListAsync();
            foreach (var row in children)
                row.ParentID = null;

            var acc = await db.FIN_ACCOUNTS.Where(x => x.BalanceCategoryID == balanceCategoryId).ToListAsync();
            foreach (var row in acc)
                row.BalanceCategoryID = null;
        }

        /// <summary>O‘lchov birligi: faqat nullable UnitID maydonlari.</summary>
        public static async Task AfterUnitSoftDeletedAsync(DBTHSNEntities db, int unitId)
        {
            foreach (var row in await db.BOM_LINES.Where(x => x.UnitID == unitId).ToListAsync())
                row.UnitID = null;
            foreach (var row in await db.P_CONTRACT_PRODUCTS.Where(x => x.UnitID == unitId).ToListAsync())
                row.UnitID = null;
            foreach (var row in await db.P_ORDER_PRODUCTS.Where(x => x.UnitID == unitId).ToListAsync())
                row.UnitID = null;
        }

        public static async Task AfterSupplierSoftDeletedAsync(DBTHSNEntities db, int supplierId)
        {
            foreach (var row in await db.P_ORDERS.Where(x => x.SupplierID == supplierId).ToListAsync())
                row.SupplierID = null;
        }

        /// <summary>Rol: foydalanuvchilardan M2M bog‘lanishni uzadi (nullable FK yo‘q).</summary>
        public static async Task AfterRoleSoftDeletedAsync(DBTHSNEntities db, int roleId)
        {
            var role = await db.ROLES.Include(r => r.USERS).FirstOrDefaultAsync(r => r.ID == roleId);
            if (role == null)
                return;
            foreach (var u in role.USERS.ToList())
                u.ROLES.Remove(role);
        }

        public static async Task AfterTaskProjectSoftDeletedAsync(DBTHSNEntities db, int projectId)
        {
            foreach (var row in await db.APP_TASKS.Where(x => x.ProjectID == projectId).ToListAsync())
                row.ProjectID = null;
        }

        public static async Task AfterDefectTypeSoftDeletedAsync(DBTHSNEntities db, int defectTypeId)
        {
            foreach (var row in await db.QUALITY_INSPECTION.Where(x => x.DefectTypeID == defectTypeId).ToListAsync())
                row.DefectTypeID = null;
        }

        public static async Task AfterProductCategorySoftDeletedAsync(DBTHSNEntities db, int categoryId)
        {
            foreach (var row in await db.PRODUCTS.Where(x => x.CategoryID == categoryId).ToListAsync())
                row.CategoryID = null;
        }

        public static async Task AfterFinTransactionSoftDeletedAsync(DBTHSNEntities db, int finTransactionId)
        {
            foreach (var row in await db.SHAREHOLDER_CONTRIBUTIONS.Where(x => x.FinTransactionID == finTransactionId).ToListAsync())
                row.FinTransactionID = null;
        }

        public static async Task AfterPContractSoftDeletedAsync(DBTHSNEntities db, int contractId)
        {
            foreach (var row in await db.P_ORDERS.Where(x => x.ContractID == contractId).ToListAsync())
                row.ContractID = null;
        }

        public static async Task AfterFContractSoftDeletedAsync(DBTHSNEntities db, int contractId)
        {
            foreach (var row in await db.F_WAYBILLS.Where(x => x.ContractID == contractId).ToListAsync())
                row.ContractID = null;
            foreach (var row in await db.F_CONTRACT_DOCS.Where(x => x.ContractID == contractId).ToListAsync())
                row.ContractID = null;
        }

        public static async Task AfterSContractSoftDeletedAsync(DBTHSNEntities db, int contractId)
        {
            foreach (var row in await db.SALES_INVOICES.Where(x => x.ContractID == contractId).ToListAsync())
                row.ContractID = null;
            foreach (var row in await db.SALES_TTNK_HEADERS.Where(x => x.ContractID == contractId).ToListAsync())
                row.ContractID = null;
        }

        public static async Task AfterPOrderSoftDeletedAsync(DBTHSNEntities db, int orderId)
        {
            foreach (var row in await db.P_INVOICES.Where(x => x.OrderID == orderId).ToListAsync())
                row.OrderID = null;
            foreach (var row in await db.F_WAYBILLS.Where(x => x.PurchaseOrderID == orderId).ToListAsync())
                row.PurchaseOrderID = null;
        }

        public static async Task AfterSOrderSoftDeletedAsync(DBTHSNEntities db, int orderId)
        {
            foreach (var row in await db.SALES_INVOICES.Where(x => x.OrderID == orderId).ToListAsync())
                row.OrderID = null;
            foreach (var row in await db.SALES_TTNK_HEADERS.Where(x => x.OrderID == orderId).ToListAsync())
                row.OrderID = null;
            foreach (var row in await db.F_WAYBILLS.Where(x => x.SalesOrderID == orderId).ToListAsync())
                row.SalesOrderID = null;
            foreach (var row in await db.QUALITY_INSPECTION.Where(x => x.SOrderID == orderId).ToListAsync())
                row.SOrderID = null;
        }

        public static async Task AfterPInvoiceSoftDeletedAsync(DBTHSNEntities db, int purchaseInvoiceId)
        {
            foreach (var row in await db.INCOMES.Where(x => x.InvoiceID == purchaseInvoiceId).ToListAsync())
                row.InvoiceID = null;
            foreach (var row in await db.F_WAYBILLS.Where(x => x.InvoiceID == purchaseInvoiceId).ToListAsync())
                row.InvoiceID = null;
        }

        public static async Task AfterDocDocumentSoftDeletedAsync(DBTHSNEntities db, long documentId)
        {
            var files = await db.DOC_FILES.Where(x => x.DocumentID == documentId && x.IsDeleted != true).ToListAsync();
            foreach (var f in files)
                f.IsDeleted = true;
        }

        public static async Task AfterProductProductionSoftDeletedAsync(DBTHSNEntities db, int productionId)
        {
            foreach (var row in await db.QUALITY_INSPECTION.Where(x => x.ProductionID == productionId).ToListAsync())
                row.ProductionID = null;
        }

        public static async Task AfterProductPlanSoftDeletedAsync(DBTHSNEntities db, int planId)
        {
            var dailyIds = await db.PRODUCTPLANS_DAILY.Where(x => x.PlanID == planId).Select(x => x.ID).ToListAsync();
            if (dailyIds.Count == 0)
                return;
            foreach (var row in await db.QUALITY_INSPECTION.Where(x => x.ProductPlanDailyID != null && dailyIds.Contains(x.ProductPlanDailyID.Value)).ToListAsync())
                row.ProductPlanDailyID = null;
        }
    }
}
