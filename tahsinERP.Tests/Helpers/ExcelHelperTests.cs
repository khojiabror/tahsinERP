using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Controllers;
using tahsinERP.Helpers;

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
        public void WaybillImport_GetCellDate_ReadsDateAndTextCells()
        {
            var method = typeof(WaybillController).GetMethod("GetCellDate", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            byte[] bytes;
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Waybills");
                ws.Cell(1, 1).Value = "Date";
                ws.Cell(2, 1).Value = new DateTime(2026, 3, 15);
                ws.Column(1).Style.NumberFormat.Format = "yyyy-mm-dd";
                bytes = workbook.ToByteArray();
            }

            using (var workbook = Reopen(bytes))
            {
                var ws = workbook.Worksheet(1);
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "Date", 1 } };

                var dateValue = (DateTime?)method.Invoke(null, new object[] { ws, 2, map, "Date" });
                var blankValue = (DateTime?)method.Invoke(null, new object[] { ws, 3, map, "Date" });

                Assert.AreEqual(new DateTime(2026, 3, 15), dateValue);
                Assert.IsNull(blankValue);
            }
        }
    }
}
