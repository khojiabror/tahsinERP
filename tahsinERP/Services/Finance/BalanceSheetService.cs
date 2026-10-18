using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;
using tahsinERP.ViewModels.Finance;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Balans varag'ining YAGONA hisob "engine"i (FIN-B02/B03/P04, D3).
    /// Avval BalanceSheet va BuildBalanceCategoryDetails (details sahifalari, DCF, Liabilities) ikki xil hisoblardi:
    /// details'da "Doimiy harajatlar qarzi" yo'q edi, CalculationType=3 kreditorlik balansda hisob qoldig'idan,
    /// details'da hujjatlardan olinardi. Endi barcha sahifalar <see cref="Compute"/> natijasini filtrlaydi.
    /// Natija so'rov davomida sana bo'yicha keshlanadi (DCF ikki marta to'liq hisoblamaydi).
    /// </summary>
    public sealed class BalanceSheetService
    {
        private readonly DBTHSNEntities _db;
        private readonly CounterpartyBalanceService _counterparties;
        private readonly Dictionary<DateTime, List<BalanceCategoryDetailData>> _cache = new Dictionary<DateTime, List<BalanceCategoryDetailData>>();

        public BalanceSheetService(DBTHSNEntities db, CounterpartyBalanceService counterparties = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _counterparties = counterparties ?? new CounterpartyBalanceService(db);
        }

        public CounterpartyBalanceService Counterparties => _counterparties;

        /// <summary>
        /// AR/AP/oylik kabi subledger kategoriyalari dinamik (FIN_OPENING_CONTRACT_BALANCES + harakatlar) hisoblanadi —
        /// ular uchun kategoriya darajasidagi FIN_BALANCE_SNAPSHOTS "OpeningBalance" yozuvi ishlatilmaydi.
        /// Kategoriya SystemCode bo'yicha aniqlanadi (avval nomidagi "qarz"/"haq"/"oylik" so'zlari bo'yicha edi).
        /// </summary>
        public static bool IsDetailedSubledgerCategory(FIN_BALANCE_CATEGORIES category)
        {
            return SystemCodes.BalanceCategory.IsSubledger(category);
        }

        private static bool Is(FIN_BALANCE_CATEGORIES category, string systemCode)
        {
            return SystemCodes.BalanceCategory.Is(category, systemCode);
        }

        private static string GetCalculationTypeName(int? calculationType)
        {
            switch (calculationType)
            {
                case 1: return "Account-Based";
                case 2: return "Transaction-Based";
                case 3: return "Document-Based";
                default: return "Noma'lum";
            }
        }

        /// <summary>Faol kategoriyalar bo'yicha balans (SortOrder tartibida), har biri aktiv/passiv ustuni bilan.</summary>
        public List<BalanceCategoryDetailData> Compute(DateTime asOfDate)
        {
            if (_cache.TryGetValue(asOfDate, out var cached))
                return cached;

            var all = Build(asOfDate, null);
            _cache[asOfDate] = all;
            return all;
        }

        /// <summary>Faqat shartga mos kategoriyalar (details sahifalari boshqa ustunni hisoblamaydi).</summary>
        private List<BalanceCategoryDetailData> Compute(DateTime asOfDate, Func<FIN_BALANCE_CATEGORIES, bool, bool> include)
        {
            if (_cache.TryGetValue(asOfDate, out var cached))
                return cached.Where(d => include(d.Category, d.IsAssetColumn)).ToList();
            return Build(asOfDate, include);
        }

        private List<BalanceCategoryDetailData> Build(DateTime asOfDate, Func<FIN_BALANCE_CATEGORIES, bool, bool> include)
        {
            var db = _db;
            var categories = db.FIN_BALANCE_CATEGORIES.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.ID)
                .ToList();

            // Faqat opening snapshot; qolgan snapshotlar balansni "muzlatmaydi" — tizim dinamik hisoblaydi.
            var openingSnapshots = db.FIN_BALANCE_SNAPSHOTS
                .Where(s => s.SnapshotDate <= asOfDate && s.SourceType == "OpeningBalance")
                .GroupBy(s => s.CategoryID)
                .Select(g => new
                {
                    CategoryID = g.Key,
                    Latest = g.OrderByDescending(s => s.SnapshotDate).FirstOrDefault()
                })
                .ToList()
                .ToDictionary(x => x.CategoryID, x => new { x.Latest.Amount, x.Latest.SnapshotDate });

            foreach (var c in categories.Where(IsDetailedSubledgerCategory))
            {
                openingSnapshots.Remove(c.ID);
            }

            var accounts = db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsActive).ToList();

            // Hisob → kategoriya: avval FIN_ACCOUNTS.BalanceCategoryID; bog'lanmagan bo'lsa — naqd hisob CASH,
            // bank hisobi BANK tizim kategoriyasiga (avval nomlar bir-biriga Contains bilan solishtirilardi).
            var mappedAccountsByCategory = accounts
                .Where(a => a.BalanceCategoryID.HasValue)
                .GroupBy(a => a.BalanceCategoryID.Value)
                .ToDictionary(g => g.Key, g => g.Select(a => a.ID).ToList());
            var accountToBalanceCategory = new Dictionary<int, int>();
            foreach (var account in accounts)
            {
                var matchingCategory = categories.FirstOrDefault(c =>
                    (account.IsCash && Is(c, SystemCodes.BalanceCategory.Cash)) ||
                    (account.IsBank && Is(c, SystemCodes.BalanceCategory.Bank)));
                if (matchingCategory != null)
                {
                    accountToBalanceCategory[account.ID] = matchingCategory.ID;
                }
            }

            // Transaction-Based kategoriyalar uchun bog'langan FIN_CATEGORIES (EF navigatsiyasi, bitta so'rov — D4).
            var transactionBasedIds = categories.Where(c => c.CalculationType == 2).Select(c => c.ID).ToList();
            var mappedFinCategories = transactionBasedIds.Count == 0
                ? new Dictionary<int, List<int>>()
                : db.FIN_BALANCE_CATEGORIES
                    .Where(c => transactionBasedIds.Contains(c.ID))
                    .Select(c => new { c.ID, Cats = c.FIN_CATEGORIES.Where(fc => fc.IsActive).Select(fc => fc.ID) })
                    .ToList()
                    .ToDictionary(x => x.ID, x => x.Cats.ToList());

            var details = new List<BalanceCategoryDetailData>();
            foreach (var category in categories)
            {
                var isAssetColumn = BalanceCategoryDisplayHelper.IsAssetColumn(category);
                if (include != null && !include(category, isAssetColumn))
                {
                    continue;
                }

                var detail = new BalanceCategoryDetailData
                {
                    Category = category,
                    CalculationTypeName = GetCalculationTypeName(category.CalculationType),
                    IsAssetColumn = isAssetColumn
                };
                details.Add(detail);

                DateTime? openingDate = null;
                if (openingSnapshots.TryGetValue(category.ID, out var opening))
                {
                    detail.Amount = opening.Amount;
                    openingDate = opening.SnapshotDate.Date;
                    detail.Explanation = $"Opening balans ({openingDate.Value:dd.MM.yyyy}) + keyingi harakatlar bo'yicha";
                }

                // Kreditorlik/debitorlik — CalculationType'dan qat'i nazar hujjatlar bo'yicha (FIFO).
                if (Is(category, SystemCodes.BalanceCategory.Payables))
                {
                    FillPayables(detail, asOfDate);
                    continue;
                }
                if (Is(category, SystemCodes.BalanceCategory.Receivables))
                {
                    FillReceivables(detail, asOfDate);
                    continue;
                }

                // Ombor: opening bo'lsa — shu qiymat (override), bo'lmasa ombor hisobi.
                if (Is(category, SystemCodes.BalanceCategory.FinishedGoods))
                {
                    if (!openingDate.HasValue)
                    {
                        detail.Amount = BalanceInventoryValuation.FinishedGoodsTotal(db, asOfDate);
                        detail.CalculationTypeName = "Ombor + Tannarx";
                        detail.Explanation =
                            "Ishlab chiqarish omboridagi tayyor maxsulot (IsFinishedGood) miqdori × shu mahsulot bo'yicha oxirgi Tannarx: SalesPriceCIPWithVAT (Sotuv narxi QQS bilan CIP).";
                    }
                    else
                    {
                        detail.CalculationTypeName = "Opening balans";
                        detail.Explanation = $"Opening balans ({openingDate.Value:dd.MM.yyyy}) qiymati (ombor hisobining o'rniga).";
                    }
                    continue;
                }
                if (Is(category, SystemCodes.BalanceCategory.RawMaterials))
                {
                    if (!openingDate.HasValue)
                    {
                        detail.Amount = BalanceInventoryValuation.RawMaterialTotal(db, asOfDate);
                        detail.CalculationTypeName = "Ombor + oxirgi kirim";
                        detail.Explanation =
                            "Xomashyo ombori qoldig'i (mahsulot kategoriyasi «Jihoz» bo'lmaganlar) × shu omborga qilingan oxirgi kirim birlik narxi (TotalPrice/Amount yoki PiecePrice).";
                    }
                    else
                    {
                        detail.CalculationTypeName = "Opening balans";
                        detail.Explanation = $"Opening balans ({openingDate.Value:dd.MM.yyyy}) qiymati (ombor hisobining o'rniga).";
                    }
                    continue;
                }

                switch (category.CalculationType)
                {
                    case 1: // Account-Based
                        if (!mappedAccountsByCategory.TryGetValue(category.ID, out var mappedAccountIds))
                        {
                            mappedAccountIds = accountToBalanceCategory
                                .Where(kvp => kvp.Value == category.ID)
                                .Select(kvp => kvp.Key)
                                .ToList();
                        }

                        if (mappedAccountIds.Count > 0)
                        {
                            var accountTxnQuery = FinQueries.Active(db)
                                .Where(t => mappedAccountIds.Contains(t.AccountID) &&
                                            t.TxnDate <= asOfDate &&
                                            (!openingDate.HasValue || t.TxnDate > openingDate.Value));
                            // Opening bo'lmasa: asOfDate gacha yakuniy qoldiq; bo'lsa: opening + keyingi harakatlar.
                            detail.Amount += PopulateTransactionStats(accountTxnQuery, detail).Net;
                            detail.Explanation = openingDate.HasValue
                                ? $"Opening balans + hisob harakatlari: {mappedAccountIds.Count} ta hisob"
                                : $"Hisoblar bo'yicha hisoblandi: {mappedAccountIds.Count} ta hisob";
                        }
                        else
                        {
                            detail.Explanation = "Hisoblar moslanmadi";
                        }
                        break;

                    case 2: // Transaction-Based
                        var categoryIds = mappedFinCategories.TryGetValue(category.ID, out var ids) ? ids : new List<int>();
                        if (categoryIds.Count > 0)
                        {
                            var txnQuery = FinQueries.Active(db)
                                .Where(t => categoryIds.Contains(t.CategoryID) &&
                                            t.TxnDate <= asOfDate &&
                                            (!openingDate.HasValue || t.TxnDate > openingDate.Value));
                            detail.Amount += PopulateTransactionStats(txnQuery, detail).Net;
                            detail.Explanation = openingDate.HasValue
                                ? $"Opening balans + kategoriyalar harakati: {categoryIds.Count} ta kategoriya"
                                : $"Kategoriyalar bo'yicha hisoblandi: {categoryIds.Count} ta kategoriya";
                        }
                        else
                        {
                            detail.Explanation = "Kategoriya moslanmadi";
                        }
                        break;

                    case 3: // Document-Based
                        if (Is(category, SystemCodes.BalanceCategory.PayrollDebt))
                        {
                            // FIN-B18: ReportsHub bilan bir xil funksiya; oylik opening'i (BalanceType 3) hisobga olinadi
                            var payroll = FinLiabilities.PayrollByEmployee(db, _counterparties, asOfDate);
                            PopulateTransactionStats(payroll.PaymentQuery, detail);
                            detail.Amount = payroll.Total;
                            detail.Explanation = payroll.OpeningDate.HasValue
                                ? $"Oylik opening ({payroll.OpeningDate.Value:dd.MM.yyyy}) {payroll.OpeningNet:N2} + hisoblangan {payroll.Accrued:N2} − to'langan {payroll.Paid:N2}"
                                : $"Oyliklar bo'yicha: hisoblangan {payroll.Accrued:N2}, to'langan {payroll.Paid:N2}";
                        }
                        else if (Is(category, SystemCodes.BalanceCategory.LogisticsDebt))
                        {
                            var logistics = FinLiabilities.Logistics(db, _counterparties, asOfDate);
                            if (logistics.PaymentQuery != null)
                            {
                                PopulateTransactionStats(logistics.PaymentQuery, detail);
                            }
                            detail.Amount = logistics.Amount;
                            detail.Explanation = logistics.OpeningDate.HasValue
                                ? $"Opening balans ({logistics.OpeningDate.Value:dd.MM.yyyy}) + yo'l varaqalari {logistics.WaybillTotal:N2} − to'lovlar {logistics.Paid:N2}"
                                : $"Yo'l varaqalari bo'yicha: {logistics.WaybillTotal:N2}, to'lovlar {logistics.Paid:N2}";
                        }
                        else if (Is(category, SystemCodes.BalanceCategory.RecurringObligations))
                        {
                            // Avval faqat BalanceSheet'da bor edi, details/DCF/Liabilities'da yo'q edi (FIN-B02).
                            var recurring = FinLiabilities.RecurringObligationsLiability(db, category.ID, asOfDate, openingDate, includeUnassigned: true);
                            detail.Amount += recurring.Amount;
                            detail.Explanation = $"Doimiy harajatlar: {recurring.Count} ta majburiyat, hisoblangan {recurring.Accrued:N2}, to'langan {recurring.Paid:N2}";
                        }
                        break;
                }
            }

            return details;
        }

        /// <summary>Balans varag'i (ikkala ustun).</summary>
        public List<BalanceCategoryData> BalanceSheet(DateTime asOfDate)
        {
            return Compute(asOfDate)
                .Select(d => new BalanceCategoryData { Category = d.Category, Amount = d.Amount, IsAssetColumn = d.IsAssetColumn })
                .ToList();
        }

        /// <summary>Passivlar va kapital ustuni (Type 2/3, aktiv nomlilar bundan mustasno).</summary>
        public List<BalanceCategoryDetailData> LiabilityDetails(DateTime asOfDate)
        {
            return Compute(asOfDate, (c, isAsset) => (c.Type == 2 || c.Type == 3) && !isAsset);
        }

        /// <summary>Aktivlar ustuni (Type 1 va aktiv nomli Type 2).</summary>
        public List<BalanceCategoryDetailData> AssetDetails(DateTime asOfDate)
        {
            return Compute(asOfDate, (c, isAsset) => (c.Type == 1 || c.Type == 2) && isAsset);
        }

        private void FillPayables(BalanceCategoryDetailData detail, DateTime asOfDate)
        {
            var apOpening = _counterparties.LatestOpeningDate(asOfDate, CounterpartyBalanceService.BalanceTypePayable);
            var summary = _counterparties.Suppliers(asOfDate, apOpening);
            detail.Amount = summary.Sum(s => s.Outstanding);
            detail.TransactionCount = summary.Sum(s => s.InvoiceCount + s.PaymentCount);
            detail.AddedAmount = summary.Sum(s => s.InvoiceTotal);
            detail.SubtractedAmount = summary.Sum(s => s.PaymentTotal);
            detail.CalculationTypeName = "Document-Based";
            detail.Explanation = apOpening.HasValue
                ? $"Opening balans ({apOpening.Value:dd.MM.yyyy}) + SUM(invoys) − SUM(to'lov). To'lov: PurchaseInvoicePayment yoki ta'minotchi + ContractNo bilan chiqim."
                : "SUM(invoys) − SUM(to'lov). To'lov: PurchaseInvoicePayment yoki ta'minotchi + ContractNo bilan chiqim.";
        }

        private void FillReceivables(BalanceCategoryDetailData detail, DateTime asOfDate)
        {
            var arOpening = _counterparties.LatestOpeningDate(asOfDate, CounterpartyBalanceService.BalanceTypeReceivable);
            var summary = _counterparties.Customers(asOfDate, arOpening);
            detail.Amount = summary.Sum(c => c.Outstanding);
            detail.TransactionCount = summary.Sum(c => c.InvoiceCount + c.ReceiptCount);
            detail.AddedAmount = summary.Sum(c => c.InvoiceTotal);
            detail.SubtractedAmount = summary.Sum(c => c.ReceiptTotal);
            detail.CalculationTypeName = "Document-Based";
            detail.Explanation = arOpening.HasValue
                ? $"Opening balans ({arOpening.Value:dd.MM.yyyy}) + sotuv invoyslari − mijozdan tushumlar (FIFO) bo'yicha hisoblandi"
                : "Sotuv invoyslari va mijozdan tushumlar (FIFO) bo'yicha hisoblandi";
        }

        private sealed class TxnStats
        {
            public int Count { get; set; }
            public decimal Added { get; set; }
            public decimal Subtracted { get; set; }
            public decimal Net { get; set; }
        }

        /// <summary>Soni, kirim, chiqim va sof summa — bitta so'rov bilan (avval 3–4 ta).</summary>
        private static TxnStats PopulateTransactionStats(IQueryable<FIN_TRANSACTIONS> query, BalanceCategoryDetailData detail)
        {
            var stats = query
                .GroupBy(t => 1)
                .Select(g => new TxnStats
                {
                    Count = g.Count(),
                    Added = g.Sum(t => t.Direction == 1 ? t.Amount : 0m),
                    Subtracted = g.Sum(t => t.Direction != 1 ? t.Amount : 0m),
                    Net = g.Sum(t => t.Direction == 1 ? t.Amount : -t.Amount)
                })
                .FirstOrDefault() ?? new TxnStats();

            detail.TransactionCount = stats.Count;
            detail.AddedAmount = stats.Added;
            detail.SubtractedAmount = stats.Subtracted;
            return stats;
        }

        /// <summary>Balans popup: kategoriya bo'yicha so'nggi 20 ta qo'shilgan/ayirilgan harakat (yoki ombor qatorlari).</summary>
        public object Movements(int categoryId, DateTime asOfDate)
        {
            var db = _db;
            var category = db.FIN_BALANCE_CATEGORIES.AsNoTracking().FirstOrDefault(c => c.ID == categoryId && c.IsActive);
            if (category == null)
            {
                return new { success = false, message = "Kategoriya topilmadi" };
            }

            if (Is(category, SystemCodes.BalanceCategory.FinishedGoods))
            {
                var wh = BalanceInventoryValuation.ResolveProductionWarehouse(db);
                return new
                {
                    success = true,
                    breakdownMode = true,
                    warehouseName = wh != null ? wh.WHName : null,
                    breakdown = BalanceInventoryValuation.FinishedGoodsLines(db, asOfDate),
                    added = new object[0],
                    subtracted = new object[0]
                };
            }

            if (Is(category, SystemCodes.BalanceCategory.RawMaterials))
            {
                var wh = BalanceInventoryValuation.ResolveRawMaterialWarehouse(db);
                return new
                {
                    success = true,
                    breakdownMode = true,
                    warehouseName = wh != null ? wh.WHName : null,
                    breakdown = BalanceInventoryValuation.RawMaterialLines(db, asOfDate),
                    added = new object[0],
                    subtracted = new object[0]
                };
            }

            // Subledger kategoriyalari uchun opening sanasi dinamik manbadan, aks holda kategoriya snapshot'idan.
            DateTime? fromExclusive;
            if (IsDetailedSubledgerCategory(category))
            {
                int balanceType = Is(category, SystemCodes.BalanceCategory.Receivables)
                    ? CounterpartyBalanceService.BalanceTypeReceivable
                    : (Is(category, SystemCodes.BalanceCategory.PayrollDebt) ? CounterpartyBalanceService.BalanceTypePayroll : CounterpartyBalanceService.BalanceTypePayable);
                fromExclusive = _counterparties.LatestOpeningDate(asOfDate, balanceType);
            }
            else
            {
                fromExclusive = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => s.CategoryID == categoryId && s.SourceType == "OpeningBalance" && s.SnapshotDate <= asOfDate)
                    .OrderByDescending(s => s.SnapshotDate)
                    .Select(s => (DateTime?)s.SnapshotDate)
                    .FirstOrDefault();
            }

            const int top = 20;

            if (Is(category, SystemCodes.BalanceCategory.Payables))
            {
                // Popup'da invoys/to'lovlar to'liq ko'rsatiladi (opening'dan keyingisi emas, asOfDate gacha).
                var addedRaw = db.P_INVOICES.AsNoTracking()
                    .Where(i => i.InvoiceDate <= asOfDate && (i.IsDeleted == null || i.IsDeleted == false))
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(top)
                    .Select(i => new { i.ID, i.InvoiceDate, i.InvoiceNo, i.Amount, SupplierName = i.SUPPLIER != null ? i.SUPPLIER.Name : "" })
                    .ToList();

                var invoiceIds = addedRaw.Select(i => i.ID).ToList();
                var incomeTotalsByInvoice = invoiceIds.Count == 0
                    ? new Dictionary<int, decimal>()
                    : db.INCOMES
                        .Where(inc => inc.InvoiceID.HasValue &&
                                      invoiceIds.Contains(inc.InvoiceID.Value) &&
                                      inc.IsDeleted != true &&
                                      inc.TotalPrice.HasValue)
                        .GroupBy(inc => inc.InvoiceID.Value)
                        .Select(g => new { InvoiceID = g.Key, Total = g.Sum(x => x.TotalPrice.Value) })
                        .ToList()
                        .ToDictionary(x => x.InvoiceID, x => Convert.ToDecimal(x.Total));

                var added = addedRaw
                    .Select(i => new
                    {
                        date = i.InvoiceDate,
                        doc = i.InvoiceNo,
                        amount = incomeTotalsByInvoice.ContainsKey(i.ID) ? incomeTotalsByInvoice[i.ID] : (decimal)(i.Amount ?? 0d),
                        note = i.SupplierName
                    })
                    .ToList();

                var subtracted = FinQueries.Active(db)
                    .Where(t => t.Direction == 2 &&
                                t.TxnDate <= asOfDate &&
                                t.CounterpartyType == 2 &&
                                t.CounterpartyID.HasValue &&
                                (t.SourceDocType == null || (t.SourceDocType != FinQueries.PurchaseInvoiceMirror && t.SourceDocType != FinQueries.SalesInvoiceMirror)) &&
                                ((t.SourceDocType == "PurchaseInvoicePayment" && t.SourceDocID.HasValue) ||
                                 !string.IsNullOrEmpty(t.ContractNo)))
                    .OrderByDescending(t => t.TxnDate)
                    .Take(top)
                    .Select(t => new { t.TxnDate, t.SourceDocType, t.Amount, t.ContractNo, t.Purpose, t.Comment })
                    .ToList()
                    .Select(t => new
                    {
                        date = t.TxnDate,
                        doc = t.SourceDocType ?? "FinTransaction",
                        amount = t.Amount,
                        note = (!string.IsNullOrEmpty(t.ContractNo) ? "[" + t.ContractNo + "] " : "") + (t.Purpose ?? t.Comment ?? "")
                    })
                    .ToList();

                return new { success = true, added, subtracted };
            }

            if (Is(category, SystemCodes.BalanceCategory.Receivables))
            {
                var added = db.SALES_INVOICES.Active()
                    .Where(i => i.InvoiceDate <= asOfDate &&
                                (!fromExclusive.HasValue || i.InvoiceDate > fromExclusive.Value))
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(top)
                    .Select(i => new
                    {
                        date = i.InvoiceDate,
                        doc = i.InvoiceNo,
                        amount = i.TotalWithVAT ?? i.TotalAmount,
                        note = (i.CUSTOMER != null ? i.CUSTOMER.Name : "")
                    })
                    .ToList();

                // Sotuv tushumi — SALES tizim kategoriyasi (avval nomida "Sotuv" bo'lgan barcha daromad kategoriyalari)
                var salesCategoryIds = db.FIN_CATEGORIES
                    .Where(c => c.IsActive && c.SystemCode == SystemCodes.FinCategory.Sales)
                    .Select(c => c.ID)
                    .ToList();

                var subtracted = FinQueries.Active(db)
                    .Where(t => t.Direction == 1 &&
                                t.TxnDate <= asOfDate &&
                                (!fromExclusive.HasValue || t.TxnDate > fromExclusive.Value) &&
                                t.SourceDocType != FinQueries.SalesInvoiceMirror &&
                                ((t.CounterpartyType == 1 && t.CounterpartyID.HasValue) ||
                                 salesCategoryIds.Contains(t.CategoryID)))
                    .OrderByDescending(t => t.TxnDate)
                    .Take(top)
                    .Select(t => new
                    {
                        date = t.TxnDate,
                        doc = t.SourceDocType ?? "FinTransaction",
                        amount = t.Amount,
                        note = t.Purpose ?? t.Comment
                    })
                    .ToList();

                return new { success = true, added, subtracted };
            }

            if (Is(category, SystemCodes.BalanceCategory.PayrollDebt))
            {
                var headers = db.HR_PAYROLL_HEADERS.AsNoTracking()
                    .Where(p => (p.IsDeleted == null || p.IsDeleted == false) &&
                                (p.Status == "Approved" || p.Status == "Posted") &&
                                p.ApprovedDT.HasValue &&
                                p.ApprovedDT.Value <= asOfDate &&
                                (!fromExclusive.HasValue || p.ApprovedDT.Value > fromExclusive.Value))
                    .OrderByDescending(p => p.ApprovedDT)
                    .Take(top)
                    .Select(p => new
                    {
                        ApprovedDT = p.ApprovedDT.Value,
                        p.PayrollType,
                        p.Year,
                        p.Month,
                        Total = p.HR_PAYROLL_LINES.Where(l => l.IsDeleted == null || l.IsDeleted == false).Sum(l => (decimal?)(l.NetSalary ?? 0)) ?? 0m
                    })
                    .ToList();
                var added = headers
                    .Select(p => new
                    {
                        date = p.ApprovedDT,
                        doc = "HR_PAYROLL",
                        amount = p.Total,
                        note = (p.PayrollType ?? "Hourly") + " " + p.Year + "-" + (p.Month < 10 ? "0" + p.Month : p.Month.ToString())
                    })
                    .ToList();

                var subtracted = FinQueries.PayrollPayments(
                        db,
                        FinQueries.SalaryCategoryIds(db),
                        fromExclusive.HasValue ? fromExclusive.Value.AddDays(1) : DateTime.MinValue,
                        asOfDate)
                    .OrderByDescending(t => t.TxnDate)
                    .Take(top)
                    .Select(t => new
                    {
                        date = t.TxnDate,
                        doc = t.SourceDocType ?? "FinTransaction",
                        amount = t.Amount,
                        note = t.Purpose ?? t.Comment
                    })
                    .ToList();

                return new { success = true, added, subtracted };
            }

            var mappedCategoryIds = db.FIN_CATEGORIES
                .Where(fc => fc.IsActive && fc.FIN_BALANCE_CATEGORIES.Any(b => b.ID == categoryId))
                .Select(fc => fc.ID)
                .ToList();
            var mappedAccountIds = db.FIN_ACCOUNTS
                .Where(a => a.IsActive && a.BalanceCategoryID == categoryId)
                .Select(a => a.ID)
                .ToList();

            var baseQuery = FinQueries.Active(db)
                .Where(t => t.TxnDate <= asOfDate &&
                            (!fromExclusive.HasValue || t.TxnDate > fromExclusive.Value) &&
                            (mappedCategoryIds.Contains(t.CategoryID) || mappedAccountIds.Contains(t.AccountID)));

            var defaultAdded = baseQuery
                .Where(t => t.Direction == 1)
                .OrderByDescending(t => t.TxnDate)
                .Take(top)
                .Select(t => new { date = t.TxnDate, doc = t.SourceDocType ?? "FinTransaction", amount = t.Amount, note = t.Purpose ?? t.Comment })
                .ToList();
            var defaultSubtracted = baseQuery
                .Where(t => t.Direction != 1)
                .OrderByDescending(t => t.TxnDate)
                .Take(top)
                .Select(t => new { date = t.TxnDate, doc = t.SourceDocType ?? "FinTransaction", amount = t.Amount, note = t.Purpose ?? t.Comment })
                .ToList();

            return new { success = true, added = defaultAdded, subtracted = defaultSubtracted };
        }
    }
}
