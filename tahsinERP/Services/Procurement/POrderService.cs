using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.ViewModels.POrder;

namespace tahsinERP.Services.Procurement
{
    /// <summary>
    /// Xarid buyurtmalari: ro'yxat so'rovi, yozish amallari va buyurtma raqami (SRM-M01 — POrdersController'dan ajratildi).
    /// Validatsiya xatolari berilgan ModelStateDictionary'ga yoziladi; xato bo'lsa yozish amali null/false qaytaradi.
    /// </summary>
    public sealed class POrderService
    {
        private readonly DBTHSNEntities _db;
        private int? _piecesUnitId;

        /// <summary>"dona" birligi (UNITS.SystemCode = PCS) — birlik ko'rsatilmaganda zaxira qiymat.</summary>
        private int PiecesUnitId()
        {
            if (!_piecesUnitId.HasValue)
                _piecesUnitId = SystemRefs.UnitId(_db, SystemCodes.Unit.Pieces) ?? 0;
            return _piecesUnitId.Value;
        }
        private readonly ModelStateDictionary _errors;

        public POrderService(DBTHSNEntities db, ModelStateDictionary errors)
        {
            _db = db;
            _errors = errors;
        }

        // ---------- ro'yxat (SRM-P03) ----------

        /// <summary>Buyurtmalar ro'yxati qatori (proyeksiya).</summary>
        public sealed class IndexRow
        {
            public int ID { get; set; }
            public string OrderNo { get; set; }
            public byte PurchaseType { get; set; }
            public DateTime IssuedDate { get; set; }
            public double? Amount { get; set; }
            public string Currency { get; set; }
        }

        public static IQueryable<IndexRow> IndexQuery(DBTHSNEntities db, string purchaseTypeFilter, int? supplierID, int? contractID)
        {
            var query = db.P_ORDERS.AsNoTracking().Where(o => o.IsDeleted != true);

            if (supplierID.HasValue && supplierID.Value > 0)
            {
                query = query.Where(o => o.SupplierID == supplierID.Value);
            }

            if (contractID.HasValue && contractID.Value > 0)
            {
                query = query.Where(o => o.ContractID == contractID.Value);
            }

            if (!string.IsNullOrEmpty(purchaseTypeFilter))
            {
                byte purchaseType = purchaseTypeFilter == "Indirect" ? ProcurementLookups.IndirectPurchase : ProcurementLookups.DirectPurchase;
                query = query.Where(o => o.PurchaseType == purchaseType);
            }

            return query.Select(o => new IndexRow
            {
                ID = o.ID,
                OrderNo = o.OrderNo,
                PurchaseType = o.PurchaseType,
                IssuedDate = o.IssuedDate,
                Amount = o.Amount,
                Currency = o.CURRENCy.Shortname
            });
        }

        public static IQueryable<IndexRow> SearchIndex(IQueryable<IndexRow> query, string s)
        {
            return query.Where(r => r.OrderNo.Contains(s) || r.Currency.Contains(s));
        }

        public static IOrderedQueryable<IndexRow> OrderIndex(IQueryable<IndexRow> query, int column, bool desc)
        {
            switch (column)
            {
                case 0: return ServerTablePage.By(query, r => r.OrderNo, desc).ThenByDescending(r => r.ID);
                case 1: return ServerTablePage.By(query, r => r.PurchaseType, desc).ThenByDescending(r => r.ID);
                case 3: return ServerTablePage.By(query, r => r.Amount, desc).ThenByDescending(r => r.ID);
                case 4: return ServerTablePage.By(query, r => r.Currency, desc).ThenByDescending(r => r.ID);
                default: return desc ? query.OrderByDescending(r => r.IssuedDate).ThenByDescending(r => r.ID)
                                     : query.OrderBy(r => r.IssuedDate).ThenBy(r => r.ID);
            }
        }

        // ---------- yozish ----------

        /// <summary>
        /// Buyurtma yaratadi: raqam, sarlavha, qatorlar va (talabnomadan bo'lsa) talabnoma holati — bitta tranzaksiyada.
        /// Raqam takrorlansa null (ModelState'da xato).
        /// </summary>
        public async Task<P_ORDERS> CreateAsync(POrderCreateViewModel model, int? userId)
        {
            using (var transaction = _db.Database.BeginTransaction())
            {
                // OrderNo berilmagan bo'lsa — qulf bilan avtomatik (SRM-B14)
                if (string.IsNullOrWhiteSpace(model.OrderNo))
                {
                    model.OrderNo = await GenerateOrderNoAsync();
                }
                else
                {
                    model.OrderNo = model.OrderNo.Trim();
                    if (await ActiveOrderNoExistsAsync(model.OrderNo, excludeOrderId: null))
                    {
                        _errors.AddModelError("OrderNo", "Bu buyurtma raqami allaqachon mavjud (faol buyurtma).");
                        return null;
                    }

                    await ReleaseOrderNoFromSoftDeletedRowsAsync(model.OrderNo, excludeOrderId: null);
                }

                var companyId = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(_db);

                // Xarid turi: talabnoma asosida bo'lsa bilvosita
                byte purchaseType = ProcurementLookups.DirectPurchase;
                P_REQUISITIONS requisition = null;
                if (model.RequisitionID.HasValue)
                {
                    requisition = await _db.P_REQUISITIONS.FindAsync(model.RequisitionID.Value);
                    if (requisition != null)
                    {
                        purchaseType = ProcurementLookups.IndirectPurchase;
                    }
                }

                var supplierId = model.SupplierID;
                if (purchaseType == ProcurementLookups.DirectPurchase || !supplierId.HasValue || supplierId.Value <= 0)
                {
                    supplierId = null;
                }

                int? contractId = model.ContractID;
                if (purchaseType == ProcurementLookups.DirectPurchase || !contractId.HasValue || contractId.Value <= 0)
                {
                    contractId = null;
                }

                var order = new P_ORDERS
                {
                    OrderNo = model.OrderNo,
                    IssuedDate = model.IssuedDate,
                    CompanyID = companyId,
                    SupplierID = supplierId,
                    ContractID = contractId,
                    CurrencyID = model.CurrencyID ?? 0,
                    Description = model.Description,
                    IsDeleted = false,
                    Amount = 0,
                    PurchaseType = purchaseType,
                    RequisitionID = requisition?.ID,
                    DepartmentID = requisition?.DepartmentID,
                    RequesterID = requisition?.RequesterID,
                    ContactPhone = requisition?.ContactPhone,
                    Purpose = requisition?.Purpose,
                    Status = "Created",
                    ContractStatus = "NotCreated",
                    InvoiceStatus = "NotCreated",
                    WarehouseStatus = "NotReceived"
                };
                _db.P_ORDERS.Add(order);

                // Buyurtma mahsulotlari: mahsulotlar va talabnoma qatorlari oldindan bitta so'rovda (SRM-P06)
                var lines = (model.OrderProducts ?? new List<POrderProductViewModel>()).Where(l => l.ProductID > 0).ToList();
                var productIds = lines.Select(l => l.ProductID).Distinct().ToList();
                var products = await _db.PRODUCTS.AsNoTracking()
                    .Where(p => productIds.Contains(p.ID) && p.IsDeleted != true)
                    .Select(p => new { p.ID, p.UnitID })
                    .ToDictionaryAsync(p => p.ID, p => p.UnitID);

                var requisitionLines = new Dictionary<int, P_REQUISITION_PRODUCTS>();
                if (requisition != null)
                {
                    requisitionLines = (await _db.P_REQUISITION_PRODUCTS.AsNoTracking()
                            .Where(rp => rp.RequisitionID == requisition.ID && productIds.Contains(rp.ProductID))
                            .ToListAsync())
                        .GroupBy(rp => rp.ProductID)
                        .ToDictionary(g => g.Key, g => g.First());
                }

                double totalAmount = 0;
                foreach (var orderProduct in lines)
                {
                    if (!products.ContainsKey(orderProduct.ProductID))
                    {
                        continue; // o'chirilgan yoki topilmagan mahsulot
                    }
                    var productUnitId = products[orderProduct.ProductID];

                    // Talabnomadan ustuvorlik va kerakli sana (mavjud bo'lsa).
                    // Hisob tushuntirish (sotuv/BOM generatsiyasi) bo'lsa — Priority maydonida saqlanadi.
                    string priority = null;
                    DateTime? requiredDate = null;
                    P_REQUISITION_PRODUCTS reqProduct;
                    if (requisitionLines.TryGetValue(orderProduct.ProductID, out reqProduct))
                    {
                        priority = reqProduct.Priority;
                        requiredDate = reqProduct.RequiredDate;
                    }

                    if (!string.IsNullOrWhiteSpace(orderProduct.CalculationSummary))
                    {
                        priority = orderProduct.CalculationSummary.Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(orderProduct.CalculationExplanation))
                    {
                        priority = orderProduct.CalculationExplanation.Trim();
                    }

                    var pOrderProduct = new P_ORDER_PRODUCTS
                    {
                        P_ORDERS = order,
                        ProductID = orderProduct.ProductID,
                        Price = orderProduct.Price,
                        Amount = orderProduct.Amount,
                        UnitID = orderProduct.UnitID ?? (productUnitId > 0 ? productUnitId : PiecesUnitId()),
                        TotalPrice = orderProduct.Price * orderProduct.Amount,
                        MOQ = orderProduct.MOQ,
                        Priority = TruncateToPriority(priority),
                        RequiredDate = requiredDate
                    };

                    _db.P_ORDER_PRODUCTS.Add(pOrderProduct);
                    totalAmount += pOrderProduct.TotalPrice;
                }

                // Buyurtmaning umumiy summasi QQS bilan: Amount = net summa + QQS.
                order.Amount = VatMath.ApplyVat(totalAmount, model.VatRate);

                await _db.SaveChangesAsync();

                // Talabnomadan yaratilgan bo'lsa — talabnoma holati (o'sha tranzaksiyada)
                if (requisition != null)
                {
                    requisition.Status = "ConvertedToOrder";
                    requisition.ConvertedToOrderID = order.ID;
                    requisition.ModifiedDT = DateTime.Now;
                    requisition.ModifiedByUserID = userId;
                    await _db.SaveChangesAsync();
                }

                transaction.Commit();
                return order;
            }
        }

        /// <summary>Buyurtmani yangilaydi (qatorlar almashtiriladi, summa qayta hisoblanadi). Raqam takrorlansa false.</summary>
        public async Task<bool> UpdateAsync(P_ORDERS order, POrderEditViewModel model)
        {
            var editOrderNo = string.IsNullOrWhiteSpace(model.OrderNo) ? null : model.OrderNo.Trim();
            if (!string.IsNullOrEmpty(editOrderNo))
            {
                if (await ActiveOrderNoExistsAsync(editOrderNo, excludeOrderId: model.ID))
                {
                    _errors.AddModelError("OrderNo", "Bu buyurtma raqami allaqachon mavjud (faol buyurtma).");
                    return false;
                }

                await ReleaseOrderNoFromSoftDeletedRowsAsync(editOrderNo, excludeOrderId: model.ID);
            }

            using (var tx = _db.Database.BeginTransaction())
            {
                order.OrderNo = editOrderNo ?? model.OrderNo;
                order.IssuedDate = model.IssuedDate;
                order.CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(_db);

                bool isDirectOrder = order.PurchaseType == ProcurementLookups.DirectPurchase;
                int? supplierId = model.SupplierID;
                if (isDirectOrder || !supplierId.HasValue || supplierId.Value <= 0)
                {
                    supplierId = null;
                }

                int? contractId = model.ContractID;
                if (isDirectOrder || !contractId.HasValue || contractId.Value <= 0)
                {
                    contractId = null;
                }

                order.SupplierID = supplierId;
                order.ContractID = contractId;
                order.CurrencyID = model.CurrencyID;
                order.Description = model.Description;

                var existingProducts = await _db.P_ORDER_PRODUCTS.Where(op => op.OrderID == order.ID).ToListAsync();
                _db.P_ORDER_PRODUCTS.RemoveRange(existingProducts);

                // Mahsulot birliklari bitta so'rovda (avval har qatorga FindAsync, SRM-P06)
                var lines = model.OrderProducts ?? new List<POrderProductViewModel>();
                var productIds = lines.Select(l => l.ProductID).Distinct().ToList();
                var products = await _db.PRODUCTS.AsNoTracking()
                    .Where(p => productIds.Contains(p.ID))
                    .Select(p => new { p.ID, p.UnitID })
                    .ToDictionaryAsync(p => p.ID, p => p.UnitID);

                double totalAmount = 0;
                foreach (var orderProduct in lines)
                {
                    if (!products.ContainsKey(orderProduct.ProductID))
                    {
                        continue;
                    }
                    var productUnitId = products[orderProduct.ProductID];

                    var pOrderProduct = new P_ORDER_PRODUCTS
                    {
                        OrderID = order.ID,
                        ProductID = orderProduct.ProductID,
                        Price = orderProduct.Price,
                        Amount = orderProduct.Amount,
                        UnitID = orderProduct.UnitID ?? (productUnitId > 0 ? productUnitId : PiecesUnitId()),
                        TotalPrice = orderProduct.Price * orderProduct.Amount,
                        MOQ = orderProduct.MOQ,
                        Priority = TruncateToPriority(
                            !string.IsNullOrWhiteSpace(orderProduct.CalculationSummary)
                                ? orderProduct.CalculationSummary.Trim()
                                : (string.IsNullOrWhiteSpace(orderProduct.CalculationExplanation)
                                    ? null
                                    : orderProduct.CalculationExplanation.Trim()))
                    };

                    _db.P_ORDER_PRODUCTS.Add(pOrderProduct);
                    totalAmount += pOrderProduct.TotalPrice;
                }

                // Umumiy summa QQS bilan: Amount = net summa + QQS.
                order.Amount = VatMath.ApplyVat(totalAmount, model.VatRate);
                await _db.SaveChangesAsync();
                tx.Commit();
            }

            return true;
        }

        /// <summary>Soft delete va bog'liq havolalarni tozalash. O'chirilgan buyurtma yoki null.</summary>
        public async Task<P_ORDERS> SoftDeleteAsync(int id)
        {
            var order = await _db.P_ORDERS.FindAsync(id);
            if (order == null || order.IsDeleted == true)
            {
                return null;
            }

            // Raqam o'zgartirilmaydi: generator o'chirilganlarni ham hisobga oladi, shuning uchun raqam qayta
            // berilmaydi (SRM-B14). Avval "_d{ID}" qo'shilardi — raqamli qism yo'qolib, keyingi buyurtma aynan shu
            // raqamni olardi. Foydalanuvchi shu raqamni qo'lda kiritsa, ReleaseOrderNoFromSoftDeletedRowsAsync uni bo'shatadi.
            order.IsDeleted = true;
            await SoftDeleteReferenceService.AfterPOrderSoftDeletedAsync(_db, id);
            await _db.SaveChangesAsync();
            return order;
        }

        // ---------- buyurtma raqami ----------

        private const int POrderNoMaxLength = 50;
        private const string ArchivedOrderNoSuffixDeleted = "_deleted";
        private const string ArchivedOrderNoSuffixDl = "_dl";

        /// <summary>PO-{yil}-{oy}-NNNN — qulf bilan; o'chirilgan buyurtmalar raqami ham hisobga olinadi (qayta berilmaydi).</summary>
        private Task<string> GenerateOrderNoAsync()
        {
            var now = DateTime.Now;
            return DocumentNumberService.NextAsync(_db, "P_ORDERS", "OrderNo", $"PO-{now.Year}-{now.Month:D2}-", 4);
        }

        private static string BuildArchivedOrderNo(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return orderNo;
            }

            var trimmed = orderNo.Trim();
            if (trimmed.EndsWith(ArchivedOrderNoSuffixDeleted, StringComparison.OrdinalIgnoreCase)
                || trimmed.EndsWith(ArchivedOrderNoSuffixDl, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            var withDeleted = trimmed + ArchivedOrderNoSuffixDeleted;
            if (withDeleted.Length <= POrderNoMaxLength)
            {
                return withDeleted;
            }

            var withDl = trimmed + ArchivedOrderNoSuffixDl;
            if (withDl.Length <= POrderNoMaxLength)
            {
                return withDl;
            }

            var keepBase = POrderNoMaxLength - ArchivedOrderNoSuffixDl.Length;
            return trimmed.Substring(0, keepBase) + ArchivedOrderNoSuffixDl;
        }

        private async Task<bool> ActiveOrderNoExistsAsync(string orderNo, int? excludeOrderId)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return false;
            }

            var normalized = orderNo.Trim();
            return await _db.P_ORDERS.AnyAsync(o =>
                o.OrderNo == normalized
                && o.IsDeleted != true
                && (!excludeOrderId.HasValue || o.ID != excludeOrderId.Value));
        }

        /// <summary>
        /// O'chirilgan buyurtmalarning OrderNo sini arxivlaydi — yangi yozuv uchun UNIQUE constraint bo'shatiladi.
        /// </summary>
        private async Task ReleaseOrderNoFromSoftDeletedRowsAsync(string orderNo, int? excludeOrderId)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return;
            }

            var normalized = orderNo.Trim();
            var rows = await _db.P_ORDERS
                .Where(o => o.IsDeleted == true
                    && o.OrderNo == normalized
                    && (!excludeOrderId.HasValue || o.ID != excludeOrderId.Value))
                .ToListAsync();

            if (!rows.Any())
            {
                return;
            }

            foreach (var row in rows)
            {
                row.OrderNo = BuildArchivedOrderNo(row.OrderNo);
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// P_ORDER_PRODUCTS.Priority — jadvalda nvarchar(50): talabnomada "ustuvorlik" uchun.
        /// Hisob tushuntirish uzun bo'lsa, saqlashdan oldin qisqartiriladi (to'liq matn DB ga sig'maydi).
        /// </summary>
        private static string TruncateToPriority(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            const int maxLen = 50;
            var t = value.Trim();
            if (t.Length <= maxLen)
            {
                return t;
            }

            return t.Substring(0, maxLen - 3) + "...";
        }
    }
}
