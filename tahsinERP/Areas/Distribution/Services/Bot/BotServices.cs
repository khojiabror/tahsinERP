using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services.Bot
{
    /// <summary>dst.SETTINGS qiymatlari.</summary>
    public static class DstSettings
    {
        public static string Get(DistributionDbContext db, string key) =>
            db.Settings.Where(s => s.Key == key).Select(s => s.Value).FirstOrDefault();

        public static decimal Decimal(DistributionDbContext db, string key, decimal fallback = 0) =>
            DstModelBinder.TryParseDecimal(Get(db, key) ?? "", out var v) ? v : fallback;

        public static int? Int(DistributionDbContext db, string key) =>
            int.TryParse(Get(db, key), out var v) ? v : (int?)null;

        public static void Set(DistributionDbContext db, string key, string value)
        {
            var row = db.Settings.Find(key);
            if (row == null) db.Settings.Add(row = new DstSetting { Key = key });
            row.Value = value;
        }
    }

    /// <summary>
    /// Telegram'ga bog'lash: ERP'da bir martalik kod (3 kun) → bot /start kod → TELEGRAM_ACCOUNTS.
    /// Kod tasodifiy (kriptografik), chalkash belgilarsiz; bitta Telegram hisobi — bitta faol bog'lanish.
    /// </summary>
    public static class TelegramLinkService
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public static readonly TimeSpan Lifetime = TimeSpan.FromDays(3);

        public static string NewCode(int length = 10)
        {
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return new string(bytes.Select(b => Alphabet[b % Alphabet.Length]).ToArray());
        }

        public static TelegramLinkCode CreateCode(DistributionDbContext db, int? userId, int? customerId, string role, int createdBy, DateTime now)
        {
            if ((userId == null) == (customerId == null)) throw new ArgumentException("UserID yoki CustomerID (faqat bittasi).");
            if (customerId.HasValue && role != BotRoles.Customer || userId.HasValue && !BotRoles.Staff.Contains(role))
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            // eski ishlatilmagan kodlar bekor
            foreach (var old in db.TelegramLinkCodes.Where(c => c.UsedAt == null && c.UserID == userId && c.CustomerID == customerId).ToList())
                old.ExpiresAt = now;
            var code = new TelegramLinkCode
            {
                Code = NewCode(), UserID = userId, CustomerID = customerId, Role = role, ExpiresAt = now.Add(Lifetime),
                CreatedBy = createdBy, CreatedAt = now
            };
            db.TelegramLinkCodes.Add(code);
            return code;
        }

        public static TelegramAccount Redeem(DistributionDbContext db, string code, long telegramUserId, long chatId, DateTime now)
        {
            var c = db.TelegramLinkCodes.Find((code ?? "").Trim().ToUpperInvariant());
            if (c == null || c.UsedAt != null || c.ExpiresAt < now)
                throw new DstValidationException(DstLocalization.T("Bot_CodeInvalid"));
            if (c.UserID.HasValue && !db.Users.Any(u => u.ID == c.UserID && u.IsActive && u.IsDeleted != true))
                throw new DstValidationException(DstLocalization.T("Bot_CodeInvalid"));

            foreach (var a in db.TelegramAccounts.Where(a => a.IsActive && (a.TelegramUserId == telegramUserId
                         || (c.UserID != null && a.UserID == c.UserID) || (c.CustomerID != null && a.CustomerID == c.CustomerID))).ToList())
                a.IsActive = false;
            db.SaveChanges();   // UNIQUE (TelegramUserId) WHERE IsActive — avval eskisi o'chadi

            c.UsedAt = now;
            c.TelegramUserId = telegramUserId;
            var account = new TelegramAccount
            {
                TelegramUserId = telegramUserId, ChatId = chatId, UserID = c.UserID, CustomerID = c.CustomerID, Role = c.Role, LinkedAt = now
            };
            db.TelegramAccounts.Add(account);
            return account;
        }

        public static void Unlink(DistributionDbContext db, long telegramUserId)
        {
            foreach (var a in db.TelegramAccounts.Where(a => a.TelegramUserId == telegramUserId && a.IsActive).ToList())
                a.IsActive = false;
        }
    }

    /// <summary>
    /// Tranzaksion outbox: bildirishnoma biznes amal bilan BIR tranzaksiyada yoziladi (amal bekor bo'lsa — xabar ham yo'q).
    /// Matn qabul qiluvchining tilida tayyorlanadi; faqat Telegram'ga bog'langanlarga yoziladi.
    /// </summary>
    public static class NotificationService
    {
        public const string OrderConfirmed = "ORDER_CONFIRMED";
        public const string OrderCancelled = "ORDER_CANCELLED";
        public const string OrderShipped = "ORDER_SHIPPED";
        public const string OrderDelivered = "ORDER_DELIVERED";
        public const string LargeOrder = "LARGE_ORDER";
        public const string CreditOverLimit = "CREDIT_OVER_LIMIT";
        public const string PaymentReceived = "PAYMENT_RECEIVED";
        public const string HandoverPending = "HANDOVER_PENDING";
        public const string StoreOrderConfirmed = "STORE_ORDER";
        public const string NewCustomer = "NEW_CUSTOMER";
        public const string IssueNew = "ISSUE_NEW";

        public static void ToUser(DistributionDbContext db, int? userId, string type, Func<string, string> text, string payload, DateTime now)
        {
            if (!userId.HasValue || !db.TelegramAccounts.Any(a => a.UserID == userId && a.IsActive)) return;
            db.Notifications.Add(new DstNotification
            {
                Type = type, UserID = userId, Text = Cut(text(DstLocalization.LanguageOf(userId.Value))), Payload = payload, CreatedAt = now
            });
        }

        public static void ToCustomer(DistributionDbContext db, int customerId, string type, Func<string, string> text, DateTime now)
        {
            if (!db.TelegramAccounts.Any(a => a.CustomerID == customerId && a.IsActive)) return;
            db.Notifications.Add(new DstNotification
            {
                Type = type, CustomerID = customerId, Text = Cut(text(DstLocalization.Uzbek)), CreatedAt = now
            });
        }

        /// <summary>Mijozga qo'shimcha ma'lumot bilan (masalan nakladnoy PDF: {"action":"invoice","stopId":N} — bot hujjatni o'zi tayyorlaydi).</summary>
        public static void ToCustomer(DistributionDbContext db, int customerId, string type, Func<string, string> text, string payload, DateTime now)
        {
            if (!db.TelegramAccounts.Any(a => a.CustomerID == customerId && a.IsActive)) return;
            db.Notifications.Add(new DstNotification
            {
                Type = type, CustomerID = customerId, Text = Cut(text(DstLocalization.Uzbek)), Payload = payload, CreatedAt = now
            });
        }

        /// <summary>Rahbarlar: Telegram'da MANAGER roli bilan bog'langanlar.</summary>
        public static void ToManagers(DistributionDbContext db, string type, Func<string, string> text, string payload, DateTime now, int? exceptUserId = null)
        {
            var managers = db.TelegramAccounts.Where(a => a.IsActive && a.Role == BotRoles.Manager && a.UserID != null)
                .Select(a => a.UserID.Value).Distinct().ToList();
            foreach (var m in managers.Where(m => m != exceptUserId))
                ToUser(db, m, type, text, payload, now);
        }

        /// <summary>
        /// Omborchilar: Telegram'da STOREKEEPER roli bilan bog'langanlar, ombor scope'i shu omborni qamrasa
        /// (scope yozuvi yo'q yoki Admin — barcha omborlar).
        /// </summary>
        public static void ToStorekeepers(DistributionDbContext db, int warehouseId, string type, Func<string, string> text, string payload, DateTime now, int? exceptUserId = null)
        {
            var keepers = (from a in db.TelegramAccounts
                           join u in db.Users on a.UserID equals u.ID
                           where a.IsActive && a.Role == BotRoles.Storekeeper && u.IsActive && u.IsDeleted != true
                           select new { u.ID, u.Uname }).Distinct().ToList();
            if (keepers.Count == 0) return;
            var scopes = new ScopeService(db);
            foreach (var k in keepers.Where(k => k.ID != exceptUserId))
            {
                if (scopes.For(k.ID, ScopeTypes.Warehouse, tahsinERP.Models.RoleHelper.IsAdminOrDeveloper(k.Uname)).Allows(warehouseId))
                    ToUser(db, k.ID, type, text, payload, now);
            }
        }

        public sealed class PendingItem
        {
            public long ID { get; set; }
            public long ChatId { get; set; }
            public string Type { get; set; }
            public string Text { get; set; }
            public string Payload { get; set; }
        }

        /// <summary>Yuborilmaganlar (bog'langan chat bilan). Hisob uzilgan bo'lsa — yuborilgan deb belgilanadi.</summary>
        public static List<PendingItem> Pending(DistributionDbContext db, int max, DateTime now)
        {
            var rows = (from n in db.Notifications
                        where n.SentAt == null && n.Attempts < 10
                        orderby n.ID
                        select new
                        {
                            n.ID, n.Type, n.Text, n.Payload,
                            ChatId = db.TelegramAccounts.Where(a => a.IsActive && ((n.UserID != null && a.UserID == n.UserID) || (n.CustomerID != null && a.CustomerID == n.CustomerID)))
                                .Select(a => (long?)a.ChatId).FirstOrDefault()
                        }).Take(max).ToList();
            var orphan = rows.Where(r => r.ChatId == null).Select(r => r.ID).ToList();
            if (orphan.Count > 0)
            {
                foreach (var n in db.Notifications.Where(n => orphan.Contains(n.ID)).ToList()) n.SentAt = now;
                db.SaveChanges();
            }
            return rows.Where(r => r.ChatId != null)
                .Select(r => new PendingItem { ID = r.ID, ChatId = r.ChatId.Value, Type = r.Type, Text = r.Text, Payload = r.Payload }).ToList();
        }

        public static void Ack(DistributionDbContext db, IEnumerable<long> sentIds, IEnumerable<long> failedIds, DateTime now)
        {
            var sent = (sentIds ?? Enumerable.Empty<long>()).ToList();
            var failed = (failedIds ?? Enumerable.Empty<long>()).ToList();
            foreach (var n in db.Notifications.Where(n => sent.Contains(n.ID)).ToList()) n.SentAt = now;
            foreach (var n in db.Notifications.Where(n => failed.Contains(n.ID)).ToList()) n.Attempts++;
        }

        private static string Cut(string s) => s == null ? "" : s.Length <= 2000 ? s : s.Substring(0, 2000);
    }
}
