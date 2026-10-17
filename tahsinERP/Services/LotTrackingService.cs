using tahsinERP.Services.Stock;

namespace tahsinERP.Services
{
    /// <summary>
    /// Eski "kirim hujjati = lot" rejimining qoldig'i. Chiqim endi har doim sof FIFO (SRM-B13), foydalanuvchi
    /// lot tanlamaydi va yangi chiqim qatorlariga "IncomeID:n" markeri yozilmaydi. Bu yerda faqat eski
    /// yozuvlardagi markerni o'qish qoldi (tahrirlash formasida izoh sifatida ko'rsatmaslik uchun).
    /// </summary>
    public static class LotTrackingService
    {
        /// <summary>Comment dan "IncomeID:X" ni ajratib oladi. Topilmasa null.</summary>
        public static int? ParseIncomeIdFromComment(string comment)
        {
            return DocumentMarkers.Parse(comment, DocumentMarkers.IncomeId);
        }
    }
}
