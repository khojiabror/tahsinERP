using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Mahsulot rasmini URL orqali beradi (MES-P23, dublikat #24): avval rasm (varbinary) har sahifaga — BOM daraxtida
    /// har tugunga — base64 data-URI bo'lib qo'shilardi, sahifalar MB'lab bo'lardi va keshlanmasdi.
    /// Rasm ETag (PRODUCTIMAGES.ID + o'lcham) bilan brauzerda, kichraytirilgan nusxa serverda keshlanadi.
    /// Rasm modulga xos ma'lumot emas — BOM/Norm ko'ruvchilar ham ko'rishi uchun faqat autentifikatsiya talab qilinadi.
    /// </summary>
    [Authorize]
    public class ProductImageController : Controller
    {
        private const int MinWidth = 16;
        private const int MaxWidth = 1600;

        /// <summary>Mahsulot rasmi URL'i. imageId versiya sifatida qo'shiladi (yangi rasm yuklanganda URL o'zgaradi).</summary>
        public static string UrlFor(int productId, int imageId, int? width = null)
        {
            var url = VirtualPathUtility.ToAbsolute("~/ProductImage/Image/" + productId) + "?v=" + imageId;
            return width.HasValue ? url + "&w=" + width.Value : url;
        }

        /// <summary>Mahsulotning joriy (o'chirilmagan, eng so'nggi) rasm ID'si; rasm bo'lmasa null.</summary>
        public static int? CurrentImageId(DBTHSNEntities db, int productId)
        {
            return db.PRODUCTIMAGES.AsNoTracking()
                .Where(pi => pi.ProdID == productId && pi.IsDeleted != true && pi.Image != null)
                .OrderByDescending(pi => pi.ID)
                .Select(pi => (int?)pi.ID)
                .FirstOrDefault();
        }

        // GET: ProductImage/Image/5?w=160
        [HttpGet]
        public ActionResult Image(int id, int? w)
        {
            int? width = w.HasValue ? Math.Max(MinWidth, Math.Min(MaxWidth, w.Value)) : (int?)null;

            using (var db = new DBTHSNEntities())
            {
                var imageId = CurrentImageId(db, id);
                if (!imageId.HasValue)
                    return HttpNotFound();

                var etag = "\"pimg-" + imageId.Value + "-" + (width ?? 0) + "\"";
                Response.Cache.SetCacheability(HttpCacheability.Private);
                Response.Cache.SetMaxAge(TimeSpan.FromDays(7));
                // Cache.SetETag Private keshda sarlavhani chiqarmaydi - to'g'ridan-to'g'ri yoziladi
                Response.AppendHeader("ETag", etag);
                if (string.Equals(Request.Headers["If-None-Match"], etag, StringComparison.Ordinal))
                    return new HttpStatusCodeResult(304);

                var cacheKey = tahsinERP.Configuration.Tenancy.TenantContext.CacheKey("pimg:" + imageId.Value + ":" + (width ?? 0));
                var cached = HttpRuntime.Cache[cacheKey] as Tuple<byte[], string>;
                if (cached == null)
                {
                    var bytes = db.PRODUCTIMAGES.AsNoTracking()
                        .Where(pi => pi.ID == imageId.Value)
                        .Select(pi => pi.Image)
                        .FirstOrDefault();
                    if (bytes == null || bytes.Length == 0)
                        return HttpNotFound();

                    cached = width.HasValue ? Thumbnail(bytes, width.Value) : Tuple.Create(bytes, DetectContentType(bytes));
                    HttpRuntime.Cache.Insert(cacheKey, cached, null, Cache.NoAbsoluteExpiration, TimeSpan.FromMinutes(30));
                }

                return File(cached.Item1, cached.Item2);
            }
        }

        /// <summary>Kengligi <paramref name="width"/> dan katta bo'lsa proporsional kichraytiradi; aks holda asl rasm.</summary>
        private static Tuple<byte[], string> Thumbnail(byte[] bytes, int width)
        {
            try
            {
                using (var input = new MemoryStream(bytes))
                using (var src = System.Drawing.Image.FromStream(input))
                {
                    if (src.Width <= width)
                        return Tuple.Create(bytes, DetectContentType(bytes));

                    var height = Math.Max(1, (int)Math.Round(src.Height * (double)width / src.Width));
                    using (var dst = new Bitmap(width, height))
                    using (var g = Graphics.FromImage(dst))
                    using (var output = new MemoryStream())
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.Clear(Color.White);
                        g.DrawImage(src, 0, 0, width, height);
                        // Fotosurat (JPEG) kichik nusxasi JPEG'da, qolganlari (shaffoflik bo'lishi mumkin) PNG'da
                        if (DetectContentType(bytes) == "image/jpeg")
                        {
                            var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                            using (var prms = new EncoderParameters(1))
                            {
                                prms.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
                                dst.Save(output, jpeg, prms);
                            }
                            return Tuple.Create(output.ToArray(), "image/jpeg");
                        }

                        dst.Save(output, ImageFormat.Png);
                        return Tuple.Create(output.ToArray(), "image/png");
                    }
                }
            }
            catch (ArgumentException)
            {
                // GDI+ o'qiy olmaydigan format (masalan webp) — asl baytlar
                return Tuple.Create(bytes, DetectContentType(bytes));
            }
        }

        private static string DetectContentType(byte[] b)
        {
            if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
            if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
            if (b.Length >= 4 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return "image/gif";
            if (b.Length >= 2 && b[0] == 0x42 && b[1] == 0x4D) return "image/bmp";
            if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
                && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "image/webp";
            return "application/octet-stream";
        }
    }
}
