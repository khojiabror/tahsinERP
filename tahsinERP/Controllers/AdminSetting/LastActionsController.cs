using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class LastActionsController : Controller
    {
        // GET: LastActions
        public async Task<ActionResult> Index(string module, int? userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            // Agar startDate yoki endDate null bo'lsa, default qiymatlarni beramiz
            startDate = startDate ?? DateTime.Now.AddDays(-30); // So'nggi 30 kunlik ma'lumot
            endDate = endDate ?? DateTime.Now; // Bugungi sana

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (userId != null)
                {
                    if (!string.IsNullOrEmpty(module))
                    {
                        List<USERLOG> logs = await db.USERLOGS.Where(x => x.UserID == userId
                                        && x.ControllerName.CompareTo(module) == 0
                                        && x.DateTime >= startDate
                                        && x.DateTime <= endDate)
                                        .ToListAsync();

                        ViewBag.Users = new SelectList(await db.USERS.Where(u => u.IsDeleted == false && u.IsActive == true).ToListAsync(), "ID", "Uname", userId);
                        ViewBag.StartDate = startDate.Value.ToString("dd-MM-yyyy");
                        ViewBag.EndDate = endDate.Value.ToString("dd-MM-yyyy");
                        ViewBag.Modules = new SelectList(await db.USERLOGS.Select(ul => ul.ControllerName).Distinct().ToListAsync());

                        return View(logs);
                    }
                    else
                    {
                        List<USERLOG> logs = await db.USERLOGS.Where(x => x.UserID == userId
                                        && x.DateTime >= startDate
                                        && x.DateTime <= endDate)
                                        .ToListAsync();

                        ViewBag.Users = new SelectList(await db.USERS.Where(u => u.IsDeleted == false && u.IsActive == true).ToListAsync(), "ID", "Uname", userId);
                        ViewBag.StartDate = startDate.Value.ToString("dd-MM-yyyy");
                        ViewBag.EndDate = endDate.Value.ToString("dd-MM-yyyy");
                        ViewBag.Modules = new SelectList(await db.USERLOGS.Select(ul => ul.ControllerName).Distinct().ToListAsync());

                        return View(logs);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(module))
                    {
                        List<USERLOG> logs = await db.USERLOGS.Where(x => x.ControllerName.CompareTo(module) == 0 && x.DateTime >= startDate && x.DateTime <= endDate).ToListAsync();

                        ViewBag.Users = new SelectList(await db.USERS.Where(u => u.IsDeleted == false && u.IsActive == true).ToListAsync(), "ID", "Uname", userId);
                        ViewBag.StartDate = startDate.Value.ToString("dd-MM-yyyy");
                        ViewBag.EndDate = endDate.Value.ToString("dd-MM-yyyy");
                        ViewBag.Modules = new SelectList(await db.USERLOGS.Select(ul => ul.ControllerName).Distinct().ToListAsync());

                        return View(logs);
                    }
                    else
                    {
                        List<USERLOG> logs = await db.USERLOGS.Where(x => x.DateTime >= startDate && x.DateTime <= endDate).ToListAsync();

                        ViewBag.Users = new SelectList(await db.USERS.Where(u => u.IsDeleted == false && u.IsActive == true).ToListAsync(), "ID", "Uname", userId);
                        ViewBag.StartDate = startDate.Value.ToString("dd-MM-yyyy");
                        ViewBag.EndDate = endDate.Value.ToString("dd-MM-yyyy");
                        ViewBag.Modules = new SelectList(await db.USERLOGS.Select(ul => ul.ControllerName).Distinct().ToListAsync());

                        return View(logs);
                    }
                }
            }
        }
    }
}
