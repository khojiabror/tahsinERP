using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Site.Data;
using tahsinERP.Areas.Site.Services;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Site.Controllers
{
    /// <summary>Saytdan kelgan arizalar: ro'yxat, ko'rish, buyurtmaga aylantirish, rad etish.</summary>
    public class SiteLeadsController : SiteControllerBase
    {
        public ActionResult Index(byte? status)
        {
            using (var db = NewDb())
            {
                var q = db.SiteLeads.AsQueryable();
                if (status.HasValue) q = q.Where(l => l.Status == status);
                else q = q.Where(l => l.Status == (byte)LeadStatus.New || l.Status == (byte)LeadStatus.Seen);
                ViewBag.Status = status;
                ViewBag.Counts = db.SiteLeads.GroupBy(l => l.Status).Select(g => new { g.Key, N = g.Count() }).ToList()
                    .ToDictionary(x => x.Key, x => x.N);
                return View(q.OrderByDescending(l => l.ID).Take(500).ToList());
            }
        }

        public ActionResult Details(int id)
        {
            using (var db = NewDb())
            {
                var lead = db.SiteLeads.Find(id);
                if (lead == null) return HttpNotFound();
                if (ViewBag.DstCanChange == true)
                {
                    SiteLeadService.MarkSeen(db, lead);
                    db.SaveChanges();
                }
                ViewBag.Items = SiteLeadService.ItemsOf(lead);
                ViewBag.Matches = SiteLeadService.MatchingCustomers(db, lead.Phone);
                ViewBag.CustomerName = lead.CustomerID.HasValue ? db.Customers.Where(c => c.ID == lead.CustomerID).Select(c => c.Name).FirstOrDefault() : null;
                ViewBag.OrderNo = lead.OrderID.HasValue ? db.Orders.Where(o => o.ID == lead.OrderID).Select(o => o.DocNo).FirstOrDefault() : null;
                return View(lead);
            }
        }

        /// <summary>Buyurtma yaratish (mavjud mijoz tanlansa — unga, aks holda yangi mijoz). Natija: buyurtma sahifasi.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Convert(int id, int? customerId)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    var actor = OrderActor.ForUser(uow.Dst, CurrentUserName);
                    var orderId = SiteLeadService.ConvertToOrder(uow, id, customerId, actor, Now);
                    uow.Commit();
                    return new { orderId, url = Url.Action("Details", "DstOrders", new { area = "Distribution", id = orderId }) };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Reject(int id, string reason)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    SiteLeadService.Reject(db, id, reason, CurrentUserId, Now);
                    db.SaveChanges();
                    return null;
                }
            });
        }
    }
}
