using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers.Finance
{
    [Permission("FinAccounts")]
    public class FinAccountsController : Controller
    {
        // Olish so'rovi: FinAccounts
        public ActionResult Index(string searchTerm, int? currencyID, string accountType, string sortOrder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.FIN_ACCOUNTS
                    .Include(a => a.CURRENCy)
                    .Where(a => a.IsActive);

                // Search filtrlash
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(a =>
                        (a.Code != null && a.Code.Contains(searchTerm)) ||
                        (a.Name != null && a.Name.Contains(searchTerm)) ||
                        (a.OwnerName != null && a.OwnerName.Contains(searchTerm)));
                }

                // Currency filtrlash
                if (currencyID.HasValue)
                {
                    query = query.Where(a => a.CurrencyID == currencyID.Value);
                }

                // Account type filtrlash
                if (!string.IsNullOrEmpty(accountType))
                {
                    if (accountType == "Cash")
                        query = query.Where(a => a.IsCash);
                    else if (accountType == "Bank")
                        query = query.Where(a => a.IsBank);
                    else if (accountType == "Other")
                        query = query.Where(a => !a.IsCash && !a.IsBank);
                }

                // Sorting
                switch (sortOrder)
                {
                    case "code_desc":
                        query = query.OrderByDescending(a => a.Code);
                        break;
                    case "name_desc":
                        query = query.OrderByDescending(a => a.Name);
                        break;
                    case "code":
                        query = query.OrderBy(a => a.Code);
                        break;
                    default:
                        query = query.OrderBy(a => a.Name);
                        break;
                }

                // Hisoblar soni kam — hammasi qaytariladi, sahifalashni DataTables qiladi.
                // Avval server 20 tadan kesardi, view'da esa pager yo'q edi: 21-hisobdan boshlab ro'yxatda ko'rinmasdi (FIN-B13).
                var accounts = query.ToList();

                // ViewBag uchun filters
                ViewBag.CurrencyList = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", currencyID);
                ViewBag.AccountTypeList = new SelectList(new[] {
                    new { Value = "", Text = "Barcha" },
                    new { Value = "Cash", Text = "Naqd pul" },
                    new { Value = "Bank", Text = "Bank" },
                    new { Value = "Other", Text = "Boshqa" }
                }, "Value", "Text", accountType);
                ViewBag.SearchTerm = searchTerm;
                ViewBag.SortOrder = sortOrder;

                return View(accounts);
            }
        }

        // Olish so'rovi: FinAccounts/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var account = db.FIN_ACCOUNTS
                    .Where(a => a.ID == id && (a.IsDeleted == false || a.IsDeleted == null))
                    .Include(a => a.CURRENCy)
                    .Include(a => a.USER)
                    .FirstOrDefault();

                if (account == null)
                {
                    return HttpNotFound();
                }

                // Ensure navigation properties are loaded
                if (account.CURRENCy == null && account.CurrencyID > 0)
                {
                    account.CURRENCy = db.CURRENCIES.Find(account.CurrencyID);
                }
                
                if (account.USER == null && account.CreatedByUserID > 0)
                {
                    account.USER = db.USERS.Find(account.CreatedByUserID);
                }

                // Olish transaction count
                ViewBag.TransactionCount = db.FIN_TRANSACTIONS.Count(t => t.AccountID == id && t.IsDeleted != true);

                return View(account);
            }
        }

        // Olish so'rovi: FinAccounts/yaratish
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.CurrencyID = new SelectList(db.CURRENCIES.ToList(), "ID", "Name");
                ViewBag.BalanceCategoryID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES
                        .Where(c => c.IsActive && c.CalculationType == 1) // Faqat Account-Based
                        .OrderBy(c => c.Name)
                        .ToList(),
                    "ID", "Name");
                return View();
            }
        }

        // POST so'rovi: FinAccounts/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FIN_ACCOUNTS account)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Tekshirish code uniqueness
                if (db.FIN_ACCOUNTS.Any(a => a.Code == account.Code && a.IsActive))
                {
                    ModelState.AddModelError("Code", "Bu kod bilan hisob allaqachon mavjud.");
                }

                if (ModelState.IsValid)
                {
                    account.IsActive = true;
                    account.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                    account.CreatedDT = DateTime.Now;

                    db.FIN_ACCOUNTS.Add(account);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinAccounts", "Create", $"Hisob yaratildi: {account.Name} ({account.Code})");

                    return RedirectToAction("Index");
                }

                ViewBag.CurrencyID = new SelectList(db.CURRENCIES.ToList(), "ID", "Name", account.CurrencyID);
                ViewBag.BalanceCategoryID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES
                        .Where(c => c.IsActive && c.CalculationType == 1) // Faqat Account-Based
                        .OrderBy(c => c.Name)
                        .ToList(),
                    "ID", "Name", account.BalanceCategoryID);
                return View(account);
            }
        }

        // Olish so'rovi: FinAccounts/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var account = db.FIN_ACCOUNTS.Find(id);
                if (account == null)
                {
                    return HttpNotFound();
                }

                ViewBag.CurrencyID = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", account.CurrencyID);
                ViewBag.BalanceCategoryID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES
                        .Where(c => c.IsActive && c.CalculationType == 1) // Faqat Account-Based
                        .OrderBy(c => c.Name)
                        .ToList(),
                    "ID", "Name", account.BalanceCategoryID);
                return View(account);
            }
        }

        // POST so'rovi: FinAccounts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FIN_ACCOUNTS account)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Tekshirish code uniqueness (excluding current record)
                if (db.FIN_ACCOUNTS.Any(a => a.Code == account.Code && a.ID != account.ID && a.IsActive))
                {
                    ModelState.AddModelError("Code", "Bu kod bilan hisob allaqachon mavjud.");
                }

                if (ModelState.IsValid)
                {
                    var accountToUpdate = db.FIN_ACCOUNTS.Find(account.ID);
                    if (accountToUpdate != null)
                    {
                        accountToUpdate.Code = account.Code;
                        accountToUpdate.Name = account.Name;
                        accountToUpdate.OwnerName = account.OwnerName;
                        accountToUpdate.CurrencyID = account.CurrencyID;
                        accountToUpdate.IsCash = account.IsCash;
                        accountToUpdate.IsBank = account.IsBank;
                        accountToUpdate.BalanceCategoryID = account.BalanceCategoryID;

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "FinAccounts", "Edit", $"Hisob tahrirlandi: {accountToUpdate.Name} ({accountToUpdate.Code})");

                        return RedirectToAction("Index");
                    }
                }

                ViewBag.CurrencyID = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", account.CurrencyID);
                ViewBag.BalanceCategoryID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES
                        .Where(c => c.IsActive && c.CalculationType == 1) // Faqat Account-Based
                        .OrderBy(c => c.Name)
                        .ToList(),
                    "ID", "Name", account.BalanceCategoryID);
                return View(account);
            }
        }

        // Olish so'rovi: FinAccounts/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var account = db.FIN_ACCOUNTS
                    .Include(a => a.CURRENCy)
                    .FirstOrDefault(a => a.ID == id);

                if (account == null)
                {
                    return HttpNotFound();
                }

                // Tekshirish agar account has transactions
                ViewBag.HasTransactions = db.FIN_TRANSACTIONS.Any(t => t.AccountID == id && t.IsDeleted != true);

                return View(account);
            }
        }

        // POST so'rovi: FinAccounts/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var account = db.FIN_ACCOUNTS.Find(id);
                if (account != null)
                {
                    // Soft o'chirish
                    account.IsActive = false;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinAccounts", "Delete", $"Hisob o'chirildi: {account.Name} ({account.Code})");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: FinAccounts/Transactions/5
        [Permission("FinTransactions")]
        public ActionResult Transactions(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            return RedirectToAction("Index", "FinTransactions", new { accountID = id });
        }
    }
}
