using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Areas.Distribution.Services.Delivery
{
    /// <summary>Ovozli xabardan ajratilgan qator (bot: nutq → matn → tovar, miqdor, birlik).</summary>
    public sealed class VoiceItem
    {
        public string Text { get; set; }
        public decimal Qty { get; set; }
        public string Unit { get; set; }
    }

    public sealed class VoiceDraftInput
    {
        public int? CustomerID { get; set; }
        public string CustomerHint { get; set; }
        public string Transcript { get; set; }
        public List<VoiceItem> Items { get; set; } = new List<VoiceItem>();
    }

    public sealed class VoiceDraftLine
    {
        public string Text { get; set; }
        public int ProductID { get; set; }
        public int? ProductUnitID { get; set; }
        public string Product { get; set; }
        public string Unit { get; set; }
        public decimal Qty { get; set; }
        public decimal? Price { get; set; }
        public decimal? Amount { get; set; }
        public decimal Available { get; set; }
        public int Score { get; set; }
    }

    public sealed class VoiceDraft
    {
        public int? CustomerID { get; set; }
        public string Customer { get; set; }
        public List<(int Id, string Name)> CustomerOptions { get; set; } = new List<(int, string)>();
        public List<VoiceDraftLine> Lines { get; set; } = new List<VoiceDraftLine>();
        public List<string> Unmatched { get; set; } = new List<string>();
        public decimal Total => Lines.Sum(l => l.Amount ?? 0);
    }

    /// <summary>
    /// Matn bo'lagini katalog bilan moslash (sof funksiyalar): kichik harf, lotin/kirill "o'"/"g'" va tutuq belgilari bir xil,
    /// so'zlar bo'yicha qamrov; raqam va model belgilari (M400, 60x60) og'irroq.
    /// </summary>
    public static class VoiceMatch
    {
        private static readonly Dictionary<char, string> Cyr = new Dictionary<char, string>
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo", ['ж'] = "j", ['з'] = "z", ['и'] = "i",
            ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
            ['у'] = "u", ['ф'] = "f", ['х'] = "x", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sh", ['ъ'] = "", ['ы'] = "i", ['ь'] = "",
            ['э'] = "e", ['ю'] = "yu", ['я'] = "ya", ['ў'] = "o", ['қ'] = "q", ['ғ'] = "g", ['ҳ'] = "h"
        };

        /// <summary>Solishtirish uchun ko'rinish: kichik harf, kirill → lotin, tutuq va belgilar olib tashlanadi.</summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (var ch in s.ToLowerInvariant())
            {
                if (Cyr.TryGetValue(ch, out var lat)) sb.Append(lat);
                else if (char.IsLetterOrDigit(ch) && ch < 128) sb.Append(ch);
                else if (ch == '\'' || ch == '‘' || ch == '’' || ch == 'ʻ' || ch == 'ʼ' || ch == '`') continue;
                else sb.Append(' ');
            }
            return string.Join(" ", sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static string[] Tokens(string s) => Normalize(s).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>0–100: so'rov so'zlarining nechtasi nomda bor (prefiks ham hisob), raqamli so'zlar ikki baravar.</summary>
        public static int Score(string query, string name)
        {
            var q = Tokens(query).Where(t => t.Length > 1 || t.Any(char.IsDigit)).ToArray();
            if (q.Length == 0) return 0;
            var n = Tokens(name);
            double total = 0, hit = 0;
            foreach (var t in q)
            {
                double w = t.Any(char.IsDigit) ? 2 : 1;
                total += w;
                if (n.Any(x => x == t)) hit += w;
                else if (t.Length >= 3 && n.Any(x => x.StartsWith(t, StringComparison.Ordinal) || t.StartsWith(x, StringComparison.Ordinal) && x.Length >= 3)) hit += w * 0.7;
            }
            return (int)Math.Round(hit * 100 / total);
        }

        /// <summary>Birlik nomi (qop, quti, dona, kg, m2...) bo'yicha mos birlik; topilmasa — null.</summary>
        public static CatalogUnit PickUnit(IList<CatalogUnit> units, string unit)
        {
            var u = Normalize(unit);
            if (u.Length == 0) return null;
            var aliases = new Dictionary<string, string[]>
            {
                ["dona"] = new[] { "dona", "sht", "shtuk", "shtuka", "ta", "pcs" },
                ["qop"] = new[] { "qop", "meshok", "mesh", "qopda" },
                ["quti"] = new[] { "quti", "korobka", "karobka", "upak", "upakovka" },
                ["kg"] = new[] { "kg", "kilogramm", "kilo", "kilogram" },
                ["m2"] = new[] { "m2", "kv", "kvadrat", "metr kv", "kvm" },
                ["metr"] = new[] { "m", "metr", "metrov" },
                ["poddon"] = new[] { "poddon", "paddon", "palet" },
                ["litr"] = new[] { "l", "litr" }
            };
            foreach (var x in units)
            {
                var name = Normalize(x.Name);
                if (name == u || name.StartsWith(u, StringComparison.Ordinal) || u.StartsWith(name, StringComparison.Ordinal) && name.Length >= 2) return x;
            }
            var group = aliases.FirstOrDefault(a => a.Value.Contains(u) || a.Key == u);
            if (group.Key == null) return null;
            return units.FirstOrDefault(x => group.Value.Contains(Normalize(x.Name)) || Normalize(x.Name) == group.Key);
        }
    }

    /// <summary>Ovozli buyurtma qoralamasi: mijozni aniqlash (agent ko'rinishidagilardan) va har qatorni katalogdan topish.</summary>
    public static class VoiceOrderService
    {
        public const int MinScore = 50;
        public const int MaxItems = 30;

        public static VoiceDraft Draft(DistributionDbContext db, VoiceDraftInput input, IQueryable<int> visibleCustomers, int? warehouseId, DateTime now)
        {
            var draft = new VoiceDraft();
            if (input.CustomerID.HasValue)
            {
                if (!visibleCustomers.Contains(input.CustomerID.Value)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                draft.CustomerID = input.CustomerID;
            }
            else if (!string.IsNullOrWhiteSpace(input.CustomerHint))
            {
                var hint = input.CustomerHint.Trim();
                var words = VoiceMatch.Tokens(hint).Where(w => w.Length >= 3).Take(3).ToList();
                var candidates = (from c in db.Customers
                                  where visibleCustomers.Contains(c.ID) && c.IsDeleted != true
                                  select new { c.ID, c.Name }).ToList()
                    .Select(c => new { c.ID, c.Name, Score = VoiceMatch.Score(hint, c.Name) })
                    .Where(c => c.Score >= MinScore || words.Any(w => VoiceMatch.Normalize(c.Name).Contains(w)))
                    .OrderByDescending(c => c.Score).ThenBy(c => c.Name).Take(5).ToList();
                if (candidates.Count == 1 || candidates.Count > 1 && candidates[0].Score >= 80 && candidates[1].Score < candidates[0].Score)
                    draft.CustomerID = candidates[0].ID;
                else
                    draft.CustomerOptions = candidates.Select(c => (c.ID, c.Name)).ToList();
            }
            if (draft.CustomerID.HasValue)
                draft.Customer = db.Customers.Where(c => c.ID == draft.CustomerID).Select(c => c.Name).FirstOrDefault();

            List<ProductName> all = null;   // kerak bo'lsa bir marta yuklanadi (kirill/lotin farqi — SQL "Contains" topmaydi)
            foreach (var item in (input.Items ?? new List<VoiceItem>()).Where(i => i != null && !string.IsNullOrWhiteSpace(i.Text)).Take(MaxItems))
            {
                var best = Best(db, item.Text, draft.CustomerID, warehouseId, now);
                if (best == null)
                {
                    all = all ?? (from p in db.Products
                                  where !p.IsDeleted && !db.ProductProfiles.Any(pf => pf.ProductID == p.ID && !pf.IsSaleActive)
                                  select new ProductName
                                  {
                                      PNo = p.PNo, Name = p.Name,
                                      NameRu = db.ProductProfiles.Where(pf => pf.ProductID == p.ID).Select(pf => pf.NameRu).FirstOrDefault()
                                  }).Take(20000).ToList();
                    var guess = all.Select(p => new { p.PNo, Score = Math.Max(VoiceMatch.Score(item.Text, p.Name), VoiceMatch.Score(item.Text, p.NameRu ?? "")) })
                        .Where(x => x.Score >= MinScore).OrderByDescending(x => x.Score).FirstOrDefault();
                    if (guess != null)
                    {
                        var it = CatalogService.Search(db, guess.PNo, draft.CustomerID, warehouseId, null, now, 5).FirstOrDefault(x => x.PNo == guess.PNo);
                        if (it != null) best = new Candidate { Item = it, Score = guess.Score };
                    }
                }
                if (best == null || item.Qty <= 0)
                {
                    draft.Unmatched.Add(item.Text.Trim() + (item.Qty > 0 ? " — " + item.Qty.ToString("0.##", CultureInfo.InvariantCulture) : ""));
                    continue;
                }
                var unit = VoiceMatch.PickUnit(best.Item.Units, item.Unit) ?? best.Item.Units.FirstOrDefault(u => u.IsDefault) ?? best.Item.Units.First();
                decimal qty = Math.Round(item.Qty, 4);
                draft.Lines.Add(new VoiceDraftLine
                {
                    Text = item.Text.Trim(), ProductID = best.Item.Id, ProductUnitID = unit.ProductUnitId, Product = best.Item.PNo + " — " + best.Item.Name,
                    Unit = unit.Name, Qty = qty, Price = unit.Price, Amount = unit.Price.HasValue ? Math.Round(unit.Price.Value * qty, 2) : (decimal?)null,
                    Available = Math.Round(best.Item.Available / (unit.Factor <= 0 ? 1 : unit.Factor), 2), Score = best.Score
                });
            }
            return draft;
        }

        private sealed class ProductName
        {
            public string PNo { get; set; }
            public string Name { get; set; }
            public string NameRu { get; set; }
        }

        private sealed class Candidate
        {
            public CatalogItem Item { get; set; }
            public int Score { get; set; }
        }

        /// <summary>Katalogdan eng mos tovar: avval to'liq matn, keyin eng uzun so'z bo'yicha qidiruv, ball bo'yicha tanlov.</summary>
        private static Candidate Best(DistributionDbContext db, string text, int? customerId, int? warehouseId, DateTime now)
        {
            var pool = new Dictionary<int, CatalogItem>();
            foreach (var q in Queries(text))
                foreach (var it in CatalogService.Search(db, q, customerId, warehouseId, null, now, 15))
                    pool[it.Id] = it;
            return pool.Values
                .Select(it => new Candidate
                {
                    Item = it,
                    Score = Math.Max(VoiceMatch.Score(text, it.Name), Math.Max(VoiceMatch.Score(text, it.NameRu ?? ""), VoiceMatch.Score(text, it.PNo)))
                })
                .Where(c => c.Score >= MinScore)
                .OrderByDescending(c => c.Score).ThenByDescending(c => c.Item.Available > 0).ThenBy(c => c.Item.Name.Length)
                .FirstOrDefault();
        }

        /// <summary>Qidiruv so'rovlari: butun matn va eng uzun 2 so'z (katalog "Contains" qidiradi).</summary>
        internal static IEnumerable<string> Queries(string text)
        {
            var trimmed = text.Trim();
            yield return trimmed;
            foreach (var w in trimmed.Split(new[] { ' ', ',', '.', '-' }, StringSplitOptions.RemoveEmptyEntries)
                         .Where(w => w.Length >= 3 || w.Any(char.IsDigit)).OrderByDescending(w => w.Length).Take(2))
                if (!string.Equals(w, trimmed, StringComparison.OrdinalIgnoreCase)) yield return w;
        }
    }
}
