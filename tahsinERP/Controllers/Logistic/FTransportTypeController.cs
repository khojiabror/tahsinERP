using System.Data.Entity.Infrastructure;
using System.Net;
using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using System.Linq;
using DocumentFormat.OpenXml.Drawing.Charts;
using tahsinERP.ViewModels;
using System.Data.Entity;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers
{
    public class FTransportTypeController : Controller
    {
        // Olish so'rovi: FTransportType
        public ActionResult Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.F_TRANSPORT_TYPES
                    .Where(t => t.IsDeleted == false || t.IsDeleted == null)
                    .Include(t => t.UNIT)
                    .ToList();
                
                // Calculate CBM uchun har bir transport type
                foreach (var item in list)
                {
                    if (item.IntLgth > 0 && item.IntWdth > 0 && item.IntHght > 0)
                    {
                        double lengthM = item.IntLgth / 100.0;
                        double widthM = item.IntWdth / 100.0;
                        double heightM = item.IntHght / 100.0;
                        ViewData[$"CBM_{item.ID}"] = lengthM * widthM * heightM;
                    }
                }
                
                return View(list);
            }
        }
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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


        /*[HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(F_TRANSPORT_TYPES transportType)
        {
            if (transportType == null)
            {
                ModelState.AddModelError("", "Forma ma'lumotlari noto'g'ri. Iltimos, kiritilgan qiymatlarni tekshiring.");
                return View(transportType);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        db.F_TRANSPORT_TYPES.Add(transportType);
                        db.SaveChanges();
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Xatolik: " + ex.Message);
                }
            }

            return View(transportType);
        }*/
        public ActionResult Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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

                // Calculate CBM capacity (internal dimensions in meters)
                double? cbmCapacity = null;
                if (transportType.IntLgth > 0 && transportType.IntWdth > 0 && transportType.IntHght > 0)
                {
                    // Aylantirish ga meters agar needed (assuming dimensions are in cm, adjust as needed)
                    double lengthM = transportType.IntLgth / 100.0;
                    double widthM = transportType.IntWdth / 100.0;
                    double heightM = transportType.IntHght / 100.0;
                    cbmCapacity = lengthM * widthM * heightM;
                }

                // Calculate possible pallet count (assuming standard pallet size: 120cm x 100cm)
                int? palletCount = null;
                if (transportType.IntLgth > 0 && transportType.IntWdth > 0)
                {
                    // Standard pallet: 120cm x 100cm
                    int palletsLength = (int)(transportType.IntLgth / 120);
                    int palletsWidth = (int)(transportType.IntWdth / 100);
                    palletCount = palletsLength * palletsWidth;
                }

                ViewBag.CBMCapacity = cbmCapacity;
                ViewBag.PalletCount = palletCount;
                ViewBag.ExternalCBM = transportType.ExtLgth > 0 && transportType.ExtWdth > 0 && transportType.ExtHght > 0 ?
                    (transportType.ExtLgth / 100.0) * (transportType.ExtWdth / 100.0) * (transportType.ExtHght / 100.0) : (double?)null;

                return View(transportType);
            }
        }
        // Olish so'rovi: FTransportType/Edit/
        public ActionResult Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Retrieve existing entity
                var transportType = db.F_TRANSPORT_TYPES.Find(id);
                if (transportType == null)
                {
                    return HttpNotFound();
                }
                db.Entry(transportType).Reference(x => x.UNIT).Load();

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // Retrieve existing entity
                    var transportTypeToUpdate = db.F_TRANSPORT_TYPES.Find(viewModel.ID);
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
                   // transportTypeToUpdate.UnitID = viewModel.UnitID;
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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {

                if (Id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                var transportType = db.F_TRANSPORT_TYPES.Find(Id);
                if (transportType == null)
                {
                    return HttpNotFound();
                }
                db.Entry(transportType).Reference(x => x.UNIT).Load();

                return View(transportType);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? ID, FormCollection gfs)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var  transportTypeToDelete = db.F_TRANSPORT_TYPES.Find(ID);
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
                return View();
            }
        }
    }
}
