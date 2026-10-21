using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>Import natijasi: xato bo'lsa hech narsa yozilmaydi (hammasi yoki hech biri).</summary>
    public sealed class ImportResult
    {
        public List<(int Row, string Message)> Errors { get; } = new List<(int, string)>();
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public bool Ok => Errors.Count == 0;
        public bool DryRun { get; set; }

        /// <summary>Ko'rib chiqish: har qator bilan nima bo'ladi (yangi / yangilanadi / o'zgarishsiz).</summary>
        public List<ImportPreviewRow> Preview { get; } = new List<ImportPreviewRow>();

        public void Plan(int row, string key, ImportAction action, string details = null)
        {
            Preview.Add(new ImportPreviewRow { Row = row, Key = key, Action = action, Details = details });
        }
    }

    public enum ImportAction { New, Update, Unchanged }

    public sealed class ImportPreviewRow
    {
        public int Row { get; set; }
        public string Key { get; set; }
        public ImportAction Action { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// Excel import (mijozlar, tovarlar, narxlar). Ustunlar sarlavha bo'yicha topiladi (o'zbekcha yoki ruscha —
    /// shablondagi kabi), tartib ahamiyatsiz. Avval BARCHA qatorlar tekshiriladi; xato bo'lsa — hech narsa
    /// yozilmaydi va qator raqami bilan xatolar ro'yxati qaytadi; xato bo'lmasa — bitta tranzaksiyada yoziladi.
    /// Spravochniklar foydalanuvchi ma'lumoti bo'yicha (nomi) moslanadi — bu tizim kodlari emas.
    /// </summary>
    public static partial class ImportService
    {
        public sealed class Column
        {
            public Column(string key, string uz, string ru, bool required = false, string example = null)
            {
                Key = key; Uz = uz; Ru = ru; Required = required; Example = example;
            }

            public string Key { get; }
            public string Uz { get; }
            public string Ru { get; }
            public bool Required { get; }
            /// <summary>Shablonning "Namuna" varag'idagi misol qiymat.</summary>
            public string Example { get; }
        }

        public static readonly Column[] CustomerColumns =
        {
            new Column("name", "Nomi", "Наименование", true, "Baraka qurilish"),
            new Column("inn", "STIR", "ИНН", false, "301234567"),
            new Column("phone", "Telefon", "Телефон", false, "+998901234567"),
            new Column("phone2", "Telefon 2", "Телефон 2"),
            new Column("city", "Shahar", "Город", false, "Toshkent"),
            new Column("address", "Manzil", "Адрес", false, "Chilonzor 9-kvartal, 12"),
            new Column("landmark", "Mo'ljal", "Ориентир", false, "Bozor yonida"),
            new Column("owner", "Egasi", "Владелец"),
            new Column("contact", "Aloqa shaxsi", "Контактное лицо"),
            new Column("pointType", "Nuqta turi", "Тип точки", false, "SHOP"),
            new Column("category", "Toifa (A/B/C)", "Категория (A/B/C)", false, "B"),
            new Column("territory", "Hudud", "Территория", false, "Toshkent sh. / Chilonzor"),
            new Column("agent", "Agent (login)", "Агент (логин)", false, "agent1"),
            new Column("priceType", "Narx turi", "Тип цены", false, "Ulgurji"),
            new Column("visitDays", "Tashrif kunlari (1-7)", "Дни визита (1-7)", false, "1,4"),
            new Column("creditLimit", "Kredit limiti", "Кредитный лимит", false, "5000000"),
            new Column("paymentDays", "Kechiktirish (kun)", "Отсрочка (дней)", false, "14"),
            new Column("lat", "Kenglik", "Широта", false, "41.2856"),
            new Column("lng", "Uzunlik", "Долгота", false, "69.2034"),
        };

        public static readonly Column[] ProductColumns =
        {
            new Column("pno", "Artikul", "Артикул", true, "SEM-M400"),
            new Column("name", "Nomi", "Наименование", true, "Sement M400 50 kg"),
            new Column("nameRu", "Nomi (rus)", "Наименование (рус)", false, "Цемент М400 50 кг"),
            new Column("category", "Kategoriya", "Категория", false, "Qurilish aralashmalari"),
            new Column("unit", "Asosiy birlik", "Базовая единица", false, "qop"),
            new Column("brand", "Brend", "Бренд", false, "Qizilqumsement"),
            new Column("manufacturer", "Ishlab chiqaruvchi", "Производитель"),
            new Column("weight", "Og'irlik (kg)", "Вес (кг)", false, "50"),
            new Column("volume", "Hajm (m3)", "Объём (м3)"),
            new Column("barcode", "Shtrix-kod", "Штрихкод", false, "4780000000014"),
            new Column("unit2", "Birlik 2", "Единица 2", false, "poddon"),
            new Column("factor2", "Koeffitsient 2", "Коэффициент 2", false, "40"),
            new Column("barcode2", "Shtrix-kod 2", "Штрихкод 2"),
            new Column("unit3", "Birlik 3", "Единица 3"),
            new Column("factor3", "Koeffitsient 3", "Коэффициент 3"),
            new Column("barcode3", "Shtrix-kod 3", "Штрихкод 3"),
        };

        public const int ProductPNoMax = 15;    // dbo.PRODUCTS.PNo varchar(15)
        public const int ProductNameMax = 50;   // dbo.PRODUCTS.Name varchar(50)

        // ---------- shablonlar ----------

        public static byte[] Template(Column[] columns, bool russian, IEnumerable<string> extraHeaders = null)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Import");
                int col = 1;
                foreach (var c in columns)
                {
                    var cell = ws.Cell(1, col++);
                    cell.Value = russian ? c.Ru : c.Uz;
                    cell.Style.Font.Bold = true;
                    if (c.Required) cell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                }
                foreach (var h in extraHeaders ?? Enumerable.Empty<string>())
                {
                    var cell = ws.Cell(1, col++);
                    cell.Value = h;
                    cell.Style.Font.Bold = true;
                }
                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents();
                if (columns.Any(c => c.Example != null))
                {
                    // import faqat birinchi varaqni o'qiydi — namuna alohida varaqda, ma'lumotga aralashmaydi
                    var sample = wb.AddWorksheet(russian ? "Пример" : "Namuna");
                    for (int i = 0; i < columns.Length; i++)
                    {
                        sample.Cell(1, i + 1).Value = russian ? columns[i].Ru : columns[i].Uz;
                        sample.Cell(1, i + 1).Style.Font.Bold = true;
                        if (columns[i].Example != null) sample.Cell(2, i + 1).SetValue(columns[i].Example);
                    }
                    sample.Columns().AdjustToContents();
                }
                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        // ---------- o'qish ----------

        /// <summary>Varaq qatorlari: sarlavha → qiymat (matn). Bo'sh qatorlar tashlab ketiladi.</summary>
        public sealed class Sheet
        {
            public Dictionary<string, int> Headers { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public List<(int Row, Dictionary<int, string> Cells)> Rows { get; } = new List<(int, Dictionary<int, string>)>();
        }

        public static Sheet Read(Stream stream)
        {
            var sheet = new Sheet();
            using (var wb = new XLWorkbook(stream))
            {
                var ws = wb.Worksheets.First();
                var used = ws.RangeUsed();
                if (used == null) return sheet;
                int lastCol = used.LastColumn().ColumnNumber(), lastRow = used.LastRow().RowNumber();
                for (int c = 1; c <= lastCol; c++)
                {
                    var h = Normalize(ws.Cell(1, c).GetFormattedString());
                    if (h.Length > 0 && !sheet.Headers.ContainsKey(h)) sheet.Headers[h] = c;
                }
                for (int r = 2; r <= lastRow; r++)
                {
                    var cells = new Dictionary<int, string>();
                    for (int c = 1; c <= lastCol; c++)
                    {
                        var cell = ws.Cell(r, c);
                        string v = cell.DataType == XLDataType.Number
                            ? cell.GetDouble().ToString("R", CultureInfo.InvariantCulture)
                            : cell.GetFormattedString();
                        v = v?.Trim();
                        if (!string.IsNullOrEmpty(v)) cells[c] = v;
                    }
                    if (cells.Count > 0) sheet.Rows.Add((r, cells));
                }
            }
            return sheet;
        }

        private static string Normalize(string header)
        {
            return (header ?? "").Trim().Replace('’', '\'').Replace('‘', '\'').Replace('ʻ', '\'').Replace('ʼ', '\'').Replace('`', '\'');
        }

        /// <summary>Ustun → varaqdagi raqam (uz yoki ru sarlavha bo'yicha); majburiy ustun yo'q bo'lsa — xato.</summary>
        private static Dictionary<string, int> MapColumns(Sheet sheet, Column[] columns, ImportResult result)
        {
            var map = new Dictionary<string, int>();
            foreach (var c in columns)
            {
                if (sheet.Headers.TryGetValue(Normalize(c.Uz), out int idx) || sheet.Headers.TryGetValue(Normalize(c.Ru), out idx))
                    map[c.Key] = idx;
                else if (c.Required)
                    result.Errors.Add((1, DstLocalization.T("Import_MissingColumn", c.Uz + " / " + c.Ru)));
            }
            return map;
        }

        private static string Get(Dictionary<int, string> cells, Dictionary<string, int> map, string key)
        {
            return map.TryGetValue(key, out int c) && cells.TryGetValue(c, out var v) ? v : null;
        }

        private static decimal? Dec(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DstModelBinder.TryParseDecimal(value, out var d)) return d;
            throw new DstValidationException(DstLocalization.T("Error_NumberFormat") + ": " + value);
        }

        private static void Collect(ImportResult result, int row, Action action)
        {
            try { action(); }
            catch (DstValidationException ex) { result.Errors.Add((row, ex.Message)); }
        }

        // ---------- mijozlar ----------

        public static ImportResult ImportCustomers(Stream stream, int? userId, Func<DistributionUnitOfWork> openUow, bool dryRun = false)
        {
            var result = new ImportResult();
            var sheet = Read(stream);
            var map = MapColumns(sheet, CustomerColumns, result);
            if (!result.Ok) return result;

            using (var uow = openUow())
            {
                var db = uow.Dst;
                var territories = db.Territories.Where(t => t.IsActive).ToList();
                var agents = (from a in db.SalesAgents
                              join u in db.Users on a.UserID equals u.ID
                              where a.IsActive && !a.IsExpeditor
                              select new { u.Uname, a.UserID }).ToList()
                    .ToDictionary(a => a.Uname, a => a.UserID, StringComparer.OrdinalIgnoreCase);
                var priceTypes = db.PriceTypes.Where(t => t.IsActive).ToList();
                var byInn = db.CustomerProfiles.Where(p => p.INN != null).Select(p => new { p.INN, p.CustomerID }).ToList()
                    .GroupBy(p => p.INN).ToDictionary(g => g.Key, g => g.First().CustomerID);
                var byName = (from p in db.CustomerProfiles
                              join c in db.Customers on p.CustomerID equals c.ID
                              where c.IsDeleted != true
                              select new { c.Name, c.ID }).ToList()
                    .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Select(x => x.ID).ToList(), StringComparer.OrdinalIgnoreCase);

                var inputs = new List<(int Row, CustomerInput Input)>();
                var seenInn = new HashSet<string>();
                foreach (var (row, cells) in sheet.Rows)
                {
                    Collect(result, row, () =>
                    {
                        var input = new CustomerInput
                        {
                            Name = Get(cells, map, "name"),
                            INN = DirectoryRules.NormalizeInn(Get(cells, map, "inn")),
                            Telephone = Get(cells, map, "phone"),
                            Phone2 = Get(cells, map, "phone2"),
                            City = Get(cells, map, "city"),
                            Address = Get(cells, map, "address"),
                            Landmark = Get(cells, map, "landmark"),
                            OwnerName = Get(cells, map, "owner"),
                            ContactPersonName = Get(cells, map, "contact"),
                            PointType = ParsePointType(Get(cells, map, "pointType")),
                            Category = Get(cells, map, "category"),
                            TerritoryID = FindTerritory(territories, Get(cells, map, "territory")),
                            AgentUserID = FindAgent(agents, Get(cells, map, "agent")),
                            PriceTypeID = FindPriceType(priceTypes, Get(cells, map, "priceType")),
                            VisitDays = ParseVisitDays(Get(cells, map, "visitDays")),
                            CreditLimit = Dec(Get(cells, map, "creditLimit")) ?? 0,
                            PaymentDays = (int)(Dec(Get(cells, map, "paymentDays")) ?? 0),
                            Lat = Dec(Get(cells, map, "lat")),
                            Lng = Dec(Get(cells, map, "lng"))
                        };
                        if (string.IsNullOrWhiteSpace(input.Name)) throw new DstValidationException(DstLocalization.T("Error_NameRequired"));
                        // telefon/geo/limit qoidalari CustomerService'da ham tekshiriladi — bu yerda oldindan (yozishdan oldin)
                        DirectoryRules.NormalizePhone(input.Telephone);
                        DirectoryRules.NormalizePhone(input.Phone2);
                        DirectoryRules.NormalizeCategory(input.Category);
                        DirectoryRules.ValidateGeo(input.Lat, input.Lng);
                        DirectoryRules.ValidateCredit(input.CreditLimit, input.PaymentDays);
                        if (input.Name.Trim().Length > CustomerService.NameMaxLength)
                            throw new DstValidationException(DstLocalization.T("Error_NameTooLong", CustomerService.NameMaxLength));

                        if (input.INN != null)
                        {
                            if (!seenInn.Add(input.INN)) throw new DstValidationException(DstLocalization.T("Error_InnDuplicate"));
                            if (byInn.TryGetValue(input.INN, out int existing)) input.ID = existing;
                        }
                        if (input.ID == null && byName.TryGetValue(input.Name.Trim(), out var sameName))
                        {
                            if (sameName.Count > 1) throw new DstValidationException(DstLocalization.T("Import_AmbiguousName"));
                            input.ID = sameName[0];
                        }
                        inputs.Add((row, input));
                    });
                }
                if (!result.Ok) return result;

                foreach (var (row, input) in inputs)
                {
                    bool isNew = input.ID == null;
                    try
                    {
                        CustomerService.Save(uow, input, userId, AuditActor.Web, DateTime.Now);
                    }
                    catch (DstValidationException ex)
                    {
                        result.Errors.Add((row, ex.Message));
                        return result;   // tranzaksiya commit qilinmaydi
                    }
                    if (isNew) result.Created++; else result.Updated++;
                    result.Plan(row, input.Name.Trim() + (input.INN != null ? " (" + input.INN + ")" : ""), isNew ? ImportAction.New : ImportAction.Update);
                }
                if (!dryRun) uow.Commit();   // tekshirish rejimi: hammasi yoziladi va rollback
            }
            return result;
        }

        private static string ParsePointType(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return PointTypes.Shop;
            var v = value.Trim();
            var code = PointTypes.All.FirstOrDefault(p => string.Equals(p, v, StringComparison.OrdinalIgnoreCase)
                || string.Equals(DstLocalization.ResourceManager.GetString("PointType_" + p, DstLocalization.CultureFor("uz")), v, StringComparison.OrdinalIgnoreCase)
                || string.Equals(DstLocalization.ResourceManager.GetString("PointType_" + p, DstLocalization.CultureFor("ru")), v, StringComparison.OrdinalIgnoreCase));
            return code ?? throw new DstValidationException(DstLocalization.T("Import_UnknownValue", value));
        }

        private static int? FindTerritory(List<Territory> all, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            // "Viloyat / Tuman / Hudud" yo'li yoki faqat oxirgi nom
            var parts = value.Split('/').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            var leaf = parts.Last();
            var candidates = all.Where(t => string.Equals(t.Name, leaf, StringComparison.OrdinalIgnoreCase)
                                            || string.Equals(t.NameRu, leaf, StringComparison.OrdinalIgnoreCase)).ToList();
            if (parts.Length > 1)
            {
                var parentName = parts[parts.Length - 2];
                candidates = candidates.Where(t => all.Any(p => p.ID == t.ParentID &&
                    (string.Equals(p.Name, parentName, StringComparison.OrdinalIgnoreCase) || string.Equals(p.NameRu, parentName, StringComparison.OrdinalIgnoreCase)))).ToList();
            }
            if (candidates.Count == 1) return candidates[0].ID;
            throw new DstValidationException(DstLocalization.T(candidates.Count == 0 ? "Import_UnknownValue" : "Import_AmbiguousTerritory", value));
        }

        private static int? FindAgent(Dictionary<string, int> agents, string login)
        {
            if (string.IsNullOrWhiteSpace(login)) return null;
            return agents.TryGetValue(login.Trim(), out int id) ? id : throw new DstValidationException(DstLocalization.T("Import_UnknownValue", login));
        }

        private static int? FindPriceType(List<PriceType> types, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var t = types.FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(x.NameRu, name.Trim(), StringComparison.OrdinalIgnoreCase));
            return t?.ID ?? throw new DstValidationException(DstLocalization.T("Import_UnknownValue", name));
        }

        /// <summary>"1,3,5" (Du=1 … Ya=7) → bitmask.</summary>
        public static byte ParseVisitDays(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            byte mask = 0;
            foreach (var part in value.Split(new[] { ',', ';', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part, out int d) || d < 1 || d > 7)
                    throw new DstValidationException(DstLocalization.T("Import_UnknownValue", value));
                mask |= (byte)(1 << (d - 1));
            }
            return mask;
        }

        // ---------- tovarlar ----------

        public static ImportResult ImportProducts(Stream stream, Func<DistributionUnitOfWork> openUow, bool dryRun = false)
        {
            var result = new ImportResult();
            var sheet = Read(stream);
            var map = MapColumns(sheet, ProductColumns, result);
            if (!result.Ok) return result;

            using (var uow = openUow())
            {
                var db = uow.Dst;
                var units = db.Units.Where(u => u.IsDeleted != true).ToList();
                var categories = db.Categories.Where(c => c.IsDeleted != true).ToList();
                var brands = db.Brands.ToList();
                var manufacturers = db.Manufacturers.ToList();
                var products = db.Products.Where(p => !p.IsDeleted).Select(p => new { p.ID, p.PNo, p.UnitID }).ToList()
                    .GroupBy(p => p.PNo, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var barcodeOwners = db.ProductBarcodes.Select(b => new { b.Barcode, b.ProductID }).ToList()
                    .ToDictionary(b => b.Barcode, b => b.ProductID);
                int defaultShop = uow.Erp.SHOPS.Where(s => s.IsDeleted != true).OrderBy(s => s.ID).Select(s => (int?)s.ID).FirstOrDefault()
                                  ?? 0;

                var rows = new List<ProductRow>();
                var seenPno = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenBarcode = new Dictionary<string, string>();
                foreach (var (row, cells) in sheet.Rows)
                {
                    Collect(result, row, () =>
                    {
                        var r = new ProductRow { Row = row, PNo = Get(cells, map, "pno"), Name = Get(cells, map, "name"), NameRu = Get(cells, map, "nameRu") };
                        if (string.IsNullOrWhiteSpace(r.PNo) || string.IsNullOrWhiteSpace(r.Name))
                            throw new DstValidationException(DstLocalization.T("Import_PnoNameRequired"));
                        if (r.PNo.Length > ProductPNoMax) throw new DstValidationException(DstLocalization.T("Import_PnoTooLong", ProductPNoMax));
                        if (r.Name.Length > ProductNameMax) throw new DstValidationException(DstLocalization.T("Error_NameTooLong", ProductNameMax));
                        if (!seenPno.Add(r.PNo)) throw new DstValidationException(DstLocalization.T("Import_DuplicateInFile", r.PNo));

                        products.TryGetValue(r.PNo, out var existing);
                        r.ExistingId = existing?.ID;

                        var unitName = Get(cells, map, "unit");
                        if (unitName != null) r.UnitId = FindUnit(units, unitName);
                        else if (existing != null) r.UnitId = existing.UnitID;
                        else throw new DstValidationException(DstLocalization.T("Import_UnitRequired"));
                        if (existing != null && r.UnitId != existing.UnitID)
                            throw new DstValidationException(DstLocalization.T("Import_BaseUnitChange"));

                        var catName = Get(cells, map, "category");
                        if (catName != null)
                        {
                            var cat = categories.Where(c => string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase)).ToList();
                            if (cat.Count != 1) throw new DstValidationException(DstLocalization.T("Import_UnknownValue", catName));
                            r.CategoryId = cat[0].ID;
                        }
                        r.Brand = Get(cells, map, "brand");
                        r.Manufacturer = Get(cells, map, "manufacturer");
                        r.Weight = Dec(Get(cells, map, "weight"));
                        r.Volume = Dec(Get(cells, map, "volume"));
                        if (r.Weight < 0 || r.Volume < 0) throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));

                        r.Barcodes.AddRange(SplitBarcodes(Get(cells, map, "barcode")).Select(b => (b, (int?)null)));
                        for (int i = 2; i <= 3; i++)
                        {
                            var u = Get(cells, map, "unit" + i);
                            var f = Dec(Get(cells, map, "factor" + i));
                            if (u == null && f == null) continue;
                            if (u == null || f == null) throw new DstValidationException(DstLocalization.T("Import_UnitFactorPair", i));
                            int uid = FindUnit(units, u);
                            UnitService.Validate(r.UnitId, uid, f.Value, r.ExtraUnits.Select(x => x.UnitId));
                            r.ExtraUnits.Add((uid, f.Value));
                            r.Barcodes.AddRange(SplitBarcodes(Get(cells, map, "barcode" + i)).Select(b => (b, (int?)uid)));
                        }
                        foreach (var (code, _) in r.Barcodes)
                        {
                            if (seenBarcode.TryGetValue(code, out var otherPno) && !string.Equals(otherPno, r.PNo, StringComparison.OrdinalIgnoreCase))
                                throw new DstValidationException(DstLocalization.T("Import_DuplicateInFile", code));
                            seenBarcode[code] = r.PNo;
                            if (barcodeOwners.TryGetValue(code, out int owner) && owner != r.ExistingId)
                                throw new DstValidationException(DstLocalization.T("Error_BarcodeTaken", owner));
                        }
                        rows.Add(r);
                    });
                }
                if (rows.Any(r => r.ExistingId == null) && defaultShop == 0)
                    result.Errors.Add((0, DstLocalization.T("Import_NoShop")));
                if (!result.Ok) return result;

                foreach (var r in rows)
                {
                    int productId;
                    if (r.ExistingId.HasValue)
                    {
                        productId = r.ExistingId.Value;
                        var p = uow.Erp.PRODUCTS.Find(productId);
                        p.Name = r.Name;
                        if (r.CategoryId.HasValue) p.CategoryID = r.CategoryId;
                        result.Updated++;
                    }
                    else
                    {
                        var p = new PRODUCT
                        {
                            PNo = r.PNo, Name = r.Name, UnitID = r.UnitId, CategoryID = r.CategoryId, ShopID = defaultShop,
                            IsDeleted = false, IsFinishedGood = true, IsComponent = false, IsRawMaterial = false
                        };
                        uow.Erp.PRODUCTS.Add(p);
                        uow.Erp.SaveChanges();
                        productId = p.ID;
                        result.Created++;
                    }

                    var profile = db.ProductProfiles.Find(productId);
                    if (profile == null) db.ProductProfiles.Add(profile = new ProductProfile { ProductID = productId });
                    if (r.NameRu != null) profile.NameRu = r.NameRu;
                    if (r.Brand != null) profile.BrandID = GetOrAdd(db, brands, r.Brand);
                    if (r.Manufacturer != null) profile.ManufacturerID = GetOrAdd(db, manufacturers, r.Manufacturer);
                    if (r.Weight.HasValue) profile.BaseWeightKg = r.Weight;
                    if (r.Volume.HasValue) profile.BaseVolumeM3 = r.Volume;

                    var existingUnits = db.ProductUnits.Where(u => u.ProductID == productId).ToList();
                    var unitRows = new Dictionary<int, ProductUnit>();
                    foreach (var (unitId, factor) in r.ExtraUnits)
                    {
                        var pu = existingUnits.FirstOrDefault(u => u.UnitID == unitId);
                        if (pu == null) db.ProductUnits.Add(pu = new ProductUnit { ProductID = productId, UnitID = unitId });
                        pu.Factor = factor;
                        pu.IsActive = true;
                        unitRows[unitId] = pu;
                    }
                    uow.Erp.SaveChanges();
                    db.SaveChanges();

                    foreach (var (code, unitId) in r.Barcodes)
                    {
                        if (barcodeOwners.ContainsKey(code)) continue;   // shu tovarniki — o'zgarishsiz
                        db.ProductBarcodes.Add(new ProductBarcode
                        {
                            ProductID = productId, Barcode = code, ProductUnitID = unitId.HasValue ? unitRows[unitId.Value].ID : (int?)null
                        });
                        barcodeOwners[code] = productId;
                    }
                    db.SaveChanges();
                    result.Plan(r.Row, r.PNo + " — " + r.Name, r.ExistingId.HasValue ? ImportAction.Update : ImportAction.New,
                        string.Join(", ", r.ExtraUnits.Select(u => units.First(x => x.ID == u.UnitId).ShortName + " = " + u.Factor.ToString("0.####", CultureInfo.InvariantCulture))));
                }
                if (!dryRun) uow.Commit();   // tekshirish rejimi: hammasi yoziladi va rollback
            }
            return result;
        }

        private sealed class ProductRow
        {
            public int Row { get; set; }
            public string PNo { get; set; }
            public string Name { get; set; }
            public string NameRu { get; set; }
            public int? ExistingId { get; set; }
            public int UnitId { get; set; }
            public int? CategoryId { get; set; }
            public string Brand { get; set; }
            public string Manufacturer { get; set; }
            public decimal? Weight { get; set; }
            public decimal? Volume { get; set; }
            public List<(int UnitId, decimal Factor)> ExtraUnits { get; } = new List<(int, decimal)>();
            public List<(string Code, int? UnitId)> Barcodes { get; } = new List<(string, int?)>();
        }

        private static IEnumerable<string> SplitBarcodes(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Enumerable.Empty<string>();
            return value.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(DirectoryRules.NormalizeBarcode);
        }

        private static int FindUnit(List<UnitRef> units, string value)
        {
            var v = value.Trim();
            var found = units.Where(u => string.Equals(u.ShortName, v, StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(u.UnitName, v, StringComparison.OrdinalIgnoreCase)).ToList();
            if (found.Count == 1) return found[0].ID;
            throw new DstValidationException(DstLocalization.T("Import_UnknownValue", value));
        }

        private static int GetOrAdd(DistributionDbContext db, List<Brand> list, string name)
        {
            var b = list.FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (b == null)
            {
                list.Add(b = new Brand { Name = name.Trim() });
                db.Brands.Add(b);
                db.SaveChanges();
            }
            return b.ID;
        }

        private static int GetOrAdd(DistributionDbContext db, List<Manufacturer> list, string name)
        {
            var m = list.FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (m == null)
            {
                list.Add(m = new Manufacturer { Name = name.Trim() });
                db.Manufacturers.Add(m);
                db.SaveChanges();
            }
            return m.ID;
        }

        // ---------- narxlar ----------

        /// <summary>
        /// Narxlar: "Artikul" + har faol narx turi uchun ustun (sarlavha = narx turi nomi) va ixtiyoriy
        /// "&lt;nom&gt; min" ustuni. Bo'sh katak — o'zgarishsiz.
        /// </summary>
        public static ImportResult ImportPrices(Stream stream, int? userId, Func<DistributionUnitOfWork> openUow, bool dryRun = false)
        {
            var result = new ImportResult();
            var sheet = Read(stream);
            int pnoCol;
            if (!sheet.Headers.TryGetValue("Artikul", out pnoCol) && !sheet.Headers.TryGetValue("Артикул", out pnoCol))
            {
                result.Errors.Add((1, DstLocalization.T("Import_MissingColumn", "Artikul / Артикул")));
                return result;
            }

            using (var uow = openUow())
            {
                var db = uow.Dst;
                var types = db.PriceTypes.Where(t => t.IsActive).ToList();
                var typeCols = new List<(PriceType Type, int Col, int? MinCol)>();
                foreach (var t in types)
                {
                    int col;
                    if (!sheet.Headers.TryGetValue(t.Name, out col) && (t.NameRu == null || !sheet.Headers.TryGetValue(t.NameRu, out col)))
                        continue;
                    int minCol;
                    int? min = sheet.Headers.TryGetValue(t.Name + " min", out minCol) || (t.NameRu != null && sheet.Headers.TryGetValue(t.NameRu + " min", out minCol))
                        ? minCol : (int?)null;
                    typeCols.Add((t, col, min));
                }
                if (typeCols.Count == 0)
                {
                    result.Errors.Add((1, DstLocalization.T("Import_NoPriceColumns")));
                    return result;
                }

                var products = db.Products.Where(p => !p.IsDeleted).Select(p => new { p.ID, p.PNo }).ToList()
                    .GroupBy(p => p.PNo, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().ID, StringComparer.OrdinalIgnoreCase);

                var changes = new List<(int Row, string PNo, int ProductId, PriceType Type, decimal Price, decimal? Min)>();
                foreach (var (row, cells) in sheet.Rows)
                {
                    Collect(result, row, () =>
                    {
                        cells.TryGetValue(pnoCol, out var pno);
                        if (string.IsNullOrWhiteSpace(pno) || !products.TryGetValue(pno.Trim(), out int productId))
                            throw new DstValidationException(DstLocalization.T("Import_UnknownValue", pno));
                        foreach (var (type, col, minCol) in typeCols)
                        {
                            cells.TryGetValue(col, out var priceText);
                            var price = Dec(priceText);
                            if (price == null) continue;
                            string minText = null;
                            if (minCol.HasValue) cells.TryGetValue(minCol.Value, out minText);
                            var min = Dec(minText);
                            if (price < 0) throw new DstValidationException(DstLocalization.T("Error_PriceNegative"));
                            if (min.HasValue && (min < 0 || min > price)) throw new DstValidationException(DstLocalization.T("Error_MinPriceRange"));
                            changes.Add((row, pno.Trim(), productId, type, price.Value, min));
                        }
                    });
                }
                if (!result.Ok) return result;

                var pricing = new PricingService(db);
                var now = DateTime.Now;
                foreach (var c in changes)
                {
                    bool changed = pricing.SetPrice(c.Type.ID, c.ProductId, c.Price, c.Min, userId, now) != null;
                    if (changed) result.Updated++;
                    else result.Skipped++;
                    result.Plan(c.Row, c.PNo, changed ? ImportAction.Update : ImportAction.Unchanged,
                        c.Type.Name + ": " + Money(c.Price) + (c.Min.HasValue ? " (min " + Money(c.Min.Value) + ")" : ""));
                }
                db.SaveChanges();
                if (!dryRun) uow.Commit();   // tekshirish rejimi: hammasi yoziladi va rollback
            }
            return result;
        }
    

        // ---------- kirish qoldiqlari (mijoz qarzlari) ----------

        public static readonly Column[] OpeningColumns =
        {
            new Column("customer", "Mijoz (STIR yoki nomi)", "Клиент (ИНН или название)", true, "301234567"),
            new Column("amount", "Qarz (+) / avans (-)", "Долг (+) / аванс (-)", true, "2500000"),
            new Column("due", "To'lov muddati", "Срок оплаты", false, "15.11.2026"),
        };

        /// <summary>
        /// Tizimga o'tishdagi qarzlar: har mijozga bitta OPENING yozuvi (takror import rad etiladi). Musbat — mijoz qarzi
        /// (debet, muddati bilan), manfiy — avans (kredit).
        /// </summary>
        public static ImportResult ImportOpening(Stream stream, int? userId, Func<DistributionUnitOfWork> openUow, bool dryRun = false)
        {
            var result = new ImportResult();
            var sheet = Read(stream);
            var map = MapColumns(sheet, OpeningColumns, result);
            if (!result.Ok) return result;

            using (var uow = openUow())
            {
                var db = uow.Dst;
                var customers = (from p in db.CustomerProfiles
                                 join c in db.Customers on p.CustomerID equals c.ID
                                 where c.IsDeleted != true
                                 select new { c.ID, c.Name, p.INN, p.AgentUserID, p.PaymentDays }).ToList();
                var already = new HashSet<int>(Services.Money.DebtService.LiveOpening(db).Select(l => l.CustomerID));   // bekor qilingani — qayta import mumkin
                var seen = new HashSet<int>();
                var entries = new List<CustomerLedgerEntry>();
                var now = DateTime.Now;

                foreach (var (row, cells) in sheet.Rows)
                {
                    Collect(result, row, () =>
                    {
                        var key = Get(cells, map, "customer")?.Trim();
                        var c0 = FindCustomer(customers, c => c.INN, c => c.Name, key);
                        if (already.Contains(c0.ID)) throw new DstValidationException(DstLocalization.T("Import_OpeningExists"));
                        if (!seen.Add(c0.ID)) throw new DstValidationException(DstLocalization.T("Import_DuplicateInFile", key));
                        var amount = Dec(Get(cells, map, "amount")) ?? 0;
                        if (amount == 0) return;
                        DateTime? due = null;
                        var dueText = Get(cells, map, "due");
                        if (!string.IsNullOrWhiteSpace(dueText))
                        {
                            if (DateTime.TryParse(dueText, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out var d)) due = d.Date;
                            else if (double.TryParse(dueText, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial)) due = DateTime.FromOADate(serial).Date;
                            else throw new DstValidationException(DstLocalization.T("Import_UnknownValue", dueText));
                        }
                        entries.Add(new CustomerLedgerEntry
                        {
                            CustomerID = c0.ID, EntryDate = now, DocType = LedgerDocTypes.Opening,
                            Debit = amount > 0 ? amount : 0, Credit = amount < 0 ? -amount : 0,
                            DueDate = amount > 0 ? (due ?? now.Date.AddDays(c0.PaymentDays)) : (DateTime?)null,
                            AgentUserID = c0.AgentUserID, CreatedBy = userId, Note = DstLocalization.T("Ledger_OPENING")
                        });
                        result.Plan(row, c0.Name, ImportAction.New, (amount > 0 ? "+" : "") + Money(amount));
                    });
                }
                if (!result.Ok) return result;

                db.CustomerLedger.AddRange(entries);
                db.SaveChanges();
                result.Created = entries.Count;
                result.Skipped = sheet.Rows.Count - entries.Count;
                if (!dryRun) uow.Commit();
            }
            return result;
        }

        /// <summary>Mijoz: avval STIR, keyin nomi bo'yicha (bittadan ortiq — xato).</summary>
        private static T FindCustomer<T>(List<T> customers, Func<T, string> inn, Func<T, string> name, string key)
        {
            var matches = customers.Where(c => inn(c) == key).ToList();
            if (matches.Count == 0) matches = customers.Where(c => string.Equals(name(c), key, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1)
                throw new DstValidationException(DstLocalization.T(matches.Count == 0 ? "Import_UnknownValue" : "Import_AmbiguousName", key));
            return matches[0];
        }

        private static string Money(decimal value) => value.ToString("#,0.##", CultureInfo.InvariantCulture);
    }
}