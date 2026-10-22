using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Services.Common;

namespace tahsinERP.Tests.Helpers
{
    [TestClass]
    public class BrandingServiceTests
    {
        [TestMethod]
        public void LegacyGreen_ReproducesPreviousIpackLook()
        {
            var t = BrandingService.BuildTokens("#198754");

            Assert.AreEqual("#198754", t.Accent);
            Assert.AreEqual("#20c997", t.Accent2);
            Assert.AreEqual("#157347", t.Hover);
            Assert.AreEqual("#146c43", t.Active);
            Assert.AreEqual("rgba(25, 135, 84, 0.15)", t.Soft);
            Assert.AreEqual("#33cc00", t.TableLink);
            Assert.AreEqual("#2db300", t.TableLinkHover);
            Assert.AreEqual("#146c43", t.TextVisited);
        }

        [TestMethod]
        public void LegacyGreen_IsCaseInsensitive()
        {
            Assert.AreEqual("#20c997", BrandingService.BuildTokens("#198754".ToUpperInvariant()).Accent2);
        }

        [TestMethod]
        public void InvalidColor_FallsBackToNeutralDefault()
        {
            Assert.AreEqual(BrandingService.DefaultColor, BrandingService.BuildTokens("red").Accent);
            Assert.AreEqual(BrandingService.DefaultColor, BrandingService.BuildTokens(null).Accent);
        }

        [TestMethod]
        public void DefaultIsNeutral_NotGreen()
        {
            Assert.AreNotEqual(BrandingService.LegacyGreen, BrandingService.DefaultColor);
        }

        [DataTestMethod]
        [DataRow("#3b5bfd")]
        [DataRow("#ffd400")] // och sariq: oq fonda matn sifatida o'qilmaydi
        [DataRow("#7c3aed")]
        [DataRow("#0a0a0a")] // deyarli qora: tungi rejimda o'qilmaydi
        [DataRow("#00ffcc")]
        public void TextTokens_MeetContrastOnLightAndDarkSurfaces(string color)
        {
            var t = BrandingService.BuildTokens(color);

            Assert.IsTrue(BrandingService.Contrast(t.Text, "#ffffff") >= 4.5, "kunduzgi: " + t.Text);
            Assert.IsTrue(BrandingService.Contrast(t.DarkText, "#272e48") >= 4.5, "tungi: " + t.DarkText);
        }

        [DataTestMethod]
        [DataRow("#3b5bfd", "#fff")]
        [DataRow("#ffd400", "#1b2333")]
        [DataRow("#c2253b", "#fff")]
        public void ContrastColor_PicksReadableTextOnAccent(string color, string expected)
        {
            Assert.AreEqual(expected, BrandingService.BuildTokens(color).Contrast);
        }

        [TestMethod]
        public void ToStoredValue_DefaultIsNull_OtherColorsUppercase()
        {
            Assert.IsNull(BrandingService.ToStoredValue(BrandingService.DefaultColor));
            Assert.IsNull(BrandingService.ToStoredValue(BrandingService.DefaultColor.ToUpperInvariant()));
            Assert.AreEqual("#198754", BrandingService.ToStoredValue("#198754").ToLowerInvariant());
            Assert.AreEqual("#ABCDEF", BrandingService.ToStoredValue("#abcdef"));
            Assert.IsNull(BrandingService.ToStoredValue("#12"));
        }

        [TestMethod]
        public void RenderCssVariables_HasLightAndDarkBlocks()
        {
            string css = BrandingService.RenderCssVariables(BrandingService.BuildTokens("#198754"));

            StringAssert.StartsWith(css, ":root{--accent:#198754;");
            StringAssert.Contains(css, ":root[data-theme=\"dark\"]{");
            StringAssert.Contains(css, "--accent-table-link:#33cc00;");
            Assert.IsFalse(css.Contains("<"), "style tegi ichida xavfsiz bo'lishi kerak");
        }
    }
}
