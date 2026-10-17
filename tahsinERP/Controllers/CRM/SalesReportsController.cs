using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Sotuv hisobotlari — oylik hisobot, mijoz/mahsulot bo'yicha. Dynamic ma'lumot, DB ga yozilmaydi.
    /// </summary>
    public class SalesReportsController : Controller
    {
        /// <summary>Barcha hisobotlar uchun bitta ruxsat tekshiruvi (avval har amalda takrorlanardi — D23).</summary>
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (!CrmReport.AnyViewPermitted(User.Identity.Name, "SalesReports", "SOrder", "SalesInvoices"))
            {
                filterContext.Result = RedirectToAction("Index", "ViewNotAuthorized");
            }
        }

        /// <summary>Davrdagi faol (o'chirilmagan) buyurtma qatorlari; yuqori chegara yarim ochiq.</summary>
        private static IQueryable<S_ORDER_PRODUCTS> OrderLines(DBTHSNEntities db, DateTime from, DateTime toExclusive)
        {
            return db.S_ORDER_PRODUCTS.AsNoTracking()
                .Where(op => (op.S_ORDERS.IsDeleted == false || op.S_ORDERS.IsDeleted == null)
                    && op.S_ORDERS.IssuedDate >= from
                    && op.S_ORDERS.IssuedDate < toExclusive);
        }

        /// <summary>Davrdagi faol (bekor qilinmagan) invoys qatorlari; yuqori chegara yarim ochiq.</summary>
        private static IQueryable<SALES_INVOICE_LINES> InvoiceLines(DBTHSNEntities db, DateTime from, DateTime toExclusive)
        {
            return db.SALES_INVOICE_LINES.AsNoTracking().Active()
                .Where(l => l.SALES_INVOICES.InvoiceDate >= from && l.SALES_INVOICES.InvoiceDate < toExclusive);
        }

        private sealed class QtyAmt
        {
            public decimal Qty { get; set; }
            public decimal Amt { get; set; }
        }

        /// <summary>Mahsulot bo'yicha buyurtma miqdori va so'mdagi summasi (valyuta × kun bo'yicha SQL'da yig'ilib, o'giriladi).</summary>
        private static Dictionary<int, QtyAmt> OrderTotalsByProduct(IQueryable<S_ORDER_PRODUCTS> lines, UzsConverter uzs)
        {
            return lines
                .GroupBy(op => new { op.ProductID, op.S_ORDERS.CurrencyID, Day = DbFunctions.TruncateTime(op.S_ORDERS.IssuedDate) })
                .Select(g => new { g.Key.ProductID, g.Key.CurrencyID, g.Key.Day, Qty = g.Sum(x => x.Amount), Amt = g.Sum(x => x.TotalPrice) })
                .ToList()
                .GroupBy(x => x.ProductID)
                .ToDictionary(g => g.Key, g => new QtyAmt
                {
                    Qty = (decimal)g.Sum(x => x.Qty),
                    Amt = g.Sum(x => uzs.ToUzsOrZero((decimal)x.Amt, x.CurrencyID, x.Day ?? DateTime.Today))
                });
        }

        /// <summary>Mahsulot bo'yicha sotilgan miqdor va so'mdagi summasi (invoys valyutasi va sanasi bo'yicha).</summary>
        private static Dictionary<int, QtyAmt> InvoiceTotalsByProduct(IQueryable<SALES_INVOICE_LINES> lines, UzsConverter uzs)
        {
            return lines
                .GroupBy(l => new { l.ProductID, l.SALES_INVOICES.CurrencyID, Day = DbFunctions.TruncateTime(l.SALES_INVOICES.InvoiceDate) })
                .Select(g => new { g.Key.ProductID, g.Key.CurrencyID, g.Key.Day, Qty = g.Sum(x => x.Qty), Amt = g.Sum(x => x.LineAmount ?? x.Qty * x.UnitPrice) })
                .ToList()
                .GroupBy(x => x.ProductID)
                .ToDictionary(g => g.Key, g => new QtyAmt
                {
                    Qty = g.Sum(x => x.Qty),
                    Amt = g.Sum(x => uzs.ToUzsOrZero(x.Amt, x.CurrencyID, x.Day ?? DateTime.Today))
                });
        }

        /// <summary>Hisobotlar ro'yxati (sahifa)</summary>
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>Oylik savdo hisoboti — mahsulot bo'yicha buyurtma va sotuv (invoys), printable.</summary>
        [HttpGet]
        public ActionResult MonthlyReport(int? year, int? month)
        {
            var y = year ?? DateTime.Now.Year;
            var m = month ?? DateTime.Now.Month;
            if (m < 1) m = 1;
            if (m > 12) m = 12;
            var periodStart = new DateTime(y, m, 1);
            var periodEndExclusive = periodStart.AddMonths(1);

            var ci = System.Globalization.CultureInfo.GetCultureInfo("uz-Latn-UZ");
            var monthName = ci?.DateTimeFormat?.GetMonthName(m) ?? periodStart.ToString("MMMM");

            using (var db = new DBTHSNEntities())
            {
                var model = new MonthlySalesReportViewModel
                {
                    Year = y,
                    Month = m,
                    MonthName = monthName,
                    CompanyName = CrmReport.CompanyName(db)
                };

                // Mahsulot bo'yicha yig'indilar SQL'da (CRM-P08: avval to'liq qatorlar Include bilan yuklanardi);
                // bekor qilingan invoyslar chiqarib tashlanadi, oyning oxirgi kuni to'liq kiradi (CRM-B07)
                var orderLinesQ = OrderLines(db, periodStart, periodEndExclusive);
                var invoiceLinesQ = InvoiceLines(db, periodStart, periodEndExclusive);

                // Summalar so'mda, hujjat sanasidagi kurs bilan (CRM-B18)
                var uzs = new UzsConverter(db);
                var orderByProduct = OrderTotalsByProduct(orderLinesQ, uzs);
                var soldByProduct = InvoiceTotalsByProduct(invoiceLinesQ, uzs);

                var allProductIds = orderByProduct.Keys.Union(soldByProduct.Keys).Distinct().ToList();
                var products = db.PRODUCTS.AsNoTracking()
                    .Include(p => p.UNIT)
                    .Where(p => allProductIds.Contains(p.ID))
                    .ToDictionary(p => p.ID);

                foreach (var productId in allProductIds)
                {
                    if (!products.TryGetValue(productId, out var product)) continue;
                    var orderAgg = orderByProduct.TryGetValue(productId, out var o) ? o : null;
                    var soldAgg = soldByProduct.TryGetValue(productId, out var s) ? s : null;
                    var orderQty = orderAgg?.Qty ?? 0;
                    var orderAmt = orderAgg?.Amt ?? 0;
                    var soldQty = soldAgg?.Qty ?? 0;
                    var soldAmt = soldAgg?.Amt ?? 0;
                    var conversion = orderAmt > 0 ? (soldAmt / orderAmt * 100) : (soldAmt > 0 ? 100m : 0);

                    model.Lines.Add(new MonthlySalesReportLineViewModel
                    {
                        ProductID = productId,
                        ProductCode = product.PNo ?? "",
                        ProductName = product.Name ?? "",
                        UnitName = product.UNIT?.ShortName ?? product.UNIT?.UnitName ?? "",
                        OrderQty = orderQty,
                        OrderAmount = orderAmt,
                        SoldQty = soldQty,
                        SoldAmount = soldAmt,
                        ConversionPercent = conversion
                    });
                }

                model.Lines = model.Lines.OrderBy(l => l.ProductCode ?? l.ProductName).ToList();
                model.TotalOrderAmount = model.Lines.Sum(l => l.OrderAmount);
                model.TotalSoldAmount = model.Lines.Sum(l => l.SoldAmount);
                model.TotalOrderCount = orderLinesQ.Select(op => op.OrderID).Distinct().Count();
                model.TotalInvoiceCount = invoiceLinesQ.Select(l => l.InvoiceID).Distinct().Count();

                ViewBag.CurrencyWarning = uzs.Warning;
                ViewBag.Year = y;
                ViewBag.Month = m;
                ViewBag.Months = Enumerable.Range(1, 12).Select(i => new SelectListItem { Value = i.ToString(), Text = new DateTime(2000, i, 1).ToString("MMMM", ci), Selected = i == m }).ToList();
                ViewBag.Years = Enumerable.Range(DateTime.Now.Year - 5, 10).Select(i => new SelectListItem { Value = i.ToString(), Text = i.ToString(), Selected = i == y }).ToList();
                return View(model);
            }
        }

        /// <summary>Mijoz bo'yicha sotuv — davr bo'yicha.</summary>
        [HttpGet]
        public ActionResult SalesByCustomer(DateTime? fromDate, DateTime? toDate)
        {
            var period = CrmReport.Period(fromDate, toDate);
            var from = period.From;
            var to = period.To;

            using (var db = new DBTHSNEntities())
            {
                var model = new SalesByCustomerReportViewModel
                {
                    FromDate = from,
                    ToDate = to,
                    CompanyName = CrmReport.CompanyName(db)
                };

                var toExclusive = period.ToExclusive;
                // Mijoz × valyuta × kun bo'yicha SQL'da, so'mga hujjat sanasidagi kurs bilan (CRM-B18)
                var uzs = new UzsConverter(db);
                var orderAgg = db.S_ORDERS.AsNoTracking()
                    .Where(o => (o.IsDeleted == false || o.IsDeleted == null) && o.IssuedDate >= from && o.IssuedDate < toExclusive)
                    .GroupBy(o => new { o.CustomerID, o.CurrencyID, Day = DbFunctions.TruncateTime(o.IssuedDate) })
                    .Select(g => new { g.Key.CustomerID, g.Key.CurrencyID, g.Key.Day, Count = g.Count(), Amount = g.Sum(o => o.Amount) ?? 0 })
                    .ToList()
                    .GroupBy(x => x.CustomerID)
                    .ToDictionary(g => g.Key, g => new
                    {
                        Count = g.Sum(x => x.Count),
                        Amount = g.Sum(x => uzs.ToUzsOrZero((decimal)x.Amount, x.CurrencyID, x.Day ?? from))
                    });
                var invoiceAgg = db.SALES_INVOICES.AsNoTracking().Active()
                    .Where(i => i.InvoiceDate >= from && i.InvoiceDate < toExclusive)
                    .GroupBy(i => new { i.CustomerID, i.CurrencyID, Day = DbFunctions.TruncateTime(i.InvoiceDate) })
                    .Select(g => new { g.Key.CustomerID, g.Key.CurrencyID, g.Key.Day, Count = g.Count(), Amount = g.Sum(i => i.TotalAmount) })
                    .ToList()
                    .GroupBy(x => x.CustomerID)
                    .ToDictionary(g => g.Key, g => new
                    {
                        Count = g.Sum(x => x.Count),
                        Amount = g.Sum(x => uzs.ToUzsOrZero(x.Amount, x.CurrencyID, x.Day ?? from))
                    });

                var customerIds = orderAgg.Keys.Union(invoiceAgg.Keys).ToList();
                var customers = db.CUSTOMERS.AsNoTracking()
                    .Where(c => customerIds.Contains(c.ID))
                    .Select(c => new { c.ID, c.Name })
                    .ToList()
                    .ToDictionary(c => c.ID, c => c.Name);

                foreach (var cid in customerIds)
                {
                    if (!customers.TryGetValue(cid, out var name)) continue;
                    orderAgg.TryGetValue(cid, out var oa);
                    invoiceAgg.TryGetValue(cid, out var ia);
                    model.Lines.Add(new SalesByCustomerLineViewModel
                    {
                        CustomerID = cid,
                        CustomerName = name ?? "",
                        OrderCount = oa?.Count ?? 0,
                        InvoiceCount = ia?.Count ?? 0,
                        TotalOrderAmount = oa?.Amount ?? 0m,
                        TotalInvoiceAmount = ia?.Amount ?? 0m
                    });
                }
                model.Lines = model.Lines.OrderByDescending(l => l.TotalInvoiceAmount).ToList();
                model.TotalAmount = model.Lines.Sum(l => l.TotalInvoiceAmount);
                ViewBag.CurrencyWarning = uzs.Warning;
                ViewBag.FromDate = from;
                ViewBag.ToDate = to;
                return View(model);
            }
        }

        /// <summary>Mahsulot bo'yicha sotuv — davr bo'yicha.</summary>
        [HttpGet]
        public ActionResult SalesByProduct(DateTime? fromDate, DateTime? toDate)
        {
            var period = CrmReport.Period(fromDate, toDate);
            var from = period.From;
            var to = period.To;

            using (var db = new DBTHSNEntities())
            {
                var model = new SalesByProductReportViewModel
                {
                    FromDate = from,
                    ToDate = to,
                    CompanyName = CrmReport.CompanyName(db)
                };

                var toExclusive = period.ToExclusive;
                var uzs = new UzsConverter(db);
                var orderByProduct = OrderTotalsByProduct(OrderLines(db, from, toExclusive), uzs);
                var soldByProduct = InvoiceTotalsByProduct(InvoiceLines(db, from, toExclusive), uzs);

                var allIds = orderByProduct.Keys.Union(soldByProduct.Keys).Distinct().ToList();
                var products = db.PRODUCTS.AsNoTracking().Include(p => p.UNIT).Where(p => allIds.Contains(p.ID)).ToDictionary(p => p.ID);

                foreach (var productId in allIds)
                {
                    if (!products.TryGetValue(productId, out var product)) continue;
                    decimal oQty = 0, oAmt = 0, sQty = 0, sAmt = 0;
                    if (orderByProduct.TryGetValue(productId, out var oa)) { oQty = oa.Qty; oAmt = oa.Amt; }
                    if (soldByProduct.TryGetValue(productId, out var sa)) { sQty = sa.Qty; sAmt = sa.Amt; }
                    var conversion = oAmt > 0 ? (sAmt / oAmt * 100) : (sAmt > 0 ? 100m : 0m);
                    model.Lines.Add(new SalesByProductLineViewModel
                    {
                        ProductID = productId,
                        ProductCode = product.PNo ?? "",
                        ProductName = product.Name ?? "",
                        UnitName = product.UNIT?.ShortName ?? product.UNIT?.UnitName ?? "",
                        OrderQty = oQty,
                        OrderAmount = oAmt,
                        SoldQty = sQty,
                        SoldAmount = sAmt,
                        ConversionPercent = conversion
                    });
                }
                model.Lines = model.Lines.OrderByDescending(l => l.SoldAmount).ToList();
                model.TotalOrderAmount = model.Lines.Sum(l => l.OrderAmount);
                model.TotalSoldAmount = model.Lines.Sum(l => l.SoldAmount);
                ViewBag.CurrencyWarning = uzs.Warning;
                ViewBag.FromDate = from;
                ViewBag.ToDate = to;
                return View(model);
            }
        }
    }
}
