using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.I18n;
using tahsinERP.Licensing;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Litsenziya biznes-logikasi: company/user tekshiruvi va seat boshqaruvi.
    ///
    /// Ishonch manbai — kalit ichidagi IMZOLANGAN payload (kod ichidagi RSA public key
    /// bilan tekshirilgan), DB ustunlari emas. Shu sabab client DB'ni qo'lda o'zgartirsa
    /// ham (masalan ExpiryDate ustunini) foyda bermaydi — imzo mos kelmaydi.
    ///
    /// Qo'shimcha himoya (dbo.LICENSE_STATE jadvali, EDMX'da emas — xom SQL):
    ///   InstallationId — birinchi ishga tushishda yaratiladigan GUID; v2 kalit shunga bog'lanadi.
    ///   LastSeenUtc    — HMAC bilan muhrlangan "oxirgi ko'rilgan vaqt"; soat orqaga
    ///                    surilsa bloklanadi, muddat esa max(hozir, oxirgi ko'rilgan) bo'yicha hisoblanadi.
    /// </summary>
    public static class LicenseService
    {
        public class Result
        {
            public bool Ok { get; set; }
            public string Message { get; set; }
            public static Result Success() { return new Result { Ok = true, Message = "" }; }
            public static Result Fail(string m) { return new Result { Ok = false, Message = m }; }
        }

        /// <summary>
        /// Tarjima qilinadigan xabar (resx kaliti + argumentlar). Holat 5 daqiqa keshlanadi va turli tildagi
        /// foydalanuvchilarga ko'rsatiladi — shuning uchun matn har o'qilganda joriy so'rov tilida yasaladi.
        /// </summary>
        public sealed class LocalizedMessage
        {
            public LocalizedMessage(string key, params object[] args)
            {
                Key = key;
                Args = args ?? new object[0];
            }

            public string Key { get; private set; }
            public object[] Args { get; private set; }

            public override string ToString()
            {
                return Args.Length == 0 ? L.T(Key) : L.T(Key, Args);
            }
        }

        public class SeatUsage
        {
            public int Max { get; set; }        // 0 = cheksiz
            public int Used { get; set; }
            public bool Unlimited { get { return Max == 0; } }
            public int Free { get { return Unlimited ? int.MaxValue : Math.Max(0, Max - Used); } }
            public bool HasFreeSeat { get { return Unlimited || Used < Max; } }
        }

        /// <summary>Kompaniya litsenziyasining umumiy holati (filter, login, banner uchun).</summary>
        public class LicenseStatus
        {
            /// <summary>To'liq ishlash mumkin (Valid yoki ExpiringSoon).</summary>
            public bool FullAccess { get; set; }
            /// <summary>Grace davri: faqat o'qish (GET) mumkin.</summary>
            public bool ReadOnly { get; set; }
            public bool Blocked { get { return !FullAccess && !ReadOnly; } }

            public LicenseHelper.LicenseState? State { get; set; }
            public LocalizedMessage MessageText { get; set; }
            /// <summary>Holat xabari — joriy so'rov tilida.</summary>
            public string Message { get { return MessageText?.ToString(); } }
            public LocalizedMessage ClockProblemText { get; set; }
            /// <summary>Soat orqaga surilgan yoki muhr buzilgan bo'lsa — sabab (License sahifasida ko'rsatiladi).</summary>
            public string ClockProblem { get { return ClockProblemText?.ToString(); } }

            public LicenseHelper.LicenseInfo Info { get; set; }
            public string InstallationId { get; set; }
            /// <summary>Muddat hisobida ishlatilgan "hozir" = max(server vaqti, oxirgi ko'rilgan vaqt).</summary>
            public DateTime EffectiveNow { get; set; }

            public int DaysRemaining { get { return Info != null ? Info.DaysRemainingAt(EffectiveNow) : 0; } }
            /// <summary>Panel bilan uzoq aloqa yo'q (onlayn tekshiruv siyosati, 08_STAGE4.md 4) — banner matni yoki null.</summary>
            public string OnlineWarning { get { return OnlineWarningText?.ToString(); } }
            public LocalizedMessage OnlineWarningText { get; set; }

            /// <summary>Admin'ga banner ko'rsatish kerakmi (tugashiga oz qoldi yoki grace).</summary>
            public bool ShowWarning
            {
                get { return State == LicenseHelper.LicenseState.ExpiringSoon || State == LicenseHelper.LicenseState.Grace; }
            }
        }

        private const string CACHE_KEY = "tahsinERP.License.Status";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ErrorCacheDuration = TimeSpan.FromSeconds(30);

        private const string STATE_INSTALLATION_ID = "InstallationId";
        private const string STATE_LAST_SEEN = "LastSeenUtc";

        // LICENSE_STATE jadvali yaratilganmi — har tenant bazasi uchun alohida ("" = bitta-baza rejimi)
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _stateTableEnsured =
            new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        private static readonly object _stateLock = new object();

        public static string PublicKey
        {
            get { return LicenseHelper.PublicKeyXml; }
        }

        /// <summary>
        /// Shu o'rnatmaning CompanyID'si: ko'p-tenant rejimida tenant reestridagi companyId (berilgan bo'lsa),
        /// aks holda Web.config'dagi companyID.
        /// </summary>
        public static int InstallationCompanyId
        {
            get
            {
                var tenant = TenantContext.Current;
                if (tenant != null && tenant.CompanyId.HasValue)
                    return tenant.CompanyId.Value;
                int id;
                return int.TryParse(ConfigurationManager.AppSettings["companyID"], out id) ? id : 0;
            }
        }

        #region Kesh

        /// <summary>
        /// Holatni 5 daqiqaga keshlab qaytaradi (har so'rovda DB'ga bormaslik uchun).
        /// Xatolik bo'lsa logga yoziladi, foydalanuvchiga umumiy xabar qaytadi.
        /// </summary>
        public static LicenseStatus GetCachedStatus()
        {
            string cacheKey = TenantContext.CacheKey(CACHE_KEY);
            var cached = MemoryCache.Default.Get(cacheKey) as LicenseStatus;
            if (cached != null)
                return cached;

            LicenseStatus status;
            TimeSpan ttl = CacheDuration;
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    status = GetStatus(db);
                    try { status.OnlineWarningText = OnlineWarningMessage(db); }
                    catch (Exception ex) { Trace.TraceWarning("[License] onlayn tekshiruv holati o'qilmadi: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("[License] Litsenziya holatini tekshirishda xatolik: " + ex);
                status = Blocked(new LocalizedMessage("Core_LicenseCheckError"));
                ttl = ErrorCacheDuration;
            }

            MemoryCache.Default.Set(cacheKey, status, DateTimeOffset.Now.Add(ttl));
            return status;
        }

        /// <summary>
        /// Onlayn tekshiruv ogohlantirishi: panelga ulangan o'rnatma uzoq vaqt muvaffaqiyatli sinxronlanmagan bo'lsa
        /// (Optional — 30 kun, Required — 3 kun; LicenseSync.OnlineCheckPolicy). Panel o'chiq/ulanmagan — null.
        /// </summary>
        public static string OnlineWarningFor(DBTHSNEntities db)
        {
            return OnlineWarningMessage(db)?.ToString();
        }

        /// <summary>
        /// OnlineCheckPolicy.Warning bilan bir xil qoida (u panel testlari bilan umumiy fayl — o'zbekcha matn qaytaradi);
        /// bu yerda xabar joriy tilda ko'rsatish uchun kalit sifatida qaytadi.
        /// </summary>
        private static LocalizedMessage OnlineWarningMessage(DBTHSNEntities db)
        {
            if (!LicenseSync.LicenseSyncService.IsEnabled || string.IsNullOrEmpty(ReadSyncState(db, STATE_PANEL_SECRET)))
                return null;
            DateTime lastOk;
            DateTime? last = DateTime.TryParse(ReadSyncState(db, STATE_LAST_SYNC_OK), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out lastOk) ? lastOk.ToUniversalTime() : (DateTime?)null;
            int grace;
            int? graceDays = int.TryParse(ReadSyncState(db, STATE_OFFLINE_GRACE_DAYS), NumberStyles.None, CultureInfo.InvariantCulture, out grace)
                ? grace : (int?)null;
            string onlineCheck = ReadSyncState(db, STATE_ONLINE_CHECK);
            DateTime nowUtc = DateTime.UtcNow;
            if (LicenseSync.OnlineCheckPolicy.Warning(onlineCheck, graceDays, last, nowUtc) == null)
                return null;
            int effectiveGrace = graceDays.HasValue && graceDays.Value > 0 ? Math.Min(graceDays.Value, 366) : LicenseSync.OnlineCheckPolicy.DefaultGraceDays;
            int days = (int)Math.Floor((nowUtc - last.Value).TotalDays);
            return LicenseSync.OnlineCheckPolicy.IsRequired(onlineCheck)
                ? new LocalizedMessage("Core_LicOnlineWarnRequired", days, effectiveGrace)
                : new LocalizedMessage("Core_LicOnlineWarnOptional", days);
        }

        /// <summary>Kalit faollashtirilganda (yoki holat o'zgarganda) keshni tozalaydi.</summary>
        public static void InvalidateCache()
        {
            MemoryCache.Default.Remove(TenantContext.CacheKey(CACHE_KEY));
        }

        #endregion

        #region O'rnatma holati (LICENSE_STATE)

        private static void EnsureStateTable(DBTHSNEntities db)
        {
            string tenantKey = TenantContext.CacheKeyPrefix;
            if (_stateTableEnsured.ContainsKey(tenantKey))
                return;

            db.Database.ExecuteSqlCommand(
                @"IF OBJECT_ID(N'dbo.LICENSE_STATE', N'U') IS NULL
                  CREATE TABLE dbo.LICENSE_STATE (
                      Name      NVARCHAR(50)  NOT NULL CONSTRAINT PK_LICENSE_STATE PRIMARY KEY,
                      Value     NVARCHAR(400) NOT NULL,
                      UpdatedAt DATETIME      NOT NULL CONSTRAINT DF_LS_UpdatedAt DEFAULT(GETDATE()));");
            _stateTableEnsured[tenantKey] = true;
        }

        private static string ReadState(DBTHSNEntities db, string name)
        {
            return db.Database.SqlQuery<string>(
                "SELECT Value FROM dbo.LICENSE_STATE WHERE Name = @p0", name).FirstOrDefault();
        }

        private static void WriteState(DBTHSNEntities db, string name, string value)
        {
            db.Database.ExecuteSqlCommand(
                @"UPDATE dbo.LICENSE_STATE SET Value = @p1, UpdatedAt = GETDATE() WHERE Name = @p0;
                  IF @@ROWCOUNT = 0 INSERT INTO dbo.LICENSE_STATE (Name, Value) VALUES (@p0, @p1);",
                name, value);
        }

        // Panel sinxronlash holati (LicenseSyncService). InstallationId va LastSeenUtc bu ro'yxatda YO'Q —
        // ularni faqat shu klass yozadi (HMAC muhri qoidasi).
        public const string STATE_REG_TOKEN = "RegToken";
        public const string STATE_PANEL_SECRET = "PanelSecret";
        public const string STATE_LAST_SYNC = "LastSyncUtc";
        public const string STATE_LAST_SYNC_RESULT = "LastSyncResult";
        /// <summary>Oxirgi MUVAFFAQIYATLI sinxronlash (UTC, "o") — onlayn tekshiruv ogohlantirishi shundan hisoblanadi.</summary>
        public const string STATE_LAST_SYNC_OK = "LastSyncOkUtc";
        /// <summary>Panel bergan siyosat: "required" / "optional" (08_STAGE4.md 4).</summary>
        public const string STATE_ONLINE_CHECK = "OnlineCheck";
        public const string STATE_OFFLINE_GRACE_DAYS = "OfflineGraceDays";
        private static readonly string[] SyncStateNames =
        {
            STATE_REG_TOKEN, STATE_PANEL_SECRET, STATE_LAST_SYNC, STATE_LAST_SYNC_RESULT,
            STATE_LAST_SYNC_OK, STATE_ONLINE_CHECK, STATE_OFFLINE_GRACE_DAYS
        };

        private static void EnsureSyncStateName(string name)
        {
            if (Array.IndexOf(SyncStateNames, name) < 0)
                throw new ArgumentException("LICENSE_STATE." + name + " ni tashqaridan o'qish/yozish mumkin emas.", nameof(name));
        }

        /// <summary>Panel sinxronlash holatini o'qiydi (RegToken, PanelSecret, LastSyncUtc, LastSyncResult).</summary>
        public static string ReadSyncState(DBTHSNEntities db, string name)
        {
            EnsureSyncStateName(name);
            EnsureStateTable(db);
            return ReadState(db, name);
        }

        /// <summary>Panel sinxronlash holatini yozadi; null/bo'sh — o'chiradi. Qiymat 400 belgigacha qisqartiriladi.</summary>
        public static void WriteSyncState(DBTHSNEntities db, string name, string value)
        {
            EnsureSyncStateName(name);
            EnsureStateTable(db);
            if (string.IsNullOrEmpty(value))
            {
                db.Database.ExecuteSqlCommand("DELETE FROM dbo.LICENSE_STATE WHERE Name = @p0", name);
                return;
            }
            WriteState(db, name, value.Length > 400 ? value.Substring(0, 400) : value);
        }

        /// <summary>
        /// Shu o'rnatmaning noyob ID'si. Birinchi chaqiruvda yaratiladi.
        /// Vendor kalit yasash uchun shu qiymatni License sahifasidan oladi.
        /// </summary>
        public static string GetOrCreateInstallationId(DBTHSNEntities db)
        {
            EnsureStateTable(db);

            string existing = LicenseHelper.NormalizeInstallationId(ReadState(db, STATE_INSTALLATION_ID));
            if (existing != null)
                return existing;

            lock (_stateLock)
            {
                existing = LicenseHelper.NormalizeInstallationId(ReadState(db, STATE_INSTALLATION_ID));
                if (existing != null)
                    return existing;

                string newId = Guid.NewGuid().ToString("D").ToUpperInvariant();
                db.Database.ExecuteSqlCommand(
                    @"IF NOT EXISTS (SELECT 1 FROM dbo.LICENSE_STATE WHERE Name = @p0)
                          INSERT INTO dbo.LICENSE_STATE (Name, Value) VALUES (@p0, @p1)
                      ELSE
                          UPDATE dbo.LICENSE_STATE SET Value = @p1, UpdatedAt = GETDATE() WHERE Name = @p0;",
                    STATE_INSTALLATION_ID, newId);
                // Yangi o'rnatma — soat muhri ham shu yerda boshlanadi
                WriteState(db, STATE_LAST_SEEN, LicenseHelper.CreateClockStamp(DateTime.UtcNow, newId));
                return newId;
            }
        }

        /// <summary>
        /// Soatni tekshiradi va muhrni yangilaydi. Muammo bo'lsa sababni qaytaradi (aks holda null).
        /// effectiveNowUtc — muddat hisobida ishlatiladigan vaqt (hech qachon orqaga ketmaydi).
        /// </summary>
        private static LocalizedMessage CheckAndAdvanceClock(DBTHSNEntities db, string installationId,
            COMPANY_LICENSES activeLicense, out DateTime effectiveNowUtc)
        {
            DateTime nowUtc = DateTime.UtcNow;
            effectiveNowUtc = nowUtc;

            string stamp = ReadState(db, STATE_LAST_SEEN);
            DateTime lastSeenUtc;
            if (stamp == null)
            {
                // InstallationId bor, lekin muhr o'chirilgan — qo'lda aralashuv
                return new LocalizedMessage("Core_LicStampMissing");
            }
            if (!LicenseHelper.TryReadClockStamp(stamp, installationId, out lastSeenUtc))
            {
                return new LocalizedMessage("Core_LicStampTampered");
            }

            // Pastki chegara: faol kalit faollashtirilgan vaqt ham "ko'rilgan" vaqt hisoblanadi
            if (activeLicense != null)
            {
                DateTime activatedUtc = DateTime.SpecifyKind(activeLicense.CreatedAt, DateTimeKind.Local).ToUniversalTime();
                if (activatedUtc > lastSeenUtc)
                    lastSeenUtc = activatedUtc;
            }

            if (LicenseHelper.IsClockRolledBack(nowUtc, lastSeenUtc))
            {
                effectiveNowUtc = lastSeenUtc;
                return new LocalizedMessage("Core_LicClockRolledBack",
                    nowUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), lastSeenUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
            }

            if (lastSeenUtc > nowUtc)
                effectiveNowUtc = lastSeenUtc;            // ruxsat etilgan chegaradagi orqaga ketish — vaqt baribir orqaga yurmaydi
            else if (nowUtc - lastSeenUtc > TimeSpan.FromMinutes(1))
                WriteState(db, STATE_LAST_SEEN, LicenseHelper.CreateClockStamp(nowUtc, installationId));

            return null;
        }

        #endregion

        #region Company license

        /// <summary>
        /// Ushbu o'rnatma (companyID) uchun faol company litsenziyasini oladi va
        /// uning imzosini tekshiradi. Yaroqli bo'lsa info to'ldiriladi.
        /// </summary>
        public static COMPANY_LICENSES GetActiveCompanyLicense(DBTHSNEntities db, out LicenseHelper.LicenseInfo info)
        {
            info = null;
            int companyId = InstallationCompanyId;

            var lic = db.COMPANY_LICENSES
                .Where(l => l.CompanyID == companyId && l.IsActive)
                .OrderByDescending(l => l.ID)
                .FirstOrDefault();

            if (lic == null)
                return null;

            LicenseHelper.LicenseInfo parsed;
            LicenseHelper.TryParseKey(lic.LicenseKey, out parsed);
            info = parsed; // imzo yaroqsiz bo'lsa SignatureValid=false yoki null
            return lic;
        }

        private static LicenseStatus Blocked(LocalizedMessage message)
        {
            return new LicenseStatus { FullAccess = false, ReadOnly = false, MessageText = message, EffectiveNow = DateTime.Now };
        }

        /// <summary>
        /// Kompaniya litsenziyasining to'liq holatini hisoblaydi (keshsiz).
        /// Soat muhrini ham yangilaydi.
        /// </summary>
        public static LicenseStatus GetStatus(DBTHSNEntities db)
        {
            string installationId = GetOrCreateInstallationId(db);

            LicenseHelper.LicenseInfo info;
            var lic = GetActiveCompanyLicense(db, out info);

            DateTime effectiveNowUtc;
            LocalizedMessage clockProblem = CheckAndAdvanceClock(db, installationId, lic, out effectiveNowUtc);

            var status = Blocked(null);
            status.InstallationId = installationId;
            status.Info = info;
            status.EffectiveNow = effectiveNowUtc.ToLocalTime();
            status.ClockProblemText = clockProblem;

            if (lic == null)
            {
                status.MessageText = new LocalizedMessage("Core_LicNotFoundContactVendor");
                return status;
            }

            if (info == null || !info.SignatureValid)
            {
                status.MessageText = new LocalizedMessage("Core_LicCompanySignatureInvalid");
                return status;
            }

            if (info.CompanyID != InstallationCompanyId)
            {
                status.MessageText = new LocalizedMessage("Core_LicOtherCompany");
                return status;
            }

            if (!info.IsBoundTo(installationId))
            {
                status.MessageText = new LocalizedMessage("Core_LicOtherInstallationSendId", installationId);
                return status;
            }

            if (clockProblem != null)
            {
                status.MessageText = clockProblem;
                return status;
            }

            var state = LicenseHelper.GetState(info, status.EffectiveNow);
            status.State = state;

            switch (state)
            {
                case LicenseHelper.LicenseState.NotStarted:
                    status.MessageText = new LocalizedMessage("Core_LicNotYetValid", info.StartDate.ToString("dd.MM.yyyy"));
                    break;
                case LicenseHelper.LicenseState.Expired:
                    status.MessageText = new LocalizedMessage("Core_LicCompanyExpired", info.ExpiryDate.ToString("dd.MM.yyyy"));
                    break;
                case LicenseHelper.LicenseState.Grace:
                    status.ReadOnly = true;
                    status.MessageText = new LocalizedMessage("Core_LicGrace",
                        info.ExpiryDate.ToString("dd.MM.yyyy"), info.GraceUntil.ToString("dd.MM.yyyy"));
                    break;
                case LicenseHelper.LicenseState.ExpiringSoon:
                    status.FullAccess = true;
                    status.MessageText = new LocalizedMessage("Core_LicExpiringSoon",
                        info.DaysRemainingAt(status.EffectiveNow), info.ExpiryDate.ToString("dd.MM.yyyy"));
                    break;
                default:
                    status.FullAccess = true;
                    status.MessageText = new LocalizedMessage("Core_LicActive");
                    break;
            }
            return status;
        }

        /// <summary>
        /// Company litsenziya holatini tekshiradi (login uchun).
        /// Grace davrida kirishga ruxsat beriladi — yozish amallarini filter bloklaydi.
        /// </summary>
        public static Result CheckCompanyLicense(DBTHSNEntities db)
        {
            var status = GetStatus(db);
            return status.Blocked ? Result.Fail(status.Message) : Result.Success();
        }

        /// <summary>
        /// Admin kiritgan kalitni tekshirib faollashtiradi.
        /// DB ustunlari kalit ichidagi imzolangan qiymatlardan to'ldiriladi.
        /// </summary>
        public static Result ActivateCompanyLicense(DBTHSNEntities db, string licenseKey, string activatedBy)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return Result.Fail(L.T("Core_LicEnterKey"));

            licenseKey = licenseKey.Trim();

            LicenseHelper.LicenseInfo info;
            bool ok = LicenseHelper.TryParseKey(licenseKey, out info);
            if (!ok || info == null)
                return Result.Fail(L.T("Core_LicKeyInvalid"));

            if (info.CompanyID != InstallationCompanyId)
                return Result.Fail(L.T("Core_LicKeyOtherCompany", info.CompanyID));

            string installationId = GetOrCreateInstallationId(db);
            if (info.Version < 2)
            {
                if (DateTime.Now.Date >= LicenseHelper.V1ActivationCutoff)
                    return Result.Fail(L.T("Core_LicV1NotAccepted", installationId));
            }
            else if (!info.IsBoundTo(installationId))
            {
                return Result.Fail(L.T("Core_LicKeyOtherInstallation", installationId));
            }

            // Vaqt muhri buzilgan/yo'q yoki soat orqaga surilgan bo'lsa — faqat avvalgi barcha
            // kalitlardan KEYIN tugaydigan (ya'ni haqiqatan yangi) vendor kaliti holatni tiklaydi.
            // Aks holda soatni orqaga surib, eski kalitni qayta faollashtirish mumkin bo'lardi.
            DateTime? maxKnownExpiry = db.COMPANY_LICENSES
                .Where(l => l.CompanyID == info.CompanyID)
                .Max(l => l.ExpiryDate);
            bool newerThanHistory = !maxKnownExpiry.HasValue || info.ExpiryDate.Date > maxKnownExpiry.Value.Date;

            string stamp = ReadState(db, STATE_LAST_SEEN);
            DateTime lastSeenUtc;
            bool stampOk = LicenseHelper.TryReadClockStamp(stamp, installationId, out lastSeenUtc);
            bool rolledBack = stampOk && LicenseHelper.IsClockRolledBack(DateTime.UtcNow, lastSeenUtc);

            if (!stampOk && !newerThanHistory)
                return Result.Fail(L.T("Core_LicStampBroken"));
            if (rolledBack && !newerThanHistory)
                return Result.Fail(L.T("Core_LicClockRolledBackActivate", lastSeenUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")));

            bool resetStamp = !stampOk || rolledBack;
            DateTime effectiveNow = !resetStamp && lastSeenUtc > DateTime.UtcNow ? lastSeenUtc.ToLocalTime() : DateTime.Now;
            if (info.IsExpiredAt(effectiveNow))
                return Result.Fail(L.T("Core_LicKeyAlreadyExpired", info.ExpiryDate.ToString("dd.MM.yyyy")));

            // Eski faol litsenziyalarni o'chiramiz
            var actives = db.COMPANY_LICENSES.Where(l => l.CompanyID == info.CompanyID && l.IsActive).ToList();
            foreach (var a in actives)
                a.IsActive = false;

            var lic = new COMPANY_LICENSES
            {
                CompanyID = info.CompanyID,
                LicenseKey = licenseKey,
                MaxUsers = info.MaxUsers,
                StartDate = info.StartDate,
                ExpiryDate = info.ExpiryDate,
                Edition = info.Edition,
                IsActive = true,
                CreatedAt = DateTime.Now,
                CreatedBy = activatedBy
            };
            db.COMPANY_LICENSES.Add(lic);
            db.SaveChanges();

            // Xodimlar seat'lari yangi kalitga ko'chiriladi — aks holda har yangilanishda (panel buni
            // avtomatik qiladi) barcha xodimlar keyingi login'da "seat berilmagan" bo'lib qolardi.
            MigrateSeats(db, actives.Select(a => a.ID).ToList(), lic);

            if (resetStamp || lastSeenUtc < DateTime.UtcNow)
                WriteState(db, STATE_LAST_SEEN, LicenseHelper.CreateClockStamp(DateTime.UtcNow, installationId));

            InvalidateCache();
            return Result.Success();
        }

        #endregion

        /// <summary>
        /// Avvalgi faol litsenziya(lar)dagi faol seat'larni yangi litsenziyaga o'tkazadi. Yangi kalitda joy
        /// kamroq bo'lsa (paket pasaygan) — birinchi berilganlar qoladi, qolganlari o'chiriladi (admin qayta taqsimlaydi).
        /// Seat muddati yangi kalit muddatidan oshmaydi.
        /// </summary>
        private static void MigrateSeats(DBTHSNEntities db, System.Collections.Generic.List<int> oldLicenseIds, COMPANY_LICENSES newLic)
        {
            if (oldLicenseIds.Count == 0)
                return;

            var seats = db.USER_LICENSES
                .Where(u => u.CompanyLicenseID.HasValue && oldLicenseIds.Contains(u.CompanyLicenseID.Value) && u.IsActive)
                .OrderBy(u => u.AssignedDate).ThenBy(u => u.ID)
                .ToList();
            if (seats.Count == 0)
                return;

            var seenUsers = new System.Collections.Generic.HashSet<int>();
            int kept = 0;
            foreach (var seat in seats)
            {
                bool duplicate = seat.UserID.HasValue && !seenUsers.Add(seat.UserID.Value);
                bool fits = newLic.MaxUsers == 0 || kept < newLic.MaxUsers;
                if (duplicate || !fits)
                {
                    seat.IsActive = false;
                    continue;
                }
                seat.CompanyLicenseID = newLic.ID;
                if (newLic.ExpiryDate.HasValue && seat.ExpiryDate.HasValue && seat.ExpiryDate.Value.Date > newLic.ExpiryDate.Value.Date)
                    seat.ExpiryDate = newLic.ExpiryDate;
                kept++;
            }
            db.SaveChanges();
            if (kept < seats.Count)
                Trace.TraceWarning("[License] Yangi kalitda joy kam: " + (seats.Count - kept) + " ta seat o'chirildi.");
        }

        #region User seat (admin mustaqil boshqaradi — company muddati ichida)

        /// <summary>Faol company litsenziyasidagi seat holati (band/jami).</summary>
        public static SeatUsage GetSeatUsage(DBTHSNEntities db)
        {
            LicenseHelper.LicenseInfo info;
            var lic = GetActiveCompanyLicense(db, out info);

            var usage = new SeatUsage { Max = info != null && info.SignatureValid ? info.MaxUsers : 0, Used = 0 };
            if (lic == null) return usage;

            usage.Used = db.USER_LICENSES.Count(u => u.CompanyLicenseID == lic.ID && u.IsActive);
            return usage;
        }

        /// <summary>Foydalanuvchining seat holatini tekshiradi (login uchun).</summary>
        public static Result CheckUserLicense(DBTHSNEntities db, int userId)
        {
            return CheckUserLicense(db, userId, DateTime.Now);
        }

        /// <summary>
        /// Seat holatini berilgan vaqtga nisbatan tekshiradi. Login'da LicenseStatus.EffectiveNow
        /// beriladi — soatni orqaga surib seat muddatini uzaytirib bo'lmaydi.
        /// </summary>
        public static Result CheckUserLicense(DBTHSNEntities db, int userId, DateTime now)
        {
            LicenseHelper.LicenseInfo info;
            var lic = GetActiveCompanyLicense(db, out info);
            if (lic == null)
                return Result.Fail(L.T("Core_LicCompanyNotFound"));

            var seat = db.USER_LICENSES.FirstOrDefault(u => u.CompanyLicenseID == lic.ID && u.UserID == userId && u.IsActive);
            if (seat == null)
                return Result.Fail(L.T("Core_LicNoSeat"));

            if (seat.ExpiryDate.HasValue && now.Date > seat.ExpiryDate.Value.Date)
                return Result.Fail(L.T("Core_LicSeatExpired", seat.ExpiryDate.Value.ToString("dd.MM.yyyy")));

            return Result.Success();
        }

        /// <summary>Xodimga seat beradi (bo'sh seat bo'lsa, muddat company doirasida).</summary>
        public static Result AssignSeat(DBTHSNEntities db, int userId, DateTime? expiryDate, string assignedBy)
        {
            LicenseHelper.LicenseInfo info;
            var lic = GetActiveCompanyLicense(db, out info);
            if (lic == null || info == null || !info.SignatureValid)
                return Result.Fail(L.T("Core_LicActivateFirst"));

            // Company muddatidan oshmasin
            DateTime effectiveExpiry = expiryDate ?? info.ExpiryDate;
            if (effectiveExpiry.Date > info.ExpiryDate.Date)
                effectiveExpiry = info.ExpiryDate;

            var existing = db.USER_LICENSES.FirstOrDefault(u => u.CompanyLicenseID == lic.ID && u.UserID == userId);
            if (existing != null)
            {
                if (existing.IsActive)
                    return Result.Fail(L.T("Core_LicSeatAlreadyActive"));

                // Qayta faollashtirish
                var usageR = GetSeatUsage(db);
                if (!usageR.HasFreeSeat)
                    return Result.Fail(L.T("Core_LicAllSeatsBusy", usageR.Max));

                existing.IsActive = true;
                existing.StartDate = DateTime.Now.Date;
                existing.ExpiryDate = effectiveExpiry;
                existing.AssignedBy = assignedBy;
                existing.AssignedDate = DateTime.Now;
                db.SaveChanges();
                return Result.Success();
            }

            var usage = GetSeatUsage(db);
            if (!usage.HasFreeSeat)
                return Result.Fail(L.T("Core_LicAllSeatsBusy", usage.Max));

            db.USER_LICENSES.Add(new USER_LICENSES
            {
                CompanyLicenseID = lic.ID,
                UserID = userId,
                StartDate = DateTime.Now.Date,
                ExpiryDate = effectiveExpiry,
                IsActive = true,
                AssignedBy = assignedBy,
                AssignedDate = DateTime.Now
            });
            db.SaveChanges();
            return Result.Success();
        }

        /// <summary>Seat muddatini uzaytiradi (company muddatidan oshmaydi).</summary>
        public static Result ExtendSeat(DBTHSNEntities db, int userLicenseId, DateTime newExpiry, string byUser)
        {
            LicenseHelper.LicenseInfo info;
            var lic = GetActiveCompanyLicense(db, out info);
            if (lic == null || info == null || !info.SignatureValid)
                return Result.Fail(L.T("Core_LicNoValidCompany"));

            var seat = db.USER_LICENSES.FirstOrDefault(u => u.ID == userLicenseId && u.CompanyLicenseID == lic.ID);
            if (seat == null)
                return Result.Fail(L.T("Core_LicSeatNotFound"));

            if (newExpiry.Date > info.ExpiryDate.Date)
                return Result.Fail(L.T("Core_LicSeatBeyondCompany", info.ExpiryDate.ToString("dd.MM.yyyy")));

            seat.ExpiryDate = newExpiry.Date;
            seat.IsActive = true;
            seat.AssignedBy = byUser;
            seat.AssignedDate = DateTime.Now;
            db.SaveChanges();
            return Result.Success();
        }

        /// <summary>Seatni olib qo'yadi (bo'shatadi).</summary>
        public static Result RevokeSeat(DBTHSNEntities db, int userLicenseId)
        {
            var seat = db.USER_LICENSES.FirstOrDefault(u => u.ID == userLicenseId);
            if (seat == null)
                return Result.Fail(L.T("Core_LicSeatNotFound"));

            seat.IsActive = false;
            db.SaveChanges();
            return Result.Success();
        }

        #endregion
    }
}
