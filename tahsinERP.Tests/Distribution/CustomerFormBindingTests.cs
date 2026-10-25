using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Web.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Controllers;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>
    /// Mijoz kartasi «Savdo shartlari» — tashrif kunlari katakchalari (name="visitDay", value = 1, 2, 4 ... 64).
    /// Avval action parametri byte[] edi: MVC byte[] ni Base64 matn sifatida bog'laydi (ByteArrayModelBinder) — biror kun
    /// belgilansa FormatException action'gacha otilib, 500 sahifa chiqardi.
    /// </summary>
    [TestClass]
    public class CustomerFormBindingTests
    {
        private static object Bind(Type type, params string[] values)
        {
            var form = new NameValueCollection();
            foreach (var v in values) form.Add("visitDay", v);
            var context = new ModelBindingContext
            {
                ModelName = "visitDay",
                ModelMetadata = ModelMetadataProviders.Current.GetMetadataForType(null, type),
                ValueProvider = new NameValueCollectionValueProvider(form, CultureInfo.InvariantCulture),
            };
            return ModelBinders.Binders.GetBinder(type).BindModel(new ControllerContext(), context);
        }

        [TestMethod]
        public void ByteArrayParameter_FailsOnCheckboxValues_RootCause()
        {
            Assert.ThrowsException<FormatException>(() => Bind(typeof(byte[]), "1", "16"));
        }

        [TestMethod]
        public void IntArrayParameter_BindsCheckboxes_AndMaskIsBuilt()
        {
            var days = (int[])Bind(typeof(int[]), "1", "16", "64");
            CollectionAssert.AreEqual(new[] { 1, 16, 64 }, days);
            Assert.AreEqual((byte)81, DstCustomersController.VisitDaysMask(days));
            Assert.AreEqual((byte)0, DstCustomersController.VisitDaysMask(null));
            Assert.AreEqual((byte)127, DstCustomersController.VisitDaysMask(new[] { 1, 2, 4, 8, 16, 32, 64, 128, -1 })); // haftadan tashqari bitlar tashlanadi
        }
    }
}
