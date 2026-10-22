using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;

namespace tahsinERP.Controllers
{
    [Permission]
    public class CustomerController : BaseController
    {
        // Olish so'rovi: Customer
        public async Task<ActionResult> Index(string searchTerm, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = BuildIndexQuery(db, searchTerm, sortOrder);

                // Server tomonida sahifalash (CRM-P05, umumiy blok — D16)
                var customers = await query.ToPageAsync(page, pageSize, ViewData);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.SortOrder = sortOrder;

                return View(customers);
            }
        }

        /// <summary>Qidiruvga mos barcha mijozlar Excel'da.</summary>
        public ActionResult ExportExcel(string searchTerm, string sortOrder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var rows = BuildIndexQuery(db, searchTerm, sortOrder).ToList();
                var bytes = ExcelExport.Create(L.T("Menu_Customers"), rows,
                    ExcelExport.Col<CUSTOMER>(L.T("Crm_Customer"), c => c.Name),
                    ExcelExport.Col<CUSTOMER>(L.T("Crm_CustEmail"), c => c.Email),
                    ExcelExport.Col<CUSTOMER>("DUNS", c => c.DUNS),
                    ExcelExport.Col<CUSTOMER>(L.T("Type"), c => c.Type),
                    ExcelExport.Col<CUSTOMER>(L.T("Crm_CustCountry"), c => c.Country),
                    ExcelExport.Col<CUSTOMER>(L.T("Crm_CustCity"), c => c.City),
                    ExcelExport.Col<CUSTOMER>(L.T("Address"), c => c.Address),
                    ExcelExport.Col<CUSTOMER>(L.T("Phone"), c => c.Telephone),
                    ExcelExport.Col<CUSTOMER>(L.T("Crm_CustContactPerson"), c => c.ContactPersonName),
                    ExcelExport.Col<CUSTOMER>(L.T("Crm_CustDirector"), c => c.DirectorName));
                return File(bytes, ExcelExport.ContentType, $"{L.T("Menu_Customers")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        private static IQueryable<CUSTOMER> BuildIndexQuery(DBTHSNEntities db, string searchTerm, string sortOrder)
        {
            var query = db.CUSTOMERS.AsNoTracking().Where(x => x.IsDeleted == false);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c =>
                    c.Name.Contains(searchTerm) ||
                    (c.Email != null && c.Email.Contains(searchTerm)) ||
                    (c.DUNS != null && c.DUNS.Contains(searchTerm)) ||
                    (c.Type != null && c.Type.Contains(searchTerm)));
            }

            switch (sortOrder)
            {
                case "name_desc":
                    return query.OrderByDescending(c => c.Name);
                case "email":
                    return query.OrderBy(c => c.Email);
                case "email_desc":
                    return query.OrderByDescending(c => c.Email);
                default:
                    return query.OrderBy(c => c.Name);
            }
        }


        public ActionResult Details(int? Id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {

                if (Id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                var customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == Id);
                if (customer == null)
                {
                    return HttpNotFound();
                }

                // Faqat sonlar kerak — to'liq kolleksiyalar yuklanmaydi; bekor qilingan invoyslar sanalmaydi (CRM-P09)
                ViewBag.OrdersCount = db.S_ORDERS.Count(o => o.CustomerID == Id && (o.IsDeleted == false || o.IsDeleted == null));
                ViewBag.ContractsCount = db.S_CONTRACTS.Count(c => c.CustomerID == Id && c.IsDeleted == false);
                ViewBag.InvoicesCount = db.SALES_INVOICES.Active().Count(i => i.CustomerID == Id);

                return View(customer);
            }
        }


        public ActionResult Create()
        {
            ViewBag.Customer = ConfigurationManager.AppSettings["Customer"]?.Split(',').ToList() ?? new List<string>();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Name, Address,DUNS, Type, Country, City, Address, Telephone, Email, ContactPersonName, DirectorName, IsDeleted")] CUSTOMER customer)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        customer.IsDeleted = false;
                        db.CUSTOMERS.Add(customer);
                        db.SaveChanges();

                        var userEmail = CurrentUserName;
                        LogHelper.LogToDatabase(userEmail, "CustomerController", $"{customer.Name} Mijozni yaratdi");

                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", L.T("Crm_ErrorPrefix", InnermostMessage(ex)));
                }

                ViewBag.Customer = ConfigurationManager.AppSettings["Customer"]?.Split(',').ToList() ?? new List<string>();
                return View(customer);
            }
        }

        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                CUSTOMER customer = db.CUSTOMERS.Find(id);
                if (customer == null)
                {
                    return HttpNotFound();
                }

                ViewBag.Customer = ConfigurationManager.AppSettings["Customer"]?.Split(',').ToList() ?? new List<string>();
                return View(customer);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ID, Name, Address, DUNS, Type, Country, City, Telephone, Email, ContactPersonName, DirectorName, IsDeleted")] CUSTOMER customer)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    customer.IsDeleted = false;
                    db.Entry(customer).State = EntityState.Modified;
                    db.SaveChanges();

                    var userEmail = CurrentUserName;
                    LogHelper.LogToDatabase(userEmail, "CustomerController", $"{customer.Name} Mijoz ni tahrirladi");

                    return RedirectToAction("Index");
                }
            }

            ViewBag.Customer = ConfigurationManager.AppSettings["Customer"]?.Split(',').ToList() ?? new List<string>();
            return View(customer);
        }
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                CUSTOMER customer = db.CUSTOMERS.Find(id);
                if (customer == null)
                {
                    return HttpNotFound();
                }

                return View(customer);
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                CUSTOMER customer = db.CUSTOMERS.Find(id);
                if (customer == null)
                {
                    // Avval NullReferenceException (CRM-S04)
                    return HttpNotFound();
                }

                // Soft delete; tarixiy hujjatlardagi bog'lanish saqlanadi (CORE-B01 siyosati)
                customer.IsDeleted = true;
                db.SaveChanges();

                LogHelper.LogToDatabase(CurrentUserName, "CustomerController", $"{customer.Name} Mijozni o'chirdi");

                return RedirectToAction("Index");
            }
        }


    }
}
