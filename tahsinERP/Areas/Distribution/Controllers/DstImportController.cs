using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Excel import: boshlang'ich ma'lumot pilot tartibida (hududlar → narx turlari → mijozlar → marshrutlar → tovarlar →
    /// birliklar → narxlar → kirish qarzlari). Oqim: fayl → ko'rib chiqish (hammasi yoziladi va rollback) → "Import qilish"
    /// (o'sha fayl qayta tekshirilib yoziladi). Shablonlar shu yerdan.
    /// Ko'rib chiqilgan fayl sessiyaga yoziladi — standart ReadOnly sessiya (TahsinControllerFactory) uni saqlamasdi va
    /// "Import qilish" doim "fayl topilmadi" berardi; shuning uchun Required.
    /// </summary>
    [SessionState(System.Web.SessionState.SessionStateBehavior.Required)]
    public class DstImportController : DstControllerBase
    {
        public const string Territories = "territories";
        public const string PriceTypes = "pricetypes";
        public const string Customers = "customers";
        public const string Routes = "routes";
        public const string Products = "products";
        public const string Units = "units";
        public const string Prices = "prices";
        public const string Opening = "opening";

        /// <summary>Yuklash tartibi — sahifadagi tablar ham shu tartibda.</summary>
        public static readonly string[] Kinds = { Territories, PriceTypes, Customers, Routes, Products, Units, Prices, Opening };

        public const int MaxFileBytes = 10 * 1024 * 1024;

        private const string StagedKey = "DstImport.Staged";
        private static readonly TimeSpan StagedLifetime = TimeSpan.FromHours(2);

        public ActionResult Index(string kind)
        {
            ViewBag.Kind = Kinds.Contains(kind) ? kind : Territories;
            return View();
        }

        [HttpGet]
        public ActionResult Template(string kind)
        {
            byte[] bytes;
            switch (kind)
            {
                case Territories: bytes = ImportService.Template(ImportService.TerritoryColumns, IsRussian); break;
                case PriceTypes: bytes = ImportService.Template(ImportService.PriceTypeColumns, IsRussian); break;
                case Routes: bytes = ImportService.Template(ImportService.RouteColumns, IsRussian); break;
                case Products: bytes = ImportService.Template(ImportService.ProductColumns, IsRussian); break;
                case Units: bytes = ImportService.Template(ImportService.UnitColumns, IsRussian); break;
                case Opening: bytes = ImportService.Template(ImportService.OpeningColumns, IsRussian); break;
                case Prices:
                    using (var db = NewDb())
                    {
                        var headers = db.PriceTypes.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToList()
                            .SelectMany(t => new[] { StoredName(t.Name, t.NameRu), StoredName(t.Name, t.NameRu) + " min" });
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

        /// <summary>Fayl yuklash. dryRun (odatiy) — ko'rib chiqish; xatosiz bo'lsa fayl vaqtincha saqlanadi va tasdiqlash tugmasi chiqadi.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Upload(string kind, HttpPostedFileBase file, bool dryRun = false)
        {
            kind = Kinds.Contains(kind) ? kind : Territories;
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

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                bytes = ms.ToArray();
            }
            var result = Run(kind, bytes, dryRun);
            if (result != null && result.Ok && dryRun)
                ViewBag.StagedToken = Stage(kind, file.FileName, bytes);
            return View("Index");
        }

        /// <summary>Ko'rib chiqilgan faylni yozish. Fayl qayta tekshiriladi — oradagi o'zgarishlar ham hisobga olinadi.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Commit(string kind, string token)
        {
            kind = Kinds.Contains(kind) ? kind : Territories;
            ViewBag.Kind = kind;
            var staged = Session?[StagedKey] as StagedFile;
            if (staged == null || staged.Token != token || staged.Kind != kind || DateTime.UtcNow - staged.At > StagedLifetime)
            {
                ViewBag.Error = DstLocalization.T("Import_StagedExpired");
                return View("Index");
            }
            var result = Run(kind, staged.Bytes, dryRun: false);
            if (result != null && result.Ok) Session.Remove(StagedKey);
            return View("Index");
        }

        private ImportResult Run(string kind, byte[] bytes, bool dryRun)
        {
            try
            {
                ImportResult result;
                using (var stream = new MemoryStream(bytes))
                {
                    switch (kind)
                    {
                        case Territories: result = ImportService.ImportTerritories(stream, NewUnitOfWork, dryRun); break;
                        case PriceTypes: result = ImportService.ImportPriceTypes(stream, NewUnitOfWork, dryRun); break;
                        case Routes: result = ImportService.ImportRoutes(stream, NewUnitOfWork, dryRun); break;
                        case Products: result = ImportService.ImportProducts(stream, NewUnitOfWork, dryRun); break;
                        case Units: result = ImportService.ImportUnits(stream, NewUnitOfWork, dryRun); break;
                        case Prices: result = ImportService.ImportPrices(stream, CurrentUserId, NewUnitOfWork, dryRun); break;
                        case Opening: result = ImportService.ImportOpening(stream, CurrentUserId, NewUnitOfWork, dryRun); break;
                        default: result = ImportService.ImportCustomers(stream, CurrentUserId, NewUnitOfWork, dryRun); break;
                    }
                }
                result.DryRun = dryRun;
                ViewBag.Result = result;
                return result;
            }
            catch (Exception ex) when (!(ex is DstValidationException))
            {
                System.Diagnostics.Trace.TraceError("DstImport {0}: {1}", kind, ex);
                ViewBag.Error = DstLocalization.T("Import_ReadFailed");
                return null;
            }
        }

        /// <summary>Ko'rib chiqilgan fayl sessiyada (bittasi, 10 MB gacha) — vaqtinchalik papka va tozalash kerak emas.</summary>
        private string Stage(string kind, string fileName, byte[] bytes)
        {
            if (Session == null) return null;
            var staged = new StagedFile { Token = Guid.NewGuid().ToString("N"), Kind = kind, FileName = Path.GetFileName(fileName), Bytes = bytes, At = DateTime.UtcNow };
            Session[StagedKey] = staged;
            ViewBag.StagedFileName = staged.FileName;
            return staged.Token;
        }

        [Serializable]
        private sealed class StagedFile
        {
            public string Token { get; set; }
            public string Kind { get; set; }
            public string FileName { get; set; }
            public byte[] Bytes { get; set; }
            public DateTime At { get; set; }
        }
    }
}
