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

                // Sorting
                switch (sortOrder)
                {
                    case "date_desc":
                        query = query.OrderByDescending(t => t.TxnDate);
                        break;
                    case "amount_desc":
                        query = query.OrderByDescending(t => t.Amount);
                        break;
                    case "amount":
                        query = query.OrderBy(t => t.Amount);
                        break;
                    default:
                        query = query.OrderByDescending(t => t.TxnDate);
                        break;
                }

                // Pagination
                pageSize = pageSize ?? 25;
                page = page ?? 1;
                ViewBag.PageSize = pageSize;
                ViewBag.CurrentPage = page;
                ViewBag.TotalRecords = query.Count();

                var transactions = query
                    .Skip((page.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value)
                    .ToList();

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
                    "ID", "Name");

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
                    "ID", "Name", transaction.CurrencyID);

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
                    "ID", "Name", transaction.CurrencyID);

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
                    "ID", "Name", transaction.CurrencyID);

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
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .FirstOrDefault(t => t.ID == id);

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
                        db.FIN_TRANSACTIONS.Remove(transaction);
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
