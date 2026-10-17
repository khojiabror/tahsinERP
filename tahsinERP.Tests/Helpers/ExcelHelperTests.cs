using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Helpers;
using tahsinERP.Services.Common;

namespace tahsinERP.Tests.Helpers
{
    [TestClass]
    public class ExcelHelperTests
    {
        private static XLWorkbook Reopen(byte[] bytes)
        {
            return new XLWorkbook(new MemoryStream(bytes));
        }

        [TestMethod]
        public void Export_Then_Import_RoundTrip_PreservesValues()
        {
            // Arrange: InventoryController.DownloadTemplate bilan bir xil shablon
            byte[] bytes;
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Template");
                ws.Cell(1, 1).Value = "ProductPNo";
                ws.Cell(1, 2).Value = "Quantity";
                ws.Cell(2, 1).Value = "00000001";
                ws.Cell(2, 2).Value = 10;
                ws.Range(1, 1, 1, 2).Style.Font.Bold = true;
                ws.Columns().AdjustToContents();
                bytes = workbook.ToByteArray();
            }

            // Act
            using (var workbook = Reopen(bytes))
            {
                var ws = workbook.Worksheet(1);

                // Assert
                Assert.AreEqual("Template", ws.Name);
                Assert.AreEqual(2, ws.LastRowNumber());
                Assert.AreEqual(2, ws.LastColumnNumber());
                Assert.AreEqual("ProductPNo", ws.Cell(1, 1).GetFormattedString());
                Assert.AreEqual("00000001", ws.Cell(2, 1).GetFormattedString());
                Assert.AreEqual("10", ws.Cell(2, 2).GetFormattedString());
                Assert.AreEqual(10d, ws.Cell(2, 2).GetRawValue());
                Assert.IsTrue(ws.Cell(1, 1).Style.Font.Bold);
            }
        }

        [TestMethod]
        public void GetRawValue_FollowsEpplusSemantics()
        {
            var date = new DateTime(2025, 11, 1);
            byte[] bytes;
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("S");
                ws.Cell(1, 1).Value = date;
                ws.Cell(1, 2).Value = "matn";
                ws.Cell(1, 3).Value = 12.5;
                bytes = workbook.ToByteArray();
            }

            using (var workbook = Reopen(bytes))
            {
                var ws = workbook.Worksheet(1);
                // Sana EPPlus'dagidek seriya raqami (double) bo'lib qaytadi
                Assert.AreEqual(date.ToOADate(), ws.Cell(1, 1).GetRawValue());
                Assert.AreEqual("matn", ws.Cell(1, 2).GetRawValue());
                Assert.AreEqual(12.5d, ws.Cell(1, 3).GetRawValue());
                // Bo'sh hujayra null
                Assert.IsNull(ws.Cell(5, 5).GetRawValue());
            }
        }

        [TestMethod]
        public void LastRowNumber_EmptySheet_ReturnsZero()
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Empty");
                Assert.AreEqual(0, ws.LastRowNumber());
                Assert.AreEqual(0, ws.LastColumnNumber());
            }
        }

        [TestMethod]
        public void ExcelCellReader_ReadsDatesNumbersAndText()
        {
            byte[] bytes;
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Waybills");
                ws.Cell(1, 1).Value = "Date";
                ws.Cell(1, 2).Value = "Mahsulot nomi";
                ws.Cell(1, 3).Value = "Summa";
                ws.Cell(2, 1).Value = new DateTime(2026, 3, 15);
                ws.Column(1).Style.NumberFormat.Format = "yyyy-mm-dd";
                ws.Cell(2, 2).Value = "Shakar";
                ws.Cell(2, 3).Value = 1875000.5;
                ws.Cell(3, 1).Value = "15.03.2026";
                ws.Cell(3, 3).Value = "1875000.5";
                bytes = workbook.ToByteArray();
            }

            using (var workbook = Reopen(bytes))
            {
                var cells = new ExcelCellReader(workbook.Worksheet(1)).Alias("Product", "mahsulot");

                Assert.AreEqual(new DateTime(2026, 3, 15), cells.Date(2, "Date"));
                Assert.AreEqual(new DateTime(2026, 3, 15), cells.Date(3, "Date"));
                Assert.IsNull(cells.Date(4, "Date"));
                Assert.AreEqual("Shakar", cells.String(2, "Product"));
                Assert.AreEqual(1875000.5d, cells.Double(2, "Summa"));
                Assert.AreEqual(1875000.5d, cells.Double(3, "Summa"));
                Assert.IsFalse(cells.Has("Yo'q"));
            }
        }

        [TestMethod]
        public void ExcelExport_Create_WritesHeaderRowsAndFormat()
        {
            var rows = new[]
            {
                new { Code = "USD", Rate = 12750.25m, Date = new DateTime(2026, 10, 1) },
                new { Code = "RUB", Rate = 131.4m, Date = new DateTime(2026, 10, 2) }
            };

            var bytes = ExcelExport.Create("Kurslar", rows,
                ExcelExport.Col<dynamic>("Kod", x => x.Code),
                ExcelExport.Col<dynamic>("Kurs", x => x.Rate, "#,##0.00"),
                ExcelExport.Col<dynamic>("Sana", x => x.Date, "dd.MM.yyyy"));

            using (var workbook = Reopen(bytes))
            {
                var ws = workbook.Worksheet("Kurslar");
                Assert.AreEqual("Kod", ws.Cell(1, 1).GetFormattedString());
                Assert.IsTrue(ws.Cell(1, 1).Style.Font.Bold);
                Assert.AreEqual(3, ws.LastRowNumber());
                Assert.AreEqual("USD", ws.Cell(2, 1).GetFormattedString());
                Assert.AreEqual(12750.25d, ws.Cell(2, 2).GetRawValue());
                Assert.AreEqual("#,##0.00", ws.Cell(2, 2).Style.NumberFormat.Format);
                Assert.AreEqual(new DateTime(2026, 10, 2).ToOADate(), ws.Cell(3, 3).GetRawValue());
                Assert.IsTrue(ws.AutoFilter.IsEnabled);
            }
        }
    }
}
