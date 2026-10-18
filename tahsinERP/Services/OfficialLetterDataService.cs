using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Rasmiy maktub matni (Quill HTML), salomlashish va mahsulot/narx satrlari — COM_OFFICIAL_LETTERS.BodyHtml/
    /// GreetingLine ustunlari va COM_OFFICIAL_LETTER_ITEMS jadvali (DOC-P10). Avval DOC_FILES'da "_letterdata.json"
    /// varbinary qatori edi: har Edit/Details/GenerateDoc'da qo'shimcha blob o'qilardi va fayl ro'yxatiga aralashardi.
    /// Eski JSON qatori faqat ustunlar bo'sh bo'lsa o'qiladi; keyingi saqlashda ustunlarga ko'chadi va o'chirilgan
    /// deb belgilanadi (Database_Migrations/Alter_OfficialLetters_BodyItems.sql).
    /// </summary>
    public class OfficialLetterDataService
    {
        // Eski (DOC-P10 gacha) DOC_FILES yordamchi yozuvi belgilari
        public const string DataFileName = "_letterdata.json";
        public const string DataFileExtension = ".json";

        /// <summary>Maktub ma'lumotlari; topilmasa bo'sh obyekt.</summary>
        public static async Task<OfficialLetterData> LoadAsync(DBTHSNEntities db, long documentId)
        {
            var letter = await db.COM_OFFICIAL_LETTERS.AsNoTracking()
                .Where(l => l.DocumentID == documentId)
                .Select(l => new { l.BodyHtml, l.GreetingLine })
                .FirstOrDefaultAsync();

            var items = await db.COM_OFFICIAL_LETTER_ITEMS.AsNoTracking()
                .Where(i => i.DocumentID == documentId)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.ID)
                .Select(i => new OfficialLetterItemData
                {
                    ItemName = i.ItemName,
                    ItemSize = i.ItemSize,
                    PriceExw = i.PriceExw,
                    PriceVatExw = i.PriceVatExw,
                    PriceCip = i.PriceCip,
                    PriceVatCip = i.PriceVatCip
                })
                .ToListAsync();

            if (letter != null && (letter.BodyHtml != null || letter.GreetingLine != null || items.Count > 0))
            {
                return new OfficialLetterData { BodyHtml = letter.BodyHtml, GreetingLine = letter.GreetingLine, Items = items };
            }

            return await LoadLegacyJsonAsync(db, documentId) ?? new OfficialLetterData();
        }

        /// <summary>
        /// Ma'lumotni ustunlar va satrlar jadvaliga yozadi (satrlar to'liq almashtiriladi); eski JSON qatori o'chirilgan
        /// deb belgilanadi. SaveChangesAsync chaqiruvchida.
        /// </summary>
        public static async Task SaveAsync(DBTHSNEntities db, long documentId, OfficialLetterData data, int? userId)
        {
            if (data == null)
                data = new OfficialLetterData();

            var letter = await db.COM_OFFICIAL_LETTERS.FindAsync(documentId);
            if (letter == null)
                return;

            letter.BodyHtml = data.BodyHtml;
            letter.GreetingLine = data.GreetingLine;

            var existing = await db.COM_OFFICIAL_LETTER_ITEMS.Where(i => i.DocumentID == documentId).ToListAsync();
            db.COM_OFFICIAL_LETTER_ITEMS.RemoveRange(existing);

            int order = 0;
            foreach (var item in data.Items ?? new List<OfficialLetterItemData>())
            {
                db.COM_OFFICIAL_LETTER_ITEMS.Add(new COM_OFFICIAL_LETTER_ITEMS
                {
                    DocumentID = documentId,
                    SortOrder = order++,
                    ItemName = item.ItemName,
                    ItemSize = item.ItemSize,
                    PriceExw = item.PriceExw,
                    PriceVatExw = item.PriceVatExw,
                    PriceCip = item.PriceCip,
                    PriceVatCip = item.PriceVatCip
                });
            }

            await db.Database.ExecuteSqlCommandAsync(
                "UPDATE DOC_FILES SET IsDeleted = 1 WHERE DocumentID = @p0 AND FileName = @p1 AND FileExtension = @p2 AND (IsDeleted IS NULL OR IsDeleted = 0)",
                documentId, DataFileName, DataFileExtension);
        }

        /// <summary>DOC-P10 gacha saqlangan JSON (ustunlar hali to'ldirilmagan xatlar uchun).</summary>
        private static async Task<OfficialLetterData> LoadLegacyJsonAsync(DBTHSNEntities db, long documentId)
        {
            var fileId = await db.DOC_FILES.AsNoTracking()
                .Where(f => f.DocumentID == documentId && f.FileExtension == DataFileExtension && f.FileName == DataFileName && f.IsDeleted != true)
                .OrderByDescending(f => f.ID)
                .Select(f => (long?)f.ID)
                .FirstOrDefaultAsync();
            if (fileId == null)
                return null;

            var bytes = await tahsinERP.Services.Documents.DocFileService.ContentAsync(db, fileId.Value);
            if (bytes == null || bytes.Length == 0)
                return null;

            try
            {
                return JsonConvert.DeserializeObject<OfficialLetterData>(Encoding.UTF8.GetString(bytes));
            }
            catch (JsonException)
            {
                return null;
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
