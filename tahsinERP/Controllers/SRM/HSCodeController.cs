using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    [Permission]
    public class HSCodeController : Controller
    {
        // Olish so'rovi: HSCode — sahifa faqat jadval qobig'i; qatorlar IndexData orqali (11 mingdan ortiq kod,
        // avval hammasi bitta HTML'da edi — SRM-P03)
        [ActionViewPermission(change: false)]
        public ActionResult Index()
        {
            return View();
        }

        // Olish so'rovi: HSCode/IndexData (DataTables server-side)
        [HttpGet]
        public async Task<ActionResult> IndexData()
        {
            // Index sahifasi bilan bir xil qat'iy tekshiruv (ActionViewPermission)
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HSCode", "Index"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 0, defaultDescending: false);
            using (var db = new DBTHSNEntities())
            {
                var result = await ServerTablePage.LoadAsync(
                    db.HSCODES.AsNoTracking().Select(h => new { h.ID, h.HSCODE1, h.Description }),
                    page,
                    (q, s) => q.Where(h => h.HSCODE1.Contains(s) || h.Description.Contains(s)),
                    (q, column, desc) => (column == 1
                        ? ServerTablePage.By(q, h => h.Description, desc)
                        : ServerTablePage.By(q, h => h.HSCODE1, desc)).ThenBy(h => h.ID));

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(h => new { id = h.ID, code = h.HSCODE1, description = h.Description })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [ActionViewPermission(change: true)]
        public ActionResult Create()
        {
            return View();
        }

        [ActionViewPermission(change: true)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HSCODE hsCode)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        db.HSCODES.Add(hsCode);
                        await db.SaveChangesAsync();
                        ProcurementLookups.InvalidateHSCodes();

                        LogHelper.LogToDatabase(User.Identity.Name, "ТНВЭД kodlar", $"{hsCode.HSCODE1} - ТНВЭД ni biriktirdi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        // Ichki istisno sahifaga chiqarilmaydi (SRM-B09)
                        System.Diagnostics.Trace.TraceError("HSCode Create: {0}", ex);
                        ModelState.AddModelError(string.Empty, SaveFailedMessage);
                    }
                }
            }
            return View(hsCode);
        }

        [ActionViewPermission(change: false)]
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var hscode = await db.HSCODES.FindAsync(id);
                if (hscode == null)
                    return HttpNotFound();

                return View(hscode);
            }
        }

        [ActionViewPermission(change: true)]
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var hscode = await db.HSCODES.FindAsync(id);
                if (hscode == null)
                    return HttpNotFound();

                return View(hscode);
            }
        }

        [ActionViewPermission(change: true)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? id, FormCollection gfs)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var hscodeToDelete = await db.HSCODES.FindAsync(id);
                if (hscodeToDelete == null)
                {
                    return HttpNotFound();
                }

                // HSCODES'da IsDeleted yo'q — faqat hech qayerda ishlatilmagan kod o'chiriladi
                // (avval SaveChanges Remove'siz chaqirilib, "o'chirildi" deb log yozilardi, SRM-B09)
                bool inUse = await db.P_CONTRACT_PRODUCTS.AnyAsync(p => p.HSCodeID == hscodeToDelete.ID)
                             || await db.PRODUCTS.AnyAsync(p => p.HSCodeID == hscodeToDelete.ID)
                             || await db.SALES_TTNK_LINES.AnyAsync(l => l.HsCodeID == hscodeToDelete.ID);
                if (inUse)
                {
                    ModelState.AddModelError(string.Empty, "Bu ТНВЭД kod mahsulot yoki hujjatlarda ishlatilgan, o'chirib bo'lmaydi.");
                    return View(hscodeToDelete);
                }

                try
                {
                    db.HSCODES.Remove(hscodeToDelete);
                    await db.SaveChangesAsync();
                    ProcurementLookups.InvalidateHSCodes();

                    LogHelper.LogToDatabase(User.Identity.Name, "ТНВЭД kodlar", $"{hscodeToDelete.HSCODE1} - HSCodeni o'chirdi");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("HSCode Delete: {0}", ex);
                    ModelState.AddModelError(string.Empty, SaveFailedMessage);
                }

                return View(hscodeToDelete);
            }
        }

        [ActionViewPermission(change: true)]
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var hscode = await db.HSCODES.FindAsync(id);
                if (hscode == null)
                    return HttpNotFound();

                return View(hscode);
            }
        }

        [ActionViewPermission(change: true)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HSCODE hsCode)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var hscodeToUpdate = await db.HSCODES.FindAsync(hsCode.ID);
                if (hscodeToUpdate == null)
                {
                    return HttpNotFound();
                }

                if (ModelState.IsValid)
                {
                    hscodeToUpdate.Description = hsCode.Description;
                    hscodeToUpdate.HSCODE1 = hsCode.HSCODE1;

                    try
                    {
                        await db.SaveChangesAsync();
                        ProcurementLookups.InvalidateHSCodes();

                        LogHelper.LogToDatabase(User.Identity.Name, "ТНВЭД kodlar", $"{hsCode.HSCODE1} - HSCodeni tahrirladi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError("HSCode Edit: {0}", ex);
                        ModelState.AddModelError("", SaveFailedMessage);
                    }
                }

                return View(hscodeToUpdate);
            }
        }

        private const string SaveFailedMessage =
            "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.";
    }
}
