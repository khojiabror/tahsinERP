using System.IO;
using ClosedXML.Excel;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// ClosedXML ustidagi kichik yordamchilar. EPPlus'dan ko'chirilgan import/eksport kodi
    /// avvalgi xatti-harakatni saqlashi uchun EPPlus semantikasini takrorlaydi.
    /// EPPlus .Text o'rniga IXLCell.GetFormattedString() ishlating — IXLCell.GetText()
    /// faqat matnli hujayrada ishlaydi, raqam/sanada InvalidCastException tashlaydi.
    /// </summary>
    public static class ExcelHelper
    {
        public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>
        /// Kitobni .xlsx baytlariga aylantiradi (EPPlus GetAsByteArray ekvivalenti).
        /// </summary>
        public static byte[] ToByteArray(this XLWorkbook workbook)
        {
            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Oxirgi to'ldirilgan qator raqami; bo'sh varaqda 0 (EPPlus Dimension?.End.Row ?? 0).
        /// </summary>
        public static int LastRowNumber(this IXLWorksheet worksheet)
        {
            return worksheet.LastRowUsed()?.RowNumber() ?? 0;
        }

        /// <summary>
        /// Oxirgi to'ldirilgan ustun raqami; bo'sh varaqda 0 (EPPlus Dimension?.End.Column ?? 0).
        /// </summary>
        public static int LastColumnNumber(this IXLWorksheet worksheet)
        {
            return worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        }

        /// <summary>
        /// Hujayra qiymati EPPlus .Value kabi: bo'sh bo'lsa null, sana/vaqt esa
        /// Excel seriya raqami (double) sifatida qaytadi.
        /// </summary>
        public static object GetRawValue(this IXLCell cell)
        {
            var value = cell.Value;
            switch (value.Type)
            {
                case XLDataType.Blank:
                    return null;
                case XLDataType.Boolean:
                    return value.GetBoolean();
                case XLDataType.Number:
                case XLDataType.DateTime:
                case XLDataType.TimeSpan:
                    return value.GetUnifiedNumber();
                case XLDataType.Text:
                    return value.GetText();
                default:
                    return value.ToString();
            }
        }
    }
}
