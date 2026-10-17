using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Soft delete (IsDeleted) dan keyingi bog'lanishlarni tartibga solish.
    ///
    /// SIYOSAT (audit 2026-10, CORE-B01 / LOG-B02 / SRM-B11):
    ///  - Tarixiy/tranzaksion hujjatlardagi FK'lar (yuk xati, to'lov, invoys, buyurtma, kirim/chiqim,
    ///    ish haqi, tabel, sifat nazorati va h.k.) NULL QILINMAYDI. Aks holda qarzdorlik, hisobotlar va
    ///    "kim/qaysi valyutada" ma'lumoti yo'qolardi, DeletedRecords/Recover esa bog'lanishni tiklay olmasdi.
    ///    O'chirilgan yozuv yangi hujjatlarda ko'rinmasligi dropdown'lardagi IsDeleted filtri bilan ta'minlanadi.
    ///  - Faqat sozlama/ierarxiya bog'lanishlari tozalanadi (BOM, norma, bo'lim ierarxiyasi, xodimning
    ///    joriy bo'limi/lavozimi, tariflar, rol a'zoligi, ochiq topshiriq ijrochisi) — ular kelajakdagi
    ///    hisob-kitoblarga ta'sir qiladi.
    /// </summary>
    public static class SoftDeleteReferenceService
    {
        public static async Task AfterProductSoftDeletedAsync(DBTHSNEntities db, int productId)
        {
            // Sozlama: BOM va normalar o'chirilgan mahsulotga tayanmasin
            var bomLines = await db.BOM_LINES.Where(x => x.ComponentID == productId).ToListAsync();
            foreach (var row in bomLines)
                row.ComponentID = null;

            var bomHeaders = await db.BOM_HEADERS.Where(x => x.ProductID == productId).ToListAsync();
            foreach (var row in bomHeaders)
                row.ProductID = null;

            var norms = await db.NORMVERSIONS.Where(x => x.ProductID == productId).ToListAsync();
            foreach (var row in norms)
                row.ProductID = null;

            // Tabel, TTNK va yetkazish jadvali qatorlari — tarix, saqlanadi

            var images = await db.PRODUCTIMAGES.Where(x => x.ProdID == productId && x.IsDeleted != true).ToListAsync();
            foreach (var img in images)
                img.IsDeleted = true;
        }

        public static async Task AfterWarehouseSoftDeletedAsync(DBTHSNEntities db, int warehouseId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            // (EXPENSES.ReceiverWhID, INCOMES.SenderWHID — ko'chirish tarixi)
            await Task.CompletedTask;
        }

        public static async Task AfterShopSoftDeletedAsync(DBTHSNEntities db, int shopId)
        {
            var wh = await db.WAREHOUSES.Where(x => x.ShopID == shopId).ToListAsync();
            foreach (var row in wh)
                row.ShopID = null;
        }

        public static async Task AfterOrgUnitSoftDeletedAsync(DBTHSNEntities db, int orgUnitId)
        {
            // Sozlama: ierarxiya va xodimning joriy bo'limi
            var children = await db.HR_ORG_UNITS.Where(x => x.ParentID == orgUnitId).ToListAsync();
            foreach (var row in children)
                row.ParentID = null;

            var emp = await db.HR_EMPLOYEES.Where(x => x.OrgUnitID == orgUnitId).ToListAsync();
            foreach (var row in emp)
                row.OrgUnitID = null;

            // Ish haqi, tabel, tasdiqlar, buyruqlar, hujjatlar va yo'riqnomalar — tarix, saqlanadi
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
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            // (to'lovlar, tariflar, ish haqi summalari valyutasiz qolmasligi uchun)
            await Task.CompletedTask;
        }

        public static async Task AfterTransportTypeSoftDeletedAsync(DBTHSNEntities db, int transportTypeId)
        {
            // Sozlama: tariflar
            var t = await db.F_CONTRACT_TARIFFS.Where(x => x.TransportTypeID == transportTypeId).ToListAsync();
            foreach (var row in t)
                row.TransportTypeID = null;

            // Yuk xatlari — tarix, saqlanadi
        }

        public static async Task AfterForwarderSoftDeletedAsync(DBTHSNEntities db, int forwarderId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            // (yuk xatlari: ekspeditor qarzi hisobotlardan yo'qolmasligi uchun)
            await Task.CompletedTask;
        }

        public static async Task AfterUserSoftDeletedAsync(DBTHSNEntities db, int userId)
        {
            // Faqat yakunlanmagan topshiriqlar ijrochisiz qoladi (qayta tayinlash uchun).
            // Muallif, yaratuvchi va tasdiqlovchi — tarix, saqlanadi.
            var tasks = await db.APP_TASKS
                .Where(x => x.AssigneeUserID == userId && x.IsDeleted == false &&
                            (x.APP_TASK_STATUS_REF == null || x.APP_TASK_STATUS_REF.Code != "DONE"))
                .ToListAsync();
            foreach (var row in tasks)
                row.AssigneeUserID = null;
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
            // Sozlama: BOM. Shartnoma/buyurtma qatorlari — tarix, saqlanadi
            foreach (var row in await db.BOM_LINES.Where(x => x.UnitID == unitId).ToListAsync())
                row.UnitID = null;
        }

        public static async Task AfterSupplierSoftDeletedAsync(DBTHSNEntities db, int supplierId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            await Task.CompletedTask;
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
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            await Task.CompletedTask;
        }

        public static async Task AfterDefectTypeSoftDeletedAsync(DBTHSNEntities db, int defectTypeId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            await Task.CompletedTask;
        }

        public static async Task AfterProductCategorySoftDeletedAsync(DBTHSNEntities db, int categoryId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            await Task.CompletedTask;
        }

        public static async Task AfterFinTransactionSoftDeletedAsync(DBTHSNEntities db, int finTransactionId)
        {
            foreach (var row in await db.SHAREHOLDER_CONTRIBUTIONS.Where(x => x.FinTransactionID == finTransactionId).ToListAsync())
                row.FinTransactionID = null;
        }

        public static async Task AfterPContractSoftDeletedAsync(DBTHSNEntities db, int contractId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            await Task.CompletedTask;
        }

        public static async Task AfterFContractSoftDeletedAsync(DBTHSNEntities db, int contractId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            await Task.CompletedTask;
        }

        public static async Task AfterSContractSoftDeletedAsync(DBTHSNEntities db, int contractId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            await Task.CompletedTask;
        }

        public static async Task AfterPOrderSoftDeletedAsync(DBTHSNEntities db, int orderId)
        {
            // Invoys va yuk xatlari — tarix, saqlanadi.
            // Talabnoma qayta buyurtmaga aylantirilishi uchun bo'shatiladi.
            foreach (var req in await db.P_REQUISITIONS.Where(x => x.ConvertedToOrderID == orderId).ToListAsync())
            {
                req.ConvertedToOrderID = null;
                req.Status = "Approved";
            }
        }

        public static async Task AfterSOrderSoftDeletedAsync(DBTHSNEntities db, int orderId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            await Task.CompletedTask;
        }

        public static async Task AfterPInvoiceSoftDeletedAsync(DBTHSNEntities db, int purchaseInvoiceId)
        {
            // Tarixiy hujjatlardagi bog‘lanish saqlanadi (audit 2026-10, CORE-B01).
            await Task.CompletedTask;
        }

        public static async Task AfterDocDocumentSoftDeletedAsync(DBTHSNEntities db, long documentId)
        {
            // Set-based: avval har versiyaning fayl blob'i (FileContent) xotiraga yuklanardi.
            // Chaqiruvchi tranzaksiyasi bo'lsa, shu tranzaksiyada bajariladi.
            await db.Database.ExecuteSqlCommandAsync(
                "UPDATE DOC_FILES SET IsDeleted = 1 WHERE DocumentID = @p0 AND (IsDeleted IS NULL OR IsDeleted = 0)",
                documentId);
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

        // --- Sinxron variantlar ---
        // Sinxron (async bo'lmagan) action'lardan chaqirilganda async metodni .GetAwaiter().GetResult()
        // bilan bloklab kutish ASP.NET sync-context deadlock'iga olib keladi. Shu sabab sinxron
        // action'lar quyidagi sinxron metodlarni ishlatishi kerak.

        public static void AfterShopSoftDeleted(DBTHSNEntities db, int shopId)
        {
            foreach (var row in db.WAREHOUSES.Where(x => x.ShopID == shopId).ToList())
                row.ShopID = null;
        }

        public static void AfterDefectTypeSoftDeleted(DBTHSNEntities db, int defectTypeId)
        {
            // Sifat nazorati yozuvlari — tarix, saqlanadi (audit 2026-10, CORE-B01)
        }

        public static void AfterProductCategorySoftDeleted(DBTHSNEntities db, int categoryId)
        {
            // Mahsulotlar kategoriyasiz qolmaydi (kategoriya bayroqlari hisob-kitobga ta'sir qiladi) — saqlanadi
        }

        public static void AfterProductPlanSoftDeleted(DBTHSNEntities db, int planId)
        {
            var dailyIds = db.PRODUCTPLANS_DAILY.Where(x => x.PlanID == planId).Select(x => x.ID).ToList();
            if (dailyIds.Count == 0)
                return;
            foreach (var row in db.QUALITY_INSPECTION.Where(x => x.ProductPlanDailyID != null && dailyIds.Contains(x.ProductPlanDailyID.Value)).ToList())
                row.ProductPlanDailyID = null;
        }
    }
}
