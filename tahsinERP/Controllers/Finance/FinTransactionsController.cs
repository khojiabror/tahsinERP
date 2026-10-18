using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.Finance;
using tahsinERP.Controllers;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.Web;

namespace tahsinERP.Controllers.Finance
{
    [Permission("FinTransactions")]
    public class FinTransactionsController : Controller
    {
        /// <summary>Ro'yxat filtrlari (Index, IndexData va ExportExcel uchun umumiy).</summary>
        public class FinTransactionFilter
        {
            public DateTime? FromDate { get; set; }
            public DateTime? ToDate { get; set; }
            public int? AccountID { get; set; }
            public int? CategoryID { get; set; }
            public byte? Direction { get; set; }
            public byte? CounterpartyType { get; set; }
            public int? CounterpartyID { get; set; }
            public string SourceDocType { get; set; }
            public string SearchTerm { get; set; }
        }

        private static IQueryable<FIN_TRANSACTIONS> ApplyFilter(IQueryable<FIN_TRANSACTIONS> query, FinTransactionFilter f)
        {
            if (f.FromDate.HasValue)
            {
                var from = f.FromDate.Value;
                query = query.Where(t => t.TxnDate >= from);
            }
            if (f.ToDate.HasValue)
            {
                var to = f.ToDate.Value;
                query = query.Where(t => t.TxnDate <= to);
            }
            if (f.AccountID.HasValue)
            {
                var accountId = f.AccountID.Value;
                query = query.Where(t => t.AccountID == accountId);
            }
            if (f.CategoryID.HasValue)
            {
                var categoryId = f.CategoryID.Value;
                query = query.Where(t => t.CategoryID == categoryId);
            }
            if (f.Direction.HasValue)
            {
                var direction = f.Direction.Value;
                query = query.Where(t => t.Direction == direction);
            }
            if (f.CounterpartyType.HasValue)
            {
                var cpType = f.CounterpartyType.Value;
                query = query.Where(t => t.CounterpartyType == cpType);
            }
            if (f.CounterpartyID.HasValue)
            {
                var cpId = f.CounterpartyID.Value;
                query = query.Where(t => t.CounterpartyID == cpId);
            }
            if (!string.IsNullOrEmpty(f.SourceDocType))
            {
                var docType = f.SourceDocType;
                query = query.Where(t => t.SourceDocType == docType);
            }
            if (!string.IsNullOrEmpty(f.SearchTerm))
            {
                var term = f.SearchTerm.Trim();
                query = query.Where(t =>
                    (t.Purpose != null && t.Purpose.Contains(term)) ||
                    (t.Comment != null && t.Comment.Contains(term)) ||
                    (t.CounterpartyName != null && t.CounterpartyName.Contains(term)) ||
                    (t.ContractNo != null && t.ContractNo.Contains(term)));
            }
            return query;
        }

        private static IQueryable<FIN_TRANSACTIONS> ActiveTransactions(DBTHSNEntities db)
        {
            return db.FIN_TRANSACTIONS.AsNoTracking().Where(t => t.IsDeleted == false || t.IsDeleted == null);
        }

        // GET: FinTransactions
        // Sahifa faqat filtrlarni chizadi; qatorlar IndexData orqali server tomonida sahifalab yuklanadi (FIN-P05).
        // Sana berilmasa — oxirgi 3 oy (avval butun jadval Include'lar bilan yuklanardi).
        public ActionResult Index(DateTime? fromDate, DateTime? toDate, int? accountID, int? categoryID,
            byte? direction, byte? counterpartyType, int? counterpartyID, string sourceDocType, string searchTerm)
        {
            if (!fromDate.HasValue && !toDate.HasValue)
            {
                toDate = DateTime.Today;
                fromDate = toDate.Value.AddMonths(-3);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.AccountList = new SelectList(
                    db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", accountID);
                ViewBag.CategoryList = new SelectList(
                    db.FIN_CATEGORIES.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", categoryID);
            }

            ViewBag.DirectionList = new SelectList(new[] {
                new { Value = (byte?)null, Text = "Barcha" },
                new { Value = (byte?)1, Text = "Kirim" },
                new { Value = (byte?)2, Text = "Chiqim" }
            }, "Value", "Text", direction);

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.AccountID = accountID;
            ViewBag.CategoryID = categoryID;
            ViewBag.Direction = direction;
            ViewBag.CounterpartyType = counterpartyType;
            ViewBag.CounterpartyID = counterpartyID;
            ViewBag.SourceDocType = sourceDocType;
            ViewBag.SearchTerm = searchTerm;

            return View();
        }

        // GET: FinTransactions/IndexData — DataTables server-side (draw/start/length/search/order).
        [HttpGet]
        public JsonResult IndexData(FinTransactionFilter filter, int draw = 1, int start = 0, int length = 25)
        {
            if (length <= 0 || length > 1000) length = 1000; // "Barchasi" ham 1000 qator bilan cheklanadi
            if (start < 0) start = 0;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var baseQuery = ApplyFilter(ActiveTransactions(db), filter ?? new FinTransactionFilter());
                var recordsTotal = baseQuery.Count();

                var query = baseQuery;
                var search = Request.QueryString["search[value]"];
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim();
                    query = query.Where(t =>
                        (t.Purpose != null && t.Purpose.Contains(s)) ||
                        (t.CounterpartyName != null && t.CounterpartyName.Contains(s)) ||
                        (t.ContractNo != null && t.ContractNo.Contains(s)) ||
                        t.FIN_ACCOUNTS.Name.Contains(s) ||
                        t.FIN_CATEGORIES.Name.Contains(s) ||
                        t.CURRENCy.Shortname.Contains(s));
                }
                var recordsFiltered = string.IsNullOrWhiteSpace(search) ? recordsTotal : query.Count();

                var desc = !string.Equals(Request.QueryString["order[0][dir]"], "asc", StringComparison.OrdinalIgnoreCase);
                int.TryParse(Request.QueryString["order[0][column]"], out var orderColumn);
                IOrderedQueryable<FIN_TRANSACTIONS> ordered;
                switch (orderColumn)
                {
                    case 1: ordered = desc ? query.OrderByDescending(t => t.FIN_ACCOUNTS.Name) : query.OrderBy(t => t.FIN_ACCOUNTS.Name); break;
                    case 2: ordered = desc ? query.OrderByDescending(t => t.FIN_CATEGORIES.Name) : query.OrderBy(t => t.FIN_CATEGORIES.Name); break;
                    case 3: ordered = desc ? query.OrderByDescending(t => t.Direction) : query.OrderBy(t => t.Direction); break;
                    case 4: ordered = desc ? query.OrderByDescending(t => t.Amount) : query.OrderBy(t => t.Amount); break;
                    case 5: ordered = desc ? query.OrderByDescending(t => t.CURRENCy.Shortname) : query.OrderBy(t => t.CURRENCy.Shortname); break;
                    case 6: ordered = desc ? query.OrderByDescending(t => t.CounterpartyName) : query.OrderBy(t => t.CounterpartyName); break;
                    case 7: ordered = desc ? query.OrderByDescending(t => t.Purpose) : query.OrderBy(t => t.Purpose); break;
                    default: ordered = desc ? query.OrderByDescending(t => t.TxnDate) : query.OrderBy(t => t.TxnDate); break;
                }

                var rows = ordered
                    .ThenByDescending(t => t.ID)
                    .Skip(start)
                    .Take(length)
                    .Select(t => new
                    {
                        t.ID,
                        t.TxnDate,
                        AccountName = t.FIN_ACCOUNTS.Name,
                        CategoryName = t.FIN_CATEGORIES.Name,
                        t.Direction,
                        t.Amount,
                        Currency = t.CURRENCy.Shortname,
                        t.CounterpartyName,
                        t.Purpose,
                        t.SourceDocType
                    })
                    .ToList()
                    .Select(t => new
                    {
                        id = t.ID,
                        date = t.TxnDate.ToString("dd.MM.yyyy"),
                        account = t.AccountName ?? "-",
                        category = t.CategoryName ?? "-",
                        direction = t.Direction,
                        amount = t.Amount.ToString("N2"),
                        currency = t.Currency ?? "-",
                        counterparty = t.CounterpartyName ?? "-",
                        purpose = t.Purpose ?? "-",
                        editable = string.IsNullOrEmpty(t.SourceDocType) || t.SourceDocType == "Manual"
                    })
                    .ToList();

                return Json(new { draw, recordsTotal, recordsFiltered, data = rows }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: FinTransactions/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var transaction = db.FIN_TRANSACTIONS.AsNoTracking()
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .Include(t => t.USER)
                    .FirstOrDefault(t => t.ID == id && (t.IsDeleted == false || t.IsDeleted == null));

                if (transaction == null)
                {
                    return HttpNotFound();
                }

                // Olish source hujjat link agar available
                if (!string.IsNullOrEmpty(transaction.SourceDocType) && transaction.SourceDocID.HasValue)
                {
                    ViewBag.SourceDocLink = GetSourceDocLink(transaction.SourceDocType, transaction.SourceDocID.Value);
                }

                return View(transaction);
            }
        }

        /// <summary>Create/Edit formasi ro'yxatlari (FIN-D14: avval 4 joyda takrorlangan).</summary>
        private void PopulateFormLists(DBTHSNEntities db, FIN_TRANSACTIONS transaction, bool manualSourceTypesOnly)
        {
            ViewBag.AccountsSelectList = new SelectList(
                db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                "ID", "Name", transaction.AccountID);
            ViewBag.CategoriesSelectList = new SelectList(
                db.FIN_CATEGORIES.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                "ID", "Name", transaction.CategoryID);
            ViewBag.CurrenciesSelectList = new SelectList(
                db.CURRENCIES.AsNoTracking().ToList(),
                "ID", "Shortname", transaction.CurrencyID);
            ViewBag.DirectionList = FinSelectLists.Directions(transaction.Direction);
            ViewBag.CounterpartyTypeList = FinSelectLists.CounterpartyTypes(transaction.CounterpartyType);
            ViewBag.SourceDocTypeList = manualSourceTypesOnly
                ? FinSelectLists.ManualSourceDocTypeList(transaction.SourceDocType)
                : FinSelectLists.SourceDocTypeList(transaction.SourceDocType);
        }

        /// <summary>
        /// Create va Edit uchun umumiy tekshiruv (FIN-B15: avval Edit'da ta'minotchiga to'lov tekshiruvi yo'q edi).
        /// Qo'lda faqat Manual / doimiy harajat / ta'minotchiga to'lov turlari tanlanadi — hujjatdan yaratilgan
        /// (SalesInvoice, PurchaseInvoice ...) turni POST orqali qo'yib bo'lmaydi.
        /// </summary>
        private void ValidateTransaction(DBTHSNEntities db, FIN_TRANSACTIONS transaction)
        {
            var account = db.FIN_ACCOUNTS.Find(transaction.AccountID);
            if (account != null && account.CurrencyID != transaction.CurrencyID)
            {
                ModelState.AddModelError("CurrencyID", "Hisob valyutasi bilan tranzaksiya valyutasi mos kelmaydi.");
            }

            transaction.SourceDocType = string.IsNullOrWhiteSpace(transaction.SourceDocType)
                ? FinSelectLists.Manual
                : transaction.SourceDocType.Trim();
            if (!FinSelectLists.ManualSourceDocTypes.Contains(transaction.SourceDocType, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("SourceDocType", "Bu manba hujjat turini qo'lda tanlab bo'lmaydi.");
            }

            // Ta'minotchiga to'lov bo'lsa, kontragent va invoys bog'lanishi majburiy.
            if (string.Equals(transaction.SourceDocType, FinSelectLists.PurchaseInvoicePayment, StringComparison.OrdinalIgnoreCase))
            {
                if (transaction.CounterpartyType != 2)
                {
                    ModelState.AddModelError("CounterpartyType", "Ta'minotchiga to'lov uchun Kontragent turi = Ta'minotchi bo'lishi kerak.");
                }

                if (!transaction.CounterpartyID.HasValue)
                {
                    ModelState.AddModelError("CounterpartyID", "Ta'minotchiga to'lov uchun Kontragent (ID) tanlanishi shart.");
                }

                if (!transaction.SourceDocID.HasValue)
                {
                    ModelState.AddModelError("SourceDocID", "Ta'minotchiga to'lov uchun Manba hujjat ID (xarid invoysi ID) kiritilishi shart.");
                }
            }
        }

        // Olish so'rovi: FinTransactions/yaratish
        public ActionResult Create(int? accountID, int? categoryID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var model = new FIN_TRANSACTIONS
                {
                    TxnDate = DateTime.Now,
                    Direction = 1, // standart In
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                };

                if (accountID.HasValue)
                {
                    var account = db.FIN_ACCOUNTS.Find(accountID.Value);
                    if (account != null)
                    {
                        model.AccountID = account.ID;
                        model.CurrencyID = account.CurrencyID;
                    }
                }

                PopulateFormLists(db, model, true);
                // Avvalgidek: yangi formada valyuta tanlanmagan, kategoriya URL'dan
                ViewBag.CategoriesSelectList = new SelectList(
                    db.FIN_CATEGORIES.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", categoryID);
                ViewBag.CurrenciesSelectList = new SelectList(db.CURRENCIES.AsNoTracking().ToList(), "ID", "Shortname");

                return View(model);
            }
        }

        // POST so'rovi: FinTransactions/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FIN_TRANSACTIONS transaction)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ValidateTransaction(db, transaction);

                if (ModelState.IsValid)
                {
                    transaction.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                    transaction.CreatedDT = DateTime.Now;

                    db.FIN_TRANSACTIONS.Add(transaction);
                    db.SaveChanges();

                    var accountName = db.FIN_ACCOUNTS.Where(a => a.ID == transaction.AccountID).Select(a => a.Name).FirstOrDefault();
                    var currency = db.CURRENCIES.Where(c => c.ID == transaction.CurrencyID).Select(c => c.Shortname).FirstOrDefault();
                    LogHelper.LogToDatabase(User.Identity.Name, "FinTransactions", "Create",
                        $"Tranzaksiya yaratildi: {transaction.Amount} {currency}, Hisob: {accountName}");

                    return RedirectToAction("Index");
                }

                PopulateFormLists(db, transaction, true);
                return View(transaction);
            }
        }

        // AJAX: kontragent turiga qarab ro'yxat qaytarish
        [Permission("FinTransactions", Change = true)]
        [HttpGet]
        public JsonResult GetCounterparties(byte? type)
        {
            if (!type.HasValue)
            {
                return Json(new { success = true, items = new object[0] }, JsonRequestBehavior.AllowGet);
            }

            // FIN-D17: OpeningBalance bilan umumiy ma'lumotnoma
            using (var db = new DBTHSNEntities())
            {
                var items = new FinCounterpartyLookupService(db).Counterparties(type.Value)
                    .Select(x => new { id = x.ID, name = x.Name })
                    .ToList();
                return Json(new { success = true, items = items }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Kontragent turi/id bo'yicha shartnomalar ro'yxati.
        /// type: 1=Mijoz(S_CONTRACTS), 2=Ta'minotchi(P_CONTRACTS), 4=Forwarder(F_CONTRACTS)
        /// </summary>
        [Permission("FinTransactions", Change = true)]
        [HttpGet]
        public JsonResult GetContractsByCounterparty(byte? type, int? counterpartyId)
        {
            if (!type.HasValue || !counterpartyId.HasValue || counterpartyId.Value <= 0)
            {
                return Json(new { success = true, data = new object[0] }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new DBTHSNEntities())
            {
                var data = new FinCounterpartyLookupService(db).Contracts(type.Value, counterpartyId.Value)
                    .Select(c => new { ID = c.ID, ContractNo = c.ContractNo })
                    .ToList();
                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
        }


        // Olish so'rovi: FinTransactions/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var transaction = db.FIN_TRANSACTIONS
                    .FirstOrDefault(t => t.ID == id && (t.IsDeleted == false || t.IsDeleted == null));
                if (transaction == null)
                {
                    return HttpNotFound();
                }
                if (!FinSelectLists.IsManual(transaction.SourceDocType))
                {
                    TempData["Error"] = $"Ushbu tranzaksiya '{transaction.SourceDocType}' hujjatidan yaratilgan. Uni manba hujjat orqali o'zgartiring.";
                    return RedirectToAction("Index");
                }

                PopulateFormLists(db, transaction, true);
                return View(transaction);
            }
        }

        // POST so'rovi: FinTransactions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FIN_TRANSACTIONS transaction)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var existing = db.FIN_TRANSACTIONS
                    .FirstOrDefault(t => t.ID == transaction.ID && (t.IsDeleted == false || t.IsDeleted == null));
                if (existing == null)
                {
                    return HttpNotFound();
                }

                // FIN-B15: hujjatdan yaratilgan (posted) tranzaksiyani to'g'ridan-to'g'ri POST bilan ham tahrirlab bo'lmaydi
                if (!FinSelectLists.IsManual(existing.SourceDocType))
                {
                    TempData["Error"] = $"Ushbu tranzaksiya '{existing.SourceDocType}' hujjatidan yaratilgan. Tahrirlash mumkin emas.";
                    return RedirectToAction("Index");
                }

                ValidateTransaction(db, transaction);

                if (ModelState.IsValid)
                {
                    existing.TxnDate = transaction.TxnDate;
                    existing.AccountID = transaction.AccountID;
                    existing.CategoryID = transaction.CategoryID;
                    existing.Direction = transaction.Direction;
                    existing.Amount = transaction.Amount;
                    existing.CurrencyID = transaction.CurrencyID;
                    existing.Qty = transaction.Qty;
                    existing.PricePerUnit = transaction.PricePerUnit;
                    existing.CounterpartyType = transaction.CounterpartyType;
                    existing.CounterpartyID = transaction.CounterpartyID;
                    existing.CounterpartyName = transaction.CounterpartyName;
                    existing.Purpose = transaction.Purpose;
                    existing.ContractNo = transaction.ContractNo;
                    existing.Comment = transaction.Comment;
                    existing.SourceDocType = transaction.SourceDocType;
                    existing.SourceDocID = transaction.SourceDocID;

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinTransactions", "Edit",
                        $"Tranzaksiya tahrirlandi: ID {transaction.ID}");

                    return RedirectToAction("Index");
                }

                PopulateFormLists(db, transaction, true);
                return View(transaction);
            }
        }

        // Olish so'rovi: FinTransactions/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var transaction = db.FIN_TRANSACTIONS
                    .Where(t => t.ID == id && (t.IsDeleted == false || t.IsDeleted == null))
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .FirstOrDefault();

                if (transaction == null)
                {
                    return HttpNotFound();
                }

                // Tekshirish agar linked ga source hujjat
                if (!string.IsNullOrEmpty(transaction.SourceDocType) && transaction.SourceDocType != "Manual")
                {
                    ViewBag.CannotDelete = true;
                    ViewBag.SourceDocType = transaction.SourceDocType;
                }

                return View(transaction);
            }
        }

        // POST so'rovi: FinTransactions/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    var transaction = db.FIN_TRANSACTIONS
                        .FirstOrDefault(t => t.ID == id && (t.IsDeleted == false || t.IsDeleted == null));

                    if (transaction == null)
                    {
                        TempData["Error"] = "Tranzaksiya topilmadi yoki oldin o'chirilgan.";
                        return RedirectToAction("Index");
                    }

                    // Faqat qo'lda (Manual) yaratilgan tranzaksiyani o'chirishga ruxsat.
                    if (!FinSelectLists.IsManual(transaction.SourceDocType))
                    {
                        TempData["Error"] = $"Ushbu tranzaksiya '{transaction.SourceDocType}' hujjatidan yaratilgan. O'chirish mumkin emas.";
                        return RedirectToAction("Delete", new { id });
                    }

                    transaction.IsDeleted = true;
                    db.Entry(transaction).State = System.Data.Entity.EntityState.Modified;

                    // Bog'liq yozuvlardagi havolani uzamiz. Sinxron action ichida async helperni
                    // .GetAwaiter().GetResult() bilan chaqirish ASP.NET sync-context deadlock'iga olib keladi
                    // (so'rov "qotib" qoladi, saqlanmaydi). Shu sabab sinxron tarzda bajaramiz.
                    foreach (var row in db.SHAREHOLDER_CONTRIBUTIONS.Where(x => x.FinTransactionID == id).ToList())
                        row.FinTransactionID = null;

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinTransactions", "Delete",
                        $"Tranzaksiya o'chirildi: ID {id}");
                    TempData["Success"] = "Tranzaksiya muvaffaqiyatli o'chirildi.";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"O'chirishda xatolik: {ex.Message}";
                    LogHelper.LogToDatabase(User.Identity.Name, "FinTransactions", "DeleteError",
                        $"Tranzaksiya o'chirishda xatolik: ID {id}. Tafsilot: {ex}");
                }

                return RedirectToAction("Index");
            }
        }

        private sealed class ExportRow
        {
            public DateTime TxnDate { get; set; }
            public string Account { get; set; }
            public string Category { get; set; }
            public byte Direction { get; set; }
            public decimal Amount { get; set; }
            public string Currency { get; set; }
            public string CounterpartyName { get; set; }
            public string Purpose { get; set; }
            public string SourceDocType { get; set; }
        }

        // Olish so'rovi: FinTransactions/ExportExcel
        public ActionResult ExportExcel(FinTransactionFilter filter)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var rows = ApplyFilter(ActiveTransactions(db), filter ?? new FinTransactionFilter())
                    .OrderByDescending(t => t.TxnDate)
                    .Select(t => new ExportRow
                    {
                        TxnDate = t.TxnDate,
                        Account = t.FIN_ACCOUNTS.Name,
                        Category = t.FIN_CATEGORIES.Name,
                        Direction = t.Direction,
                        Amount = t.Amount,
                        Currency = t.CURRENCy.Shortname,
                        CounterpartyName = t.CounterpartyName,
                        Purpose = t.Purpose,
                        SourceDocType = t.SourceDocType
                    })
                    .ToList();

                // FIN-D22: umumiy ExcelExport (litsenziya Application_Start'da o'rnatiladi)
                var bytes = ExcelExport.Create("Tranzaksiyalar", rows,
                    ExcelExport.Col<ExportRow>("Sana", x => x.TxnDate.ToString("yyyy-MM-dd")),
                    ExcelExport.Col<ExportRow>("Hisob", x => x.Account),
                    ExcelExport.Col<ExportRow>("Kategoriya", x => x.Category),
                    ExcelExport.Col<ExportRow>("Yo'nalish", x => x.Direction == 1 ? "Kirim" : "Chiqim"),
                    ExcelExport.Col<ExportRow>("Summa", x => x.Amount),
                    ExcelExport.Col<ExportRow>("Valyuta", x => x.Currency),
                    ExcelExport.Col<ExportRow>("Kontragent", x => x.CounterpartyName),
                    ExcelExport.Col<ExportRow>("Maqsad", x => x.Purpose),
                    ExcelExport.Col<ExportRow>("Manba hujjat", x => x.SourceDocType));

                return File(bytes, ExcelExport.ContentType, $"FinTransactions_{DateTime.Now:yyyyMMdd}.xlsx");
            }
        }

        // Olish so'rovi: FinTransactions/Import
        public ActionResult Import()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.CurrencyShortname = FinTransactionImportService.CurrencyHint(db);
            }

            return View();
        }

        // Olish so'rovi: FinTransactions/DownloadTemplate
        [Permission("FinTransactions", Change = true)]
        public ActionResult DownloadTemplate()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                return File(FinTransactionImportService.Template(db), ExcelExport.ContentType, "FinTransactions_Template.xlsx");
            }
        }

        // POST so'rovi: FinTransactions/Import
        // Qatorlarni o'qish va tekshirish — FinTransactionImportService (FIN-M03)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Import(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                ViewBag.Error = "Fayl yuklanmadi yoki bo'sh!";
                return View();
            }

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "Faqat .xlsx formatidagi fayllarni yuklash mumkin!";
                return View();
            }

            try
            {
                using (var workbook = new XLWorkbook(file.InputStream))
                {
                    var worksheet = workbook.Worksheet(1);
                    if (worksheet.LastRowNumber() < 2)
                    {
                        ViewBag.Error = "Excel faylda ma'lumot yo'q!";
                        return View();
                    }

                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        int currentUserId;
                        try
                        {
                            currentUserId = UserHelper.GetUserId(User.Identity.Name);
                        }
                        catch (Exception userEx)
                        {
                            ViewBag.Error = $"Foydalanuvchi topilmadi: {User.Identity.Name}. Xatolik: {userEx.Message}. Iltimos, tizimga qayta kiring yoki administratorga murojaat qiling.";
                            return View();
                        }

                        var result = FinTransactionImportService.Parse(worksheet, db, currentUserId);
                        var messages = result.Errors.Concat(result.Warnings).ToList();

                        if (result.Transactions.Count == 0)
                        {
                            ViewBag.Error = "Hech qanday to'g'ri tranzaksiya topilmadi!";
                            if (messages.Count > 0)
                            {
                                ViewBag.Errors = messages;
                            }
                            return View();
                        }

                        try
                        {
                            db.FIN_TRANSACTIONS.AddRange(result.Transactions);
                            db.SaveChanges();
                        }
                        catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
                        {
                            var validationErrors = dbEx.EntityValidationErrors
                                .SelectMany(v => v.ValidationErrors)
                                .Select(e => $"Maydon: {e.PropertyName}, Xatolik: {e.ErrorMessage}");
                            ViewBag.Error = $"Ma'lumotlar validatsiyadan o'tmadi: {string.Join("; ", validationErrors)}";
                            if (dbEx.InnerException != null)
                            {
                                ViewBag.Error += $" | Ichki xatolik: {dbEx.InnerException.Message}";
                            }
                            if (messages.Count > 0)
                            {
                                ViewBag.Errors = messages;
                            }
                            return View();
                        }
                        catch (System.Data.Entity.Infrastructure.DbUpdateException dbUpdateEx)
                        {
                            ViewBag.Error = "Ma'lumotlar bazasini yangilashda xatolik: " + ExceptionChain(dbUpdateEx);
                            if (messages.Count > 0)
                            {
                                ViewBag.Errors = messages;
                            }
                            return View();
                        }

                        ViewBag.Success = $"{result.Transactions.Count} ta tranzaksiya muvaffaqiyatli import qilindi!";
                        if (messages.Count > 0)
                        {
                            ViewBag.Errors = messages;
                            ViewBag.Warning = $"{result.Errors.Count} ta qatorda xatolik, {result.Warnings.Count} ta ogohlantirish.";
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "FinTransactionsController",
                            $"{result.Transactions.Count} ta tranzaksiya Excel dan import qilindi");
                    }
                }

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Import jarayonida xatolik: {ExceptionChain(ex)}";
                LogHelper.LogToDatabase(User.Identity.Name, "FinTransactionsController",
                    $"Import xatolik: {ex.Message}", ex.ToString());
                return View();
            }
        }

        private static string ExceptionChain(Exception ex)
        {
            var messages = new List<string>();
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrEmpty(current.Message))
                {
                    messages.Add(current.Message);
                }
            }
            return string.Join(" -> ", messages);
        }

        private string GetSourceDocLink(string sourceDocType, int sourceDocID)
        {
            switch (sourceDocType)
            {
                case "SalesInvoice":
                    return Url.Action("Details", "SalesInvoices", new { id = sourceDocID });
                case "PurchaseInvoice":
                    return Url.Action("Details", "PInvoices", new { id = sourceDocID });
                case "IncomeDoc":
                    return Url.Action("Details", "Incomes", new { id = sourceDocID });
                case "ExpenseDoc":
                    return Url.Action("Details", "Expenses", new { id = sourceDocID });
                case "ShareholderContribution":
                    // Aksiyador hissasi yozuvida SourceDocID aksiyador emas — ro'yxatga yo'naltiramiz
                    return Url.Action("Index", "Shareholder");
                case "WaybillPayment":
                    return Url.Action("Details", "WaybillPayment", new { id = sourceDocID });
                default:
                    return null;
            }
        }
    }
}
