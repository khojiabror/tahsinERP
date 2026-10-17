using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Rasmiy maktub uchun matn (Quill HTML), salomlashish va mahsulot/narx
    /// satrlarini DOC_FILES jadvalida JSON ko'rinishida saqlash/yuklash xizmati.
    /// Jadval sxemasini o'zgartirmasdan qo'shimcha ma'lumotni saqlash uchun ishlatiladi.
    /// </summary>
    public class OfficialLetterDataService
    {
        // DOC_FILES ichida ushbu yordamchi yozuvni aniqlash uchun belgilar
        public const string DataFileName = "_letterdata.json";
        public const string DataFileExtension = ".json";

        /// <summary>
        /// Berilgan hujjat uchun maktub ma'lumotlarini (matn, salomlashish, mahsulotlar) yuklaydi.
        /// Topilmasa, bo'sh obyekt qaytaradi.
        /// </summary>
        public static async Task<OfficialLetterData> LoadAsync(DBTHSNEntities db, long documentId)
        {
            var file = await db.DOC_FILES
                .Where(f => f.DocumentID == documentId && f.FileExtension == DataFileExtension && f.FileName == DataFileName)
                .OrderByDescending(f => f.ID)
                .FirstOrDefaultAsync();

            if (file == null || file.FileContent == null || file.FileContent.Length == 0)
            {
                return new OfficialLetterData();
            }

            try
            {
                var json = Encoding.UTF8.GetString(file.FileContent);
                return JsonConvert.DeserializeObject<OfficialLetterData>(json) ?? new OfficialLetterData();
            }
            catch
            {
                return new OfficialLetterData();
            }
        }

        /// <summary>
        /// Maktub ma'lumotlarini DOC_FILES jadvaliga saqlaydi (mavjud bo'lsa yangilaydi).
        /// SaveChangesAsync chaqiruvchi tomonidan amalga oshiriladi.
        /// </summary>
        public static async Task SaveAsync(DBTHSNEntities db, long documentId, OfficialLetterData data, int? userId)
        {
            if (data == null)
                data = new OfficialLetterData();

            var json = JsonConvert.SerializeObject(data);
            var bytes = Encoding.UTF8.GetBytes(json);

            var file = await db.DOC_FILES
                .Where(f => f.DocumentID == documentId && f.FileExtension == DataFileExtension && f.FileName == DataFileName)
                .OrderByDescending(f => f.ID)
                .FirstOrDefaultAsync();

            if (file == null)
            {
                file = new DOC_FILES
                {
                    DocumentID = documentId,
                    VersionNo = 0, // 0 = yordamchi ma'lumot fayli, hujjat versiyalariga aralashmaydi
                    FileName = DataFileName,
                    FileExtension = DataFileExtension,
                    MimeType = "application/json",
                    FileSizeBytes = bytes.Length,
                    FileContent = bytes,
                    IsMain = false,
                    CreatedByUserID = userId,
                    CreatedDT = DateTime.Now
                };
                db.DOC_FILES.Add(file);
            }
            else
            {
                file.FileContent = bytes;
                file.FileSizeBytes = bytes.Length;
            }
        }
    }

    /// <summary>
    /// DOC_FILES JSON yozuvida saqlanadigan maktub ma'lumotlari.
    /// </summary>
    public class OfficialLetterData
    {
        [JsonProperty("bodyHtml")]
        public string BodyHtml { get; set; }

        [JsonProperty("greetingLine")]
        public string GreetingLine { get; set; }

        [JsonProperty("items")]
        public List<OfficialLetterItemData> Items { get; set; } = new List<OfficialLetterItemData>();
    }

    /// <summary>
    /// Bitta mahsulot/narx satri (EXW va CIP narxlari bilan).
    /// </summary>
    public class OfficialLetterItemData
    {
        [JsonProperty("itemName")]
        public string ItemName { get; set; }

        [JsonProperty("itemSize")]
        public string ItemSize { get; set; }

        [JsonProperty("priceExw")]
        public decimal? PriceExw { get; set; }

        [JsonProperty("priceVatExw")]
        public decimal? PriceVatExw { get; set; }

        [JsonProperty("priceCip")]
        public decimal? PriceCip { get; set; }

        [JsonProperty("priceVatCip")]
        public decimal? PriceVatCip { get; set; }
    }
}
