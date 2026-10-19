using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.ViewModels.PContract;

namespace tahsinERP.Services.Procurement
{
    /// <summary>
    /// Xarid shartnomalari: ro'yxat so'rovi va yozish amallari (SRM-M01 — PContractsController'dan ajratildi).
    /// Controller'da faqat ruxsat, ModelState va ViewBag qoladi. Validatsiya xatolari berilgan
    /// ModelStateDictionary'ga yoziladi; xato bo'lsa yozish amali null/false qaytaradi.
    /// </summary>
    public sealed class PContractService
    {
        /// <summary>P_CONTRACTS.ContractNo ustun uzunligi (arxiv qo'shimchasi shunga sig'diriladi).</summary>
        private const int NumberMaxLength = 50;

        private readonly DBTHSNEntities _db;
        private int? _piecesUnitId;
        private int? _cubicMeterUnitId;

        /// <summary>"dona" birligi (UNITS.SystemCode = PCS) — birlik ko'rsatilmaganda zaxira qiymat.</summary>
        private int PiecesUnitId()
        {
            if (!_piecesUnitId.HasValue)
                _piecesUnitId = SystemRefs.UnitId(_db, SystemCodes.Unit.Pieces) ?? 0;
            return _piecesUnitId.Value;
        }

        /// <summary>m3 birligi (UNITS.SystemCode = M3) — kesiladigan mahsulotlar shartnomada CBM'da yuritiladi.</summary>
        private int? CubicMeterUnitId()
        {
            if (!_cubicMeterUnitId.HasValue)
                _cubicMeterUnitId = SystemRefs.UnitId(_db, SystemCodes.Unit.CubicMeter) ?? 0;
            return _cubicMeterUnitId.Value > 0 ? _cubicMeterUnitId : null;
        }
        private readonly ModelStateDictionary _errors;

        public PContractService(DBTHSNEntities db, ModelStateDictionary errors)
        {
            _db = db;
            _errors = errors;
        }

        // ---------- ro'yxat (SRM-P03) ----------

        /// <summary>Shartnomalar ro'yxati qatori (proyeksiya).</summary>
        public sealed class IndexRow
        {
            public int ID { get; set; }
            public string ContractNo { get; set; }
            public int SupplierID { get; set; }
            public string SupplierName { get; set; }
            public byte PurchaseType { get; set; }
            public double? Amount { get; set; }
            public string PaymentTerms { get; set; }
            public DateTime IssuedDate { get; set; }
        }

        public static IQueryable<IndexRow> IndexQuery(DBTHSNEntities db, string purchaseTypeFilter,
            int? supplierID, int? contractID, DateTime? fromDate, DateTime? toDate)
        {
            var query = db.P_CONTRACTS.AsNoTracking().Where(c => c.IsDeleted != true);

            if (supplierID.HasValue && supplierID.Value > 0)
            {
                query = query.Where(c => c.SupplierID == supplierID.Value);
            }

            if (contractID.HasValue && contractID.Value > 0)
            {
                query = query.Where(c => c.ID == contractID.Value);
            }

            if (!string.IsNullOrEmpty(purchaseTypeFilter))
            {
                byte purchaseType = purchaseTypeFilter == "Indirect" ? ProcurementLookups.IndirectPurchase : ProcurementLookups.DirectPurchase;
                query = query.Where(c => c.PurchaseType == purchaseType);
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(c => c.IssuedDate >= from);
            }
            if (toDate.HasValue)
            {
                var toExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(c => c.IssuedDate < toExclusive);
            }

            return query.Select(c => new IndexRow
            {
                ID = c.ID,
                ContractNo = c.ContractNo,
                SupplierID = c.SupplierID,
                SupplierName = c.SUPPLIER.Name,
                PurchaseType = c.PurchaseType,
                Amount = (double?)c.Amount,
                PaymentTerms = c.PaymentTerms,
                IssuedDate = c.IssuedDate
            });
        }

        public static IQueryable<IndexRow> SearchIndex(IQueryable<IndexRow> query, string s)
        {
            return query.Where(r => r.ContractNo.Contains(s) || r.SupplierName.Contains(s) || r.PaymentTerms.Contains(s));
        }

        public static IOrderedQueryable<IndexRow> OrderIndex(IQueryable<IndexRow> query, int column, bool desc)
        {
            switch (column)
            {
                case 0: return ServerTablePage.By(query, r => r.ContractNo, desc).ThenByDescending(r => r.ID);
                case 1: return ServerTablePage.By(query, r => r.SupplierName, desc).ThenByDescending(r => r.ID);
                case 2: return ServerTablePage.By(query, r => r.PurchaseType, desc).ThenByDescending(r => r.ID);
                case 3: return ServerTablePage.By(query, r => r.Amount, desc).ThenByDescending(r => r.ID);
                case 4: return ServerTablePage.By(query, r => r.PaymentTerms, desc).ThenByDescending(r => r.ID);
                default: return desc ? query.OrderByDescending(r => r.IssuedDate).ThenByDescending(r => r.ID)
                                     : query.OrderBy(r => r.IssuedDate).ThenBy(r => r.ID);
            }
        }

        // ---------- yozish ----------

        /// <summary>
        /// Sarlavha, qatorlar va hujjat — bitta tranzaksiyada (SRM-B12). Raqam xatosi bo'lsa null.
        /// </summary>
        public async Task<P_CONTRACTS> CreateAsync(PContractCreateViewModel model, byte purchaseType, HttpPostedFileBase file)
        {
            using (var tx = _db.Database.BeginTransaction())
            {
                var number = await ResolveContractNoAsync(model.ContractNo, excludeId: null);
                if (number == null)
                {
                    return null;
                }

                var contract = new P_CONTRACTS
                {
                    ContractNo = number,
                    IssuedDate = model.IssuedDate,
                    CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(_db),
                    SupplierID = model.SupplierID,
                    CurrencyID = model.CurrencyID,
                    Incoterms = model.Incoterms,
                    PaymentTerms = model.PaymentTerms,
                    DueDate = model.DueDate,
                    IDN = model.IDN,
                    IsDeleted = false,
                    PurchaseType = purchaseType
                };
                _db.P_CONTRACTS.Add(contract);

                // Jami (QQS bilan) — bog'langan model qatorlaridan (avval Request.Form qayta o'qilardi, SRM-D10)
                contract.Amount = (decimal?)(await AddLinesAsync(contract, model.ContractProducts));
                AddDocument(contract, file);

                await _db.SaveChangesAsync();
                tx.Commit();
                return contract;
            }
        }

        /// <summary>Sarlavha va qatorlarni yangilaydi; yangi hujjat eskisining o'rniga. Raqam xatosi bo'lsa false.</summary>
        public async Task<bool> UpdateAsync(P_CONTRACTS contract, PContractEditViewModel model, HttpPostedFileBase file)
        {
            var number = await ResolveContractNoAsync(model.ContractNo, excludeId: contract.ID);
            if (number == null)
            {
                return false;
            }

            using (var tx = _db.Database.BeginTransaction())
            {
                contract.ContractNo = number;
                contract.IssuedDate = model.IssuedDate;
                contract.CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(_db);
                contract.SupplierID = model.SupplierID;
                contract.CurrencyID = model.CurrencyID;
                contract.Incoterms = model.Incoterms;
                contract.PaymentTerms = model.PaymentTerms;
                contract.DueDate = model.DueDate;
                contract.IDN = model.IDN;

                var existingProducts = await _db.P_CONTRACT_PRODUCTS.Where(cp => cp.ContractID == contract.ID).ToListAsync();
                _db.P_CONTRACT_PRODUCTS.RemoveRange(existingProducts);

                contract.Amount = (decimal?)(await AddLinesAsync(contract, model.ContractProducts));

                if (PdfUpload.HasFile(file))
                {
                    // Yangi hujjat eskisining o'rniga (eski blob'lar xotiraga yuklanmaydi)
                    await _db.Database.ExecuteSqlCommandAsync("DELETE FROM P_CONTRACT_DOCS WHERE ContractID = @p0", contract.ID);
                    AddDocument(contract, file);
                }

                await _db.SaveChangesAsync();
                tx.Commit();
                return true;
            }
        }

        /// <summary>Soft delete va bog'liq havolalarni tozalash. O'chirilgan shartnoma yoki null.</summary>
        public async Task<P_CONTRACTS> SoftDeleteAsync(int id)
        {
            var contract = await _db.P_CONTRACTS.FindAsync(id);
            if (contract == null || contract.IsDeleted == true)
            {
                return null;
            }

            contract.IsDeleted = true;
            await SoftDeleteReferenceService.AfterPContractSoftDeletedAsync(_db, id);
            await _db.SaveChangesAsync();
            return contract;
        }

        // ---------- yordamchilar ----------

        /// <summary>
        /// Shartnoma raqami: bo'sh bo'lsa PC-{yil}-NNNN (qulf bilan, o'chirilganlar ham hisobga olinadi — SRM-B14);
        /// qo'lda kiritilsa — faol shartnomalarda takrorlanmasligi tekshiriladi, o'chirilgan hujjat raqami bo'shatiladi.
        /// Avtomatik raqam tranzaksiya ichida chaqirilishi kerak. Xato bo'lsa null (ModelState'ga yoziladi).
        /// </summary>
        private async Task<string> ResolveContractNoAsync(string requested, int? excludeId)
        {
            var number = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
            if (number == null)
            {
                if (excludeId.HasValue)
                {
                    // Tahrirlashda bo'sh qoldirilsa — mavjud raqam saqlanadi
                    return await _db.P_CONTRACTS.Where(c => c.ID == excludeId.Value).Select(c => c.ContractNo).FirstOrDefaultAsync();
                }

                return await DocumentNumberService.NextAsync(_db, "P_CONTRACTS", "ContractNo", $"PC-{DateTime.Now.Year}-", 4);
            }

            // Faol hujjatlarda takrorlanmasin. Bazada ContractNo bo'yicha global UNIQUE indeks bor (o'chirilganlar ham):
            // shu raqamli o'chirilgan hujjat bo'lsa, uning raqami arxivlanadi va raqam bo'shaydi (SRM-D4, POrders bilan bir xil)
            if (await DocumentNumberService.ActiveNumberExistsAsync(_db, "P_CONTRACTS", "ContractNo", number, excludeId))
            {
                _errors.AddModelError("ContractNo", "Shu raqamli shartnoma allaqachon mavjud.");
                return null;
            }

            await DocumentNumberService.ReleaseNumberFromDeletedAsync(_db, "P_CONTRACTS", "ContractNo", number, NumberMaxLength, excludeId);
            return number;
        }

        /// <summary>
        /// Shartnoma qatorlarini qo'shadi va QQS bilan jami summani qaytaradi.
        /// Mahsulot birligi va "kesiladimi" belgisi bitta so'rovda olinadi (avval har qatorga 2 so'rov, SRM-P06).
        /// </summary>
        private async Task<double> AddLinesAsync(P_CONTRACTS contract, IEnumerable<PContractProductViewModel> lines)
        {
            var list = (lines ?? Enumerable.Empty<PContractProductViewModel>()).ToList();
            if (list.Count == 0)
            {
                return 0;
            }

            var productIds = list.Select(l => l.ProductID).Distinct().ToList();
            var products = await _db.PRODUCTS.AsNoTracking()
                .Where(p => productIds.Contains(p.ID))
                .Select(p => new
                {
                    p.ID,
                    p.UnitID,
                    RequiresCutting = p.CategoryID.HasValue && p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.RequiresCutting
                })
                .ToDictionaryAsync(p => p.ID);

            double total = 0;
            foreach (var line in list)
            {
                var vatPercent = VatMath.ClampPercent(line.VatPercent);
                var lineAmount = line.Price * line.Quantity;
                var vatAmount = VatMath.AmountFromPercent(lineAmount, vatPercent);

                // Kesiladigan mahsulotlar CBM'da (m3), qolganlari — tanlangan birlikda yoki mahsulot birligida
                var product = products.ContainsKey(line.ProductID) ? products[line.ProductID] : null;
                int unitId = product != null && product.RequiresCutting && CubicMeterUnitId().HasValue
                    ? CubicMeterUnitId().Value
                    : line.UnitID ?? (product?.UnitID ?? PiecesUnitId());

                _db.P_CONTRACT_PRODUCTS.Add(new P_CONTRACT_PRODUCTS
                {
                    P_CONTRACTS = contract,
                    ProductID = line.ProductID,
                    Price = (decimal)line.Price,
                    Quantity = (decimal)line.Quantity, // kesiladiganlarda allaqachon CBM'da
                    UnitID = unitId,
                    MOQ = (decimal?)line.MOQ,
                    HSCodeID = line.HSCodeID,
                    ActivePart = true,
                    VATAmount = (decimal?)(vatAmount)
                });

                // Avvalgi qoida saqlandi: jamiga faqat mahsuloti tanlangan, narx va miqdori musbat qatorlar kiradi
                if (line.ProductID > 0 && line.Price > 0 && line.Quantity > 0)
                {
                    total += lineAmount + vatAmount;
                }
            }

            return total;
        }

        private void AddDocument(P_CONTRACTS contract, HttpPostedFileBase file)
        {
            if (!PdfUpload.HasFile(file))
            {
                return;
            }

            _db.P_CONTRACT_DOCS.Add(new P_CONTRACT_DOCS { P_CONTRACTS = contract, Doc = PdfUpload.Read(file) });
        }
    }
}
