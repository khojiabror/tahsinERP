using System;

namespace tahsinERP.Services.Procurement
{
    /// <summary>Shartnoma/invoys formalaridagi umumiy xato matni (avval ikki controller'da bir xil nusxa).</summary>
    public static class ProcurementErrors
    {
        public static string Format(Exception ex)
        {
            var message = $"Xatolik: {ex.Message}";
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                message += $" | Ichki xatolik: {inner.Message}";
            }
            return message;
        }
    }
}
