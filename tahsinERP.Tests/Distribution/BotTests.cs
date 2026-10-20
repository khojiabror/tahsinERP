using System;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Configuration;
using tahsinERP.Integration;
using tahsinERP.Services.Common;

namespace tahsinERP.Tests.Distribution
{
    [TestClass]
    public class BotPureTests
    {
        private const string Master = "0123456789abcdef0123456789abcdef-test";

        [TestMethod]
        public void Signature_RoundTrip_AndTamperDetection()
        {
            var key = DstApiSignature.TenantKey(Master, "firma1");
            var now = DateTimeOffset.FromUnixTimeSeconds(1792400000);
            var body = Encoding.UTF8.GetBytes("{\"customerId\":5}");
            var sig = DstApiSignature.Sign(key, now.ToUnixTimeSeconds(), "post", "/DstApi/Order?x=1", "12", body);

            Assert.IsTrue(DstApiSignature.Verify(key, "1792400000", sig, "POST", "/DstApi/Order?x=1", "12", body, now));
            Assert.IsTrue(DstApiSignature.Verify(key, "1792400000", sig, "POST", "/DstApi/Order?x=1", "12", body, now.AddSeconds(299)));
            Assert.IsFalse(DstApiSignature.Verify(key, "1792400000", sig, "POST", "/DstApi/Order?x=1", "12", body, now.AddSeconds(301)), "eskirgan");
            Assert.IsFalse(DstApiSignature.Verify(key, "1792400000", sig, "POST", "/DstApi/Order?x=1", "13", body, now), "boshqa foydalanuvchi");
            Assert.IsFalse(DstApiSignature.Verify(key, "1792400000", sig, "POST", "/DstApi/Order?x=2", "12", body, now), "boshqa yo'l");
            Assert.IsFalse(DstApiSignature.Verify(key, "1792400000", sig, "POST", "/DstApi/Order?x=1", "12", Encoding.UTF8.GetBytes("{}"), now), "boshqa body");
            Assert.IsFalse(DstApiSignature.Verify(DstApiSignature.TenantKey(Master, "firma2"), "1792400000", sig, "POST", "/DstApi/Order?x=1", "12", body, now),
                "boshqa tenant kaliti");
            Assert.IsFalse(DstApiSignature.Verify(key, "abc", sig, "POST", "/DstApi/Order?x=1", "12", body, now));
            Assert.IsFalse(DstApiSignature.Verify(key, "1792400000", null, "POST", "/DstApi/Order?x=1", "12", body, now));
        }

        [TestMethod]
        public void TenantKey_NormalizesSlug_AndRejectsShortMaster()
        {
            CollectionAssert.AreEqual(DstApiSignature.TenantKey(Master, "default"), DstApiSignature.TenantKey(Master, null));
            CollectionAssert.AreEqual(DstApiSignature.TenantKey(Master, "firma1"), DstApiSignature.TenantKey(Master, " FIRMA1 "));
            Assert.ThrowsException<ArgumentException>(() => DstApiSignature.TenantKey("short", "x"));
        }

        [TestMethod]
        public void FileSignature_ExpiresAndBoundToKey()
        {
            var key = DstApiSignature.TenantKey(Master, "firma1");
            var now = DateTimeOffset.FromUnixTimeSeconds(1792400000);
            long exp = now.ToUnixTimeSeconds() + 7200;
            var sig = DstApiSignature.SignFile(key, "firma1/2026/10/a.jpg", exp);
            Assert.IsTrue(DstApiSignature.VerifyFile(key, "firma1/2026/10/a.jpg", exp.ToString(), sig, now));
            Assert.IsFalse(DstApiSignature.VerifyFile(key, "firma1/2026/10/a.jpg", exp.ToString(), sig, now.AddSeconds(7201)), "muddati o'tgan");
            Assert.IsFalse(DstApiSignature.VerifyFile(key, "firma1/2026/10/b.jpg", exp.ToString(), sig, now), "boshqa fayl");
            Assert.IsFalse(DstApiSignature.VerifyFile(DstApiSignature.TenantKey(Master, "firma2"), "firma1/2026/10/a.jpg", exp.ToString(), sig, now));
            Assert.IsTrue(sig.IndexOfAny(new[] { '+', '/', '=' }) < 0, "URL uchun xavfsiz: " + sig);
        }

        [TestMethod]
        public void MapUrl_SinglePointAndSampledTrack()
        {
            Assert.IsNull(tahsinERP.Areas.Distribution.Controllers.DstFieldController.MapUrl(new (decimal, decimal)[0]));
            Assert.AreEqual("https://yandex.uz/maps/?pt=69.2797,41.3111&z=17&l=map",
                tahsinERP.Areas.Distribution.Controllers.DstFieldController.MapUrl(new[] { (41.3111m, 69.2797m) }));
            var track = Enumerable.Range(0, 250).Select(i => (41m + i / 1000m, 69m)).ToList();
            var url = tahsinERP.Areas.Distribution.Controllers.DstFieldController.MapUrl(track);
            var points = url.Split(new[] { "pt=" }, StringSplitOptions.None)[1].Split('&')[0].Split('~');
            Assert.AreEqual(100, points.Length);
            Assert.AreEqual("69,41", points[0]);
            Assert.AreEqual("69,41.249", points[99], "oxirgi nuqta saqlanadi");
        }

        [TestMethod]
        public void Geo_Distance()
        {
            Assert.AreEqual(0, Geo.DistanceM(41.3111, 69.2797, 41.3111, 69.2797), 0.001);
            Assert.AreEqual(111195, Geo.DistanceM(41, 69, 42, 69), 100, "1 daraja kenglik ≈ 111 km");
            var d = Geo.DistanceM(41.3111, 69.2797, 41.3120, 69.2797);   // ~100 m shimolga
            Assert.IsTrue(d > 95 && d < 106, d.ToString());
        }

        [TestMethod]
        public void RecommendFrom_RegularProductsOverdueFirst()
        {
            var now = new DateTime(2026, 10, 19);
            var history = new[]
            {
                (1, 10m, now.AddDays(-30)), (1, 20m, now.AddDays(-20)),               // oralig'i 10 kun, 20 kun olmagan -> overdue
                (2, 5m, now.AddDays(-6)), (2, 5m, now.AddDays(-3)), (2, 8m, now.AddDays(-1)), // muntazam, yaqinda olgan
                (3, 1m, now.AddDays(-2)),                                              // bir marta — tavsiya emas
                (4, 3m, now.AddDays(-5)), (4, 3m, now.AddDays(-5))                    // bir kunda ikki marta — 1 sana
            };
            var r = FieldService.RecommendFrom(history, now);
            CollectionAssert.AreEqual(new[] { 1, 2 }, r.Select(x => x.ProductID).ToArray());
            Assert.IsTrue(r[0].Overdue);
            Assert.AreEqual(15m, r[0].AvgQtyBase);
            Assert.AreEqual(20, r[0].DaysSinceLast);
            Assert.IsFalse(r[1].Overdue);
            Assert.AreEqual(3, r[1].Times);
        }

        [TestMethod]
        public void LinkCode_UsesUnambiguousAlphabet()
        {
            for (int i = 0; i < 50; i++)
            {
                var c = TelegramLinkService.NewCode();
                Assert.AreEqual(10, c.Length);
                Assert.IsFalse(c.IndexOfAny("01OI".ToCharArray()) >= 0, c);
            }
        }
    }

    /// <summary>S4: Telegram bog'lash, bildirishnomalar navbati, tashrif — lokal bazada, rollback bilan.</summary>
    [TestClass]
    [TestCategory("Integration")]
    public class BotIntegrationTests
    {
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                _sql = DbConfig.GetSqlConnectionString();
                using (var c = new SqlConnection(_sql))
                using (var cmd = new SqlCommand("SELECT OBJECT_ID('dst.TELEGRAM_ACCOUNTS', 'U')", c))
                {
                    c.Open();
                    if (cmd.ExecuteScalar() is DBNull) _sql = null;
                }
            }
            catch (Exception)
            {
                _sql = null;
            }
        }

        [TestInitialize]
        public void RequireDatabase()
        {
            if (_sql == null) Assert.Inconclusive("Lokal baza yoki migratsiya 020 yo'q.");
        }

        private static long FakeTelegramId() => -(DateTime.Now.Ticks % 1000000000000L) - new Random().Next(1, 1000);

        [TestMethod]
        public void Link_RedeemOnce_RelinkDeactivatesOld_Unlink()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var db = uow.Dst;
                var now = DateTime.Now;
                var user = db.Users.First(u => u.IsActive && u.IsDeleted != true);
                long tg1 = FakeTelegramId(), tg2 = tg1 - 1;

                Assert.ThrowsException<DstValidationException>(() => TelegramLinkService.CreateCode(db, user.ID, null, BotRoles.Customer, user.ID, now),
                    "xodimga mijoz roli berilmaydi");
                var code = TelegramLinkService.CreateCode(db, user.ID, null, BotRoles.Manager, user.ID, now);
                db.SaveChanges();
                var acc = TelegramLinkService.Redeem(db, code.Code.ToLowerInvariant(), tg1, tg1, now);
                db.SaveChanges();
                Assert.AreEqual(user.ID, acc.UserID);
                Assert.ThrowsException<DstValidationException>(() => TelegramLinkService.Redeem(db, code.Code, tg1, tg1, now), "bir martalik");

                // eski kod yangi kod bilan bekor bo'ladi; yangi telefon (tg2) bilan qayta ulash eskisini o'chiradi
                var stale = TelegramLinkService.CreateCode(db, user.ID, null, BotRoles.Manager, user.ID, now);
                db.SaveChanges();
                var fresh = TelegramLinkService.CreateCode(db, user.ID, null, BotRoles.Agent, user.ID, now);
                db.SaveChanges();
                Assert.ThrowsException<DstValidationException>(() => TelegramLinkService.Redeem(db, stale.Code, tg2, tg2, now.AddSeconds(1)));
                TelegramLinkService.Redeem(db, fresh.Code, tg2, tg2, now.AddSeconds(1));
                db.SaveChanges();
                var active = db.TelegramAccounts.Where(a => a.UserID == user.ID && a.IsActive).ToList();
                Assert.AreEqual(1, active.Count);
                Assert.AreEqual(tg2, active[0].TelegramUserId);
                Assert.AreEqual(BotRoles.Agent, active[0].Role);

                Assert.ThrowsException<DstValidationException>(() => TelegramLinkService.Redeem(db,
                    TelegramLinkService.CreateCode(db, user.ID, null, BotRoles.Agent, user.ID, now.AddDays(-5)).Code, tg1, tg1, now), "muddati o'tgan");

                TelegramLinkService.Unlink(db, tg2);
                db.SaveChanges();
                Assert.IsFalse(db.TelegramAccounts.Any(a => a.UserID == user.ID && a.IsActive));
            }
        }

        [TestMethod]
        public void Notifications_OnlyForLinked_PendingThenAck()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var db = uow.Dst;
                var now = DateTime.Now;
                var user = db.Users.First(u => u.IsActive && u.IsDeleted != true);
                foreach (var a in db.TelegramAccounts.Where(a => a.UserID == user.ID && a.IsActive).ToList()) a.IsActive = false;
                db.SaveChanges();

                int before = db.Notifications.Count();
                NotificationService.ToUser(db, user.ID, "TEST", l => "salom " + l, null, now);
                db.SaveChanges();
                Assert.AreEqual(before, db.Notifications.Count(), "bog'lanmagan — yozilmaydi");

                long tg = FakeTelegramId();
                var code = TelegramLinkService.CreateCode(db, user.ID, null, BotRoles.Manager, user.ID, now);
                db.SaveChanges();
                TelegramLinkService.Redeem(db, code.Code, tg, tg, now);
                db.SaveChanges();
                NotificationService.ToManagers(db, "TEST", l => "yirik " + l, "{\"a\":1}", now);
                db.SaveChanges();
                var mine = db.Notifications.Where(n => n.UserID == user.ID && n.Type == "TEST").ToList();
                Assert.AreEqual(1, mine.Count);
                var id = mine[0].ID;

                var pending = NotificationService.Pending(db, 100000, now);
                var item = pending.Single(p => p.ID == id);
                Assert.AreEqual(tg, item.ChatId);
                Assert.AreEqual("{\"a\":1}", item.Payload);

                NotificationService.Ack(db, null, new[] { id }, now);
                db.SaveChanges();
                Assert.AreEqual(1, db.Notifications.Find(id).Attempts);
                NotificationService.Ack(db, new[] { id }, null, now);
                db.SaveChanges();
                Assert.IsFalse(NotificationService.Pending(db, 100000, now).Any(p => p.ID == id));

                // hisob uzilsa — navbatdagi xabar "yetim" bo'lib yopiladi
                NotificationService.ToUser(db, user.ID, "TEST", l => "ikkinchi", null, now);
                db.SaveChanges();
                var id2 = db.Notifications.Where(n => n.UserID == user.ID && n.Type == "TEST").Max(n => n.ID);
                TelegramLinkService.Unlink(db, tg);
                db.SaveChanges();
                Assert.IsFalse(NotificationService.Pending(db, 100000, now).Any(p => p.ID == id2));
                Assert.IsNotNull(db.Notifications.Find(id2).SentAt);
            }
        }

        [TestMethod]
        public void CheckIn_RespectsRadius_StartsDay_AndNoOrderNeedsReason()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 1000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var profile = db.CustomerProfiles.Find(f.CustomerId);
                profile.Lat = 41.311100m;
                profile.Lng = 69.279700m;
                DstSettings.Set(db, DstSetting.VisitRadiusM, "200");
                DstSettings.Set(db, DstSetting.RequireSelfie, "0");
                foreach (var d in db.AgentDays.Where(d => d.UserID == f.UserId && d.WorkDate == now.Date).ToList()) db.AgentDays.Remove(d);
                db.SaveChanges();

                var ex = Assert.ThrowsException<DstValidationException>(() =>
                    FieldService.CheckIn(db, f.UserId, f.CustomerId, 41.315100m, 69.279700m, 10, true, now));   // ~445 m
                StringAssert.Contains(ex.Message, "200");
                Assert.IsFalse(db.AgentDays.Any(d => d.UserID == f.UserId && d.WorkDate == now.Date), "rad etilganda kun boshlanmaydi");

                // 250 m, lekin GPS aniqligi 80 m — tolerantlik bilan qabul
                var r = FieldService.CheckIn(db, f.UserId, f.CustomerId, 41.313350m, 69.279700m, 80, false, now);
                Assert.IsTrue(r.DistanceM > 240 && r.DistanceM < 260, r.DistanceM.ToString());
                Assert.IsTrue(db.AgentDays.Any(d => d.UserID == f.UserId && d.WorkDate == now.Date), "birinchi tashrif kunni boshlaydi");

                Assert.ThrowsException<DstValidationException>(() =>
                    FieldService.SetResult(db, r.VisitId, f.UserId, VisitResults.NoOrder, null, null, null, null, null, now));
                var reason = db.Reasons.FirstOrDefault(x => x.Type == ReasonTypes.NoOrder);
                if (reason != null)
                {
                    FieldService.SetResult(db, r.VisitId, f.UserId, VisitResults.NoOrder, reason.ID, null, null, null, "yopiq", now);
                    db.SaveChanges();
                    Assert.AreEqual(VisitResults.NoOrder, db.Visits.Find(r.VisitId).Result);
                    Assert.IsNotNull(db.Visits.Find(r.VisitId).CheckOutAt);
                }
                Assert.IsTrue(FieldService.Route(db, f.UserId, now).Any(s => s.CustomerID == f.CustomerId && s.Visited), "bugun tashrif buyurilgan — marshrutda");
                Assert.IsTrue(db.AgentTrack.Any(t => t.UserID == f.UserId && t.Source == TrackSources.Visit && t.At >= now.Date), "tashrif nuqtasi izda");
            }
        }

        [TestMethod]
        public void Selfie_RequiredSetting_BlocksDayStartAndAutoStart()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 1000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                foreach (var d in db.AgentDays.Where(d => d.UserID == f.UserId && d.WorkDate == now.Date).ToList()) db.AgentDays.Remove(d);
                DstSettings.Set(db, DstSetting.RequireSelfie, "1");
                db.SaveChanges();

                var ex = Assert.ThrowsException<DstValidationException>(() => FieldService.StartDay(db, f.UserId, 41.3m, 69.2m, null, now));
                StringAssert.Contains(ex.Message, "selfi");
                Assert.ThrowsException<DstValidationException>(() => FieldService.CheckIn(db, f.UserId, f.CustomerId, 41.3m, 69.2m, 10, true, now),
                    "selfi majburiy — \"keldim\" kunni o'zi boshlamaydi");

                var day = FieldService.StartDay(db, f.UserId, 41.3m, 69.2m, "demo/2026/10/selfie.jpg", now);
                db.SaveChanges();
                Assert.AreEqual("demo/2026/10/selfie.jpg", db.AgentDays.Find(day.ID).SelfieKey);
                Assert.IsTrue(db.AgentTrack.Any(t => t.UserID == f.UserId && t.Source == TrackSources.Day && t.At >= now.Date));

                // ixtiyoriy rejimda selfisiz boshlangan kunga selfi keyin qo'shiladi
                DstSettings.Set(db, DstSetting.RequireSelfie, "0");
                db.AgentDays.Find(day.ID).SelfieKey = null;
                db.SaveChanges();
                var same = FieldService.StartDay(db, f.UserId, null, null, "demo/2026/10/later.jpg", now.AddMinutes(5));
                db.SaveChanges();
                Assert.AreEqual(day.ID, same.ID);
                Assert.AreEqual("demo/2026/10/later.jpg", db.AgentDays.Find(day.ID).SelfieKey);
            }
        }

        [TestMethod]
        public void Track_OnlyDuringOpenDay_Throttled()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var db = uow.Dst;
                var user = db.Users.First(u => u.IsActive && u.IsDeleted != true);
                var now = DateTime.Now;
                foreach (var d in db.AgentDays.Where(d => d.UserID == user.ID && d.WorkDate == now.Date).ToList()) db.AgentDays.Remove(d);
                db.AgentTrack.RemoveRange(db.AgentTrack.Where(t => t.UserID == user.ID && t.At >= now.Date).ToList());
                DstSettings.Set(db, DstSetting.RequireSelfie, "0");
                db.SaveChanges();

                Assert.IsFalse(FieldService.Track(db, user.ID, 41.3m, 69.2m, 10, now), "kun boshlanmagan — shaxsiy vaqt yozilmaydi");
                FieldService.StartDay(db, user.ID, 41.3m, 69.2m, null, now);
                db.SaveChanges();
                Assert.IsTrue(FieldService.Track(db, user.ID, 41.301m, 69.2m, 10, now.AddSeconds(1)));
                db.SaveChanges();
                Assert.IsFalse(FieldService.Track(db, user.ID, 41.302m, 69.2m, 10, now.AddSeconds(20)), "30 s ichida");
                Assert.IsTrue(FieldService.Track(db, user.ID, 41.303m, 69.2m, 10, now.AddSeconds(32)));
                db.SaveChanges();
                Assert.ThrowsException<DstValidationException>(() => FieldService.Track(db, user.ID, 91m, 69.2m, null, now.AddSeconds(70)));

                FieldService.EndDay(db, user.ID, 41.31m, 69.21m, now.AddMinutes(2));
                Assert.IsFalse(FieldService.Track(db, user.ID, 41.304m, 69.2m, 10, now.AddMinutes(3)), "kun yakunlangan");
                var sources = db.AgentTrack.Where(t => t.UserID == user.ID && t.At >= now.Date).OrderBy(t => t.At).Select(t => t.Source).ToList();
                CollectionAssert.AreEqual(new[] { TrackSources.Day, TrackSources.Live, TrackSources.Live, TrackSources.Day }, sources);
            }
        }

        [TestMethod]
        public void Confirm_NotifiesStorekeepersOfThatWarehouseOnly()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var keepers = db.Users.Where(u => u.IsActive && u.IsDeleted != true && u.ID != f.UserId).OrderBy(u => u.ID).ToList()
                    .Where(u => !tahsinERP.Models.RoleHelper.IsAdminOrDeveloper(u.Uname)).Take(2).ToList();
                if (keepers.Count < 2) Assert.Inconclusive("Admin bo'lmagan ikki foydalanuvchi kerak.");
                foreach (var a in db.TelegramAccounts.Where(a => a.IsActive && a.Role == BotRoles.Storekeeper).ToList()) a.IsActive = false;
                foreach (var k in keepers)
                {
                    foreach (var a in db.TelegramAccounts.Where(a => a.IsActive && a.UserID == k.ID).ToList()) a.IsActive = false;
                    db.UserScopes.RemoveRange(db.UserScopes.Where(s => s.UserID == k.ID && s.ScopeType == ScopeTypes.Warehouse).ToList());
                }
                db.SaveChanges();
                db.UserScopes.Add(new UserScope { UserID = keepers[0].ID, ScopeType = ScopeTypes.Warehouse, ScopeID = f.WarehouseId });
                db.UserScopes.Add(new UserScope { UserID = keepers[1].ID, ScopeType = ScopeTypes.Warehouse, ScopeID = f.WarehouseId + 100000 });
                db.SaveChanges();
                long tg = FakeTelegramId();
                foreach (var k in keepers)
                {
                    var code = TelegramLinkService.CreateCode(db, k.ID, null, BotRoles.Storekeeper, f.UserId, now);
                    db.SaveChanges();
                    TelegramLinkService.Redeem(db, code.Code, tg, tg, now);
                    db.SaveChanges();
                    tg--;
                }

                var orderId = OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, DeliveryDate = now.Date.AddDays(1),
                    Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 3 } }
                }, OrderFlowIntegrationTests.Actor(f), now);
                OrderService.Confirm(uow, orderId, OrderFlowIntegrationTests.Actor(f), null, now);
                var docNo = db.Orders.Find(orderId).DocNo;

                var store = db.Notifications.Where(n => n.Type == NotificationService.StoreOrderConfirmed && n.Text.Contains(docNo)).ToList();
                Assert.AreEqual(1, store.Count, "faqat o'sha ombor omborchisiga");
                Assert.AreEqual(keepers[0].ID, store[0].UserID);
                StringAssert.Contains(store[0].Text, now.Date.AddDays(1).ToString("dd.MM.yyyy"));
                StringAssert.Contains(store[0].Text, "165");   // 3 × 55 000
            }
        }

        [TestMethod]
        public void FileLinks_SignedUrlFromSettings()
        {
            const string Master = "0123456789abcdef0123456789abcdef-test";
            var old = Environment.GetEnvironmentVariable(tahsinERP.Areas.Distribution.Controllers.Api.DstApiAuthAttribute.EnvKey);
            try
            {
                Environment.SetEnvironmentVariable(tahsinERP.Areas.Distribution.Controllers.Api.DstApiAuthAttribute.EnvKey, Master);
                using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
                {
                    var db = uow.Dst;
                    var now = DateTimeOffset.FromUnixTimeSeconds(1792400123);
                    DstSettings.Set(db, DstSetting.BotPublicUrl, null);
                    db.SaveChanges();
                    Assert.IsFalse(FileLinks.For(db, now).Enabled, "manzil yo'q — havolasiz");
                    Assert.IsNull(FileLinks.For(db, now).Url("demo/a.jpg"));

                    DstSettings.Set(db, DstSetting.BotPublicUrl, "https://bot.example.uz/");
                    db.SaveChanges();
                    var links = FileLinks.For(db, now);
                    Assert.IsNull(links.Url(null));
                    var url = new Uri(links.Url("demo/2026/10/a b.jpg"));
                    Assert.AreEqual("/files/demo/2026/10/a%20b.jpg", url.AbsolutePath);
                    var q = System.Web.HttpUtility.ParseQueryString(url.Query);
                    Assert.AreEqual((1792400123L / 3600 * 3600 + 3600 * 2).ToString(), q["e"], "soat boshi + 2 soat (havola kamida 1 soat amal qiladi)");
                    Assert.IsTrue(DstApiSignature.VerifyFile(DstApiSignature.TenantKey(Master, DstApiSignature.DefaultTenant), "demo/2026/10/a b.jpg", q["e"], q["s"], now));
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(tahsinERP.Areas.Distribution.Controllers.Api.DstApiAuthAttribute.EnvKey, old);
            }
        }
    }
}
