using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Site.Data;

namespace tahsinERP.Areas.Site.Services
{
    /// <summary>
    /// Sayt rasmlari: yuklangan fayl tekshiriladi, EXIF burilishi to'g'rilanadi, katta tomoni 2000 px gacha kichraytiriladi
    /// (JPEG 85% yoki shaffoflik bo'lsa PNG) va site.MEDIA ga yoziladi. Bir xil rasm (SHA-256) qayta yozilmaydi.
    /// Saytdagi o'lchamlar (WebP, 200/600/1200) ochiq sayt servisi tomonida tayyorlanadi.
    /// </summary>
    public static class SiteMediaService
    {
        public const int MaxUploadBytes = 10 * 1024 * 1024;
        public const int MaxSide = 2000;

        public sealed class Prepared
        {
            public byte[] Content { get; set; }
            public string ContentType { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public string Hash { get; set; }
        }

        /// <summary>Rasmni tekshirish va saqlashga tayyorlash (bazasiz — testlanadi).</summary>
        public static Prepared Prepare(byte[] input)
        {
            if (input == null || input.Length == 0 || input.Length > MaxUploadBytes)
                throw new DstValidationException(DstLocalization.T("Site_ImageTooBig"));
            Image source;
            try
            {
                source = Image.FromStream(new MemoryStream(input), true, true);
            }
            catch (ArgumentException)
            {
                throw new DstValidationException(DstLocalization.T("Site_ImageInvalid"));
            }
            using (source)
            {
                bool isPng = source.RawFormat.Equals(ImageFormat.Png) || source.RawFormat.Equals(ImageFormat.Gif);
                if (!isPng && !source.RawFormat.Equals(ImageFormat.Jpeg) && !source.RawFormat.Equals(ImageFormat.Bmp))
                    throw new DstValidationException(DstLocalization.T("Site_ImageInvalid"));
                ApplyExifOrientation(source);

                double scale = Math.Min(1.0, (double)MaxSide / Math.Max(source.Width, source.Height));
                int w = Math.Max(1, (int)Math.Round(source.Width * scale)), h = Math.Max(1, (int)Math.Round(source.Height * scale));
                bool keepPng = isPng && Image.IsAlphaPixelFormat(source.PixelFormat);

                using (var bmp = new Bitmap(w, h, keepPng ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        if (!keepPng) g.Clear(Color.White);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        using (var attrs = new ImageAttributes())
                        {
                            attrs.SetWrapMode(WrapMode.TileFlipXY);   // chetlarda xira chiziq bo'lmasin
                            g.DrawImage(source, new Rectangle(0, 0, w, h), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
                        }
                    }
                    byte[] output;
                    using (var ms = new MemoryStream())
                    {
                        if (keepPng)
                            bmp.Save(ms, ImageFormat.Png);
                        else
                        {
                            var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                            using (var p = new EncoderParameters(1))
                            {
                                p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
                                bmp.Save(ms, jpeg, p);
                            }
                        }
                        output = ms.ToArray();
                    }
                    return new Prepared
                    {
                        Content = output, ContentType = keepPng ? "image/png" : "image/jpeg", Width = w, Height = h, Hash = Sha256Hex(output)
                    };
                }
            }
        }

        public static SiteMedia Save(DistributionDbContext db, byte[] input, int? userId, DateTime now)
        {
            var p = Prepare(input);
            var existing = db.SiteMedia.FirstOrDefault(m => m.Hash == p.Hash);
            if (existing != null) return existing;
            var media = new SiteMedia
            {
                Content = p.Content, ContentType = p.ContentType, Width = p.Width, Height = p.Height, Bytes = p.Content.Length,
                Hash = p.Hash, CreatedBy = userId, CreatedAt = now
            };
            db.SiteMedia.Add(media);
            db.SaveChanges();
            return media;
        }

        /// <summary>dbo.PRODUCTIMAGES (eski, bazadagi rasmlar) → tovar galereyasi. Qo'shilganlar soni.</summary>
        public static int ImportProductImages(DistributionDbContext db, int productId, int? userId, DateTime now)
        {
            var images = db.Database.SqlQuery<byte[]>(
                "SELECT Image FROM dbo.PRODUCTIMAGES WHERE ProdID = @p0 AND ISNULL(IsDeleted, 0) = 0 AND Image IS NOT NULL ORDER BY ID", productId).ToList();
            int added = 0;
            int order = db.SiteProductMedia.Where(m => m.ProductID == productId).Select(m => (int?)m.SortOrder).Max() ?? -1;
            foreach (var bytes in images)
            {
                SiteMedia media;
                try
                {
                    media = Save(db, bytes, userId, now);
                }
                catch (DstValidationException)
                {
                    continue;   // buzilgan yoki noma'lum formatdagi eski rasm — o'tkazib yuboriladi
                }
                if (db.SiteProductMedia.Any(m => m.ProductID == productId && m.MediaID == media.ID)) continue;
                db.SiteProductMedia.Add(new SiteProductMedia { ProductID = productId, MediaID = media.ID, SortOrder = ++order });
                db.SaveChanges();
                added++;
            }
            return added;
        }

        /// <summary>Hech qayerda ishlatilmayotgan rasmlarni o'chirish (galereyadan olib tashlangandan keyin).</summary>
        public static void DeleteIfUnused(DistributionDbContext db, int mediaId)
        {
            bool used = db.SiteProductMedia.Any(m => m.MediaID == mediaId) || db.SiteBanners.Any(b => b.MediaID == mediaId)
                        || db.SiteCategories.Any(c => c.MediaID == mediaId)
                        || db.SiteSettings.Any(s => s.Key == SiteSettingKeys.LogoMediaId && s.Value == mediaId.ToString());
            if (used) return;
            var media = db.SiteMedia.Find(mediaId);
            if (media != null) db.SiteMedia.Remove(media);
        }

        public static string Sha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2")));
        }

        private static void ApplyExifOrientation(Image img)
        {
            const int OrientationId = 0x0112;
            if (!img.PropertyIdList.Contains(OrientationId)) return;
            var flip = RotateFlipType.RotateNoneFlipNone;
            switch (img.GetPropertyItem(OrientationId).Value[0])
            {
                case 2: flip = RotateFlipType.RotateNoneFlipX; break;
                case 3: flip = RotateFlipType.Rotate180FlipNone; break;
                case 4: flip = RotateFlipType.Rotate180FlipX; break;
                case 5: flip = RotateFlipType.Rotate90FlipX; break;
                case 6: flip = RotateFlipType.Rotate90FlipNone; break;
                case 7: flip = RotateFlipType.Rotate270FlipX; break;
                case 8: flip = RotateFlipType.Rotate270FlipNone; break;
            }
            if (flip != RotateFlipType.RotateNoneFlipNone) img.RotateFlip(flip);
            img.RemovePropertyItem(OrientationId);
        }
    }
}
