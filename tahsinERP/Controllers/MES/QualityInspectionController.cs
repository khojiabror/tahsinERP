using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers.MES
{
    public class QualityInspectionController : Controller
    {
        // GET: QualityInspection
        public async Task<ActionResult> Index(string searchTerm, int? productID, string statusCode, DateTime? fromDate, DateTime? toDate)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.QUALITY_INSPECTION
                    .Include(q => q.PRODUCT)
                    .Include(q => q.DEFECT_TYPES)
                    .Where(q => q.IsDeleted == false)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(q => q.PRODUCT.PNo.Contains(searchTerm) || 
                                             q.PRODUCT.Name.Contains(searchTerm) ||
                                             (q.Notes != null && q.Notes.Contains(searchTerm)));
                }

                if (productID.HasValue)
                {
                    query = query.Where(q => q.ProductID == productID.Value);
                }

                if (!string.IsNullOrEmpty(statusCode))
                {
                    query = query.Where(q => q.StatusCode == statusCode);
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(q => q.InspectionDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(q => q.InspectionDate <= toDate.Value);
                }

                var inspections = await query
                    .OrderByDescending(q => q.InspectionDate)
                    .ThenByDescending(q => q.ID)
                    .ToListAsync();

                var products = await db.PRODUCTS.Where(p => p.IsDeleted == false).ToListAsync();
                ViewBag.Products = new SelectList(
                    products.Select(p => new { p.ID, DisplayText = $"{p.PNo} - {p.Name}" }), 
                    "ID", 
                    "DisplayText", 
                    productID
                );
                ViewBag.StatusCodes = new SelectList(new[] { "DRAFT", "APPROVED", "REJECTED" }, statusCode);
                ViewBag.DefectTypes = new SelectList(await db.DEFECT_TYPES.ToListAsync(), "ID", "Name");
                ViewBag.FromDate = fromDate ?? DateTime.Now.AddDays(-30);
                ViewBag.ToDate = toDate ?? DateTime.Now;
                ViewBag.SearchTerm = searchTerm;

                return View(inspections);
            }
        }

        // GET: QualityInspection/Details/5
        public ActionResult Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspection = db.QUALITY_INSPECTION
                    .Where(q => q.ID == id && (q.IsDeleted == false || q.IsDeleted == null))
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

                return View(inspection);
            }
        }

        // GET: QualityInspection/Create
        public ActionResult Create(DateTime? date)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspectionDate = (date ?? DateTime.Now).Date;
                var viewModel = BuildDailyInspectionViewModel(inspectionDate, db);

                ViewBag.DefectTypes = new SelectList(db.DEFECT_TYPES.ToList(), "ID", "Name");
                return View(viewModel);
            }
        }

        // POST: QualityInspection/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(QualityInspectionDailyViewModel viewModel)
        {
            if (viewModel == null)
            {
                ModelState.AddModelError("", "Ma'lumotlar topilmadi");
            }

            if (viewModel?.Items == null || !viewModel.Items.Any())
            {
                ModelState.AddModelError("", "Tekshiruv uchun mahsulotlar topilmadi");
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        var createdCount = 0;

                        foreach (var item in viewModel.Items)
                        {
                            var inspectedQty = item.QtyAccepted + item.QtyRejected;
                            if (inspectedQty <= 0)
                            {
                                continue;
                            }

                            var production = db.PRODUCT_PRODUCTION
                                .Include(p => p.PRODUCT)
                                .FirstOrDefault(p => p.ID == item.ProductionID
                                    && (p.IsDeleted == false || p.IsDeleted == null));

                            if (production == null)
                            {
                                ModelState.AddModelError("", $"Ishlab chiqarish topilmadi: ID {item.ProductionID}");
                                continue;
                            }

                            var alreadyInspected = db.QUALITY_INSPECTION
                                .Where(q => q.ProductionID == item.ProductionID && (q.IsDeleted == false || q.IsDeleted == null))
                                .Sum(q => (decimal?)q.QtyInspected) ?? 0;

                            var remaining = (decimal)production.Total - alreadyInspected;
                            if (inspectedQty > remaining)
                            {
                                ModelState.AddModelError("",
                                    $"Tekshirilgan miqdor ishlab chiqarilgan qoldiqdan oshib ketdi. Mahsulot: {production.PRODUCT?.PNo}, Qoldiq: {remaining}");
                                continue;
                            }

                            var status = (item.QtyAccepted == 0 && item.QtyRejected > 0)
                                ? "REJECTED"
                                : "APPROVED";

                            var inspection = new QUALITY_INSPECTION
                            {
                                ProductionID = item.ProductionID,
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
                                InspectedByUserID = UserHelper.GetUserId(User.Identity.Name)
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
                            ViewBag.DefectTypes = new SelectList(db.DEFECT_TYPES.ToList(), "ID", "Name");
                            return View(viewModel);
                        }

                        if (createdCount == 0)
                        {
                            ModelState.AddModelError("", "Tekshiruv uchun miqdor kiritilmadi");
                            ViewBag.DefectTypes = new SelectList(db.DEFECT_TYPES.ToList(), "ID", "Name");
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
                        var innerEx = ex.InnerException;
                        while (innerEx != null)
                        {
                            errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                            innerEx = innerEx.InnerException;
                        }
                        ModelState.AddModelError("", errorMessage);
                    }
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.DefectTypes = new SelectList(db.DEFECT_TYPES.ToList(), "ID", "Name");
            }

            return View(viewModel);
        }

        private QualityInspectionDailyViewModel BuildDailyInspectionViewModel(DateTime inspectionDate, DBTHSNEntities db)
        {
            var dateOnly = inspectionDate.Date;

            var productions = db.PRODUCT_PRODUCTION
                .Include(p => p.PRODUCT)
                .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                    && DbFunctions.TruncateTime(p.Date) == dateOnly
                    && p.PRODUCT.IsFinishedGood == true
                    && (p.PRODUCT.IsDeleted == false || p.PRODUCT.IsDeleted == null))
                .ToList();

            var items = new List<QualityInspectionDailyItemViewModel>();

            foreach (var production in productions)
            {
                var alreadyInspected = db.QUALITY_INSPECTION
                    .Where(q => q.ProductionID == production.ID && (q.IsDeleted == false || q.IsDeleted == null))
                    .Sum(q => (decimal?)q.QtyInspected) ?? 0;

                var producedQty = (decimal)production.Total;
                var remaining = producedQty - alreadyInspected;

                if (remaining <= 0)
                {
                    continue;
                }

                items.Add(new QualityInspectionDailyItemViewModel
                {
                    ProductionID = production.ID,
                    ProductID = production.ProductID,
                    ProductNo = production.PRODUCT?.PNo,
                    ProductName = production.PRODUCT?.Name,
                    ProducedQty = producedQty,
                    AlreadyInspectedQty = alreadyInspected,
                    RemainingQty = remaining,
                    QtyAccepted = remaining,
                    QtyRejected = 0
                });
            }

            return new QualityInspectionDailyViewModel
            {
                InspectionDate = dateOnly,
                Items = items
            };
        }

        // GET: QualityInspection/Edit/5
        public ActionResult Edit(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspection = db.QUALITY_INSPECTION.Find(id);
                if (inspection == null)
                {
                    return HttpNotFound();
                }

                // Prevent editing if approved
                if (inspection.StatusCode == "APPROVED")
                {
                    TempData["ErrorMessage"] = "Tasdiqlangan sifat nazoratini tahrirlash mumkin emas";
                    return RedirectToAction("Details", new { id = id });
                }

                var viewModel = new QualityInspectionViewModel
                {
                    ID = inspection.ID,
                    SOrderID = inspection.SOrderID,
                    DeliveryScheduleID = inspection.DeliveryScheduleID,
                    ProductPlanDailyID = inspection.ProductPlanDailyID,
                    ProductionID = inspection.ProductionID,
                    ProductID = inspection.ProductID,
                    QtyInspected = inspection.QtyInspected,
                    QtyAccepted = inspection.QtyAccepted,
                    QtyRejected = inspection.QtyRejected,
                    DefectTypeID = inspection.DefectTypeID,
                    StatusCode = inspection.StatusCode,
                    Notes = inspection.Notes,
                    InspectionDate = inspection.InspectionDate,
                    ApprovalDate = inspection.ApprovalDate
                };

                PopulateViewBags(viewModel, db);
                return View(viewModel);
            }
        }

        // POST: QualityInspection/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(QualityInspectionViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var inspection = db.QUALITY_INSPECTION.Find(viewModel.ID);
                    if (inspection == null)
                    {
                        return HttpNotFound();
                    }

                    // Prevent editing if approved
                    if (inspection.StatusCode == "APPROVED")
                    {
                        ModelState.AddModelError("", "Tasdiqlangan sifat nazoratini tahrirlash mumkin emas");
                        PopulateViewBags(viewModel, db);
                        return View(viewModel);
                    }

                    // Validate quantities
                    if (viewModel.QtyAccepted + viewModel.QtyRejected != viewModel.QtyInspected)
                    {
                        ModelState.AddModelError("", "Qabul qilingan va rad etilgan miqdorlar tekshirilgan miqdorga teng bo'lishi kerak");
                        PopulateViewBags(viewModel, db);
                        return View(viewModel);
                    }

                    inspection.QtyInspected = viewModel.QtyInspected;
                    inspection.QtyAccepted = viewModel.QtyAccepted;
                    inspection.QtyRejected = viewModel.QtyRejected;
                    inspection.DefectTypeID = viewModel.DefectTypeID;
                    inspection.StatusCode = viewModel.StatusCode;
                    inspection.Notes = viewModel.Notes;
                    inspection.InspectionDate = viewModel.InspectionDate;
                    inspection.UpdatedDT = DateTime.Now;

                    db.Entry(inspection).State = EntityState.Modified;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "QualityInspectionController", $"Sifat nazorati tahrirlandi: ID {viewModel.ID}");

                    TempData["Success"] = "Sifat nazorati muvaffaqiyatli yangilandi";
                    return RedirectToAction("Index");
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PopulateViewBags(viewModel, db);
            }

            return View(viewModel);
        }

        // GET: QualityInspection/Approve/5
        public ActionResult Approve(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspection = db.QUALITY_INSPECTION
                    .Include(q => q.PRODUCT)
                    .Include(q => q.DEFECT_TYPES)
                    .FirstOrDefault(q => q.ID == id);

                if (inspection == null)
                {
                    return HttpNotFound();
                }

                if (inspection.StatusCode == "APPROVED")
                {
                    TempData["ErrorMessage"] = "Bu sifat nazorati allaqachon tasdiqlangan";
                    return RedirectToAction("Details", new { id = id });
                }

                return View(inspection);
            }
        }

        // POST: QualityInspection/Approve/5
        [HttpPost, ActionName("Approve")]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveConfirmed(long id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspection = db.QUALITY_INSPECTION.Find(id);
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
                inspection.ApprovedByUserID = UserHelper.GetUserId(User.Identity.Name);
                inspection.UpdatedDT = DateTime.Now;

                db.Entry(inspection).State = EntityState.Modified;
                db.SaveChanges();

                // Move rejected qty to DAMAGED_PRODUCTS if not already done
                if (inspection.QtyRejected > 0 && inspection.DefectTypeID.HasValue)
                {
                    var existingDamaged = db.DAMAGED_PRODUCTS
                        .FirstOrDefault(d => d.ProductID == inspection.ProductID 
                            && d.DefectTypeID == inspection.DefectTypeID.Value
                            && d.IssueDateTime.Date == inspection.InspectionDate.Date
                            && d.IsDeleted == false);

                    if (existingDamaged == null)
                    {
                        var damagedProduct = new DAMAGED_PRODUCTS
                        {
                            ProductID = inspection.ProductID,
                            DefectTypeID = inspection.DefectTypeID.Value,
                            Quantity = (double)inspection.QtyRejected,
                            IssueDateTime = DateTime.Now,
                            IsDeleted = false
                        };
                        db.DAMAGED_PRODUCTS.Add(damagedProduct);
                        db.SaveChanges();
                    }
                }

                LogHelper.LogToDatabase(User.Identity.Name, "QualityInspectionController", $"Sifat nazorati tasdiqlandi: ID {id}");

                TempData["Success"] = "Sifat nazorati muvaffaqiyatli tasdiqlandi";
                return RedirectToAction("Index");
            }
        }

        // GET: QualityInspection/Delete/5
        public ActionResult Delete(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspection = db.QUALITY_INSPECTION
                    .Include(q => q.PRODUCT)
                    .Include(q => q.DEFECT_TYPES)
                    .FirstOrDefault(q => q.ID == id);

                if (inspection == null)
                {
                    return HttpNotFound();
                }

                // Prevent deleting if approved
                if (inspection.StatusCode == "APPROVED")
                {
                    TempData["ErrorMessage"] = "Tasdiqlangan sifat nazoratini o'chirish mumkin emas";
                    return RedirectToAction("Details", new { id = id });
                }

                return View(inspection);
            }
        }

        // POST: QualityInspection/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(long id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inspection = db.QUALITY_INSPECTION.Find(id);
                if (inspection == null)
                {
                    return HttpNotFound();
                }

                // Prevent deleting if approved
                if (inspection.StatusCode == "APPROVED")
                {
                    TempData["ErrorMessage"] = "Tasdiqlangan sifat nazoratini o'chirish mumkin emas";
                    return RedirectToAction("Index");
                }

                inspection.IsDeleted = true;
                inspection.UpdatedDT = DateTime.Now;
                db.Entry(inspection).State = EntityState.Modified;
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "QualityInspectionController", $"Sifat nazorati o'chirildi: ID {id}");

                TempData["Success"] = "Sifat nazorati muvaffaqiyatli o'chirildi";
                return RedirectToAction("Index");
            }
        }

        // Helper method to populate ViewBag
        private void PopulateViewBags(QualityInspectionViewModel viewModel, DBTHSNEntities db)
        {
            ViewBag.Products = new SelectList(
                db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).ToList(),
                "ID",
                "PNo",
                viewModel.ProductID);
            ViewBag.DefectTypes = new SelectList(db.DEFECT_TYPES.ToList(), "ID", "Name", viewModel.DefectTypeID);
            ViewBag.SOrders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", viewModel.SOrderID);
            ViewBag.DeliverySchedules = new SelectList(db.SORDER_DELIVERY_SCHEDULE.ToList(), "ID", "ID", viewModel.DeliveryScheduleID);
        }

        // AJAX: Get available quantity for invoicing
        [HttpGet]
        public JsonResult GetAvailableQtyForInvoicing(int productID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Get total accepted qty from approved inspections
                var totalAccepted = db.QUALITY_INSPECTION
                    .Where(q => q.ProductID == productID 
                        && q.StatusCode == "APPROVED" 
                        && q.IsDeleted == false)
                    .Sum(q => (decimal?)q.QtyAccepted) ?? 0;

                // Get total already invoiced
                var totalInvoiced = db.SALES_INVOICE_LINES
                    .Where(l => l.ProductID == productID)
                    .Sum(l => (decimal?)l.Qty) ?? 0;

                var available = totalAccepted - totalInvoiced;

                return Json(new { availableQty = available, totalAccepted = totalAccepted, totalInvoiced = totalInvoiced }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}

