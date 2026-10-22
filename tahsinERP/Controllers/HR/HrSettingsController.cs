using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.HR;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.HR
{
    /// <summary>
    /// HR sozlamalari (HRM-B15): ish haftasi, bayram kunlari, kunlik me'yor, qo'shimcha soat koeffitsienti, standart soliq foizi.
    /// Ko'rish va o'zgartirish — "HrPayroll" moduli huquqi bo'yicha.
    /// </summary>
    [Authorize]
    public class HrSettingsController : Controller
    {
        private const string Module = "HrPayroll";

        public sealed class HrSettingsViewModel
        {
            public List<int> WorkDays { get; set; } = new List<int>();
            public string Holidays { get; set; }
            public decimal DailyNormHours { get; set; }
            public decimal OvertimeMultiplier { get; set; }
            public decimal DefaultIncomeTaxPercent { get; set; }
        }

        public ActionResult Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, Module))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var s = HrSettingsService.Current;
            ViewBag.CanChange = RoleHelper.IsChangePermitted(User.Identity.Name, Module);
            var now = DateTime.Today;
            ViewBag.ExampleMonth = now.ToString("MM.yyyy");
            ViewBag.ExampleWorkingDays = s.WorkingDaysInMonth(now.Year, now.Month);
            ViewBag.ExampleFundHours = s.FundHoursForMonth(now.Year, now.Month);
            return View(new HrSettingsViewModel
            {
                WorkDays = s.WorkDays.ToList(),
                Holidays = string.Join(Environment.NewLine, s.Holidays),
                DailyNormHours = s.DailyNormHours,
                OvertimeMultiplier = s.OvertimeMultiplier,
                DefaultIncomeTaxPercent = s.DefaultIncomeTaxPercent
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(HrSettingsViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, Module))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var holidays = new List<string>();
            var invalid = new List<string>();
            foreach (var raw in (model.Holidays ?? "").Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var text = raw.Trim();
                if (text.Length == 0) continue;
                DateTime d;
                if (DateTime.TryParseExact(text, new[] { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    holidays.Add(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                else
                    invalid.Add(text);
            }

            if (invalid.Any())
            {
                TempData["Error"] = L.T("Hr_SetInvalidDates", string.Join(", ", invalid));
                return RedirectToAction("Index");
            }

            HrSettingsService.Save(new HrSettings
            {
                WorkDays = model.WorkDays ?? new List<int>(),
                Holidays = holidays,
                DailyNormHours = model.DailyNormHours,
                OvertimeMultiplier = model.OvertimeMultiplier,
                DefaultIncomeTaxPercent = model.DefaultIncomeTaxPercent
            });

            new UserActionLogger(User?.Identity?.Name ?? "System").Log("HrSettings", "Save",
                $"IshKunlari: {string.Join(",", model.WorkDays ?? new List<int>())}, Me'yor: {model.DailyNormHours}, Koef: {model.OvertimeMultiplier}, Soliq: {model.DefaultIncomeTaxPercent}%, Bayramlar: {holidays.Count}");
            TempData["Success"] = L.T("Hr_SetSaved");
            return RedirectToAction("Index");
        }
    }
}
