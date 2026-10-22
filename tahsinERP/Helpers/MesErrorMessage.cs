using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.I18n;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Istisno → foydalanuvchi xabari (MES/Engineering, dublikat #10): avval ProductPlan, ProductProduction,
    /// QualityInspection va Norm controllerlarida "Xatolik: ... | Ichki xatolik: ..." zanjiri alohida yozilgan edi.
    /// Ombor (DLOC) xatosi va biznes qoidasi xatolari (InvalidOperationException, jumladan InsufficientStockException)
    /// o'z xabari bilan ko'rsatiladi.
    /// </summary>
    public static class MesErrorMessage
    {
        public static string From(Exception ex)
        {
            if (ex == null)
                return L.T("Error_Generic");

            if (ex is WarehouseDlocException || (ex is InvalidOperationException && ex.InnerException == null))
                return ex.Message;

            var messages = new List<string>();
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                    messages.Add(current.Message.Trim());
            }

            return L.T("Mes_Prod_ErrorPrefix", string.Join(" | " + L.T("Mes_Prod_InnerError") + ": ", messages.Distinct()));
        }
    }
}
