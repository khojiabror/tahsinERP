using System;
using System.Diagnostics;

namespace tahsinERP.Services.Editions
{
    /// <summary>
    /// Joriy tenant'ning paketi (imzolangan kalitdagi Edition) bo'yicha modul ruxsati.
    /// Holat LicenseService keshidan (5 daqiqa, tenant bo'yicha) — har chaqiruvda DB'ga bormaydi.
    /// </summary>
    public static class EditionGate
    {
        /// <summary>
        /// Joriy paket: faqat imzosi yaroqli v2 kalitdan. v1 kalit, kalit yo'q yoki xato — null (hamma modul ochiq;
        /// litsenziyaning o'zini LicenseCheckAttribute tekshiradi).
        /// </summary>
        public static string CurrentEdition
        {
            get
            {
                try
                {
                    var info = LicenseService.GetCachedStatus().Info;
                    return info != null && info.SignatureValid && info.Version >= 2 ? info.Edition : null;
                }
                catch (Exception ex)
                {
                    Trace.TraceError("[Edition] paketni aniqlab bo'lmadi: " + ex.Message);
                    return null;
                }
            }
        }

        /// <summary>Controller joriy paketda bormi. Core controllerlar uchun litsenziya holatiga murojaat qilinmaydi.</summary>
        public static bool IsControllerAllowed(string controller)
        {
            var module = EditionModules.ModuleOf(controller);
            return module == ProductModule.Core || EditionModules.IsModuleAllowed(CurrentEdition, module);
        }
    }
}
