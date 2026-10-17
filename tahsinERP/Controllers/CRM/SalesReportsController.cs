using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Crm;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Sotuv hisobotlari — oylik hisobot, mijoz/mahsulot bo'yicha. Dynamic ma'lumot, DB ga yozilmaydi.
    /// </summary>
    public class SalesReportsController : Controller
    {
        private static bool HasSalesReportPermission(string userName)
        {
            return RoleHelper.IsViewPermitted(userName, "SalesReports") ||
                   RoleHelper.IsViewPermitted(userName, "SOrder") ||
                   RoleHelper.IsViewPermitted(userName, "SalesInvoices");
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

        /// <summary>Hisobotlar ro'yxati (sahifa)</summary>
        public ActionResult Index()
        {
            if (!HasSalesReportPermission(User.Identity.Name))
                return RedirectToAction("Index", "ViewNotAuthorized");
            return View();
        }

        /// <summary>Oylik savdo hisoboti — mahsulot bo'yicha buyurtma va sotuv (invoys), printable.</summary>
        [HttpGet]
        public ActionResult MonthlyReport(int? year, int? month)
        {
            if (!HasSalesReportPermission(User.Identity.Name))
                return RedirectToAction("Index", "ViewNotAuthorized");

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
                var company = db.COMPANIES.FirstOrDefault();
                var model = new MonthlySalesReportViewModel
                {
                    Year = y,
                    Month = m,
                    MonthName = monthName,
                    CompanyName = company?.Name ?? "Tahsin ERP"
                };

                // Mahsulot bo'yicha yig'indilar SQL'da (CRM-P08: avval to'liq qatorlar Include bilan yuklanardi);
                // bekor qilingan invoyslar chiqarib tashlanadi, oyning oxirgi kuni to'liq kiradi (CRM-B07)
                var orderLinesQ = OrderLines(db, periodStart, periodEndExclusive);
                var invoiceLinesQ = InvoiceLines(db, periodStart, periodEndExclusive);

                var orderByProduct = orderLinesQ
                    .GroupBy(op => op.ProductID)
                    .Select(g => new { ProductID = g.Key, OrderQty = g.Sum(x => x.Amount), OrderAmount = g.Sum(x => x.TotalPrice) })
                    .ToList()
                    .ToDictionary(x => x.ProductID, x => new { OrderQty = (decimal)x.OrderQty, OrderAmount = (decimal)x.OrderAmount });

                var soldByProduct = invoiceLinesQ
                    .GroupBy(l => l.ProductID)
                    .Select(g => new { ProductID = g.Key, SoldQty = g.Sum(x => x.Qty), SoldAmount = g.Sum(x => x.LineAmount ?? x.Qty * x.UnitPrice) })
                    .ToList()
                    .ToDictionary(x => x.ProductID, x => new { x.SoldQty, x.SoldAmount });

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
                    var orderQty = orderAgg?.OrderQty ?? 0;
                    var orderAmt = orderAgg?.OrderAmount ?? 0;
                    var soldQty = soldAgg?.SoldQty ?? 0;
                    var soldAmt = soldAgg?.SoldAmount ?? 0;
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
            if (!HasSalesReportPermission(User.Identity.Name))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var from = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var to = toDate ?? DateTime.Now.Date;
            if (from > to) { var t = from; from = to; to = t; }

            using (var db = new DBTHSNEntities())
            {
                var company = db.COMPANIES.FirstOrDefault();
                var model = new SalesByCustomerReportViewModel
                {
                    FromDate = from,
                    ToDate = to,
                    CompanyName = company?.Name ?? "Tahsin ERP"
                };

                var toExclusive = to.AddDays(1);
                var orderAgg = db.S_ORDERS.AsNoTracking()
                    .Where(o => (o.IsDeleted == false || o.IsDeleted == null) && o.IssuedDate >= from && o.IssuedDate < toExclusive)
                    .GroupBy(o => o.CustomerID)
                    .Select(g => new { CustomerID = g.Key, Count = g.Count(), Amount = g.Sum(o => o.Amount) ?? 0 })
                    .ToList()
                    .ToDictionary(x => x.CustomerID);
                var invoiceAgg = db.SALES_INVOICES.AsNoTracking().Active()
                    .Where(i => i.InvoiceDate >= from && i.InvoiceDate < toExclusive)
                    .GroupBy(i => i.CustomerID)
                    .Select(g => new { CustomerID = g.Key, Count = g.Count(), Amount = g.Sum(i => i.TotalAmount) })
                    .ToList()
                    .ToDictionary(x => x.CustomerID);

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
                        TotalOrderAmount = (decimal)(oa?.Amount ?? 0),
                        TotalInvoiceAmount = ia?.Amount ?? 0m
                    });
                }
                model.Lines = model.Lines.OrderByDescending(l => l.TotalInvoiceAmount).ToList();
                model.TotalAmount = model.Lines.Sum(l => l.TotalInvoiceAmount);
                ViewBag.FromDate = from;
                ViewBag.ToDate = to;
                return View(model);
            }
        }

        /// <summary>Mahsulot bo'yicha sotuv — davr bo'yicha.</summary>
        [HttpGet]
        public ActionResult SalesByProduct(DateTime? fromDate, DateTime? toDate)
        {
            if (!HasSalesReportPermission(User.Identity.Name))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var from = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var to = toDate ?? DateTime.Now.Date;
            if (from > to) { var t = from; from = to; to = t; }

            using (var db = new DBTHSNEntities())
            {
                var company = db.COMPANIES.FirstOrDefault();
                var model = new SalesByProductReportViewModel
                {
                    FromDate = from,
                    ToDate = to,
                    CompanyName = company?.Name ?? "Tahsin ERP"
                };

                var toExclusive = to.AddDays(1);
                var orderByProduct = OrderLines(db, from, toExclusive)
                    .GroupBy(op => op.ProductID)
                    .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => x.Amount), Amt = g.Sum(x => x.TotalPrice) })
                    .ToList()
                    .ToDictionary(x => x.ProductID, x => new { Qty = (decimal)x.Qty, Amt = (decimal)x.Amt });
                var soldByProduct = InvoiceLines(db, from, toExclusive)
                    .GroupBy(l => l.ProductID)
                    .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => x.Qty), Amt = g.Sum(x => x.LineAmount ?? x.Qty * x.UnitPrice) })
                    .ToList()
                    .ToDictionary(x => x.ProductID, x => new { x.Qty, x.Amt });

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
                ViewBag.FromDate = from;
                ViewBag.ToDate = to;
                return View(model);
            }
        }
    }
}
