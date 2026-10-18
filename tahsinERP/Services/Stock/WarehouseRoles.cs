using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Models;

namespace tahsinERP.Services.Stock
{
    /// <summary>
    /// Ombor roli — WAREHOUSES.WarehouseType (SRM-B12). Avval rol ombor NOMIdagi kalit so'zlar bilan taxmin
    /// qilinardi, topilmasa "ID 7" yoki "birinchi ombor"ga tushardi; endi faqat ustun hal qiladi, sozlanmagan
    /// bo'lsa null qaytariladi (chaqiruvchi aniq xato beradi).
    /// Faol omborlar (ID, turi, sex) jarayon darajasida keshlanadi (SRM-P14) — kalit = tenant prefiksi, aks holda
    /// bir tenantning ombor ID'lari boshqa tenant bazasiga yoziladi; ombor yaratilganda/tahrirlanganda/
    /// o'chirilganda <see cref="Invalidate"/> chaqiriladi (faqat joriy tenant keshi tozalanadi).
    /// </summary>
    public static class WarehouseRoles
    {
        public const string Supply = "SUPPLY";
        public const string Production = "PRODUCTION";
        public const string Sales = "SALES";

        private sealed class Row
        {
            public int ID { get; set; }
            public string WarehouseType { get; set; }
            public int? ShopID { get; set; }
        }

        private static readonly ConcurrentDictionary<string, List<Row>> _active =
            new ConcurrentDictionary<string, List<Row>>(StringComparer.Ordinal);

        public static void Invalidate()
        {
            List<Row> removed;
            _active.TryRemove(TenantContext.CacheKeyPrefix, out removed);
        }

        private static List<Row> Active(DBTHSNEntities db)
        {
            string key = TenantContext.CacheKeyPrefix;
            List<Row> rows;
            if (_active.TryGetValue(key, out rows))
                return rows;

            rows = db.WAREHOUSES
                .AsNoTracking()
                .Where(w => w.IsDeleted != true)
                .OrderBy(w => w.ID)
                .Select(w => new Row { ID = w.ID, WarehouseType = w.WarehouseType, ShopID = w.ShopID })
                .ToList();
            _active[key] = rows;
            return rows;
        }

        /// <summary>Berilgan turdagi faol ombor ID'si; bir nechta bo'lsa — sexi mos kelgani, aks holda eng kichik ID.</summary>
        public static int? GetWarehouseId(DBTHSNEntities db, string warehouseType, int? shopId = null)
        {
            var matches = Active(db).Where(r => r.WarehouseType == warehouseType).ToList();
            if (matches.Count == 0)
                return null;
            if (shopId.HasValue)
            {
                var byShop = matches.FirstOrDefault(r => r.ShopID == shopId.Value);
                if (byShop != null)
                    return byShop.ID;
            }
            return matches[0].ID;
        }

        /// <summary>Faol omborning turi; ombor topilmasa yoki turi berilmagan bo'lsa null.</summary>
        public static string GetType(DBTHSNEntities db, int warehouseId)
        {
            return Active(db).FirstOrDefault(r => r.ID == warehouseId)?.WarehouseType;
        }

        /// <summary>Ombor faolmi (o'chirilmagan).</summary>
        public static bool Exists(DBTHSNEntities db, int warehouseId)
        {
            return Active(db).Any(r => r.ID == warehouseId);
        }

        public static bool IsProductionOrSales(string warehouseType)
        {
            return warehouseType == Production || warehouseType == Sales;
        }

        public static string DisplayName(string warehouseType)
        {
            switch (warehouseType)
            {
                case Supply: return "Xomashyo (ta'minot)";
                case Production: return "Ishlab chiqarish";
                case Sales: return "Savdo";
                default: return "—";
            }
        }

        public static SelectList SelectList(string selected)
        {
            var items = new[] { Supply, Production, Sales }
                .Select(t => new SelectListItem { Value = t, Text = DisplayName(t) })
                .ToList();
            return new SelectList(items, "Value", "Text", selected);
        }
    }
}
