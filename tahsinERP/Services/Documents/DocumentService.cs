using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.Documents
{
    /// <summary>
    /// DOC_DOCUMENTS sarlavhasi va uning kichik turlari (xat, buyruq, standart) uchun umumiy amallar.
    /// Avval har controller o'z nusxasini yozardi (OfficialLetters, CompanyOrders, STD).
    /// </summary>
    public static class DocumentService
    {
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
}
