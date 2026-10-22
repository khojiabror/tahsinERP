using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Yo'l varaqasi formasining AJAX endpointlari (LOG-M01: WaybillController'dan ajratildi).
    /// Ruxsat "Waybill" yozuvlari bo'yicha; Get/Search — ko'rish huquqi (LOG-S02).
    /// </summary>
    [Permission(Controller = "Waybill")]
    public class WaybillLookupController : BaseController
    {
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> GetTariffsByContract(int? contractId)
        {
            if (!contractId.HasValue)
            {
                return Json(new { success = false, message = L.T("Log_ContractIdRequired") });
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var tariffs = await db.F_CONTRACT_TARIFFS.AsNoTracking()
                    .Include(t => t.F_TRANSPORT_TYPES)
                    .Where(t => t.ContractID == contractId.Value && t.IsActive)
                    .OrderByDescending(t => t.CreatedDT)
                    .ToListAsync();

                var result = tariffs.Select(t => new
                {
                    id = t.ID,
                    text = WaybillService.FormatTariffDisplay(t),
                    rate = t.Rate,
                    basis = t.FreightBasis,
                    currencyId = t.CurrencyID,
                    transportTypeId = t.TransportTypeID,
                    minimumCharge = t.MinimumCharge
                }).ToList();

                return Json(new { success = true, items = result });
            }
        }

        /// <summary>Hisob-fakturadan yo'nalish, joylar, valyuta va to'lovchini avtomatik to'ldirish.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> GetDataFromInvoice(int? invoiceId, string invoiceType)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (invoiceType == "Purchase" && invoiceId.HasValue)
                {
                    var invoice = await db.P_INVOICES.AsNoTracking()
                        .Include(i => i.SUPPLIER)
                        .FirstOrDefaultAsync(i => i.ID == invoiceId.Value);

                    if (invoice != null)
                    {
                        return Json(new
                        {
                            success = true,
                            fromLocation = invoice.SUPPLIER?.Address,
                            toLocation = L.T("Log_OurWarehouse"),
                            direction = 0, // Kiruvchi
                            currencyId = invoice.CurrencyID,
                            payerType = 0 // Bizning kompaniya
                        });
                    }
                }
                else if (invoiceType == "Sales" && invoiceId.HasValue)
                {
                    var invoice = await db.SALES_INVOICES.AsNoTracking()
                        .Include(i => i.CUSTOMER)
                        .FirstOrDefaultAsync(i => i.ID == invoiceId.Value);

                    if (invoice != null)
                    {
                        return Json(new
                        {
                            success = true,
                            fromLocation = L.T("Log_OurWarehouse"),
                            toLocation = invoice.CUSTOMER?.Address,
                            direction = 1, // Chiquvchi
                            currencyId = invoice.CurrencyID,
                            payerType = 1 // Mijoz
                        });
                    }
                }

                return Json(new { success = false, message = L.T("Log_InvoiceNotFound") });
            }
        }

        /// <summary>
        /// select2 AJAX qidiruvi: type = pinvoice | sinvoice | sorder | porder | ttnk; eng so'nggi 20 ta mos yozuv.
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> SearchDocuments(string type, string q)
        {
            const int take = 20;
            q = (q ?? "").Trim();

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                List<SearchItem> items;
                switch (type)
                {
                    case "pinvoice":
                        items = await db.P_INVOICES.AsNoTracking()
                            .Where(i => i.IsDeleted != true && i.InvoiceNo.Contains(q))
                            .OrderByDescending(i => i.ID).Take(take)
                            .Select(i => new SearchItem { id = i.ID, text = i.InvoiceNo }).ToListAsync();
                        break;
                    case "sinvoice":
                        items = await db.SALES_INVOICES.AsNoTracking()
                            .Where(i => i.InvoiceNo.Contains(q))
                            .OrderByDescending(i => i.ID).Take(take)
                            .Select(i => new SearchItem { id = i.ID, text = i.InvoiceNo }).ToListAsync();
                        break;
                    case "sorder":
                        items = await db.S_ORDERS.AsNoTracking()
                            .Where(o => o.IsDeleted != true && o.OrderNo.Contains(q))
                            .OrderByDescending(o => o.ID).Take(take)
                            .Select(o => new SearchItem { id = o.ID, text = o.OrderNo }).ToListAsync();
                        break;
                    case "porder":
                        items = await db.P_ORDERS.AsNoTracking()
                            .Where(o => o.IsDeleted != true && o.OrderNo.Contains(q))
                            .OrderByDescending(o => o.ID).Take(take)
                            .Select(o => new SearchItem { id = o.ID, text = o.OrderNo }).ToListAsync();
                        break;
                    case "ttnk":
                        items = await db.SALES_TTNK_HEADERS.AsNoTracking()
                            .Where(t => t.TTNKNumber.Contains(q))
                            .OrderByDescending(t => t.ID).Take(take)
                            .Select(t => new SearchItem { id = t.ID, text = t.TTNKNumber }).ToListAsync();
                        break;
                    default:
                        items = new List<SearchItem>();
                        break;
                }

                return Json(new { results = items }, JsonRequestBehavior.AllowGet);
            }
        }

        public sealed class SearchItem
        {
            // select2 kutadigan nomlar
            public long id { get; set; }
            public string text { get; set; }
        }
    }
}
