using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services.Documents
{
    /// <summary>
    /// DOC_DOCUMENTS sarlavhasi va uning kichik turlari (xat, buyruq, standart) uchun umumiy amallar.
    /// Avval har controller o'z nusxasini yozardi (OfficialLetters, CompanyOrders, STD).
    /// </summary>
    public static class DocumentService
    {
        public const string OfficialLetterType = "OFFICIAL_LETTER";
        public const string CompanyOrderType = "COMP_ORDER";

        public const string Draft = "DRAFT";
        public const string ForApproval = "FOR_APPROVAL";
        public const string Approved = "APPROVED";
        public const string Cancelled = "CANCELLED";

        /// <summary>
        /// Ruxsat etilgan status o'tishlari — Details sahifasidagi tugmalar bilan bir xil.
        /// Avval server istalgan statusga o'tkazardi: bekor qilingan hujjatni ham tasdiqlash mumkin edi (DOC-B08).
        /// </summary>
        private static readonly Dictionary<string, string[]> Transitions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { Draft, new[] { ForApproval, Approved, Cancelled } },
            { ForApproval, new[] { Approved, Cancelled } }
        };

        public sealed class StatusChangeResult
        {
            public bool Success { get; set; }
            public string Error { get; set; }
            public string StatusName { get; set; }
            public string DocumentCode { get; set; }
        }

        /// <summary>
        /// Hujjat statusini o'zgartiradi: hujjat shu turga tegishli va o'chirilmagan bo'lishi,
        /// o'tish ruxsat etilgan bo'lishi kerak. APPROVED'da tasdiqlovchi yoziladi. SaveChanges shu yerda.
        /// </summary>
        public static async Task<StatusChangeResult> ChangeStatusAsync(DBTHSNEntities db, long documentId, string docTypeCode, string statusCode, int userId)
        {
            var doc = await db.DOC_DOCUMENTS
                .Include(d => d.DOC_DOCUMENT_STATUS_REF)
                .FirstOrDefaultAsync(d => d.ID == documentId && d.IsDeleted != true && d.DOC_DOCUMENT_TYPES.Code == docTypeCode);
            if (doc == null)
                return null;

            string current = doc.DOC_DOCUMENT_STATUS_REF?.Code;
            string[] allowed;
            if (current == null || !Transitions.TryGetValue(current, out allowed) ||
                !allowed.Contains(statusCode, StringComparer.OrdinalIgnoreCase))
            {
                return new StatusChangeResult { Error = "Bu holatdan tanlangan holatga o'tkazib bo'lmaydi" };
            }

            var status = await db.DOC_DOCUMENT_STATUS_REF.FirstOrDefaultAsync(s => s.Code == statusCode && s.IsActive);
            if (status == null)
                return new StatusChangeResult { Error = "Noto'g'ri holat kodi" };

            doc.StatusID = status.ID;
            doc.UpdatedDT = DateTime.Now;
            if (string.Equals(statusCode, Approved, StringComparison.OrdinalIgnoreCase))
                doc.ApprovedByUserID = userId;

            await db.SaveChangesAsync();
            return new StatusChangeResult { Success = true, StatusName = status.Name, DocumentCode = doc.Code };
        }

        /// <summary>Hujjat turi va DRAFT holati ID'lari (seed bo'lmasa null).</summary>
        /// <summary>
        /// Yangi DOC_DOCUMENTS sarlavhasi (kichik tur qatori chaqiruvchida biriktiriladi va bitta SaveChanges/tranzaksiyada
        /// saqlanadi). Avval xat (chiquvchi/kiruvchi), buyruq va STD'da bir xil initsializator nusxalangan edi (dublikat #13).
        /// </summary>
        public static DOC_DOCUMENTS NewHeader(int docTypeId, int statusId, string code, string title, int? userId)
        {
            return new DOC_DOCUMENTS
            {
                DocTypeID = docTypeId,
                StatusID = statusId,
                Code = code,
                Title = title,
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now,
                IntegrationUID = Guid.NewGuid()
            };
        }

        /// <summary>Holat ID'si kod bo'yicha; yo'q bo'lsa yaratiladi (STD'ning "Active" holati seed'da bo'lmasligi mumkin).</summary>
        public static async Task<int> EnsureStatusIdAsync(DBTHSNEntities db, string statusCode)
        {
            var id = await db.DOC_DOCUMENT_STATUS_REF.Where(st => st.Code == statusCode).Select(st => (int?)st.ID).FirstOrDefaultAsync();
            if (id.HasValue)
                return id.Value;

            var status = new DOC_DOCUMENT_STATUS_REF { Code = statusCode, Name = statusCode, IsActive = true, SortOrder = 0 };
            db.DOC_DOCUMENT_STATUS_REF.Add(status);
            await db.SaveChangesAsync();
            return status.ID;
        }

        public static async Task<Tuple<int, int>> DraftHeaderIdsAsync(DBTHSNEntities db, string docTypeCode)
        {
            var typeId = await db.DOC_DOCUMENT_TYPES.Where(t => t.Code == docTypeCode).Select(t => (int?)t.ID).FirstOrDefaultAsync();
            var statusId = await db.DOC_DOCUMENT_STATUS_REF.Where(s => s.Code == Draft).Select(s => (int?)s.ID).FirstOrDefaultAsync();
            return typeId.HasValue && statusId.HasValue ? Tuple.Create(typeId.Value, statusId.Value) : null;
        }

        /// <summary>
        /// Hujjatni, uning kichik turi qatorini va barcha fayllarini set-based UPDATE bilan soft-delete qiladi.
        /// Entity yuklanmaydi (avval Include(DOC_FILES) va SoftDeleteReferenceService barcha blob'larni o'qirdi).
        /// STD'da avval faqat DOC_DOCUMENTS belgilanardi, STD_COMPANY_STANDARDS.IsDeleted esa qolardi (DOC-B01).
        /// </summary>
        public static async Task SoftDeleteAsync(DBTHSNEntities db, long documentId)
        {
            // Bitta batch — ExecuteSqlCommand uni o'z tranzaksiyasida bajaradi
            await db.Database.ExecuteSqlCommandAsync(
                "UPDATE DOC_DOCUMENTS SET IsDeleted = 1, UpdatedDT = GETDATE() WHERE ID = @p0; " +
                "UPDATE COM_OFFICIAL_LETTERS SET IsDeleted = 1 WHERE DocumentID = @p0; " +
                "UPDATE ADM_COMPANY_ORDERS SET IsDeleted = 1 WHERE DocumentID = @p0; " +
                "UPDATE STD_COMPANY_STANDARDS SET IsDeleted = 1 WHERE DocumentID = @p0; " +
                "UPDATE DOC_FILES SET IsDeleted = 1 WHERE DocumentID = @p0 AND (IsDeleted IS NULL OR IsDeleted = 0);",
                documentId);
        }
    }

    /// <summary>
    /// Chiquvchi xat va buyruq raqamlari ("0123"). Avval barcha raqamlar xotiraga yuklanib, qulfsiz
    /// hisoblanar (parallel saqlashda takror), kiruvchi xatlarning tashqi raqamlari ham chiquvchi
    /// ketma-ketlikka qo'shilardi va "IN-12345".Substring(4) kabi tasodifiy max berardi (DOC-B06).
    /// Endi: faqat raqamli qiymatlar SQL'da MAX, xatlarda faqat DirectionCode='OUT', o'chirilganlar ham
    /// hisoblanadi (raqam qayta berilmaydi), saqlashda qulf ostida tekshiriladi.
    /// </summary>
    public static class DocNumbering
    {
        private const string LetterMaxSql =
            "SELECT MAX(TRY_CAST(LetterNo AS BIGINT)) FROM COM_OFFICIAL_LETTERS WHERE DirectionCode = 'OUT'";
        private const string OrderMaxSql =
            "SELECT MAX(TRY_CAST(OrderNo AS BIGINT)) FROM ADM_COMPANY_ORDERS";

        /// <summary>Formada ko'rsatiladigan taxminiy raqam (qulfsiz; saqlashda qayta tekshiriladi).</summary>
        public static Task<string> PeekLetterNoAsync(DBTHSNEntities db) => NextAsync(db, LetterMaxSql);

        public static Task<string> PeekOrderNoAsync(DBTHSNEntities db) => NextAsync(db, OrderMaxSql);

        /// <summary>
        /// Saqlash tranzaksiyasi ichida: bo'sh yoki band raqam o'rniga keyingisini beradi.
        /// Qaytaradi: yakuniy raqam (o'zgargan bo'lsa — chaqiruvchi foydalanuvchiga xabar beradi).
        /// </summary>
        public static async Task<string> EnsureOutgoingLetterNoAsync(DBTHSNEntities db, string requested)
        {
            await DocumentNumberService.AcquireLockAsync(db, "COM_OFFICIAL_LETTERS.LetterNo:OUT");
            if (!string.IsNullOrWhiteSpace(requested))
            {
                string no = requested.Trim();
                // O'chirilganlar ham hisoblanadi: bazadagi UX_COM_OFFICIAL_LETTERS_No_Date_Dir ularni ham o'z ichiga oladi
                bool taken = await db.COM_OFFICIAL_LETTERS.AnyAsync(l => l.DirectionCode == "OUT" && l.LetterNo == no);
                if (!taken)
                    return no;
            }
            return await NextAsync(db, LetterMaxSql);
        }

        public static async Task<string> EnsureOrderNoAsync(DBTHSNEntities db, string requested)
        {
            await DocumentNumberService.AcquireLockAsync(db, "ADM_COMPANY_ORDERS.OrderNo");
            if (!string.IsNullOrWhiteSpace(requested))
            {
                string no = requested.Trim();
                // O'chirilganlar ham hisoblanadi (raqam qayta berilmaydi; UX_ADM_COMPANY_ORDERS_No_Date)
                bool taken = await db.ADM_COMPANY_ORDERS.AnyAsync(o => o.OrderNo == no);
                if (!taken)
                    return no;
            }
            return await NextAsync(db, OrderMaxSql);
        }

        private static async Task<string> NextAsync(DBTHSNEntities db, string maxSql)
        {
            long? max = await db.Database.SqlQuery<long?>(maxSql).FirstOrDefaultAsync();
            return ((max ?? 0) + 1).ToString("D4");
        }
    }
}
