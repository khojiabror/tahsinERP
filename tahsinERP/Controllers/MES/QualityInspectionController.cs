using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Controllers;
using tahsinERP.Helpers;

namespace tahsinERP.Controllers.MES
{
    /// <summary>
    /// Sifat nazorati. Ish oqimi: Create (kunlik) yozuvni darhol APPROVED/REJECTED qiladi va brakni
    /// DAMAGED_PRODUCTS ga yozadi. Edit/Approve/Delete GET sahifalari (view'lari) hech qachon mavjud bo'lmagan —
    /// ular olib tashlandi; tasdiqlash va o'chirish faqat POST tugmalar orqali (audit MES-B04).
    /// </summary>
    public class QualityInspectionController : BaseController
    {
        // Olish so'rovi: QualityInspection
        public async Task<ActionResult> Index(string searchTerm, int? productID, string statusCode, DateTime? fromDate, DateTime? toDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "QualityInspection", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Parametrsiz ochilganda ham ko'rsatilgan "oxirgi 30 kun" filtri qo'llanadi (avval butun jadval yuklanardi, MES-P18)
                var effectiveFrom = (fromDate ?? DateTime.Now.AddDays(-30)).Date;

                var query = db.QUALITY_INSPECTION.AsNoTracking()
                    .Include(q => q.PRODUCT)
                    .Include(q => q.DEFECT_TYPES)
                    .Where(q => !q.IsDeleted && q.InspectionDate >= effectiveFrom);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(q =>
                        (
                            q.PRODUCT != null &&
                            (
                                (q.PRODUCT.PNo != null && q.PRODUCT.PNo.Contains(searchTerm)) ||
                                (q.PRODUCT.Name != null && q.PRODUCT.Name.Contains(searchTerm))
                            )
                        )
                        ||
                        (q.Notes != null && q.Notes.Contains(searchTerm))
                    );
                }

                if (productID.HasValue)
                {
                    query = query.Where(q => q.ProductID == productID.Value);
                }

                if (!string.IsNullOrEmpty(statusCode))
                {
                    query = query.Where(q => q.StatusCode == statusCode);
                }

                if (toDate.HasValue)
                {
                    // Tanlangan kun to'liq kirishi uchun < toDate+1
                    var toExclusive = toDate.Value.Date.AddDays(1);
                    query = query.Where(q => q.InspectionDate < toExclusive);
                }

                var inspections = await query
                    .OrderByDescending(q => q.InspectionDate)
                    .ThenByDescending(q => q.ID)
                    .ToListAsync();

                var products = await db.PRODUCTS.AsNoTracking()
                    .Where(p => p.IsDeleted == false && p.IsFinishedGood == true)
                    .OrderBy(p => p.PNo)
                    .Select(p => new { p.ID, p.PNo, p.Name })
                    .ToListAsync();
                ViewBag.Products = new SelectList(
                    products.Select(p => new { p.ID, DisplayText = p.PNo + " - " + p.Name }),
                    "ID",
                    "DisplayText",
                    productID
                );
                ViewBag.StatusCodes = new SelectList(new[] {
                    new { Value = "DRAFT", Text = "Qoralama" },
                    new { Value = "APPROVED", Text = "Tasdiqlangan" },
                    new { Value = "REJECTED", Text = "Rad etilgan" }
                }, "Value", "Text", statusCode);
                ViewBag.FromDate = effectiveFrom;
                ViewBag.ToDate = toDate ?? DateTime.Now;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.ChangePermit = RoleHelper.IsChangePermitted(User.Identity.Name, "QualityInspection", "Index");

                return View(inspections);
            }
        }

        // Olish so'rovi: QualityInspection/Details/5
        public ActionResult Details(long? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "QualityInspection", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspection = db.QUALITY_INSPECTION.AsNoTracking()
                    .Where(q => q.ID == id && !q.IsDeleted)
                    .Include(q => q.PRODUCT)
                    .Include(q => q.DEFECT_TYPES)
                    .Include(q => q.S_ORDERS)
                    .Include(q => q.SORDER_DELIVERY_SCHEDULE)
                    .Include(q => q.PRODUCTPLANS_DAILY)
                    .Include(q => q.PRODUCT_PRODUCTION)
                    .FirstOrDefault();

                if (inspection == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ChangePermit = RoleHelper.IsChangePermitted(User.Identity.Name, "QualityInspection", "Details");
                return View(inspection);
            }
        }

        // Olish so'rovi: QualityInspection/yaratish
        public ActionResult Create(DateTime? date)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "QualityInspection", "Create"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspectionDate = ResolveInspectionCalendarDate(date);
                var viewModel = BuildDailyInspectionViewModel(inspectionDate, db);

                SetDefectTypesViewBag(db);
                return View(viewModel);
            }
        }

        /// <summary>
        /// Yuklash: ?date=yyyy-MM-dd — avvalo query string (brauzer date input ISO), keyin model binder.
        /// </summary>
        private DateTime ResolveInspectionCalendarDate(DateTime? date)
        {
            var raw = Request["date"];
            if (!string.IsNullOrWhiteSpace(raw)
                && DateTime.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedExact))
            {
                return parsedExact.Date;
            }

            if (date.HasValue)
            {
                return date.Value.Date;
            }

            return DateTime.Now.Date;
        }

        // POST so'rovi: QualityInspection/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(QualityInspectionDailyViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "QualityInspection", "Create"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (viewModel == null)
            {
                ModelState.AddModelError("", "Ma'lumotlar topilmadi");
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        var createdCount = 0;
                        var userId = RoleHelper.GetUserId(CurrentUserName);

                        var itemsToSave = (viewModel.Items ?? new List<QualityInspectionDailyItemViewModel>())
                            .Where(i => i.QtyAccepted + i.QtyRejected > 0)
                            .ToList();

                        // Ishlab chiqarishlar va ularning qabul qilingan miqdori bir martada (avval har qatorga 3–4 so'rov, MES-P16)
                        var productionIds = itemsToSave.Select(i => i.ProductionID).Distinct().ToList();
                        var productions = db.PRODUCT_PRODUCTION
                            .Include(p => p.PRODUCT)
                            .Where(p => productionIds.Contains(p.ID) && (p.IsDeleted == false || p.IsDeleted == null))
                            .ToDictionary(p => p.ID);
                        var acceptedByProduction = QualityInspectionBalanceHelper.GetAcceptedQtyByProduction(productionIds, db);

                        foreach (var item in itemsToSave)
                        {
                            var inspectedQty = item.QtyAccepted + item.QtyRejected;

                            // GET ro'yxat bilan bir xil: IsDeleted == null ham faol yozuv
                            if (!productions.TryGetValue(item.ProductionID, out var production))
                            {
                                ModelState.AddModelError("", $"Ishlab chiqarish topilmadi: ID {item.ProductionID}");
                                continue;
                            }

                            acceptedByProduction.TryGetValue(production.ID, out var acceptedSoFar);
                            if (!QualityInspectionBalanceHelper.TryValidateInspectionQty(
                                    production, item.QtyAccepted, item.QtyRejected, acceptedSoFar, out var qtyError))
                            {
                                ModelState.AddModelError("", qtyError);
                                continue;
                            }

                            var status = (item.QtyAccepted == 0 && item.QtyRejected > 0)
                                ? "REJECTED"
                                : "APPROVED";

                            var inspection = new QUALITY_INSPECTION
                            {
                                ProductionID = item.ProductionID,
                                ProductPlanDailyID = null,
                                ProductID = production.ProductID,
                                QtyInspected = inspectedQty,
                                QtyAccepted = item.QtyAccepted,
                                QtyRejected = item.QtyRejected,
                                DefectTypeID = item.DefectTypeID,
                                StatusCode = status,
                                Notes = item.Notes,
                                InspectionDate = viewModel.InspectionDate,
                                CreatedDT = DateTime.Now,
                                IsDeleted = false,
                                InspectedByUserID = userId
                            };

                            db.QUALITY_INSPECTION.Add(inspection);
                            createdCount++;

                            if (item.QtyRejected > 0 && item.DefectTypeID.HasValue)
                            {
                                var damagedProduct = new DAMAGED_PRODUCTS
                                {
                                    ProductID = production.ProductID,
                                    DefectTypeID = item.DefectTypeID.Value,
                                    Quantity = (double)item.QtyRejected,
                                    IssueDateTime = DateTime.Now,
                                    IsDeleted = false
                                };
                                db.DAMAGED_PRODUCTS.Add(damagedProduct);
                            }
                        }

                        if (!ModelState.IsValid)
                        {
                            viewModel = BuildDailyInspectionViewModel(viewModel.InspectionDate, db);
                            SetDefectTypesViewBag(db);
                            return View(viewModel);
                        }

                        if (createdCount == 0)
                        {
                            ModelState.AddModelError("", "Tekshiruv uchun miqdor kiritilmadi");
                            SetDefectTypesViewBag(db);
                            return View(viewModel);
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "QualityInspectionController",
                            $"Sifat nazorati yaratildi: Sana {viewModel.InspectionDate:yyyy-MM-dd}, {createdCount} ta yozuv");

                        TempData["Success"] = "Sifat nazorati muvaffaqiyatli yaratildi";
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Xatolik: {ex.Message}";
                        for (var innerEx = ex.InnerException; innerEx != null; innerEx = innerEx.InnerException)
                        {
                            errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                        }
                        ModelState.AddModelError("", errorMessage);
                    }
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (viewModel != null)
                {
                    viewModel = BuildDailyInspectionViewModel(viewModel.InspectionDate, db);
                }
                SetDefectTypesViewBag(db);
            }

            return View(viewModel);
        }

        private static QualityInspectionDailyViewModel BuildDailyInspectionViewModel(DateTime inspectionDate, DBTHSNEntities db)
        {
            var dateOnly = inspectionDate.Date;
            var nextDay = dateOnly.AddDays(1);

            // Faqat fakt: tanlangan kalendar kuni. IsDeleted: null ham faol deb hisoblanadi.
            var productions = db.PRODUCT_PRODUCTION.AsNoTracking()
                .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                    && p.Date >= dateOnly
                    && p.Date < nextDay
                    && p.PRODUCT.IsFinishedGood == true
                    && p.PRODUCT.IsDeleted == false)
                .Select(p => new { p.ID, p.ProductID, p.Total, p.PRODUCT.PNo, p.PRODUCT.Name })
                .ToList();

            // Qabul qilingan miqdorlar bitta guruhlangan so'rovda (avval har ishlab chiqarishga 2 ta so'rov, MES-P16)
            var acceptedByProduction = QualityInspectionBalanceHelper.GetAcceptedQtyByProduction(productions.Select(p => p.ID), db);

            var items = new List<QualityInspectionDailyItemViewModel>();
            foreach (var production in productions)
            {
                var producedQty = (decimal)production.Total;
                acceptedByProduction.TryGetValue(production.ID, out var alreadyAccepted);
                var remaining = producedQty - alreadyAccepted;

                if (remaining <= 0)
                {
                    continue;
                }

                items.Add(new QualityInspectionDailyItemViewModel
                {
                    ProductionID = production.ID,
                    ProductID = production.ProductID,
                    ProductNo = production.PNo,
                    ProductName = production.Name,
                    ProducedQty = producedQty,
                    AlreadyInspectedQty = alreadyAccepted,
                    RemainingQty = remaining,
                    QtyAccepted = 0,
                    QtyRejected = 0
                });
            }

            return new QualityInspectionDailyViewModel
            {
                InspectionDate = dateOnly,
                Items = items,
                ProductionRowCountForDay = productions.Count
            };
        }

        // POST so'rovi: QualityInspection/Approve/5 (GET tasdiqlash sahifasi yo'q edi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(long id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "QualityInspection", "Approve"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspection = db.QUALITY_INSPECTION
                    .FirstOrDefault(q => q.ID == id && !q.IsDeleted);
                if (inspection == null)
                {
                    return HttpNotFound();
                }

                if (inspection.StatusCode == "APPROVED")
                {
                    TempData["ErrorMessage"] = "Bu sifat nazorati allaqachon tasdiqlangan";
                    return RedirectToAction("Index");
                }

                inspection.StatusCode = "APPROVED";
                inspection.ApprovalDate = DateTime.Now;
                inspection.ApprovedByUserID = RoleHelper.GetUserId(CurrentUserName);
                inspection.UpdatedDT = DateTime.Now;

                // Brak DAMAGED_PRODUCTS ga — agar Create paytida yozilmagan bo'lsa. Create brakni CreatedDT kuni yozadi.
                // Avval LINQ ichida d.IssueDateTime.Date ishlatilgan va QtyRejected > 0 bo'lganda NotSupportedException berardi.
                if (inspection.QtyRejected > 0 && inspection.DefectTypeID.HasValue)
                {
                    var createdDay = inspection.CreatedDT.Date;
                    var createdNextDay = createdDay.AddDays(1);
                    var defectTypeId = inspection.DefectTypeID.Value;
                    var alreadyRecorded = db.DAMAGED_PRODUCTS
                        .Any(d => d.ProductID == inspection.ProductID
                            && d.DefectTypeID == defectTypeId
                            && d.IssueDateTime >= createdDay
                            && d.IssueDateTime < createdNextDay
                            && d.IsDeleted == false);

                    if (!alreadyRecorded)
                    {
                        db.DAMAGED_PRODUCTS.Add(new DAMAGED_PRODUCTS
                        {
                            ProductID = inspection.ProductID,
                            DefectTypeID = defectTypeId,
                            Quantity = (double)inspection.QtyRejected,
                            IssueDateTime = DateTime.Now,
                            IsDeleted = false
                        });
                    }
                }

                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "QualityInspectionController", $"Sifat nazorati tasdiqlandi: ID {id}");

                TempData["Success"] = "Sifat nazorati muvaffaqiyatli tasdiqlandi";
                return RedirectToAction("Index");
            }
        }

        // POST so'rovi: QualityInspection/Delete/5 (GET tasdiqlash sahifasi yo'q edi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(long id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "QualityInspection", "Delete"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspection = db.QUALITY_INSPECTION
                    .FirstOrDefault(q => q.ID == id && !q.IsDeleted);
                if (inspection == null)
                {
                    return HttpNotFound();
                }

                // Prevent deleting agar approved
                if (inspection.StatusCode == "APPROVED")
                {
                    TempData["ErrorMessage"] = "Tasdiqlangan sifat nazoratini o'chirish mumkin emas";
                    return RedirectToAction("Index");
                }

                inspection.IsDeleted = true;
                inspection.UpdatedDT = DateTime.Now;
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "QualityInspectionController", $"Sifat nazorati o'chirildi: ID {id}");

                TempData["Success"] = "Sifat nazorati muvaffaqiyatli o'chirildi";
                return RedirectToAction("Index");
            }
        }

        private void SetDefectTypesViewBag(DBTHSNEntities db)
        {
            ViewBag.DefectTypes = new SelectList(
                db.DEFECT_TYPES.AsNoTracking()
                    .Where(x => x.IsDeleted != true)
                    .Select(x => new { x.ID, x.DefectType })
                    .ToList(),
                "ID", "DefectType");
        }
    }
}
