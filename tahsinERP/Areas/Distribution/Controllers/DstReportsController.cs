using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Reports;
using tahsinERP.Services.Common;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Savdo hisobotlari: kesimlar (agent, mijoz, tovar, hudud, kategoriya), tashrif reja/fakt va samaradorlik,
    /// agentlar topshirgan pul. Har birida Excel eksport. Ko'rinish — ro'yxatlar bilan bir xil scope'lar;
    /// tannarx va foyda — faqat <see cref="DstPermissions.SeeCost"/> bilan.
    /// </summary>
    public class DstReportsController : DstControllerBase
    {
        /// <summary>Excel'ga bir martada chiqariladigan qatorlar chegarasi.</summary>
        private const int ExportLimit = 100000;

        public ActionResult Index()
        {
            return RedirectToAction("Sales");
        }

        private static ReportFilter Filter(DateTime? from, DateTime? to, int? agentId, int? customerId, int? territoryId,
            int? categoryId, int? productId, int? warehouseId)
        {
            var f = ReportFilter.MonthToDate(DateTime.Today);
            if (from.HasValue) f.From = from.Value;
            if (to.HasValue) f.To = to.Value;
            f.AgentId = agentId;
            f.CustomerId = customerId;
            f.TerritoryId = territoryId;
            f.CategoryId = categoryId;
            f.ProductId = productId;
            f.WarehouseId = warehouseId;
            return f.Normalized();
        }

        private void FillLookups(DistributionDbContext db, ReportFilter f)
        {
            var agentScope = new ScopeService(db).AgentsFor(CurrentUserName);
            ViewBag.Agents = agentScope.Apply(db.SalesAgents.Where(a => !a.IsExpeditor), a => a.UserID)
                .Join(db.Users, a => a.UserID, u => u.ID, (a, u) => new { a.UserID, Name = u.FullName ?? u.Uname })
                .OrderBy(a => a.Name).ToList()
                .Select(a => new SelectListItem { Value = a.UserID.ToString(), Text = a.Name, Selected = a.UserID == f.AgentId }).ToList();
            ViewBag.Territories = TerritoryTree.Flatten(db.Territories.AsNoTracking().ToList(), IsRussian)
                .Select(n => new SelectListItem { Value = n.Item.ID.ToString(), Text = n.Path, Selected = n.Item.ID == f.TerritoryId }).ToList();
            ViewBag.Categories = db.Categories.Where(c => c.IsDeleted != true).OrderBy(c => c.Name).ToList()
                .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name, Selected = c.ID == f.CategoryId }).ToList();
            var whScope = new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse);
            ViewBag.Warehouses = db.Warehouses.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToList()
                .Where(w => whScope.Allows(w.ID))
                .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName, Selected = w.ID == f.WarehouseId }).ToList();
            ViewBag.Filter = f;
            ViewBag.CustomerName = f.CustomerId.HasValue ? db.Customers.Where(c => c.ID == f.CustomerId).Select(c => c.Name).FirstOrDefault() : null;
            ViewBag.ProductName = f.ProductId.HasValue ? db.Products.Where(p => p.ID == f.ProductId).Select(p => p.PNo + " · " + p.Name).FirstOrDefault() : null;
        }

        private static SalesDimension ParseDimension(string dim)
        {
            return Enum.TryParse(dim, true, out SalesDimension d) ? d : SalesDimension.Agent;
        }

        // ---------- savdo ----------

        public ActionResult Sales(string dim, DateTime? from, DateTime? to, int? agentId, int? customerId, int? territoryId,
            int? categoryId, int? productId, int? warehouseId)
        {
            var f = Filter(from, to, agentId, customerId, territoryId, categoryId, productId, warehouseId);
            using (var db = NewDb())
            {
                FillLookups(db, f);
                ViewBag.Dimension = ParseDimension(dim);
                return View();
            }
        }

        /// <summary>DataTables ustunlari tartibi (Sales.cshtml bilan bir xil).</summary>
        private static readonly string[] SalesColumns = { "name", "qty", "amount", "returns", "net", "profit", "margin", "orders", "customers" };

        [HttpGet]
        public async Task<ActionResult> SalesData(string dim, DateTime? from, DateTime? to, int? agentId, int? customerId, int? territoryId,
            int? categoryId, int? productId, int? warehouseId)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 4, defaultDescending: true);
            var f = Filter(from, to, agentId, customerId, territoryId, categoryId, productId, warehouseId);
            var dimension = ParseDimension(dim);
            bool cost = CanSeeCost;
            using (var db = NewDb())
            {
                var reportScope = ReportScope.For(db, CurrentUserName);
                var facts = SalesReportService.Facts(db, f, reportScope);
                var rows = SalesReportService.ByDimension(db, facts, dimension, IsRussian);
                var column = page.OrderColumn >= 0 && page.OrderColumn < SalesColumns.Length ? SalesColumns[page.OrderColumn] : "net";
                if (!cost && (column == "profit" || column == "margin")) column = "net";
                if (column == "margin") column = "profit";

                var result = await ServerTablePage.LoadAsync(rows, page,
                    (x, s) => x.Where(r => r.Name.Contains(s)),
                    (x, c, desc) => SalesReportService.Order(x, column, desc));
                var totals = SalesReportService.Totals(facts);
                // landed cost'ning sotilganga tushgan qismi — faqat filtrsiz (agent/mijoz/tovar bo'yicha taqsimlanmaydi)
                bool whole = reportScope.IsUnrestricted && f.AgentId == null && f.CustomerId == null && f.TerritoryId == null && f.CategoryId == null && f.ProductId == null && f.WarehouseId == null;
                decimal? landed = cost && whole ? SalesReportService.LandedCostOnSold(db, f.From, f.To) : (decimal?)null;

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    totals = new
                    {
                        amount = totals.Amount, returns = totals.ReturnAmount, net = totals.Net,
                        profit = cost ? totals.Profit : (decimal?)null, margin = cost ? totals.MarginPct : null,
                        orders = totals.Orders, customers = totals.Customers,
                        landed, netProfit = landed.HasValue ? totals.Profit - landed.Value : (decimal?)null
                    },
                    data = result.Rows.Select(r => new
                    {
                        key = r.Key, name = r.Name ?? (r.Key.HasValue ? "#" + r.Key : DstLocalization.T("Report_NotSet")),
                        qty = dimension == SalesDimension.Product ? r.Qty - r.ReturnQty : (decimal?)null,
                        amount = r.Amount, returns = r.ReturnAmount, net = r.Net,
                        profit = cost ? r.Profit : (decimal?)null, margin = cost ? r.MarginPct : null,
                        orders = r.Orders, customers = r.Customers
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult ExportSales(string dim, DateTime? from, DateTime? to, int? agentId, int? customerId, int? territoryId,
            int? categoryId, int? productId, int? warehouseId)
        {
            var f = Filter(from, to, agentId, customerId, territoryId, categoryId, productId, warehouseId);
            var dimension = ParseDimension(dim);
            using (var db = NewDb())
            {
                var facts = SalesReportService.Facts(db, f, ReportScope.For(db, CurrentUserName));
                var rows = SalesReportService.Order(SalesReportService.ByDimension(db, facts, dimension, IsRussian), "net", true)
                    .Take(ExportLimit).ToList();
                var columns = new List<ExcelExport.Column<SalesRow>>
                {
                    ExcelExport.Col<SalesRow>(DstLocalization.T("Report_Dim_" + dimension), r => r.Name ?? DstLocalization.T("Report_NotSet"))
                };
                if (dimension == SalesDimension.Product)
                    columns.Add(ExcelExport.Col<SalesRow>(DstLocalization.T("Qty"), r => r.Qty - r.ReturnQty, "#,##0.###"));
                columns.Add(ExcelExport.Col<SalesRow>(DstLocalization.T("Report_Shipped"), r => r.Amount, "#,##0"));
                columns.Add(ExcelExport.Col<SalesRow>(DstLocalization.T("Returns"), r => r.ReturnAmount, "#,##0"));
                columns.Add(ExcelExport.Col<SalesRow>(DstLocalization.T("Report_Revenue"), r => r.Net, "#,##0"));
                if (CanSeeCost)
                {
                    columns.Add(ExcelExport.Col<SalesRow>(DstLocalization.T("Report_Cost"), r => r.NetCost, "#,##0"));
                    columns.Add(ExcelExport.Col<SalesRow>(DstLocalization.T("Report_GrossProfit"), r => r.Profit, "#,##0"));
                    columns.Add(ExcelExport.Col<SalesRow>(DstLocalization.T("Report_Margin"), r => r.MarginPct, "0.0"));
                }
                columns.Add(ExcelExport.Col<SalesRow>(DstLocalization.T("Orders"), r => r.Orders));
                columns.Add(ExcelExport.Col<SalesRow>(DstLocalization.T("Customers"), r => r.Customers));

                var bytes = ExcelExport.Create(DstLocalization.T("Report_Sales"), rows, columns.ToArray());
                return File(bytes, ExcelExport.ContentType, string.Format("sales_{0}_{1:yyyyMMdd}_{2:yyyyMMdd}.xlsx", dimension.ToString().ToLowerInvariant(), f.From, f.To));
            }
        }

        // ---------- tashriflar ----------

        public ActionResult Visits(DateTime? from, DateTime? to, int? agentId, int? territoryId)
        {
            var f = Filter(from, to, agentId, null, territoryId, null, null, null);
            using (var db = NewDb())
            {
                FillLookups(db, f);
                return View(VisitReportService.ByAgent(db, f, ReportScope.For(db, CurrentUserName)));
            }
        }

        public ActionResult ExportVisits(DateTime? from, DateTime? to, int? agentId, int? territoryId)
        {
            var f = Filter(from, to, agentId, null, territoryId, null, null, null);
            using (var db = NewDb())
            {
                var rows = VisitReportService.ByAgent(db, f, ReportScope.For(db, CurrentUserName));
                var bytes = ExcelExport.Create(DstLocalization.T("Report_Visits"), rows,
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Agent"), r => r.Agent),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_WorkDays"), r => r.WorkDays),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_Plan"), r => r.Planned),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_Fact"), r => r.Visits),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_PlannedVisited"), r => r.PlannedVisited),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_Unplanned"), r => r.Unplanned),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_Coverage"), r => r.CoveragePct, "0.0"),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_WithOrder"), r => r.WithOrder),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_Strike"), r => r.StrikePct, "0.0"),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_WithPayment"), r => r.WithPayment),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_NoOrder"), r => r.NoOrder),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_OrderTotal"), r => r.OrderTotal, "#,##0"),
                    ExcelExport.Col<VisitReportService.AgentRow>(DstLocalization.T("Report_AvgOrder"), r => r.AvgOrder, "#,##0"));
                return File(bytes, ExcelExport.ContentType, string.Format("visits_{0:yyyyMMdd}_{1:yyyyMMdd}.xlsx", f.From, f.To));
            }
        }

        // ---------- agentlar topshirgan pul ----------

        public ActionResult Cash(DateTime? from, DateTime? to, int? agentId)
        {
            var f = Filter(from, to, agentId, null, null, null, null, null);
            using (var db = NewDb())
            {
                FillLookups(db, f);
                return View(CashReportService.ByAgent(db, f, ReportScope.For(db, CurrentUserName)));
            }
        }

        private static readonly string[] CashColumns = { "docNo", "date", "agent", "account", "declared", "received", "difference", "status" };

        [HttpGet]
        public async Task<ActionResult> CashData(DateTime? from, DateTime? to, int? agentId, byte? status)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 1, defaultDescending: true);
            var f = Filter(from, to, agentId, null, null, null, null, null);
            using (var db = NewDb())
            {
                var q = CashReportService.Handovers(db, f, ReportScope.For(db, CurrentUserName), (DocumentStatus?)status);
                var column = page.OrderColumn >= 0 && page.OrderColumn < CashColumns.Length ? CashColumns[page.OrderColumn] : "date";
                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(r => r.DocNo.Contains(s) || r.Agent.Contains(s)),
                    (x, c, desc) =>
                    {
                        switch (column)
                        {
                            case "docNo": return ServerTablePage.By(x, r => r.DocNo, desc).ThenBy(r => r.ID);
                            case "agent": return ServerTablePage.By(x, r => r.Agent, desc).ThenBy(r => r.ID);
                            case "declared": return ServerTablePage.By(x, r => r.Declared, desc).ThenBy(r => r.ID);
                            case "received": return ServerTablePage.By(x, r => r.Received, desc).ThenBy(r => r.ID);
                            case "difference": return ServerTablePage.By(x, r => r.Received - r.Declared, desc).ThenBy(r => r.ID);
                            default: return ServerTablePage.By(x, r => r.DocDate, desc).ThenBy(r => r.ID);
                        }
                    });
                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID, docNo = r.DocNo, date = r.DocDate.ToString("dd.MM.yyyy"), agent = r.Agent, account = r.Account,
                        declared = r.Declared, received = r.Received, difference = r.Difference,
                        status = (byte)r.Status, statusText = DstLocalization.T("Report_Handover_" + r.Status), note = r.Note
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult ExportCash(DateTime? from, DateTime? to, int? agentId, byte? status)
        {
            var f = Filter(from, to, agentId, null, null, null, null, null);
            using (var db = NewDb())
            {
                var rows = CashReportService.Handovers(db, f, ReportScope.For(db, CurrentUserName), (DocumentStatus?)status)
                    .OrderByDescending(r => r.DocDate).ThenByDescending(r => r.ID).Take(ExportLimit).ToList();
                var bytes = ExcelExport.Create(DstLocalization.T("Report_Cash"), rows,
                    ExcelExport.Col<CashReportService.HandoverRow>(DstLocalization.T("Report_DocNo"), r => r.DocNo),
                    ExcelExport.Col<CashReportService.HandoverRow>(DstLocalization.T("Date"), r => r.DocDate, "dd.MM.yyyy"),
                    ExcelExport.Col<CashReportService.HandoverRow>(DstLocalization.T("Agent"), r => r.Agent),
                    ExcelExport.Col<CashReportService.HandoverRow>(DstLocalization.T("Payment_Account"), r => r.Account),
                    ExcelExport.Col<CashReportService.HandoverRow>(DstLocalization.T("Cash_Declared"), r => r.Declared, "#,##0"),
                    ExcelExport.Col<CashReportService.HandoverRow>(DstLocalization.T("Cash_Received"), r => r.Received, "#,##0"),
                    ExcelExport.Col<CashReportService.HandoverRow>(DstLocalization.T("Cash_Difference"), r => r.Difference, "#,##0"),
                    ExcelExport.Col<CashReportService.HandoverRow>(DstLocalization.T("Status"), r => DstLocalization.T("Report_Handover_" + r.Status)),
                    ExcelExport.Col<CashReportService.HandoverRow>(DstLocalization.T("Comment"), r => r.Note));
                return File(bytes, ExcelExport.ContentType, string.Format("handovers_{0:yyyyMMdd}_{1:yyyyMMdd}.xlsx", f.From, f.To));
            }
        }
    }
}
