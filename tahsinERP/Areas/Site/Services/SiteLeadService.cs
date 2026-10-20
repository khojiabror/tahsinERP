using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Site.Data;

namespace tahsinERP.Areas.Site.Services
{
    public sealed class LeadItemInput
    {
        public int ProductId { get; set; }
        public int? ProductUnitId { get; set; }
        public decimal Qty { get; set; }
    }

    public sealed class LeadInput
    {
        public Guid ClientRequestId { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string CompanyName { get; set; }
        public string Inn { get; set; }
        public string Message { get; set; }
        public string Language { get; set; }
        public string Host { get; set; }
        public List<LeadItemInput> Items { get; set; } = new List<LeadItemInput>();
    }

    /// <summary>Ariza qatori (site.LEADS.Items JSON): yuborilgan paytdagi nom, birlik va narx bilan.</summary>
    public sealed class LeadItem
    {
        public int ProductId { get; set; }
        public int? ProductUnitId { get; set; }
        public decimal Qty { get; set; }
        public string Sku { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal? Price { get; set; }
    }

    /// <summary>
    /// Saytdan kelgan arizalar: qabul (SiteApi), ko'rish, buyurtmaga aylantirish (mavjud yoki yangi mijoz + dst.D_ORDERS qoralama),
    /// rad etish. Yangi ariza — rahbarlarga Telegram xabari (mavjud bildirishnoma navbati).
    /// </summary>
    public static class SiteLeadService
    {
        public const string NotificationType = "SITE_LEAD";
        public const int MaxItems = 100;

        public static int Receive(DistributionDbContext db, LeadInput input, DateTime now)
        {
            if (input == null || input.ClientRequestId == Guid.Empty) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var existing = db.SiteLeads.Where(l => l.ClientRequestId == input.ClientRequestId).Select(l => (int?)l.ID).FirstOrDefault();
            if (existing.HasValue) return existing.Value;   // takroriy yuborish — o'sha ariza

            var settings = SiteSettingsStore.All(db);
            if (SiteSettingsStore.Get(settings, SiteSettingKeys.LeadsEnabled, "1") != "1")
                throw new DstValidationException(DstLocalization.T("Site_LeadsDisabled"));
            var name = SiteText.Clean(input.Name, 100);
            if (name == null || name.Length < 2) throw new DstValidationException(DstLocalization.T("Error_NameRequired"));
            var phone = DirectoryRules.NormalizePhone(input.Phone) ?? throw new DstValidationException(DstLocalization.T("Error_PhoneFormat"));
            var items = (input.Items ?? new List<LeadItemInput>()).Where(i => i != null).ToList();
            if (items.Count > MaxItems || items.Any(i => i.Qty <= 0 || i.Qty > 1000000))
                throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));

            var lead = new SiteLead
            {
                ClientRequestId = input.ClientRequestId, CreatedAt = now, Name = name, Phone = phone,
                CompanyName = SiteText.Clean(input.CompanyName, 200), INN = SiteText.Clean(input.Inn, 20), Message = SiteText.Clean(input.Message, 1000),
                Language = input.Language == "ru" ? "ru" : "uz", Host = SiteText.Clean(input.Host, 253),
                Items = items.Count == 0 ? null : JsonConvert.SerializeObject(Describe(db, items, settings, now)), Status = (byte)LeadStatus.New
            };
            db.SiteLeads.Add(lead);
            db.SaveChanges();
            NotificationService.ToManagers(db, NotificationType,
                l => DstLocalization.TFor(l, "Notify_SiteLead", lead.ID, lead.CompanyName ?? lead.Name, lead.Phone, items.Count), null, now);
            db.SaveChanges();
            return lead.ID;
        }

        /// <summary>Faqat nashr qilingan tovarlar; nom, birlik va (narx ko'rsatilsa) narx yozib qo'yiladi.</summary>
        private static List<LeadItem> Describe(DistributionDbContext db, List<LeadItemInput> items, IReadOnlyDictionary<string, string> settings, DateTime now)
        {
            var ids = items.Select(i => i.ProductId).Distinct().ToList();
            var products = (from c in db.SiteProducts
                            join p in db.Products on c.ProductID equals p.ID
                            where ids.Contains(c.ProductID) && c.IsPublished && !p.IsDeleted
                            select new { p.ID, p.PNo, Name = c.TitleUz ?? p.Name }).ToList().ToDictionary(p => p.ID);
            if (products.Count != ids.Count) throw new DstValidationException(DstLocalization.T("Site_ProductUnavailable"));
            var units = new UnitService(db);
            bool showPrice = SiteSettingsStore.Get(settings, SiteSettingKeys.PriceMode) == PriceModes.Show;
            var prices = showPrice ? new PricingService(db).Resolve(null, ids, now) : new Dictionary<int, ResolvedPrice>();
            return items.Select(i =>
            {
                var unit = units.UnitsOf(i.ProductId).FirstOrDefault(u => u.ProductUnitID == i.ProductUnitId)
                           ?? throw new DstValidationException(DstLocalization.T("Site_ProductUnavailable"));
                var p = products[i.ProductId];
                decimal? price = prices.TryGetValue(i.ProductId, out var rp) && rp.Found ? UnitMath.UnitPrice(rp.Price.Value, unit.Factor) : (decimal?)null;
                return new LeadItem { ProductId = i.ProductId, ProductUnitId = i.ProductUnitId, Qty = i.Qty, Sku = p.PNo, Name = p.Name, Unit = unit.UnitName, Price = price };
            }).ToList();
        }

        public static List<LeadItem> ItemsOf(SiteLead lead) =>
            string.IsNullOrEmpty(lead.Items) ? new List<LeadItem>() : JsonConvert.DeserializeObject<List<LeadItem>>(lead.Items);

        /// <summary>Telefon bo'yicha mavjud mijozlar (asosiy telefon yoki profildagi ikkinchi raqam).</summary>
        public static List<(int Id, string Name)> MatchingCustomers(DistributionDbContext db, string phone)
        {
            return (from c in db.Customers
                    where c.IsDeleted != true && (c.Telephone == phone || db.CustomerProfiles.Any(p => p.CustomerID == c.ID && p.Phone2 == phone))
                    orderby c.Name
                    select new { c.ID, c.Name }).Take(10).ToList().Select(x => (x.ID, x.Name)).ToList();
        }

        public static void MarkSeen(DistributionDbContext db, SiteLead lead)
        {
            if (lead.Status == (byte)LeadStatus.New) lead.Status = (byte)LeadStatus.Seen;
        }

        /// <summary>
        /// Arizadan buyurtma: mijoz berilmasa — yangi mijoz (sayt orqali, tasdiqlanmagan). Buyurtma qoralama holatida,
        /// ClientRequestId = arizaniki — ikki marta bosilsa ham bitta buyurtma. Chaqiruvchi Commit qiladi.
        /// </summary>
        public static int ConvertToOrder(DistributionUnitOfWork uow, int leadId, int? customerId, OrderActor actor, DateTime now)
        {
            var db = uow.Dst;
            var lead = db.SiteLeads.Find(leadId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (lead.Status == (byte)LeadStatus.Rejected) throw new DstValidationException(DstLocalization.T("Site_LeadRejected"));
            if (lead.OrderID.HasValue) return lead.OrderID.Value;
            var items = ItemsOf(lead);
            if (items.Count == 0) throw new DstValidationException(DstLocalization.T("Site_LeadNoItems"));

            if (customerId.HasValue)
            {
                if (!db.Customers.Any(c => c.ID == customerId && c.IsDeleted != true))
                    throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            }
            else
            {
                var title = lead.CompanyName ?? lead.Name;
                customerId = CustomerService.Save(uow, new CustomerInput
                {
                    Name = title.Length > CustomerService.NameMaxLength ? title.Substring(0, CustomerService.NameMaxLength) : title,
                    Telephone = lead.Phone, ContactPersonName = lead.Name, INN = lead.INN
                }, actor.UserId, "site", now);
            }

            var comment = DstLocalization.T("Site_LeadOrderComment", lead.ID) + (string.IsNullOrEmpty(lead.Message) ? "" : ": " + lead.Message);
            var orderId = OrderService.SaveDraft(uow, new OrderInput
            {
                CustomerID = customerId.Value, ClientRequestId = lead.ClientRequestId,
                Comment = comment.Length > 500 ? comment.Substring(0, 500) : comment,
                Lines = items.Select(i => new OrderLineInput { ProductID = i.ProductId, ProductUnitID = i.ProductUnitId, Qty = i.Qty }).ToList()
            }, actor, now);

            lead.CustomerID = customerId;
            lead.OrderID = orderId;
            lead.Status = (byte)LeadStatus.Ordered;
            lead.ProcessedBy = actor.UserId;
            lead.ProcessedAt = now;
            db.SaveChanges();
            return orderId;
        }

        public static void Reject(DistributionDbContext db, int leadId, string reason, int? userId, DateTime now)
        {
            var lead = db.SiteLeads.Find(leadId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (lead.Status == (byte)LeadStatus.Ordered) throw new DstValidationException(DstLocalization.T("Site_LeadAlreadyOrdered"));
            reason = SiteText.Clean(reason, 300) ?? throw new DstValidationException(DstLocalization.T("Error_CancelReasonRequired"));
            lead.Status = (byte)LeadStatus.Rejected;
            lead.RejectReason = reason;
            lead.ProcessedBy = userId;
            lead.ProcessedAt = now;
        }
    }
}
