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
    ///
    /// Bajarilishi (CORE-P15 / SRM-P17): har bog'liq qatorni EF'ga yuklab bittalab o'zgartirish o'rniga
    /// bitta set-based UPDATE/DELETE. Chaqiruvchida db.Database tranzaksiyasi ochiq bo'lsa, shu
    /// tranzaksiyada bajariladi. Sinxron va async variantlar bir xil SQL'ni ishlatadi.
    /// </summary>
    public static class SoftDeleteReferenceService
    {
        // --- Sozlama bog'lanishlarini tozalaydiganlar ---

        public static Task AfterProductSoftDeletedAsync(DBTHSNEntities db, int productId)
        {
            // BOM va normalar o'chirilgan mahsulotga tayanmasin; mahsulot rasmlari ham yashiriladi.
            // Tabel, TTNK va yetkazish jadvali qatorlari — tarix, saqlanadi.
            return Exec(db,
                "UPDATE BOM_LINES SET ComponentID = NULL WHERE ComponentID = @p0; " +
                "UPDATE BOM_HEADERS SET ProductID = NULL WHERE ProductID = @p0; " +
                "UPDATE NORMVERSIONS SET ProductID = NULL WHERE ProductID = @p0; " +
                "UPDATE PRODUCTIMAGES SET IsDeleted = 1 WHERE ProdID = @p0 AND (IsDeleted IS NULL OR IsDeleted = 0);",
                productId);
        }

        public static Task AfterShopSoftDeletedAsync(DBTHSNEntities db, int shopId)
        {
            return Exec(db, "UPDATE WAREHOUSES SET ShopID = NULL WHERE ShopID = @p0;", shopId);
        }

        public static Task AfterOrgUnitSoftDeletedAsync(DBTHSNEntities db, int orgUnitId)
        {
            // Ierarxiya va xodimning joriy bo'limi. Ish haqi, tabel, buyruq, hujjatlar — tarix, saqlanadi.
            return Exec(db,
                "UPDATE HR_ORG_UNITS SET ParentID = NULL WHERE ParentID = @p0; " +
                "UPDATE HR_EMPLOYEES SET OrgUnitID = NULL WHERE OrgUnitID = @p0;",
                orgUnitId);
        }

        public static Task AfterPositionSoftDeletedAsync(DBTHSNEntities db, int positionId)
        {
            return Exec(db,
                "UPDATE HR_EMPLOYEES SET PositionID = NULL WHERE PositionID = @p0; " +
                "UPDATE HR_PIECE_RATE_TARIFFS SET PositionID = NULL WHERE PositionID = @p0;",
                positionId);
        }

        public static Task AfterSalaryGradeSoftDeletedAsync(DBTHSNEntities db, int gradeId)
        {
            return Exec(db,
                "UPDATE HR_EMPLOYEES SET SalaryGradeID = NULL WHERE SalaryGradeID = @p0; " +
                "UPDATE HR_STAFFING_PLAN SET SalaryGradeID = NULL WHERE SalaryGradeID = @p0;",
                gradeId);
        }

        public static Task AfterTransportTypeSoftDeletedAsync(DBTHSNEntities db, int transportTypeId)
        {
            // Tariflar — sozlama; yuk xatlari — tarix, saqlanadi.
            return Exec(db, "UPDATE F_CONTRACT_TARIFFS SET TransportTypeID = NULL WHERE TransportTypeID = @p0;", transportTypeId);
        }

        public static Task AfterUserSoftDeletedAsync(DBTHSNEntities db, int userId)
        {
            // Faqat yakunlanmagan topshiriqlar ijrochisiz qoladi (qayta tayinlash uchun).
            // Muallif, yaratuvchi va tasdiqlovchi — tarix, saqlanadi.
            return Exec(db,
                "UPDATE t SET t.AssigneeUserID = NULL " +
                "FROM APP_TASKS t LEFT JOIN APP_TASK_STATUS_REF s ON s.ID = t.StatusID " +
                "WHERE t.AssigneeUserID = @p0 AND t.IsDeleted = 0 AND (s.ID IS NULL OR s.Code <> 'DONE');",
                userId);
        }

        public static Task AfterFinCategorySoftDeletedAsync(DBTHSNEntities db, int categoryId)
        {
            return Exec(db, "UPDATE FIN_CATEGORIES SET ParentID = NULL WHERE ParentID = @p0;", categoryId);
        }

        public static Task AfterFinBalanceCategorySoftDeletedAsync(DBTHSNEntities db, int balanceCategoryId)
        {
            return Exec(db,
                "UPDATE FIN_BALANCE_CATEGORIES SET ParentID = NULL WHERE ParentID = @p0; " +
                "UPDATE FIN_ACCOUNTS SET BalanceCategoryID = NULL WHERE BalanceCategoryID = @p0;",
                balanceCategoryId);
        }

        /// <summary>O'lchov birligi: faqat BOM (sozlama). Shartnoma/buyurtma qatorlari — tarix, saqlanadi.</summary>
        public static Task AfterUnitSoftDeletedAsync(DBTHSNEntities db, int unitId)
        {
            return Exec(db, "UPDATE BOM_LINES SET UnitID = NULL WHERE UnitID = @p0;", unitId);
        }

        /// <summary>Rol: foydalanuvchilardan M2M bog'lanishni uzadi.</summary>
        public static Task AfterRoleSoftDeletedAsync(DBTHSNEntities db, int roleId)
        {
            return Exec(db, "DELETE FROM USERROLES WHERE RoleID = @p0;", roleId);
        }

        public static Task AfterFinTransactionSoftDeletedAsync(DBTHSNEntities db, int finTransactionId)
        {
            return Exec(db, "UPDATE SHAREHOLDER_CONTRIBUTIONS SET FinTransactionID = NULL WHERE FinTransactionID = @p0;", finTransactionId);
        }

        public static Task AfterPOrderSoftDeletedAsync(DBTHSNEntities db, int orderId)
        {
            // Invoys va yuk xatlari — tarix, saqlanadi. Talabnoma qayta buyurtmaga aylantirilishi uchun bo'shatiladi.
            return Exec(db,
                "UPDATE P_REQUISITIONS SET ConvertedToOrderID = NULL, Status = 'Approved' WHERE ConvertedToOrderID = @p0;",
                orderId);
        }

        public static Task AfterDocDocumentSoftDeletedAsync(DBTHSNEntities db, long documentId)
        {
            // Har versiyaning fayl blob'ini (FileContent) yuklamasdan
            return Exec(db,
                "UPDATE DOC_FILES SET IsDeleted = 1 WHERE DocumentID = @p0 AND (IsDeleted IS NULL OR IsDeleted = 0);",
                documentId);
        }

        public static Task AfterProductProductionSoftDeletedAsync(DBTHSNEntities db, int productionId)
        {
            return Exec(db, "UPDATE QUALITY_INSPECTION SET ProductionID = NULL WHERE ProductionID = @p0;", productionId);
        }

        public static Task AfterProductPlanSoftDeletedAsync(DBTHSNEntities db, int planId)
        {
            return Exec(db, ProductPlanSql, planId);
        }

        // --- Tarixiy bog'lanish saqlanadi (hech narsa qilinmaydi; chaqiruvlar mos qolishi uchun) ---

        /// <summary>EXPENSES.ReceiverWhID, INCOMES.SenderWHID — ko'chirish tarixi saqlanadi.</summary>
        public static Task AfterWarehouseSoftDeletedAsync(DBTHSNEntities db, int warehouseId) => Task.CompletedTask;

        /// <summary>To'lov, tarif va ish haqi summalari valyutasiz qolmasligi uchun saqlanadi.</summary>
        public static Task AfterCurrencySoftDeletedAsync(DBTHSNEntities db, int currencyId) => Task.CompletedTask;

        /// <summary>Yuk xatlari: ekspeditor qarzi hisobotlardan yo'qolmasligi uchun saqlanadi.</summary>
        public static Task AfterForwarderSoftDeletedAsync(DBTHSNEntities db, int forwarderId) => Task.CompletedTask;

        public static Task AfterSupplierSoftDeletedAsync(DBTHSNEntities db, int supplierId) => Task.CompletedTask;
        public static Task AfterTaskProjectSoftDeletedAsync(DBTHSNEntities db, int projectId) => Task.CompletedTask;
        public static Task AfterDefectTypeSoftDeletedAsync(DBTHSNEntities db, int defectTypeId) => Task.CompletedTask;

        /// <summary>Mahsulotlar kategoriyasiz qolmaydi (kategoriya bayroqlari hisob-kitobga ta'sir qiladi).</summary>
        public static Task AfterProductCategorySoftDeletedAsync(DBTHSNEntities db, int categoryId) => Task.CompletedTask;

        public static Task AfterPContractSoftDeletedAsync(DBTHSNEntities db, int contractId) => Task.CompletedTask;
        public static Task AfterFContractSoftDeletedAsync(DBTHSNEntities db, int contractId) => Task.CompletedTask;
        public static Task AfterSContractSoftDeletedAsync(DBTHSNEntities db, int contractId) => Task.CompletedTask;
        public static Task AfterSOrderSoftDeletedAsync(DBTHSNEntities db, int orderId) => Task.CompletedTask;
        public static Task AfterPInvoiceSoftDeletedAsync(DBTHSNEntities db, int purchaseInvoiceId) => Task.CompletedTask;

        // --- Sinxron variantlar (sinxron action'lar uchun; async'ni bloklab kutish deadlock beradi) ---

        public static void AfterShopSoftDeleted(DBTHSNEntities db, int shopId)
        {
            ExecSync(db, "UPDATE WAREHOUSES SET ShopID = NULL WHERE ShopID = @p0;", shopId);
        }

        public static void AfterDefectTypeSoftDeleted(DBTHSNEntities db, int defectTypeId)
        {
            // Sifat nazorati yozuvlari — tarix, saqlanadi
        }

        public static void AfterProductCategorySoftDeleted(DBTHSNEntities db, int categoryId)
        {
            // Mahsulotlar kategoriyasiz qolmaydi — saqlanadi
        }

        public static void AfterProductPlanSoftDeleted(DBTHSNEntities db, int planId)
        {
            ExecSync(db, ProductPlanSql, planId);
        }

        private const string ProductPlanSql =
            "UPDATE q SET q.ProductPlanDailyID = NULL FROM QUALITY_INSPECTION q " +
            "JOIN PRODUCTPLANS_DAILY d ON d.ID = q.ProductPlanDailyID WHERE d.PlanID = @p0;";

        private static Task Exec(DBTHSNEntities db, string sql, object id)
        {
            return db.Database.ExecuteSqlCommandAsync(sql, id);
        }

        private static void ExecSync(DBTHSNEntities db, string sql, object id)
        {
            db.Database.ExecuteSqlCommand(sql, id);
        }
    }
}
