using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class PartStockController : Controller
    {
        PART_WRHS warehouse = null;
        // GET: PartStock
        public async Task<ActionResult> Index()
        {
            this.warehouse = GetWarehouseOfMRP(User.Identity.Name);
            warehouse = GetWarehouseOfMRP(User.Identity.Name);
            if (warehouse is null)
                ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
            else
                ViewBag.WHName = warehouse.WHName;
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var userID = GetUserID(User.Identity.Name);
                var list = await db.PART_STOCKS
                                        .Include(x => x.PART_WRHS)
                                        .Include(x => x.PART)
                                        .Include(x => x.UNIT)
                                        .Where(x => x.PART_WRHS.MRP == userID)
                                        .ToListAsync();
                return View(list);
            }
        }
        private PART_WRHS GetWarehouseOfMRP(string email)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                USER currentUser = db.USERS.Where(u => u.Email.CompareTo(email) == 0 && u.IsDeleted == false && u.IsActive == true).FirstOrDefault();
                PART_WRHS his_warehouse = db.PART_WRHS.Where(wrhs => wrhs.MRP == currentUser.ID).FirstOrDefault();

                return his_warehouse;
            }
        }
        private int? GetUserID(string email)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                USER currentUser = db.USERS
                                     .Where(u => u.Email.CompareTo(email) == 0 && u.IsDeleted == false && u.IsActive == true)
                                     .FirstOrDefault();

                return currentUser?.ID;
            }
        }
    }
}