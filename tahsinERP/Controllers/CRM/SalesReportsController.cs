using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
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
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

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

                // Buyurtmalar — shu oydagi S_ORDERS → S_ORDER_PRODUCTS
                var orderLines = db.S_ORDER_PRODUCTS
                    .Include(op => op.S_ORDERS)
                    .Include(op => op.PRODUCT)
                    .Include(op => op.UNIT)
                    .Where(op => op.S_ORDERS != null &&
                                 (op.S_ORDERS.IsDeleted == false || op.S_ORDERS.IsDeleted == null) &&
                                 op.S_ORDERS.IssuedDate >= periodStart &&
                                 op.S_ORDERS.IssuedDate <= periodEnd)
                    .ToList();

                var orderByProduct = orderLines
                    .GroupBy(op => op.ProductID)
                    .Select(g => new
                    {
                        ProductID = g.Key,
                        Product = g.First().PRODUCT,
                        UnitName = g.First().UNIT?.ShortName ?? g.First().UNIT?.UnitName ?? "",
                        OrderQty = (decimal)g.Sum(x => x.Amount),
                        OrderAmount = (decimal)g.Sum(x => x.TotalPrice)
                    })
                    .ToDictionary(x => x.ProductID);

                // Invoyslar — shu oydagi SALES_INVOICES → SALES_INVOICE_LINES
                var invoiceLines = db.SALES_INVOICE_LINES
                    .Include(l => l.SALES_INVOICES)
                    .Include(l => l.PRODUCT)
                    .Where(l => l.SALES_INVOICES != null &&
                               l.SALES_INVOICES.InvoiceDate >= periodStart &&
                               l.SALES_INVOICES.InvoiceDate <= periodEnd)
                    .ToList();

                var soldByProduct = invoiceLines
                    .GroupBy(l => l.ProductID)
                    .Select(g => new
                    {
                        ProductID = g.Key,
                        Product = g.First().PRODUCT,
                        SoldQty = g.Sum(x => x.Qty),
                        SoldAmount = g.Sum(x => x.LineAmount ?? x.Qty * x.UnitPrice)
                    })
                    .ToDictionary(x => x.ProductID);

                var allProductIds = orderByProduct.Keys.Union(soldByProduct.Keys).Distinct().ToList();
                var products = db.PRODUCTS
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
                model.TotalOrderCount = orderLines.Select(op => op.S_ORDERS?.ID).Distinct().Count(x => x > 0);
                model.TotalInvoiceCount = invoiceLines.Select(l => l.SALES_INVOICES?.ID).Distinct().Count(x => x > 0);

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

                var orders = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Where(o => (o.IsDeleted == false || o.IsDeleted == null) && o.IssuedDate >= from && o.IssuedDate <= to)
                    .ToList();
                var invoices = db.SALES_INVOICES
                    .Include(i => i.CUSTOMER)
                    .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to)
                    .ToList();

                var customerIds = orders.Select(o => o.CustomerID).Union(invoices.Select(i => i.CustomerID)).Distinct().ToList();
                var customers = db.CUSTOMERS.Where(c => customerIds.Contains(c.ID)).ToDictionary(c => c.ID);

                foreach (var cid in customerIds)
                {
                    if (!customers.TryGetValue(cid, out var cust)) continue;
                    var custOrders = orders.Where(o => o.CustomerID == cid).ToList();
                    var custInvoices = invoices.Where(i => i.CustomerID == cid).ToList();
                    model.Lines.Add(new SalesByCustomerLineViewModel
                    {
                        CustomerID = cid,
                        CustomerName = cust.Name ?? "",
                        OrderCount = custOrders.Count,
                        InvoiceCount = custInvoices.Count,
                        TotalOrderAmount = (decimal)(custOrders.Sum(o => o.Amount) ?? 0),
                        TotalInvoiceAmount = custInvoices.Sum(i => i.TotalAmount)
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

                var orderLines = db.S_ORDER_PRODUCTS
                    .Include(op => op.S_ORDERS)
                    .Include(op => op.PRODUCT)
                    .Include(op => op.UNIT)
                    .Where(op => op.S_ORDERS != null &&
                                 (op.S_ORDERS.IsDeleted == false || op.S_ORDERS.IsDeleted == null) &&
                                 op.S_ORDERS.IssuedDate >= from &&
                                 op.S_ORDERS.IssuedDate <= to)
                    .ToList();
                var invoiceLines = db.SALES_INVOICE_LINES
                    .Include(l => l.SALES_INVOICES)
                    .Include(l => l.PRODUCT)
                    .Where(l => l.SALES_INVOICES != null &&
                               l.SALES_INVOICES.InvoiceDate >= from &&
                               l.SALES_INVOICES.InvoiceDate <= to)
                    .ToList();

                var orderByProduct = orderLines
                    .GroupBy(op => op.ProductID)
                    .ToDictionary(g => g.Key, g => new { Qty = (decimal)g.Sum(x => x.Amount), Amt = (decimal)g.Sum(x => x.TotalPrice) });
                var soldByProduct = invoiceLines
                    .GroupBy(l => l.ProductID)
                    .ToDictionary(g => g.Key, g => new { Qty = g.Sum(x => x.Qty), Amt = g.Sum(x => x.LineAmount ?? x.Qty * x.UnitPrice) });

                var allIds = orderByProduct.Keys.Union(soldByProduct.Keys).Distinct().ToList();
                var products = db.PRODUCTS.Include(p => p.UNIT).Where(p => allIds.Contains(p.ID)).ToDictionary(p => p.ID);

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
