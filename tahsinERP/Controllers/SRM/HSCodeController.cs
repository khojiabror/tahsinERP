using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.Services.Procurement;

namespace tahsinERP.Controllers
{
    [Permission]
    public class HSCodeController : Controller
    {
        // Olish so'rovi: HSCode
        [ActionViewPermission(change: false)]
        public async Task<ActionResult> Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = await db.HSCODES.AsNoTracking().ToListAsync();
                return View(list);
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
