using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services.Procurement
{
    /// <summary>Mahsulot dropdowni uchun yengil element (PRODUCT entity'si o'rniga).</summary>
    public sealed class ProductLookupItem
    {
        public int ID { get; set; }
        public string PNo { get; set; }
        public string Name { get; set; }

        /// <summary>"PNo - Name" — SRM formalarida ishlatiladigan ko'rinish.</summary>
        public string Text => PNo + " - " + (Name ?? "");
    }

    /// <summary>Xarid shartnomasi + uning QQS stavkasi (supplier bo'yicha AJAX javobi).</summary>
    public sealed class SupplierContractItem
    {
        public int ID { get; set; }
        public string ContractNo { get; set; }
        public double VatRate { get; set; }
    }

    /// <summary>
    /// SRM xarid formalari (POrders, PContracts, PInvoices, PRequisitions, Tender) uchun umumiy
    /// spravochniklar. Avval har controllerda 3-4 martadan nusxa ko'chirilgan bloklar edi
    /// (SRM-D01/D02/D05) va mahsulotlar butun PRODUCT + PRODUCT_CATEGORIES entity'lari bilan
    /// kuzatuvda yuklanardi (SRM-P01).
    ///
    /// Valyuta, birlik, ta'minotchi — <see cref="ReferenceDataCache"/>dan (SaveChanges'da avtomatik
    /// invalidatsiya). Mahsulotlar — o'z keshi: PRODUCTS uchun SaveChanges hook'i yo'q, shuning uchun
    /// kesh arzon "shtamp" (qatorlar soni, MAX(ID), o'chirilganlar soni) bilan tekshiriladi va
    /// 2 daqiqadan keyin baribir yangilanadi (nom/kategoriya tahriri shu muddatda ko'rinadi).
    /// </summary>
    public static class ProcurementLookups
    {
        public const byte DirectPurchase = 0;
        public const byte IndirectPurchase = 1;

        private static readonly MemoryCache Cache = new MemoryCache("tahsinERP.Procurement");
        private static readonly TimeSpan ProductTtl = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan LongTtl = TimeSpan.FromMinutes(10);

        private const string HsCodesKey = "HSCodes";
        private const string DefaultCompanyKey = "DefaultCompany";

        private sealed class ProductCacheEntry
        {
            public string Stamp;
            public List<ProductLookupItem> Items;
        }

        // ---------- Mahsulotlar ----------

        /// <summary>
        /// Xarid qilinadigan mahsulotlar (PNo bo'yicha tartiblangan).
        /// Bevosita (0): tayyor mahsulot bo'lmagan hammasi. Bilvosita (1): sarf materiali yoki xaridga ruxsat etilganlar.
        /// </summary>
        public static async Task<IReadOnlyList<ProductLookupItem>> GetPurchasableProductsAsync(DBTHSNEntities db, byte purchaseType)
        {
            string key = "Products:" + (purchaseType == IndirectPurchase ? "1" : "0");
            string stamp = await GetProductStampAsync(db);

            var cached = Cache.Get(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(key)) as ProductCacheEntry;
            if (cached != null && cached.Stamp == stamp)
            {
                return cached.Items;
            }

            var query = db.PRODUCTS.AsNoTracking().Where(p => p.IsDeleted != true);
            if (purchaseType == IndirectPurchase)
            {
                query = query.Where(p => p.PRODUCT_CATEGORIES == null ||
                    (p.PRODUCT_CATEGORIES.IsFinishedGood == false &&
                     (p.PRODUCT_CATEGORIES.IsConsumable == true ||
                      p.PRODUCT_CATEGORIES.AllowPurchase == true)));
            }
            else
            {
                query = query.Where(p => p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood == false);
            }

            var items = await query
                .OrderBy(p => p.PNo)
                .Select(p => new ProductLookupItem { ID = p.ID, PNo = p.PNo, Name = p.Name })
                .ToListAsync();

            Cache.Set(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(key), new ProductCacheEntry { Stamp = stamp, Items = items }, DateTimeOffset.Now.Add(ProductTtl));
            return items;
        }

        /// <summary>
        /// Xarid qilinadigan mahsulotlar + hujjatda allaqachon bor, lekin hozir filtrga tushmaydigan
        /// mahsulotlar (kategoriyasi o'zgargan bo'lsa ham tahrirlash sahifasida tanlov yo'qolmasin).
        /// </summary>
        public static async Task<IReadOnlyList<ProductLookupItem>> GetPurchasableProductsAsync(
            DBTHSNEntities db, byte purchaseType, IEnumerable<int> ensureProductIds)
        {
            var items = await GetPurchasableProductsAsync(db, purchaseType);
            if (ensureProductIds == null)
            {
                return items;
            }

            var existing = new HashSet<int>(items.Select(p => p.ID));
            var missingIds = ensureProductIds.Where(id => id > 0 && !existing.Contains(id)).Distinct().ToList();
            if (missingIds.Count == 0)
            {
                return items;
            }

            var missing = await db.PRODUCTS.AsNoTracking()
                .Where(p => missingIds.Contains(p.ID) && p.IsDeleted != true)
                .Select(p => new ProductLookupItem { ID = p.ID, PNo = p.PNo, Name = p.Name })
                .ToListAsync();

            return items.Concat(missing).OrderBy(p => p.PNo).ToList();
        }

        /// <summary>"PNo - Name" ko'rinishidagi SelectList (Value = ID).</summary>
        public static SelectList ProductSelectList(IEnumerable<ProductLookupItem> products, object selectedValue = null)
        {
            var items = products.Select(p => new SelectListItem { Value = p.ID.ToString(), Text = p.Text }).ToList();
            return new SelectList(items, "Value", "Text", selectedValue);
        }

        private static async Task<string> GetProductStampAsync(DBTHSNEntities db)
        {
            // Indeks bo'yicha tez: yangi mahsulot yoki soft-delete darhol ko'rinadi
            return await db.Database.SqlQuery<string>(
                "SELECT CONCAT(COUNT_BIG(*), ':', MAX(ID), ':', SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END)) FROM PRODUCTS")
                .FirstOrDefaultAsync();
        }

        // ---------- Spravochniklar ----------

        public static SelectList Suppliers(object selectedValue = null)
        {
            return ReferenceDataCache.SelectList(ReferenceDataCache.Suppliers, selectedValue);
        }

        /// <summary>Valyutalar (Text = Shortname).</summary>
        public static SelectList Currencies(object selectedValue = null)
        {
            return ReferenceDataCache.SelectList(ReferenceDataCache.Currencies, selectedValue);
        }

        /// <summary>O'lchov birliklari (Text = ShortName).</summary>
        public static SelectList Units(object selectedValue = null)
        {
            return new SelectList(UnitItems(), "ID", "Text", selectedValue);
        }

        /// <summary>Birliklar ro'yxati (ID, Text = ShortName) — view'larda qatorma-qator SelectList qurish uchun.</summary>
        public static List<LookupItem> UnitItems()
        {
            return ReferenceDataCache.Get(ReferenceDataCache.Units)
                .Select(u => new LookupItem { ID = u.ID, Text = u.Extra })
                .ToList();
        }

        /// <summary>HS (ТНВЭД) kodlari (Text = HSCODE1). Faqat HSCodeController o'zgartiradi — u keshni tozalaydi.</summary>
        public static SelectList HSCodes(object selectedValue = null)
        {
            return new SelectList(HSCodeItems(), "ID", "Text", selectedValue);
        }

        public static List<LookupItem> HSCodeItems()
        {
            var cached = Cache.Get(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(HsCodesKey)) as List<LookupItem>;
            if (cached != null)
            {
                return cached;
            }

            List<LookupItem> items;
            using (var db = new DBTHSNEntities())
            {
                items = db.HSCODES.AsNoTracking()
                    .Select(h => new LookupItem { ID = h.ID, Text = h.HSCODE1 })
                    .ToList();
            }

            Cache.Set(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(HsCodesKey), items, DateTimeOffset.Now.Add(LongTtl));
            return items;
        }

        public static void InvalidateHSCodes()
        {
            Cache.Remove(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(HsCodesKey));
        }

        /// <summary>Standart kompaniya — birinchi faol COMPANIES yozuvi (topilmasa 0).</summary>
        public static async Task<int> GetDefaultCompanyIdAsync(DBTHSNEntities db)
        {
            var cached = Cache.Get(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(DefaultCompanyKey));
            if (cached is int)
            {
                return (int)cached;
            }

            var id = await db.COMPANIES.AsNoTracking()
                .Where(c => c.IsActive != false)
                .Select(c => (int?)c.ID)
                .FirstOrDefaultAsync() ?? 0;

            Cache.Set(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(DefaultCompanyKey), id, DateTimeOffset.Now.Add(LongTtl));
            return id;
        }

        // ---------- Shartnomalar ----------

        /// <summary>Ta'minotchining o'chirilmagan shartnomalari (yangi sanasi birinchi) va ularning QQS stavkasi.</summary>
        public static async Task<List<SupplierContractItem>> GetSupplierContractsWithVatAsync(DBTHSNEntities db, int supplierId)
        {
            var contracts = await db.P_CONTRACTS.AsNoTracking()
                .Where(c => c.SupplierID == supplierId && c.IsDeleted != true)
                .OrderByDescending(c => c.IssuedDate)
                .Select(c => new SupplierContractItem { ID = c.ID, ContractNo = c.ContractNo })
                .ToListAsync();

            var vatByContract = await PContractVatHelper.GetContractVatRatesAsync(db, contracts.Select(c => c.ID).ToList());
            foreach (var c in contracts)
            {
                double rate;
                c.VatRate = vatByContract.TryGetValue(c.ID, out rate) ? rate : 0d;
            }

            return contracts;
        }

        /// <summary>Ta'minotchi shartnomalari SelectList'i (supplierId bo'lmasa bo'sh ro'yxat).</summary>
        public static async Task<SelectList> ContractsBySupplierAsync(DBTHSNEntities db, int? supplierId, object selectedValue = null)
        {
            if (!supplierId.HasValue || supplierId.Value <= 0)
            {
                return new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
            }

            var contracts = await db.P_CONTRACTS.AsNoTracking()
                .Where(c => c.SupplierID == supplierId.Value && c.IsDeleted != true)
                .OrderByDescending(c => c.IssuedDate)
                .Select(c => new { c.ID, c.ContractNo })
                .ToListAsync();

            return new SelectList(contracts, "ID", "ContractNo", selectedValue);
        }

        /// <summary>Filtr dropdowni uchun barcha o'chirilmagan shartnomalar (yangi birinchi).</summary>
        public static async Task<SelectList> AllContractsAsync(DBTHSNEntities db, object selectedValue = null)
        {
            var contracts = await db.P_CONTRACTS.AsNoTracking()
                .Where(c => c.IsDeleted != true)
                .OrderByDescending(c => c.IssuedDate)
                .ThenByDescending(c => c.ID)
                .Select(c => new { c.ID, c.ContractNo })
                .ToListAsync();

            return new SelectList(contracts, "ID", "ContractNo", selectedValue);
        }
    }

    /// <summary>Oddiy (ID, Text) juftligi.</summary>
    public sealed class LookupItem
    {
        public int ID { get; set; }
        public string Text { get; set; }
    }
}
