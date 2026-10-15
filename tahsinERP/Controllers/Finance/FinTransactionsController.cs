using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Controllers;
using ClosedXML.Excel;
using System.IO;
using System.Collections.Generic;
using System.Web;

namespace tahsinERP.Controllers.Finance
{
    public class FinTransactionsController : Controller
    {
        // Olish so'rovi: FinTransactions
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

                // Date range filtrlash
                if (fromDate.HasValue)
                {
                    query = query.Where(t => t.TxnDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(t => t.TxnDate <= toDate.Value);
                }

                // Account filtrlash
                if (accountID.HasValue)
                {
                    query = query.Where(t => t.AccountID == accountID.Value);
                }

                // Category filtrlash
                if (categoryID.HasValue)
                {
                    query = query.Where(t => t.CategoryID == categoryID.Value);
                }

                // Direction filtrlash (1=In, 2=Out)
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

                // Source hujjat type filtrlash
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

                // standart sorting - DataTable will handle client-side sorting
                query = query.OrderByDescending(t => t.TxnDate);

                // Yuklash barcha transactions uchun client-side DataTable (no server-side pagination)
                var transactions = query.ToList();

                // Ensure navigation properties are loaded uchun barcha transactions
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

                // ViewBag uchun filters
                ViewBag.AccountList = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", accountID);

                // Build category tree uchun dropdown
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

        // Olish so'rovi: FinTransactions/Details/5
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
                    .FirstOrDefault(t => t.ID == id && (t.IsDeleted == false || t.IsDeleted == null));

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

                // Olish source hujjat link agar available
                if (!string.IsNullOrEmpty(transaction.SourceDocType) && transaction.SourceDocID.HasValue)
                {
                    ViewBag.SourceDocLink = GetSourceDocLink(transaction.SourceDocType, transaction.SourceDocID.Value);
                }

                return View(transaction);
            }
        }

        // Olish so'rovi: FinTransactions/yaratish
        public ActionResult Create(int? accountID, int? categoryID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.AccountsSelectList = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", accountID);

                ViewBag.CategoriesSelectList = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", categoryID);

                ViewBag.CurrenciesSelectList = new SelectList(
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

                ViewBag.SourceDocTypeList = new SelectList(new[] {
                    new { Value = "Manual", Text = "Qo'lda" },
                    new { Value = "RecurringObligationPayment", Text = "Doimiy harajat to'lovi (ijara, buxgalteriya va h.k.)" },
                    new { Value = "PurchaseInvoicePayment", Text = "Ta'minotchiga to'lov (xarid invoysiga bog'lash)" }
                }, "Value", "Text", "Manual");

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

                return View(model);
            }
        }

        // POST so'rovi: FinTransactions/yaratish
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
                // Tekshirish account currency matches transaction currency
                var account = db.FIN_ACCOUNTS.Find(transaction.AccountID);
                if (account != null && account.CurrencyID != transaction.CurrencyID)
                {
                    ModelState.AddModelError("CurrencyID", "Hisob valyutasi bilan tranzaksiya valyutasi mos kelmaydi.");
                }

                // Ta'minotchiga to'lov bo'lsa, kontragent va invoys bog'lanishi majburiy.
                if (string.Equals(transaction.SourceDocType, "PurchaseInvoicePayment", StringComparison.OrdinalIgnoreCase))
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

                ViewBag.AccountsSelectList = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name),
                    "ID", "Name", transaction.AccountID);

                ViewBag.CategoriesSelectList = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name),
                    "ID", "Name", transaction.CategoryID);

                ViewBag.CurrenciesSelectList = new SelectList(
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

                ViewBag.SourceDocTypeList = new SelectList(new[] {
                    new { Value = "Manual", Text = "Qo'lda" },
                    new { Value = "RecurringObligationPayment", Text = "Doimiy harajat to'lovi (ijara, buxgalteriya va h.k.)" },
                    new { Value = "PurchaseInvoicePayment", Text = "Ta'minotchiga to'lov (xarid invoysiga bog'lash)" }
                }, "Value", "Text", transaction.SourceDocType ?? "Manual");

                return View(transaction);
            }
        }

        // AJAX: kontragent turiga qarab ro'yxat qaytarish
        [HttpGet]
        public JsonResult GetCounterparties(byte? type)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" }, JsonRequestBehavior.AllowGet);
            }

            if (!type.HasValue)
            {
                return Json(new { success = true, items = new object[0] }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new DBTHSNEntities())
            {
                switch (type.Value)
                {
                    case 1: // Mijoz
                        return Json(new
                        {
                            success = true,
                            items = db.CUSTOMERS
                                .Where(c => c.IsDeleted != true)
                                .OrderBy(c => c.Name)
                                .Select(c => new { id = c.ID, name = c.Name })
                                .ToList()
                        }, JsonRequestBehavior.AllowGet);
                    case 2: // Ta'minotchi
                        return Json(new
                        {
                            success = true,
                            items = db.SUPPLIERS
                                .Where(s => s.IsDeleted != true)
                                .OrderBy(s => s.Name)
                                .Select(s => new { id = s.ID, name = s.Name })
                                .ToList()
                        }, JsonRequestBehavior.AllowGet);
                    case 3: // Aksiyador
                        return Json(new
                        {
                            success = true,
                            items = db.SHAREHOLDERS
                                .Where(s => s.IsDeleted != true)
                                .OrderBy(s => s.FullName)
                                .Select(s => new { id = s.ID, name = s.FullName })
                                .ToList()
                        }, JsonRequestBehavior.AllowGet);
                    case 4: // Forwarder
                        return Json(new
                        {
                            success = true,
                            items = db.FORWARDERS
                                .Where(f => f.IsDeleted != true)
                                .OrderBy(f => f.ForwarderName)
                                .Select(f => new { id = f.ID, name = f.ForwarderName })
                                .ToList()
                        }, JsonRequestBehavior.AllowGet);
                    case 6: // Xodim
                        return Json(new
                        {
                            success = true,
                            items = db.HR_EMPLOYEES
                                .Where(e => e.IsDeleted != true && e.IsActive)
                                .OrderBy(e => e.FullName)
                                .Select(e => new { id = e.ID, name = e.FullName })
                                .ToList()
                        }, JsonRequestBehavior.AllowGet);
                    default:
                        return Json(new { success = true, items = new object[0] }, JsonRequestBehavior.AllowGet);
                }
            }
        }

        /// <summary>
        /// Kontragent turi/id bo'yicha shartnomalar ro'yxati.
        /// type: 1=Mijoz(S_CONTRACTS), 2=Ta'minotchi(P_CONTRACTS), 4=Forwarder(F_CONTRACTS)
        /// </summary>
        [HttpGet]
        public JsonResult GetContractsByCounterparty(byte? type, int? counterpartyId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" }, JsonRequestBehavior.AllowGet);
            }

            if (!type.HasValue || !counterpartyId.HasValue || counterpartyId.Value <= 0)
            {
                return Json(new { success = true, data = new object[0] }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new DBTHSNEntities())
            {
                switch (type.Value)
                {
                    case 1: // Mijoz
                        var salesContracts = db.S_CONTRACTS
                            .Where(c => c.CustomerID == counterpartyId.Value && (c.IsDeleted == false || c.IsDeleted == null))
                            .OrderByDescending(c => c.IssuedDate)
                            .Select(c => new { ID = c.ID, ContractNo = c.ContractNo })
                            .ToList();
                        return Json(new { success = true, data = salesContracts }, JsonRequestBehavior.AllowGet);
                    case 2: // Ta'minotchi
                        var purchaseContracts = db.P_CONTRACTS
                            .Where(c => c.SupplierID == counterpartyId.Value && c.IsDeleted != true)
                            .OrderByDescending(c => c.IssuedDate)
                            .Select(c => new { ID = c.ID, ContractNo = c.ContractNo })
                            .ToList();
                        return Json(new { success = true, data = purchaseContracts }, JsonRequestBehavior.AllowGet);
                    case 4: // Forwarder
                        var forwarderContracts = db.F_CONTRACTS
                            .Where(c => c.ForwarderID == counterpartyId.Value && !c.IsDeleted)
                            .OrderByDescending(c => c.IssueDate)
                            .Select(c => new { ID = c.ID, ContractNo = c.ContractNo })
                            .ToList();
                        return Json(new { success = true, data = forwarderContracts }, JsonRequestBehavior.AllowGet);
                    default:
                        return Json(new { success = true, data = new object[0] }, JsonRequestBehavior.AllowGet);
                }
            }
        }

        /// <summary>
        /// Backward compatibility: eski endpoint (faqat ta'minotchi).
        /// </summary>
        [HttpGet]
        public JsonResult GetContractsBySupplier(int supplierId)
        {
            return GetContractsByCounterparty(2, supplierId);
        }

        // Olish so'rovi: FinTransactions/Edit/5
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
                var transaction = db.FIN_TRANSACTIONS
                    .FirstOrDefault(t => t.ID == id && (t.IsDeleted == false || t.IsDeleted == null));
                if (transaction == null)
                {
                    return HttpNotFound();
                }
                ViewBag.AccountsSelectList = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", transaction.AccountID);

                ViewBag.CategoriesSelectList = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", transaction.CategoryID);

                ViewBag.CurrenciesSelectList = new SelectList(
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

                ViewBag.SourceDocTypeList = BuildFinTransactionSourceDocTypeSelectList(transaction.SourceDocType);

                return View(transaction);
            }
        }

        // POST so'rovi: FinTransactions/Edit/5
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
                var existing = db.FIN_TRANSACTIONS
                    .FirstOrDefault(t => t.ID == transaction.ID && (t.IsDeleted == false || t.IsDeleted == null));
                if (existing == null)
                {
                    return HttpNotFound();
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
                    existing.SourceDocType = transaction.SourceDocType;
                    existing.SourceDocID = transaction.SourceDocID;

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinTransactions", "Edit", 
                        $"Tranzaksiya tahrirlandi: ID {transaction.ID}");

                    return RedirectToAction("Index");
                }

                ViewBag.AccountsSelectList = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", transaction.AccountID);

                ViewBag.CategoriesSelectList = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", transaction.CategoryID);

                ViewBag.CurrenciesSelectList = new SelectList(
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

                ViewBag.SourceDocTypeList = BuildFinTransactionSourceDocTypeSelectList(transaction.SourceDocType);

                return View(transaction);
            }
        }

        // Olish so'rovi: FinTransactions/o'chirish/5
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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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

                    var sourceDocType = (transaction.SourceDocType ?? string.Empty).Trim();
                    var isManual = string.IsNullOrEmpty(sourceDocType) ||
                                   string.Equals(sourceDocType, "Manual", StringComparison.OrdinalIgnoreCase);

                    // Faqat qo'lda (Manual) yaratilgan tranzaksiyani o'chirishga ruxsat.
                    if (!isManual)
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

        // Olish so'rovi: FinTransactions/ExportExcel
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
                    .Where(t => t.IsDeleted == false || t.IsDeleted == null)
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

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Tranzaksiyalar");
                    worksheet.Cell(1, 1).Value = "Sana";
                    worksheet.Cell(1, 2).Value = "Hisob";
                    worksheet.Cell(1, 3).Value = "Kategoriya";
                    worksheet.Cell(1, 4).Value = "Yo'nalish";
                    worksheet.Cell(1, 5).Value = "Summa";
                    worksheet.Cell(1, 6).Value = "Valyuta";
                    worksheet.Cell(1, 7).Value = "Kontragent";
                    worksheet.Cell(1, 8).Value = "Maqsad";
                    worksheet.Cell(1, 9).Value = "Manba hujjat";

                    int row = 2;
                    foreach (var txn in transactions)
                    {
                        worksheet.Cell(row, 1).Value = txn.TxnDate.ToString("yyyy-MM-dd");
                        worksheet.Cell(row, 2).Value = txn.FIN_ACCOUNTS?.Name;
                        worksheet.Cell(row, 3).Value = txn.FIN_CATEGORIES?.Name;
                        worksheet.Cell(row, 4).Value = txn.Direction == 1 ? "Kirim" : "Chiqim";
                        worksheet.Cell(row, 5).Value = txn.Amount;
                        worksheet.Cell(row, 6).Value = txn.CURRENCy?.Shortname;
                        worksheet.Cell(row, 7).Value = txn.CounterpartyName;
                        worksheet.Cell(row, 8).Value = txn.Purpose;
                        worksheet.Cell(row, 9).Value = txn.SourceDocType;
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();

                    var stream = new MemoryStream();
                    workbook.SaveAs(stream);
                    stream.Position = 0;

                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"FinTransactions_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }

        // Olish so'rovi: FinTransactions/Import
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

        // Olish so'rovi: FinTransactions/DownloadTemplate
        public ActionResult DownloadTemplate()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinTransactions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Tranzaksiyalar");

                    // Header qator
                    worksheet.Cell(1, 1).Value = "Sana (DD.MM.YYYY)";
                    worksheet.Cell(1, 2).Value = "Vaqt (HH:mm)";
                    worksheet.Cell(1, 3).Value = "Hisob kodi";
                    worksheet.Cell(1, 4).Value = "Kategoriya nomi";
                    worksheet.Cell(1, 5).Value = "Yo'nalish (1=Kirim, 2=Chiqim)";
                    worksheet.Cell(1, 6).Value = "Summa";
                    var defaultCurrency = db.CURRENCIES
                        .Where(c => c.IsDeleted != true)
                        .OrderBy(c => c.ID)
                        .FirstOrDefault();
                    var currencyShortname = defaultCurrency?.Shortname ?? "UZS";

                    worksheet.Cell(1, 7).Value = $"Valyuta qisqa nomi (masalan: {currencyShortname})";
                    worksheet.Cell(1, 8).Value = "Kontragent nomi (ixtiyoriy)";
                    worksheet.Cell(1, 9).Value = "Maqsad (ixtiyoriy)";
                    worksheet.Cell(1, 10).Value = "Shartnoma raqami (ixtiyoriy)";
                    worksheet.Cell(1, 11).Value = "Izoh (ixtiyoriy)";
                    worksheet.Cell(1, 12).Value = "Miqdor (ixtiyoriy)";
                    worksheet.Cell(1, 13).Value = "Narx birligi (ixtiyoriy)";

                    // Style header
                    var range = worksheet.Range(1, 1, 1, 13);
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = XLFillPatternValues.Solid;
                    range.Style.Fill.BackgroundColor = XLColor.FromColor(System.Drawing.Color.LightGray);

                    // Qo'shish example qator
                    worksheet.Cell(2, 1).Value = "01.11.2025";
                    worksheet.Cell(2, 2).Value = "10:30";
                    worksheet.Cell(2, 3).Value = "9000";
                    worksheet.Cell(2, 4).Value = "Sotuv";
                    worksheet.Cell(2, 5).Value = "1";
                    worksheet.Cell(2, 6).Value = "1000000.00";
                    worksheet.Cell(2, 7).Value = "UZS";
                    worksheet.Cell(2, 8).Value = "Mijoz nomi";
                    worksheet.Cell(2, 9).Value = "Mahsulot sotuvi";
                    worksheet.Cell(2, 10).Value = "K-001";
                    worksheet.Cell(2, 11).Value = "Qo'shimcha izoh";

                    // Olish available accounts va categories uchun reference
                    var accounts = db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Code).ToList();
                    var categories = db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
                    var currencies = db.CURRENCIES.OrderBy(c => c.Shortname).ToList();

                    // Qo'shish reference varaqlar
                    var accountsSheet = workbook.Worksheets.Add("Hisoblar");
                    accountsSheet.Cell(1, 1).Value = "Kod";
                    accountsSheet.Cell(1, 2).Value = "Nomi";
                    int accRow = 2;
                    foreach (var acc in accounts)
                    {
                        accountsSheet.Cell(accRow, 1).Value = acc.Code;
                        accountsSheet.Cell(accRow, 2).Value = acc.Name;
                        accRow++;
                    }

                    var categoriesSheet = workbook.Worksheets.Add("Kategoriyalar");
                    categoriesSheet.Cell(1, 1).Value = "Nomi";
                    int catRow = 2;
                    foreach (var cat in categories)
                    {
                        categoriesSheet.Cell(catRow, 1).Value = cat.Name;
                        catRow++;
                    }

                    var currenciesSheet = workbook.Worksheets.Add("Valyutalar");
                    currenciesSheet.Cell(1, 1).Value = "Qisqa nomi";
                    currenciesSheet.Cell(1, 2).Value = "To'liq nomi";
                    int currRow = 2;
                    foreach (var curr in currencies)
                    {
                        currenciesSheet.Cell(currRow, 1).Value = curr.Shortname;
                        currenciesSheet.Cell(currRow, 2).Value = curr.Shortname;
                        currRow++;
                    }

                    worksheet.Columns().AdjustToContents();
                    accountsSheet.Columns().AdjustToContents();
                    categoriesSheet.Columns().AdjustToContents();
                    currenciesSheet.Columns().AdjustToContents();

                    var stream = new MemoryStream();
                    workbook.SaveAs(stream);
                    stream.Position = 0;

                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "FinTransactions_Template.xlsx");
                }
            }
        }

        // POST so'rovi: FinTransactions/Import
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
                var transactions = new List<FIN_TRANSACTIONS>();
                var errors = new List<string>();

                using (var workbook = new XLWorkbook(file.InputStream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rowCount = worksheet.LastRowNumber();

                    if (rowCount < 2)
                    {
                        ViewBag.Error = "Excel faylda ma'lumot yo'q!";
                        return View();
                    }

                    // Olish lookup dictionaries va current user
                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        // Use UserHelper ga get current user ID (standard approach in this codebase)
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
                                // O'qish date - try ga get as DateTime value birinchi (Excel stores dates as numbers)
                                DateTime txnDate;
                                var dateCell = worksheet.Cell(row, 1);
                                var dateValue = dateCell.GetRawValue();
                                
                                // Avval try ga get as DateTime value (agar Excel stored it as date)
                                if (dateValue != null)
                                {
                                    if (dateValue is DateTime dateTimeValue)
                                    {
                                        txnDate = dateTimeValue;
                                    }
                                    else if (dateValue is double dateDouble)
                                    {
                                        // Excel stores dates as serial numbers (days since 1900-01-01)
                                        txnDate = DateTime.FromOADate(dateDouble);
                                    }
                                    else
                                    {
                                        // Urinish ga tahlil as string
                                        var dateStr = dateValue.ToString()?.Trim();
                                        if (string.IsNullOrEmpty(dateStr))
                                        {
                                            continue; // Skip empty qatorlar
                                        }
                                        
                                        // Urinish multiple date formats
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
                                    // Cell is empty yoki null
                                    var dateStr = dateCell.GetFormattedString().Trim();
                                    if (string.IsNullOrEmpty(dateStr))
                                    {
                                        continue; // Skip empty qatorlar
                                    }
                                    
                                    // Urinish multiple date formats
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

                                // O'qish time - try ga get as DateTime value birinchi
                                var timeCell = worksheet.Cell(row, 2);
                                var timeValue = timeCell.GetRawValue();
                                if (timeValue != null)
                                {
                                    if (timeValue is DateTime timeDateTime)
                                    {
                                        txnDate = txnDate.Date.Add(timeDateTime.TimeOfDay);
                                    }
                                    else if (timeValue is double timeDouble && timeDouble < 1.0)
                                    {
                                        // Excel stores time as fraction of day (0.0 ga 0.999...)
                                        var timeSpan = TimeSpan.FromDays(timeDouble);
                                        txnDate = txnDate.Date.Add(timeSpan);
                                    }
                                    else
                                    {
                                        var timeStr = timeValue.ToString()?.Trim();
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
                                    var timeStr = timeCell.GetFormattedString().Trim();
                                    if (!string.IsNullOrEmpty(timeStr))
                                    {
                                        TimeSpan time;
                                        if (TimeSpan.TryParse(timeStr, out time))
                                        {
                                            txnDate = txnDate.Date.Add(time);
                                        }
                                    }
                                }

                                // O'qish account code
                                var accountCode = worksheet.Cell(row, 3).GetFormattedString()?.Trim();
                                if (string.IsNullOrEmpty(accountCode) || !accounts.ContainsKey(accountCode))
                                {
                                    errors.Add($"Qator {row}: Hisob kodi topilmadi - {accountCode}");
                                    continue;
                                }
                                int accountId = accounts[accountCode];

                                // O'qish category name
                                var categoryName = worksheet.Cell(row, 4).GetFormattedString()?.Trim();
                                if (string.IsNullOrEmpty(categoryName) || !categories.ContainsKey(categoryName))
                                {
                                    errors.Add($"Qator {row}: Kategoriya topilmadi - {categoryName}");
                                    continue;
                                }
                                int categoryId = categories[categoryName];

                                // O'qish direction
                                var directionStr = worksheet.Cell(row, 5).GetFormattedString()?.Trim();
                                byte direction;
                                if (!byte.TryParse(directionStr, out direction) || (direction != 1 && direction != 2))
                                {
                                    errors.Add($"Qator {row}: Yo'nalish noto'g'ri (1 yoki 2 bo'lishi kerak) - {directionStr}");
                                    continue;
                                }

                                // O'qish amount
                                var amountStr = worksheet.Cell(row, 6).GetFormattedString()?.Trim();
                                if (string.IsNullOrEmpty(amountStr) || !decimal.TryParse(amountStr.Replace(" ", "").Replace(",", "."), 
                                    System.Globalization.NumberStyles.Any, 
                                    System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
                                {
                                    errors.Add($"Qator {row}: Summa noto'g'ri - {amountStr}");
                                    continue;
                                }

                                // O'qish currency
                                var currencyShortname = worksheet.Cell(row, 7).GetFormattedString()?.Trim();
                                if (string.IsNullOrEmpty(currencyShortname) || !currencies.ContainsKey(currencyShortname))
                                {
                                    errors.Add($"Qator {row}: Valyuta topilmadi - {currencyShortname}");
                                    continue;
                                }
                                int currencyId = currencies[currencyShortname];

                                // O'qish optional fields
                                var counterpartyName = worksheet.Cell(row, 8).GetFormattedString()?.Trim();
                                var purpose = worksheet.Cell(row, 9).GetFormattedString()?.Trim();
                                var contractNo = worksheet.Cell(row, 10).GetFormattedString()?.Trim();
                                var comment = worksheet.Cell(row, 11).GetFormattedString()?.Trim();
                                var qtyStr = worksheet.Cell(row, 12).GetFormattedString()?.Trim();
                                var pricePerUnitStr = worksheet.Cell(row, 13).GetFormattedString()?.Trim();

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

                        // Bulk insert bilan detailed xatolik handling
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
                                    validationErrors.Add($"Maydon: {error.PropertyName}, Xatolik: {error.ErrorMessage}");
                                }
                            }
                            ViewBag.Error = $"Ma'lumotlar validatsiyadan o'tmadi: {string.Join("; ", validationErrors)}";
                            if (dbEx.InnerException != null)
                            {
                                ViewBag.Error += $" | Ichki xatolik: {dbEx.InnerException.Message}";
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
                            updateErrors.Add($"Ma'lumotlar bazasini yangilashda xatolik: {dbUpdateEx.Message}");
                            
                            Exception innerEx = dbUpdateEx.InnerException;
                            while (innerEx != null)
                            {
                                updateErrors.Add($"Ichki xatolik: {innerEx.Message}");
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
                // Barcha ichki xatolik xabarlarini rekursiv tarzda yig'ish
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
                
                // Also log full exception details uchun debugging
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

        private static SelectList BuildFinTransactionSourceDocTypeSelectList(string current)
        {
            var items = new List<object>
            {
                new { Value = "Manual", Text = "Qo'lda" },
                new { Value = "RecurringObligationPayment", Text = "Doimiy harajat to'lovi (ijara, buxgalteriya va h.k.)" },
                new { Value = "PurchaseInvoicePayment", Text = "Ta'minotchiga to'lov (xarid invoysiga bog'lash)" },
                new { Value = "SalesInvoice", Text = "Sotuv invoysi" },
                new { Value = "PurchaseInvoice", Text = "Xarid invoysi" },
                new { Value = "IncomeDoc", Text = "Ombor kirimi" },
                new { Value = "ExpenseDoc", Text = "Ombor chiqimi" },
                new { Value = "WaybillPayment", Text = "Yo'l varaqasi to'lovi" },
                new { Value = "ShareholderContribution", Text = "Aksiyador hissasi" }
            };
            var selected = string.IsNullOrWhiteSpace(current) ? "Manual" : current.Trim();
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Manual", "RecurringObligationPayment", "PurchaseInvoicePayment", "SalesInvoice", "PurchaseInvoice",
                "IncomeDoc", "ExpenseDoc", "WaybillPayment", "ShareholderContribution"
            };
            if (!known.Contains(selected))
            {
                items.Insert(0, new { Value = selected, Text = selected + " (maxsus)" });
            }
            return new SelectList(items, "Value", "Text", selected);
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
