using System;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Bot;

namespace tahsinERP.Areas.Distribution.Services.Money
{
    /// <summary>
    /// Agent (bot) qabul qila oladigan to'lov usullari va har usulning hisobi (dst.SETTINGS):
    /// AgentPayMethods = "CASH,CARD,CLICK" (yozilmagan bo'lsa — shu), AgentAccount_CARD = FIN_ACCOUNTS.ID …
    /// Naqd — hisobsiz, agent qo'lida (kassaga topshiriladi, kassa — AgentCashAccountId).
    /// </summary>
    public static class AgentPayments
    {
        public static readonly string[] Default = { PaymentMethods.Cash, PaymentMethods.Card, PaymentMethods.Click };

        public static string AccountKey(string method) => DstSetting.AgentAccountPrefix + method;

        public static string[] Allowed(DistributionDbContext db)
        {
            var raw = DstSettings.Get(db, DstSetting.AgentPayMethods);
            if (raw == null) return Default;
            return raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim().ToUpperInvariant()).Where(m => PaymentMethods.All.Contains(m)).Distinct().ToArray();
        }

        public static int? Account(DistributionDbContext db, string method) => DstSettings.Int(db, AccountKey(method));

        /// <summary>Agent to'lovi uchun hisob: naqd — null (qo'lida); boshqa usul — ruxsat etilgan va hisobi sozlangan bo'lishi shart.</summary>
        public static int? ResolveAccount(DistributionDbContext db, string method)
        {
            if (!Allowed(db).Contains(method)) throw new DstValidationException(DstLocalization.T("Payment_MethodNotAllowed"));
            if (PaymentMethods.CanStayWithAgent(method)) return null;
            return Account(db, method) ?? throw new DstValidationException(DstLocalization.T("Payment_MethodNoAccount"));
        }
    }
}
