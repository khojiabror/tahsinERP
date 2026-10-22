using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers.Logistic
{
    /// <summary>Ruxsat: [Permission] (GET — ko'rish, POST/Create/Edit/Delete — o'zgartirish; AJAX endpointlar ham, LOG-S02).</summary>
    // LOG-S03: POST entity binding faqat ruxsat etilgan maydonlar bilan ([Bind(Include)]) — over-posting yo'q
    [Permission]
    public class WaybillPaymentController : BaseController
    {
        private const string ControllerName = "WaybillPaymentController";

        // Olish so'rovi: WaybillPayment
        public ActionResult Index(int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.F_WAYBILL_PAYMENTS.AsNoTracking().Where(x => !x.IsDeleted);
                var paging = new LogisticsPageInfo(page, pageSize, query.Count());

                List<F_WAYBILL_PAYMENTS> list = query
                    .Include(x => x.FORWARDER)
                    .Include(x => x.CURRENCy)
                    .Include(x => x.F_WAYBILLS)
                    .OrderByDescending(x => x.PaymentDate ?? DateTime.MinValue)
                    .ThenByDescending(x => x.ID)
                    .Skip(paging.Skip)
                    .Take(paging.PageSize)
                    .ToList();

                var paymentIds = list.Select(x => x.ID).ToList();
                var splitsByPayment = WaybillLedgerService.GetSplitsByPayment(db,
                    db.F_WAYBILL_PAYMENTS.Where(p => paymentIds.Contains(p.ID)).Select(p => p.ID));

                // To'lov → bog'langan yo'l varaqalari (split bo'lmasa — asosiy WaybillID)
                var paymentWaybillIdsMap = list.ToDictionary(
                    p => p.ID,
                    p =>
                    {
                        var ids = splitsByPayment[p.ID].Select(s => s.WaybillID).Distinct().ToList();
                        if (ids.Count == 0 && p.WaybillID > 0) ids.Add(p.WaybillID);
                        return ids;
                    });

                var allWaybillIds = paymentWaybillIdsMap.SelectMany(x => x.Value).Distinct().ToList();
                var waybillInfo = db.F_WAYBILLS.AsNoTracking()
                    .Where(w => allWaybillIds.Contains(w.ID))
                    .Select(w => new { w.ID, w.WaybillNo, w.Amount })
                    .ToList()
                    .ToDictionary(x => x.ID);
                var paidLookup = WaybillLedgerService.GetPaidLookup(db, allWaybillIds);

                var linkedWaybillMap = new Dictionary<int, string>();
                var paymentWaybillAmountMap = new Dictionary<int, decimal>();
                var paymentOutstandingMap = new Dictionary<int, decimal>();
                foreach (var kv in paymentWaybillIdsMap)
                {
                    var known = kv.Value.Where(waybillInfo.ContainsKey).ToList();
                    linkedWaybillMap[kv.Key] = string.Join(", ", known
                        .Select(id => waybillInfo[id].WaybillNo)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct());
                    paymentWaybillAmountMap[kv.Key] = known.Sum(id => waybillInfo[id].Amount ?? 0);
                    paymentOutstandingMap[kv.Key] = known.Sum(id =>
                        (waybillInfo[id].Amount ?? 0) - WaybillLedgerService.PaidOf(paidLookup, id));
                }

                ViewBag.LinkedWaybillMap = linkedWaybillMap;
                ViewBag.PaymentWaybillAmountMap = paymentWaybillAmountMap;
                ViewBag.PaymentOutstandingMap = paymentOutstandingMap;
                ViewBag.Paging = paging;
                return View(list);
            }
        }

        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PopulateFormBags(db);
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "ForwarderID,WaybillID,Payment_Amount,PaymentDate,Comment,CurrencyID")] F_WAYBILL_PAYMENTS model, int? AccountID, int[] SelectedWaybillIDs)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillIds = ResolveWaybillIds(model.WaybillID, SelectedWaybillIDs);
                model.WaybillID = waybillIds.FirstOrDefault();
                ValidatePayment(db, model, waybillIds);

                if (!ModelState.IsValid)
                {
                    ModelState.AddModelError("", L.T("Log_CheckInputRetry"));
                    PopulateFormBags(db, model.WaybillID, waybillIds, model.ForwarderID, model.CurrencyID, AccountID);
                    return View(model);
                }

                if (!model.PaymentDate.HasValue)
                {
                    model.PaymentDate = DateTime.Now;
                }
                model.IsDeleted = false;

                // To'lov + split'lar + moliya yozuvi — bitta tranzaksiya (LOG-B08)
                bool posted;
                using (var tx = db.Database.BeginTransaction())
                {
                    try
                    {
                        db.F_WAYBILL_PAYMENTS.Add(model);
                        db.SaveChanges();

                        // LOG-B01: yangi to'lovning o'zi taqsimotda "to'langan" deb hisoblanmaydi
                        WaybillLedgerService.ReplacePaymentSplits(db, model.ID, waybillIds, model.Payment_Amount, model.CurrencyID, model.Comment, model.PaymentDate);
                        db.SaveChanges();

                        posted = FinancePostingService.PostWaybillPayment(model.ID, AccountID, db, CurrentUserId);
                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Create", $"Xatolik (to'lov saqlanmadi): {ex.Message}");
                        DetachAll(db);
                        model.ID = 0;
                        ModelState.AddModelError("", L.T("Log_PaySaveError"));
                        PopulateFormBags(db, model.WaybillID, waybillIds, model.ForwarderID, model.CurrencyID, AccountID);
                        return View(model);
                    }
                }

                WarnIfNotPosted(posted);
                WarnIfOverpaid(db, waybillIds);

                var waybillNo = db.F_WAYBILLS.Where(w => w.ID == model.WaybillID).Select(w => w.WaybillNo).FirstOrDefault();
                LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Create",
                    $"{waybillNo} - raqamli yo'l varaqasiga to'lov kiritildi.");

                return RedirectToAction("Index");
            }
        }

        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS.AsNoTracking()
                            .Include(x => x.F_WAYBILLS)
                            .Include(x => x.CURRENCy)
                            .Include(x => x.FORWARDER)
                            .FirstOrDefault(x => x.ID == id && !x.IsDeleted);
                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }

                var linkedWaybillIds = WaybillLedgerService.GetPaymentWaybillIds(db, waybillPayment.ID, waybillPayment.WaybillID);
                var linkSplits = db.F_WAYBILL_PAYMENT_SPLITS.AsNoTracking()
                    .Where(s => s.PayerType == WaybillLedgerService.SplitPayerType && s.PayerID == waybillPayment.ID)
                    .ToList();

                var linkedWaybills = db.F_WAYBILLS.AsNoTracking()
                    .Include(w => w.CURRENCy)
                    .Where(w => linkedWaybillIds.Contains(w.ID))
                    .ToList();
                var paidLookup = WaybillLedgerService.GetPaidLookup(db, linkedWaybillIds);

                var linkedRows = linkedWaybills.Select(w =>
                {
                    var splitAmount = linkSplits.Where(s => s.WaybillID == w.ID).Sum(s => (decimal?)s.Amount) ?? 0;
                    var allocated = splitAmount > 0
                        ? splitAmount
                        : (w.ID == waybillPayment.WaybillID ? waybillPayment.Payment_Amount : 0);

                    var totalPaid = WaybillLedgerService.PaidOf(paidLookup, w.ID);
                    return new WaybillPaymentLinkedWaybillViewModel
                    {
                        WaybillID = w.ID,
                        WaybillNo = w.WaybillNo,
                        Amount = w.Amount ?? 0,
                        Paid = totalPaid,
                        Outstanding = (w.Amount ?? 0) - totalPaid,
                        Allocated = allocated,
                        Currency = w.CURRENCy != null ? w.CURRENCy.Shortname : (waybillPayment.CURRENCy != null ? waybillPayment.CURRENCy.Shortname : "")
                    };
                }).OrderBy(x => x.WaybillNo).ToList();

                ViewBag.LinkedWaybills = linkedRows;
                ViewBag.TotalPaid = linkedRows.Sum(x => x.Paid);
                ViewBag.Outstanding = linkedRows.Sum(x => x.Outstanding);
                ViewBag.WaybillAmount = linkedRows.Sum(x => x.Amount);

                var finTransactionId = FindFinTransaction(db, waybillPayment.ID)?.ID;
                ViewBag.HasFinTransaction = finTransactionId.HasValue;
                ViewBag.FinTransactionID = finTransactionId;

                return View(waybillPayment);
            }
        }

        public ActionResult Delete(int? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = LoadForDisplay(db, Id.Value);
                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }
                return View(waybillPayment);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public ActionResult DeleteConfirmed(int? ID)
        {
            if (ID is null)
            {
                return HttpNotFound();
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPaymentToDelete = db.F_WAYBILL_PAYMENTS.Include(x => x.F_WAYBILLS).FirstOrDefault(x => x.ID == ID && !x.IsDeleted);
                if (waybillPaymentToDelete == null)
                {
                    return HttpNotFound();
                }

                var waybillNo = waybillPaymentToDelete.F_WAYBILLS?.WaybillNo ?? "Noma'lum";

                // To'lov, split'lar va moliya yozuvi birga bekor qilinadi (LOG-B08)
                using (var tx = db.Database.BeginTransaction())
                {
                    try
                    {
                        waybillPaymentToDelete.IsDeleted = true;
                        WaybillLedgerService.RemovePaymentSplits(db, waybillPaymentToDelete.ID);

                        var finTransaction = FindFinTransaction(db, waybillPaymentToDelete.ID);
                        if (finTransaction != null)
                        {
                            finTransaction.IsDeleted = true;

                            // Bog'liq yozuvlardagi havolani uzamiz (sinxron: async helperni .GetResult() bilan
                            // chaqirish ASP.NET sync-context deadlock'iga olib keladi).
                            var finTxnId = finTransaction.ID;
                            foreach (var row in db.SHAREHOLDER_CONTRIBUTIONS.Where(x => x.FinTransactionID == finTxnId).ToList())
                                row.FinTransactionID = null;
                        }

                        db.SaveChanges();
                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Delete", $"Xatolik: {ex.Message}");
                        DetachAll(db);
                        ModelState.AddModelError("", L.T("Log_PayDeleteError"));
                        // LOG-B09: view null model bilan ochilmasin
                        return View(LoadForDisplay(db, ID.Value));
                    }
                }

                LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Delete",
                    $"{waybillNo} - raqamli yo'l varaqasi to'lovi o'chirildi.");

                return RedirectToAction("Index");
            }
        }

        public ActionResult Edit(int? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = LoadForDisplay(db, Id.Value);
                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }

                var selectedWaybillIds = WaybillLedgerService.GetPaymentWaybillIds(db, waybillPayment.ID, waybillPayment.WaybillID);
                var finTxn = FindFinTransaction(db, waybillPayment.ID);
                PopulateFormBags(
                    db,
                    waybillPayment.WaybillID,
                    selectedWaybillIds,
                    waybillPayment.ForwarderID,
                    waybillPayment.CurrencyID,
                    finTxn != null ? (int?)finTxn.AccountID : null);

                return View(waybillPayment);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ID,ForwarderID,WaybillID,Payment_Amount,PaymentDate,Comment,CurrencyID")] F_WAYBILL_PAYMENTS waybillPayment, int? AccountID, int[] SelectedWaybillIDs)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillIds = ResolveWaybillIds(waybillPayment.WaybillID, SelectedWaybillIDs);
                waybillPayment.WaybillID = waybillIds.FirstOrDefault();
                ValidatePayment(db, waybillPayment, waybillIds);

                var waybillPaymentToUpdate = ModelState.IsValid
                    ? db.F_WAYBILL_PAYMENTS.FirstOrDefault(x => x.ID == waybillPayment.ID && !x.IsDeleted)
                    : null;
                if (ModelState.IsValid && waybillPaymentToUpdate == null)
                {
                    ModelState.AddModelError("", L.T("Log_PaymentNotFound"));
                }

                if (!ModelState.IsValid)
                {
                    ModelState.AddModelError("", L.T("Log_CheckInputRetry"));
                    PopulateFormBags(db, waybillPayment.WaybillID, waybillIds, waybillPayment.ForwarderID, waybillPayment.CurrencyID, AccountID);
                    return View(waybillPayment);
                }

                bool posted = true; // avvalgi moliya yozuvi yangilanadi yoki quyida yangisi yoziladi
                using (var tx = db.Database.BeginTransaction())
                {
                    try
                    {
                        waybillPaymentToUpdate.ForwarderID = waybillPayment.ForwarderID;
                        waybillPaymentToUpdate.WaybillID = waybillPayment.WaybillID;
                        waybillPaymentToUpdate.Payment_Amount = waybillPayment.Payment_Amount;
                        waybillPaymentToUpdate.PaymentDate = waybillPayment.PaymentDate ?? DateTime.Now;
                        waybillPaymentToUpdate.Comment = waybillPayment.Comment;
                        waybillPaymentToUpdate.CurrencyID = waybillPayment.CurrencyID;

                        WaybillLedgerService.ReplacePaymentSplits(db, waybillPaymentToUpdate.ID, waybillIds,
                            waybillPaymentToUpdate.Payment_Amount, waybillPaymentToUpdate.CurrencyID, waybillPaymentToUpdate.Comment,
                            waybillPaymentToUpdate.PaymentDate);

                        // Bog'liq chiqim tranzaksiyasi summa/sana bilan bir xil bo'lib qolsin
                        var finTransaction = FindFinTransaction(db, waybillPaymentToUpdate.ID);
                        if (finTransaction != null)
                        {
                            finTransaction.Amount = waybillPaymentToUpdate.Payment_Amount;
                            finTransaction.TxnDate = waybillPaymentToUpdate.PaymentDate.Value;
                            if (AccountID.HasValue)
                                finTransaction.AccountID = AccountID.Value;
                            finTransaction.CounterpartyID = waybillPaymentToUpdate.ForwarderID;
                            finTransaction.CounterpartyName = db.FORWARDERS.Where(f => f.ID == waybillPaymentToUpdate.ForwarderID).Select(f => f.ForwarderName).FirstOrDefault();
                            finTransaction.CurrencyID = waybillPaymentToUpdate.CurrencyID ?? finTransaction.CurrencyID;
                        }
                        db.SaveChanges();

                        if (finTransaction == null)
                        {
                            // Avval moliyaga o'tmagan bo'lsa — hozir o'tkazamiz
                            posted = FinancePostingService.PostWaybillPayment(waybillPaymentToUpdate.ID, AccountID, db, CurrentUserId);
                        }
                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Edit", $"Xatolik (o'zgarishlar saqlanmadi): {ex.Message}");
                        DetachAll(db);
                        ModelState.AddModelError("", L.T("Log_PayEditSaveError"));
                        PopulateFormBags(db, waybillPayment.WaybillID, waybillIds, waybillPayment.ForwarderID, waybillPayment.CurrencyID, AccountID);
                        return View(waybillPayment);
                    }
                }

                WarnIfNotPosted(posted);
                WarnIfOverpaid(db, waybillIds);

                var waybillNo = db.F_WAYBILLS.Where(w => w.ID == waybillPayment.WaybillID).Select(w => w.WaybillNo).FirstOrDefault();
                LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Edit",
                    $"{waybillNo} - raqamli yo'l varaqasi to'lovi tahrirlandi.");

                return RedirectToAction("Index");
            }
        }

        // ------------------------------------------------------------------ yordamchilar

        /// <summary>LOG-B20: summa musbat, tanlangan yo'l varaqalari mavjud va shu yuk tashuvchiniki.</summary>
        private void ValidatePayment(DBTHSNEntities db, F_WAYBILL_PAYMENTS model, List<int> waybillIds)
        {
            if (!waybillIds.Any())
            {
                ModelState.AddModelError("WaybillID", L.T("Log_SelectAtLeastOneWaybill"));
                return;
            }

            if (model.Payment_Amount <= 0)
            {
                ModelState.AddModelError("Payment_Amount", L.T("Log_PaymentAmountPositive"));
            }

            var waybills = db.F_WAYBILLS.AsNoTracking()
                .Where(w => waybillIds.Contains(w.ID) && w.IsDeleted != true)
                .Select(w => new { w.ID, w.WaybillNo, w.ForwarderID, w.CurrencyID })
                .ToList();
            if (waybills.Count != waybillIds.Count)
            {
                ModelState.AddModelError("WaybillID", L.T("Log_SelectedWaybillMissing"));
            }

            // LOG-B13: bitta to'lovning yo'l varaqalari bitta valyutada (qoldiq va jami shu valyutada ko'rsatiladi);
            // to'lov boshqa valyutada bo'lsa — to'lov sanasidagi kurs bo'lishi shart (taqsimot kurs bilan)
            var waybillCurrencies = waybills.Where(w => w.CurrencyID.HasValue).Select(w => w.CurrencyID.Value).Distinct().ToList();
            if (waybillCurrencies.Count > 1)
            {
                ModelState.AddModelError("WaybillID", L.T("Log_MixedCurrencyWaybills"));
            }
            else if (waybillCurrencies.Count == 1 && model.CurrencyID.HasValue && model.CurrencyID != waybillCurrencies[0])
            {
                var date = (model.PaymentDate ?? DateTime.Today).Date;
                if (!new LogisticsMoney(db).Convert(1, model.CurrencyID, waybillCurrencies[0], date).HasValue)
                {
                    ModelState.AddModelError("CurrencyID",
                        L.T("Log_RateNotFoundForDate", date.ToString("dd.MM.yyyy"),
                            ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, model.CurrencyID),
                            ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, waybillCurrencies[0])));
                }
            }

            var foreign = waybills
                .Where(w => w.ForwarderID.HasValue && model.ForwarderID > 0 && w.ForwarderID.Value != model.ForwarderID)
                .Select(w => w.WaybillNo)
                .ToList();
            if (foreign.Any())
            {
                ModelState.AddModelError("ForwarderID",
                    L.T("Log_WaybillsOtherForwarder", string.Join(", ", foreign)));
            }
        }

        private void WarnIfNotPosted(bool posted)
        {
            if (!posted)
            {
                // Foydalanuvchi qarori: bank hisobi topilmasa to'lov saqlanadi, lekin ogohlantiriladi
                TempData["Warning"] = L.T("Log_PayNotPostedNoAccount");
            }
        }

        private void WarnIfOverpaid(DBTHSNEntities db, List<int> waybillIds)
        {
            var amounts = db.F_WAYBILLS.AsNoTracking()
                .Where(w => waybillIds.Contains(w.ID))
                .Select(w => new { w.ID, w.WaybillNo, w.Amount })
                .ToList();
            var paid = WaybillLedgerService.GetPaidLookup(db, waybillIds);
            var overpaid = amounts
                .Where(w => WaybillLedgerService.PaidOf(paid, w.ID) - (w.Amount ?? 0) > 0.005m)
                .Select(w => w.WaybillNo)
                .ToList();
            if (overpaid.Any())
            {
                var text = L.T("Log_Overpaid", string.Join(", ", overpaid));
                TempData["Warning"] = TempData["Warning"] == null ? text : TempData["Warning"] + " " + text;
            }
        }

        private static FIN_TRANSACTIONS FindFinTransaction(DBTHSNEntities db, int paymentId)
        {
            return db.FIN_TRANSACTIONS.FirstOrDefault(t => t.SourceDocType == WaybillLedgerService.FinSourceDocType
                                                         && t.SourceDocID == paymentId && t.IsDeleted != true);
        }

        private static F_WAYBILL_PAYMENTS LoadForDisplay(DBTHSNEntities db, int id)
        {
            return db.F_WAYBILL_PAYMENTS.AsNoTracking()
                .Include(x => x.FORWARDER)
                .Include(x => x.F_WAYBILLS)
                .Include(x => x.CURRENCy)
                .FirstOrDefault(x => x.ID == id && !x.IsDeleted);
        }

        /// <summary>Rollback'dan keyin kontekstda qolgan (saqlanmagan) o'zgarishlarni tashlab yuborish.</summary>
        private static void DetachAll(DBTHSNEntities db)
        {
            foreach (var entry in db.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;
        }

        private void PopulateFormBags(
            DBTHSNEntities db,
            int? selectedWaybillId = null,
            IEnumerable<int> selectedWaybillIds = null,
            int? selectedForwarderId = null,
            int? selectedCurrencyId = null,
            int? selectedAccountId = null)
        {
            var selectedSet = new HashSet<int>((selectedWaybillIds ?? Enumerable.Empty<int>()).Distinct());
            if (selectedWaybillId.HasValue && selectedWaybillId.Value > 0)
            {
                selectedSet.Add(selectedWaybillId.Value);
            }

            // LOG-P10: faqat ID va raqam (avval butun F_WAYBILLS qatorlari yuklanardi)
            var waybills = db.F_WAYBILLS.AsNoTracking()
                .Where(x => x.IsDeleted != true)
                .OrderByDescending(x => x.IssueDate)
                .ThenBy(x => x.WaybillNo)
                .Select(x => new { x.ID, x.WaybillNo })
                .ToList();

            ViewBag.waybillItems = waybills.Select(w => new SelectListItem
            {
                Value = w.ID.ToString(),
                Text = w.WaybillNo,
                Selected = selectedSet.Contains(w.ID)
            }).ToList();

            ViewBag.forwarders = ReferenceDataCache.SelectList(ReferenceDataCache.Forwarders, selectedForwarderId);
            ViewBag.currencies = ReferenceDataCache.SelectList(ReferenceDataCache.Currencies, selectedCurrencyId);

            // Faqat hisob-raqam (bank) hisoblari
            var accounts = db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsBank && a.IsActive).OrderBy(a => a.Name)
                .Select(a => new { a.ID, a.Name }).ToList();
            ViewBag.accounts = new SelectList(accounts, "ID", "Name", selectedAccountId);
        }

        private static List<int> ResolveWaybillIds(int fallbackWaybillId, int[] selectedWaybillIDs)
        {
            var ids = (selectedWaybillIDs ?? new int[0]).Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0 && fallbackWaybillId > 0)
            {
                ids.Add(fallbackWaybillId);
            }
            return ids;
        }
    }
}
