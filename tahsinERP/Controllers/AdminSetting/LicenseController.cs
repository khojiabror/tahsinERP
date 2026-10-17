using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Licensing;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.LicenseSync;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Litsenziya boshqaruvi: company kalitni faollashtirish + xodim seat CRUD.
    /// Faqat Admin/Developer kira oladi. "Blocked" sahifasi esa litsenziya
    /// tugaganda hammaga ko'rinadi (LicenseCheckAttribute yo'naltiradi).
    /// </summary>
    public class LicenseController : Controller
    {
        private bool CanManage()
        {
            return RoleHelper.CanManageLicense(User.Identity.Name);
        }

        // GET: /License
        public ActionResult Index()
        {
            if (!CanManage())
                return RedirectToAction("Index", "ViewNotAuthorized");

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var vm = BuildViewModel(db);
                    return View(vm);
                }
            }
            catch (Exception ex)
            {
                // Masalan, LICENSE_STATE jadvalini yaratishga huquq yo'q — sabab logda
                System.Diagnostics.Trace.TraceError("[License] License sahifasini qurishda xatolik: " + ex);
                TempData["Error"] = "Litsenziya holatini o'qishda xatolik yuz berdi. Database_Migrations/Create_License_System.sql " +
                                    "ishga tushirilganini tekshiring yoki tizim logini ko'ring.";
                return View(new LicenseIndexViewModel { StatusMessage = "Holat aniqlanmadi." });
            }
        }

        // GET: /License/Blocked  — litsenziya yaroqsiz bo'lganda ko'rsatiladi
        [AllowAnonymous]
        public ActionResult Blocked()
        {
            ViewBag.Message = TempData["LicenseMessage"] as string
                ?? "Kompaniya litsenziyasi yaroqsiz yoki muddati tugagan.";
            bool canManage = User.Identity.IsAuthenticated && CanManage();
            ViewBag.CanManage = canManage;
            if (canManage)
            {
                try
                {
                    using (var db = new DBTHSNEntities())
                        ViewBag.InstallationId = LicenseService.GetOrCreateInstallationId(db);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("[License] InstallationId o'qilmadi: " + ex);
                }
            }
            return View();
        }

        // POST: /License/Activate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Activate(string licenseKey, string returnTo)
        {
            if (!CanManage())
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                var result = LicenseService.ActivateCompanyLicense(db, licenseKey, User.Identity.Name);
                TempData[result.Ok ? "Success" : "Error"] = result.Ok
                    ? "Litsenziya muvaffaqiyatli faollashtirildi."
                    : result.Message;
            }

            if (string.Equals(returnTo, "Blocked", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            return RedirectToAction("Index");
        }

        // POST: /License/ConnectPanel — reg_token saqlanadi va darhol ro'yxatdan o'tiladi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ConnectPanel(string regToken)
        {
            if (!CanManage())
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                var saved = LicenseSyncService.SaveRegToken(db, regToken);
                if (!saved.Ok)
                {
                    TempData["Error"] = saved.Message;
                    return RedirectToAction("Index");
                }
            }
            return await RunSync();
        }

        // POST: /License/SyncNow — "Hozir tekshirish"
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SyncNow()
        {
            if (!CanManage())
                return RedirectToAction("Index", "ViewNotAuthorized");
            return await RunSync();
        }

        private async Task<ActionResult> RunSync()
        {
            var result = await LicenseSyncService.SyncNowAsync(Request.Url.Host);
            TempData[result.Ok ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Index");
        }

        // POST: /License/AssignSeat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignSeat(int userId, DateTime? expiryDate)
        {
            if (!CanManage())
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                var result = LicenseService.AssignSeat(db, userId, expiryDate, User.Identity.Name);
                TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "Seat berildi." : result.Message;
            }
            return RedirectToAction("Index");
        }

        // POST: /License/ExtendSeat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExtendSeat(int userLicenseId, DateTime newExpiry)
        {
            if (!CanManage())
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                var result = LicenseService.ExtendSeat(db, userLicenseId, newExpiry, User.Identity.Name);
                TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "Muddat uzaytirildi." : result.Message;
            }
            return RedirectToAction("Index");
        }

        // POST: /License/RevokeSeat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RevokeSeat(int userLicenseId)
        {
            if (!CanManage())
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                var result = LicenseService.RevokeSeat(db, userLicenseId);
                TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "Seat olib qo'yildi." : result.Message;
            }
            return RedirectToAction("Index");
        }

        #region ViewModel qurish

        private LicenseIndexViewModel BuildViewModel(DBTHSNEntities db)
        {
            var vm = new LicenseIndexViewModel();
            int companyId = LicenseService.InstallationCompanyId;

            var status = LicenseService.GetStatus(db);
            var info = status.Info;
            LicenseHelper.LicenseInfo ignored;
            var lic = LicenseService.GetActiveCompanyLicense(db, out ignored);

            vm.InstallationId = status.InstallationId;
            vm.ClockProblem = status.ClockProblem;

            vm.PanelEnabled = LicenseSyncService.IsEnabled;
            vm.PanelConnected = !string.IsNullOrEmpty(LicenseService.ReadSyncState(db, LicenseService.STATE_PANEL_SECRET));
            vm.HasRegToken = !string.IsNullOrEmpty(LicenseService.ReadSyncState(db, LicenseService.STATE_REG_TOKEN));
            vm.LastSyncResult = LicenseService.ReadSyncState(db, LicenseService.STATE_LAST_SYNC_RESULT);
            DateTime lastSync;
            if (DateTime.TryParse(LicenseService.ReadSyncState(db, LicenseService.STATE_LAST_SYNC), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out lastSync))
                vm.LastSyncUtc = lastSync;

            if (lic == null)
            {
                vm.HasLicense = false;
                vm.StatusMessage = "Kompaniya litsenziyasi topilmadi. Kalitni kiriting.";
            }
            else
            {
                vm.HasLicense = true;
                vm.SignatureValid = info != null && info.SignatureValid;
                if (info != null && info.SignatureValid)
                {
                    DateTime now = status.EffectiveNow;
                    vm.KeyVersion = info.Version;
                    vm.MaxUsers = info.MaxUsers;
                    vm.Unlimited = info.MaxUsers == 0;
                    vm.StartDate = info.StartDate;
                    vm.ExpiryDate = info.ExpiryDate;
                    vm.Edition = info.Edition;
                    vm.IsExpired = info.IsExpiredAt(now);
                    vm.InGrace = status.State == LicenseHelper.LicenseState.Grace;
                    vm.GraceUntil = info.GraceUntil;
                    vm.ExpiringSoon = status.State == LicenseHelper.LicenseState.ExpiringSoon;
                    vm.WrongInstallation = !info.IsBoundTo(status.InstallationId);
                    vm.IsValid = status.FullAccess;
                    vm.DaysRemaining = info.DaysRemainingAt(now);
                    vm.StatusMessage = status.Message;
                }
                else
                {
                    vm.StatusMessage = "Litsenziya imzosi yaroqsiz (soxta yoki buzilgan).";
                }

                vm.UsedSeats = db.USER_LICENSES.Count(u => u.CompanyLicenseID == lic.ID && u.IsActive);
            }

            // Xodimlar ro'yxati (shu kompaniya). Developer'lar faqat Developer'ga ko'rinadi.
            bool viewerIsDev = RoleHelper.IsDeveloper(User.Identity.Name);

            var users = db.USERS
                .Where(u => u.CompanyID == companyId && u.IsDeleted != true)
                .OrderBy(u => u.FullName)
                .ToList();

            foreach (var u in users)
            {
                // Developer xodimlarni yashirish (Developer bo'lmagan admin uchun)
                if (!viewerIsDev && RoleHelper.IsDeveloper(u.Uname))
                    continue;

                var seat = lic == null ? null :
                    db.USER_LICENSES.FirstOrDefault(x => x.CompanyLicenseID == lic.ID && x.UserID == u.ID && x.IsActive);

                vm.Users.Add(new SeatRow
                {
                    UserID = u.ID,
                    Uname = u.Uname,
                    FullName = u.FullName ?? u.Uname,
                    IsActiveUser = u.IsActive,
                    UserLicenseID = seat != null ? (int?)seat.ID : null,
                    HasSeat = seat != null,
                    SeatExpiry = seat != null ? seat.ExpiryDate : null,
                    SeatExpired = seat != null && seat.ExpiryDate.HasValue && DateTime.Now.Date > seat.ExpiryDate.Value.Date
                });
            }

            // Tarix
            vm.History = db.COMPANY_LICENSES
                .Where(l => l.CompanyID == companyId)
                .OrderByDescending(l => l.ID)
                .ToList()
                .Select(l => new LicenseHistoryRow
                {
                    ID = l.ID,
                    MaxUsers = l.MaxUsers,
                    StartDate = l.StartDate,
                    ExpiryDate = l.ExpiryDate,
                    Edition = l.Edition,
                    IsActive = l.IsActive,
                    CreatedAt = l.CreatedAt,
                    CreatedBy = l.CreatedBy
                })
                .ToList();

            return vm;
        }

        #endregion
    }
}
