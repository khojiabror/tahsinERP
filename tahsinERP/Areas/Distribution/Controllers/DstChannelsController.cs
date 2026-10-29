using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Site.Controllers;
using tahsinERP.Areas.Site.Data;
using tahsinERP.Areas.Site.Services;
using tahsinERP.Filters;
using tahsinERP.Services.Editions;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Sotuv kanallari: Telegram bot / Mini App va sayt-katalog bitta joyda — paketda nima ochiq, bot sozlamalari, sayt holati,
    /// kanallar qaysi ombor qoldig'i va narxidan foydalanadi. Kanallar Savdo (Commerce) moduliga tayanadi: SAVDO paketida
    /// (agentlarsiz) ham, DIST/KORP'da ham ishlaydi; dala (agent, marshrut, reys) — Distribution moduli.
    /// </summary>
    public class DstChannelsController : DstControllerBase
    {
        public sealed class ModuleRow
        {
            public string Name { get; set; }
            public string Hint { get; set; }
            public bool Allowed { get; set; }
        }

        public sealed class SiteSummary
        {
            public bool Allowed { get; set; }
            public bool Enabled { get; set; }
            public string Url { get; set; }
            public string PriceMode { get; set; }
            public string PriceType { get; set; }
            public string StockMode { get; set; }
            public bool LeadsEnabled { get; set; }
            public int Published { get; set; }
            public int NewLeads { get; set; }
        }

        public ActionResult Index()
        {
            string edition = EditionGate.CurrentEdition;
            bool field = EditionModules.IsModuleAllowed(edition, ProductModule.Distribution);
            bool website = EditionModules.IsModuleAllowed(edition, ProductModule.Website);
            ViewBag.Modules = new List<ModuleRow>
            {
                new ModuleRow { Name = DstLocalization.T("Channels_ModCommerce"), Hint = DstLocalization.T("Channels_ModCommerceHint"), Allowed = true },
                new ModuleRow { Name = DstLocalization.T("Channels_ModField"), Hint = DstLocalization.T("Channels_ModFieldHint"), Allowed = field },
                new ModuleRow { Name = DstLocalization.T("Channels_ModSite"), Hint = DstLocalization.T("Channels_ModSiteHint"), Allowed = website },
            };
            ViewBag.FieldEnabled = field;

            using (var db = NewDb())
            {
                ViewBag.BotUsername = Services.Bot.DstSettings.Get(db, DstSetting.BotUsername);
                ViewBag.BotPublicUrl = Services.Bot.DstSettings.Get(db, DstSetting.BotPublicUrl);
                ViewBag.LinkedStaff = db.TelegramAccounts.Count(a => a.IsActive && a.UserID != null);
                ViewBag.LinkedCustomers = db.TelegramAccounts.Count(a => a.IsActive && a.CustomerID != null);
                ViewBag.ShopEnabled = Services.Bot.DstSettings.Get(db, DstSetting.ShopEnabled) == "1";
                ViewBag.ShopSignup = Services.Bot.DstSettings.Get(db, DstSetting.ShopSignup) == "1";
                ViewBag.ShopShowStock = Services.Bot.DstSettings.Get(db, DstSetting.ShopShowStock) == "1";
                ViewBag.ShopUserId = Services.Bot.DstSettings.Int(db, DstSetting.ShopUserId);
                ViewBag.ShopPaymentNote = Services.Bot.DstSettings.Get(db, DstSetting.ShopPaymentNote);
                ViewBag.ShopUsers = db.Users.Where(u => u.IsActive && u.IsDeleted != true).OrderBy(u => u.FullName ?? u.Uname).ToList()
                    .Select(u => new SelectListItem { Value = u.ID.ToString(), Text = u.FullName ?? u.Uname }).ToList();
                var since = DateTime.Today.AddDays(-30);
                ViewBag.ShopOrders30 = db.Orders.Count(o => o.Source == Data.AuditActor.Shop && o.DocDate >= since);

                var salesIds = StockService.SalesWarehouses(db);
                ViewBag.SalesWarehouses = db.Warehouses.Where(w => salesIds.Contains(w.ID)).OrderBy(w => w.WHName).Select(w => w.WHName).ToList();
                int? defaultType = new PricingService(db).DefaultPriceTypeId();
                var priceTypes = db.PriceTypes.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToList();
                ViewBag.DefaultPriceType = priceTypes.Where(t => t.ID == defaultType).Select(t => Localized(t.Name, t.NameRu)).FirstOrDefault();

                var site = new SiteSummary { Allowed = website };
                if (website)
                {
                    var s = SiteSettingsStore.All(db);
                    site.Enabled = SiteSettingsStore.Get(s, SiteSettingKeys.Enabled) == "1";
                    site.Url = SiteControllerBase.SiteUrl(db);
                    site.PriceMode = SiteSettingsStore.Get(s, SiteSettingKeys.PriceMode, PriceModes.Ask);
                    site.StockMode = SiteSettingsStore.Get(s, SiteSettingKeys.StockMode, StockModes.Status);
                    site.LeadsEnabled = SiteSettingsStore.Get(s, SiteSettingKeys.LeadsEnabled, "1") == "1";
                    int.TryParse(SiteSettingsStore.Get(s, SiteSettingKeys.PriceTypeId), out var sitePriceType);
                    site.PriceType = priceTypes.Where(t => t.ID == (sitePriceType > 0 ? sitePriceType : defaultType)).Select(t => Localized(t.Name, t.NameRu)).FirstOrDefault();
                    site.Published = db.SiteProducts.Count(p => p.IsPublished);
                    site.NewLeads = db.SiteLeads.Count(l => l.Status == (byte)LeadStatus.New);
                }
                ViewBag.Site = site;
            }
            return View();
        }

        /// <summary>
        /// Telegram do'kon: mijoz Mini App'da o'zi buyurtma beradi. Buyurtma mas'ul xodim nomidan qoralama bo'ladi va unga xabar boradi.
        /// Onlayn to'lov yo'q — to'lov tartibi matni faqat ma'lumot (mijoz firmaga bevosita to'laydi).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveShop(bool enabled, bool signup, bool showStock, int? userId, string paymentNote)
        {
            return DstJson(() =>
            {
                paymentNote = string.IsNullOrWhiteSpace(paymentNote) ? null : paymentNote.Trim();
                if (paymentNote != null && paymentNote.Length > 400) throw new DstValidationException(DstLocalization.T("Channels_ShopNoteTooLong"));
                using (var db = NewDb())
                {
                    if (userId.HasValue && !db.Users.Any(u => u.ID == userId && u.IsActive && u.IsDeleted != true))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    if (enabled && !userId.HasValue) throw new DstValidationException(DstLocalization.T("Channels_ShopUserRequired"));
                    Services.Bot.DstSettings.Set(db, DstSetting.ShopEnabled, enabled ? "1" : "0");
                    Services.Bot.DstSettings.Set(db, DstSetting.ShopSignup, signup ? "1" : "0");
                    Services.Bot.DstSettings.Set(db, DstSetting.ShopShowStock, showStock ? "1" : "0");
                    Services.Bot.DstSettings.Set(db, DstSetting.ShopUserId, userId?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Services.Bot.DstSettings.Set(db, DstSetting.ShopPaymentNote, paymentNote);
                    db.SaveChanges();
                    return null;
                }
            });
        }

        /// <summary>Telegram bot nomi (t.me havolalari, ulash kodlari) va bot ochiq manzili (selfi/fotolar uchun imzoli havolalar).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveBot(string botUsername, string botPublicUrl)
        {
            return DstJson(() =>
            {
                botUsername = string.IsNullOrWhiteSpace(botUsername) ? null : botUsername.Trim().TrimStart('@');
                if (botUsername != null && (botUsername.Length > 64 || !botUsername.All(c => char.IsLetterOrDigit(c) || c == '_')))
                    throw new DstValidationException(DstLocalization.T("Channels_BotNameInvalid"));
                botPublicUrl = string.IsNullOrWhiteSpace(botPublicUrl) ? null : botPublicUrl.Trim().TrimEnd('/');
                if (botPublicUrl != null && (botPublicUrl.Length > 400 || !Uri.TryCreate(botPublicUrl, UriKind.Absolute, out var botUri)
                                             || (botUri.Scheme != Uri.UriSchemeHttps && botUri.Scheme != Uri.UriSchemeHttp)))
                    throw new DstValidationException(DstLocalization.T("Settings_BotUrlInvalid"));
                using (var db = NewDb())
                {
                    Services.Bot.DstSettings.Set(db, DstSetting.BotUsername, botUsername);
                    Services.Bot.DstSettings.Set(db, DstSetting.BotPublicUrl, botPublicUrl);
                    db.SaveChanges();
                    return null;
                }
            });
        }
    }
}
