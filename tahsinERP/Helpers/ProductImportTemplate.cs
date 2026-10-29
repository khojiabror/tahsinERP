using ClosedXML.Excel;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Nomenklatura Excel importi shabloni (maxsulot.xlsx). Avval SAMPLE_FILES jadvalidan o'qilardi,
    /// lekin hech bir migratsiya/seed u yerga fayl yozmaydi — yangi firmalarda shablon 404 berardi.
    /// Endi kodda yasaladi; ustunlar ProductController.Save o'qiydigan nomlar bilan bir xil.
    /// </summary>
    public static class ProductImportTemplate
    {
        public const string FileName = "maxsulot.xlsx";

        public static readonly string[] Columns =
        {
            "Partnumber", "Name", "Type", "Weight", "Length", "Width", "Height", "PNo2", "PNo3", "PNo4"
        };

        public static byte[] Build()
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Maxsulot");
                for (int i = 0; i < Columns.Length; i++)
                    ws.Cell(1, i + 1).Value = Columns[i];

                // Namuna qator: Type bo'sh = tayyor mahsulot; XOMASHYO / YARIM TAYYOR ham mumkin
                ws.Cell(2, 1).Value = "MS-100";
                ws.Cell(2, 2).Value = "Namuna mahsulot";
                ws.Cell(2, 3).Value = "MAXSULOT";

                // PNo raqamdek ko'rinsa ham matn bo'lib qolsin (oldidagi nollar yo'qolmasin)
                ws.Column(1).Style.NumberFormat.Format = "@";
                ws.Range(1, 1, 1, Columns.Length).Style.Font.Bold = true;
                ws.Columns().AdjustToContents();
                return workbook.ToByteArray();
            }
        }
    }
}
