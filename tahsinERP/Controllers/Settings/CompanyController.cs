using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Filters;
using tahsinERP.Services.Common;

namespace tahsinERP.Controllers.Settings
{
    [Permission]
    public class CompanyController : Controller
    {
        // Olish so'rovi: Company
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
                                string brandError = SaveBranding(db, companyToUpdate.ID);
                                if (brandError != null)
                                {
                                    ModelState.AddModelError("", brandError);
                                    return View(company);
                                }
                                db.Entry(companyToUpdate).State = EntityState.Modified;
                                db.SaveChanges();
                                BrandingService.Invalidate();

                                LogHelper.LogToDatabase(User.Identity.Name, "Korxona ma'lumotlari", $"{company.Name} - korxona ma'lumotlarini tahrirladi");
                                return Redirect("/Home");
                            }
                            catch (Exception ex)
                            {
                                //Log the xatolik (uncomment dex variable name va add a line here ga write a log.
                                ModelState.AddModelError("", ex.Message);
                                return View(company);
                            }
                        }
                    }
                }
            }
            return View(company);
        }

        private const int LogoMaxBytes = 200 * 1024;
        private static readonly string[] LogoTypes = { "image/png", "image/jpeg" };

        /// <summary>Brend rangi va logo (COMPANIES.BrandColor/LogoImage — EDMX'da yo'q, xom SQL). Xato matni yoki null.</summary>
        private string SaveBranding(DBTHSNEntities db, int companyId)
        {
            string color = Request.Form["BrandColor"];
            if (!string.IsNullOrEmpty(color))
            {
                if (!BrandingService.IsValidColor(color))
                    return "Brend rangi #RRGGBB ko'rinishida bo'lishi kerak.";
                // Standart (neytral) rang tanlansa NULL saqlanadi (keyinchalik standart o'zgarsa ham shunga ergashadi)
                db.Database.ExecuteSqlCommand("UPDATE dbo.COMPANIES SET BrandColor = @p0 WHERE ID = @p1",
                    (object)BrandingService.ToStoredValue(color) ?? DBNull.Value, companyId);
            }

            if (Request.Form["RemoveLogo"] == "true")
            {
                db.Database.ExecuteSqlCommand("UPDATE dbo.COMPANIES SET LogoImage = NULL, LogoContentType = NULL WHERE ID = @p0", companyId);
                return null;
            }

            var file = Request.Files["LogoFile"];
            if (file != null && file.ContentLength > 0)
            {
                if (file.ContentLength > LogoMaxBytes)
                    return "Logo 200 KB dan katta.";
                if (Array.IndexOf(LogoTypes, (file.ContentType ?? "").ToLowerInvariant()) < 0)
                    return "Logo PNG yoki JPG bo'lishi kerak.";
                var bytes = new byte[file.ContentLength];
                file.InputStream.Read(bytes, 0, bytes.Length);
                db.Database.ExecuteSqlCommand("UPDATE dbo.COMPANIES SET LogoImage = @p0, LogoContentType = @p1 WHERE ID = @p2",
                    bytes, file.ContentType.ToLowerInvariant(), companyId);
            }
            return null;
        }
    }
}
