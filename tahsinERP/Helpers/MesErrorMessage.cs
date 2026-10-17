using System;
using System.Collections.Generic;
using System.Linq;
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
                return "Xatolik yuz berdi.";

            if (ex is WarehouseDlocException || (ex is InvalidOperationException && ex.InnerException == null))
                return ex.Message;

            var messages = new List<string>();
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                    messages.Add(current.Message.Trim());
            }

            return "Xatolik: " + string.Join(" | Ichki xatolik: ", messages.Distinct());
        }
    }
}
