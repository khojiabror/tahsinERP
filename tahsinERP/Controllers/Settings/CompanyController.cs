using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers.Settings
{
    public class CompanyController : Controller
    {
        // GET: Company
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                return View(db.COMPANIES.FirstOrDefault());
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(COMPANy company)
        {
            if (ModelState.IsValid)
            {
                if (company != null)
                {
                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        COMPANy companyToUpdate = db.COMPANIES.Find(company.ID);
                        if (TryUpdateModel(companyToUpdate, "", new string[] { "Name", "Email", "Country", "Telephone", "City", "DUNS", "Address" }))
                        {
                            try
                            {
                                db.Entry(companyToUpdate).State = EntityState.Modified;
                                db.SaveChanges();

                                LogHelper.LogToDatabase(User.Identity.Name, "Korxona ma'lumotlari", $"{company.Name} - korxona ma'lumotlarini tahrirladi");
                                return Redirect("/Home");
                            }
                            catch (Exception ex)
                            {
                                //Log the error (uncomment dex variable name and add a line here to write a log.
                                ModelState.AddModelError("", ex.Message);
                                return View(company);
                            }
                        }
                    }
                }
            }
            return View(company);
        }
    }
}