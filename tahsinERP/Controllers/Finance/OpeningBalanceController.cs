using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Controllers;
using tahsinERP.Models;
using tahsinERP.ViewModels.Finance;
using tahsinERP.Helpers;

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
                var rows = db.FIN_BALANCE_SNAPSHOTS
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
                var date = model.SnapshotDate.Date;
                var userId = UserHelper.GetUserId(User.Identity.Name);

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

                        // 0: "Bo'sh qoldirish" belgilangan bo'lsa — qator yozilmaydi; belgilanmagan bo'lsa — aniq 0 saqlanadi.
                        if (item.Amount == 0 && item.LeaveEmpty)
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
            return RedirectToAction("Edit", new { snapshotDate = model.SnapshotDate.ToString("yyyy-MM-dd") });
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
                var lineCount = db.FIN_BALANCE_SNAPSHOTS.Count(s =>
                    DbFunctions.TruncateTime(s.SnapshotDate) == date &&
                    (s.SourceType == "OpeningBalance" || s.SourceType == null));

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
                var existing = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => DbFunctions.TruncateTime(s.SnapshotDate) == date &&
                                (s.SourceType == "OpeningBalance" || s.SourceType == null))
                    .ToList();

                if (existing.Any())
                {
                    db.FIN_BALANCE_SNAPSHOTS.RemoveRange(existing);
                    db.SaveChanges();
                }
            }

            TempData["Success"] = $"Opening balans ({date:yyyy-MM-dd}) o'chirildi.";
            return RedirectToAction("Index");
        }

        private static OpeningBalanceViewModel BuildOpeningBalanceViewModel(DBTHSNEntities db, DateTime date)
        {
            var categories = db.FIN_BALANCE_CATEGORIES
                .Where(c => c.IsActive)
                .OrderBy(c => c.Type)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList();

            var snapshots = db.FIN_BALANCE_SNAPSHOTS
                .Where(s => DbFunctions.TruncateTime(s.SnapshotDate) == DbFunctions.TruncateTime(date) &&
                            (s.SourceType == "OpeningBalance" || s.SourceType == null))
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
                    TypeName = BalanceCategoryDisplayHelper.GetSideLabel(cat),
                    Amount = snap?.Amount ?? 0,
                    CurrencyID = snap?.CurrencyID ?? (defaultCurrency?.ID ?? 1),
                    CurrencyShortname = snap != null
                        ? currencies.FirstOrDefault(c => c.ID == snap.CurrencyID)?.Shortname ?? defaultCurrency?.Shortname
                        : defaultCurrency?.Shortname,
                    // DBda snapshot yo'q = bo'sh; yozuv bor (0 ham) = aniq qiymat
                    LeaveEmpty = snap == null
                };

                model.Items.Add(item);
            }

            return model;
        }
    }
}
