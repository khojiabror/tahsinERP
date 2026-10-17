using System.Data.Entity.Infrastructure;
using System.Net;
using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services;
using System.Linq;
using tahsinERP.ViewModels;
using System.Data.Entity;

namespace tahsinERP.Controllers
{
    /// <summary>Ruxsat: [Permission] (GET — ko'rish, POST/Create/Edit/Delete — o'zgartirish; AJAX endpointlar ham, LOG-S02).</summary>
    [Permission]
    public class FTransportTypeController : BaseController
    {
        // Olish so'rovi: FTransportType
        public ActionResult Index()
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.F_TRANSPORT_TYPES.AsNoTracking()
                    .Where(t => t.IsDeleted != true)
                    .ToList();

                return View(list);
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                return View();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(TransportTypeViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                // Map ViewModel ga Entity Model
                var transportType = new F_TRANSPORT_TYPES
                {
                    TransportType = viewModel.TransportType,
                    ExtLgth = viewModel.ExtLgth,
                    ExtWdth = viewModel.ExtWdth,
                    ExtHght = viewModel.ExtHght,
                    IntLgth = viewModel.IntLgth,
                    IntWdth = viewModel.IntWdth,
                    IntHght = viewModel.IntHght,
                    UnitID = viewModel.UnitID,
                    CapableOfLifting = viewModel.CapableOfLifting,
                    TransportWeight = viewModel.TransportWeight
                };

                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        db.F_TRANSPORT_TYPES.Add(transportType);
                        db.SaveChanges();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "FTransportTypeController", "Create", $"{viewModel.TransportType} - transport turi yaratildi.");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Ma'lumotni saqlashda xatolik yuz berdi. Qayta urinib ko'ring.");
                        LogHelper.LogToDatabase(User.Identity.Name, "FTransportTypeController", "Create", $"Xatolik: {ex.Message}");
                        ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                    }
                }
            }
            else
            {
                ModelState.AddModelError("", "Iltimos, majburiy maydonlarni to'ldiring.");
            }

            // agar ModelState is not valid, return the view bilan the ViewModel
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
            }
            return View(viewModel);
        }

        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var transportType = db.F_TRANSPORT_TYPES
                    .Where(t => t.ID == id && (t.IsDeleted == false || t.IsDeleted == null))
                    .FirstOrDefault();
                if (transportType == null)
                    return HttpNotFound();

                db.Entry(transportType).Reference(x => x.UNIT).Load();

                // LOG-B10: o'lchamlar mm (avval sm deb ÷100 qilinardi — simulyator bilan 1000 marta farq)
                ViewBag.CBMCapacity = TransportLoadCalculator.InternalCbm(transportType);
                ViewBag.PalletCount = TransportLoadCalculator.Pallets(transportType);
                ViewBag.ExternalCBM = TransportLoadCalculator.ExternalCbm(transportType);

                return View(transportType);
            }
        }
        // Olish so'rovi: FTransportType/Edit/
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Retrieve existing entity
                var transportType = db.F_TRANSPORT_TYPES.Include(t => t.UNIT).FirstOrDefault(t => t.ID == id && t.IsDeleted != true);
                if (transportType == null)
                {
                    return HttpNotFound();
                }

                // Map entity ga view model
                var viewModel = new TransportTypeViewModel
                {
                    ID = transportType.ID,
                    TransportType = transportType.TransportType,
                    ExtLgth = transportType.ExtLgth,
                    ExtWdth = transportType.ExtWdth,
                    ExtHght = transportType.ExtHght,
                    IntLgth = transportType.IntLgth,
                    IntWdth = transportType.IntWdth,
                    IntHght = transportType.IntHght,
                    UnitID = (int)transportType.UnitID,
                    UNIT = transportType.UNIT,
                    CapableOfLifting = transportType.CapableOfLifting,
                    TransportWeight = transportType.TransportWeight
                };
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                return View(viewModel);
            }
        }
        // POST so'rovi: FTransportType/Edit/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(TransportTypeViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // Retrieve existing entity
                    var transportTypeToUpdate = db.F_TRANSPORT_TYPES.FirstOrDefault(t => t.ID == viewModel.ID && t.IsDeleted != true);
                    if (transportTypeToUpdate == null)
                    {
                        return HttpNotFound();
                    }

                    // Yangilash entity bilan ma'lumot dan view model
                    transportTypeToUpdate.TransportType = viewModel.TransportType;
                    transportTypeToUpdate.ExtLgth = viewModel.ExtLgth;
                    transportTypeToUpdate.ExtWdth = viewModel.ExtWdth;
                    transportTypeToUpdate.ExtHght = viewModel.ExtHght;
                    transportTypeToUpdate.IntLgth = viewModel.IntLgth;
                    transportTypeToUpdate.IntWdth = viewModel.IntWdth;
                    transportTypeToUpdate.IntHght = viewModel.IntHght;
                    transportTypeToUpdate.UnitID = viewModel.UnitID;
                    transportTypeToUpdate.CapableOfLifting = viewModel.CapableOfLifting;
                    transportTypeToUpdate.TransportWeight = viewModel.TransportWeight;

                    try
                    {
                        db.SaveChanges();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "FTransportTypeController", "Edit", $"{viewModel.TransportType} - transport turi tahrirlandi.");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Ma'lumotni saqlashda xatolik yuz berdi. Qayta urinib ko'ring.");
                        LogHelper.LogToDatabase(User.Identity.Name, "FTransportTypeController", "Edit", $"Xatolik: {ex.Message}");
                    }
                }
            }
            else
            {
                ModelState.AddModelError("", "Iltimos, majburiy maydonlarni to'ldiring.");
            }

            // agar ModelState is not valid, return the view bilan the ViewModel
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
            }
            return View(viewModel);
        }
        public ActionResult Delete(int? Id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {

                if (Id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                var transportType = LoadTransportTypeForDelete(db, Id.Value);
                if (transportType == null)
                {
                    return HttpNotFound();
                }

                return View(transportType);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<ActionResult> DeleteConfirmed(int? ID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var  transportTypeToDelete = db.F_TRANSPORT_TYPES.FirstOrDefault(t => t.ID == ID && t.IsDeleted != true);
                    if (transportTypeToDelete != null)
                    {
                        transportTypeToDelete.IsDeleted = true;
                        db.Entry(transportTypeToDelete).State = System.Data.Entity.EntityState.Modified;

                        try
                        {
                            await SoftDeleteReferenceService.AfterTransportTypeSoftDeletedAsync(db, transportTypeToDelete.ID);
                            await db.SaveChangesAsync();

                            var userEmail = User.Identity.Name;
                            LogHelper.LogToDatabase(userEmail, "FTransportTypeController", "Delete", $"{transportTypeToDelete.TransportType} - transport turi o'chirildi.");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError("", "Ma'lumotni o'chirishda xatolik yuz berdi. Qayta urinib ko'ring.");
                            LogHelper.LogToDatabase(User.Identity.Name, "FTransportTypeController", "Delete", $"Xatolik: {ex.Message}");
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Ma'lumotlar noto'g'ri yuborildi. Qayta urinib ko'ring.");
                }

                // LOG-B09: view null model bilan ochilmasin
                var model = ID.HasValue ? LoadTransportTypeForDelete(db, ID.Value) : null;
                if (model == null)
                {
                    return HttpNotFound();
                }
                return View(model);
            }
        }

        private static F_TRANSPORT_TYPES LoadTransportTypeForDelete(DBTHSNEntities db, int id)
        {
            return db.F_TRANSPORT_TYPES.AsNoTracking().Include(t => t.UNIT).FirstOrDefault(t => t.ID == id && t.IsDeleted != true);
        }
    }
}
