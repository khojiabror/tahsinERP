using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Services.Common
{
    /// <summary>Keshlangan spravochnik elementi (dropdown uchun).</summary>
    public sealed class RefItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
        /// <summary>Qo'shimcha maydon: valyuta/birlik qisqa nomi, ombor MRP'si va h.k.</summary>
        public string Extra { get; set; }
        public int? ExtraId { get; set; }
    }

    /// <summary>
    /// Kam o'zgaradigan spravochniklar (valyuta, birlik, ombor, mijoz, yetkazib beruvchi,
    /// ekspeditor, kategoriya, sex, ekspeditor shartnomasi, transport turi) uchun xotiradagi kesh. Avval har Create/Edit sahifasi
    /// va har validatsiya xatosida bu jadvallar bazadan qayta o'qilardi (~600 joy).
    ///
    /// Invalidatsiya avtomatik: <see cref="DBTHSNEntities"/>.SaveChanges tegishli jadvaldagi
    /// o'zgarishni ko'rsa, <see cref="InvalidateFor(Type)"/> chaqiriladi. Raw SQL bilan
    /// o'zgartirilsa — 10 daqiqada o'zi yangilanadi yoki <see cref="Invalidate"/> chaqiring.
    ///
    /// Faqat o'chirilmagan (IsDeleted != true) yozuvlar, nom bo'yicha tartiblangan.
    /// </summary>
    public static class ReferenceDataCache
    {
        private static readonly MemoryCache Cache = new MemoryCache("tahsinERP.ReferenceData");
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

        public const string Currencies = "Currencies";
        public const string Units = "Units";
        public const string Warehouses = "Warehouses";
        public const string Customers = "Customers";
        public const string Suppliers = "Suppliers";
        public const string Forwarders = "Forwarders";
        public const string ProductCategories = "ProductCategories";
        public const string Shops = "Shops";
        /// <summary>Ekspeditor shartnomalari: Name = ContractNo, ExtraId = ForwarderID.</summary>
        public const string FContracts = "FContracts";
        public const string TransportTypes = "TransportTypes";

        private static readonly Dictionary<Type, string> EntityKinds = new Dictionary<Type, string>
        {
            { typeof(CURRENCy), Currencies },
            { typeof(UNIT), Units },
            { typeof(Warehouse), Warehouses },
            { typeof(CUSTOMER), Customers },
            { typeof(SUPPLIER), Suppliers },
            { typeof(FORWARDER), Forwarders },
            { typeof(PRODUCT_CATEGORIES), ProductCategories },
            { typeof(SHOP), Shops },
            { typeof(F_CONTRACTS), FContracts },
            { typeof(F_TRANSPORT_TYPES), TransportTypes },
        };

        private static readonly Dictionary<string, Func<DBTHSNEntities, List<RefItem>>> Loaders =
            new Dictionary<string, Func<DBTHSNEntities, List<RefItem>>>
            {
                { Currencies, db => db.CURRENCIES.AsNoTracking().Where(x => x.IsDeleted != true)
                    .Select(x => new RefItem { ID = x.ID, Name = x.Shortname, Extra = x.Longname }).ToList() },
                { Units, db => db.UNITS.AsNoTracking().Where(x => x.IsDeleted != true)
                    .Select(x => new RefItem { ID = x.ID, Name = x.UnitName, Extra = x.ShortName }).ToList() },
                { Warehouses, db => db.WAREHOUSES.AsNoTracking().Where(x => x.IsDeleted != true)
                    .Select(x => new RefItem { ID = x.ID, Name = x.WHName, ExtraId = x.MRP }).ToList() },
                { Customers, db => db.CUSTOMERS.AsNoTracking().Where(x => x.IsDeleted != true)
                    .Select(x => new RefItem { ID = x.ID, Name = x.Name, Extra = x.Type }).ToList() },
                { Suppliers, db => db.SUPPLIERS.AsNoTracking().Where(x => x.IsDeleted != true)
                    .Select(x => new RefItem { ID = x.ID, Name = x.Name, Extra = x.Type }).ToList() },
                { Forwarders, db => db.FORWARDERS.AsNoTracking().Where(x => x.IsDeleted != true)
                    .Select(x => new RefItem { ID = x.ID, Name = x.ForwarderName }).ToList() },
                { ProductCategories, db => db.PRODUCT_CATEGORIES.AsNoTracking().Where(x => x.IsDeleted != true && x.IsActive)
                    .Select(x => new RefItem { ID = x.ID, Name = x.Name, Extra = x.Code }).ToList() },
                { Shops, db => db.SHOPS.AsNoTracking().Where(x => x.IsDeleted != true)
                    .Select(x => new RefItem { ID = x.ID, Name = x.ShopName }).ToList() },
                { FContracts, db => db.F_CONTRACTS.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new RefItem { ID = x.ID, Name = x.ContractNo, ExtraId = x.ForwarderID }).ToList() },
                { TransportTypes, db => db.F_TRANSPORT_TYPES.AsNoTracking().Where(x => x.IsDeleted != true)
                    .Select(x => new RefItem { ID = x.ID, Name = x.TransportType }).ToList() },
            };

        /// <summary>Spravochnik ro'yxati (o'zgartirmang — umumiy nusxa).</summary>
        public static IReadOnlyList<RefItem> Get(string kind)
        {
            var cached = Cache.Get(kind) as List<RefItem>;
            if (cached != null) return cached;

            Func<DBTHSNEntities, List<RefItem>> loader;
            if (!Loaders.TryGetValue(kind, out loader))
                throw new ArgumentException("Noma'lum spravochnik: " + kind, nameof(kind));

            List<RefItem> items;
            using (var db = new DBTHSNEntities())
            {
                items = loader(db)
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            Cache.Set(kind, items, DateTimeOffset.Now.Add(Ttl));
            return items;
        }

        /// <summary>Dropdown uchun SelectList (Value = ID, Text = Name).</summary>
        public static SelectList SelectList(string kind, object selectedValue = null)
        {
            return new SelectList(Get(kind), "ID", "Name", selectedValue);
        }

        /// <summary>ID bo'yicha nom (topilmasa null).</summary>
        public static string NameOf(string kind, int? id)
        {
            if (!id.HasValue) return null;
            return Get(kind).FirstOrDefault(x => x.ID == id.Value)?.Name;
        }

        public static void Invalidate(string kind)
        {
            Cache.Remove(kind);
        }

        /// <summary>Entity turi bo'yicha (SaveChanges hook'i chaqiradi).</summary>
        public static void InvalidateFor(Type entityType)
        {
            string kind;
            if (entityType != null && EntityKinds.TryGetValue(entityType, out kind))
                Cache.Remove(kind);
        }

        /// <summary>Kesh qaysi entity turlarini kuzatadi.</summary>
        public static bool Tracks(Type entityType)
        {
            return entityType != null && EntityKinds.ContainsKey(entityType);
        }
    }
}
