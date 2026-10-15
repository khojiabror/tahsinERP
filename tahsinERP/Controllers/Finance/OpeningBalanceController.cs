using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Web.Mvc;
using OfficeOpenXml;
using tahsinERP.Controllers;
using tahsinERP.Models;
using tahsinERP.ViewModels.Finance;
using tahsinERP.Helpers;
using tahsinERP.Services;

namespace tahsinERP.Controllers.Finance
{
    public class OpeningBalanceController : Controller
    {
        /// <summary>Ro'yxat: OpeningBalance moduli — ko'rish ruxsati</summary>
        public ActionResult Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                EnsureOpeningContractBalanceTable(db);
                var snapshotRows = db.FIN_BALANCE_SNAPSHOTS
                    .AsNoTracking()
                    .Where(s => s.SourceType == "OpeningBalance" || s.SourceType == null)
                    .Select(s => s.SnapshotDate)
                    .ToList()
                    .GroupBy(d => d.Date)
                    .Select(g => new OpeningBalanceDateRow
                    {
                        SnapshotDate = g.Key,
                        LineCount = g.Count()
                    })
                    .ToList();

                var contractRows = db.Database.SqlQuery<OpeningBalanceDateRow>(
                    "SELECT SnapshotDate, COUNT(1) AS LineCount FROM FIN_OPENING_CONTRACT_BALANCES GROUP BY SnapshotDate")
                    .ToList();

                var rows = snapshotRows
                    .Concat(contractRows)
                    .GroupBy(x => x.SnapshotDate.Date)
                    .Select(g => new OpeningBalanceDateRow
                    {
                        SnapshotDate = g.Key,
                        LineCount = g.Sum(x => x.LineCount)
                    })
                    .OrderByDescending(r => r.SnapshotDate)
                    .ToList();

                ViewBag.CanChange = RoleHelper.IsChangePermitted(User.Identity.Name, "OpeningBalance");
                return View(rows);
            }
        }

        /// <summary>Tahrir / ko'rish — GET: ko'rish ruxsati</summary>
        public ActionResult Edit(DateTime? snapshotDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var date = (snapshotDate ?? DateTime.Today).Date;
            using (var db = new DBTHSNEntities())
            {
                var model = BuildOpeningBalanceViewModel(db, date);
                ViewBag.CanChange = RoleHelper.IsChangePermitted(User.Identity.Name, "OpeningBalance");
                return View(model);
            }
        }

        /// <summary>Details — faqat ko'rish rejimi</summary>
        public ActionResult Details(DateTime? snapshotDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!snapshotDate.HasValue)
            {
                return RedirectToAction("Index");
            }

            var date = snapshotDate.Value.Date;
            using (var db = new DBTHSNEntities())
            {
                var model = BuildOpeningBalanceViewModel(db, date);
                return View(model);
            }
        }

        /// <summary>Saqlash — faqat o'zgartirish ruxsati</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(OpeningBalanceViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OpeningBalance"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (model == null)
            {
                return RedirectToAction("Index");
            }

            using (var db = new DBTHSNEntities())
            {
              try
              {
                EnsureOpeningContractBalanceTable(db);
                var date = model.SnapshotDate.Date;
                var userId = UserHelper.GetUserId(User.Identity.Name);

                // Shu sanadagi barcha eski opening yozuvlarini AVVAL tozalaymiz: kontragent balanslari
                // (FIN_OPENING_CONTRACT_BALANCES) + FIN_BALANCE_SNAPSHOTS dagi OpeningBalance/OpeningBalanceDetailed.
                // MUHIM: tozalash manual Items saqlanishidan OLDIN bo'lishi shart. Avval bu DeleteOpeningByDate
                // Items saqlangandan KEYIN chaqirilardi va endigina yozilgan manual kategoriya yozuvlarini
                // (Naqd pul, Xomashyo, Tayyor maxsulot, ...) o'chirib yuborardi — shu sabab saqlanmayotgandek edi.
                DeleteOpeningByDate(db, date);

                var currencies = db.CURRENCIES.ToDictionary(c => c.ID, c => c);

                // Mavjud bo'lgan standart valyutani aniqlaymiz (avval ID=5, bo'lmasa birinchisi).
                // model.DefaultCurrencyID formada hidden bo'lmagani uchun POST'da 0 bo'lib kelishi mumkin —
                // shu sabab DB'dagi haqiqiy valyuta ID'siga tayanamiz. Aks holda CurrencyID FK buziladi (500).
                int resolvedDefaultCurrencyId = currencies.ContainsKey(5)
                    ? 5
                    : (currencies.ContainsKey(model.DefaultCurrencyID) ? model.DefaultCurrencyID : currencies.Keys.FirstOrDefault());
                Func<int, int> clampCurrency = cid =>
                    (cid > 0 && currencies.ContainsKey(cid)) ? cid : resolvedDefaultCurrencyId;

                if (model.Items != null)
                {
                    foreach (var item in model.Items)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        // 0: "Bo'sh qoldirish" belgilangan bo'lsa — qator yozilmaydi; belgilanmagan bo'lsa — aniq 0 saqlanadi.
                        if (item.Amount == 0 && item.LeaveEmpty)
                        {
                            continue;
                        }

                        var currencyId = clampCurrency(item.CurrencyID);

                        var snap = new FIN_BALANCE_SNAPSHOTS
                        {
                            SnapshotDate = date,
                            CategoryID = item.CategoryID,
                            Amount = item.Amount,
                            CurrencyID = currencyId,
                            SourceType = "OpeningBalance",
                            SourceID = null,
                            CreatedByUserID = userId,
                            CreatedDT = DateTime.Now
                        };

                        db.FIN_BALANCE_SNAPSHOTS.Add(snap);
                    }

                    db.SaveChanges();
                }

                var contractRows = new List<OpeningContractBalanceRow>();
                if (!model.LeaveCustomerReceivableEmpty && model.CustomerReceivableLines != null)
                {
                    var sContractNoById = db.S_CONTRACTS
                        .Where(c => c.IsDeleted != true)
                        .ToDictionary(c => c.ID, c => c.ContractNo ?? "");
                    for (int i = 0; i < model.CustomerReceivableLines.Count; i++)
                    {
                        var x = model.CustomerReceivableLines[i];
                        if (x == null || x.CounterpartyID <= 0) continue;
                        var total = ParseDecimalFlexible(Request.Form["CustomerReceivableLines[" + i + "].TotalAmount"], x.TotalAmount);
                        var paid = ParseDecimalFlexible(Request.Form["CustomerReceivableLines[" + i + "].PaidAmount"], x.PaidAmount);
                        if (total == 0 && paid == 0) continue;
                        if (paid > total) paid = total;
                        contractRows.Add(new OpeningContractBalanceRow
                        {
                            SnapshotDate = date,
                            BalanceType = 1,
                            CounterpartyID = x.CounterpartyID,
                            ContractID = x.ContractID,
                            ContractNo = x.ContractID.HasValue && sContractNoById.ContainsKey(x.ContractID.Value)
                                ? sContractNoById[x.ContractID.Value]
                                : (x.ContractNo ?? ""),
                            TotalAmount = total,
                            PaidAmount = paid,
                            Amount = total - paid,
                            CurrencyID = clampCurrency(x.CurrencyID),
                            CreatedByUserID = userId
                        });
                    }
                }
                if (!model.LeaveSupplierPayableEmpty && model.SupplierPayableLines != null)
                {
                    var pContractNoById = db.P_CONTRACTS
                        .Where(c => c.IsDeleted != true)
                        .ToDictionary(c => c.ID, c => c.ContractNo ?? "");
                    for (int i = 0; i < model.SupplierPayableLines.Count; i++)
                    {
                        var x = model.SupplierPayableLines[i];
                        if (x == null || x.CounterpartyID <= 0) continue;
                        var total = ParseDecimalFlexible(Request.Form["SupplierPayableLines[" + i + "].TotalAmount"], x.TotalAmount);
                        var paid = ParseDecimalFlexible(Request.Form["SupplierPayableLines[" + i + "].PaidAmount"], x.PaidAmount);
                        if (total == 0 && paid == 0) continue;
                        if (paid > total) paid = total;
                        contractRows.Add(new OpeningContractBalanceRow
                        {
                            SnapshotDate = date,
                            BalanceType = 2,
                            CounterpartyID = x.CounterpartyID,
                            ContractID = x.ContractID,
                            ContractNo = x.ContractID.HasValue && pContractNoById.ContainsKey(x.ContractID.Value)
                                ? pContractNoById[x.ContractID.Value]
                                : (x.ContractNo ?? ""),
                            TotalAmount = total,
                            PaidAmount = paid,
                            Amount = total - paid,
                            CurrencyID = clampCurrency(x.CurrencyID),
                            CreatedByUserID = userId
                        });
                    }
                }
                if (!model.LeavePayrollEmpty && model.EmployeePayrollLines != null)
                {
                    for (int i = 0; i < model.EmployeePayrollLines.Count; i++)
                    {
                        var x = model.EmployeePayrollLines[i];
                        if (x == null || x.EmployeeID <= 0) continue;
                        var total = ParseDecimalFlexible(Request.Form["EmployeePayrollLines[" + i + "].TotalAmount"], x.TotalAmount);
                        var paid = ParseDecimalFlexible(Request.Form["EmployeePayrollLines[" + i + "].PaidAmount"], x.PaidAmount);
                        if (total == 0 && paid == 0) continue;
                        if (paid > total) paid = total;
                        contractRows.Add(new OpeningContractBalanceRow
                        {
                            SnapshotDate = date,
                            BalanceType = 3,
                            CounterpartyID = x.EmployeeID,
                            ContractID = null,
                            ContractNo = null,
                            TotalAmount = total,
                            PaidAmount = paid,
                            Amount = total - paid,
                            CurrencyID = clampCurrency(x.CurrencyID),
                            CreatedByUserID = userId
                        });
                    }
                }
                if (!model.LeaveForwarderPayableEmpty && model.ForwarderPayableLines != null)
                {
                    var fContractNoById = db.F_CONTRACTS
                        .Where(c => c.IsDeleted != true)
                        .ToDictionary(c => c.ID, c => c.ContractNo ?? "");
                    for (int i = 0; i < model.ForwarderPayableLines.Count; i++)
                    {
                        var x = model.ForwarderPayableLines[i];
                        if (x == null || x.CounterpartyID <= 0) continue;
                        var total = ParseDecimalFlexible(Request.Form["ForwarderPayableLines[" + i + "].TotalAmount"], x.TotalAmount);
                        var paid = ParseDecimalFlexible(Request.Form["ForwarderPayableLines[" + i + "].PaidAmount"], x.PaidAmount);
                        if (total == 0 && paid == 0) continue;
                        if (paid > total) paid = total;
                        contractRows.Add(new OpeningContractBalanceRow
                        {
                            SnapshotDate = date,
                            BalanceType = 4,
                            CounterpartyID = x.CounterpartyID,
                            ContractID = x.ContractID,
                            ContractNo = x.ContractID.HasValue && fContractNoById.ContainsKey(x.ContractID.Value)
                                ? fContractNoById[x.ContractID.Value]
                                : (x.ContractNo ?? ""),
                            TotalAmount = total,
                            PaidAmount = paid,
                            Amount = total - paid,
                            CurrencyID = clampCurrency(x.CurrencyID),
                            CreatedByUserID = userId
                        });
                    }
                }

                foreach (var row in contractRows)
                {
                    db.Database.ExecuteSqlCommand(
                        "INSERT INTO FIN_OPENING_CONTRACT_BALANCES (SnapshotDate, BalanceType, CounterpartyID, ContractID, ContractNo, TotalAmount, PaidAmount, Amount, CurrencyID, CreatedByUserID, CreatedDT) " +
                        "VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10)",
                        new SqlParameter("@p0", row.SnapshotDate),
                        new SqlParameter("@p1", row.BalanceType),
                        new SqlParameter("@p2", row.CounterpartyID),
                        new SqlParameter("@p3", (object)row.ContractID ?? DBNull.Value),
                        new SqlParameter("@p4", (object)row.ContractNo ?? DBNull.Value),
                        new SqlParameter("@p5", row.TotalAmount),
                        new SqlParameter("@p6", row.PaidAmount),
                        new SqlParameter("@p7", row.Amount),
                        new SqlParameter("@p8", row.CurrencyID),
                        new SqlParameter("@p9", (object)row.CreatedByUserID ?? DBNull.Value),
                        new SqlParameter("@p10", DateTime.Now));
                }

                UpsertDetailedOpeningSnapshots(db, date, userId, resolvedDefaultCurrencyId, contractRows);
              }
              catch (Exception ex)
              {
                  var msg = "Saqlashda xatolik: " + ex.Message;
                  var inner = ex.InnerException;
                  while (inner != null)
                  {
                      msg += " | " + inner.Message;
                      inner = inner.InnerException;
                  }
                  TempData["Error"] = msg;
                  return RedirectToAction("Edit", new { snapshotDate = model.SnapshotDate.ToString("yyyy-MM-dd") });
              }
            }

            TempData["Success"] = "Opening balans muvaffaqiyatli saqlandi.";
            return RedirectToAction("Edit", new { snapshotDate = model.SnapshotDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AutoFillFromCurrentBalances(DateTime snapshotDate)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OpeningBalance"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                var date = snapshotDate.Date;
                using (var db = new DBTHSNEntities())
                {
                    EnsureOpeningContractBalanceTable(db);
                    var userId = UserHelper.GetUserId(User.Identity.Name);
                    // Bu sahifada standart valyuta ID = 5 (mavjud bo'lmasa, birinchi valyuta).
                    var defaultCurrencyId = db.CURRENCIES.Any(c => c.ID == 5)
                        ? 5
                        : db.CURRENCIES.Select(c => c.ID).FirstOrDefault();
                    if (defaultCurrencyId <= 0) defaultCurrencyId = 5;

                DeleteOpeningByDate(db, date);

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
                // QQS faqat P_CONTRACT da saqlanadi: invoice summasini bog'langan shartnomaning
                // QQS stavkasi bo'yicha QQS bilan hisoblaymiz (QQS bo'lmasa, stavka 0 va summa o'zgarmaydi).
                var pInvoicesRaw = db.P_INVOICES
                    .Where(i => i.InvoiceDate <= date && (i.IsDeleted == null || i.IsDeleted == false))
                    .Select(i => new { i.ID, i.SupplierID, i.ContractID, i.Amount })
                    .ToList();
                var pContractIds = pInvoicesRaw.Where(i => i.ContractID > 0).Select(i => i.ContractID).Distinct().ToList();
                var pVatRateByContract = pContractIds.Count == 0
                    ? new Dictionary<int, double>()
                    : PContractVatHelper.BuildContractVatRates(
                        db.P_CONTRACT_PRODUCTS
                            .Where(p => pContractIds.Contains(p.ContractID))
                            .Select(p => new PContractVatHelper.ContractProductVatLine
                            {
                                ContractID = p.ContractID,
                                Amount = p.Amount,
                                VATAmount = p.VATAmount
                            })
                            .ToList());
                var pInvoices = pInvoicesRaw
                    .Select(i =>
                    {
                        var baseAmt = Convert.ToDecimal(i.Amount);
                        var rate = pVatRateByContract.ContainsKey(i.ContractID) ? pVatRateByContract[i.ContractID] : 0d;
                        return new { i.ID, i.SupplierID, i.ContractID, Amount = baseAmt + baseAmt * (decimal)rate / 100m };
                    })
                    .ToList();
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
                var payrollCalculatedByEmployee = db.HR_PAYROLL_LINES
                    .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                                l.HR_PAYROLL_HEADERS != null &&
                                (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                                (l.HR_PAYROLL_HEADERS.Status == "Approved" || l.HR_PAYROLL_HEADERS.Status == "Posted") &&
                                l.HR_PAYROLL_HEADERS.ApprovedDT.HasValue &&
                                l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= date)
                    .GroupBy(l => l.EmployeeID)
                    .Select(g => new { EmployeeID = g.Key, Total = g.Sum(x => (decimal?)x.NetSalary) ?? 0m })
                    .ToList()
                    .ToDictionary(x => x.EmployeeID, x => x.Total);
                var payrollPaidByEmployee = db.FIN_TRANSACTIONS
                    .Where(t => (t.IsDeleted == null || t.IsDeleted == false) &&
                                t.Direction == 2 &&
                                t.CounterpartyType == 6 &&
                                t.CounterpartyID.HasValue &&
                                t.TxnDate <= date)
                    .GroupBy(t => t.CounterpartyID.Value)
                    .Select(g => new { EmployeeID = g.Key, Paid = g.Sum(x => x.Amount) })
                    .ToList()
                    .ToDictionary(x => x.EmployeeID, x => x.Paid);

                var rows = new List<OpeningContractBalanceRow>();

                foreach (var key in cInvoiceTotalsByKey.Keys.Union(cPaymentTotalsByKey.Keys).Distinct())
                {
                    var total = cInvoiceTotalsByKey.ContainsKey(key) ? cInvoiceTotalsByKey[key] : 0m;
                    var paid = cPaymentTotalsByKey.ContainsKey(key) ? cPaymentTotalsByKey[key] : 0m;
                    var net = total - paid;
                    if (net <= 0) continue;
                    rows.Add(new OpeningContractBalanceRow
                    {
                        SnapshotDate = date,
                        BalanceType = 1,
                        CounterpartyID = key.Item1,
                        ContractID = cContractIdByKey.ContainsKey(key) ? cContractIdByKey[key] : null,
                        ContractNo = key.Item2,
                        TotalAmount = total,
                        PaidAmount = Math.Min(paid, total),
                        Amount = net,
                        CurrencyID = defaultCurrencyId,
                        CreatedByUserID = userId
                    });
                }

                foreach (var key in pInvoiceTotalsByKey.Keys.Union(pPaymentTotalsByKey.Keys).Distinct())
                {
                    var total = pInvoiceTotalsByKey.ContainsKey(key) ? pInvoiceTotalsByKey[key] : 0m;
                    var paid = pPaymentTotalsByKey.ContainsKey(key) ? pPaymentTotalsByKey[key] : 0m;
                    var net = total - paid;
                    if (net <= 0) continue;
                    rows.Add(new OpeningContractBalanceRow
                    {
                        SnapshotDate = date,
                        BalanceType = 2,
                        CounterpartyID = key.Item1,
                        ContractID = pContractIdByKey.ContainsKey(key) ? pContractIdByKey[key] : null,
                        ContractNo = key.Item2,
                        TotalAmount = total,
                        PaidAmount = Math.Min(paid, total),
                        Amount = net,
                        CurrencyID = defaultCurrencyId,
                        CreatedByUserID = userId
                    });
                }

                foreach (var employeeId in payrollCalculatedByEmployee.Keys.Union(payrollPaidByEmployee.Keys).Distinct())
                {
                    var total = payrollCalculatedByEmployee.ContainsKey(employeeId) ? payrollCalculatedByEmployee[employeeId] : 0m;
                    var paid = payrollPaidByEmployee.ContainsKey(employeeId) ? payrollPaidByEmployee[employeeId] : 0m;
                    var net = total - paid;
                    if (net <= 0) continue;
                    rows.Add(new OpeningContractBalanceRow
                    {
                        SnapshotDate = date,
                        BalanceType = 3,
                        CounterpartyID = employeeId,
                        ContractID = null,
                        ContractNo = null,
                        TotalAmount = total,
                        PaidAmount = Math.Min(paid, total),
                        Amount = net,
                        CurrencyID = defaultCurrencyId,
                        CreatedByUserID = userId
                    });
                }

                // 4) Logistikadan qarzimiz — ekspeditor shartnomasi kesimida (yo'l varaqalari − to'lovlar)
                var fContractInfo = db.F_CONTRACTS
                    .Where(c => c.IsDeleted != true)
                    .Select(c => new { c.ID, c.ForwarderID, c.ContractNo })
                    .ToList();
                // double ustunlarni (Amount/Payment_Amount) EF ichida decimal'ga cast qilmaymiz — avval
                // xotiraga olib, keyin decimal summalaymiz (GetForwarderContractTotals bilan bir xil yondashuv).
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
                    var info = fContractInfo.FirstOrDefault(c => c.ID == contractId);
                    if (info == null || info.ForwarderID <= 0) continue;
                    var total = waybillTotalsByContract.ContainsKey(contractId) ? waybillTotalsByContract[contractId] : 0m;
                    var paid = waybillPaidByContract.ContainsKey(contractId) ? waybillPaidByContract[contractId] : 0m;
                    var net = total - paid;
                    if (net <= 0) continue;
                    rows.Add(new OpeningContractBalanceRow
                    {
                        SnapshotDate = date,
                        BalanceType = 4,
                        CounterpartyID = info.ForwarderID,
                        ContractID = contractId,
                        ContractNo = info.ContractNo,
                        TotalAmount = total,
                        PaidAmount = Math.Min(paid, total),
                        Amount = net,
                        CurrencyID = defaultCurrencyId,
                        CreatedByUserID = userId
                    });
                }

                    foreach (var row in rows)
                    {
                        db.Database.ExecuteSqlCommand(
                            "INSERT INTO FIN_OPENING_CONTRACT_BALANCES (SnapshotDate, BalanceType, CounterpartyID, ContractID, ContractNo, TotalAmount, PaidAmount, Amount, CurrencyID, CreatedByUserID, CreatedDT) " +
                            "VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10)",
                            new SqlParameter("@p0", row.SnapshotDate),
                            new SqlParameter("@p1", row.BalanceType),
                            new SqlParameter("@p2", row.CounterpartyID),
                            new SqlParameter("@p3", (object)row.ContractID ?? DBNull.Value),
                            new SqlParameter("@p4", (object)row.ContractNo ?? DBNull.Value),
                            new SqlParameter("@p5", row.TotalAmount),
                            new SqlParameter("@p6", row.PaidAmount),
                            new SqlParameter("@p7", row.Amount),
                            new SqlParameter("@p8", row.CurrencyID),
                            new SqlParameter("@p9", (object)row.CreatedByUserID ?? DBNull.Value),
                            new SqlParameter("@p10", DateTime.Now));
                    }

                    UpsertDetailedOpeningSnapshots(db, date, userId, defaultCurrencyId, rows);
                }

                TempData["Success"] = "Auto-fill bajarildi: current balances asosida detailed opening qatorlari yangilandi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Auto-fill xatoligi: " + ex.Message;
            }

            return RedirectToAction("Edit", new { snapshotDate = snapshotDate.ToString("yyyy-MM-dd") });
        }

        [HttpGet]
        public JsonResult GetCustomerContracts(int customerId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return Json(new { success = false, message = "Not authorized" }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new DBTHSNEntities())
            {
                var data = db.S_CONTRACTS
                    .Where(c => c.IsDeleted != true && c.CustomerID == customerId)
                    .OrderByDescending(c => c.IssuedDate)
                    .ThenByDescending(c => c.ID)
                    .Select(c => new
                    {
                        id = c.ID,
                        text = c.ContractNo ?? ("Shartnoma #" + c.ID)
                    })
                    .ToList();

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetSupplierContracts(int supplierId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return Json(new { success = false, message = "Not authorized" }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new DBTHSNEntities())
            {
                var data = db.P_CONTRACTS
                    .Where(c => c.IsDeleted != true && c.SupplierID == supplierId)
                    .OrderByDescending(c => c.IssuedDate)
                    .ThenByDescending(c => c.ID)
                    .Select(c => new
                    {
                        id = c.ID,
                        text = c.ContractNo ?? ("Shartnoma #" + c.ID)
                    })
                    .ToList();

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetCustomerContractTotals(int customerId, int? contractId, DateTime? snapshotDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return Json(new { success = false, message = "Not authorized" }, JsonRequestBehavior.AllowGet);
            }

            if (customerId <= 0 || !contractId.HasValue || contractId.Value <= 0)
            {
                return Json(new { success = false, message = "Invalid parameters" }, JsonRequestBehavior.AllowGet);
            }

            var date = (snapshotDate ?? DateTime.Today).Date;
            using (var db = new DBTHSNEntities())
            {
                Func<string, string> normalize = s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpper();

                var invoices = db.SALES_INVOICES
                    .Where(i => i.CustomerID == customerId && i.ContractID == contractId.Value && i.InvoiceDate <= date)
                    .Select(i => new { i.ID, Amount = i.TotalWithVAT ?? i.TotalAmount })
                    .ToList();

                var contractNo = db.S_CONTRACTS
                    .Where(c => c.ID == contractId.Value && c.CustomerID == customerId && c.IsDeleted != true)
                    .Select(c => c.ContractNo)
                    .FirstOrDefault() ?? "";
                var contractNoNorm = normalize(contractNo);

                var invIdSet = new HashSet<long>(invoices.Select(x => x.ID));
                var payCandidates = db.FIN_TRANSACTIONS
                    .Where(t => t.TxnDate <= date &&
                                (t.IsDeleted == null || t.IsDeleted == false) &&
                                t.SourceDocType != "SalesInvoice" &&
                                ((t.Direction == 1 && t.CounterpartyType == 1 && t.CounterpartyID == customerId) ||
                                 (t.SourceDocType == "SalesInvoicePayment" && t.SourceDocID.HasValue)))
                    .Select(t => new { t.Amount, t.SourceDocType, t.SourceDocID, t.ContractNo })
                    .ToList();

                var paid = payCandidates
                    .Where(p =>
                        (p.SourceDocType == "SalesInvoicePayment" && p.SourceDocID.HasValue && invIdSet.Contains((long)p.SourceDocID.Value))
                        || (!string.IsNullOrWhiteSpace(contractNoNorm) && normalize(p.ContractNo) == contractNoNorm))
                    .Sum(p => Math.Abs(p.Amount));

                var total = invoices.Sum(x => x.Amount);
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalAmount = total,
                        paidAmount = Math.Min(paid, total),
                        amount = total - Math.Min(paid, total)
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetSupplierContractTotals(int supplierId, int? contractId, DateTime? snapshotDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return Json(new { success = false, message = "Not authorized" }, JsonRequestBehavior.AllowGet);
            }

            if (supplierId <= 0 || !contractId.HasValue || contractId.Value <= 0)
            {
                return Json(new { success = false, message = "Invalid parameters" }, JsonRequestBehavior.AllowGet);
            }

            var date = (snapshotDate ?? DateTime.Today).Date;
            using (var db = new DBTHSNEntities())
            {
                Func<string, string> normalize = s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpper();

                // QQS faqat P_CONTRACT da: invoice bazasini shartnoma QQS stavkasi bilan QQS bilan olamiz.
                var vatRate = PContractVatHelper.CalculateContractVatRate(
                    db.P_CONTRACT_PRODUCTS.Where(p => p.ContractID == contractId.Value).ToList());
                var vatMultiplier = 1m + (decimal)vatRate / 100m;
                var invoices = db.P_INVOICES
                    .Where(i => i.SupplierID == supplierId && i.ContractID == contractId.Value && i.InvoiceDate <= date && (i.IsDeleted == null || i.IsDeleted == false))
                    .Select(i => new { i.ID, i.Amount })
                    .ToList()
                    .Select(i => new { i.ID, Amount = Convert.ToDecimal(i.Amount) * vatMultiplier })
                    .ToList();

                var contractNo = db.P_CONTRACTS
                    .Where(c => c.ID == contractId.Value && c.SupplierID == supplierId && c.IsDeleted != true)
                    .Select(c => c.ContractNo)
                    .FirstOrDefault() ?? "";
                var contractNoNorm = normalize(contractNo);

                var invIdSet = new HashSet<int>(invoices.Select(x => x.ID));
                var payCandidates = db.FIN_TRANSACTIONS
                    .Where(t => t.TxnDate <= date &&
                                (t.IsDeleted == null || t.IsDeleted == false) &&
                                (t.SourceDocType == null || (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice")) &&
                                ((t.Direction == 2 && t.CounterpartyType == 2 && t.CounterpartyID == supplierId) ||
                                 (t.SourceDocType == "PurchaseInvoicePayment" && t.SourceDocID.HasValue)))
                    .Select(t => new { t.Amount, t.SourceDocType, t.SourceDocID, t.ContractNo })
                    .ToList();

                var paid = payCandidates
                    .Where(p =>
                        (p.SourceDocType == "PurchaseInvoicePayment" && p.SourceDocID.HasValue && invIdSet.Contains(p.SourceDocID.Value))
                        || (!string.IsNullOrWhiteSpace(contractNoNorm) && normalize(p.ContractNo) == contractNoNorm))
                    .Sum(p => Math.Abs(p.Amount));

                var total = invoices.Sum(x => x.Amount);
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalAmount = total,
                        paidAmount = Math.Min(paid, total),
                        amount = total - Math.Min(paid, total)
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetForwarderContracts(int forwarderId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return Json(new { success = false, message = "Not authorized" }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new DBTHSNEntities())
            {
                var data = db.F_CONTRACTS
                    .Where(c => c.IsDeleted != true && c.ForwarderID == forwarderId)
                    .OrderByDescending(c => c.IssueDate)
                    .ThenByDescending(c => c.ID)
                    .Select(c => new
                    {
                        id = c.ID,
                        text = c.ContractNo ?? ("Shartnoma #" + c.ID)
                    })
                    .ToList();

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetForwarderContractTotals(int forwarderId, int? contractId, DateTime? snapshotDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return Json(new { success = false, message = "Not authorized" }, JsonRequestBehavior.AllowGet);
            }

            if (forwarderId <= 0 || !contractId.HasValue || contractId.Value <= 0)
            {
                return Json(new { success = false, message = "Invalid parameters" }, JsonRequestBehavior.AllowGet);
            }

            var date = (snapshotDate ?? DateTime.Today).Date;
            using (var db = new DBTHSNEntities())
            {
                // Umumiy oldi-berdi: shu shartnoma bo'yicha yo'l varaqalari summasi (sana gacha).
                // MUHIM: customer/supplier endpointlari kabi summani XOTIRADA hisoblaymiz. EF6 ichida
                // double ustun (w.Amount) ni (decimal?) ga cast qilib .DefaultIfEmpty(0m).Sum() qilish
                // SQL'ga tarjima qilinganda xato berib, fetch .catch() ichida jim qolardi — "Umumiy oldi-berdi" 0 bo'lib yuklanmasdi.
                var total = db.F_WAYBILLS
                    .Where(w => w.ContractID == contractId.Value && w.IsDeleted != true &&
                                w.Amount.HasValue && w.IssueDate.HasValue && w.IssueDate.Value <= date)
                    .Select(w => w.Amount)
                    .ToList()
                    .Sum(a => (decimal)(a ?? 0d));

                // To'langan: shu shartnoma yo'l varaqalariga qilingan ekspeditor to'lovlari (sana gacha).
                var paid = db.F_WAYBILL_PAYMENTS
                    .Where(p => !p.IsDeleted &&
                                p.F_WAYBILLS.ContractID == contractId.Value &&
                                (!p.PaymentDate.HasValue || p.PaymentDate.Value <= date))
                    .Select(p => p.Payment_Amount)
                    .ToList()
                    .Sum(a => (decimal)a);

                if (paid > total) paid = total;

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalAmount = total,
                        paidAmount = paid,
                        amount = total - paid
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult DownloadOpeningBalanceExcelTemplate()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var ar = package.Workbook.Worksheets.Add("AR");
                var ap = package.Workbook.Worksheets.Add("AP");
                var headers = new[]
                {
                    "CounterpartyName",
                    "CounterpartyID",
                    "ContractNo",
                    "ContractID",
                    "TotalAmount",
                    "PaidAmount",
                    "Currency"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    ar.Cells[1, i + 1].Value = headers[i];
                    ap.Cells[1, i + 1].Value = headers[i];
                }

                ar.Cells[2, 1].Value = "Uz-Koram Kompani";
                ar.Cells[2, 2].Value = "";
                ar.Cells[2, 3].Value = "27/10-2025";
                ar.Cells[2, 4].Value = "";
                ar.Cells[2, 5].Value = 56576700;
                ar.Cells[2, 6].Value = 51467720;
                ar.Cells[2, 7].Value = "UZS";

                ap.Cells[2, 1].Value = "Supplier name";
                ap.Cells[2, 2].Value = "";
                ap.Cells[2, 3].Value = "Contract-001";
                ap.Cells[2, 4].Value = "";
                ap.Cells[2, 5].Value = 10000000;
                ap.Cells[2, 6].Value = 2500000;
                ap.Cells[2, 7].Value = "UZS";

                ar.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
                ap.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
                ar.Cells.AutoFitColumns();
                ap.Cells.AutoFitColumns();

                var file = package.GetAsByteArray();
                return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "opening-balance-template.xlsx");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ImportOpeningBalanceExcel(DateTime snapshotDate)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OpeningBalance"))
            {
                return Json(new { success = false, message = "Not authorized" });
            }

            var file = Request.Files["file"];
            if (file == null || file.ContentLength <= 0)
            {
                return Json(new { success = false, message = "Fayl tanlanmagan." });
            }
            if (Path.GetExtension(file.FileName ?? "").ToLowerInvariant() != ".xlsx")
            {
                return Json(new { success = false, message = "Faqat .xlsx format qabul qilinadi." });
            }

            var customerRows = new List<object>();
            var supplierRows = new List<object>();
            var errors = new List<string>();

            using (var db = new DBTHSNEntities())
            {
                // Bu sahifada standart valyuta ID = 5 (mavjud bo'lmasa, birinchi valyuta).
                var defaultCurrencyId = db.CURRENCIES.Any(c => c.ID == 5)
                    ? 5
                    : db.CURRENCIES.Select(c => c.ID).FirstOrDefault();
                if (defaultCurrencyId <= 0) defaultCurrencyId = 5;
                var currencyByCode = db.CURRENCIES.ToDictionary(x => (x.Shortname ?? "").Trim().ToUpper(), x => x.ID);

                var customers = db.CUSTOMERS.Where(x => x.IsDeleted != true).Select(x => new { x.ID, x.Name }).ToList();
                var suppliers = db.SUPPLIERS.Where(x => x.IsDeleted != true).Select(x => new { x.ID, x.Name }).ToList();
                var sContracts = db.S_CONTRACTS.Where(x => x.IsDeleted != true).Select(x => new { x.ID, x.CustomerID, x.ContractNo }).ToList();
                var pContracts = db.P_CONTRACTS.Where(x => x.IsDeleted != true).Select(x => new { x.ID, x.SupplierID, x.ContractNo }).ToList();

                Func<string, string> norm = s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpperInvariant();
                Func<string, decimal?> parseDec = s =>
                {
                    if (string.IsNullOrWhiteSpace(s)) return null;
                    decimal v;
                    var cleaned = s.Replace(" ", "").Replace(",", ".");
                    if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
                    if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.CurrentCulture, out v)) return v;
                    return null;
                };

                Func<int, int, decimal> calcCustomerTotal = (customerId, contractId) =>
                {
                    return db.SALES_INVOICES
                        .Where(i => i.CustomerID == customerId && i.ContractID == contractId && i.InvoiceDate <= snapshotDate)
                        .Select(i => (decimal?)(i.TotalWithVAT ?? i.TotalAmount))
                        .DefaultIfEmpty(0m).Sum() ?? 0m;
                };
                Func<int, int, decimal> calcSupplierTotal = (supplierId, contractId) =>
                {
                    var baseTotal = db.P_INVOICES
                        .Where(i => i.SupplierID == supplierId && i.ContractID == contractId && i.InvoiceDate <= snapshotDate && (i.IsDeleted == null || i.IsDeleted == false))
                        .Select(i => (decimal?)i.Amount)
                        .DefaultIfEmpty(0m).Sum() ?? 0m;
                    // QQS faqat P_CONTRACT da: shartnoma QQS stavkasi bilan QQS bilan olamiz.
                    var vatRate = PContractVatHelper.CalculateContractVatRate(
                        db.P_CONTRACT_PRODUCTS.Where(p => p.ContractID == contractId).ToList());
                    return baseTotal + baseTotal * (decimal)vatRate / 100m;
                };

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage(file.InputStream))
                {
                    Action<string, bool> importSheet = (sheetName, isCustomer) =>
                    {
                        var ws = package.Workbook.Worksheets.FirstOrDefault(x => x.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
                        if (ws == null || ws.Dimension == null) return;

                        var rowCount = ws.Dimension.Rows;
                        for (int r = 2; r <= rowCount; r++)
                        {
                            var cpNameRaw = ws.Cells[r, 1].Text?.Trim();
                            var cpIdRaw = ws.Cells[r, 2].Text?.Trim();
                            var contractNoRaw = ws.Cells[r, 3].Text?.Trim();
                            var contractIdRaw = ws.Cells[r, 4].Text?.Trim();
                            var totalRaw = ws.Cells[r, 5].Text?.Trim();
                            var paidRaw = ws.Cells[r, 6].Text?.Trim();
                            var currencyRaw = ws.Cells[r, 7].Text?.Trim();

                            if (string.IsNullOrWhiteSpace(cpNameRaw) &&
                                string.IsNullOrWhiteSpace(cpIdRaw) &&
                                string.IsNullOrWhiteSpace(contractNoRaw) &&
                                string.IsNullOrWhiteSpace(contractIdRaw) &&
                                string.IsNullOrWhiteSpace(totalRaw) &&
                                string.IsNullOrWhiteSpace(paidRaw))
                            {
                                continue;
                            }

                            int cpId = 0;
                            int tmpInt;
                            if (!string.IsNullOrWhiteSpace(cpIdRaw) && int.TryParse(cpIdRaw, out tmpInt))
                            {
                                cpId = tmpInt;
                            }
                            else if (!string.IsNullOrWhiteSpace(cpNameRaw))
                            {
                                cpId = isCustomer
                                    ? (customers.FirstOrDefault(x => norm(x.Name) == norm(cpNameRaw))?.ID ?? 0)
                                    : (suppliers.FirstOrDefault(x => norm(x.Name) == norm(cpNameRaw))?.ID ?? 0);
                            }

                            if (cpId <= 0)
                            {
                                errors.Add(sheetName + " row " + r + ": kontragent topilmadi.");
                                continue;
                            }

                            int contractId = 0;
                            if (!string.IsNullOrWhiteSpace(contractIdRaw) && int.TryParse(contractIdRaw, out tmpInt))
                            {
                                contractId = tmpInt;
                            }
                            else if (!string.IsNullOrWhiteSpace(contractNoRaw))
                            {
                                contractId = isCustomer
                                    ? (sContracts.FirstOrDefault(x => x.CustomerID == cpId && norm(x.ContractNo) == norm(contractNoRaw))?.ID ?? 0)
                                    : (pContracts.FirstOrDefault(x => x.SupplierID == cpId && norm(x.ContractNo) == norm(contractNoRaw))?.ID ?? 0);
                            }

                            if (contractId <= 0)
                            {
                                errors.Add(sheetName + " row " + r + ": shartnoma topilmadi.");
                                continue;
                            }

                            var contractNo = isCustomer
                                ? (sContracts.FirstOrDefault(x => x.ID == contractId && x.CustomerID == cpId)?.ContractNo ?? "")
                                : (pContracts.FirstOrDefault(x => x.ID == contractId && x.SupplierID == cpId)?.ContractNo ?? "");
                            if (string.IsNullOrWhiteSpace(contractNo))
                            {
                                errors.Add(sheetName + " row " + r + ": shartnoma kontragentga mos emas.");
                                continue;
                            }

                            var totalParsed = parseDec(totalRaw);
                            var paidParsed = parseDec(paidRaw) ?? 0m;
                            var totalAmount = totalParsed ?? (isCustomer ? calcCustomerTotal(cpId, contractId) : calcSupplierTotal(cpId, contractId));
                            var paidAmount = paidParsed;
                            if (paidAmount > totalAmount) paidAmount = totalAmount;
                            if (paidAmount < 0) paidAmount = 0;

                            var currencyCode = norm(currencyRaw);
                            var currencyId = !string.IsNullOrWhiteSpace(currencyCode) && currencyByCode.ContainsKey(currencyCode)
                                ? currencyByCode[currencyCode]
                                : defaultCurrencyId;

                            var cpName = isCustomer
                                ? (customers.FirstOrDefault(x => x.ID == cpId)?.Name ?? "")
                                : (suppliers.FirstOrDefault(x => x.ID == cpId)?.Name ?? "");

                            var mapped = new
                            {
                                CounterpartyID = cpId,
                                CounterpartyName = cpName,
                                ContractID = contractId,
                                ContractNo = contractNo,
                                TotalAmount = totalAmount,
                                PaidAmount = paidAmount,
                                Amount = totalAmount - paidAmount,
                                CurrencyID = currencyId
                            };

                            if (isCustomer) customerRows.Add(mapped);
                            else supplierRows.Add(mapped);
                        }
                    };

                    importSheet("AR", true);
                    importSheet("AP", false);
                }
            }

            return Json(new
            {
                success = true,
                customerLines = customerRows,
                supplierLines = supplierRows,
                errors = errors
            });
        }

        /// <summary>O'chirish tasdiq — faqat o'zgartirish ruxsati</summary>
        [HttpGet]
        public ActionResult Delete(DateTime? snapshotDate)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OpeningBalance"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (snapshotDate == null)
            {
                return RedirectToAction("Index");
            }

            var date = snapshotDate.Value.Date;
            using (var db = new DBTHSNEntities())
            {
                EnsureOpeningContractBalanceTable(db);
                var lineCount = db.FIN_BALANCE_SNAPSHOTS.Count(s =>
                    DbFunctions.TruncateTime(s.SnapshotDate) == date &&
                    (s.SourceType == "OpeningBalance" || s.SourceType == null));
                var contractLineCount = db.Database.SqlQuery<int>(
                    "SELECT COUNT(1) FROM FIN_OPENING_CONTRACT_BALANCES WHERE SnapshotDate = @p0",
                    new SqlParameter("@p0", date)).FirstOrDefault();
                lineCount += contractLineCount;

                if (lineCount == 0)
                {
                    TempData["Error"] = "Tanlangan sana uchun opening balans yozuvlari topilmadi.";
                    return RedirectToAction("Index");
                }

                return View(new OpeningBalanceDeleteViewModel
                {
                    SnapshotDate = date,
                    LineCount = lineCount
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(DateTime snapshotDate)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OpeningBalance"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var date = snapshotDate.Date;
            using (var db = new DBTHSNEntities())
            {
                EnsureOpeningContractBalanceTable(db);
                var existing = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => DbFunctions.TruncateTime(s.SnapshotDate) == date &&
                                (s.SourceType == "OpeningBalance" || s.SourceType == null))
                    .ToList();

                if (existing.Any())
                {
                    db.FIN_BALANCE_SNAPSHOTS.RemoveRange(existing);
                    db.SaveChanges();
                }

                DeleteOpeningByDate(db, date);
            }

            TempData["Success"] = $"Opening balans ({date:yyyy-MM-dd}) o'chirildi.";
            return RedirectToAction("Index");
        }

        private static OpeningBalanceViewModel BuildOpeningBalanceViewModel(DBTHSNEntities db, DateTime date)
        {
            EnsureOpeningContractBalanceTable(db);
            var categories = db.FIN_BALANCE_CATEGORIES
                .Where(c => c.IsActive)
                .OrderBy(c => c.Type)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList()
                .Where(c => !IsDetailedSubledgerCategory(c.Name))
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

            var model = new OpeningBalanceViewModel
            {
                SnapshotDate = date,
                DefaultCurrencyID = defaultCurrency?.ID ?? 5,
                CurrencyList = currencies.Select(c => new SelectListItem
                {
                    Value = c.ID.ToString(),
                    Text = c.Shortname
                }),
                CustomerList = db.CUSTOMERS
                    .Where(c => c.IsDeleted != true)
                    .OrderBy(c => c.Name)
                    .Select(c => new SelectListItem
                    {
                        Value = c.ID.ToString(),
                        Text = c.Name
                    })
                    .ToList(),
                SupplierList = db.SUPPLIERS
                    .Where(s => s.IsDeleted != true)
                    .OrderBy(s => s.Name)
                    .Select(s => new SelectListItem
                    {
                        Value = s.ID.ToString(),
                        Text = s.Name
                    })
                    .ToList(),
                EmployeeList = db.HR_EMPLOYEES
                    .Where(e => e.IsDeleted != true)
                    .OrderBy(e => e.FullName)
                    .Select(e => new SelectListItem
                    {
                        Value = e.ID.ToString(),
                        Text = (e.TabNo ?? "-") + " - " + (e.FullName ?? "")
                    })
                    .ToList(),
                ForwarderList = db.FORWARDERS
                    .Where(f => f.IsDeleted != true)
                    .OrderBy(f => f.ForwarderName)
                    .Select(f => new SelectListItem
                    {
                        Value = f.ID.ToString(),
                        Text = f.ForwarderName
                    })
                    .ToList()
            };

            foreach (var cat in categories)
            {
                FIN_BALANCE_SNAPSHOTS snap = null;
                snapshots.TryGetValue(cat.ID, out snap);

                var item = new OpeningBalanceCategoryItem
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
                };

                model.Items.Add(item);
            }

            var openingContractRows = db.Database.SqlQuery<OpeningContractBalanceRow>(
                "SELECT ID, SnapshotDate, BalanceType, CounterpartyID, ContractID, ContractNo, TotalAmount, PaidAmount, Amount, CurrencyID, CreatedByUserID, CreatedDT " +
                "FROM FIN_OPENING_CONTRACT_BALANCES WHERE SnapshotDate = @p0",
                new SqlParameter("@p0", date)).ToList();

            var customerNameById = db.CUSTOMERS
                .Where(c => c.IsDeleted != true)
                .ToDictionary(c => c.ID, c => c.Name ?? "Noma'lum");
            var supplierNameById = db.SUPPLIERS
                .Where(s => s.IsDeleted != true)
                .ToDictionary(s => s.ID, s => s.Name ?? "Noma'lum");
            var sContractNoById = db.S_CONTRACTS
                .Where(c => c.IsDeleted != true)
                .ToDictionary(c => c.ID, c => c.ContractNo ?? "");
            var pContractNoById = db.P_CONTRACTS
                .Where(c => c.IsDeleted != true)
                .ToDictionary(c => c.ID, c => c.ContractNo ?? "");

            model.CustomerReceivableLines = openingContractRows
                .Where(x => x.BalanceType == 1)
                .Select(x => new OpeningBalanceContractLineItem
                {
                    CounterpartyID = x.CounterpartyID,
                    CounterpartyName = customerNameById.ContainsKey(x.CounterpartyID) ? customerNameById[x.CounterpartyID] : $"Customer #{x.CounterpartyID}",
                    ContractID = x.ContractID,
                    ContractNo = x.ContractID.HasValue && sContractNoById.ContainsKey(x.ContractID.Value)
                        ? sContractNoById[x.ContractID.Value]
                        : (x.ContractNo ?? ""),
                    TotalAmount = x.TotalAmount,
                    PaidAmount = x.PaidAmount,
                    Amount = x.Amount,
                    CurrencyID = x.CurrencyID > 0 ? x.CurrencyID : model.DefaultCurrencyID
                })
                .ToList();

            model.SupplierPayableLines = openingContractRows
                .Where(x => x.BalanceType == 2)
                .Select(x => new OpeningBalanceContractLineItem
                {
                    CounterpartyID = x.CounterpartyID,
                    CounterpartyName = supplierNameById.ContainsKey(x.CounterpartyID) ? supplierNameById[x.CounterpartyID] : $"Supplier #{x.CounterpartyID}",
                    ContractID = x.ContractID,
                    ContractNo = x.ContractID.HasValue && pContractNoById.ContainsKey(x.ContractID.Value)
                        ? pContractNoById[x.ContractID.Value]
                        : (x.ContractNo ?? ""),
                    TotalAmount = x.TotalAmount,
                    PaidAmount = x.PaidAmount,
                    Amount = x.Amount,
                    CurrencyID = x.CurrencyID > 0 ? x.CurrencyID : model.DefaultCurrencyID
                })
                .ToList();

            var employeeNameById = db.HR_EMPLOYEES
                .Where(e => e.IsDeleted != true)
                .ToDictionary(e => e.ID, e => (e.TabNo ?? "-") + " - " + (e.FullName ?? ""));
            model.EmployeePayrollLines = openingContractRows
                .Where(x => x.BalanceType == 3)
                .Select(x => new OpeningBalancePayrollLineItem
                {
                    EmployeeID = x.CounterpartyID,
                    EmployeeName = employeeNameById.ContainsKey(x.CounterpartyID) ? employeeNameById[x.CounterpartyID] : $"Xodim #{x.CounterpartyID}",
                    TotalAmount = x.TotalAmount,
                    PaidAmount = x.PaidAmount,
                    Amount = x.Amount,
                    CurrencyID = x.CurrencyID > 0 ? x.CurrencyID : model.DefaultCurrencyID
                })
                .ToList();

            var forwarderNameById = db.FORWARDERS
                .Where(f => f.IsDeleted != true)
                .ToDictionary(f => f.ID, f => f.ForwarderName ?? "Noma'lum");
            var fContractNoById = db.F_CONTRACTS
                .Where(c => c.IsDeleted != true)
                .ToDictionary(c => c.ID, c => c.ContractNo ?? "");
            model.ForwarderPayableLines = openingContractRows
                .Where(x => x.BalanceType == 4)
                .Select(x => new OpeningBalanceContractLineItem
                {
                    CounterpartyID = x.CounterpartyID,
                    CounterpartyName = forwarderNameById.ContainsKey(x.CounterpartyID) ? forwarderNameById[x.CounterpartyID] : $"Ekspeditor #{x.CounterpartyID}",
                    ContractID = x.ContractID,
                    ContractNo = x.ContractID.HasValue && fContractNoById.ContainsKey(x.ContractID.Value)
                        ? fContractNoById[x.ContractID.Value]
                        : (x.ContractNo ?? ""),
                    TotalAmount = x.TotalAmount,
                    PaidAmount = x.PaidAmount,
                    Amount = x.Amount,
                    CurrencyID = x.CurrencyID > 0 ? x.CurrencyID : model.DefaultCurrencyID
                })
                .ToList();

            return model;
        }

        private static void EnsureOpeningContractBalanceTable(DBTHSNEntities db)
        {
            const string sql = @"
IF OBJECT_ID('dbo.FIN_OPENING_CONTRACT_BALANCES', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FIN_OPENING_CONTRACT_BALANCES(
        ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SnapshotDate DATE NOT NULL,
        BalanceType TINYINT NOT NULL, -- 1: Customer receivable, 2: Supplier payable
        CounterpartyID INT NOT NULL,
        ContractID INT NULL,
        ContractNo NVARCHAR(100) NULL,
        TotalAmount DECIMAL(19,2) NOT NULL CONSTRAINT DF_FIN_OPENING_CONTRACT_BALANCES_TotalAmount DEFAULT(0),
        PaidAmount DECIMAL(19,2) NOT NULL CONSTRAINT DF_FIN_OPENING_CONTRACT_BALANCES_PaidAmount DEFAULT(0),
        Amount DECIMAL(19,2) NOT NULL,
        CurrencyID INT NOT NULL,
        CreatedByUserID INT NULL,
        CreatedDT DATETIME NOT NULL DEFAULT(GETDATE())
    );
    CREATE INDEX IX_FIN_OPENING_CONTRACT_BALANCES_SnapshotDate ON dbo.FIN_OPENING_CONTRACT_BALANCES(SnapshotDate);
    CREATE INDEX IX_FIN_OPENING_CONTRACT_BALANCES_TypeCounterparty ON dbo.FIN_OPENING_CONTRACT_BALANCES(BalanceType, CounterpartyID);
END";
            db.Database.ExecuteSqlCommand(sql);
            db.Database.ExecuteSqlCommand("IF COL_LENGTH('dbo.FIN_OPENING_CONTRACT_BALANCES','TotalAmount') IS NULL ALTER TABLE dbo.FIN_OPENING_CONTRACT_BALANCES ADD TotalAmount DECIMAL(19,2) NOT NULL CONSTRAINT DF_FIN_OPENING_CONTRACT_BALANCES_TotalAmount_2 DEFAULT(0)");
            db.Database.ExecuteSqlCommand("IF COL_LENGTH('dbo.FIN_OPENING_CONTRACT_BALANCES','PaidAmount') IS NULL ALTER TABLE dbo.FIN_OPENING_CONTRACT_BALANCES ADD PaidAmount DECIMAL(19,2) NOT NULL CONSTRAINT DF_FIN_OPENING_CONTRACT_BALANCES_PaidAmount_2 DEFAULT(0)");
            db.Database.ExecuteSqlCommand("UPDATE dbo.FIN_OPENING_CONTRACT_BALANCES SET TotalAmount = Amount, PaidAmount = 0 WHERE TotalAmount = 0 AND PaidAmount = 0");
        }

        private static bool IsDetailedSubledgerCategory(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var n = name.Trim().ToLowerInvariant();
            return n.Contains("debitor")
                || n.Contains("kreditor")
                || n.Contains("mijoz")
                || n.Contains("xaridor")
                || n.Contains("haqq")
                || n.Contains("ta'minot")
                || n.Contains("ta'minotchi")
                || n.Contains("qarz")
                || n.Contains("oylik");
        }

        private static void UpsertDetailedOpeningSnapshots(
            DBTHSNEntities db,
            DateTime date,
            int userId,
            int defaultCurrencyId,
            List<OpeningContractBalanceRow> contractRows)
        {
            if (contractRows == null) return;

            var arTotal = contractRows.Where(x => x.BalanceType == 1).Sum(x => x.Amount);
            var apTotal = contractRows.Where(x => x.BalanceType == 2).Sum(x => x.Amount);
            var payrollTotal = contractRows.Where(x => x.BalanceType == 3).Sum(x => x.Amount);

            var categories = db.FIN_BALANCE_CATEGORIES
                .Where(c => c.IsActive)
                .ToList();

            Func<string, string> norm = s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
            int? ResolveCategoryId(params string[] keywords)
            {
                return categories
                    .Where(c =>
                    {
                        var n = norm(c.Name);
                        return keywords.Any(k => n.Contains(k));
                    })
                    .OrderBy(c => c.SortOrder)
                    .Select(c => (int?)c.ID)
                    .FirstOrDefault();
            }

            var arCategoryId = ResolveCategoryId("debitor", "xaridor", "mijoz", "haqq");
            var apCategoryId = ResolveCategoryId("kreditor", "ta'minot", "qarz");
            var payrollCategoryId = ResolveCategoryId("oylik", "ish haqi", "salary");

            void Upsert(int? categoryId, decimal amount)
            {
                if (!categoryId.HasValue || categoryId.Value <= 0) return;

                db.Database.ExecuteSqlCommand(
                    "DELETE FROM FIN_BALANCE_SNAPSHOTS WHERE CONVERT(date, SnapshotDate) = @p0 AND CategoryID = @p1 AND (SourceType = 'OpeningBalance' OR SourceType IS NULL OR SourceType LIKE 'OpeningBalance%')",
                    new SqlParameter("@p0", date.Date),
                    new SqlParameter("@p1", categoryId.Value));

                if (amount == 0m) return;

                db.FIN_BALANCE_SNAPSHOTS.Add(new FIN_BALANCE_SNAPSHOTS
                {
                    SnapshotDate = date.Date,
                    CategoryID = categoryId.Value,
                    Amount = amount,
                    CurrencyID = defaultCurrencyId > 0 ? defaultCurrencyId : 1,
                    SourceType = "OpeningBalanceDetailed",
                    SourceID = null,
                    CreatedByUserID = userId,
                    CreatedDT = DateTime.Now
                });
                db.SaveChanges();
            }

            Upsert(arCategoryId, arTotal);
            Upsert(apCategoryId, apTotal);
            Upsert(payrollCategoryId, payrollTotal);
        }

        private static void DeleteOpeningByDate(DBTHSNEntities db, DateTime date)
        {
            var d = date.Date;
            db.Database.ExecuteSqlCommand(
                "DELETE FROM FIN_OPENING_CONTRACT_BALANCES WHERE SnapshotDate = @p0",
                new SqlParameter("@p0", d));

            db.Database.ExecuteSqlCommand(
                "DELETE FROM FIN_BALANCE_SNAPSHOTS WHERE CONVERT(date, SnapshotDate) = @p0 AND (SourceType = 'OpeningBalance' OR SourceType IS NULL OR SourceType LIKE 'OpeningBalance%')",
                new SqlParameter("@p0", d));
        }

        private static decimal ParseDecimalFlexible(string raw, decimal fallback = 0m)
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

        private class OpeningContractBalanceRow
        {
            public int ID { get; set; }
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
            public DateTime CreatedDT { get; set; }
        }
    }
}
