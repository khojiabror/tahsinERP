using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using tahsinERP.Helpers;

namespace tahsinERP.Services.Common
{
    /// <summary>
    /// Oddiy jadvalni .xlsx ga eksport qilish (18 ta Export/Template amalidagi qo'lda yozilgan
    /// sarlavha/ustun kodining o'rniga). ClosedXML (MIT) asosida.
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

        public static byte[] Create<T>(string sheetName, IEnumerable<T> rows, params Column<T>[] columns)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName);

                for (int c = 0; c < columns.Length; c++)
                {
                    var cell = ws.Cell(1, c + 1);
                    cell.Value = columns[c].Header;
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(235, 241, 250);
                }

                int r = 2;
                foreach (var row in rows)
                {
                    for (int c = 0; c < columns.Length; c++)
                    {
                        ws.Cell(r, c + 1).Value = XLCellValue.FromObject(columns[c].Value(row));
                    }
                    r++;
                }

                for (int c = 0; c < columns.Length; c++)
                {
                    if (!string.IsNullOrEmpty(columns[c].Format) && r > 2)
                        ws.Range(2, c + 1, r - 1, c + 1).Style.NumberFormat.Format = columns[c].Format;
                }

                var used = ws.RangeUsed();
                if (used != null)
                {
                    ws.SheetView.FreezeRows(1);
                    used.SetAutoFilter();
                    ws.Columns(1, columns.Length).AdjustToContents(8.0, 60.0);
                }

                return workbook.ToByteArray();
            }
        }
    }
}
