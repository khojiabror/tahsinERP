using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>
    /// Pilotga boshlang'ich ma'lumot: hududlar, narx turlari, marshrutlar (agent + tashrif kunlari), tovar birliklari.
    /// Qoidalar asosiy import bilan bir xil: avval hamma qator tekshiriladi, xato bo'lsa hech narsa yozilmaydi.
    /// </summary>
    public static partial class ImportService
    {
        // ---------- hududlar ----------

        public static readonly Column[] TerritoryColumns =
        {
            new Column("region", "Viloyat", "Область", true, "Toshkent sh."),
            new Column("regionRu", "Viloyat (rus)", "Область (рус)", false, "г. Ташкент"),
            new Column("district", "Tuman", "Район", false, "Chilonzor"),
            new Column("districtRu", "Tuman (rus)", "Район (рус)", false, "Чиланзар"),
            new Column("area", "Hudud", "Территория", false, "Chilonzor bozori"),
            new Column("areaRu", "Hudud (rus)", "Территория (рус)", false, "Чиланзарский рынок"),
        };

        /// <summary>Viloyat → tuman → hudud. Bor tugun nomi (uz yoki ru) bo'yicha topiladi, yo'g'i yaratiladi; takror yuklash xavfsiz.</summary>
        public static ImportResult ImportTerritories(Stream stream, Func<DistributionUnitOfWork> openUow, bool dryRun = false)
        {
            var result = new ImportResult();
            var sheet = Read(stream);
            var map = MapColumns(sheet, TerritoryColumns, result);
            if (!result.Ok) return result;

            var rows = new List<(int Row, (string Name, string NameRu)[] Path)>();
            foreach (var (row, cells) in sheet.Rows)
            {
                Collect(result, row, () =>
                {
                    var levels = new[] { ("region", "regionRu"), ("district", "districtRu"), ("area", "areaRu") }
                        .Select(l => (Name: Get(cells, map, l.Item1), NameRu: Get(cells, map, l.Item2))).ToArray();
                    int depth = levels.Count(l => l.Name != null);
                    // bo'sh oraliq taqiqlanadi: hudud tumansiz bo'lmaydi
                    if (levels[0].Name == null || levels.Take(depth).Any(l => l.Name == null))
                        throw new DstValidationException(DstLocalization.T("Import_TerritoryPath"));
                    foreach (var l in levels.Take(depth))
                        if (l.Name.Length > 100 || (l.NameRu?.Length ?? 0) > 100)
                            throw new DstValidationException(DstLocalization.T("Error_NameTooLong", 100));
                    rows.Add((row, levels.Take(depth).ToArray()));
                });
            }
            if (!result.Ok) return result;

            using (var uow = openUow())
            {
                var db = uow.Dst;
                var all = db.Territories.ToList();
                var touched = new HashSet<Territory>();
                foreach (var (row, path) in rows)
                {
                    int? parentId = null;
                    var action = ImportAction.Unchanged;
                    try
                    {
                        for (int i = 0; i < path.Length; i++)
                        {
                            var (name, nameRu) = path[i];
                            var same = all.Where(t => t.ParentID == parentId &&
                                (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(t.NameRu, name, StringComparison.OrdinalIgnoreCase))).ToList();
                            if (same.Count > 1) throw new DstValidationException(DstLocalization.T("Import_AmbiguousTerritory", name));
                            var node = same.FirstOrDefault();
                            if (node == null)
                            {
                                node = new Territory { ParentID = parentId, Level = (byte)(i + 1), Name = name, NameRu = nameRu };
                                db.Territories.Add(node);
                                db.SaveChanges();   // bolalari uchun ID kerak
                                all.Add(node);
                                touched.Add(node);
                                result.Created++;
                                action = ImportAction.New;
                            }
                            else if (!node.IsActive || (nameRu != null && node.NameRu != nameRu))
                            {
                                node.IsActive = true;
                                if (nameRu != null) node.NameRu = nameRu;
                                if (touched.Add(node)) result.Updated++;
                                if (action == ImportAction.Unchanged) action = ImportAction.Update;
                            }
                            parentId = node.ID;
                        }
                    }
                    catch (DstValidationException ex)
                    {
                        result.Errors.Add((row, ex.Message));
                        return result;   // commit qilinmaydi
                    }
                    if (action == ImportAction.Unchanged) result.Skipped++;
                    result.Plan(row, string.Join(" / ", path.Select(p => p.Name)), action);
                }
                db.SaveChanges();
                if (!dryRun) uow.Commit();
            }
            return result;
        }

        // ---------- narx turlari ----------

        public static readonly Column[] PriceTypeColumns =
        {
            new Column("name", "Nomi", "Наименование", true, "Ulgurji"),
            new Column("nameRu", "Nomi (rus)", "Наименование (рус)", false, "Оптовая"),
            new Column("default", "Standart (ha/yo'q)", "По умолчанию (да/нет)", false, "ha"),
            new Column("sort", "Tartib", "Порядок", false, "1"),
        };

        /// <summary>
        /// Narx turlari nomi (uz yoki ru) bo'yicha yangilanadi yoki yaratiladi. "Standart" — faqat bitta qatorda; bazada
        /// standart yo'q va faylda ham berilmagan bo'lsa birinchi qator standart bo'ladi (narx aniqlash unga tayanadi).
        /// </summary>
        public static ImportResult ImportPriceTypes(Stream stream, Func<DistributionUnitOfWork> openUow, bool dryRun = false)
        {
            var result = new ImportResult();
            var sheet = Read(stream);
            var map = MapColumns(sheet, PriceTypeColumns, result);
            if (!result.Ok) return result;

            var rows = new List<(int Row, string Name, string NameRu, bool? IsDefault, int? Sort)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (row, cells) in sheet.Rows)
            {
                Collect(result, row, () =>
                {
                    var name = Get(cells, map, "name");
                    var nameRu = Get(cells, map, "nameRu");
                    if (string.IsNullOrWhiteSpace(name)) throw new DstValidationException(DstLocalization.T("Error_NameRequired"));
                    if (name.Length > 50 || (nameRu?.Length ?? 0) > 50) throw new DstValidationException(DstLocalization.T("Error_NameTooLong", 50));
                    if (!seen.Add(name)) throw new DstValidationException(DstLocalization.T("Import_DuplicateInFile", name));
                    var sort = Dec(Get(cells, map, "sort"));
                    rows.Add((row, name, nameRu, ParseYesNo(Get(cells, map, "default")), sort.HasValue ? (int?)(int)sort.Value : null));
                });
            }
            if (rows.Count(r => r.IsDefault == true) > 1)
                result.Errors.Add((0, DstLocalization.T("Import_DefaultTwice")));
            if (!result.Ok) return result;

            using (var uow = openUow())
            {
                var db = uow.Dst;
                var types = db.PriceTypes.ToList();
                int nextSort = types.Count == 0 ? 1 : types.Max(t => t.SortOrder) + 1;
                PriceType makeDefault = null;
                foreach (var r in rows)
                {
                    var t = types.FirstOrDefault(x => string.Equals(x.Name, r.Name, StringComparison.OrdinalIgnoreCase)
                                                      || string.Equals(x.NameRu, r.Name, StringComparison.OrdinalIgnoreCase));
                    ImportAction action;
                    if (t == null)
                    {
                        t = new PriceType { Name = r.Name, NameRu = r.NameRu, SortOrder = r.Sort ?? nextSort++, IsActive = true };
                        db.PriceTypes.Add(t);
                        types.Add(t);
                        result.Created++;
                        action = ImportAction.New;
                    }
                    else
                    {
                        bool changed = !t.IsActive || (r.NameRu != null && t.NameRu != r.NameRu) || (r.Sort.HasValue && t.SortOrder != r.Sort);
                        t.IsActive = true;
                        if (r.NameRu != null) t.NameRu = r.NameRu;
                        if (r.Sort.HasValue) t.SortOrder = r.Sort.Value;
                        action = changed ? ImportAction.Update : ImportAction.Unchanged;
                        if (changed) result.Updated++; else result.Skipped++;
                    }
                    if (r.IsDefault == true) makeDefault = t;
                    result.Plan(r.Row, r.Name, action, r.IsDefault == true ? DstLocalization.T("Import_IsDefault") : null);
                }
                if (makeDefault == null && !types.Any(t => t.IsDefault && t.IsActive))
                {
                    makeDefault = types.First(t => string.Equals(t.Name, rows[0].Name, StringComparison.OrdinalIgnoreCase)
                                                   || string.Equals(t.NameRu, rows[0].Name, StringComparison.OrdinalIgnoreCase));
                    result.Preview[0].Details = DstLocalization.T("Import_IsDefault");
                }
                if (makeDefault != null && !makeDefault.IsDefault)
                {
                    foreach (var other in types.Where(t => t.IsDefault))
                        other.IsDefault = false;
                    db.SaveChanges();   // UNIQUE (IsDefault=1) indeksi: avval eskisini o'chiramiz
                    makeDefault.IsDefault = true;
                }
                db.SaveChanges();
                if (!dryRun) uow.Commit();
            }
            return result;
        }

        /// <summary>ha/yo'q, да/нет, 1/0, +/- ; bo'sh — null (o'zgarishsiz).</summary>
        public static bool? ParseYesNo(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            switch (Normalize(value).ToLowerInvariant())
            {
                case "ha": case "xa": case "yes": case "да": case "1": case "+": case "true":
                    return true;
                case "yo'q": case "yoq": case "no": case "нет": case "0": case "-": case "false":
                    return false;
                default:
                    throw new DstValidationException(DstLocalization.T("Import_UnknownValue", value));
            }
        }

        // ---------- marshrutlar ----------

        public static readonly Column[] RouteColumns =
        {
            new Column("customer", "Mijoz (STIR yoki nomi)", "Клиент (ИНН или название)", true, "301234567"),
            new Column("agent", "Agent (login)", "Агент (логин)", false, "agent1"),
            new Column("visitDays", "Tashrif kunlari (1-7)", "Дни визита (1-7)", false, "1,4"),
        };

        /// <summary>
        /// Marshrut = mijozning agenti va tashrif kunlari (Du=1 … Ya=7). Bo'sh katak — o'zgarishsiz, "-" — tozalash.
        /// Faqat mavjud distributsiya mijozlari.
        /// </summary>
        public static ImportResult ImportRoutes(Stream stream, Func<DistributionUnitOfWork> openUow, bool dryRun = false)
        {
            var result = new ImportResult();
            var sheet = Read(stream);
            var map = MapColumns(sheet, RouteColumns, result);
            if (!result.Ok) return result;

            using (var uow = openUow())
            {
                var db = uow.Dst;
                var customers = (from p in db.CustomerProfiles
                                 join c in db.Customers on p.CustomerID equals c.ID
                                 where c.IsDeleted != true
                                 select new { c.ID, c.Name, p.INN }).ToList();
                var agents = (from a in db.SalesAgents
                              join u in db.Users on a.UserID equals u.ID
                              where a.IsActive && !a.IsExpeditor
                              select new { u.Uname, a.UserID }).ToList()
                    .ToDictionary(a => a.Uname, a => a.UserID, StringComparer.OrdinalIgnoreCase);
                var names = agents.ToDictionary(a => a.Value, a => a.Key);

                var changes = new List<(int Row, int CustomerId, string Name, bool SetAgent, int? AgentId, byte? VisitDays)>();
                var seen = new HashSet<int>();
                foreach (var (row, cells) in sheet.Rows)
                {
                    Collect(result, row, () =>
                    {
                        var key = Get(cells, map, "customer")?.Trim();
                        var c = FindCustomer(customers, x => x.INN, x => x.Name, key);
                        if (!seen.Add(c.ID)) throw new DstValidationException(DstLocalization.T("Import_DuplicateInFile", key));
                        var agentText = Get(cells, map, "agent");
                        var daysText = Get(cells, map, "visitDays");
                        bool setAgent = agentText != null;
                        int? agentId = setAgent && agentText != "-" ? FindAgent(agents, agentText) : null;
                        byte? days = daysText == null ? (byte?)null : daysText == "-" ? (byte)0 : ParseVisitDays(daysText);
                        changes.Add((row, c.ID, c.Name, setAgent, agentId, days));
                    });
                }
                if (!result.Ok) return result;

                foreach (var ch in changes)
                {
                    var profile = db.CustomerProfiles.Find(ch.CustomerId);
                    bool changed = false;
                    if (ch.SetAgent && profile.AgentUserID != ch.AgentId) { profile.AgentUserID = ch.AgentId; changed = true; }
                    if (ch.VisitDays.HasValue && profile.VisitDays != ch.VisitDays.Value) { profile.VisitDays = ch.VisitDays.Value; changed = true; }
                    if (changed) result.Updated++; else result.Skipped++;
                    result.Plan(ch.Row, ch.Name, changed ? ImportAction.Update : ImportAction.Unchanged,
                        (profile.AgentUserID.HasValue && names.TryGetValue(profile.AgentUserID.Value, out var login) ? login : "—") + "; " + VisitDaysText(profile.VisitDays));
                }
                db.SaveChanges();
                if (!dryRun) uow.Commit();
            }
            return result;
        }

        /// <summary>Bitmask → "1,4" (ParseVisitDays teskarisi).</summary>
        public static string VisitDaysText(byte mask)
        {
            var days = Enumerable.Range(1, 7).Where(d => (mask & (1 << (d - 1))) != 0).ToList();
            return days.Count == 0 ? "—" : string.Join(",", days);
        }

        // ---------- tovar birliklari ----------

        public static readonly Column[] UnitColumns =
        {
            new Column("pno", "Artikul", "Артикул", true, "SEM-M400"),
            new Column("unit", "Birlik", "Единица", true, "poddon"),
            new Column("factor", "Koeffitsient", "Коэффициент", true, "40"),
            new Column("weight", "Og'irlik (kg)", "Вес (кг)", false, "2000"),
            new Column("volume", "Hajm (m3)", "Объём (м3)", false, "1.2"),
            new Column("barcode", "Shtrix-kod", "Штрихкод", false, "4780000000021"),
            new Column("default", "Odatiy sotish (ha/yo'q)", "Продажа по умолчанию (да/нет)", false, "yo'q"),
        };

        /// <summary>
        /// Mavjud tovarlarga qo'shimcha birliklar: bitta qator = tovar × birlik (Factor — asosiy birlikda). Bor birlik
        /// yangilanadi (nofaol bo'lsa faollashadi), shtrix-kod qo'shiladi. "Odatiy sotish" tovar bo'yicha faqat bittasida.
        /// </summary>
        public static ImportResult ImportUnits(Stream stream, Func<DistributionUnitOfWork> openUow, bool dryRun = false)
        {
            var result = new ImportResult();
            var sheet = Read(stream);
            var map = MapColumns(sheet, UnitColumns, result);
            if (!result.Ok) return result;

            using (var uow = openUow())
            {
                var db = uow.Dst;
                var units = db.Units.Where(u => u.IsDeleted != true).ToList();
                var products = db.Products.Where(p => !p.IsDeleted).Select(p => new { p.ID, p.PNo, p.UnitID }).ToList()
                    .GroupBy(p => p.PNo, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var barcodeOwners = db.ProductBarcodes.Select(b => new { b.Barcode, b.ProductID }).ToList()
                    .ToDictionary(b => b.Barcode, b => b.ProductID);

                var rows = new List<UnitRow>();
                var seenUnit = new HashSet<(int, int)>();
                var seenBarcode = new Dictionary<string, int>();
                var defaults = new HashSet<int>();
                foreach (var (row, cells) in sheet.Rows)
                {
                    Collect(result, row, () =>
                    {
                        var pno = Get(cells, map, "pno");
                        if (pno == null || !products.TryGetValue(pno.Trim(), out var product))
                            throw new DstValidationException(DstLocalization.T("Import_UnknownValue", pno));
                        var unitName = Get(cells, map, "unit") ?? throw new DstValidationException(DstLocalization.T("Import_UnitRequired"));
                        var factor = Dec(Get(cells, map, "factor")) ?? throw new DstValidationException(DstLocalization.T("Error_FactorPositive"));
                        var r = new UnitRow
                        {
                            Row = row, PNo = pno.Trim(), ProductId = product.ID, UnitId = FindUnit(units, unitName), Factor = factor,
                            Weight = Dec(Get(cells, map, "weight")), Volume = Dec(Get(cells, map, "volume")),
                            IsDefault = ParseYesNo(Get(cells, map, "default"))
                        };
                        UnitService.Validate(product.UnitID, r.UnitId, r.Factor, null);
                        if (r.Weight < 0 || r.Volume < 0) throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));
                        if (!seenUnit.Add((r.ProductId, r.UnitId))) throw new DstValidationException(DstLocalization.T("Error_UnitDuplicate"));
                        if (r.IsDefault == true && !defaults.Add(r.ProductId))
                            throw new DstValidationException(DstLocalization.T("Import_DefaultTwice"));
                        foreach (var code in SplitBarcodes(Get(cells, map, "barcode")))
                        {
                            if (seenBarcode.TryGetValue(code, out int otherProduct) && otherProduct != r.ProductId)
                                throw new DstValidationException(DstLocalization.T("Import_DuplicateInFile", code));
                            seenBarcode[code] = r.ProductId;
                            if (barcodeOwners.TryGetValue(code, out int owner) && owner != r.ProductId)
                                throw new DstValidationException(DstLocalization.T("Error_BarcodeTaken", owner));
                            r.Barcodes.Add(code);
                        }
                        rows.Add(r);
                    });
                }
                if (!result.Ok) return result;

                foreach (var r in rows)
                {
                    var existing = db.ProductUnits.Where(u => u.ProductID == r.ProductId).ToList();
                    var pu = existing.FirstOrDefault(u => u.UnitID == r.UnitId);
                    bool isNew = pu == null;
                    if (isNew) db.ProductUnits.Add(pu = new ProductUnit { ProductID = r.ProductId, UnitID = r.UnitId });
                    pu.Factor = r.Factor;
                    if (r.Weight.HasValue) pu.WeightKg = r.Weight;
                    if (r.Volume.HasValue) pu.VolumeM3 = r.Volume;
                    pu.IsActive = true;
                    if (r.IsDefault.HasValue) pu.IsDefaultSale = r.IsDefault.Value;
                    if (r.IsDefault == true)
                        foreach (var other in existing.Where(u => u != pu && u.IsDefaultSale))
                            other.IsDefaultSale = false;
                    db.SaveChanges();

                    foreach (var code in r.Barcodes.Where(c => !barcodeOwners.ContainsKey(c)))
                    {
                        db.ProductBarcodes.Add(new ProductBarcode { ProductID = r.ProductId, Barcode = code, ProductUnitID = pu.ID });
                        barcodeOwners[code] = r.ProductId;
                    }
                    db.SaveChanges();
                    if (isNew) result.Created++; else result.Updated++;
                    result.Plan(r.Row, r.PNo + " — " + units.First(u => u.ID == r.UnitId).ShortName, isNew ? ImportAction.New : ImportAction.Update,
                        "× " + r.Factor.ToString("0.####", CultureInfo.InvariantCulture) + (pu.IsDefaultSale ? ", " + DstLocalization.T("Import_IsDefault") : ""));
                }
                if (!dryRun) uow.Commit();
            }
            return result;
        }

        private sealed class UnitRow
        {
            public int Row { get; set; }
            public string PNo { get; set; }
            public int ProductId { get; set; }
            public int UnitId { get; set; }
            public decimal Factor { get; set; }
            public decimal? Weight { get; set; }
            public decimal? Volume { get; set; }
            public bool? IsDefault { get; set; }
            public List<string> Barcodes { get; } = new List<string>();
        }
    }
}
