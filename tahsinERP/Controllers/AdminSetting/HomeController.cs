using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels.Home;

namespace tahsinERP.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var userEmail = User.Identity.Name;
            if (CookieHelper.IsConfirmed(userEmail, HttpContext) == false)
            {
                Session["HasSeenCookieConsent"] = true;
                ViewBag.ShowCookieConsent = true;
            }
            else
            {
                ViewBag.ShowCookieConsent = false;
            }
            return View();
        }

        // Finance Module - P&L Plan vs Fact Monthly
        [HttpGet]
        public JsonResult GetFinancePlanVsFact(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var query = db.V_FIN_PL_PLAN_VS_FACT_MONTHLY.AsQueryable();
                    
                    if (fromDate.HasValue)
                        query = query.Where(x => x.PeriodStartDate >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(x => x.PeriodStartDate <= toDate.Value);

                    var data = query.OrderBy(x => x.PeriodStartDate).ThenBy(x => x.SortOrder).ToList();
                    
                    // Ma'lumotlarni memory'ga yuklab, keyin formatlash
                    var result = data.GroupBy(x => new { x.Year, x.Month, x.PeriodStartDate })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            PeriodStartDate = g.Key.PeriodStartDate,
                            plan = g.Sum(x => (double?)x.AmountPlan) ?? 0,
                            fact = g.Sum(x => (double?)x.AmountFact) ?? 0,
                            delta = g.Sum(x => (double?)x.Delta) ?? 0
                        })
                        .ToList()
                        .Select(g => new
                        {
                            period = g.PeriodStartDate?.ToString("yyyy-MM") ?? string.Format("{0}-{1:D2}", g.Year, g.Month),
                            plan = g.plan,
                            fact = g.fact,
                            delta = g.delta
                        }).ToList();

                    return Json(result, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Finance Module - P&L Fact Monthly
        [HttpGet]
        public JsonResult GetFinanceFactMonthly(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var query = db.V_FIN_PL_FACT_MONTHLY.AsQueryable();
                    
                    if (fromDate.HasValue)
                        query = query.Where(x => x.PeriodStartDate >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(x => x.PeriodStartDate <= toDate.Value);

                    var data = query.OrderBy(x => x.PeriodStartDate).ThenBy(x => x.SortOrder).ToList();
                    
                    var result = data.GroupBy(x => new { x.ItemName, x.PeriodStartDate })
                        .Select(g => new
                        {
                            item = g.Key.ItemName,
                            period = g.Key.PeriodStartDate?.ToString("yyyy-MM") ?? "N/A",
                            amount = g.Sum(x => (double?)x.AmountFact) ?? 0
                        }).ToList();

                    return Json(result, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Finance Module - Cash Summary
        [HttpGet]
        public JsonResult GetFinanceCashSummary(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var from = fromDate ?? DateTime.Now.AddMonths(-6);
                    var to = toDate ?? DateTime.Now;
                    
                    var results = db.sp_Fin_CashSummary(from, to).ToList();
                    
                    var data = results.Select(x => new
                    {
                        accountName = x.AccountName,
                        accountCode = x.AccountCode,
                        openingBalance = (double)x.OpeningBalance,
                        income = (double)x.Income,
                        expense = (double)x.Expense,
                        closingBalance = (double)(x.ClosingBalance ?? 0)
                    }).ToList();

                    return Json(data, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // SRM Module - Product Stocks Statistics
        [HttpGet]
        public JsonResult GetSRMMaterialRequirements()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var data = (from stock in db.PRODUCT_STOCKS
                               join product in db.PRODUCTS on stock.ProductID equals product.ID
                               where product.IsDeleted == false
                               group stock by new { product.PNo, product.ID } into g
                               select new
                               {
                                   productNo = g.Key.PNo,
                                   totalStock = g.Sum(x => (double?)x.Amount) ?? 0,
                                   warehouseCount = g.Select(x => x.WHID).Distinct().Count()
                               })
                               .OrderByDescending(x => x.totalStock)
                               .Take(10)
                               .ToList();

                    return Json(data, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // CRM Module - Sales Orders Statistics
        [HttpGet]
        public JsonResult GetCRMSalesOrdersStats(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var query = db.S_ORDERS.Where(x => x.IsDeleted == false);
                    
                    if (fromDate.HasValue)
                        query = query.Where(x => x.IssuedDate >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(x => x.IssuedDate <= toDate.Value);

                    // Avval ma'lumotlarni memory'ga yuklash
                    var data = query
                        .GroupBy(x => new { Year = x.IssuedDate.Year, Month = x.IssuedDate.Month })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            orderCount = g.Count(),
                            totalAmount = g.Sum(x => (double?)x.Amount) ?? 0
                        })
                        .ToList()
                        // Keyin string formatlash (memory'da)
                        .Select(g => new
                        {
                            period = string.Format("{0}-{1:D2}", g.Year, g.Month),
                            orderCount = g.orderCount,
                            totalAmount = g.totalAmount
                        })
                        .OrderBy(x => x.period)
                        .ToList();

                    return Json(data, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // CRM Module - Customers Statistics
        [HttpGet]
        public JsonResult GetCRMCustomersStats()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var totalCustomers = db.CUSTOMERS.Count();
                    var activeCustomers = db.CUSTOMERS.Count(); // Add IsActive filter if exists
                    var totalOrders = db.S_ORDERS.Count(x => x.IsDeleted == false);
                    var totalContracts = db.S_CONTRACTS.Count();

                    return Json(new
                    {
                        totalCustomers,
                        activeCustomers,
                        totalOrders,
                        totalContracts
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // MES Module - Production Plans Statistics
        [HttpGet]
        public JsonResult GetMESProductionStats(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var query = db.PRODUCTPLANS.Where(x => x.IsDeleted == false);
                    
                    if (fromDate.HasValue)
                        query = query.Where(x => x.StartDate >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(x => x.StartDate <= toDate.Value);

                    // Avval ma'lumotlarni memory'ga yuklash
                    var data = query
                        .GroupBy(x => new { Year = x.StartDate.Year, Month = x.StartDate.Month })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            planCount = g.Count(),
                            totalQuantity = g.Sum(x => (double?)x.Amount) ?? 0
                        })
                        .ToList()
                        // Keyin string formatlash (memory'da)
                        .Select(g => new
                        {
                            period = string.Format("{0}-{1:D2}", g.Year, g.Month),
                            planCount = g.planCount,
                            totalQuantity = g.totalQuantity
                        })
                        .OrderBy(x => x.period)
                        .ToList();

                    return Json(data, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // HR Module - Basic Statistics
        [HttpGet]
        public JsonResult GetHRStats()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var totalEmployees = db.HR_EMPLOYEES.Count();
                    var activeEmployees = db.HR_EMPLOYEES.Count(e => e.IsActive);
                    var totalDepartments = db.HR_ORG_UNITS.Count(d => d.IsActive);

                    return Json(new
                    {
                        totalEmployees,
                        activeEmployees,
                        totalDepartments
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Tasks Module - Statistics
        [HttpGet]
        public JsonResult GetTasksStats()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var totalTasks = db.APP_TASKS.Count(t => t.IsDeleted == false);
                    var completedTasks = db.APP_TASKS.Count(t => t.IsDeleted == false && 
                        t.APP_TASK_STATUS_REF != null && t.APP_TASK_STATUS_REF.Code == "DONE");
                    var pendingTasks = totalTasks - completedTasks;

                    return Json(new
                    {
                        totalTasks,
                        completedTasks,
                        pendingTasks
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Documents Module - Statistics
        [HttpGet]
        public JsonResult GetDocumentsStats()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // Date'ni query'dan tashqarida hisoblash
                    var oneMonthAgo = DateTime.Now.AddMonths(-1);
                    
                    var totalDocuments = db.DOC_DOCUMENTS.Count();
                    var recentDocuments = db.DOC_DOCUMENTS.Count(d => 
                        d.CreatedDT >= oneMonthAgo);

                    return Json(new
                    {
                        totalDocuments,
                        recentDocuments
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult CookieConfirm()
        {
            try
            {
                var userEmail = User.Identity.Name;
                CookieHelper.Confirm(userEmail, HttpContext);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult About()
        {
            ViewBag.Message = "Ilova tavsif sahifasi.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Sizning aloqa sahifangiz.";

            return View();
        }
    }
}