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
        /// Word hujjatidan rasmlarni DOC_FILES jadvaliga saqlash uchun formatlash
        /// </summary>
        public static List<DocumentImageInfo> PrepareImagesForStorage(List<WordImage> images, long documentID, int versionNo)
        {
            var result = new List<DocumentImageInfo>();
            int imageIndex = 1;

            foreach (var image in images)
            {
                result.Add(new DocumentImageInfo
                {
                    DocumentID = documentID,
                    VersionNo = versionNo,
                    FileName = image.FileName ?? $"image_{imageIndex}.{GetFileExtension(image.MimeType)}",
                    FileExtension = GetFileExtension(image.MimeType),
                    MimeType = image.MimeType,
                    FileSizeBytes = image.ImageBytes.Length,
                    FileContent = image.ImageBytes,
                    IsMain = false, // Rasmlar asosiy fayl emas
                    CreatedDT = DateTime.Now
                });

                imageIndex++;
            }

            return result;
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

    /// <summary>
    /// DOC_FILES jadvaliga saqlash uchun rasm ma'lumotlari
    /// </summary>
    public class DocumentImageInfo
    {
        public long DocumentID { get; set; }
        public int VersionNo { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string MimeType { get; set; }
        public long FileSizeBytes { get; set; }
        public byte[] FileContent { get; set; }
        public bool IsMain { get; set; }
        public DateTime CreatedDT { get; set; }
    }
}

