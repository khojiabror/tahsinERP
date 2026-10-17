using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.ViewModels.Finance;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Opening balans: sahifa modeli, saqlash, joriy qoldiqlardan avto-to'ldirish va o'chirish
    /// (FIN-M02: OpeningBalanceController'dan ajratildi). Saqlash/AutoFill/o'chirish chaqiruvchining
    /// DB tranzaksiyasi ichida bajariladi.
    /// </summary>
    public sealed class OpeningBalanceService
    {
        public const byte TypeReceivable = 1;
        public const byte TypePayable = 2;
        public const byte TypePayroll = 3;
        public const byte TypeForwarder = 4;

        /// <summary>Hisob kesimidagi opening snapshot'i: SourceID = FIN_ACCOUNTS.ID (FIN-B16).</summary>
        public const string AccountOpeningSourceType = "OpeningBalanceAccount";

        private readonly DBTHSNEntities _db;

        public OpeningBalanceService(DBTHSNEntities db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        private sealed class ContractRow
        {
            public DateTime SnapshotDate { get; set; }
            public byte BalanceType { get; set; }
            public int CounterpartyID { get; set; }
            public int? ContractID { get; set; }
            public string ContractNo { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal PaidAmount { get; set; }
            public decimal Amount { get; set; }
            public int CurrencyID { get; set; }
            public int? CreatedByUserID { get; set; }
        }

        /// <summary>Sahifadagi standart valyuta: ID = 5 bo'lsa shu, aks holda birinchisi.</summary>
        public int DefaultCurrencyId()
        {
            var id = _db.CURRENCIES.Any(c => c.ID == 5) ? 5 : _db.CURRENCIES.Select(c => c.ID).FirstOrDefault();
            return id > 0 ? id : 5;
        }

        #region Ro'yxat va o'chirish

        /// <summary>Opening sanalari va har biridagi qatorlar soni (kategoriya snapshot'lari + kontragent qatorlari).</summary>
        public List<OpeningBalanceDateRow> Dates()
        {
            var snapshotDates = _db.FIN_BALANCE_SNAPSHOTS.AsNoTracking()
                .Where(s => s.SourceType == "OpeningBalance" || s.SourceType == null)
                .Select(s => s.SnapshotDate)
                .ToList();
            var contractDates = _db.FIN_OPENING_CONTRACT_BALANCES.AsNoTracking()
                .Select(x => x.SnapshotDate)
                .ToList();

            return snapshotDates.Concat(contractDates)
                .GroupBy(d => d.Date)
                .Select(g => new OpeningBalanceDateRow { SnapshotDate = g.Key, LineCount = g.Count() })
                .OrderByDescending(r => r.SnapshotDate)
                .ToList();
        }

        public int LineCount(DateTime date)
        {
            var d = date.Date;
            var snapshots = _db.FIN_BALANCE_SNAPSHOTS.Count(s =>
                DbFunctions.TruncateTime(s.SnapshotDate) == d &&
                (s.SourceType == "OpeningBalance" || s.SourceType == null));
            var contracts = _db.FIN_OPENING_CONTRACT_BALANCES.Count(x => x.SnapshotDate == d);
            return snapshots + contracts;
        }

        /// <summary>Shu sanadagi barcha opening yozuvlari: kontragent qatorlari va OpeningBalance* snapshot'lari.</summary>
        public void DeleteByDate(DateTime date)
        {
            var d = date.Date;
            _db.Database.ExecuteSqlCommand(
                "DELETE FROM FIN_OPENING_CONTRACT_BALANCES WHERE SnapshotDate = @p0",
                new SqlParameter("@p0", d));

            _db.Database.ExecuteSqlCommand(
                "DELETE FROM FIN_BALANCE_SNAPSHOTS WHERE CONVERT(date, SnapshotDate) = @p0 AND (SourceType = 'OpeningBalance' OR SourceType IS NULL OR SourceType LIKE 'OpeningBalance%')",
                new SqlParameter("@p0", d));
        }

        private void AddContractRows(IEnumerable<ContractRow> rows)
        {
            var now = DateTime.Now;
            foreach (var row in rows)
            {
                _db.FIN_OPENING_CONTRACT_BALANCES.Add(new FIN_OPENING_CONTRACT_BALANCES
                {
                    SnapshotDate = row.SnapshotDate.Date,
                    BalanceType = row.BalanceType,
                    CounterpartyID = row.CounterpartyID,
                    ContractID = row.ContractID,
                    ContractNo = row.ContractNo,
                    TotalAmount = row.TotalAmount,
                    PaidAmount = row.PaidAmount,
                    Amount = row.Amount,
                    CurrencyID = row.CurrencyID,
                    CreatedByUserID = row.CreatedByUserID,
                    CreatedDT = now
                });
            }
        }

        #endregion

        #region Sahifa modeli

        public OpeningBalanceViewModel BuildViewModel(DateTime date)
        {
            var db = _db;
            // FIN-D11: subledger nomi testi balans bilan bitta funksiyada
            var categories = db.FIN_BALANCE_CATEGORIES
                .Where(c => c.IsActive)
                .OrderBy(c => c.Type)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList()
                .Where(c => !BalanceSheetService.IsDetailedSubledgerCategoryName(c.Name))
                .ToList();

            var snapshots = db.FIN_BALANCE_SNAPSHOTS
                .Where(s => DbFunctions.TruncateTime(s.SnapshotDate) == DbFunctions.TruncateTime(date) &&
                            (s.SourceType == "OpeningBalance" || s.SourceType == null))
                .ToList()
                .GroupBy(s => s.CategoryID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedDT).FirstOrDefault());

            // Bu sahifada standart valyuta ID = 5 (mavjud bo'lmasa, birinchi valyuta).
            var defaultCurrency = db.CURRENCIES.FirstOrDefault(c => c.ID == 5) ?? db.CURRENCIES.FirstOrDefault();
            var currencies = db.CURRENCIES.OrderBy(c => c.Shortname).ToList();

            var lookup = new FinCounterpartyLookupService(db);
            Func<byte, List<SelectListItem>> counterpartyList = type => lookup.Counterparties(type)
                .Select(x => new SelectListItem { Value = x.ID.ToString(), Text = x.Name })
                .ToList();

            var model = new OpeningBalanceViewModel
            {
                SnapshotDate = date,
                DefaultCurrencyID = defaultCurrency?.ID ?? 5,
                CurrencyList = currencies.Select(c => new SelectListItem
                {
                    Value = c.ID.ToString(),
                    Text = c.Shortname
                }),
                CustomerList = counterpartyList(FinCounterpartyLookupService.Customer),
                SupplierList = counterpartyList(FinCounterpartyLookupService.Supplier),
                // Xodimlar: tabel raqami bilan, nofaollar ham (eski opening qatorlari uchun)
                EmployeeList = db.HR_EMPLOYEES
                    .Where(e => e.IsDeleted != true)
                    .OrderBy(e => e.FullName)
                    .Select(e => new SelectListItem
                    {
                        Value = e.ID.ToString(),
                        Text = (e.TabNo ?? "-") + " - " + (e.FullName ?? "")
                    })
                    .ToList(),
                ForwarderList = counterpartyList(FinCounterpartyLookupService.Forwarder)
            };

            foreach (var cat in categories)
            {
                snapshots.TryGetValue(cat.ID, out var snap);
                model.Items.Add(new OpeningBalanceCategoryItem
                {
                    CategoryID = cat.ID,
                    CategoryName = cat.Name,
                    Type = cat.Type,
                    TypeName = BalanceCategoryDisplayHelper.GetSideLabel(cat),
                    Amount = snap?.Amount ?? 0,
                    CurrencyID = snap?.CurrencyID ?? (defaultCurrency?.ID ?? 5),
                    CurrencyShortname = snap != null
                        ? currencies.FirstOrDefault(c => c.ID == snap.CurrencyID)?.Shortname ?? defaultCurrency?.Shortname
                        : defaultCurrency?.Shortname,
                    // DBda snapshot yo'q = bo'sh; yozuv bor (0 ham) = aniq qiymat
                    LeaveEmpty = snap == null
                });
            }

            // FIN-B16: faol hisoblar (kategoriyasi shu sahifadagi qo'lda kiritiladigan kategoriyalardan) — hisob kesimidagi opening
            var categoryNames = categories.ToDictionary(c => c.ID, c => c.Name);
            var accountSnapshots = AccountOpenings(date);
            model.AccountItems = db.FIN_ACCOUNTS.AsNoTracking()
                .Where(a => a.IsActive && a.BalanceCategoryID.HasValue)
                .Select(a => new { a.ID, a.Name, a.Code, CategoryID = a.BalanceCategoryID.Value, Currency = a.CURRENCy.Shortname })
                .ToList()
                .Where(a => categoryNames.ContainsKey(a.CategoryID))
                .OrderBy(a => categories.FindIndex(c => c.ID == a.CategoryID))
                .ThenBy(a => a.Name)
                .Select(a => new OpeningBalanceAccountItem
                {
                    AccountID = a.ID,
                    AccountName = string.IsNullOrEmpty(a.Code) ? a.Name : a.Code + " — " + a.Name,
                    CategoryID = a.CategoryID,
                    CategoryName = categoryNames[a.CategoryID],
                    CurrencyShortname = a.Currency,
                    Amount = accountSnapshots.TryGetValue(a.ID, out var amount) ? amount : (decimal?)null
                })
                .ToList();

            var d = date.Date;
            var rows = db.FIN_OPENING_CONTRACT_BALANCES.AsNoTracking()
                .Where(x => x.SnapshotDate == d)
                .ToList();

            var names = new Dictionary<byte, Dictionary<int, string>>
            {
                [TypeReceivable] = model.CustomerList.ToDictionary(x => int.Parse(x.Value), x => x.Text ?? "Noma'lum"),
                [TypePayable] = model.SupplierList.ToDictionary(x => int.Parse(x.Value), x => x.Text ?? "Noma'lum"),
                [TypePayroll] = model.EmployeeList.ToDictionary(x => int.Parse(x.Value), x => x.Text),
                [TypeForwarder] = model.ForwarderList.ToDictionary(x => int.Parse(x.Value), x => x.Text ?? "Noma'lum")
            };
            var contractNos = ContractNumbers(rows.Where(x => x.ContractID.HasValue).ToList());

            List<OpeningBalanceContractLineItem> ContractLines(byte type, string fallbackName)
            {
                return rows.Where(x => x.BalanceType == type)
                    .Select(x => new OpeningBalanceContractLineItem
                    {
                        CounterpartyID = x.CounterpartyID,
                        CounterpartyName = names[type].TryGetValue(x.CounterpartyID, out var n) ? n : $"{fallbackName} #{x.CounterpartyID}",
                        ContractID = x.ContractID,
                        ContractNo = x.ContractID.HasValue && contractNos[type].TryGetValue(x.ContractID.Value, out var no)
                            ? no
                            : (x.ContractNo ?? ""),
                        TotalAmount = x.TotalAmount,
                        PaidAmount = x.PaidAmount,
                        Amount = x.Amount,
                        CurrencyID = x.CurrencyID > 0 ? x.CurrencyID : model.DefaultCurrencyID
                    })
                    .ToList();
            }

            model.CustomerReceivableLines = ContractLines(TypeReceivable, "Customer");
            model.SupplierPayableLines = ContractLines(TypePayable, "Supplier");
            model.ForwarderPayableLines = ContractLines(TypeForwarder, "Ekspeditor");
            model.EmployeePayrollLines = rows.Where(x => x.BalanceType == TypePayroll)
                .Select(x => new OpeningBalancePayrollLineItem
                {
                    EmployeeID = x.CounterpartyID,
                    EmployeeName = names[TypePayroll].TryGetValue(x.CounterpartyID, out var n) ? n : $"Xodim #{x.CounterpartyID}",
                    TotalAmount = x.TotalAmount,
                    PaidAmount = x.PaidAmount,
                    Amount = x.Amount,
                    CurrencyID = x.CurrencyID > 0 ? x.CurrencyID : model.DefaultCurrencyID
                })
                .ToList();

            return model;
        }

        /// <summary>Shu sanadagi hisob kesimidagi opening'lar: AccountID → summa.</summary>
        private Dictionary<int, decimal> AccountOpenings(DateTime date)
        {
            var d = date.Date;
            return _db.FIN_BALANCE_SNAPSHOTS.AsNoTracking()
                .Where(s => s.SourceType == AccountOpeningSourceType && s.SourceID.HasValue &&
                            DbFunctions.TruncateTime(s.SnapshotDate) == d)
                .Select(s => new { AccountID = s.SourceID.Value, s.Amount, s.ID })
                .ToList()
                .GroupBy(s => s.AccountID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ID).First().Amount);
        }

        /// <summary>
        /// Opening qatorlaridagi shartnoma raqamlari (tur → ContractID → raqam), faqat ishlatilganlari
        /// (avval S/P/F_CONTRACTS jadvallari butunlay yuklanardi).
        /// </summary>
        private Dictionary<byte, Dictionary<int, string>> ContractNumbers(List<FIN_OPENING_CONTRACT_BALANCES> rows)
        {
            List<int> Ids(byte type) => rows.Where(r => r.BalanceType == type).Select(r => r.ContractID.Value).Distinct().ToList();
            var sIds = Ids(TypeReceivable);
            var pIds = Ids(TypePayable);
            var fIds = Ids(TypeForwarder);
            return new Dictionary<byte, Dictionary<int, string>>
            {
                [TypeReceivable] = sIds.Count == 0 ? new Dictionary<int, string>() : _db.S_CONTRACTS.AsNoTracking()
                    .Where(c => c.IsDeleted != true && sIds.Contains(c.ID)).Select(c => new { c.ID, c.ContractNo }).ToList()
                    .ToDictionary(c => c.ID, c => c.ContractNo ?? ""),
                [TypePayable] = pIds.Count == 0 ? new Dictionary<int, string>() : _db.P_CONTRACTS.AsNoTracking()
                    .Where(c => c.IsDeleted != true && pIds.Contains(c.ID)).Select(c => new { c.ID, c.ContractNo }).ToList()
                    .ToDictionary(c => c.ID, c => c.ContractNo ?? ""),
                [TypeForwarder] = fIds.Count == 0 ? new Dictionary<int, string>() : _db.F_CONTRACTS.AsNoTracking()
                    .Where(c => c.IsDeleted != true && fIds.Contains(c.ID)).Select(c => new { c.ID, c.ContractNo }).ToList()
                    .ToDictionary(c => c.ID, c => c.ContractNo ?? "")
            };
        }

        #endregion

        #region Saqlash

        public static decimal ParseDecimalFlexible(string raw, decimal fallback = 0m)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            var s = raw.Trim().Replace(" ", "");
            decimal v;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out v)) return v;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
            s = s.Replace(",", ".");
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        /// <summary>
        /// Shu sanadagi opening'ni qayta yozadi: avval eskisi o'chiriladi (AVVAL — aks holda endigina yozilgan
        /// kategoriya qatorlari o'chib ketardi), so'ng kategoriya snapshot'lari va kontragent qatorlari.
        /// <paramref name="rawForm"/> — summa maydonlarining xom qiymati (lokal formatdagi vergul/bo'shliqlar uchun).
        /// </summary>
        public void Save(OpeningBalanceViewModel model, Func<string, string> rawForm, int userId)
        {
            var db = _db;
            var date = model.SnapshotDate.Date;
            DeleteByDate(date);

            var currencyIds = new HashSet<int>(db.CURRENCIES.Select(c => c.ID).ToList());
            // model.DefaultCurrencyID POST'da 0 bo'lib kelishi mumkin — bazadagi haqiqiy valyutaga tayanamiz (FK).
            int resolvedDefaultCurrencyId = currencyIds.Contains(5)
                ? 5
                : (currencyIds.Contains(model.DefaultCurrencyID) ? model.DefaultCurrencyID : currencyIds.FirstOrDefault());
            Func<int, int> clampCurrency = cid => (cid > 0 && currencyIds.Contains(cid)) ? cid : resolvedDefaultCurrencyId;

            // FIN-B16: hisob kesimidagi opening; kiritilgan hisoblar kategoriyasining opening'i ularning yig'indisi
            var postedAccountIds = (model.AccountItems ?? new List<OpeningBalanceAccountItem>())
                .Where(a => a != null && a.Amount.HasValue)
                .Select(a => a.AccountID)
                .Distinct()
                .ToList();
            var accounts = postedAccountIds.Count == 0
                ? new Dictionary<int, FIN_ACCOUNTS>()
                : db.FIN_ACCOUNTS.Where(a => postedAccountIds.Contains(a.ID) && a.BalanceCategoryID.HasValue).ToDictionary(a => a.ID);
            var accountSumByCategory = new Dictionary<int, decimal>();
            foreach (var a in model.AccountItems ?? new List<OpeningBalanceAccountItem>())
            {
                if (a == null || !a.Amount.HasValue || !accounts.TryGetValue(a.AccountID, out var account)) continue;
                var categoryId = account.BalanceCategoryID.Value;
                db.FIN_BALANCE_SNAPSHOTS.Add(new FIN_BALANCE_SNAPSHOTS
                {
                    SnapshotDate = date,
                    CategoryID = categoryId,
                    Amount = a.Amount.Value,
                    CurrencyID = clampCurrency(account.CurrencyID),
                    SourceType = AccountOpeningSourceType,
                    SourceID = account.ID,
                    CreatedByUserID = userId,
                    CreatedDT = DateTime.Now
                });
                accountSumByCategory[categoryId] = (accountSumByCategory.TryGetValue(categoryId, out var s) ? s : 0m) + a.Amount.Value;
            }

            foreach (var item in model.Items ?? new List<OpeningBalanceCategoryItem>())
            {
                if (item == null) continue;
                var hasAccounts = accountSumByCategory.TryGetValue(item.CategoryID, out var accountSum);
                // 0: "Bo'sh qoldirish" belgilangan bo'lsa — qator yozilmaydi; belgilanmagan bo'lsa — aniq 0 saqlanadi.
                if (!hasAccounts && item.Amount == 0 && item.LeaveEmpty) continue;

                db.FIN_BALANCE_SNAPSHOTS.Add(new FIN_BALANCE_SNAPSHOTS
                {
                    SnapshotDate = date,
                    CategoryID = item.CategoryID,
                    Amount = hasAccounts ? accountSum : item.Amount,
                    CurrencyID = clampCurrency(item.CurrencyID),
                    SourceType = "OpeningBalance",
                    SourceID = null,
                    CreatedByUserID = userId,
                    CreatedDT = DateTime.Now
                });
            }

            var lines = new List<KeyValuePair<byte, OpeningBalanceContractLineItem>>();
            var rows = new List<ContractRow>();

            void AddLines(byte type, string prefix, IList<OpeningBalanceContractLineItem> items, bool leaveEmpty)
            {
                if (leaveEmpty || items == null) return;
                for (int i = 0; i < items.Count; i++)
                {
                    var x = items[i];
                    if (x == null || x.CounterpartyID <= 0) continue;
                    var total = ParseDecimalFlexible(rawForm(prefix + "[" + i + "].TotalAmount"), x.TotalAmount);
                    var paid = ParseDecimalFlexible(rawForm(prefix + "[" + i + "].PaidAmount"), x.PaidAmount);
                    if (total == 0 && paid == 0) continue;
                    if (paid > total) paid = total;
                    rows.Add(new ContractRow
                    {
                        SnapshotDate = date,
                        BalanceType = type,
                        CounterpartyID = x.CounterpartyID,
                        ContractID = x.ContractID,
                        ContractNo = x.ContractNo ?? "",
                        TotalAmount = total,
                        PaidAmount = paid,
                        Amount = total - paid,
                        CurrencyID = clampCurrency(x.CurrencyID),
                        CreatedByUserID = userId
                    });
                }
            }

            AddLines(TypeReceivable, "CustomerReceivableLines", model.CustomerReceivableLines, model.LeaveCustomerReceivableEmpty);
            AddLines(TypePayable, "SupplierPayableLines", model.SupplierPayableLines, model.LeaveSupplierPayableEmpty);
            AddLines(TypeForwarder, "ForwarderPayableLines", model.ForwarderPayableLines, model.LeaveForwarderPayableEmpty);

            if (!model.LeavePayrollEmpty && model.EmployeePayrollLines != null)
            {
                for (int i = 0; i < model.EmployeePayrollLines.Count; i++)
                {
                    var x = model.EmployeePayrollLines[i];
                    if (x == null || x.EmployeeID <= 0) continue;
                    var total = ParseDecimalFlexible(rawForm("EmployeePayrollLines[" + i + "].TotalAmount"), x.TotalAmount);
                    var paid = ParseDecimalFlexible(rawForm("EmployeePayrollLines[" + i + "].PaidAmount"), x.PaidAmount);
                    if (total == 0 && paid == 0) continue;
                    if (paid > total) paid = total;
                    rows.Add(new ContractRow
                    {
                        SnapshotDate = date,
                        BalanceType = TypePayroll,
                        CounterpartyID = x.EmployeeID,
                        TotalAmount = total,
                        PaidAmount = paid,
                        Amount = total - paid,
                        CurrencyID = clampCurrency(x.CurrencyID),
                        CreatedByUserID = userId
                    });
                }
            }

            // Shartnoma raqami — formadagi matn emas, shartnoma jadvalidagisi (bo'lsa)
            var contractNos = ContractNumbers(rows
                .Where(r => r.ContractID.HasValue)
                .Select(r => new FIN_OPENING_CONTRACT_BALANCES { BalanceType = r.BalanceType, ContractID = r.ContractID })
                .ToList());
            foreach (var r in rows.Where(r => r.ContractID.HasValue && contractNos.ContainsKey(r.BalanceType)))
            {
                if (contractNos[r.BalanceType].TryGetValue(r.ContractID.Value, out var no))
                    r.ContractNo = no;
            }

            AddContractRows(rows);
            db.SaveChanges();
        }

        #endregion

        #region Avto-to'ldirish

        /// <summary>
        /// Shu sana holatidagi joriy qoldiqlardan kontragent opening qatorlarini tuzadi (eski opening o'chiriladi):
        /// AR/AP — shartnoma kesimida (invoyslar − to'lovlar), oylik — xodim kesimida, ekspeditor — yo'l
        /// varaqalari − to'lovlar. Faqat musbat qoldiqlar yoziladi.
        /// </summary>
        public void AutoFill(DateTime snapshotDate, int userId)
        {
            var db = _db;
            var date = snapshotDate.Date;
            var defaultCurrencyId = DefaultCurrencyId();

            DeleteByDate(date);

            Func<string, string> normalize = s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpper();

            // 1) Customer receivables by contract
            var sInvoices = db.SALES_INVOICES
                .Where(i => i.InvoiceDate <= date)
                .Select(i => new { ID = (int)i.ID, i.CustomerID, i.ContractID, Amount = i.TotalWithVAT ?? i.TotalAmount })
                .ToList();
            var sContractIds = sInvoices.Where(i => i.ContractID.HasValue).Select(i => i.ContractID.Value).Distinct().ToList();
            var sContractNoById = sContractIds.Count == 0
                ? new Dictionary<int, string>()
                : db.S_CONTRACTS.Where(c => sContractIds.Contains(c.ID))
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToList()
                    .ToDictionary(x => x.ID, x => normalize(x.ContractNo));

            var cPayments = db.FIN_TRANSACTIONS
                .Where(t => t.TxnDate <= date &&
                            (t.IsDeleted == null || t.IsDeleted == false) &&
                            t.SourceDocType != "SalesInvoice" &&
                            ((t.Direction == 1 && t.CounterpartyType == 1 && t.CounterpartyID.HasValue) ||
                             (t.SourceDocType == "SalesInvoicePayment" && t.SourceDocID.HasValue)))
                .Select(t => new { t.CounterpartyID, t.Amount, t.SourceDocType, t.SourceDocID, t.ContractNo })
                .ToList();

            var cLinkedIds = cPayments.Where(p => p.SourceDocType == "SalesInvoicePayment" && p.SourceDocID.HasValue).Select(p => (long)p.SourceDocID.Value).Distinct().ToList();
            var cInvoiceMeta = cLinkedIds.Count == 0
                ? new Dictionary<long, Tuple<int, string, int?>>()
                : db.SALES_INVOICES.Where(i => cLinkedIds.Contains(i.ID))
                    .Select(i => new { i.ID, i.CustomerID, i.ContractID })
                    .ToList()
                    .ToDictionary(
                        x => x.ID,
                        x => Tuple.Create(x.CustomerID, x.ContractID.HasValue && sContractNoById.ContainsKey(x.ContractID.Value) ? sContractNoById[x.ContractID.Value] : "", x.ContractID));

            var cInvoiceTotalsByKey = sInvoices
                .GroupBy(i => Tuple.Create(i.CustomerID, i.ContractID.HasValue && sContractNoById.ContainsKey(i.ContractID.Value) ? sContractNoById[i.ContractID.Value] : ""))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var cContractIdByKey = sInvoices
                .Where(i => i.ContractID.HasValue)
                .GroupBy(i => Tuple.Create(i.CustomerID, sContractNoById.ContainsKey(i.ContractID.Value) ? sContractNoById[i.ContractID.Value] : ""))
                .ToDictionary(g => g.Key, g => g.Select(x => x.ContractID).FirstOrDefault());

            var cPaymentTotalsByKey = cPayments
                .Select(p =>
                {
                    int customerId = p.CounterpartyID ?? 0;
                    var contractNo = normalize(p.ContractNo);
                    if (p.SourceDocType == "SalesInvoicePayment" && p.SourceDocID.HasValue && cInvoiceMeta.ContainsKey((long)p.SourceDocID.Value))
                    {
                        var meta = cInvoiceMeta[(long)p.SourceDocID.Value];
                        customerId = meta.Item1;
                        if (string.IsNullOrWhiteSpace(contractNo)) contractNo = meta.Item2;
                    }
                    if (customerId <= 0) return null;
                    return new { CustomerID = customerId, ContractNo = contractNo, Amount = Math.Abs(p.Amount) };
                })
                .Where(x => x != null)
                .GroupBy(x => Tuple.Create(x.CustomerID, x.ContractNo))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            // 2) Supplier payables by contract.
            // Xarid summasi — dashboard/balans/Akt-sverki bilan bir xil qoida (FIN-B08):
            // invoysga bog'langan kirimlar (INCOMES.TotalPrice) bo'lsa shu, aks holda P_INVOICES.Amount.
            var pInvoiceQuery = db.P_INVOICES
                .Where(i => i.InvoiceDate <= date && (i.IsDeleted == null || i.IsDeleted == false));
            var pAmountById = CounterpartyBalanceService.PurchaseInvoiceAmounts(db, pInvoiceQuery, date);
            var pInvoices = pInvoiceQuery
                .Select(i => new { i.ID, i.SupplierID, i.ContractID })
                .ToList()
                .Select(i => new { i.ID, i.SupplierID, i.ContractID, Amount = pAmountById.TryGetValue(i.ID, out var a) ? a : 0m })
                .ToList();
            var pContractIds = pInvoices.Where(i => i.ContractID > 0).Select(i => i.ContractID).Distinct().ToList();
            var pContractNoById = pContractIds.Count == 0
                ? new Dictionary<int, string>()
                : db.P_CONTRACTS.Where(c => pContractIds.Contains(c.ID))
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToList()
                    .ToDictionary(x => x.ID, x => normalize(x.ContractNo));

            var sPayments = db.FIN_TRANSACTIONS
                .Where(t => t.TxnDate <= date &&
                            (t.IsDeleted == null || t.IsDeleted == false) &&
                            (t.SourceDocType == null || (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice")) &&
                            ((t.Direction == 2 && t.CounterpartyType == 2 && t.CounterpartyID.HasValue) ||
                             (t.SourceDocType == "PurchaseInvoicePayment" && t.SourceDocID.HasValue)))
                .Select(t => new { t.CounterpartyID, t.Amount, t.SourceDocType, t.SourceDocID, t.ContractNo })
                .ToList();

            var sLinkedIds = sPayments.Where(p => p.SourceDocType == "PurchaseInvoicePayment" && p.SourceDocID.HasValue).Select(p => p.SourceDocID.Value).Distinct().ToList();
            var pInvoiceMeta = sLinkedIds.Count == 0
                ? new Dictionary<int, Tuple<int, string>>()
                : db.P_INVOICES.Where(i => sLinkedIds.Contains(i.ID))
                    .Select(i => new { i.ID, i.SupplierID, i.ContractID })
                    .ToList()
                    .ToDictionary(x => x.ID, x => Tuple.Create(x.SupplierID, x.ContractID > 0 && pContractNoById.ContainsKey(x.ContractID) ? pContractNoById[x.ContractID] : ""));

            var pInvoiceTotalsByKey = pInvoices
                .GroupBy(i => Tuple.Create(i.SupplierID, i.ContractID > 0 && pContractNoById.ContainsKey(i.ContractID) ? pContractNoById[i.ContractID] : ""))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var pContractIdByKey = pInvoices
                .Where(i => i.ContractID > 0)
                .GroupBy(i => Tuple.Create(i.SupplierID, pContractNoById.ContainsKey(i.ContractID) ? pContractNoById[i.ContractID] : ""))
                .ToDictionary(g => g.Key, g => g.Select(x => (int?)x.ContractID).FirstOrDefault());

            var pPaymentTotalsByKey = sPayments
                .Select(p =>
                {
                    int supplierId = p.CounterpartyID ?? 0;
                    var contractNo = normalize(p.ContractNo);
                    if (p.SourceDocType == "PurchaseInvoicePayment" && p.SourceDocID.HasValue && pInvoiceMeta.ContainsKey(p.SourceDocID.Value))
                    {
                        var meta = pInvoiceMeta[p.SourceDocID.Value];
                        supplierId = meta.Item1;
                        if (string.IsNullOrWhiteSpace(contractNo)) contractNo = meta.Item2;
                    }
                    if (supplierId <= 0) return null;
                    return new { SupplierID = supplierId, ContractNo = contractNo, Amount = Math.Abs(p.Amount) };
                })
                .Where(x => x != null)
                .GroupBy(x => Tuple.Create(x.SupplierID, x.ContractNo))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            // 3) Payroll by employee
            var payrollCalculatedByEmployee = FinQueries.ApprovedPayrollLines(db)
                .Where(l => l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= date)
                .GroupBy(l => l.EmployeeID)
                .Select(g => new { EmployeeID = g.Key, Total = g.Sum(x => (decimal?)x.NetSalary) ?? 0m })
                .ToList()
                .ToDictionary(x => x.EmployeeID, x => x.Total);
            var payrollPaidByEmployee = FinQueries.Active(db)
                .Where(t => t.Direction == 2 &&
                            t.CounterpartyType == 6 &&
                            t.CounterpartyID.HasValue &&
                            t.TxnDate <= date)
                .GroupBy(t => t.CounterpartyID.Value)
                .Select(g => new { EmployeeID = g.Key, Paid = g.Sum(x => x.Amount) })
                .ToList()
                .ToDictionary(x => x.EmployeeID, x => x.Paid);

            var rows = new List<ContractRow>();

            void AddPositive(byte type, int counterpartyId, int? contractId, string contractNo, decimal total, decimal paid)
            {
                var net = total - paid;
                if (net <= 0) return;
                rows.Add(new ContractRow
                {
                    SnapshotDate = date,
                    BalanceType = type,
                    CounterpartyID = counterpartyId,
                    ContractID = contractId,
                    ContractNo = contractNo,
                    TotalAmount = total,
                    PaidAmount = Math.Min(paid, total),
                    Amount = net,
                    CurrencyID = defaultCurrencyId,
                    CreatedByUserID = userId
                });
            }

            foreach (var key in cInvoiceTotalsByKey.Keys.Union(cPaymentTotalsByKey.Keys).Distinct())
            {
                AddPositive(TypeReceivable, key.Item1, cContractIdByKey.TryGetValue(key, out var cid) ? cid : null, key.Item2,
                    cInvoiceTotalsByKey.TryGetValue(key, out var t) ? t : 0m,
                    cPaymentTotalsByKey.TryGetValue(key, out var p) ? p : 0m);
            }

            foreach (var key in pInvoiceTotalsByKey.Keys.Union(pPaymentTotalsByKey.Keys).Distinct())
            {
                AddPositive(TypePayable, key.Item1, pContractIdByKey.TryGetValue(key, out var cid) ? cid : null, key.Item2,
                    pInvoiceTotalsByKey.TryGetValue(key, out var t) ? t : 0m,
                    pPaymentTotalsByKey.TryGetValue(key, out var p) ? p : 0m);
            }

            foreach (var employeeId in payrollCalculatedByEmployee.Keys.Union(payrollPaidByEmployee.Keys).Distinct())
            {
                AddPositive(TypePayroll, employeeId, null, null,
                    payrollCalculatedByEmployee.TryGetValue(employeeId, out var t) ? t : 0m,
                    payrollPaidByEmployee.TryGetValue(employeeId, out var p) ? p : 0m);
            }

            // 4) Logistikadan qarzimiz — ekspeditor shartnomasi kesimida (yo'l varaqalari − to'lovlar)
            var fContractInfo = db.F_CONTRACTS
                .Where(c => c.IsDeleted != true)
                .Select(c => new { c.ID, c.ForwarderID, c.ContractNo })
                .ToList()
                .ToDictionary(c => c.ID);
            // double ustunlar (Amount/Payment_Amount) xotirada decimal'ga yig'iladi
            var waybillTotalsByContract = db.F_WAYBILLS
                .Where(w => w.ContractID.HasValue && w.IsDeleted != true &&
                            w.Amount.HasValue && w.IssueDate.HasValue && w.IssueDate.Value <= date)
                .Select(w => new { ContractID = w.ContractID.Value, w.Amount })
                .ToList()
                .GroupBy(w => w.ContractID)
                .ToDictionary(g => g.Key, g => g.Sum(x => (decimal)(x.Amount ?? 0d)));
            var waybillPaidByContract = db.F_WAYBILL_PAYMENTS
                .Where(p => !p.IsDeleted && p.F_WAYBILLS.ContractID.HasValue &&
                            (!p.PaymentDate.HasValue || p.PaymentDate.Value <= date))
                .Select(p => new { ContractID = p.F_WAYBILLS.ContractID.Value, p.Payment_Amount })
                .ToList()
                .GroupBy(p => p.ContractID)
                .ToDictionary(g => g.Key, g => g.Sum(x => (decimal)x.Payment_Amount));

            foreach (var contractId in waybillTotalsByContract.Keys.Union(waybillPaidByContract.Keys).Distinct())
            {
                if (!fContractInfo.TryGetValue(contractId, out var info) || info.ForwarderID <= 0) continue;
                AddPositive(TypeForwarder, info.ForwarderID, contractId, info.ContractNo,
                    waybillTotalsByContract.TryGetValue(contractId, out var t) ? t : 0m,
                    waybillPaidByContract.TryGetValue(contractId, out var p) ? p : 0m);
            }

            AddContractRows(rows);
            db.SaveChanges();
        }

        #endregion
    }
}
