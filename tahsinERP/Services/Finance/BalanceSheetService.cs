using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;
using tahsinERP.ViewModels.Finance;
using tahsinERP.I18n;

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
                case 1: return L.T("Fin_CalcAccountBased");
                case 2: return L.T("Fin_CalcTransactionBased");
                case 3: return L.T("Fin_CalcDocumentBased");
                default: return L.T("Fin_Unknown");
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
            var openingSnapshots = db.FIN_BALANCE_SNAPSHOTS.AsNoTracking()
                .Where(s => s.SnapshotDate <= asOfDate && s.SourceType == "OpeningBalance")
                .GroupBy(s => s.CategoryID)
                .Select(g => new
                {
                    CategoryID = g.Key,
                    Latest = g.OrderByDescending(s => s.SnapshotDate).FirstOrDefault()
                })
                .ToList()
                // FIN-B01: opening summasi so'mga (snapshot sanasidagi kurs)
                .ToDictionary(x => x.CategoryID, x => new { Amount = FinMoney.For(db).Uzs(x.Latest.Amount, x.Latest.CurrencyID, x.Latest.SnapshotDate), x.Latest.SnapshotDate });

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
                : db.FIN_BALANCE_CATEGORIES.AsNoTracking()
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
                    detail.Explanation = L.T("Fin_Exp_OpeningPlusMoves", openingDate.Value.ToString("dd.MM.yyyy"));
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
                        detail.CalculationTypeName = L.T("Fin_Calc_WarehouseCost");
                        detail.Explanation =
                            L.T("Fin_Exp_FinishedGoods");
                    }
                    else
                    {
                        detail.CalculationTypeName = L.T("Fin_OpeningBalance");
                        detail.Explanation = L.T("Fin_Exp_OpeningInsteadOfWarehouse", openingDate.Value.ToString("dd.MM.yyyy"));
                    }
                    continue;
                }
                if (Is(category, SystemCodes.BalanceCategory.RawMaterials))
                {
                    if (!openingDate.HasValue)
                    {
                        detail.Amount = BalanceInventoryValuation.RawMaterialTotal(db, asOfDate);
                        detail.CalculationTypeName = L.T("Fin_Calc_WarehouseLastIncome");
                        detail.Explanation =
                            L.T("Fin_Exp_RawMaterials");
                    }
                    else
                    {
                        detail.CalculationTypeName = L.T("Fin_OpeningBalance");
                        detail.Explanation = L.T("Fin_Exp_OpeningInsteadOfWarehouse", openingDate.Value.ToString("dd.MM.yyyy"));
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
                                ? L.T("Fin_Exp_OpeningAccounts", mappedAccountIds.Count)
                                : L.T("Fin_Exp_ByAccounts", mappedAccountIds.Count);
                        }
                        else
                        {
                            detail.Explanation = L.T("Fin_Exp_NoAccounts");
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
                                ? L.T("Fin_Exp_OpeningCategories", categoryIds.Count)
                                : L.T("Fin_Exp_ByCategories", categoryIds.Count);
                        }
                        else
                        {
                            detail.Explanation = L.T("Fin_Exp_NoCategories");
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
                                ? L.T("Fin_Exp_PayrollOpening", payroll.OpeningDate.Value.ToString("dd.MM.yyyy"), payroll.OpeningNet.ToString("N2"), payroll.Accrued.ToString("N2"), payroll.Paid.ToString("N2"))
                                : L.T("Fin_Exp_Payroll", payroll.Accrued.ToString("N2"), payroll.Paid.ToString("N2"));
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
                                ? L.T("Fin_Exp_LogisticsOpening", logistics.OpeningDate.Value.ToString("dd.MM.yyyy"), logistics.WaybillTotal.ToString("N2"), logistics.Paid.ToString("N2"))
                                : L.T("Fin_Exp_Logistics", logistics.WaybillTotal.ToString("N2"), logistics.Paid.ToString("N2"));
                        }
                        else if (Is(category, SystemCodes.BalanceCategory.RecurringObligations))
                        {
                            // Avval faqat BalanceSheet'da bor edi, details/DCF/Liabilities'da yo'q edi (FIN-B02).
                            var recurring = FinLiabilities.RecurringObligationsLiability(db, category.ID, asOfDate, openingDate, includeUnassigned: true);
                            detail.Amount += recurring.Amount;
                            detail.Explanation = L.T("Fin_Exp_Recurring", recurring.Count, recurring.Accrued.ToString("N2"), recurring.Paid.ToString("N2"));
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
        /// <summary>Passiv ustunida ko'rsatiladigan summa: majburiyat (Type 2) musbat, kapital (Type 3) o'z belgisida.</summary>
        public static decimal PassiveDisplayAmount(FIN_BALANCE_CATEGORIES category, decimal amount)
        {
            return category.Type == 2 ? Math.Abs(amount) : amount;
        }

        /// <summary>
        /// Balans tengligi: Aktiv = Majburiyatlar + Kapital + hisoblangan taqsimlanmagan foyda (balanslovchi).
        /// Taqsimlanmagan foyda kategoriya sifatida kiritilmagan — aktivdan majburiyat va kiritilgan kapital ayirib topiladi.
        /// </summary>
        public BalanceTotals Totals(DateTime asOfDate)
        {
            var all = Compute(asOfDate);
            return new BalanceTotals
            {
                Assets = all.Where(d => d.IsAssetColumn).Sum(d => d.Amount),
                Liabilities = all.Where(d => !d.IsAssetColumn && d.Category.Type == 2).Sum(d => Math.Abs(d.Amount)),
                Equity = all.Where(d => !d.IsAssetColumn && d.Category.Type != 2).Sum(d => d.Amount)
            };
        }

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
            detail.CalculationTypeName = L.T("Fin_CalcDocumentBased");
            detail.Explanation = apOpening.HasValue
                ? L.T("Fin_Exp_ApOpening", apOpening.Value.ToString("dd.MM.yyyy"))
                : L.T("Fin_Exp_Ap");
        }

        private void FillReceivables(BalanceCategoryDetailData detail, DateTime asOfDate)
        {
            var arOpening = _counterparties.LatestOpeningDate(asOfDate, CounterpartyBalanceService.BalanceTypeReceivable);
            var summary = _counterparties.Customers(asOfDate, arOpening);
            detail.Amount = summary.Sum(c => c.Outstanding);
            detail.TransactionCount = summary.Sum(c => c.InvoiceCount + c.ReceiptCount);
            detail.AddedAmount = summary.Sum(c => c.InvoiceTotal);
            detail.SubtractedAmount = summary.Sum(c => c.ReceiptTotal);
            detail.CalculationTypeName = L.T("Fin_CalcDocumentBased");
            detail.Explanation = arOpening.HasValue
                ? L.T("Fin_Exp_ArOpening", arOpening.Value.ToString("dd.MM.yyyy"))
                : L.T("Fin_Exp_Ar");
        }

        private sealed class TxnStats
        {
            public int Count { get; set; }
            public decimal Added { get; set; }
            public decimal Subtracted { get; set; }
            public decimal Net { get; set; }
        }

        /// <summary>Soni, kirim, chiqim va sof summa — so'mda (FIN-B01: kun kursi), bitta so'rov bilan.</summary>
        private TxnStats PopulateTransactionStats(IQueryable<FIN_TRANSACTIONS> query, BalanceCategoryDetailData detail)
        {
            var buckets = query.UzsBuckets(_db);
            var stats = new TxnStats
            {
                Count = buckets.Sum(b => b.Count),
                Added = buckets.Where(b => b.Direction == 1).Sum(b => b.Uzs),
                Subtracted = buckets.Where(b => b.Direction != 1).Sum(b => b.Uzs),
                Net = buckets.Sum(b => b.SignedUzs)
            };

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
                return new { success = false, message = L.T("Fin_CategoryNotFound") };
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
            // Popup qatorlari ham so'mda (FIN-B01): har summa o'z sanasidagi kurs bilan.
            var money = FinMoney.For(db);

            if (Is(category, SystemCodes.BalanceCategory.Payables))
            {
                // Popup'da invoys/to'lovlar to'liq ko'rsatiladi (opening'dan keyingisi emas, asOfDate gacha).
                var addedRaw = db.P_INVOICES.AsNoTracking()
                    .Where(i => i.InvoiceDate <= asOfDate && (i.IsDeleted == null || i.IsDeleted == false))
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(top)
                    .Select(i => new { i.ID, i.InvoiceDate, i.InvoiceNo, i.Amount, SupplierName = i.SUPPLIER != null ? i.SUPPLIER.Name : "" })
                    .ToList();

                // So'mda (FIN-B01): kirim hujjatlari yig'indisi, kirimi yo'q invoys — invoys summasi, har biri o'z sanasidagi kurs bilan.
                var invoiceIds = addedRaw.Select(i => i.ID).ToList();
                var amountsUzs = CounterpartyBalanceService.PurchaseInvoiceAmountsUzs(db, db.P_INVOICES.Where(i => invoiceIds.Contains(i.ID)));

                var added = addedRaw
                    .Select(i => new
                    {
                        date = i.InvoiceDate,
                        doc = i.InvoiceNo,
                        amount = amountsUzs.TryGetValue(i.ID, out var amountUzs) ? amountUzs : 0m,
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
                    .Select(t => new { t.TxnDate, t.SourceDocType, t.Amount, t.CurrencyID, t.ContractNo, t.Purpose, t.Comment })
                    .ToList()
                    .Select(t => new
                    {
                        date = t.TxnDate,
                        doc = t.SourceDocType ?? "FinTransaction",
                        amount = money.Uzs(t.Amount, t.CurrencyID, t.TxnDate),
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
                        i.CurrencyID,
                        note = (i.CUSTOMER != null ? i.CUSTOMER.Name : "")
                    })
                    .ToList()
                    .Select(i => new { i.date, i.doc, amount = money.Uzs(i.amount, i.CurrencyID, i.date), i.note })
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
                    .UzsMovementRows(money);

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
                    .UzsMovementRows(money);

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
                .UzsMovementRows(money);
            var defaultSubtracted = baseQuery
                .Where(t => t.Direction != 1)
                .OrderByDescending(t => t.TxnDate)
                .Take(top)
                .UzsMovementRows(money);

            return new { success = true, added = defaultAdded, subtracted = defaultSubtracted };
        }
    }

    /// <summary>Balans popup qatori (date, doc, amount, note) — summa so'mda.</summary>
    public sealed class BalanceMovementRow
    {
        public DateTime date { get; set; }
        public string doc { get; set; }
        public decimal amount { get; set; }
        public string note { get; set; }
    }

    internal static class BalanceMovementQueries
    {
        /// <summary>FIN harakatlarini popup qatorlariga aylantiradi, summani tranzaksiya sanasidagi kurs bilan so'mga o'girib (FIN-B01).</summary>
        public static List<BalanceMovementRow> UzsMovementRows(this IQueryable<FIN_TRANSACTIONS> query, FinMoney money)
        {
            return query
                .Select(t => new { t.TxnDate, t.SourceDocType, t.Amount, t.CurrencyID, t.Purpose, t.Comment })
                .ToList()
                .Select(t => new BalanceMovementRow
                {
                    date = t.TxnDate,
                    doc = t.SourceDocType ?? "FinTransaction",
                    amount = money.Uzs(t.Amount, t.CurrencyID, t.TxnDate),
                    note = t.Purpose ?? t.Comment
                })
                .ToList();
        }
    }
}
