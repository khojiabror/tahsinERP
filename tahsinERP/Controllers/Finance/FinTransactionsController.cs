using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Controllers;
using OfficeOpenXml;
using System.IO;
using System.Collections.Generic;
using System.Web;

namespace tahsinERP.Controllers.Finance
{
    public class FinTransactionsController : Controller
    {
        // GET: FinTransactions
        public ActionResult Index(DateTime? fromDate, DateTime? toDate, int? accountID, int? categoryID, 
            byte? direction, byte? counterpartyType, int? counterpartyID, string sourceDocType, 
            string searchTerm, string sortOrder, int? page, int? pageSize)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.FIN_TRANSACTIONS
                    .Where(t => t.IsDeleted == false || t.IsDeleted == null)
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .Include(t => t.USER);

                // Date range filter
                if (fromDate.HasValue)
                {
                    query = query.Where(t => t.TxnDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(t => t.TxnDate <= toDate.Value);
                }

                // Account filter
                if (accountID.HasValue)
                {
                    query = query.Where(t => t.AccountID == accountID.Value);
                }

                // Category filter
                if (categoryID.HasValue)
                {
                    query = query.Where(t => t.CategoryID == categoryID.Value);
                }

                // Direction filter (1=In, 2=Out)
                if (direction.HasValue)
                {
                    query = query.Where(t => t.Direction == direction.Value);
                }

                // Counterparty filters
                if (counterpartyType.HasValue)
                {
                    query = query.Where(t => t.CounterpartyType == counterpartyType.Value);
                }

                if (counterpartyID.HasValue)
                {
                    query = query.Where(t => t.CounterpartyID == counterpartyID.Value);
                }

                // Source document type filter
                if (!string.IsNullOrEmpty(sourceDocType))
                {
                    query = query.Where(t => t.SourceDocType == sourceDocType);
                }

                // Search term (purpose, comment, counterparty name, contract no)
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(t =>
                        (t.Purpose != null && t.Purpose.Contains(searchTerm)) ||
                        (t.Comment != null && t.Comment.Contains(searchTerm)) ||
                        (t.CounterpartyName != null && t.CounterpartyName.Contains(searchTerm)) ||
                        (t.ContractNo != null && t.ContractNo.Contains(searchTerm)));
                }

                // Default sorting - DataTable will handle client-side sorting
                query = query.OrderByDescending(t => t.TxnDate);

                // Load all transactions for client-side DataTable (no server-side pagination)
                var transactions = query.ToList();

                // Ensure navigation properties are loaded for all transactions
                var accountIds = transactions.Where(t => t.AccountID > 0).Select(t => t.AccountID).Distinct().ToList();
                var categoryIds = transactions.Where(t => t.CategoryID > 0).Select(t => t.CategoryID).Distinct().ToList();
                var currencyIds = transactions.Where(t => t.CurrencyID > 0).Select(t => t.CurrencyID).Distinct().ToList();

                var accounts = db.FIN_ACCOUNTS.Where(a => accountIds.Contains(a.ID)).ToDictionary(a => a.ID, a => a);
                var categoriesDict = db.FIN_CATEGORIES.Where(c => categoryIds.Contains(c.ID)).ToDictionary(c => c.ID, c => c);
                var currencies = db.CURRENCIES.Where(c => currencyIds.Contains(c.ID)).ToDictionary(c => c.ID, c => c);

                foreach (var txn in transactions)
                {
                    if (txn.FIN_ACCOUNTS == null && txn.AccountID > 0 && accounts.ContainsKey(txn.AccountID))
                    {
                        txn.FIN_ACCOUNTS = accounts[txn.AccountID];
                    }
                    if (txn.FIN_CATEGORIES == null && txn.CategoryID > 0 && categoriesDict.ContainsKey(txn.CategoryID))
                    {
                        txn.FIN_CATEGORIES = categoriesDict[txn.CategoryID];
                    }
                    if (txn.CURRENCy == null && txn.CurrencyID > 0 && currencies.ContainsKey(txn.CurrencyID))
                    {
                        txn.CURRENCy = currencies[txn.CurrencyID];
                    }
                }

                // ViewBag for filters
                ViewBag.AccountList = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", accountID);

                // Build category tree for dropdown
                var categories = db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
                ViewBag.CategoryList = new SelectList(categories, "ID", "Name", categoryID);

                ViewBag.DirectionList = new SelectList(new[] {
                    new { Value = (byte?)null, Text = "Barcha" },
                    new { Value = (byte?)1, Text = "Kirim" },
                    new { Value = (byte?)2, Text = "Chiqim" }
                }, "Value", "Text", direction);

                ViewBag.CounterpartyTypeList = new SelectList(new[] {
                    new { Value = (byte?)null, Text = "Barcha" },
                    new { Value = (byte?)1, Text = "Mijoz" },
                    new { Value = (byte?)2, Text = "Ta'minotchi" },
                    new { Value = (byte?)3, Text = "Aksiyador" },
                    new { Value = (byte?)4, Text = "Forwarder" },
                    new { Value = (byte?)6, Text = "Xodim" },
                    new { Value = (byte?)5, Text = "Boshqa" }
                }, "Value", "Text", counterpartyType);

                ViewBag.SourceDocTypeList = new SelectList(new[] {
                    new { Value = "", Text = "Barcha" },
                    new { Value = "SalesInvoice", Text = "Sotuv invoysi" },
                    new { Value = "PurchaseInvoice", Text = "Xarid invoysi" },
                    new { Value = "IncomeDoc", Text = "Ombor kirimi" },
                    new { Value = "ExpenseDoc", Text = "Ombor chiqimi" },
                    new { Value = "ShareholderContribution", Text = "Aksiyador hissasi" },
                    new { Value = "WaybillPayment", Text = "Yo'l varaqasi to'lovi" },
                    new { Value = "Manual", Text = "Qo'lda kiritilgan" }
                }, "Value", "Text", sourceDocType);

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.SortOrder = sortOrder;

                return View(transactions);
            }
        }

        // GET: FinTransactions/Details/5
        public ActionResult Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var transaction = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .Include(t => t.USER)
                    .FirstOrDefault(t => t.ID == id);

                if (transaction == null)
                {
                    return HttpNotFound();
                }

                // Ensure navigation properties are loaded
                if (transaction.FIN_ACCOUNTS == null && transaction.AccountID > 0)
                {
                    transaction.FIN_ACCOUNTS = db.FIN_ACCOUNTS.Find(transaction.AccountID);
                }
                if (transaction.FIN_CATEGORIES == null && transaction.CategoryID > 0)
                {
                    transaction.FIN_CATEGORIES = db.FIN_CATEGORIES.Find(transaction.CategoryID);
                }
                if (transaction.CURRENCy == null && transaction.CurrencyID > 0)
                {
                    transaction.CURRENCy = db.CURRENCIES.Find(transaction.CurrencyID);
                }
                if (transaction.USER == null && transaction.CreatedByUserID > 0)
                {
                    transaction.USER = db.USERS.Find(transaction.CreatedByUserID);
                }

                // Get source document link if available
                if (!string.IsNullOrEmpty(transaction.SourceDocType) && transaction.SourceDocID.HasValue)
                {
                    ViewBag.SourceDocLink = GetSourceDocLink(transaction.SourceDocType, transaction.SourceDocID.Value);
                }

                return View(transaction);
            }
        }

        // GET: FinTransactions/Create
        public ActionResult Create(int? accountID, int? categoryID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.AccountID = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", accountID);

                ViewBag.CategoryID = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", categoryID);

                ViewBag.CurrencyID = new SelectList(
                    db.CURRENCIES.ToList(),
                    "ID", "Shortname");

                ViewBag.DirectionList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Kirim" },
                    new { Value = (byte)2, Text = "Chiqim" }
                }, "Value", "Text");

                ViewBag.CounterpartyTypeList = new SelectList(new[] {
                    new { Value = (byte?)null, Text = "Tanlanmagan" },
                    new { Value = (byte?)1, Text = "Mijoz" },
                    new { Value = (byte?)2, Text = "Ta'minotchi" },
                    new { Value = (byte?)3, Text = "Aksiyador" },
                    new { Value = (byte?)4, Text = "Forwarder" },
                    new { Value = (byte?)6, Text = "Xodim" },
                    new { Value = (byte?)5, Text = "Boshqa" }
                }, "Value", "Text");

                var model = new FIN_TRANSACTIONS
                {
                    TxnDate = DateTime.Now,
                    Direction = 1, // Default In
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

                return View(model);
            }
        }

        // POST: FinTransactions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FIN_TRANSACTIONS transaction)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Validate account currency matches transaction currency
                var account = db.FIN_ACCOUNTS.Find(transaction.AccountID);
                if (account != null && account.CurrencyID != transaction.CurrencyID)
                {
                    ModelState.AddModelError("CurrencyID", "Hisob valyutasi bilan tranzaksiya valyutasi mos kelmaydi.");
                }

                if (ModelState.IsValid)
                {
                    transaction.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                    transaction.CreatedDT = DateTime.Now;
                    transaction.SourceDocType = transaction.SourceDocType ?? "Manual";

                    db.FIN_TRANSACTIONS.Add(transaction);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinTransactions", "Create", 
                        $"Tranzaksiya yaratildi: {transaction.Amount} {transaction.CURRENCy?.Shortname}, Hisob: {account?.Name}");

                    return RedirectToAction("Index");
                }

                ViewBag.AccountID = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name),
                    "ID", "Name", transaction.AccountID);

                ViewBag.CategoryID = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name),
                    "ID", "Name", transaction.CategoryID);

                ViewBag.CurrencyID = new SelectList(
                    db.CURRENCIES,
                    "ID", "Shortname", transaction.CurrencyID);

                ViewBag.DirectionList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Kirim" },
                    new { Value = (byte)2, Text = "Chiqim" }
                }, "Value", "Text", transaction.Direction);

                ViewBag.CounterpartyTypeList = new SelectList(new[] {
                    new { Value = (byte?)null, Text = "Tanlanmagan" },
                    new { Value = (byte?)1, Text = "Mijoz" },
                    new { Value = (byte?)2, Text = "Ta'minotchi" },
                    new { Value = (byte?)3, Text = "Aksiyador" },
                    new { Value = (byte?)4, Text = "Forwarder" },
                    new { Value = (byte?)6, Text = "Xodim" },
                    new { Value = (byte?)5, Text = "Boshqa" }
                }, "Value", "Text", transaction.CounterpartyType);

                return View(transaction);
            }
        }

        // GET: FinTransactions/Edit/5
        public ActionResult Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var transaction = db.FIN_TRANSACTIONS.Find(id);
                if (transaction == null)
                {
                    return HttpNotFound();
                }

                // Don't allow editing if linked to source document
                if (!string.IsNullOrEmpty(transaction.SourceDocType) && transaction.SourceDocType != "Manual")
                {
                    ViewBag.CannotEdit = true;
                    ViewBag.SourceDocType = transaction.SourceDocType;
                    return View(transaction);
                }

                ViewBag.AccountID = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", transaction.AccountID);

                ViewBag.CategoryID = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", transaction.CategoryID);

                ViewBag.CurrencyID = new SelectList(
                    db.CURRENCIES.ToList(),
                    "ID", "Shortname", transaction.CurrencyID);

                ViewBag.DirectionList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Kirim" },
                    new { Value = (byte)2, Text = "Chiqim" }
                }, "Value", "Text", transaction.Direction);

                ViewBag.CounterpartyTypeList = new SelectList(new[] {
                    new { Value = (byte?)null, Text = "Tanlanmagan" },
                    new { Value = (byte?)1, Text = "Mijoz" },
                    new { Value = (byte?)2, Text = "Ta'minotchi" },
                    new { Value = (byte?)3, Text = "Aksiyador" },
                    new { Value = (byte?)4, Text = "Forwarder" },
                    new { Value = (byte?)6, Text = "Xodim" },
                    new { Value = (byte?)5, Text = "Boshqa" }
                }, "Value", "Text", transaction.CounterpartyType);

                return View(transaction);
            }
        }

        // POST: FinTransactions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FIN_TRANSACTIONS transaction)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var existing = db.FIN_TRANSACTIONS.Find(transaction.ID);
                if (existing == null)
                {
                    return HttpNotFound();
                }

                // Don't allow editing if linked to source document
                if (!string.IsNullOrEmpty(existing.SourceDocType) && existing.SourceDocType != "Manual")
                {
                    ViewBag.CannotEdit = true;
                    ViewBag.SourceDocType = existing.SourceDocType;
                    return View(transaction);
                }

                var account = db.FIN_ACCOUNTS.Find(transaction.AccountID);
                if (account != null && account.CurrencyID != transaction.CurrencyID)
                {
                    ModelState.AddModelError("CurrencyID", "Hisob valyutasi bilan tranzaksiya valyutasi mos kelmaydi.");
                }

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

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinTransactions", "Edit", 
                        $"Tranzaksiya tahrirlandi: ID {transaction.ID}");

                    return RedirectToAction("Index");
                }

                ViewBag.AccountID = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", transaction.AccountID);

                ViewBag.CategoryID = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", transaction.CategoryID);

                ViewBag.CurrencyID = new SelectList(
                    db.CURRENCIES.ToList(),
                    "ID", "Shortname", transaction.CurrencyID);

                ViewBag.DirectionList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Kirim" },
                    new { Value = (byte)2, Text = "Chiqim" }
                }, "Value", "Text", transaction.Direction);

                ViewBag.CounterpartyTypeList = new SelectList(new[] {
                    new { Value = (byte?)null, Text = "Tanlanmagan" },
                    new { Value = (byte?)1, Text = "Mijoz" },
                    new { Value = (byte?)2, Text = "Ta'minotchi" },
                    new { Value = (byte?)3, Text = "Aksiyador" },
                    new { Value = (byte?)4, Text = "Forwarder" },
                    new { Value = (byte?)6, Text = "Xodim" },
                    new { Value = (byte?)5, Text = "Boshqa" }
                }, "Value", "Text", transaction.CounterpartyType);

                return View(transaction);
            }
        }

        // GET: FinTransactions/Delete/5
        public ActionResult Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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

                // Check if linked to source document
                if (!string.IsNullOrEmpty(transaction.SourceDocType) && transaction.SourceDocType != "Manual")
                {
                    ViewBag.CannotDelete = true;
                    ViewBag.SourceDocType = transaction.SourceDocType;
                }

                return View(transaction);
            }
        }

        // POST: FinTransactions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var transaction = db.FIN_TRANSACTIONS.Find(id);
                if (transaction != null)
                {
                    // Only allow deletion of manual transactions
                    if (string.IsNullOrEmpty(transaction.SourceDocType) || transaction.SourceDocType == "Manual")
                    {
                        transaction.IsDeleted = true;
                        db.Entry(transaction).State = System.Data.Entity.EntityState.Modified;
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "FinTransactions", "Delete", 
                            $"Tranzaksiya o'chirildi: ID {id}");
                    }
                }

                return RedirectToAction("Index");
            }
        }

        // GET: FinTransactions/ExportExcel
        public ActionResult ExportExcel(DateTime? fromDate, DateTime? toDate, int? accountID, int? categoryID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .AsQueryable();

                if (fromDate.HasValue)
                    query = query.Where(t => t.TxnDate >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(t => t.TxnDate <= toDate.Value);
                if (accountID.HasValue)
                    query = query.Where(t => t.AccountID == accountID.Value);
                if (categoryID.HasValue)
                    query = query.Where(t => t.CategoryID == categoryID.Value);

                var transactions = query.OrderByDescending(t => t.TxnDate).ToList();

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Tranzaksiyalar");
                    worksheet.Cells[1, 1].Value = "Sana";
                    worksheet.Cells[1, 2].Value = "Hisob";
                    worksheet.Cells[1, 3].Value = "Kategoriya";
                    worksheet.Cells[1, 4].Value = "Yo'nalish";
                    worksheet.Cells[1, 5].Value = "Summa";
                    worksheet.Cells[1, 6].Value = "Valyuta";
                    worksheet.Cells[1, 7].Value = "Kontragent";
                    worksheet.Cells[1, 8].Value = "Maqsad";
                    worksheet.Cells[1, 9].Value = "Manba hujjat";

                    int row = 2;
                    foreach (var txn in transactions)
                    {
                        worksheet.Cells[row, 1].Value = txn.TxnDate.ToString("yyyy-MM-dd");
                        worksheet.Cells[row, 2].Value = txn.FIN_ACCOUNTS?.Name;
                        worksheet.Cells[row, 3].Value = txn.FIN_CATEGORIES?.Name;
                        worksheet.Cells[row, 4].Value = txn.Direction == 1 ? "Kirim" : "Chiqim";
                        worksheet.Cells[row, 5].Value = txn.Amount;
                        worksheet.Cells[row, 6].Value = txn.CURRENCy?.Shortname;
                        worksheet.Cells[row, 7].Value = txn.CounterpartyName;
                        worksheet.Cells[row, 8].Value = txn.Purpose;
                        worksheet.Cells[row, 9].Value = txn.SourceDocType;
                        row++;
                    }

                    worksheet.Cells.AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"FinTransactions_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }

        // GET: FinTransactions/Import
        public ActionResult Import()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var defaultCurrency = db.CURRENCIES
                    .Where(c => c.IsDeleted != true)
                    .OrderBy(c => c.ID)
                    .FirstOrDefault();
                ViewBag.CurrencyShortname = defaultCurrency?.Shortname ?? "UZS";
            }

            return View();
        }

        // GET: FinTransactions/DownloadTemplate
        public ActionResult DownloadTemplate()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Tranzaksiyalar");

                    // Header row
                    worksheet.Cells[1, 1].Value = "Sana (DD.MM.YYYY)";
                    worksheet.Cells[1, 2].Value = "Vaqt (HH:mm)";
                    worksheet.Cells[1, 3].Value = "Hisob kodi";
                    worksheet.Cells[1, 4].Value = "Kategoriya nomi";
                    worksheet.Cells[1, 5].Value = "Yo'nalish (1=Kirim, 2=Chiqim)";
                    worksheet.Cells[1, 6].Value = "Summa";
                    var defaultCurrency = db.CURRENCIES
                        .Where(c => c.IsDeleted != true)
                        .OrderBy(c => c.ID)
                        .FirstOrDefault();
                    var currencyShortname = defaultCurrency?.Shortname ?? "UZS";

                    worksheet.Cells[1, 7].Value = $"Valyuta qisqa nomi (masalan: {currencyShortname})";
                    worksheet.Cells[1, 8].Value = "Kontragent nomi (ixtiyoriy)";
                    worksheet.Cells[1, 9].Value = "Maqsad (ixtiyoriy)";
                    worksheet.Cells[1, 10].Value = "Shartnoma raqami (ixtiyoriy)";
                    worksheet.Cells[1, 11].Value = "Izoh (ixtiyoriy)";
                    worksheet.Cells[1, 12].Value = "Miqdor (ixtiyoriy)";
                    worksheet.Cells[1, 13].Value = "Narx birligi (ixtiyoriy)";

                    // Style header
                    using (var range = worksheet.Cells[1, 1, 1, 13])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }

                    // Add example row
                    worksheet.Cells[2, 1].Value = "01.11.2025";
                    worksheet.Cells[2, 2].Value = "10:30";
                    worksheet.Cells[2, 3].Value = "9000";
                    worksheet.Cells[2, 4].Value = "Sotuv";
                    worksheet.Cells[2, 5].Value = "1";
                    worksheet.Cells[2, 6].Value = "1000000.00";
                    worksheet.Cells[2, 7].Value = "UZS";
                    worksheet.Cells[2, 8].Value = "Mijoz nomi";
                    worksheet.Cells[2, 9].Value = "Mahsulot sotuvi";
                    worksheet.Cells[2, 10].Value = "K-001";
                    worksheet.Cells[2, 11].Value = "Qo'shimcha izoh";

                    // Get available accounts and categories for reference
                    var accounts = db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Code).ToList();
                    var categories = db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
                    var currencies = db.CURRENCIES.OrderBy(c => c.Shortname).ToList();

                    // Add reference sheets
                    var accountsSheet = package.Workbook.Worksheets.Add("Hisoblar");
                    accountsSheet.Cells[1, 1].Value = "Kod";
                    accountsSheet.Cells[1, 2].Value = "Nomi";
                    int accRow = 2;
                    foreach (var acc in accounts)
                    {
                        accountsSheet.Cells[accRow, 1].Value = acc.Code;
                        accountsSheet.Cells[accRow, 2].Value = acc.Name;
                        accRow++;
                    }

                    var categoriesSheet = package.Workbook.Worksheets.Add("Kategoriyalar");
                    categoriesSheet.Cells[1, 1].Value = "Nomi";
                    int catRow = 2;
                    foreach (var cat in categories)
                    {
                        categoriesSheet.Cells[catRow, 1].Value = cat.Name;
                        catRow++;
                    }

                    var currenciesSheet = package.Workbook.Worksheets.Add("Valyutalar");
                    currenciesSheet.Cells[1, 1].Value = "Qisqa nomi";
                    currenciesSheet.Cells[1, 2].Value = "To'liq nomi";
                    int currRow = 2;
                    foreach (var curr in currencies)
                    {
                        currenciesSheet.Cells[currRow, 1].Value = curr.Shortname;
                        currenciesSheet.Cells[currRow, 2].Value = curr.Shortname;
                        currRow++;
                    }

                    worksheet.Cells.AutoFitColumns();
                    accountsSheet.Cells.AutoFitColumns();
                    categoriesSheet.Cells.AutoFitColumns();
                    currenciesSheet.Cells.AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "FinTransactions_Template.xlsx");
                }
            }
        }

        // POST: FinTransactions/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Import(HttpPostedFileBase file)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                var transactions = new List<FIN_TRANSACTIONS>();
                var errors = new List<string>();

                using (var package = new ExcelPackage(file.InputStream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension?.Rows ?? 0;

                    if (rowCount < 2)
                    {
                        ViewBag.Error = "Excel faylda ma'lumot yo'q!";
                        return View();
                    }

                    // Get lookup dictionaries and current user
                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        // Use UserHelper to get current user ID (standard approach in this codebase)
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

                        var accounts = db.FIN_ACCOUNTS.Where(a => a.IsActive).ToDictionary(a => a.Code, a => a.ID, StringComparer.OrdinalIgnoreCase);
                        var categories = db.FIN_CATEGORIES.Where(c => c.IsActive).ToDictionary(c => c.Name, c => c.ID, StringComparer.OrdinalIgnoreCase);
                        var currencies = db.CURRENCIES.ToDictionary(c => c.Shortname, c => c.ID, StringComparer.OrdinalIgnoreCase);

                        for (int row = 2; row <= rowCount; row++)
                        {
                            try
                            {
                                // Read date - try to get as DateTime value first (Excel stores dates as numbers)
                                DateTime txnDate;
                                var dateCell = worksheet.Cells[row, 1];
                                
                                // First try to get as DateTime value (if Excel stored it as date)
                                if (dateCell.Value != null)
                                {
                                    if (dateCell.Value is DateTime dateTimeValue)
                                    {
                                        txnDate = dateTimeValue;
                                    }
                                    else if (dateCell.Value is double dateDouble)
                                    {
                                        // Excel stores dates as serial numbers (days since 1900-01-01)
                                        txnDate = DateTime.FromOADate(dateDouble);
                                    }
                                    else
                                    {
                                        // Try to parse as string
                                        var dateStr = dateCell.Value.ToString()?.Trim();
                                        if (string.IsNullOrEmpty(dateStr))
                                        {
                                            continue; // Skip empty rows
                                        }
                                        
                                        // Try multiple date formats
                                        var dateFormats = new[] { 
                                            "dd.MM.yyyy", "dd/MM/yyyy", "d.M.yyyy", "d/M/yyyy",
                                            "MM/dd/yyyy", "M/d/yyyy", "M/dd/yyyy", "MM/d/yyyy",
                                            "yyyy-MM-dd", "yyyy/MM/dd",
                                            "dd-MM-yyyy", "d-M-yyyy"
                                        };
                                        
                                        if (!DateTime.TryParseExact(dateStr, dateFormats, 
                                            System.Globalization.CultureInfo.InvariantCulture, 
                                            System.Globalization.DateTimeStyles.None, out txnDate))
                                        {
                                            // Last resort: try general DateTime parsing
                                            if (!DateTime.TryParse(dateStr, 
                                                System.Globalization.CultureInfo.InvariantCulture, 
                                                System.Globalization.DateTimeStyles.None, out txnDate))
                                            {
                                                errors.Add($"Qator {row}: Sana noto'g'ri format - {dateStr}");
                                                continue;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Cell is empty or null
                                    var dateStr = dateCell.Text?.Trim();
                                    if (string.IsNullOrEmpty(dateStr))
                                    {
                                        continue; // Skip empty rows
                                    }
                                    
                                    // Try multiple date formats
                                    var dateFormats = new[] { 
                                        "dd.MM.yyyy", "dd/MM/yyyy", "d.M.yyyy", "d/M/yyyy",
                                        "MM/dd/yyyy", "M/d/yyyy", "M/dd/yyyy", "MM/d/yyyy",
                                        "yyyy-MM-dd", "yyyy/MM/dd",
                                        "dd-MM-yyyy", "d-M-yyyy"
                                    };
                                    
                                    if (!DateTime.TryParseExact(dateStr, dateFormats, 
                                        System.Globalization.CultureInfo.InvariantCulture, 
                                        System.Globalization.DateTimeStyles.None, out txnDate))
                                    {
                                        // Last resort: try general DateTime parsing
                                        if (!DateTime.TryParse(dateStr, 
                                            System.Globalization.CultureInfo.InvariantCulture, 
                                            System.Globalization.DateTimeStyles.None, out txnDate))
                                        {
                                            errors.Add($"Qator {row}: Sana noto'g'ri format - {dateStr}");
                                            continue;
                                        }
                                    }
                                }

                                // Read time - try to get as DateTime value first
                                var timeCell = worksheet.Cells[row, 2];
                                if (timeCell.Value != null)
                                {
                                    if (timeCell.Value is DateTime timeDateTime)
                                    {
                                        txnDate = txnDate.Date.Add(timeDateTime.TimeOfDay);
                                    }
                                    else if (timeCell.Value is double timeDouble && timeDouble < 1.0)
                                    {
                                        // Excel stores time as fraction of day (0.0 to 0.999...)
                                        var timeSpan = TimeSpan.FromDays(timeDouble);
                                        txnDate = txnDate.Date.Add(timeSpan);
                                    }
                                    else
                                    {
                                        var timeStr = timeCell.Value.ToString()?.Trim();
                                        if (!string.IsNullOrEmpty(timeStr))
                                        {
                                            TimeSpan time;
                                            if (TimeSpan.TryParse(timeStr, out time))
                                            {
                                                txnDate = txnDate.Date.Add(time);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var timeStr = timeCell.Text?.Trim();
                                    if (!string.IsNullOrEmpty(timeStr))
                                    {
                                        TimeSpan time;
                                        if (TimeSpan.TryParse(timeStr, out time))
                                        {
                                            txnDate = txnDate.Date.Add(time);
                                        }
                                    }
                                }

                                // Read account code
                                var accountCode = worksheet.Cells[row, 3].Text?.Trim();
                                if (string.IsNullOrEmpty(accountCode) || !accounts.ContainsKey(accountCode))
                                {
                                    errors.Add($"Qator {row}: Hisob kodi topilmadi - {accountCode}");
                                    continue;
                                }
                                int accountId = accounts[accountCode];

                                // Read category name
                                var categoryName = worksheet.Cells[row, 4].Text?.Trim();
                                if (string.IsNullOrEmpty(categoryName) || !categories.ContainsKey(categoryName))
                                {
                                    errors.Add($"Qator {row}: Kategoriya topilmadi - {categoryName}");
                                    continue;
                                }
                                int categoryId = categories[categoryName];

                                // Read direction
                                var directionStr = worksheet.Cells[row, 5].Text?.Trim();
                                byte direction;
                                if (!byte.TryParse(directionStr, out direction) || (direction != 1 && direction != 2))
                                {
                                    errors.Add($"Qator {row}: Yo'nalish noto'g'ri (1 yoki 2 bo'lishi kerak) - {directionStr}");
                                    continue;
                                }

                                // Read amount
                                var amountStr = worksheet.Cells[row, 6].Text?.Trim();
                                if (string.IsNullOrEmpty(amountStr) || !decimal.TryParse(amountStr.Replace(" ", "").Replace(",", "."), 
                                    System.Globalization.NumberStyles.Any, 
                                    System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
                                {
                                    errors.Add($"Qator {row}: Summa noto'g'ri - {amountStr}");
                                    continue;
                                }

                                // Read currency
                                var currencyShortname = worksheet.Cells[row, 7].Text?.Trim();
                                if (string.IsNullOrEmpty(currencyShortname) || !currencies.ContainsKey(currencyShortname))
                                {
                                    errors.Add($"Qator {row}: Valyuta topilmadi - {currencyShortname}");
                                    continue;
                                }
                                int currencyId = currencies[currencyShortname];

                                // Read optional fields
                                var counterpartyName = worksheet.Cells[row, 8].Text?.Trim();
                                var purpose = worksheet.Cells[row, 9].Text?.Trim();
                                var contractNo = worksheet.Cells[row, 10].Text?.Trim();
                                var comment = worksheet.Cells[row, 11].Text?.Trim();
                                var qtyStr = worksheet.Cells[row, 12].Text?.Trim();
                                var pricePerUnitStr = worksheet.Cells[row, 13].Text?.Trim();

                                decimal? qty = null;
                                if (!string.IsNullOrEmpty(qtyStr) && decimal.TryParse(qtyStr.Replace(",", "."), 
                                    System.Globalization.NumberStyles.Any, 
                                    System.Globalization.CultureInfo.InvariantCulture, out decimal qtyVal))
                                {
                                    qty = qtyVal;
                                }

                                decimal? pricePerUnit = null;
                                if (!string.IsNullOrEmpty(pricePerUnitStr) && decimal.TryParse(pricePerUnitStr.Replace(",", "."), 
                                    System.Globalization.NumberStyles.Any, 
                                    System.Globalization.CultureInfo.InvariantCulture, out decimal priceVal))
                                {
                                    pricePerUnit = priceVal;
                                }

                                var transaction = new FIN_TRANSACTIONS
                                {
                                    TxnDate = txnDate,
                                    AccountID = accountId,
                                    CategoryID = categoryId,
                                    Direction = direction,
                                    Amount = amount,
                                    CurrencyID = currencyId,
                                    CounterpartyName = string.IsNullOrEmpty(counterpartyName) ? null : counterpartyName,
                                    Purpose = string.IsNullOrEmpty(purpose) ? null : purpose,
                                    ContractNo = string.IsNullOrEmpty(contractNo) ? null : contractNo,
                                    Comment = string.IsNullOrEmpty(comment) ? null : comment,
                                    Qty = qty,
                                    PricePerUnit = pricePerUnit,
                                    SourceDocType = "Manual",
                                    CreatedByUserID = currentUserId,
                                    CreatedDT = DateTime.Now
                                };

                                transactions.Add(transaction);
                            }
                            catch (Exception ex)
                            {
                                var errorMsg = $"Qator {row}: {ex.Message}";
                                if (ex.InnerException != null)
                                {
                                    errorMsg += $" ({ex.InnerException.Message})";
                                }
                                errors.Add(errorMsg);
                            }
                        }

                        if (transactions.Count == 0)
                        {
                            ViewBag.Error = "Hech qanday to'g'ri tranzaksiya topilmadi!";
                            if (errors.Count > 0)
                            {
                                ViewBag.Errors = errors;
                            }
                            return View();
                        }

                        // Bulk insert with detailed error handling
                        try
                        {
                            db.FIN_TRANSACTIONS.AddRange(transactions);
                            db.SaveChanges();
                        }
                        catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
                        {
                            var validationErrors = new List<string>();
                            foreach (var validationError in dbEx.EntityValidationErrors)
                            {
                                foreach (var error in validationError.ValidationErrors)
                                {
                                    validationErrors.Add($"Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                                }
                            }
                            ViewBag.Error = $"Ma'lumotlar validatsiyadan o'tmadi: {string.Join("; ", validationErrors)}";
                            if (dbEx.InnerException != null)
                            {
                                ViewBag.Error += $" | Inner Exception: {dbEx.InnerException.Message}";
                            }
                            if (errors.Count > 0)
                            {
                                ViewBag.Errors = errors;
                            }
                            return View();
                        }
                        catch (System.Data.Entity.Infrastructure.DbUpdateException dbUpdateEx)
                        {
                            var updateErrors = new List<string>();
                            updateErrors.Add($"Database update xatolik: {dbUpdateEx.Message}");
                            
                            Exception innerEx = dbUpdateEx.InnerException;
                            while (innerEx != null)
                            {
                                updateErrors.Add($"Inner: {innerEx.Message}");
                                innerEx = innerEx.InnerException;
                            }
                            
                            ViewBag.Error = string.Join(" -> ", updateErrors);
                            if (errors.Count > 0)
                            {
                                ViewBag.Errors = errors;
                            }
                            return View();
                        }

                        ViewBag.Success = $"{transactions.Count} ta tranzaksiya muvaffaqiyatli import qilindi!";
                        if (errors.Count > 0)
                        {
                            ViewBag.Errors = errors;
                            ViewBag.Warning = $"{errors.Count} ta qatorda xatolik bo'ldi.";
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "FinTransactionsController",
                            $"{transactions.Count} ta tranzaksiya Excel dan import qilindi");
                    }
                }

                return View();
            }
            catch (Exception ex)
            {
                // Get all inner exception messages recursively
                var errorMessages = new List<string>();
                Exception currentEx = ex;
                while (currentEx != null)
                {
                    if (!string.IsNullOrEmpty(currentEx.Message))
                    {
                        errorMessages.Add(currentEx.Message);
                    }
                    currentEx = currentEx.InnerException;
                }

                ViewBag.Error = $"Import jarayonida xatolik: {string.Join(" -> ", errorMessages)}";
                
                // Also log full exception details for debugging
                try
                {
                    LogHelper.LogToDatabase(User.Identity.Name, "FinTransactionsController",
                        $"Import xatolik: {ex.Message}", ex.ToString());
                }
                catch
                {
                    // Ignore logging errors
                }

                return View();
            }
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
                    return Url.Action("Details", "WhIncome", new { id = sourceDocID });
                case "ExpenseDoc":
                    return Url.Action("Details", "WhExpense", new { id = sourceDocID });
                case "ShareholderContribution":
                    return Url.Action("Details", "Shareholders", new { id = sourceDocID });
                case "WaybillPayment":
                    return Url.Action("Details", "WaybillPayment", new { id = sourceDocID });
                default:
                    return null;
            }
        }
    }
}
