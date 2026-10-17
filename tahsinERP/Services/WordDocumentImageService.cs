using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace tahsinERP.Services
{
    /// <summary>
    /// Word hujjatidan rasmlarni ajratib olish va qayta ishlash uchun servis
    /// </summary>
    public class WordDocumentImageService
    {
        /// <summary>
        /// Word hujjatidan barcha rasmlarni ajratib olish
        /// </summary>
        /// <param name="docxBytes">Word hujjatining byte array</param>
        /// <returns>Rasmlar ro'yxati (fileName, imageBytes, mimeType)</returns>
        public static List<WordImage> ExtractImagesFromWord(byte[] docxBytes)
        {
            var images = new List<WordImage>();

            try
            {
                using (var stream = new MemoryStream(docxBytes))
                using (var wordDocument = WordprocessingDocument.Open(stream, false))
                {
                    if (wordDocument?.MainDocumentPart == null)
                        return images;

                    var mainPart = wordDocument.MainDocumentPart;
                    var imageParts = mainPart.ImageParts.ToList();

                    foreach (var imagePart in imageParts)
                    {
                        try
                        {
                            var imageBytes = new byte[0];
                            using (var imageStream = imagePart.GetStream())
                            using (var memoryStream = new MemoryStream())
                            {
                                imageStream.CopyTo(memoryStream);
                                imageBytes = memoryStream.ToArray();
                            }

                            var contentType = imagePart.ContentType;
                            var relationshipId = mainPart.GetIdOfPart(imagePart);
                            var fileName = GetImageFileName(relationshipId, contentType);
                            var mimeType = GetMimeType(contentType);

                            images.Add(new WordImage
                            {
                                FileName = fileName,
                                ImageBytes = imageBytes,
                                MimeType = mimeType,
                                ContentType = contentType
                            });
                        }
                        catch (Exception ex)
                        {
                            // Xatolikni log qilish, lekin boshqa rasmlarni o'qishda davom etish
                            System.Diagnostics.Debug.WriteLine($"Rasm o'qishda xatolik: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Word hujjatini ochishda xatolik: {ex.Message}");
            }

            return images;
        }

        /// <summary>
        /// Rasmlar ro'yxati (faqat metadata, baytlar o'qilmaydi). Indeks <see cref="GetImage"/> bilan bir xil tartibda.
        /// Rasmlar endi DOC_FILES'ga alohida saqlanmaydi — ular .docx ichida bor, har versiyada takror saqlanardi (DOC-P08).
        /// </summary>
        public static List<WordImageInfo> ListImages(byte[] docxBytes)
        {
            var result = new List<WordImageInfo>();
            try
            {
                using (var stream = new MemoryStream(docxBytes))
                using (var wordDocument = WordprocessingDocument.Open(stream, false))
                {
                    var mainPart = wordDocument?.MainDocumentPart;
                    if (mainPart == null)
                        return result;

                    int index = 0;
                    foreach (var imagePart in mainPart.ImageParts)
                    {
                        long size;
                        using (var s = imagePart.GetStream())
                            size = s.Length;
                        result.Add(new WordImageInfo
                        {
                            Index = index++,
                            FileName = GetImageFileName(mainPart.GetIdOfPart(imagePart), imagePart.ContentType),
                            MimeType = GetMimeType(imagePart.ContentType),
                            SizeBytes = size
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("Word rasmlarini o'qib bo'lmadi: {0}", ex.Message);
            }
            return result;
        }

        /// <summary>Hujjatdagi <paramref name="index"/>-rasm; topilmasa null.</summary>
        public static WordImage GetImage(byte[] docxBytes, int index)
        {
            var images = ExtractImagesFromWord(docxBytes);
            return index >= 0 && index < images.Count ? images[index] : null;
        }

        private static string GetImageFileName(string relationshipId, string contentType)
        {
            // Relationship ID dan fayl nomini olish
            var fileName = $"image_{relationshipId}";

            // Content type dan fayl kengaytmasini aniqlash
            var extension = GetFileExtension(GetMimeType(contentType));
            return $"{fileName}.{extension}";
        }

        private static string GetMimeType(string contentType)
        {
            // Content type dan MIME type ni olish
            var mimeTypeMap = new Dictionary<string, string>
            {
                { "image/png", "image/png" },
                { "image/jpeg", "image/jpeg" },
                { "image/jpg", "image/jpeg" },
                { "image/gif", "image/gif" },
                { "image/bmp", "image/bmp" },
                { "image/x-emf", "image/x-emf" },
                { "image/x-wmf", "image/x-wmf" }
            };

            if (mimeTypeMap.ContainsKey(contentType))
                return mimeTypeMap[contentType];

            // Default
            return contentType ?? "image/png";
        }

        private static string GetFileExtension(string mimeType)
        {
            var extensionMap = new Dictionary<string, string>
            {
                { "image/png", "png" },
                { "image/jpeg", "jpg" },
                { "image/jpg", "jpg" },
                { "image/gif", "gif" },
                { "image/bmp", "bmp" },
                { "image/x-emf", "emf" },
                { "image/x-wmf", "wmf" }
            };

            if (extensionMap.ContainsKey(mimeType))
                return extensionMap[mimeType];

            return "png"; // Default
        }
    }

    /// <summary>
    /// Word hujjatidan ajratilgan rasm ma'lumotlari
    /// </summary>
    public class WordImage
    {
        public string FileName { get; set; }
        public byte[] ImageBytes { get; set; }
        public string MimeType { get; set; }
        public string ContentType { get; set; }
    }

    /// <summary>Hujjatdagi rasm metadata'si (galereya uchun).</summary>
    public class WordImageInfo
    {
        public int Index { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public long SizeBytes { get; set; }
    }
}
