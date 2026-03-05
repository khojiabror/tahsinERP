using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Akt-sverki (O'zaro hisob-kitoblar akti) - ta'minotchi, xaridor va yuk tashuvchi uchun
    /// Barcha kontent o'zbekcha
    /// </summary>
    public class ReconciliationController : Controller
    {
        public async Task<ActionResult> Supplier(int? id, DateTime? asOfDate)
        {
            try
            {
                if (User?.Identity?.Name == null || !RoleHelper.IsViewPermitted(User.Identity.Name, "Supplier", "Details"))
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }
                if (id == null)
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

                asOfDate = asOfDate ?? DateTime.Today;

                using (var db = new DBTHSNEntities())
                {
                    var supplier = await db.SUPPLIERS.FindAsync(id.Value);
                    if (supplier == null || supplier.IsDeleted == true)
                        return HttpNotFound();

                    var companyName = (db.COMPANIES != null && db.COMPANIES.Any())
                        ? db.COMPANIES.FirstOrDefault()?.Name
                        : null;
                    var vm = ReconciliationService.BuildSupplierReconciliation(db, id.Value, asOfDate.Value, companyName, supplier.Name ?? "");
                    return View("AktSverki", vm);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User?.Identity?.Name ?? "System", "Reconciliation", "Supplier", $"Xatolik (Supplier {id}): {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public async Task<ActionResult> Customer(int? id, DateTime? asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Customer", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            asOfDate = asOfDate ?? DateTime.Today;

            using (var db = new DBTHSNEntities())
            {
                var customer = db.CUSTOMERS.Find(id.Value);
                if (customer == null || customer.IsDeleted == true)
                    return HttpNotFound();

                var companyName = db.COMPANIES.Any() ? db.COMPANIES.FirstOrDefault()?.Name : null;
                var vm = ReconciliationService.BuildCustomerReconciliation(db, id.Value, asOfDate.Value, companyName, customer.Name ?? "");
                return View("AktSverki", vm);
            }
        }

        public async Task<ActionResult> Forwarder(int? id, DateTime? asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Forwarder", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            asOfDate = asOfDate ?? DateTime.Today;

            using (var db = new DBTHSNEntities())
            {
                var forwarder = await db.FORWARDERS.FindAsync(id.Value);
                if (forwarder == null || forwarder.IsDeleted == true)
                    return HttpNotFound();

                var companyName = db.COMPANIES.Any() ? db.COMPANIES.FirstOrDefault()?.Name : null;
                var vm = ReconciliationService.BuildForwarderReconciliation(db, id.Value, asOfDate.Value, companyName, forwarder.ForwarderName ?? "");
                return View("AktSverki", vm);
            }
        }
    }
}
