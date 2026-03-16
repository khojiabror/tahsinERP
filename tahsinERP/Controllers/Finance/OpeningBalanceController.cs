using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Controllers;
using tahsinERP.Models;
using tahsinERP.ViewModels.Finance;

namespace tahsinERP.Controllers.Finance
{
    public class OpeningBalanceController : Controller
    {
        // Olish so'rovi: OpeningBalance
        public ActionResult Index(DateTime? snapshotDate)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var date = snapshotDate ?? DateTime.Today;

                var categories = db.FIN_BALANCE_CATEGORIES
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Type)
                    .ThenBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .ToList();

                var snapshots = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => DbFunctions.TruncateTime(s.SnapshotDate) == DbFunctions.TruncateTime(date))
                    .ToList()
                    .GroupBy(s => s.CategoryID)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedDT).FirstOrDefault());

                var defaultCurrency = db.CURRENCIES.FirstOrDefault();
                var currencies = db.CURRENCIES.OrderBy(c => c.Shortname).ToList();

                var model = new OpeningBalanceViewModel
                {
                    SnapshotDate = date,
                    DefaultCurrencyID = defaultCurrency?.ID ?? 1,
                    CurrencyList = currencies.Select(c => new SelectListItem
                    {
                        Value = c.ID.ToString(),
                        Text = c.Shortname
                    })
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
                        TypeName = cat.Type == 1 ? "Aktiv" : cat.Type == 2 ? "Passiv" : "Kapital",
                        Amount = snap?.Amount ?? 0,
                        CurrencyID = snap?.CurrencyID ?? (defaultCurrency?.ID ?? 1),
                        CurrencyShortname = snap != null
                            ? currencies.FirstOrDefault(c => c.ID == snap.CurrencyID)?.Shortname ?? defaultCurrency?.Shortname
                            : defaultCurrency?.Shortname
                    };

                    model.Items.Add(item);
                }

                return View(model);
            }
        }

        // POST so'rovi: OpeningBalance saqlash
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(OpeningBalanceViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (model == null)
            {
                return RedirectToAction("Index");
            }

            using (var db = new DBTHSNEntities())
            {
                var date = model.SnapshotDate.Date;
                var userId = UserHelper.GetUserId(User.Identity.Name);

                // Eski opening snapshotlarni shu sana uchun tozalash
                var existing = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => DbFunctions.TruncateTime(s.SnapshotDate) == date &&
                                (s.SourceType == "OpeningBalance" || s.SourceType == null))
                    .ToList();

                if (existing.Any())
                {
                    db.FIN_BALANCE_SNAPSHOTS.RemoveRange(existing);
                    db.SaveChanges();
                }

                var currencies = db.CURRENCIES.ToDictionary(c => c.ID, c => c);

                if (model.Items != null)
                {
                    foreach (var item in model.Items)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        // Faqat nol bo'lmagan summalarni saqlaymiz
                        if (item.Amount == 0)
                        {
                            continue;
                        }

                        var currencyId = item.CurrencyID;
                        if (!currencies.ContainsKey(currencyId))
                        {
                            var firstCurrency = currencies.Values.FirstOrDefault();
                            currencyId = firstCurrency != null ? firstCurrency.ID : 1;
                        }

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
            }

            TempData["Success"] = "Opening balans muvaffaqiyatli saqlandi.";
            return RedirectToAction("Index", new { snapshotDate = model.SnapshotDate.ToString("yyyy-MM-dd") });
        }
    }
}

