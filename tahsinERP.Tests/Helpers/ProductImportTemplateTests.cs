using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Helpers;

namespace tahsinERP.Tests.Helpers
{
    [TestClass]
    public class ProductImportTemplateTests
    {
        // Regressiya: Nomenklatura → Excel orqali yuklash → maxsulot.xlsx 404 qaytarardi
        // (shablon SAMPLE_FILES'dan o'qilardi, u yerda qator yo'q edi). Endi baza kerak emas.
        [TestMethod]
        public void Build_ReturnsWorkbook_WithColumnsSaveReads()
        {
            var bytes = ProductImportTemplate.Build();
            Assert.IsTrue(bytes.Length > 0);

            using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
            {
                var ws = workbook.Worksheet(1);
                var headers = Enumerable.Range(1, ws.LastColumnNumber())
                    .Select(c => ws.Cell(1, c).GetFormattedString())
                    .ToArray();

                CollectionAssert.AreEqual(ProductImportTemplate.Columns, headers);
                // ProductController.Save shu ustun nomlariga tayanadi
                CollectionAssert.IsSubsetOf(
                    new[] { "Partnumber", "Name", "Type", "Weight", "Length", "Width", "Height", "PNo2", "PNo3", "PNo4" },
                    headers);
            }
        }
    }
}
