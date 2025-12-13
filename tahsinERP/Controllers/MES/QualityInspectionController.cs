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

                ViewBag.Products = new SelectList(await db.PRODUCTS.Where(p => p.IsDeleted == false).ToListAsync(), "ID", "PNo", productID);
                ViewBag.StatusCodes = new SelectList(new[] { "DRAFT", "APPROVED", "REJECTED" }, statusCode);
                ViewBag.DefectTypes = new SelectList(await db.DEFECT_TYPES.ToListAsync(), "ID", "Name");
                ViewBag.FromDate = fromDate ?? DateTime.Now.AddDays(-30);
                ViewBag.ToDate = toDate ?? DateTime.Now;

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
                    .Include(q => q.PRODUCT)
                    .Include(q => q.DEFECT_TYPES)
                    .Include(q => q.S_ORDERS)
                    .Include(q => q.SORDER_DELIVERY_SCHEDULE)
                    .Include(q => q.PRODUCTPLANS_DAILY)
                    .Include(q => q.PRODUCT_PRODUCTION)
                    .FirstOrDefault(q => q.ID == id);

                if (inspection == null)
                {
                    return HttpNotFound();
                }

                return View(inspection);
            }
        }

        // GET: QualityInspection/Create
        public ActionResult Create(int? sOrderID, long? deliveryScheduleID, int? productPlanDailyID, int? productionID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new QualityInspectionViewModel
                {
                    SOrderID = sOrderID,
                    DeliveryScheduleID = deliveryScheduleID,
                    ProductPlanDailyID = productPlanDailyID,
                    ProductionID = productionID,
                    InspectionDate = DateTime.Now,
                    StatusCode = "DRAFT"
                };

                // Pre-fill product and quantities based on source
                if (productionID.HasValue)
                {
                    var production = db.PRODUCT_PRODUCTION
                        .Include(p => p.PRODUCT)
                        .FirstOrDefault(p => p.ID == productionID.Value);
                    if (production != null)
                    {
                        viewModel.ProductID = production.ProductID;
                        viewModel.QtyInspected = (decimal)production.Total;
                        viewModel.QtyAccepted = (decimal)production.Total;
                        viewModel.QtyRejected = 0;
                    }
                }
                else if (productPlanDailyID.HasValue)
                {
                    var dailyPlan = db.PRODUCTPLANS_DAILY
                        .Include(d => d.PRODUCTPLAN)
                        .Include(d => d.PRODUCTPLAN.PRODUCT)
                        .FirstOrDefault(d => d.ID == productPlanDailyID.Value);
                    if (dailyPlan != null)
                    {
                        viewModel.ProductID = dailyPlan.PRODUCTPLAN.ProductID;
                        viewModel.QtyInspected = (decimal)dailyPlan.Total;
                        viewModel.QtyAccepted = (decimal)dailyPlan.Total;
                        viewModel.QtyRejected = 0;
                    }
                }
                else if (deliveryScheduleID.HasValue)
                {
                    var schedule = db.SORDER_DELIVERY_SCHEDULE
                        .Include(s => s.PRODUCT)
                        .FirstOrDefault(s => s.ID == deliveryScheduleID.Value);
                    if (schedule != null)
                    {
                        viewModel.ProductID = schedule.ProductID ?? 0;
                        viewModel.QtyInspected = schedule.QtyPlanned;
                        viewModel.QtyAccepted = schedule.QtyPlanned;
                        viewModel.QtyRejected = 0;
                    }
                }
                else if (sOrderID.HasValue)
                {
                    var orderProduct = db.S_ORDER_PRODUCTS
                        .Include(op => op.PRODUCT)
                        .FirstOrDefault(op => op.OrderID == sOrderID.Value);
                    if (orderProduct != null)
                    {
                        viewModel.ProductID = orderProduct.ProductID;
                        viewModel.QtyInspected = (decimal)orderProduct.Amount;
                        viewModel.QtyAccepted = (decimal)orderProduct.Amount;
                        viewModel.QtyRejected = 0;
                    }
                }

                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo", viewModel.ProductID);
                ViewBag.DefectTypes = new SelectList(db.DEFECT_TYPES.ToList(), "ID", "Name");
                ViewBag.SOrders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", sOrderID);
                ViewBag.DeliverySchedules = new SelectList(db.SORDER_DELIVERY_SCHEDULE.ToList(), "ID", "ID", deliveryScheduleID);

                return View(viewModel);
            }
        }

        // POST: QualityInspection/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(QualityInspectionViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Validate quantities
                        if (viewModel.QtyAccepted + viewModel.QtyRejected != viewModel.QtyInspected)
                        {
                            ModelState.AddModelError("", "Qabul qilingan va rad etilgan miqdorlar tekshirilgan miqdorga teng bo'lishi kerak");
                            PopulateViewBags(viewModel, db);
                            return View(viewModel);
                        }

                        // Check if quantities exceed available
                        if (viewModel.ProductionID.HasValue)
                        {
                            var production = db.PRODUCT_PRODUCTION.Find(viewModel.ProductionID.Value);
                            if (production != null && viewModel.QtyInspected > (decimal)production.Total)
                            {
                                ModelState.AddModelError("", $"Tekshirilgan miqdor ishlab chiqarilgan miqdordan ({production.Total}) oshib ketdi");
                                PopulateViewBags(viewModel, db);
                                return View(viewModel);
                            }
                        }

                        var inspection = new QUALITY_INSPECTION
                        {
                            SOrderID = viewModel.SOrderID,
                            DeliveryScheduleID = viewModel.DeliveryScheduleID,
                            ProductPlanDailyID = viewModel.ProductPlanDailyID,
                            ProductionID = viewModel.ProductionID,
                            ProductID = viewModel.ProductID,
                            QtyInspected = viewModel.QtyInspected,
                            QtyAccepted = viewModel.QtyAccepted,
                            QtyRejected = viewModel.QtyRejected,
                            DefectTypeID = viewModel.DefectTypeID,
                            StatusCode = viewModel.StatusCode,
                            Notes = viewModel.Notes,
                            InspectionDate = viewModel.InspectionDate,
                            CreatedDT = DateTime.Now,
                            IsDeleted = false,
                            InspectedByUserID = UserHelper.GetUserId(User.Identity.Name)
                        };

                        db.QUALITY_INSPECTION.Add(inspection);
                        db.SaveChanges();

                        // If approved, move rejected qty to DAMAGED_PRODUCTS
                        if (viewModel.StatusCode == "APPROVED" && viewModel.QtyRejected > 0 && viewModel.DefectTypeID.HasValue)
                        {
                            var damagedProduct = new DAMAGED_PRODUCTS
                            {
                                ProductID = viewModel.ProductID,
                                DefectTypeID = viewModel.DefectTypeID.Value,
                                Quantity = (double)viewModel.QtyRejected,
                                IssueDateTime = DateTime.Now,
                                IsDeleted = false
                            };
                            db.DAMAGED_PRODUCTS.Add(damagedProduct);
                            db.SaveChanges();
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "QualityInspectionController", $"Sifat nazorati yaratildi: ID {inspection.ID}, Mahsulot: {viewModel.ProductID}");

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
                PopulateViewBags(viewModel, db);
            }

            return View(viewModel);
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
            ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo", viewModel.ProductID);
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

