using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>
    /// Pilot mashqi uchun namunaviy kompaniya (qurilish mollari ulgurji savdosi): 5 agent, 40 savdo nuqtasi Toshkentda,
    /// 24 tovar (qo'shimcha birliklar bilan), narxlar, kirish qarzlari, boshlang'ich qoldiq. Import shablonlari shaklida —
    /// xuddi foydalanuvchi to'ldiradigan fayllar (Docs/Distribution/pilot-sample/ ga ham yoziladi).
    /// </summary>
    internal static class PilotSampleData
    {
        public static readonly string[] Agents = { "agent1", "agent2", "agent3", "agent4", "agent5" };
        public static readonly string[] AgentNames = { "Akmal Rasulov", "Bekzod Tursunov", "Dilshod Karimov", "Farrux Yusupov", "Jasur Aliyev" };

        public sealed class Product
        {
            public string PNo, Name, NameRu, Category, Unit, Brand, Unit2;
            public decimal Weight, Factor2, Cost, Wholesale, Retail, OpeningQty;
        }

        public sealed class Customer
        {
            public string Name, Inn, Phone, Territory, Agent, PriceType, VisitDays, Category;
            public decimal Limit, Lat, Lng, Opening;
            public int PaymentDays;
        }

        public static readonly (string Region, string RegionRu, string District, string DistrictRu, string Area)[] Territories =
        {
            ("Toshkent sh.", "г. Ташкент", "Chilonzor", "Чиланзар", "Chilonzor bozori"),
            ("Toshkent sh.", "г. Ташкент", "Chilonzor", "Чиланзар", "Qatortol"),
            ("Toshkent sh.", "г. Ташкент", "Yunusobod", "Юнусабад", "Yunusobod bozori"),
            ("Toshkent sh.", "г. Ташкент", "Sergeli", "Сергели", "Sergeli qurilish bozori"),
            ("Toshkent sh.", "г. Ташкент", "Yashnobod", "Яшнабад", "Jomiy bozori"),
            ("Toshkent sh.", "г. Ташкент", "Olmazor", "Алмазар", "Qorasaroy"),
        };

        // markaz koordinatalari — mijozlar shu atrofida
        private static readonly (decimal Lat, decimal Lng)[] AreaCenters =
        {
            (41.2858m, 69.2040m), (41.2720m, 69.1870m), (41.3640m, 69.2860m), (41.2260m, 69.2190m), (41.2950m, 69.3330m), (41.3420m, 69.2140m)
        };

        public static readonly Product[] Products =
        {
            P("QM-SEM400", "Sement M400 50 kg", "Цемент М400 50 кг", "Sement va aralashmalar", "dona", "Qizilqumsement", 50, "tn", 20, 52000, 60000, 66000, 800),
            P("QM-SEM500", "Sement M500 50 kg", "Цемент М500 50 кг", "Sement va aralashmalar", "dona", "Ohangaronsement", 50, "tn", 20, 58000, 67000, 74000, 600),
            P("QM-GIPS30", "Gips shtukaturka 30 kg", "Гипсовая штукатурка 30 кг", "Sement va aralashmalar", "dona", "Knauf", 30, null, 0, 61000, 72000, 79000, 300),
            P("QM-YEL25", "Plitka yelimi 25 kg", "Плиточный клей 25 кг", "Sement va aralashmalar", "dona", "Ceresit", 25, null, 0, 48000, 56000, 62000, 400),
            P("QM-SHPAK20", "Shpaklyovka 20 kg", "Шпаклёвка 20 кг", "Sement va aralashmalar", "dona", "Knauf", 20, null, 0, 64000, 75000, 82000, 250),
            P("QM-KAF3060", "Kafel 30x60 oq", "Кафель 30x60 белый", "Kafel va laminat", "m2", "Kerama", 18, null, 0, 68000, 79000, 88000, 900),
            P("QM-KAF6060", "Keramogranit 60x60", "Керамогранит 60x60", "Kafel va laminat", "m2", "Kerama", 22, null, 0, 98000, 115000, 128000, 700),
            P("QM-LAM8", "Laminat 8 mm 32-sinf", "Ламинат 8 мм 32 класс", "Kafel va laminat", "m2", "Kronospan", 7, null, 0, 72000, 85000, 95000, 1200),
            P("QM-ARM12", "Armatura 12 mm", "Арматура 12 мм", "Metall", "kg", "BMZ", 1, "tn", 1000, 8600, 9800, 10500, 12000),
            P("QM-ARM10", "Armatura 10 mm", "Арматура 10 мм", "Metall", "kg", "BMZ", 1, "tn", 1000, 8700, 9900, 10600, 9000),
            P("QM-PROF4020", "Profil truba 40x20", "Профильная труба 40x20", "Metall", "m", "Artel Metall", 1.6m, null, 0, 21000, 25000, 28000, 2000),
            P("QM-GKL125", "Gipsokarton 12,5 mm", "Гипсокартон 12,5 мм", "Gipsokarton", "dona", "Knauf", 25, null, 0, 64000, 74000, 82000, 500),
            P("QM-PROFCD", "Profil CD 60 3 m", "Профиль CD 60 3 м", "Gipsokarton", "dona", "Knauf", 1.4m, null, 0, 19000, 23000, 26000, 1500),
            P("QM-BOY10", "Emulsion bo'yoq 10 l oq", "Водоэмульсионная краска 10 л", "Bo'yoqlar", "dona", "Dufa", 14, null, 0, 145000, 168000, 185000, 200),
            P("QM-GRUNT10", "Grunt 10 l", "Грунтовка 10 л", "Bo'yoqlar", "dona", "Ceresit", 10, null, 0, 88000, 102000, 112000, 220),
            P("QM-EMAL", "Emal PF-115 2,7 kg", "Эмаль ПФ-115 2,7 кг", "Bo'yoqlar", "dona", "Dufa", 2.7m, null, 0, 52000, 61000, 67000, 300),
            P("QM-KAB25", "Kabel VVG 3x2,5", "Кабель ВВГ 3x2,5", "Elektr", "m", "Uzkabel", 0.12m, null, 0, 9500, 11200, 12500, 5000),
            P("QM-ROZ", "Rozetka ikki o'rinli", "Розетка двойная", "Elektr", "dona", "Schneider", 0.1m, null, 0, 24000, 29000, 33000, 600),
            P("QM-AVT16", "Avtomat 16A", "Автомат 16А", "Elektr", "dona", "IEK", 0.1m, null, 0, 31000, 37000, 41000, 400),
            P("QM-TRUB20", "PPR truba 20 mm", "Труба PPR 20 мм", "Santexnika", "m", "Kalde", 0.17m, null, 0, 6500, 7800, 8700, 3000),
            P("QM-SMES", "Smesitel oshxona", "Смеситель кухонный", "Santexnika", "dona", "Frap", 1.2m, null, 0, 185000, 215000, 240000, 80),
            P("QM-UNITAZ", "Unitaz kompakt", "Унитаз компакт", "Santexnika", "dona", "Santek", 28, null, 0, 690000, 790000, 860000, 40),
            P("QM-PENO", "Montaj ko'pigi 750 ml", "Монтажная пена 750 мл", "Kimyo", "dona", "Penosil", 0.9m, null, 0, 41000, 48000, 54000, 500),
            P("QM-SILIK", "Silikon germetik", "Силиконовый герметик", "Kimyo", "dona", "Tytan", 0.3m, null, 0, 23000, 27000, 30000, 600),
        };

        private static Product P(string pno, string name, string nameRu, string cat, string unit, string brand, decimal weight, string unit2, decimal factor2,
            decimal cost, decimal wholesale, decimal retail, decimal opening) =>
            new Product
            {
                PNo = pno, Name = name, NameRu = nameRu, Category = cat, Unit = unit, Brand = brand, Weight = weight, Unit2 = unit2, Factor2 = factor2,
                Cost = cost, Wholesale = wholesale, Retail = retail, OpeningQty = opening
            };

        private static readonly string[] ShopWords = { "Baraka", "Usta", "Mega", "Qurilish", "Mustahkam", "Oltin uy", "Navro'z", "Bunyod", "Sifat", "Hamkor" };

        public static List<Customer> Customers()
        {
            var list = new List<Customer>();
            var rnd = new Random(2026);
            for (int i = 0; i < 40; i++)
            {
                int agent = i / 8, area = (i * 7 + agent) % Territories.Length;
                var t = Territories[area];
                var c = AreaCenters[area];
                list.Add(new Customer
                {
                    Name = ShopWords[i % ShopWords.Length] + " qurilish do'koni " + (i + 1),
                    Inn = (300100000 + i * 137).ToString(CultureInfo.InvariantCulture),
                    Phone = "+99890" + (1000000 + i * 7919 % 8999999).ToString("0000000", CultureInfo.InvariantCulture),
                    Territory = t.District + " / " + t.Area,
                    Agent = Agents[agent],
                    PriceType = i % 5 == 4 ? "Chakana" : "Ulgurji",
                    VisitDays = i % 2 == 0 ? "1,3,5" : "2,4,6",
                    Category = i % 8 < 2 ? "A" : i % 8 < 6 ? "B" : "C",
                    Limit = i % 8 == 7 ? 1000000m : (3 + (i % 5) * 3) * 1000000m,
                    PaymentDays = i % 3 == 0 ? 14 : 7,
                    Lat = c.Lat + (decimal)((rnd.NextDouble() - 0.5) * 0.02), Lng = c.Lng + (decimal)((rnd.NextDouble() - 0.5) * 0.02),
                    Opening = i % 3 == 1 ? 0 : (i % 7 + 1) * 350000m
                });
            }
            return list;
        }

        // ---------- Excel (import shablonlari shaklida) ----------

        private static string D(decimal v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        public static Dictionary<string, Func<byte[]>> Files(DateTime today) => new Dictionary<string, Func<byte[]>>
        {
            ["1_hududlar.xlsx"] = () => Xlsx(new[] { "Viloyat", "Viloyat (rus)", "Tuman", "Tuman (rus)", "Hudud" },
                Territories.Select(t => new[] { t.Region, t.RegionRu, t.District, t.DistrictRu, t.Area })),
            ["2_narx_turlari.xlsx"] = () => Xlsx(new[] { "Nomi", "Nomi (rus)", "Standart (ha/yo'q)", "Tartib" },
                new[] { new[] { "Chakana", "Розница", "ha", "1" }, new[] { "Ulgurji", "Опт", null, "2" } }),
            ["3_mijozlar.xlsx"] = () => Xlsx(new[] { "Nomi", "STIR", "Telefon", "Hudud", "Agent (login)", "Narx turi", "Tashrif kunlari (1-7)", "Kredit limiti", "Kechiktirish (kun)", "Kenglik", "Uzunlik", "Toifa (A/B/C)" },
                Customers().Select(c => new[] { c.Name, c.Inn, c.Phone, c.Territory, c.Agent, c.PriceType, c.VisitDays, D(c.Limit), c.PaymentDays.ToString(), D(c.Lat), D(c.Lng), c.Category })),
            ["4_marshrutlar.xlsx"] = () => Xlsx(new[] { "Mijoz (STIR yoki nomi)", "Agent (login)", "Tashrif kunlari (1-7)" },
                Customers().Take(8).Select(c => new[] { c.Inn, c.Agent, c.VisitDays })),
            ["5_tovarlar.xlsx"] = () => Xlsx(new[] { "Artikul", "Nomi", "Nomi (rus)", "Asosiy birlik", "Brend", "Og'irlik (kg)" },
                Products.Select(p => new[] { p.PNo, p.Name, p.NameRu, p.Unit, p.Brand, D(p.Weight) })),
            ["6_birliklar.xlsx"] = () => Xlsx(new[] { "Artikul", "Birlik", "Koeffitsient", "Odatiy sotish (ha/yo'q)" },
                Products.Where(p => p.Unit2 != null).Select(p => new[] { p.PNo, p.Unit2, D(p.Factor2), "yo'q" })),
            ["7_narxlar.xlsx"] = () => Xlsx(new[] { "Artikul", "Chakana", "Ulgurji", "Ulgurji min" },
                Products.Select(p => new[] { p.PNo, D(p.Retail), D(p.Wholesale), D(Math.Round(p.Wholesale * 0.95m / 100) * 100) })),
            ["8_kirish_qarzlari.xlsx"] = () => Xlsx(new[] { "Mijoz (STIR yoki nomi)", "Qarz (+) / avans (-)", "To'lov muddati" },
                Customers().Where(c => c.Opening != 0).Select((c, i) => new[] { c.Inn, D(c.Opening), today.AddDays(i % 4 == 0 ? -3 : 5 + i % 10).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) })),
            ["9_boshlangich_qoldiq.xlsx"] = () => Xlsx(new[] { "Artikul", "Nomi", "Birlik", "Sanalgan", "Tannarx" },
                Products.Select(p => new[] { p.PNo, p.Name, p.Unit, D(p.OpeningQty), D(p.Cost) })),
        };

        public static byte[] Xlsx(string[] header, IEnumerable<string[]> rows)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Import");
                for (int c = 0; c < header.Length; c++)
                {
                    ws.Cell(1, c + 1).Value = header[c];
                    ws.Cell(1, c + 1).Style.Font.Bold = true;
                }
                int r = 2;
                foreach (var row in rows)
                {
                    for (int c = 0; c < row.Length; c++)
                        if (row[c] != null) ws.Cell(r, c + 1).SetValue(row[c]);
                    r++;
                }
                ws.Columns().AdjustToContents();
                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}
