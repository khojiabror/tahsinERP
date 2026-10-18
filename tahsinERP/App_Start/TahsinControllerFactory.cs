using System;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.SessionState;
using tahsinERP.Services.Common;

namespace tahsinERP
{
    /// <summary>
    /// CORE-P05: standart holda controller'lar ReadOnly sessiya bilan ishlaydi (Session'ga yozish faqat
    /// login/parol almashtirishda — AccountController [SessionState(Required)]). ReadOnly sessiya eksklyuziv
    /// qulf olmaydi, shuning uchun bir foydalanuvchining parallel AJAX so'rovlari (dashboard, Select2 qidiruv)
    /// ketma-ket navbatga turmaydi. TempData Session o'rniga <see cref="CacheTempDataProvider"/>'da.
    /// </summary>
    public sealed class TahsinControllerFactory : DefaultControllerFactory
    {
        private static readonly ITempDataProvider TempDataProvider = new CacheTempDataProvider();

        public override IController CreateController(RequestContext requestContext, string controllerName)
        {
            var controller = base.CreateController(requestContext, controllerName);
            if (controller is Controller mvc)
                mvc.TempDataProvider = TempDataProvider;
            return controller;
        }

        protected override SessionStateBehavior GetControllerSessionBehavior(RequestContext requestContext, Type controllerType)
        {
            if (controllerType != null && Attribute.IsDefined(controllerType, typeof(SessionStateAttribute), inherit: true))
                return base.GetControllerSessionBehavior(requestContext, controllerType);
            return SessionStateBehavior.ReadOnly;
        }
    }
}
