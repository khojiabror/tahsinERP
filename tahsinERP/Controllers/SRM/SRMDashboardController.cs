using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// SRM Dashboard - Jahon standartidagi ERP larda bo'ladigan ta'minotchi munosabatlari hisobotlari
    /// Spend, Aging, Delivery performance, Supplier performance
    /// </summary>
    public class SRMDashboardController : Controller
    {
        public async Task<ActionResult> Index(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                if (User?.Identity?.Name == null || !RoleHelper.IsViewPermitted(User.Identity.Name, "Supplier"))
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }

                fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
                toDate = toDate ?? DateTime.Now;
                // Foydalanuvchi tanlagan oxirgi kun to'liq kiradi (sana 00:00 bo'lgani uchun <= oxirgi kunni tashlab yuborardi, SRM-B24)
                var toExclusive = toDate.Value.Date.AddDays(1);

                using (var db = new DBTHSNEntities())
                {
                    // 1. Spend by Supplier - Xarajat ta'minotchi bo'yicha (guruhlash xotirada - decimal cast xatoligini oldini olish)
                    var invoiceData = await db.P_INVOICES
                        .Where(i => i.InvoiceDate >= fromDate.Value && i.InvoiceDate < toExclusive &&
                            (i.IsDeleted == null || i.IsDeleted == false))
                        .Select(i => new { i.SupplierID, i.Amount })
                        .ToListAsync();

                    var spendRaw = invoiceData
                        .GroupBy(i => i.SupplierID)
                        .Select(g => new { SupplierID = g.Key, InvoiceCount = g.Count(), TotalSpend = g.Sum(x => x.Amount) })
                        .OrderByDescending(x => x.TotalSpend)
                        .Take(15)
                        .ToList();

                var spendSupplierIds = spendRaw.Select(x => x.SupplierID).Distinct().ToList();
                Dictionary<int, string> spendSupplierNames;
                if (spendSupplierIds.Count == 0)
                    spendSupplierNames = new Dictionary<int, string>();
                else
                    spendSupplierNames = await db.SUPPLIERS
                        .Where(s => spendSupplierIds.Contains(s.ID))
                        .ToDictionaryAsync(s => s.ID, s => s.Name ?? L.T("Srm_Unknown"));

                var spendBySupplier = spendRaw.Select(x => new SRMSpendBySupplierItem
                {
                    SupplierID = x.SupplierID,
                    SupplierName = spendSupplierNames.ContainsKey(x.SupplierID) ? spendSupplierNames[x.SupplierID] : L.T("Srm_Unknown"),
                    InvoiceCount = x.InvoiceCount,
                    TotalSpend = Convert.ToDecimal(x.TotalSpend)
                }).ToList();

                // 2. Kreditorlik — Finance bilan yagona hisob (CounterpartyBalanceService: opening + invoyslar/kirimlar
                // − ta'minotchiga to'lovlar). Avval SRM'da alohida, barcha invoys/to'lovlarni xotirada O(S×K) guruhlaydigan
                // nusxa edi va natijasi Finance dashboard'idan farq qilardi (SRM-P09). Dashboard faqat musbat qarzni ko'rsatadi.
                var supplierOutstanding = new tahsinERP.Services.Finance.CounterpartyBalanceService(db)
                    .Suppliers(toDate.Value)
                    .Where(s => s.Outstanding > 0)
                    .ToList();
                var totalPayables = supplierOutstanding.Sum(s => s.Outstanding);

                // 3. On-time delivery (F_WAYBILLS - rejalashtirilgan vs faktik yetkazib berish)
                var waybillsWithDelivery = await db.F_WAYBILLS
                    .Where(w => w.PlannedDeliveryDate.HasValue && w.ActualDeliveryDate.HasValue &&
                        w.PlannedDeliveryDate >= fromDate.Value && w.PlannedDeliveryDate < toExclusive &&
                        (w.IsDeleted == null || w.IsDeleted == false))
                    .Select(w => new { w.PlannedDeliveryDate, w.ActualDeliveryDate })
                    .ToListAsync();

                var onTimeCount = waybillsWithDelivery.Count(w => w.ActualDeliveryDate <= w.PlannedDeliveryDate);
                var totalDeliveries = waybillsWithDelivery.Count;
                var onTimeRate = totalDeliveries > 0 ? (double)onTimeCount / totalDeliveries * 100 : 0;

                // 4. Summary KPIs
                var totalSuppliers = await db.SUPPLIERS.Where(s => s.IsDeleted != true).CountAsync();
                // Jami xarajat barcha ta'minotchilar bo'yicha (avval faqat top-15 yig'ilardi, SRM-B08)
                var totalSpend = Convert.ToDecimal(invoiceData.Sum(x => x.Amount ?? 0));
                var totalInvoices = invoiceData.Count;
                var totalContracts = await db.P_CONTRACTS.Where(c => c.IsDeleted != true).CountAsync();
                var totalOrders = await db.P_ORDERS
                    .Where(o => o.IssuedDate >= fromDate.Value && o.IssuedDate < toExclusive &&
                        (o.IsDeleted == null || o.IsDeleted == false))
                    .CountAsync();

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
                ViewBag.TotalSuppliers = totalSuppliers;
                ViewBag.TotalSpend = totalSpend;
                ViewBag.TotalInvoices = totalInvoices;
                ViewBag.TotalContracts = totalContracts;
                ViewBag.TotalOrders = totalOrders;
                ViewBag.TotalPayables = totalPayables;
                ViewBag.OnTimeRate = Math.Round(onTimeRate, 1);
                ViewBag.OnTimeCount = onTimeCount;
                ViewBag.TotalDeliveries = totalDeliveries;
                ViewBag.SpendBySupplier = spendBySupplier;

                var outstandingIds = supplierOutstanding.Select(x => x.SupplierID).Distinct().ToList();
                Dictionary<int, string> supplierNames;
                if (outstandingIds.Count == 0)
                    supplierNames = new Dictionary<int, string>();
                else
                    supplierNames = await db.SUPPLIERS
                        .Where(s => outstandingIds.Contains(s.ID))
                        .ToDictionaryAsync(s => s.ID, s => s.Name ?? L.T("Srm_Unknown"));

                ViewBag.PayablesBySupplier = supplierOutstanding
                    .OrderByDescending(s => s.Outstanding)
                    .Take(15)
                    .Select(s => new SRMPayablesBySupplierItem
                    {
                        SupplierID = s.SupplierID,
                        SupplierName = supplierNames.ContainsKey(s.SupplierID) ? supplierNames[s.SupplierID] : L.T("Srm_Unknown"),
                        Outstanding = s.Outstanding
                    })
                    .ToList();
                }

                return View();
            }
            catch (Exception ex)
            {
                // 500 xatosini oldini olish - xatolikni log qilish
                LogHelper.LogToDatabase(User?.Identity?.Name ?? "System", "SRMDashboard", "Index", $"Xatolik: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
    }
}
