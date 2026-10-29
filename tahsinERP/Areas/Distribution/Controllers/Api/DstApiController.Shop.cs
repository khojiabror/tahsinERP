using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Areas.Distribution.Controllers.Api
{
    /// <summary>
    /// Telegram do'kon: mijozning o'zi Mini App'da katalog → savat → buyurtma (agentsiz; Savdo moduli, SAVDO paketida ham).
    /// Mijoz Telegram hisobi orqali aniqlanadi (TELEGRAM_ACCOUNTS, CUSTOMER roli) — bot faqat telegramUserId yuboradi, mijozni ERP topadi.
    /// Buyurtma — qoralama (yangi holat), sozlamadagi mas'ul xodim nomidan; operator tasdiqlaydi (rezerv, qarz limiti — odatdagidek).
    /// TO'LOV YO'Q: tizim onlayn to'lov qabul qilmaydi va pulga aralashmaydi — mijoz firmaning o'ziga to'laydi (savatda firma yozgan
    /// to'lov tartibi matni ko'rsatiladi, xolos). Chegirma berilmaydi (aksiyalar avtomatik hisoblanadi).
    /// </summary>
    public partial class DstApiController
    {
        /// <summary>"Kam qoldi" chegarasi (asosiy birlikda) — mijozga aniq miqdor ko'rsatilmaydi.</summary>
        private const decimal ShopLowStock = 10m;

        private sealed class ShopConfig
        {
            public bool Enabled { get; set; }
            public bool Signup { get; set; }
            public int? UserId { get; set; }
            public string UserName { get; set; }
            public string PaymentNote { get; set; }
            public bool ShowStock { get; set; }
        }

        private static ShopConfig Shop(DistributionDbContext db)
        {
            var userId = DstSettings.Int(db, DstSetting.ShopUserId);
            var user = userId.HasValue
                ? db.Users.Where(u => u.ID == userId && u.IsActive && u.IsDeleted != true).Select(u => u.Uname).FirstOrDefault()
                : null;
            return new ShopConfig
            {
                // mas'ul xodimsiz do'kon ishlamaydi: buyurtma kimning nomidan yozilishi va kimga xabar borishi kerak
                Enabled = DstSettings.Get(db, DstSetting.ShopEnabled) == "1" && user != null,
                Signup = DstSettings.Get(db, DstSetting.ShopSignup) == "1",
                UserId = user != null ? userId : null,
                UserName = user,
                PaymentNote = DstSettings.Get(db, DstSetting.ShopPaymentNote),
                ShowStock = DstSettings.Get(db, DstSetting.ShopShowStock) == "1"
            };
        }

        private static ShopConfig RequireShop(DistributionDbContext db)
        {
            var shop = Shop(db);
            if (!shop.Enabled) throw new DstValidationException(DstLocalization.T("Shop_Disabled"));
            return shop;
        }

        /// <summary>Bog'lanmagan foydalanuvchi uchun (bot /start): do'kon yoqilganmi, telefon bilan ro'yxatdan o'tish mumkinmi.</summary>
        [HttpGet]
        public ActionResult ShopPublic()
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                {
                    var shop = Shop(db);
                    return new { enabled = shop.Enabled, signup = shop.Enabled && shop.Signup };
                }
            });
        }

        /// <summary>
        /// Telefon raqami bilan ro'yxatdan o'tish (bot Telegram "kontakt yuborish" orqali — raqam foydalanuvchining o'ziniki ekanini bot
        /// tekshiradi). Raqam bitta mijozga mos kelsa — shunga bog'lanadi, aks holda yangi mijoz (tasdiqlanmagan) yaratiladi.
        /// </summary>
        [HttpPost]
        public ActionResult ShopRegister(long telegramUserId, long chatId, string phone, string name, string language = null)
        {
            return RunSystem(() =>
            {
                using (var uow = Uow(AuditActor.Shop))
                {
                    var db = uow.Dst;
                    var shop = RequireShop(db);
                    if (!shop.Signup) throw new DstValidationException(DstLocalization.T("Shop_SignupDisabled"));
                    var existing = db.TelegramAccounts.FirstOrDefault(a => a.TelegramUserId == telegramUserId && a.IsActive);
                    if (existing != null) return AccountInfo(db, existing);   // allaqachon bog'langan (xodim yoki mijoz)

                    var normalized = DirectoryRules.NormalizePhone(phone) ?? throw new DstValidationException(DstLocalization.T("Error_PhoneFormat"));
                    var matches = db.Customers.Where(c => c.IsDeleted != true
                                                          && (c.Telephone == normalized || db.CustomerProfiles.Any(p => p.CustomerID == c.ID && p.Phone2 == normalized)))
                        .Select(c => c.ID).Take(2).ToList();
                    var now = DateTime.Now;
                    int customerId;
                    bool created = matches.Count != 1;
                    if (!created) customerId = matches[0];
                    else
                    {
                        var title = string.IsNullOrWhiteSpace(name) ? normalized : name.Trim();
                        customerId = CustomerService.Save(uow, new CustomerInput
                        {
                            Name = title.Length > CustomerService.NameMaxLength ? title.Substring(0, CustomerService.NameMaxLength) : title,
                            Telephone = normalized, ContactPersonName = name
                        }, shop.UserId, AuditActor.Shop, now);
                    }
                    var account = TelegramLinkService.Attach(db, telegramUserId, chatId, null, customerId, BotRoles.Customer, now, language);
                    string customerName = db.Customers.Where(c => c.ID == customerId).Select(c => c.Name).FirstOrDefault();
                    NotificationService.ToUser(db, shop.UserId, NotificationService.ShopCustomer,
                        l => DstLocalization.TFor(l, created ? "Notify_ShopNewCustomer" : "Notify_ShopLinkedCustomer", customerName, normalized), null, now);
                    db.SaveChanges();
                    uow.Commit();
                    return AccountInfo(db, account);
                }
            });
        }

        /// <summary>Mini App sarlavhasi: mijoz nomi, to'lov tartibi matni, qoldiq ko'rsatiladimi.</summary>
        [HttpGet]
        public ActionResult ShopInfo(long telegramUserId)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                {
                    var shop = RequireShop(db);
                    var customerId = CustomerOf(db, telegramUserId);
                    var approved = db.CustomerProfiles.Where(p => p.CustomerID == customerId).Select(p => (bool?)p.IsApproved).FirstOrDefault() ?? false;
                    return new
                    {
                        name = db.Customers.Where(c => c.ID == customerId).Select(c => c.Name).FirstOrDefault(),
                        paymentNote = shop.PaymentNote, showStock = shop.ShowStock, approved,
                        minOrder = DstSettings.Decimal(db, DstSetting.MinOrderAmount)
                    };
                }
            });
        }

        [HttpGet]
        public ActionResult ShopCategories(long telegramUserId)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                {
                    RequireShop(db);
                    CustomerOf(db, telegramUserId);
                    return db.Categories.Where(c => c.IsDeleted != true && c.IsActive).OrderBy(c => c.Name)
                        .Select(c => new { id = c.ID, name = c.Name }).ToList();
                }
            });
        }

        /// <summary>Katalog: mijoz narxi bilan; qoldiq miqdori ko'rsatilmaydi — faqat holat (sozlamaga ko'ra) va "mavjud emas".</summary>
        [HttpGet]
        public ActionResult ShopCatalog(long telegramUserId, string q, int? categoryId)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                {
                    var shop = RequireShop(db);
                    var customerId = CustomerOf(db, telegramUserId);
                    return CatalogService.Search(db, q, customerId, null, categoryId, DateTime.Now, 50)
                        .Select(i => new
                        {
                            i.Id, i.PNo, i.Name, i.Units, i.Promo,
                            stock = i.Available <= 0 ? "out" : !shop.ShowStock ? null : i.Available <= ShopLowStock ? "low" : "in"
                        }).ToList();
                }
            });
        }

        public sealed class ApiShopOrderInput
        {
            public long TelegramUserId { get; set; }
            public bool IsPickup { get; set; }
            public string Address { get; set; }
            public string Comment { get; set; }
            public Guid? ClientRequestId { get; set; }
            public List<OrderLineInput> Lines { get; set; } = new List<OrderLineInput>();
        }

        /// <summary>Mijoz buyurtmasi → qoralama (operator tasdiqlaydi). ClientRequestId bilan takror yuborish xavfsiz. To'lov qadami yo'q.</summary>
        [HttpPost]
        public ActionResult ShopOrder(ApiShopOrderInput input)
        {
            return RunSystem(() =>
            {
                if (input == null || input.Lines == null || input.Lines.Count == 0 || input.Lines.Count > 200)
                    throw new DstValidationException(DstLocalization.T("Order_NoLines"));
                using (var uow = Uow(AuditActor.Shop))
                {
                    var db = uow.Dst;
                    var shop = RequireShop(db);
                    var customerId = CustomerOf(db, input.TelegramUserId);
                    var actor = OrderActor.ForUser(db, shop.UserName, AuditActor.Shop);
                    actor.MaxDiscountPct = null;   // mijoz chegirma bermaydi (aksiyalar — avtomatik)
                    actor.DiscountUnlimited = false;
                    var now = DateTime.Now;

                    var lang = DstLocalization.Normalize(db.TelegramAccounts.Where(a => a.TelegramUserId == input.TelegramUserId && a.IsActive)
                        .Select(a => a.Language).FirstOrDefault());
                    string delivery = input.IsPickup
                        ? DstLocalization.T("Shop_CommentPickup")
                        : DstLocalization.T("Shop_CommentDelivery") + (string.IsNullOrWhiteSpace(input.Address) ? "" : ": " + input.Address.Trim());
                    string comment = "Telegram · " + delivery + (string.IsNullOrWhiteSpace(input.Comment) ? "" : " · " + input.Comment.Trim());
                    var id = OrderService.SaveDraft(uow, new OrderInput
                    {
                        CustomerID = customerId, IsPickup = input.IsPickup, ClientRequestId = input.ClientRequestId,
                        Comment = comment.Length > 500 ? comment.Substring(0, 500) : comment,
                        Lines = input.Lines.Select(l => new OrderLineInput { ProductID = l.ProductID, ProductUnitID = l.ProductUnitID, Qty = l.Qty }).ToList()
                    }, actor, now);
                    var order = db.Orders.Find(id);
                    string customerName = db.Customers.Where(c => c.ID == customerId).Select(c => c.Name).FirstOrDefault();
                    // takror yuborilganda (shu ClientRequestId) xabar qayta ketmasin
                    string payload = "{\"action\":\"shopOrder\",\"orderId\":" + id + "}";
                    if (!db.Notifications.Any(n => n.Type == NotificationService.ShopOrder && n.Payload == payload))
                    {
                        NotificationService.ToUser(db, shop.UserId, NotificationService.ShopOrder,
                            l => DstLocalization.TFor(l, "Notify_ShopOrder", order.DocNo, customerName, order.Total.ToString("N0")), payload, now);
                        NotificationService.ToCustomer(db, customerId, NotificationService.ShopOrder,
                            l => DstLocalization.TFor(l, "Notify_ShopOrderReceived", order.DocNo, order.Total.ToString("N0")), payload, now);
                    }
                    db.SaveChanges();
                    uow.Commit();
                    return new { id, order.DocNo, order.Total };
                }
            });
        }

        /// <summary>Mijozning so'nggi buyurtmalari (holati bilan) — Mini App "Buyurtmalarim".</summary>
        [HttpGet]
        public ActionResult ShopOrders(long telegramUserId)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                {
                    RequireShop(db);
                    var customerId = CustomerOf(db, telegramUserId);
                    return db.Orders.Where(o => o.CustomerID == customerId).OrderByDescending(o => o.ID).Take(20)
                        .Select(o => new { o.ID, o.DocNo, o.DocDate, o.Total, o.OrderStatus, o.IsPickup }).ToList()
                        .Select(o => new { o.ID, o.DocNo, date = o.DocDate.ToString("dd.MM.yyyy"), o.Total, status = ((OrderStatus)o.OrderStatus).ToString(), o.IsPickup })
                        .ToList();
                }
            });
        }
    }
}
