using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    [AllowAnonymous]
    public class CheckForPOController : Controller
    {
        [AllowAnonymous]
        public ActionResult Index()
        {
            //using (DBTHSNEntities db = new DBTHSNEntities())
            //{
            //    var itemList = db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList();
            //}
            return View();
        }
        [HttpPost]
        public ActionResult Index(string ordercode)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PURCHASING_ORDERS po = db.PURCHASING_ORDERS.Where(p => p.OrderCode.CompareTo(ordercode) == 0 && p.IsDeleted == false).FirstOrDefault();
                if (po != null)
                {
                    return RedirectToAction("Tracing", new { orderID = po.ID});
                }
                else
                {
                    ViewBag.Message = "Bunday buyurtma ushbu tizimda topilmadi, buyurtmangiz yoki hali to'liq tasdiqlanmagan yoki ushbu tizimga kiritilmagan!";
                    return View();
                }
            }
        }
        public ActionResult Tracing(int orderID)
        {
            var model = new OrderTracingViewModel();
            model.IsOrderCompleted = true;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var existContract = db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false && x.OrderID == orderID).FirstOrDefault();
                if (existContract != null)
                {
                    model.IsContractConfirmed = true;
                    
                    var existPayment = db.PURCHASING_CONTRACT_PAYMENTS.Where(x => x.ContractID == existContract.ID && x.IsDeleted == false).FirstOrDefault();
                    if (existPayment != null)
                        model.IsPaymentMade = true;

                    var existWrhsIncome = db.PURCHASING_WRHS_INCOMES.Where(x => x.IsDeleted == false && x.ContractID == existContract.ID).FirstOrDefault();
                    if(existWrhsIncome != null) 
                        model.HasArrivedInWarehouse = true;
                }
            }

            return View(model);
        }
    }
}