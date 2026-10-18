using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services.Crm;
using tahsinERP.ViewModels;
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
            // FIN-D11: subledger testi (SystemCode) balans bilan bitta funksiyada
            var categories = db.FIN_BALANCE_CATEGORIES
                .Where(c => c.IsActive)
                .OrderBy(c => c.Type)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList()
                .Where(c => !BalanceSheetService.IsDetailedSubledgerCategory(c))
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
        /// Shu sana holatidagi joriy qoldiqlardan kontragent opening qatorlarini tuzadi (eski opening o'chiriladi).
        /// Hisob akt-sverki va balans bilan bir xil: oldingi opening (bo'lsa) + undan keyingi hujjat/to'lovlar,
        /// shartnoma kesimida; shartnomaga bog'lanmagan to'lovlar qoldig'i kontragentning shartnomasiz qatori.
        /// Avans (manfiy qoldiq) ham yoziladi — opening jami kontragent qarzlariga (balansga) teng bo'ladi.
        /// Avval butun tarix noldan qayta hisoblanardi: oldingi (qo'lda to'g'rilangan) opening e'tiborsiz qolar,
        /// shartnomasiz to'lov va avanslar tushib qolardi (2026-06-30: AP +48 mln, AR −11,9 mln farq).
        /// </summary>
        public void AutoFill(DateTime snapshotDate, int userId)
        {
            var db = _db;
            var date = snapshotDate.Date;
            var defaultCurrencyId = DefaultCurrencyId();

            DeleteByDate(date);

            var rows = new List<ContractRow>();

            // Kreditorlik (ta'minotchi, ekspeditor): Party1 krediti — hujjatlar, debeti — to'lovlar.
            // Debitorlik (mijoz): Party1 debeti — hujjatlar, krediti — to'lovlar.
            void AddCounterparty<TContract>(byte type, int counterpartyId, List<TContract> contracts, Func<TContract, int> id, Func<TContract, string> no,
                Func<int?, string, ReconciliationViewModel> build, bool receivable)
            {
                decimal Docs(ReconciliationViewModel vm) => receivable ? vm.Party1DebitTotal : vm.Party1CreditTotal;
                decimal Paid(ReconciliationViewModel vm) => receivable ? vm.Party1CreditTotal : vm.Party1DebitTotal;

                var all = build(null, null);
                decimal restDocs = Docs(all), restPaid = Paid(all), restNet = all.FinalBalance;
                foreach (var c in contracts)
                {
                    var vm = build(id(c), no(c) ?? "");
                    restDocs -= Docs(vm); restPaid -= Paid(vm); restNet -= vm.FinalBalance;
                    if (vm.FinalBalance == 0) continue;
                    rows.Add(new ContractRow
                    {
                        SnapshotDate = date, BalanceType = type, CounterpartyID = counterpartyId,
                        ContractID = id(c), ContractNo = no(c) ?? "",
                        TotalAmount = Docs(vm), PaidAmount = Paid(vm), Amount = vm.FinalBalance,
                        CurrencyID = defaultCurrencyId, CreatedByUserID = userId
                    });
                }
                // Hech qaysi shartnomaga bog'lanmagan qoldiq (masalan shartnomasiz to'lov — avans)
                if (restNet != 0)
                {
                    rows.Add(new ContractRow
                    {
                        SnapshotDate = date, BalanceType = type, CounterpartyID = counterpartyId,
                        ContractID = null, ContractNo = "",
                        TotalAmount = Math.Max(0m, restDocs), PaidAmount = Math.Max(0m, restDocs) - restNet, Amount = restNet,
                        CurrencyID = defaultCurrencyId, CreatedByUserID = userId
                    });
                }
            }

            // 1) Mijozlar
            var sContracts = db.S_CONTRACTS.AsNoTracking().Where(c => c.IsDeleted != true)
                .Select(c => new { c.ID, c.CustomerID, c.ContractNo }).ToList();
            foreach (var customerId in db.CUSTOMERS.AsNoTracking().Where(c => c.IsDeleted != true).Select(c => c.ID).ToList())
            {
                var cid = customerId;
                AddCounterparty(TypeReceivable, cid, sContracts.Where(c => c.CustomerID == cid).ToList(), c => c.ID, c => c.ContractNo,
                    (contractId, no) => ReconciliationService.BuildCustomerReconciliation(db, cid, contractId, date, null, "", no), receivable: true);
            }

            // 2) Ta'minotchilar
            var pContracts = db.P_CONTRACTS.AsNoTracking().Where(c => c.IsDeleted != true)
                .Select(c => new { c.ID, c.SupplierID, c.ContractNo }).ToList();
            foreach (var supplierId in db.SUPPLIERS.AsNoTracking().Where(s => s.IsDeleted != true).Select(s => s.ID).ToList())
            {
                var sid = supplierId;
                AddCounterparty(TypePayable, sid, pContracts.Where(c => c.SupplierID == sid).ToList(), c => c.ID, c => c.ContractNo,
                    (contractId, no) => ReconciliationService.BuildSupplierReconciliation(db, sid, contractId, date, null, "", no), receivable: false);
            }

            // 3) Ekspeditorlar
            var fContracts = db.F_CONTRACTS.AsNoTracking().Where(c => c.IsDeleted == false)
                .Select(c => new { c.ID, c.ForwarderID, c.ContractNo }).ToList();
            foreach (var forwarderId in db.FORWARDERS.AsNoTracking().Where(f => f.IsDeleted != true).Select(f => f.ID).ToList())
            {
                var fid = forwarderId;
                AddCounterparty(TypeForwarder, fid, fContracts.Where(c => c.ForwarderID == fid).ToList(), c => c.ID, c => c.ContractNo,
                    (contractId, no) => ReconciliationService.BuildForwarderReconciliation(db, fid, contractId, date, null, "", no), receivable: false);
            }

            // 4) Oylik — xodim kesimida, balans bilan bir xil funksiya (oldingi opening + hisoblangan − to'langan)
            foreach (var e in FinLiabilities.PayrollByEmployee(db, new CounterpartyBalanceService(db), date).Employees.Where(e => e.Debt != 0))
            {
                rows.Add(new ContractRow
                {
                    SnapshotDate = date, BalanceType = TypePayroll, CounterpartyID = e.EmployeeID,
                    TotalAmount = e.Calculated, PaidAmount = e.TotalPaid, Amount = e.Debt,
                    CurrencyID = defaultCurrencyId, CreatedByUserID = userId
                });
            }

            AddContractRows(rows);
            db.SaveChanges();
        }

        #endregion
    }
}
