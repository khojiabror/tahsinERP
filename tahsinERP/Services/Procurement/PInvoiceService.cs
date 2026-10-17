using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.ViewModels.PInvoice;

namespace tahsinERP.Services.Procurement
{
    /// <summary>
    /// Xarid invoyslari: ro'yxat so'rovi va yozish amallari (SRM-M01 — PInvoicesController'dan ajratildi).
    /// Validatsiya xatolari berilgan ModelStateDictionary'ga yoziladi; xato bo'lsa yozish amali null/false qaytaradi.
    /// Finance'ga yozish (PostPurchaseInvoice) chaqiruvchi tomonda — tranzaksiyadan keyin.
    /// </summary>
    public sealed class PInvoiceService
    {
        private readonly DBTHSNEntities _db;
        private readonly ModelStateDictionary _errors;

        public PInvoiceService(DBTHSNEntities db, ModelStateDictionary errors)
        {
            _db = db;
            _errors = errors;
        }

        // ---------- ro'yxat (SRM-P03) ----------

        /// <summary>Invoyslar ro'yxati qatori (proyeksiya).</summary>
        public sealed class IndexRow
        {
            public int ID { get; set; }
            public string InvoiceNo { get; set; }
            public int SupplierID { get; set; }
            public string SupplierName { get; set; }
            public byte PurchaseType { get; set; }
            public int? OrderID { get; set; }
            public string OrderNo { get; set; }
            public int ContractID { get; set; }
            public string ContractNo { get; set; }
            public int? VatContractID { get; set; }
            public double? Amount { get; set; }
            public double? Net { get; set; }
            public DateTime InvoiceDate { get; set; }
        }

        public static IQueryable<IndexRow> IndexQuery(DBTHSNEntities db, string purchaseTypeFilter,
            int? supplierID, int? contractID, DateTime? fromDate, DateTime? toDate)
        {
            var query = db.P_INVOICES.AsNoTracking().Where(i => i.IsDeleted != true);

            if (supplierID.HasValue && supplierID.Value > 0)
            {
                query = query.Where(i => i.SupplierID == supplierID.Value);
            }

            if (contractID.HasValue && contractID.Value > 0)
            {
                query = query.Where(i => i.ContractID == contractID.Value);
            }

            if (!string.IsNullOrEmpty(purchaseTypeFilter))
            {
                byte purchaseType = purchaseTypeFilter == "Indirect" ? ProcurementLookups.IndirectPurchase : ProcurementLookups.DirectPurchase;
                query = query.Where(i => i.PurchaseType == purchaseType);
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(i => i.InvoiceDate >= from);
            }
            if (toDate.HasValue)
            {
                var toExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(i => i.InvoiceDate < toExclusive);
            }

            return query.Select(i => new IndexRow
            {
                ID = i.ID,
                InvoiceNo = i.InvoiceNo,
                SupplierID = i.SupplierID,
                SupplierName = i.SUPPLIER.Name,
                PurchaseType = i.PurchaseType,
                OrderID = i.OrderID,
                OrderNo = i.P_ORDERS.OrderNo,
                ContractID = i.ContractID,
                ContractNo = i.P_CONTRACTS.ContractNo,
                // QQS stavkasi shartnomadan: invoysning o'zi yoki buyurtmasi orqali (SRM-B18)
                VatContractID = i.ContractID > 0 ? (int?)i.ContractID : i.P_ORDERS.ContractID,
                Amount = i.Amount,
                // Amount QQS bilan saqlanadi (SRM-B25); QQSsiz summa — qatorlar yig'indisi
                Net = i.P_INVOICE_PRODUCTS.Sum(x => (double?)x.TotalPrice),
                InvoiceDate = i.InvoiceDate
            });
        }

        public static IQueryable<IndexRow> SearchIndex(IQueryable<IndexRow> query, string s)
        {
            return query.Where(r => r.InvoiceNo.Contains(s) || r.SupplierName.Contains(s)
                || r.OrderNo.Contains(s) || r.ContractNo.Contains(s));
        }

        public static IOrderedQueryable<IndexRow> OrderIndex(IQueryable<IndexRow> query, int column, bool desc)
        {
            switch (column)
            {
                case 0: return ServerTablePage.By(query, r => r.InvoiceNo, desc).ThenByDescending(r => r.ID);
                case 1: return ServerTablePage.By(query, r => r.SupplierName, desc).ThenByDescending(r => r.ID);
                case 2: return ServerTablePage.By(query, r => r.PurchaseType, desc).ThenByDescending(r => r.ID);
                case 3: return ServerTablePage.By(query, r => r.OrderNo, desc).ThenByDescending(r => r.ID);
                case 4: return ServerTablePage.By(query, r => r.ContractNo, desc).ThenByDescending(r => r.ID);
                case 5: return ServerTablePage.By(query, r => r.Amount, desc).ThenByDescending(r => r.ID);
                default: return desc ? query.OrderByDescending(r => r.InvoiceDate).ThenByDescending(r => r.ID)
                                     : query.OrderBy(r => r.InvoiceDate).ThenBy(r => r.ID);
            }
        }

        // ---------- yozish ----------

        /// <summary>
        /// Invoys yaratadi: bog'lanishlar tekshiruvi, keyin sarlavha, qatorlar va hujjat bitta tranzaksiyada (SRM-B12).
        /// Xato bo'lsa null.
        /// </summary>
        /// <param name="formIndirect">Buyurtmasiz invoysda formadagi "Indirect" belgisi.</param>
        public async Task<P_INVOICES> CreateAsync(PInvoiceCreateViewModel model, bool formIndirect, HttpPostedFileBase file)
        {
            // Xarid turi: buyurtmadan, bo'lmasa formadagi belgidan
            byte purchaseType = ProcurementLookups.DirectPurchase;
            P_ORDERS order = null;
            if (model.OrderID.HasValue)
            {
                order = await _db.P_ORDERS.FindAsync(model.OrderID.Value);
                if (order != null)
                {
                    purchaseType = order.PurchaseType;
                }
            }
            else if (formIndirect)
            {
                purchaseType = ProcurementLookups.IndirectPurchase;
            }

            await ValidateLinksAsync(order, model.SupplierID, model.ContractID, "yaratilganda");
            if (!_errors.IsValid)
            {
                return null;
            }

            using (var tx = _db.Database.BeginTransaction())
            {
                var number = await ResolveInvoiceNoAsync(model.InvoiceNo, excludeId: null);
                if (number == null)
                {
                    return null;
                }

                var invoice = new P_INVOICES
                {
                    InvoiceNo = number,
                    InvoiceDate = model.InvoiceDate,
                    OrderID = model.OrderID,
                    SupplierID = model.SupplierID,
                    ContractID = model.ContractID ?? 0,
                    CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(_db),
                    CurrencyID = model.CurrencyID,
                    IsDeleted = false,
                    PurchaseType = purchaseType
                };
                _db.P_INVOICES.Add(invoice);

                // Qatorlar navigatsiya orqali — EF FK'ni o'zi qo'yadi, bitta SaveChanges.
                // Amount QQS bilan saqlanadi (SRM-B25 qarori): net + shartnoma QQS stavkasi.
                var net = await AddLinesAsync(invoice, model.InvoiceProducts);
                invoice.Amount = VatMath.ApplyVat(net, await ResolveVatRateAsync(model.ContractID, order));
                AddDocument(invoice, file);

                await _db.SaveChangesAsync();
                tx.Commit();
                return invoice;
            }
        }

        /// <summary>Invoysni yangilaydi (qatorlar almashtiriladi, summa qayta hisoblanadi). Xato bo'lsa false.</summary>
        public async Task<bool> UpdateAsync(P_INVOICES invoice, PInvoiceEditViewModel model, HttpPostedFileBase file)
        {
            P_ORDERS order = null;
            if (model.OrderID.HasValue && model.OrderID.Value > 0)
            {
                order = await _db.P_ORDERS.FindAsync(model.OrderID.Value);
            }
            byte purchaseType = await ResolvePurchaseTypeAsync(model.ContractID, model.OrderID);

            await ValidateLinksAsync(order, model.SupplierID, model.ContractID, "tahrirlashda");
            if (!_errors.IsValid)
            {
                return false;
            }

            var invoiceNo = await ResolveInvoiceNoAsync(model.InvoiceNo, excludeId: invoice.ID);
            if (!_errors.IsValid)
            {
                return false;
            }

            using (var tx = _db.Database.BeginTransaction())
            {
                invoice.InvoiceNo = invoiceNo;
                invoice.InvoiceDate = model.InvoiceDate;
                invoice.OrderID = model.OrderID; // ixtiyoriy
                invoice.SupplierID = model.SupplierID;
                invoice.ContractID = model.ContractID ?? 0; // shartnoma asosidagi hisoblar uchun
                invoice.CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(_db);
                invoice.CurrencyID = model.CurrencyID;
                invoice.PurchaseType = purchaseType;

                // Qatorlarni almashtirish va summani qayta hisoblash (SRM-B01:
                // avval Amount eski qolib, Finance'ga eski summa qayta yozilardi)
                var existingProducts = await _db.P_INVOICE_PRODUCTS.Where(ip => ip.InvoiceID == invoice.ID).ToListAsync();
                _db.P_INVOICE_PRODUCTS.RemoveRange(existingProducts);
                var net = await AddLinesAsync(invoice, model.InvoiceProducts);
                invoice.Amount = VatMath.ApplyVat(net, await ResolveVatRateAsync(model.ContractID, order));

                if (PdfUpload.HasFile(file))
                {
                    // Yangi hujjat eskisining o'rniga (eski blob'larni xotiraga yuklamasdan o'chiramiz)
                    await _db.Database.ExecuteSqlCommandAsync("DELETE FROM P_INVOICE_DOCS WHERE InvoiceID = @p0", invoice.ID);
                    AddDocument(invoice, file);
                }

                await _db.SaveChangesAsync();
                tx.Commit();
                return true;
            }
        }

        /// <summary>Invoys va uning finance yozuvi birga bekor qilinadi. O'chirilgan invoys yoki null.</summary>
        public async Task<P_INVOICES> SoftDeleteAsync(int id)
        {
            var invoice = await _db.P_INVOICES.FindAsync(id);
            if (invoice == null || invoice.IsDeleted == true)
            {
                return null;
            }

            using (var tx = _db.Database.BeginTransaction())
            {
                invoice.IsDeleted = true;
                await SoftDeleteReferenceService.AfterPInvoiceSoftDeletedAsync(_db, id);
                FinancePostingService.RemovePurchaseInvoicePosting(id, _db);
                await _db.SaveChangesAsync();
                tx.Commit();
            }

            return invoice;
        }

        /// <summary>Xarid turi: shartnomadan, bo'lmasa buyurtmadan (standart — bevosita).</summary>
        public async Task<byte> ResolvePurchaseTypeAsync(int? contractId, int? orderId)
        {
            if (contractId.HasValue && contractId.Value > 0)
            {
                var fromContract = await _db.P_CONTRACTS
                    .Where(c => c.ID == contractId.Value)
                    .Select(c => (byte?)c.PurchaseType)
                    .FirstOrDefaultAsync();
                if (fromContract.HasValue)
                {
                    return fromContract.Value;
                }
            }
            else if (orderId.HasValue && orderId.Value > 0)
            {
                var fromOrder = await _db.P_ORDERS
                    .Where(o => o.ID == orderId.Value)
                    .Select(o => (byte?)o.PurchaseType)
                    .FirstOrDefaultAsync();
                if (fromOrder.HasValue)
                {
                    return fromOrder.Value;
                }
            }

            return ProcurementLookups.DirectPurchase;
        }

        // ---------- yordamchilar ----------

        /// <summary>
        /// Invoys QQS stavkasi: shartnoma (to'g'ridan-to'g'ri yoki buyurtma orqali) qatorlaridagi stavka;
        /// shartnoma bo'lmasa 0. QQS faqat P_CONTRACT_PRODUCTS'da saqlanadi.
        /// </summary>
        private Task<double> ResolveVatRateAsync(int? contractId, P_ORDERS order)
        {
            int? cid = contractId > 0 ? contractId : order?.ContractID;
            return PContractVatHelper.CalculateContractVatRateAsync(_db, cid);
        }

        /// <summary>Invoys — buyurtma — shartnoma — ta'minotchi mosligi (Create/Edit'da bir xil, SRM-D8).</summary>
        private async Task ValidateLinksAsync(P_ORDERS order, int supplierId, int? contractId, string actionWord)
        {
            if (order != null)
            {
                if (order.PurchaseType == ProcurementLookups.DirectPurchase)
                {
                    if (!order.SupplierID.HasValue && supplierId <= 0)
                    {
                        _errors.AddModelError("SupplierID", $"Direct buyurtmadan invoice {actionWord} ta'minotchi tanlanishi shart.");
                    }
                    if (!order.ContractID.HasValue && (!contractId.HasValue || contractId.Value <= 0))
                    {
                        _errors.AddModelError("ContractID", $"Direct buyurtmadan invoice {actionWord} shartnoma tanlanishi shart.");
                    }
                }

                if (order.SupplierID.HasValue && order.SupplierID.Value > 0 && supplierId != order.SupplierID.Value)
                {
                    _errors.AddModelError("SupplierID", "Tanlangan ta'minotchi buyurtmadagi ta'minotchi bilan mos emas.");
                }
                if (order.ContractID.HasValue && order.ContractID.Value > 0 && contractId != order.ContractID.Value)
                {
                    _errors.AddModelError("ContractID", "Tanlangan shartnoma buyurtmadagi shartnoma bilan mos emas.");
                }
            }

            if (contractId.HasValue && contractId.Value > 0)
            {
                var contract = await _db.P_CONTRACTS
                    .Where(c => c.ID == contractId.Value)
                    .Select(c => new { c.SupplierID, c.IsDeleted })
                    .FirstOrDefaultAsync();
                if (contract == null || contract.IsDeleted == true)
                {
                    _errors.AddModelError("ContractID", "Tanlangan shartnoma topilmadi.");
                }
                else if (supplierId > 0 && contract.SupplierID != supplierId)
                {
                    _errors.AddModelError("ContractID", "Tanlangan shartnoma tanlangan ta'minotchiga tegishli emas.");
                }
            }
        }

        /// <summary>
        /// Invoys raqami: bo'sh bo'lsa PI-{yil}-NNNN (qulf bilan, o'chirilganlar ham hisobga olinadi — SRM-B14);
        /// qo'lda kiritilsa — barcha invoyslarda (o'chirilganlar ham) takrorlanmasligi tekshiriladi.
        /// Avtomatik raqam tranzaksiya ichida chaqirilishi kerak. Xato bo'lsa null (ModelState'ga yoziladi).
        /// </summary>
        private async Task<string> ResolveInvoiceNoAsync(string requested, int? excludeId)
        {
            var number = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
            if (number == null)
            {
                if (excludeId.HasValue)
                {
                    // Tahrirlashda bo'sh qoldirilsa — mavjud raqam saqlanadi
                    return await _db.P_INVOICES.Where(i => i.ID == excludeId.Value).Select(i => i.InvoiceNo).FirstOrDefaultAsync();
                }

                return await DocumentNumberService.NextAsync(_db, "P_INVOICES", "InvoiceNo", $"PI-{DateTime.Now.Year}-", 4);
            }

            // Bazada InvoiceNo bo'yicha global UNIQUE indeks bor (o'chirilganlar ham) — tekshiruv ham global
            bool duplicate = await _db.P_INVOICES.AnyAsync(i =>
                i.InvoiceNo == number &&
                (!excludeId.HasValue || i.ID != excludeId.Value));
            if (duplicate)
            {
                _errors.AddModelError("InvoiceNo", "Shu raqamli invoys allaqachon mavjud (o'chirilganlar orasida ham bo'lishi mumkin).");
                return null;
            }

            return number;
        }

        /// <summary>
        /// Invoys qatorlarini navigatsiya orqali qo'shadi va QQSsiz jami summani qaytaradi.
        /// Mahsulot birliklari bitta so'rovda olinadi (avval har qatorga FindAsync, SRM-P06).
        /// </summary>
        private async Task<double> AddLinesAsync(P_INVOICES invoice, IEnumerable<PInvoiceProductViewModel> lines)
        {
            var list = (lines ?? Enumerable.Empty<PInvoiceProductViewModel>()).ToList();
            if (list.Count == 0)
            {
                return 0;
            }

            var productIds = list.Select(l => l.ProductID).Distinct().ToList();
            var productUnits = await _db.PRODUCTS.AsNoTracking()
                .Where(p => productIds.Contains(p.ID))
                .Select(p => new { p.ID, p.UnitID })
                .ToDictionaryAsync(p => p.ID, p => p.UnitID);

            double total = 0;
            foreach (var line in list)
            {
                // Foydalanuvchi tanlagan birlik saqlanadi (masalan, m3/CBM). Avval kesiladigan mahsulotlar
                // majburan donaga (UnitID=1) o'tkazilardi — m3 da kiritilgan miqdor donaga aylanib qolardi.
                int productUnit;
                int unitId = line.UnitID > 0
                    ? line.UnitID
                    : (productUnits.TryGetValue(line.ProductID, out productUnit) ? productUnit : 1);

                var invoiceProduct = new P_INVOICE_PRODUCTS
                {
                    ProductID = line.ProductID,
                    Quantity = line.Quantity,
                    Price = line.Price,
                    TotalPrice = line.Price * line.Quantity,
                    UnitID = unitId
                };

                // FK navigatsiya orqali: yangi invoysda ID SaveChanges'da beriladi
                invoiceProduct.P_INVOICES = invoice;
                _db.P_INVOICE_PRODUCTS.Add(invoiceProduct);
                total += invoiceProduct.TotalPrice;
            }

            return total;
        }

        private void AddDocument(P_INVOICES invoice, HttpPostedFileBase file)
        {
            if (!PdfUpload.HasFile(file))
            {
                return;
            }

            _db.P_INVOICE_DOCS.Add(new P_INVOICE_DOCS { P_INVOICES = invoice, Doc = PdfUpload.Read(file) });
        }
    }
}
