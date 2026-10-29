using System;
using tahsinERP.Services.Editions;

namespace tahsinERP.Areas.Distribution.Controllers.Api
{
    /// <summary>
    /// DstApi action'i qaysi modulni talab qiladi (controller o'zi — Commerce). Dala amallari (kun, tashrif, GPS, marshrut, reys,
    /// yetkazish) — <see cref="ProductModule.Distribution"/>: SAVDO paketida bot katalog/buyurtma/ombor/qarz bilan ishlaydi,
    /// dala tugmalari 403 "Edition" oladi. Tekshiruv — <see cref="DstApiAuthAttribute"/> ichida (imzo va litsenziyadan keyin).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class DstApiModuleAttribute : Attribute
    {
        public DstApiModuleAttribute(ProductModule module)
        {
            Module = module;
        }

        public ProductModule Module { get; }
    }
}
