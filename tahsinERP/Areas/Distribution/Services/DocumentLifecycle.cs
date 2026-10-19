using System;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>Foydalanuvchiga ko'rsatiladigan biznes-qoida xatosi (ichki tafsilotsiz).</summary>
    public class DstValidationException : InvalidOperationException
    {
        public DstValidationException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Hujjat holatining yagona joyi: Draft → Posted → Cancelled.
    /// Ombor/qarz kabi oqibatlarni hujjat servisi bajaradi; bu sinf faqat holat va kim/qachon maydonlarini qo'yadi.
    /// </summary>
    public static class DocumentLifecycle
    {
        public const int MinCancelReasonLength = 3;

        public static void Post(DstDocument doc, int userId, DateTime now)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (doc.Status != DocumentStatus.Draft)
                throw new DstValidationException("Faqat qoralama hujjatni o'tkazish mumkin (" + doc.DocNo + ": " + doc.Status + ").");

            doc.Status = DocumentStatus.Posted;
            doc.PostedBy = userId;
            doc.PostedAt = now;
        }

        public static void Cancel(DstDocument doc, int userId, string reason, DateTime now)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (doc.Status == DocumentStatus.Cancelled)
                throw new DstValidationException("Hujjat allaqachon bekor qilingan (" + doc.DocNo + ").");

            reason = reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < MinCancelReasonLength)
                throw new DstValidationException(DstLocalization.T("Error_CancelReasonRequired"));
            if (reason.Length > 500)
                reason = reason.Substring(0, 500);

            doc.Status = DocumentStatus.Cancelled;
            doc.CancelledBy = userId;
            doc.CancelledAt = now;
            doc.CancelReason = reason;
        }

        /// <summary>O'chirish faqat qoralama uchun (o'tkazilgani — bekor qilinadi).</summary>
        public static bool CanDelete(DstDocument doc) => doc != null && doc.Status == DocumentStatus.Draft;
    }
}
