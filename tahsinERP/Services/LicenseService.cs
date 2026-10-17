using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Caching;
using tahsinERP.Configuration.Tenancy;
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
            public string Message { get; set; }
            /// <summary>Soat orqaga surilgan yoki muhr buzilgan bo'lsa — sabab (License sahifasida ko'rsatiladi).</summary>
            public string ClockProblem { get; set; }

            public LicenseHelper.LicenseInfo Info { get; set; }
            public string InstallationId { get; set; }
            /// <summary>Muddat hisobida ishlatilgan "hozir" = max(server vaqti, oxirgi ko'rilgan vaqt).</summary>
            public DateTime EffectiveNow { get; set; }

            public int DaysRemaining { get { return Info != null ? Info.DaysRemainingAt(EffectiveNow) : 0; } }
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
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("[License] Litsenziya holatini tekshirishda xatolik: " + ex);
                status = Blocked("Litsenziyani tekshirishda xatolik yuz berdi. Administrator bilan bog'laning.");
                ttl = ErrorCacheDuration;
            }

            MemoryCache.Default.Set(cacheKey, status, DateTimeOffset.Now.Add(ttl));
            return status;
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
        private static readonly string[] SyncStateNames = { STATE_REG_TOKEN, STATE_PANEL_SECRET, STATE_LAST_SYNC, STATE_LAST_SYNC_RESULT };

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
        private static string CheckAndAdvanceClock(DBTHSNEntities db, string installationId,
            COMPANY_LICENSES activeLicense, out DateTime effectiveNowUtc)
        {
            DateTime nowUtc = DateTime.UtcNow;
            effectiveNowUtc = nowUtc;

            string stamp = ReadState(db, STATE_LAST_SEEN);
            DateTime lastSeenUtc;
            if (stamp == null)
            {
                // InstallationId bor, lekin muhr o'chirilgan — qo'lda aralashuv
                return "Litsenziya vaqt muhri topilmadi (bazadan o'chirilgan). Yetkazib beruvchidan yangi kalit oling.";
            }
            if (!LicenseHelper.TryReadClockStamp(stamp, installationId, out lastSeenUtc))
            {
                return "Litsenziya vaqt muhri buzilgan (qo'lda o'zgartirilgan). Yetkazib beruvchidan yangi kalit oling.";
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
                return string.Format(
                    "Server soati orqaga surilgan: hozirgi vaqt {0:dd.MM.yyyy HH:mm}, oxirgi ko'rilgan vaqt {1:dd.MM.yyyy HH:mm}. " +
                    "Server sana/vaqtini to'g'rilang.",
                    nowUtc.ToLocalTime(), lastSeenUtc.ToLocalTime());
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

        private static LicenseStatus Blocked(string message)
        {
            return new LicenseStatus { FullAccess = false, ReadOnly = false, Message = message, EffectiveNow = DateTime.Now };
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
            string clockProblem = CheckAndAdvanceClock(db, installationId, lic, out effectiveNowUtc);

            var status = Blocked(null);
            status.InstallationId = installationId;
            status.Info = info;
            status.EffectiveNow = effectiveNowUtc.ToLocalTime();
            status.ClockProblem = clockProblem;

            if (lic == null)
            {
                status.Message = "Kompaniya litsenziyasi topilmadi. Yetkazib beruvchi bilan bog'laning.";
                return status;
            }

            if (info == null || !info.SignatureValid)
            {
                status.Message = "Kompaniya litsenziyasi yaroqsiz (imzo mos kelmadi). Yetkazib beruvchi bilan bog'laning.";
                return status;
            }

            if (info.CompanyID != InstallationCompanyId)
            {
                status.Message = "Litsenziya boshqa kompaniyaga tegishli.";
                return status;
            }

            if (!info.IsBoundTo(installationId))
            {
                status.Message = "Litsenziya boshqa o'rnatma uchun chiqarilgan. Yetkazib beruvchiga shu o'rnatma ID'sini yuboring: " + installationId;
                return status;
            }

            if (clockProblem != null)
            {
                status.Message = clockProblem;
                return status;
            }

            var state = LicenseHelper.GetState(info, status.EffectiveNow);
            status.State = state;

            switch (state)
            {
                case LicenseHelper.LicenseState.NotStarted:
                    status.Message = "Litsenziya hali kuchga kirmagan (boshlanish sanasi: " + info.StartDate.ToString("dd.MM.yyyy") + ").";
                    break;
                case LicenseHelper.LicenseState.Expired:
                    status.Message = "Kompaniya litsenziyasi muddati tugagan (" + info.ExpiryDate.ToString("dd.MM.yyyy") + "). Yetkazib beruvchi bilan bog'laning.";
                    break;
                case LicenseHelper.LicenseState.Grace:
                    status.ReadOnly = true;
                    status.Message = "Litsenziya muddati " + info.ExpiryDate.ToString("dd.MM.yyyy") +
                        " da tugagan. Tizim " + info.GraceUntil.ToString("dd.MM.yyyy") +
                        " gacha faqat o'qish rejimida ishlaydi — yangi kalitni faollashtiring.";
                    break;
                case LicenseHelper.LicenseState.ExpiringSoon:
                    status.FullAccess = true;
                    status.Message = "Litsenziya muddati tugashiga " + info.DaysRemainingAt(status.EffectiveNow) +
                        " kun qoldi (" + info.ExpiryDate.ToString("dd.MM.yyyy") + ").";
                    break;
                default:
                    status.FullAccess = true;
                    status.Message = "Litsenziya faol.";
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
                return Result.Fail("Kalitni kiriting.");

            licenseKey = licenseKey.Trim();

            LicenseHelper.LicenseInfo info;
            bool ok = LicenseHelper.TryParseKey(licenseKey, out info);
            if (!ok || info == null)
                return Result.Fail("Kalit yaroqsiz yoki soxta (imzo mos kelmadi).");

            if (info.CompanyID != InstallationCompanyId)
                return Result.Fail("Bu kalit boshqa kompaniyaga tegishli (CompanyID=" + info.CompanyID + ").");

            string installationId = GetOrCreateInstallationId(db);
            if (info.Version < 2)
            {
                if (DateTime.Now.Date >= LicenseHelper.V1ActivationCutoff)
                    return Result.Fail("Eski formatdagi (v1) kalitlar endi qabul qilinmaydi. Yetkazib beruvchidan o'rnatma ID'si bilan yangi kalit oling: " + installationId);
            }
            else if (!info.IsBoundTo(installationId))
            {
                return Result.Fail("Bu kalit boshqa o'rnatma uchun chiqarilgan. Shu o'rnatma ID'si: " + installationId);
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
                return Result.Fail("Litsenziya vaqt muhri buzilgan. Yetkazib beruvchidan yangi kalit oling.");
            if (rolledBack && !newerThanHistory)
                return Result.Fail("Server soati orqaga surilgan (oxirgi ko'rilgan vaqt: " +
                    lastSeenUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm") + "). Avval sana/vaqtni to'g'rilang.");

            bool resetStamp = !stampOk || rolledBack;
            DateTime effectiveNow = !resetStamp && lastSeenUtc > DateTime.UtcNow ? lastSeenUtc.ToLocalTime() : DateTime.Now;
            if (info.IsExpiredAt(effectiveNow))
                return Result.Fail("Bu kalitning muddati allaqachon tugagan (" + info.ExpiryDate.ToString("dd.MM.yyyy") + ").");

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
                return Result.Fail("Kompaniya litsenziyasi topilmadi.");

            var seat = db.USER_LICENSES.FirstOrDefault(u => u.CompanyLicenseID == lic.ID && u.UserID == userId && u.IsActive);
            if (seat == null)
                return Result.Fail("Sizga litsenziya (seat) berilmagan. Administrator bilan bog'laning.");

            if (seat.ExpiryDate.HasValue && now.Date > seat.ExpiryDate.Value.Date)
                return Result.Fail("Sizning litsenziyangiz muddati tugagan (" + seat.ExpiryDate.Value.ToString("dd.MM.yyyy") + "). Administrator bilan bog'laning.");

            return Result.Success();
        }

        /// <summary>Xodimga seat beradi (bo'sh seat bo'lsa, muddat company doirasida).</summary>
        public static Result AssignSeat(DBTHSNEntities db, int userId, DateTime? expiryDate, string assignedBy)
        {
            LicenseHelper.LicenseInfo info;
            var lic = GetActiveCompanyLicense(db, out info);
            if (lic == null || info == null || !info.SignatureValid)
                return Result.Fail("Avval yaroqli kompaniya litsenziyasini faollashtiring.");

            // Company muddatidan oshmasin
            DateTime effectiveExpiry = expiryDate ?? info.ExpiryDate;
            if (effectiveExpiry.Date > info.ExpiryDate.Date)
                effectiveExpiry = info.ExpiryDate;

            var existing = db.USER_LICENSES.FirstOrDefault(u => u.CompanyLicenseID == lic.ID && u.UserID == userId);
            if (existing != null)
            {
                if (existing.IsActive)
                    return Result.Fail("Bu xodimda allaqachon faol seat bor.");

                // Qayta faollashtirish
                var usageR = GetSeatUsage(db);
                if (!usageR.HasFreeSeat)
                    return Result.Fail("Barcha " + usageR.Max + " seat band. Yetkazib beruvchidan qo'shimcha oling.");

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
                return Result.Fail("Barcha " + usage.Max + " seat band. Yetkazib beruvchidan qo'shimcha oling.");

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
                return Result.Fail("Yaroqli kompaniya litsenziyasi yo'q.");

            var seat = db.USER_LICENSES.FirstOrDefault(u => u.ID == userLicenseId && u.CompanyLicenseID == lic.ID);
            if (seat == null)
                return Result.Fail("Seat topilmadi.");

            if (newExpiry.Date > info.ExpiryDate.Date)
                return Result.Fail("Muddat kompaniya litsenziyasidan (" + info.ExpiryDate.ToString("dd.MM.yyyy") + ") oshmasligi kerak.");

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
                return Result.Fail("Seat topilmadi.");

            seat.IsActive = false;
            db.SaveChanges();
            return Result.Success();
        }

        #endregion
    }
}
