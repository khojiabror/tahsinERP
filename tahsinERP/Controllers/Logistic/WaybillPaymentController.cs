using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers.Logistic
{
    public class WaybillPaymentController : Controller
    {
        private string ControllerName = "WaybillPaymentController";
        private const byte WaybillPaymentSplitPayerType = 99;
        // Olish so'rovi: WaybillPayment
        public ActionResult Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                List<F_WAYBILL_PAYMENTS> list = db.F_WAYBILL_PAYMENTS
                    .Include(x => x.FORWARDER)
                    .Include(x => x.CURRENCy)
                    .Include(x => x.F_WAYBILLS)
                    .Where(x => x.IsDeleted == false)
                    .OrderByDescending(x => x.PaymentDate ?? DateTime.MinValue)
                    .ToList();

                var paymentIds = list.Select(x => x.ID).ToList();
                var splitRows = db.F_WAYBILL_PAYMENT_SPLITS
                    .Where(s => s.PayerType == WaybillPaymentSplitPayerType && s.PayerID.HasValue && paymentIds.Contains(s.PayerID.Value))
                    .ToList();

                var splitWaybillIds = splitRows.Select(s => s.WaybillID).Distinct().ToList();
                var splitWaybills = db.F_WAYBILLS
                    .Where(w => splitWaybillIds.Contains(w.ID))
                    .ToDictionary(w => w.ID, w => w.WaybillNo);

                var linkedWaybillMap = new Dictionary<int, string>();
                foreach (var item in list)
                {
                    var ids = splitRows
                        .Where(s => s.PayerID == item.ID)
                        .Select(s => s.WaybillID)
                        .Distinct()
                        .ToList();

                    if (!ids.Any() && item.WaybillID > 0)
                    {
                        ids.Add(item.WaybillID);
                    }

                    var waybillNos = ids
                        .Select(id => splitWaybills.ContainsKey(id)
                            ? splitWaybills[id]
                            : (id == item.WaybillID ? item.F_WAYBILLS?.WaybillNo : null))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct()
                        .ToList();

                    linkedWaybillMap[item.ID] = string.Join(", ", waybillNos);
                }

                ViewBag.LinkedWaybillMap = linkedWaybillMap;
                return View(list);
            }
        }
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PopulateFormBags(db);
                return View();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(F_WAYBILL_PAYMENTS model, int? AccountID, int[] SelectedWaybillIDs)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillIds = ResolveWaybillIds(model.WaybillID, SelectedWaybillIDs);
                if (!waybillIds.Any())
                {
                    ModelState.AddModelError("WaybillID", "Kamida bitta yo'l varaqasi tanlang.");
                }

                model.WaybillID = waybillIds.FirstOrDefault();

                if (ModelState.IsValid)
                {
                    // Belgilash PaymentDate agar not provided
                    if (!model.PaymentDate.HasValue)
                    {
                        model.PaymentDate = DateTime.Now;
                    }

                    model.IsDeleted = false;
                    db.F_WAYBILL_PAYMENTS.Add(model);
                    try
                    {
                        db.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Ma'lumotni saqlashda xatolik yuz berdi. Qayta urinib ko'ring.");
                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Create", $"Xatolik: {ex.Message}");
                        PopulateFormBags(db, model.WaybillID, waybillIds, model.ForwarderID, model.CurrencyID, AccountID);
                        return View(model);
                    }

                    // Bitta ga'lovga bir nechta yo'l varaqasi biriktirish (split)
                    SavePaymentSplits(db, model.ID, waybillIds, model.Payment_Amount, model.CurrencyID, model.Comment);

                    // Moliya bo'limiga o'tkazish
                    try
                    {
                        var userId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                        FinancePostingService.PostWaybillPayment(model.ID, AccountID, db, userId);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Create",
                            $"{LogisticsTerms.Waybill} to'lovi bo'yicha moliya o'tkazmasida xatolik (ID: {model.ID}): {ex.Message}");
                    }

                    var waybill = db.F_WAYBILLS.Find(model.WaybillID);
                    LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Create",
                        $"{waybill?.WaybillNo} - raqamli {LogisticsTerms.Waybill.ToLower()}ga to'lov kiritildi.");

                    return RedirectToAction("Index");
                }
                PopulateFormBags(db, model.WaybillID, waybillIds, model.ForwarderID, model.CurrencyID, AccountID);

                ModelState.AddModelError("", "Ma'lumotlarni to'liq kiritganingizga ishonch hosil qilib qaytadan urinib ko'ring.");
                return View(model);
            }
        }
        public ActionResult Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS
                            .Include(x => x.F_WAYBILLS)
                            .Include(x => x.CURRENCy)
                            .Include(x => x.FORWARDER)
                            .Where(x => x.ID == id && x.IsDeleted == false)
                            .FirstOrDefault();
                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }

                var linkedWaybillIds = GetPaymentWaybillIds(db, waybillPayment.ID, waybillPayment.WaybillID);
                var linkSplits = db.F_WAYBILL_PAYMENT_SPLITS
                    .Where(s => s.PayerType == WaybillPaymentSplitPayerType && s.PayerID == waybillPayment.ID)
                    .ToList();

                var linkedWaybills = db.F_WAYBILLS
                    .Include(w => w.CURRENCy)
                    .Where(w => linkedWaybillIds.Contains(w.ID))
                    .ToList();

                var linkedRows = linkedWaybills.Select(w =>
                {
                    var splitAmount = linkSplits.Where(s => s.WaybillID == w.ID).Sum(s => (double?)s.Amount) ?? 0;
                    var allocated = splitAmount > 0
                        ? splitAmount
                        : (w.ID == waybillPayment.WaybillID ? waybillPayment.Payment_Amount : 0);

                    var totalPaid = GetWaybillPaidTotal(db, w.ID);
                    var outstanding = (w.Amount ?? 0) - totalPaid;

                    return new
                    {
                        WaybillID = w.ID,
                        WaybillNo = w.WaybillNo,
                        Amount = w.Amount ?? 0,
                        Paid = totalPaid,
                        Outstanding = outstanding,
                        Allocated = allocated,
                        Currency = w.CURRENCy != null ? w.CURRENCy.Shortname : (waybillPayment.CURRENCy != null ? waybillPayment.CURRENCy.Shortname : "")
                    };
                }).OrderBy(x => x.WaybillNo).ToList();

                ViewBag.LinkedWaybills = linkedRows;
                ViewBag.TotalPaid = linkedRows.Sum(x => x.Paid);
                ViewBag.Outstanding = linkedRows.Sum(x => x.Outstanding);
                ViewBag.WaybillAmount = linkedRows.Sum(x => x.Amount);

                var finTransaction = db.FIN_TRANSACTIONS
                    .FirstOrDefault(t => t.SourceDocType == "WaybillPayment" && t.SourceDocID == id && t.IsDeleted != true);
                ViewBag.HasFinTransaction = finTransaction != null;
                ViewBag.FinTransactionID = finTransaction?.ID;

                return View(waybillPayment);
            }
        }
        public ActionResult Delete(int? Id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS
                            .Include(x => x.FORWARDER)
                            .Include(x => x.F_WAYBILLS)
                            .Include(x => x.CURRENCy)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();
                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }
                return View(waybillPayment);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int? ID, FormCollection nimadir)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ID is null)
            {
                return HttpNotFound();
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPaymentToDelete = db.F_WAYBILL_PAYMENTS.Find(ID);
                if (waybillPaymentToDelete != null)
                {
                    var waybillNo = waybillPaymentToDelete.F_WAYBILLS?.WaybillNo ?? "Noma'lum";
                    waybillPaymentToDelete.IsDeleted = true;
                    db.SaveChanges();

                    var splits = db.F_WAYBILL_PAYMENT_SPLITS
                        .Where(s => s.PayerType == WaybillPaymentSplitPayerType && s.PayerID == ID)
                        .ToList();
                    if (splits.Any())
                    {
                        db.F_WAYBILL_PAYMENT_SPLITS.RemoveRange(splits);
                        db.SaveChanges();
                    }

                    // Bog'liq moliyaviy tranzaksiyani ham bekor qilish
                    var finTransaction = db.FIN_TRANSACTIONS
                        .FirstOrDefault(t => t.SourceDocType == "WaybillPayment" && t.SourceDocID == ID);
                    if (finTransaction != null)
                    {
                        finTransaction.IsDeleted = true;
                        db.SaveChanges();
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Delete",
                        $"{waybillNo} - raqamli {LogisticsTerms.Waybill.ToLower()} to'lovi o'chirildi.");

                    return RedirectToAction("Index");
                }
                return View(waybillPaymentToDelete);
            }
        }
        public ActionResult Edit(int? Id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS
                            .Include(x => x.FORWARDER)
                            .Include(x => x.F_WAYBILLS)
                            .Include(x => x.CURRENCy)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();

                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }

                var selectedWaybillIds = GetPaymentWaybillIds(db, waybillPayment.ID, waybillPayment.WaybillID);
                var finTxn = db.FIN_TRANSACTIONS.FirstOrDefault(t => t.SourceDocType == "WaybillPayment" && t.SourceDocID == Id && t.IsDeleted != true);
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
        public ActionResult Edit(F_WAYBILL_PAYMENTS waybillPayment, int? AccountID, int[] SelectedWaybillIDs)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillIds = ResolveWaybillIds(waybillPayment.WaybillID, SelectedWaybillIDs);
                if (!waybillIds.Any())
                {
                    ModelState.AddModelError("WaybillID", "Kamida bitta yo'l varaqasi tanlang.");
                }
                waybillPayment.WaybillID = waybillIds.FirstOrDefault();

                if (ModelState.IsValid)
                {
                    var waybillPaymentToUpdate = db.F_WAYBILL_PAYMENTS.Find(waybillPayment.ID);
                    if (waybillPaymentToUpdate != null)
                    {
                        waybillPaymentToUpdate.ForwarderID = waybillPayment.ForwarderID;
                        waybillPaymentToUpdate.WaybillID = waybillPayment.WaybillID;
                        waybillPaymentToUpdate.Payment_Amount = waybillPayment.Payment_Amount;
                        waybillPaymentToUpdate.PaymentDate = waybillPayment.PaymentDate ?? DateTime.Now;
                        waybillPaymentToUpdate.Comment = waybillPayment.Comment;
                        waybillPaymentToUpdate.CurrencyID = waybillPayment.CurrencyID;

                        try
                        {
                            db.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError("", "Ma'lumotni saqlashda xatolik yuz berdi. Qayta urinib ko'ring.");
                            LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Edit", $"Xatolik: {ex.Message}");
                            PopulateFormBags(db, waybillPayment.WaybillID, waybillIds, waybillPayment.ForwarderID, waybillPayment.CurrencyID, AccountID);
                            return View(waybillPayment);
                        }

                        SavePaymentSplits(
                            db,
                            waybillPayment.ID,
                            waybillIds,
                            waybillPayment.Payment_Amount,
                            waybillPayment.CurrencyID,
                            waybillPayment.Comment,
                            waybillPayment.ID);

                        // Bog'liq chiqim tranzaksiyasi summa/sana bilan bir xil bo'lib qolsin
                        var finTransaction = db.FIN_TRANSACTIONS
                            .FirstOrDefault(t => t.SourceDocType == "WaybillPayment" && t.SourceDocID == waybillPayment.ID);
                        if (finTransaction != null)
                        {
                            try
                            {
                                finTransaction.Amount = (decimal)waybillPayment.Payment_Amount;
                                finTransaction.TxnDate = waybillPayment.PaymentDate ?? DateTime.Now;
                                if (AccountID.HasValue)
                                    finTransaction.AccountID = AccountID.Value;
                                finTransaction.CounterpartyID = waybillPayment.ForwarderID;
                                finTransaction.CounterpartyName = db.FORWARDERS.Where(f => f.ID == waybillPayment.ForwarderID).Select(f => f.ForwarderName).FirstOrDefault();
                                finTransaction.CurrencyID = waybillPayment.CurrencyID ?? finTransaction.CurrencyID;
                                db.SaveChanges();
                            }
                            catch (Exception ex)
                            {
                                LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Edit", $"Moliya tranzaksiyasini yangilashda xatolik: {ex.Message}");
                            }
                        }

                        var waybill = db.F_WAYBILLS.Find(waybillPayment.WaybillID);
                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Edit",
                            $"{waybill?.WaybillNo} - raqamli {LogisticsTerms.Waybill.ToLower()} to'lovi tahrirlandi.");

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ModelState.AddModelError("", "To'lov topilmadi.");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Ma'lumotlarni to'liq kiritganingizga ishonch hosil qilib qaytadan urinib ko'ring.");
                }
                PopulateFormBags(db, waybillPayment.WaybillID, waybillIds, waybillPayment.ForwarderID, waybillPayment.CurrencyID, AccountID);
                return View(waybillPayment);
            }
        }

        private void PopulateFormBags(
            DBTHSNEntities db,
            int? selectedWaybillId = null,
            IEnumerable<int> selectedWaybillIds = null,
            int? selectedForwarderId = null,
            int? selectedCurrencyId = null,
            int? selectedAccountId = null)
        {
            var waybills = db.F_WAYBILLS
                .Where(x => x.IsDeleted == false)
                .OrderByDescending(x => x.IssueDate)
                .ThenBy(x => x.WaybillNo)
                .ToList();

            var selectedSet = new HashSet<int>((selectedWaybillIds ?? Enumerable.Empty<int>()).Distinct());
            if (selectedWaybillId.HasValue && selectedWaybillId.Value > 0)
            {
                selectedSet.Add(selectedWaybillId.Value);
            }

            ViewBag.waybills = new SelectList(waybills, "ID", "WaybillNo", selectedWaybillId);
            ViewBag.waybillItems = waybills.Select(w => new SelectListItem
            {
                Value = w.ID.ToString(),
                Text = w.WaybillNo,
                Selected = selectedSet.Contains(w.ID)
            }).ToList();

            ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName", selectedForwarderId);
            ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", selectedCurrencyId);

            // Faqat hisob-raqam (bank) hisoblari
            var accounts = db.FIN_ACCOUNTS.Where(a => a.IsBank && a.IsActive).OrderBy(a => a.Name).ToList();
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

        private List<int> GetPaymentWaybillIds(DBTHSNEntities db, int paymentId, int fallbackWaybillId)
        {
            var splitWaybillIds = db.F_WAYBILL_PAYMENT_SPLITS
                .Where(s => s.PayerType == WaybillPaymentSplitPayerType && s.PayerID == paymentId)
                .Select(s => s.WaybillID)
                .Distinct()
                .ToList();

            if (splitWaybillIds.Count == 0 && fallbackWaybillId > 0)
            {
                splitWaybillIds.Add(fallbackWaybillId);
            }

            return splitWaybillIds;
        }

        private double GetWaybillPaidTotal(DBTHSNEntities db, int waybillId, int? excludePaymentId = null)
        {
            var directTotal = db.F_WAYBILL_PAYMENTS
                .Where(p =>
                    p.WaybillID == waybillId &&
                    p.IsDeleted == false &&
                    (!excludePaymentId.HasValue || p.ID != excludePaymentId.Value) &&
                    !db.F_WAYBILL_PAYMENT_SPLITS.Any(s => s.PayerType == WaybillPaymentSplitPayerType && s.PayerID == p.ID))
                .Sum(p => (double?)p.Payment_Amount) ?? 0;

            var splitTotal = (from s in db.F_WAYBILL_PAYMENT_SPLITS
                              join p in db.F_WAYBILL_PAYMENTS on s.PayerID equals p.ID
                              where s.PayerType == WaybillPaymentSplitPayerType
                                    && s.WaybillID == waybillId
                                    && p.IsDeleted == false
                                    && (!excludePaymentId.HasValue || p.ID != excludePaymentId.Value)
                              select (double?)s.Amount).Sum() ?? 0;

            return directTotal + splitTotal;
        }

        private Dictionary<int, double> AllocateAcrossWaybills(DBTHSNEntities db, List<int> waybillIds, double paymentAmount, int? excludePaymentId = null)
        {
            var result = new Dictionary<int, double>();
            if (waybillIds == null || waybillIds.Count == 0 || paymentAmount <= 0)
            {
                return result;
            }

            var waybills = db.F_WAYBILLS
                .Where(w => waybillIds.Contains(w.ID))
                .ToDictionary(w => w.ID, w => w);

            var remaining = paymentAmount;

            // Avval qoldig'i bor yo'l varaqalariga taqsimlaymiz
            foreach (var waybillId in waybillIds)
            {
                if (!waybills.ContainsKey(waybillId)) continue;

                var bill = waybills[waybillId];
                var alreadyPaid = GetWaybillPaidTotal(db, waybillId, excludePaymentId);
                var outstanding = Math.Max(0, (bill.Amount ?? 0) - alreadyPaid);

                if (outstanding <= 0 || remaining <= 0) continue;

                var allocated = Math.Min(outstanding, remaining);
                result[waybillId] = allocated;
                remaining -= allocated;
            }

            if (remaining <= 0)
            {
                return result;
            }

            // Agar ortiqcha summa qolsa, tanlangan waybilllarga teng taqsimlaymiz
            var baseIds = waybillIds.Where(id => waybills.ContainsKey(id)).ToList();
            if (baseIds.Count == 0) return result;

            var per = remaining / baseIds.Count;
            foreach (var id in baseIds)
            {
                if (!result.ContainsKey(id)) result[id] = 0;
                result[id] += per;
            }

            return result;
        }

        private void SavePaymentSplits(
            DBTHSNEntities db,
            int paymentId,
            List<int> waybillIds,
            double paymentAmount,
            int? currencyId,
            string comment,
            int? excludePaymentIdForOutstanding = null)
        {
            var oldSplits = db.F_WAYBILL_PAYMENT_SPLITS
                .Where(s => s.PayerType == WaybillPaymentSplitPayerType && s.PayerID == paymentId)
                .ToList();
            if (oldSplits.Any())
            {
                db.F_WAYBILL_PAYMENT_SPLITS.RemoveRange(oldSplits);
                db.SaveChanges();
            }

            var alloc = AllocateAcrossWaybills(db, waybillIds, paymentAmount, excludePaymentIdForOutstanding);

            foreach (var kv in alloc.Where(x => x.Value > 0))
            {
                db.F_WAYBILL_PAYMENT_SPLITS.Add(new F_WAYBILL_PAYMENT_SPLITS
                {
                    WaybillID = kv.Key,
                    PayerType = WaybillPaymentSplitPayerType,
                    PayerID = paymentId,
                    Amount = kv.Value,
                    CurrencyID = currencyId,
                    Comment = string.IsNullOrWhiteSpace(comment) ? "WaybillPaymentLink" : comment
                });
            }

            db.SaveChanges();
        }
    }
}
