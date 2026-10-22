using System;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Linq;
using System.Web.Mvc;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Saqlashdagi xatoni forma ustida ko'rsatish (SRM-D6). Avval POrdersController'da
    /// AppendDatabaseErrorsToModelState va boshqa controller'larda qo'lda yozilgan
    /// "Xatolik: ... | Ichki xatolik: ..." sikllari edi.
    /// </summary>
    public static class ModelStateExtensions
    {
        public static void AddDbErrors(this ModelStateDictionary modelState, Exception ex)
        {
            if (ex == null)
                return;

            var dbVal = Find<DbEntityValidationException>(ex);
            if (dbVal != null)
            {
                foreach (var eve in dbVal.EntityValidationErrors)
                {
                    string entityLabel;
                    try { entityLabel = eve.Entry?.Entity?.GetType()?.Name ?? "Entity"; }
                    catch { entityLabel = "Entity"; }

                    if (eve.ValidationErrors == null || !eve.ValidationErrors.Any())
                    {
                        modelState.AddModelError("", tahsinERP.I18n.L.T("Core_ValidationErrorNoDetails", entityLabel));
                        continue;
                    }

                    foreach (var ve in eve.ValidationErrors)
                    {
                        var prop = string.IsNullOrEmpty(ve.PropertyName) ? tahsinERP.I18n.L.T("Core_FieldParen") : ve.PropertyName;
                        modelState.AddModelError("", $"{entityLabel} — {prop}: {ve.ErrorMessage}");
                    }
                }
                return;
            }

            var dbUp = Find<DbUpdateException>(ex);
            if (dbUp != null)
            {
                modelState.AddModelError("", tahsinERP.I18n.L.T("Core_DbWriteError", dbUp.Message));
                for (var inner = dbUp.InnerException; inner != null; inner = inner.InnerException)
                    modelState.AddModelError("", tahsinERP.I18n.L.T("Core_InnerError", inner.Message));
                return;
            }

            modelState.AddModelError("", ErrorChain(ex));
        }

        /// <summary>"Xatolik: ... | Ichki xatolik: ..." — butun InnerException zanjiri bitta qatorda.</summary>
        public static string ErrorChain(Exception ex)
        {
            var msg = tahsinERP.I18n.L.T("Core_ErrorShort", ex.Message);
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                msg += " | " + tahsinERP.I18n.L.T("Core_InnerErrorFull", inner.Message);
            return msg;
        }

        private static T Find<T>(Exception ex) where T : Exception
        {
            for (var cur = ex; cur != null; cur = cur.InnerException)
                if (cur is T match)
                    return match;
            return null;
        }
    }
}
