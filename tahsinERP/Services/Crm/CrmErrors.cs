using System;
using System.Data.Entity.Validation;
using System.Linq;

namespace tahsinERP.Services.Crm
{
    /// <summary>
    /// Foydalanuvchiga ko'rsatiladigan xato matni. BaseController.InnermostMessage EF validatsiyasida faqat
    /// "Validation failed for one or more entities" beradi — qaysi maydon ekani ko'rinmasdi (UAT T-CRM-11).
    /// </summary>
    public static class CrmErrors
    {
        public static string Describe(Exception ex)
        {
            var validation = ex as DbEntityValidationException ?? ex?.GetBaseException() as DbEntityValidationException;
            if (validation != null)
            {
                var fields = validation.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => e.PropertyName + ": " + e.ErrorMessage)
                    .Distinct()
                    .ToList();
                return "Majburiy maydonlar to'ldirilmagan yoki noto'g'ri — " + string.Join("; ", fields);
            }

            return ex?.GetBaseException().Message ?? "";
        }
    }
}
