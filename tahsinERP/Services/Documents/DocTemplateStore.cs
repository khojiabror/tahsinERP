using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.Documents
{
    public sealed class DocTemplateListItem
    {
        public int ID { get; set; }
        public string TemplateCode { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDT { get; set; }
        public int UsageCount { get; set; }
    }

    /// <summary>DOC_TEMPLATES (shablonlar bazada saqlanadi) — ro'yxat va tanlash uchun metadata, blob'siz.</summary>
    public static class DocTemplateStore
    {
        public static Task<int?> DocTypeIdAsync(DBTHSNEntities db, string docTypeCode)
        {
            return db.DOC_DOCUMENT_TYPES.Where(t => t.Code == docTypeCode).Select(t => (int?)t.ID).FirstOrDefaultAsync();
        }

        /// <summary>Hujjat turi yozuvi bo'lmasa yaratadi (STD avvaldan shunday qilardi).</summary>
        public static async Task<int> EnsureDocTypeIdAsync(DBTHSNEntities db, string docTypeCode)
        {
            var id = await DocTypeIdAsync(db, docTypeCode);
            if (id.HasValue)
                return id.Value;

            var type = new DOC_DOCUMENT_TYPES
            {
                Code = docTypeCode,
                Name = docTypeCode == DocPlaceholders.StandardType ? "CompanyStandard" : DocPlaceholders.TypeName(docTypeCode),
                IsActive = true,
                SortOrder = 0
            };
            db.DOC_DOCUMENT_TYPES.Add(type);
            await db.SaveChangesAsync();
            return type.ID;
        }

        public static async Task<List<DocTemplateListItem>> ListAsync(DBTHSNEntities db, string docTypeCode, bool activeOnly)
        {
            var typeId = await DocTypeIdAsync(db, docTypeCode);
            if (!typeId.HasValue)
                return new List<DocTemplateListItem>();

            var query = db.DOC_TEMPLATES.AsNoTracking().Where(t => t.DocTypeID == typeId.Value);
            if (activeOnly)
                query = query.Where(t => t.IsActive);

            return await query
                .OrderByDescending(t => t.IsActive)
                .ThenByDescending(t => t.CreatedDT)
                .Select(t => new DocTemplateListItem
                {
                    ID = t.ID,
                    TemplateCode = t.TemplateCode,
                    Name = t.Name,
                    FileName = t.FileName,
                    IsActive = t.IsActive,
                    CreatedDT = t.CreatedDT,
                    UsageCount = t.DOC_DOCUMENTS.Count()
                })
                .ToListAsync();
        }

        /// <summary>Tanlangan (yoki yagona/eng yangi faol) shablon ID'si; tur mos kelmasa null.</summary>
        public static async Task<int?> ResolveActiveAsync(DBTHSNEntities db, string docTypeCode, int? templateId)
        {
            var typeId = await DocTypeIdAsync(db, docTypeCode);
            if (!typeId.HasValue)
                return null;

            var query = db.DOC_TEMPLATES.Where(t => t.DocTypeID == typeId.Value && t.IsActive);
            if (templateId.HasValue)
                query = query.Where(t => t.ID == templateId.Value);

            return await query.OrderByDescending(t => t.CreatedDT).Select(t => (int?)t.ID).FirstOrDefaultAsync();
        }
    }
}
