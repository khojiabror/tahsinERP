using System;
using System.Collections.Generic;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace tahsinERP.Services.Common
{
    /// <summary>
    /// Oddiy jadvalni .xlsx ga eksport qilish (18 ta Export/Template amalidagi qo'lda yozilgan
    /// sarlavha/ustun kodining o'rniga). EPPlus litsenziyasi Application_Start'da bir marta o'rnatiladi.
    /// <code>
    /// var bytes = ExcelExport.Create("Valyutalar", rows,
    ///     ExcelExport.Col&lt;CURRENCy&gt;("Kod", x => x.Shortname),
    ///     ExcelExport.Col&lt;CURRENCy&gt;("Nomi", x => x.Longname));
    /// return File(bytes, ExcelExport.ContentType, "currencies.xlsx");
    /// </code>
    /// </summary>
    public static class ExcelExport
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public sealed class Column<T>
        {
            public string Header { get; set; }
            public Func<T, object> Value { get; set; }
            /// <summary>Excel raqam formati, masalan "#,##0.00" yoki "dd.MM.yyyy".</summary>
            public string Format { get; set; }
        }

        public static Column<T> Col<T>(string header, Func<T, object> value, string format = null)
        {
            return new Column<T> { Header = header, Value = value, Format = format };
        }

        /// <summary>EPPlus litsenziya konteksti (Global.asax'dan bir marta chaqiriladi).</summary>
        public static void Configure()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public static byte[] Create<T>(string sheetName, IEnumerable<T> rows, params Column<T>[] columns)
        {
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName);

                for (int c = 0; c < columns.Length; c++)
                {
                    var cell = ws.Cells[1, c + 1];
                    cell.Value = columns[c].Header;
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(235, 241, 250));
                }

                int r = 2;
                foreach (var row in rows)
                {
                    for (int c = 0; c < columns.Length; c++)
                    {
                        ws.Cells[r, c + 1].Value = columns[c].Value(row);
                    }
                    r++;
                }

                for (int c = 0; c < columns.Length; c++)
                {
                    if (!string.IsNullOrEmpty(columns[c].Format) && r > 2)
                        ws.Cells[2, c + 1, r - 1, c + 1].Style.Numberformat.Format = columns[c].Format;
                }

                if (ws.Dimension != null)
                {
                    ws.View.FreezePanes(2, 1);
                    ws.Cells[ws.Dimension.Address].AutoFilter = true;
                    ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 60);
                }

                return package.GetAsByteArray();
            }
        }
    }
}
