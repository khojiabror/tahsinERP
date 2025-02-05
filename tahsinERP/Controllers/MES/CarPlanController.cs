using DocumentFormat.OpenXml.Vml.Spreadsheet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class CarPlanController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var models = db.CARS.Select(c => c.Model).Distinct().ToList();
                ViewBag.CarModelList = new SelectList(models);
                ViewBag.CarOptionList = new SelectList(db.CARS.ToList(), "ID", "OptionCode");
                return View();
            }
        }
        public async Task<JsonResult> GetOptionCode(string modelName)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var optionCodes = await db.CARS.Where(c => c.Model == modelName)
                                               .Select(x => new { x.ID, x.OptionCode })
                                               .ToListAsync();

                if (optionCodes.Count <= 0)
                {
                    return Json(new { success = false, message = "Berilgan Model bo'yicha OptionCode topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = true, data = optionCodes }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public ActionResult Create(CAR_PLANS plan)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    CAR tempoCar = db.CARS.Where(c => c.ID == plan.CarID).FirstOrDefault();
                    CAR_PLANS checkPlan = db.CAR_PLANS.Where(pp => pp.CarID == plan.CarID &&
                        DbFunctions.TruncateTime(pp.StartDate) == DbFunctions.TruncateTime(plan.StartDate) &&
                        DbFunctions.TruncateTime(pp.DueDate) == DbFunctions.TruncateTime(plan.DueDate)).FirstOrDefault();

                    if (checkPlan == null)
                    {
                        CAR_PLANS newPlan = new CAR_PLANS();
                        newPlan.CarID = plan.CarID;
                        newPlan.Amount = plan.Amount;
                        newPlan.IsDeleted = false;
                        newPlan.StartDate = plan.StartDate;
                        newPlan.DueDate = plan.DueDate;

                        db.CAR_PLANS.Add(newPlan);
                        db.SaveChanges();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "Avtomobilga reja", $"{tempoCar.Model} uchun {plan.Amount} reja kiritdi");

                        ViewBag.DataTable = db.Database.SqlQuery<GetCarPlanRequirements_Result>("EXEC GetCarPlanRequirements @OptionCode", new SqlParameter("@OptionCode", tempoCar.OptionCode)).ToList();
                        ViewBag.DataTableModel = JsonConvert.SerializeObject(ViewBag.DataTable);
                        ViewBag.IsFileUploaded = true;

                        var models = db.CARS.Select(c => c.Model).Distinct().ToList();
                        ViewBag.CarModelList = new SelectList(models);
                        ViewBag.CarOptionList = new SelectList(db.CARS.ToList(), "ID", "OptionCode");
                    }
                    else
                    {
                        ViewBag.DataTable = db.Database.SqlQuery<GetCarPlanRequirements_Result>("EXEC GetCarPlanRequirements @OptionCode", new SqlParameter("@OptionCode", tempoCar.OptionCode)).ToList();
                        ViewBag.DataTableModel = JsonConvert.SerializeObject(ViewBag.DataTable);
                        ViewBag.IsFileUploaded = true;

                        var models = db.CARS.Select(c => c.Model).Distinct().ToList();
                        ViewBag.CarModelList = new SelectList(models);
                        ViewBag.CarOptionList = new SelectList(db.CARS.ToList(), "ID", "OptionCode");
                    }
                }
                return View("Create");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(ex.Message, ex);
                return View();
            }
        }
        [HttpPost]
        public async Task<ActionResult> Save(string dataTableModel)
        {
            int dayCount = 0;
            double dayPlanAmount;
            DateTime startDate;
            if (!string.IsNullOrEmpty(dataTableModel))
            {
                //await Task.Run(() =>
                //{
                var tableModel = JsonConvert.DeserializeObject<DataTable>(dataTableModel);

                try
                {
                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        foreach (DataRow row in tableModel.Rows)
                        {
                            string PNo = row["PNo"].ToString();
                            string Requirement = row["Requirement"].ToString();
                            string StartDate = row["StartDate"].ToString();
                            string DueDate = row["DueDate"].ToString();
                            DateTime d1 = Convert.ToDateTime(StartDate);
                            DateTime d2 = Convert.ToDateTime(DueDate);

                            PRODUCT product = db.PRODUCTS.Where(p => p.PNo.CompareTo(PNo) == 0 && p.IsDeleted == false).FirstOrDefault();
                            PRODUCTPLAN checkPlan = db.PRODUCTPLANS.Where(pp => pp.ProductID == product.ID &&
                                                    DbFunctions.TruncateTime(pp.StartDate) == DbFunctions.TruncateTime(d1) &&
                                                    DbFunctions.TruncateTime(pp.DueDate) == DbFunctions.TruncateTime(d2))
                                                    .FirstOrDefault();

                            if (checkPlan == null)
                            {
                                PRODUCTPLAN plan = new PRODUCTPLAN();
                                plan.ProductID = product.ID;
                                plan.Amount = Convert.ToDouble(Requirement);
                                plan.StartDate = d1;
                                plan.DueDate = d2;
                                plan.IsDeleted = false;

                                db.PRODUCTPLANS.Add(plan);
                                await db.SaveChangesAsync();

                                var userEmail1 = User.Identity.Name;
                                LogHelper.LogToDatabase(userEmail1, "Maxsulotga reja", $"{PNo} ga {Requirement} rejasini Avtomobilga reja berish oynasi orqali qo'shdi");

                                dayCount = (int)(plan.DueDate - plan.StartDate).TotalDays;
                                dayPlanAmount = Math.Ceiling(plan.Amount / dayCount);
                                startDate = plan.StartDate;

                                for (int i = 0; i < dayCount; i++)
                                {
                                    PRODUCTPLANS_DAILY dailyPlan = new PRODUCTPLANS_DAILY();
                                    //if (!productPlan.IsTwoShiftPlan)
                                    dailyPlan.DayShift = dayPlanAmount;
                                    //else
                                    //{
                                    //    dailyPlan.DayShift = Math.Ceiling(dayPlanAmount / 2);
                                    //    dailyPlan.NightShift = dayPlanAmount - dailyPlan.DayShift;
                                    //}
                                    dailyPlan.PlanID = plan.ID;
                                    dailyPlan.Day = startDate;
                                    startDate = startDate.AddDays(1);
                                    db.PRODUCTPLANS_DAILY.Add(dailyPlan);
                                }
                                await db.SaveChangesAsync();
                            }
                        }
                        return RedirectToAction("Index", "ProductPlan");
                    }
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = ex.Message });
                }
            }
            return RedirectToAction("Create");
        }
    }
}