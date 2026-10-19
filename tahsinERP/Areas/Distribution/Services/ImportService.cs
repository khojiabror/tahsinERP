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
    }

    /// <summary>
    /// Excel import (mijozlar, tovarlar, narxlar). Ustunlar sarlavha bo'yicha topiladi (o'zbekcha yoki ruscha —
    /// shablondagi kabi), tartib ahamiyatsiz. Avval BARCHA qatorlar tekshiriladi; xato bo'lsa — hech narsa
    /// yozilmaydi va qator raqami bilan xatolar ro'yxati qaytadi; xato bo'lmasa — bitta tranzaksiyada yoziladi.
    /// Spravochniklar foydalanuvchi ma'lumoti bo'yicha (nomi) moslanadi — bu tizim kodlari emas.
    /// </summary>
    public static class ImportService
    {
        public sealed class Column
        {
            public Column(string key, string uz, string ru, bool required = false)
            {
                Key = key; Uz = uz; Ru = ru; Required = required;
            }

            public string Key { get; }
            public string Uz { get; }
            public string Ru { get; }
            public bool Required { get; }
        }

        public static readonly Column[] CustomerColumns =
        {
            new Column("name", "Nomi", "Наименование", true),
            new Column("inn", "STIR", "ИНН"),
            new Column("phone", "Telefon", "Телефон"),
            new Column("phone2", "Telefon 2", "Телефон 2"),
            new Column("city", "Shahar", "Город"),
            new Column("address", "Manzil", "Адрес"),
            new Column("landmark", "Mo'ljal", "Ориентир"),
            new Column("owner", "Egasi", "Владелец"),
            new Column("contact", "Aloqa shaxsi", "Контактное лицо"),
            new Column("pointType", "Nuqta turi", "Тип точки"),
            new Column("category", "Toifa (A/B/C)", "Категория (A/B/C)"),
            new Column("territory", "Hudud", "Территория"),
            new Column("agent", "Agent (login)", "Агент (логин)"),
            new Column("priceType", "Narx turi", "Тип цены"),
            new Column("visitDays", "Tashrif kunlari (1-7)", "Дни визита (1-7)"),
            new Column("creditLimit", "Kredit limiti", "Кредитный лимит"),
            new Column("paymentDays", "Kechiktirish (kun)", "Отсрочка (дней)"),
            new Column("lat", "Kenglik", "Широта"),
            new Column("lng", "Uzunlik", "Долгота"),
        };

        public static readonly Column[] ProductColumns =
        {
            new Column("pno", "Artikul", "Артикул", true),
            new Column("name", "Nomi", "Наименование", true),
            new Column("nameRu", "Nomi (rus)", "Наименование (рус)"),
            new Column("category", "Kategoriya", "Категория"),
            new Column("unit", "Asosiy birlik", "Базовая единица"),
            new Column("brand", "Brend", "Бренд"),
            new Column("manufacturer", "Ishlab chiqaruvchi", "Производитель"),
            new Column("weight", "Og'irlik (kg)", "Вес (кг)"),
            new Column("volume", "Hajm (m3)", "Объём (м3)"),
            new Column("barcode", "Shtrix-kod", "Штрихкод"),
            new Column("unit2", "Birlik 2", "Единица 2"),
            new Column("factor2", "Koeffitsient 2", "Коэффициент 2"),
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
            return (header ?? "").Trim().Replace('’', '\'').Replace('ʻ', '\'').Replace('`', '\'');
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

                var changes = new List<(int ProductId, int TypeId, decimal Price, decimal? Min)>();
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
                            changes.Add((productId, type.ID, price.Value, min));
                        }
                    });
                }
                if (!result.Ok) return result;

                var pricing = new PricingService(db);
                var now = DateTime.Now;
                foreach (var c in changes)
                {
                    if (pricing.SetPrice(c.TypeId, c.ProductId, c.Price, c.Min, userId, now) != null) result.Updated++;
                    else result.Skipped++;
                }
                db.SaveChanges();
                if (!dryRun) uow.Commit();   // tekshirish rejimi: hammasi yoziladi va rollback
            }
            return result;
        }
    }
}
