using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Excel import: mijozlar, tovarlar (birlik va shtrix-kodlar bilan), narxlar. Shablonlar shu yerdan.</summary>
    public class DstImportController : DstControllerBase
    {
        public const string Customers = "customers";
        public const string Products = "products";
        public const string Prices = "prices";

        public const int MaxFileBytes = 10 * 1024 * 1024;

        public ActionResult Index(string kind)
        {
            ViewBag.Kind = new[] { Customers, Products, Prices }.Contains(kind) ? kind : Customers;
            return View();
        }

        [HttpGet]
        public ActionResult Template(string kind)
        {
            byte[] bytes;
            switch (kind)
            {
                case Products:
                    bytes = ImportService.Template(ImportService.ProductColumns, IsRussian);
                    break;
                case Prices:
                    using (var db = NewDb())
                    {
                        var headers = db.PriceTypes.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToList()
                            .SelectMany(t => new[] { Localized(t.Name, t.NameRu), Localized(t.Name, t.NameRu) + " min" });
                        bytes = ImportService.Template(new[] { new ImportService.Column("pno", "Artikul", "Артикул", true) }, IsRussian, headers);
                    }
                    break;
                default:
                    kind = Customers;
                    bytes = ImportService.Template(ImportService.CustomerColumns, IsRussian);
                    break;
            }
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "dst_" + kind + "_template.xlsx");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Upload(string kind, HttpPostedFileBase file, bool dryRun = false)
        {
            kind = new[] { Customers, Products, Prices }.Contains(kind) ? kind : Customers;
            ViewBag.Kind = kind;
            if (file == null || file.ContentLength == 0 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = DstLocalization.T("Import_XlsxOnly");
                return View("Index");
            }
            if (file.ContentLength > MaxFileBytes)
            {
                ViewBag.Error = DstLocalization.T("Import_TooLarge");
                return View("Index");
            }

            try
            {
                ImportResult result;
                switch (kind)
                {
                    case Products:
                        result = ImportService.ImportProducts(file.InputStream, NewUnitOfWork, dryRun);
                        break;
                    case Prices:
                        result = ImportService.ImportPrices(file.InputStream, CurrentUserId, NewUnitOfWork, dryRun);
                        break;
                    default:
                        result = ImportService.ImportCustomers(file.InputStream, CurrentUserId, NewUnitOfWork, dryRun);
                        break;
                }
                result.DryRun = dryRun;
                ViewBag.Result = result;
            }
            catch (Exception ex) when (!(ex is DstValidationException))
            {
                System.Diagnostics.Trace.TraceError("DstImport {0}: {1}", kind, ex);
                ViewBag.Error = DstLocalization.T("Import_ReadFailed");
            }
            return View("Index");
        }
    }
}
