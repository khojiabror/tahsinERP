using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers.Finance
{
    [Permission("Shareholder")]
    public class ShareholderController : Controller
    {
        // Olish so'rovi: Shareholder
        public ActionResult Index(string searchTerm)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.SHAREHOLDERS.Where(s => s.IsActive && s.IsDeleted != true); // FIN-B12: o'chirilganlar ro'yxatda qolmasin

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(s => s.FullName.Contains(searchTerm) || 
                        (s.Phone != null && s.Phone.Contains(searchTerm)));
                }

                var shareholders = query.OrderBy(s => s.FullName).ToList();
                ViewBag.SearchTerm = searchTerm;

                return View(shareholders);
            }
        }

        // Olish so'rovi: Shareholder/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var shareholder = db.SHAREHOLDERS
                    .Where(s => s.ID == id && (s.IsDeleted == false || s.IsDeleted == null))
                    .Include(s => s.SHAREHOLDER_SHARE_PERIODS)
                    .Include(s => s.SHAREHOLDER_CONTRIBUTIONS)
                    .FirstOrDefault();

                if (shareholder == null)
                {
                    return HttpNotFound();
                }

                // Olish current share percentage
                var currentShare = shareholder.SHAREHOLDER_SHARE_PERIODS
                    .Where(sp => sp.FromDate <= DateTime.Now && 
                        (sp.ToDate == null || sp.ToDate >= DateTime.Now))
                    .OrderByDescending(sp => sp.FromDate)
                    .FirstOrDefault();

                ViewBag.CurrentSharePercent = currentShare?.SharePercent ?? 0;
                
                // Yuklash contributions bilan CURRENCy navigation property included
                var contributions = db.SHAREHOLDER_CONTRIBUTIONS
                    .Include(c => c.CURRENCy)
                    .Where(c => c.ShareholderID == id.Value)
                    .OrderByDescending(c => c.ContributionDate)
                    .ToList();
                
                // Ensure barcha CURRENCy navigation properties are loaded
                var currencyIds = contributions.Where(c => c.CurrencyID > 0).Select(c => c.CurrencyID).Distinct().ToList();
                var currencies = db.CURRENCIES.Where(c => currencyIds.Contains(c.ID)).ToDictionary(c => c.ID, c => c);
                
                foreach (var contrib in contributions)
                {
                    if (contrib.CURRENCy == null && contrib.CurrencyID > 0 && currencies.ContainsKey(contrib.CurrencyID))
                    {
                        contrib.CURRENCy = currencies[contrib.CurrencyID];
                    }
                }
                
                ViewBag.Contributions = contributions;

                return View(shareholder);
            }
        }

        // Olish so'rovi: Shareholder/yaratish
        public ActionResult Create()
        {
            return View();
        }

        // POST so'rovi: Shareholder/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SHAREHOLDER shareholder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    shareholder.IsActive = true;
                    db.SHAREHOLDERS.Add(shareholder);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "Shareholder", "Create", $"Aksiyador yaratildi: {shareholder.FullName}");

                    return RedirectToAction("Index");
                }

                return View(shareholder);
            }
        }

        // Olish so'rovi: Shareholder/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var shareholder = db.SHAREHOLDERS.Find(id);
                if (shareholder == null)
                {
                    return HttpNotFound();
                }

                return View(shareholder);
            }
        }

        // POST so'rovi: Shareholder/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SHAREHOLDER shareholder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var shareholderToUpdate = db.SHAREHOLDERS.Find(shareholder.ID);
                    if (shareholderToUpdate != null)
                    {
                        shareholderToUpdate.FullName = shareholder.FullName;
                        shareholderToUpdate.Phone = shareholder.Phone;
                        shareholderToUpdate.Notes = shareholder.Notes;
                        shareholderToUpdate.IsActive = shareholder.IsActive;

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "Shareholder", "Edit", $"Aksiyador tahrirlandi: {shareholderToUpdate.FullName}");

                        return RedirectToAction("Index");
                    }
                }

                return View(shareholder);
            }
        }

        // Olish so'rovi: Shareholder/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var shareholder = db.SHAREHOLDERS.Find(id);
                if (shareholder == null)
                {
                    return HttpNotFound();
                }

                ViewBag.HasContributions = db.SHAREHOLDER_CONTRIBUTIONS.Any(c => c.ShareholderID == id);
                ViewBag.HasSharePeriods = db.SHAREHOLDER_SHARE_PERIODS.Any(sp => sp.ShareholderID == id);

                return View(shareholder);
            }
        }

        // POST so'rovi: Shareholder/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var shareholder = db.SHAREHOLDERS.Find(id);
                if (shareholder != null)
                {
                    shareholder.IsDeleted = true;
                    db.Entry(shareholder).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "Shareholder", "Delete", $"Aksiyador o'chirildi: {shareholder.FullName}");

                    return RedirectToAction("Index");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: Shareholder/ManageSharePeriods/5
        [Permission("Shareholder", Change = true)]
        public ActionResult ManageSharePeriods(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var shareholder = db.SHAREHOLDERS.Find(id);
                if (shareholder == null)
                {
                    return HttpNotFound();
                }

                var periods = db.SHAREHOLDER_SHARE_PERIODS
                    .Where(sp => sp.ShareholderID == id)
                    .OrderByDescending(sp => sp.FromDate)
                    .ToList();

                ViewBag.Shareholder = shareholder;
                ViewBag.Periods = periods;

                return View();
            }
        }

        // POST so'rovi: Shareholder/AddSharePeriod
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddSharePeriod(int shareholderID, DateTime fromDate, DateTime? toDate, decimal sharePercent)
        {
            // FIN-B24: xato TempData orqali ko'rsatiladi (avval ModelState'ga yozilib redirect'da yo'qolardi)
            if (sharePercent <= 0 || sharePercent > 100)
            {
                TempData["Error"] = "Ulush 0% dan katta va 100% dan oshmasligi kerak.";
                return RedirectToAction("ManageSharePeriods", new { id = shareholderID });
            }
            if (toDate.HasValue && toDate.Value < fromDate)
            {
                TempData["Error"] = "Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.";
                return RedirectToAction("ManageSharePeriods", new { id = shareholderID });
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Shu davr bilan kesishgan barcha ulushlar — aksiyadorning o'z davrlari ham (avval chiqarib tashlanardi,
                // natijada bir aksiyador ustma-ust davrlar bilan 100% dan oshirib yuborishi mumkin edi)
                var upper = toDate ?? DateTime.MaxValue;
                var totalShares = db.SHAREHOLDER_SHARE_PERIODS
                    .Where(sp => sp.FromDate <= upper &&
                        (sp.ToDate == null || sp.ToDate >= fromDate) &&
                        sp.SHAREHOLDER.IsActive && sp.SHAREHOLDER.IsDeleted != true)
                    .Sum(sp => (decimal?)sp.SharePercent) ?? 0m;

                if (totalShares + sharePercent > 100)
                {
                    TempData["Error"] = $"Jami aksiyalar 100% dan oshib ketdi (shu davrda band: {totalShares:0.##}%). " +
                                        "Aksiyadorning avvalgi davri bo'lsa, avval uning tugash sanasini belgilang.";
                    return RedirectToAction("ManageSharePeriods", new { id = shareholderID });
                }

                var period = new SHAREHOLDER_SHARE_PERIODS
                {
                    ShareholderID = shareholderID,
                    FromDate = fromDate,
                    ToDate = toDate,
                    SharePercent = sharePercent
                };

                db.SHAREHOLDER_SHARE_PERIODS.Add(period);
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "Shareholder", "AddSharePeriod",
                    $"Aksiyador ulushi qo'shildi: {shareholderID}, {sharePercent}%");

                TempData["Success"] = "Ulush davri qo'shildi.";
                return RedirectToAction("ManageSharePeriods", new { id = shareholderID });
            }
        }

        // Olish so'rovi: Shareholder/AddContribution/5
        [Permission("Shareholder", Change = true)]
        public ActionResult AddContribution(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var shareholder = db.SHAREHOLDERS.Find(id);
                if (shareholder == null)
                {
                    return HttpNotFound();
                }

                ViewBag.Shareholder = shareholder;
                ViewBag.CurrencyID = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                ViewBag.ContributionTypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Naqd pul" },
                    new { Value = (byte)2, Text = "Tovarlar" },
                    new { Value = (byte)3, Text = "Xomashyo" },
                    new { Value = (byte)4, Text = "Tayyor mahsulot" },
                    new { Value = (byte)5, Text = "Boshqa" }
                }, "Value", "Text");

                var model = new SHAREHOLDER_CONTRIBUTIONS
                {
                    ShareholderID = id.Value,
                    ContributionDate = DateTime.Now
                };

                return View(model);
            }
        }

        // POST so'rovi: Shareholder/AddContribution
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddContribution(SHAREHOLDER_CONTRIBUTIONS contribution)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    // FIN-B24: kategoriya, tranzaksiya va hissa bitta DB tranzaksiyasida (avval 3 ta alohida SaveChanges)
                    using (var tx = db.Database.BeginTransaction())
                    {
                        string warning = null;
                        if (contribution.ContributionType == 1) // Naqd pul — kassa hisobiga kirim
                        {
                            var cashAccount = db.FIN_ACCOUNTS
                                .FirstOrDefault(a => a.IsCash && a.IsActive && a.CurrencyID == contribution.CurrencyID);

                            if (cashAccount != null)
                            {
                                contribution.FIN_TRANSACTIONS = new FIN_TRANSACTIONS
                                {
                                    TxnDate = contribution.ContributionDate,
                                    AccountID = cashAccount.ID,
                                    CategoryID = GetContributionCategoryID(db),
                                    Direction = 1, // In
                                    Amount = contribution.Amount,
                                    CurrencyID = contribution.CurrencyID,
                                    CounterpartyType = 3, // Shareholder
                                    CounterpartyID = contribution.ShareholderID,
                                    CounterpartyName = db.SHAREHOLDERS.Find(contribution.ShareholderID)?.FullName,
                                    Purpose = "Aksiyador hissasi",
                                    SourceDocType = "ShareholderContribution",
                                    Comment = contribution.Description,
                                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                                    CreatedDT = DateTime.Now
                                };
                            }
                            else
                            {
                                // Avval jimgina tranzaksiyasiz saqlanardi
                                warning = "Shu valyutadagi faol kassa hisobi topilmadi — hissa saqlandi, lekin kassaga kirim yozilmadi.";
                            }
                        }

                        db.SHAREHOLDER_CONTRIBUTIONS.Add(contribution);
                        db.SaveChanges();
                        tx.Commit();

                        if (warning != null)
                        {
                            TempData["Error"] = warning;
                        }
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, "Shareholder", "AddContribution",
                        $"Aksiyador hissasi qo'shildi: {contribution.ShareholderID}, {contribution.Amount}");

                    return RedirectToAction("Details", new { id = contribution.ShareholderID });
                }

                ViewBag.CurrencyID = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", contribution.CurrencyID);
                ViewBag.ContributionTypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Naqd pul" },
                    new { Value = (byte)2, Text = "Tovarlar" },
                    new { Value = (byte)3, Text = "Xomashyo" },
                    new { Value = (byte)4, Text = "Tayyor mahsulot" },
                    new { Value = (byte)5, Text = "Boshqa" }
                }, "Value", "Text", contribution.ContributionType);

                var shareholder = db.SHAREHOLDERS.Find(contribution.ShareholderID);
                ViewBag.Shareholder = shareholder;

                return View(contribution);
            }
        }

        private int GetContributionCategoryID(DBTHSNEntities db)
        {
            // Find yoki yaratish a category uchun shareholder contributions
            var category = db.FIN_CATEGORIES
                .FirstOrDefault(c => c.Name.Contains("Aksiyador") && c.Type == 1 && c.IsActive);

            if (category == null)
            {
                // Yaratish standart category agar not mavjud
                category = new FIN_CATEGORIES
                {
                    Name = "Aksiyador hissalari",
                    Type = 1, // Income
                    IsActive = true,
                    SortOrder = 999
                };
                db.FIN_CATEGORIES.Add(category);
                db.SaveChanges(); // ID kerak; chaqiruvchining DB tranzaksiyasi ichida
            }

            return category.ID;
        }
    }
}
