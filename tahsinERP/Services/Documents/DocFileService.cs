using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.Documents
{
    /// <summary>DOC_FILES qatorining blob'siz (FileContent'siz) ko'rinishi — ro'yxat va tarix uchun.</summary>
    public sealed class DocFileListItem
    {
        public long ID { get; set; }
        public int VersionNo { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string MimeType { get; set; }
        public long? FileSizeBytes { get; set; }
        public bool IsMain { get; set; }
        public DateTime CreatedDT { get; set; }
    }

    /// <summary>
    /// DOC_FILES bilan ishlash. FileContent (varbinary(max)) metadata bilan bitta entity'da
    /// bo'lgani uchun DOC_FILES'ni materializatsiya qilish (Include, FindAsync, ToListAsync)
    /// har safar barcha versiyalar blob'ini SQL'dan o'qirdi (audit DOC-P01/P02/P05/P06).
    /// Bu yerda faqat proyeksiya va set-based UPDATE ishlatiladi; blob faqat yuklab olishda o'qiladi.
    /// </summary>
    public static class DocFileService
    {
        public const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        /// <summary>Hujjatning asosiy (IsMain) fayllari, yangi versiya birinchi. Yordamchi .json yozuvlari kirmaydi.</summary>
        public static Task<List<DocFileListItem>> ListMainAsync(DBTHSNEntities db, long documentId)
        {
            return Metadata(db.DOC_FILES.Where(f =>
                    f.DocumentID == documentId &&
                    f.IsMain &&
                    f.IsDeleted != true &&
                    f.FileExtension != OfficialLetterDataService.DataFileExtension))
                .OrderByDescending(f => f.VersionNo)
                .ToListAsync();
        }

        /// <summary>Berilgan kengaytmadagi barcha versiyalar (masalan, .docx tarixi), yangi birinchi.</summary>
        public static Task<List<DocFileListItem>> ListVersionsAsync(DBTHSNEntities db, long documentId, string extension)
        {
            return Metadata(db.DOC_FILES.Where(f =>
                    f.DocumentID == documentId &&
                    f.FileExtension == extension &&
                    f.IsDeleted != true))
                .OrderByDescending(f => f.VersionNo)
                .ToListAsync();
        }

        /// <summary>Eng so'nggi asosiy fayl ID'si (blob o'qilmaydi).</summary>
        public static Task<long?> LatestMainFileIdAsync(DBTHSNEntities db, long documentId, string extension)
        {
            return db.DOC_FILES
                .Where(f => f.DocumentID == documentId && f.IsMain && f.IsDeleted != true && f.FileExtension == extension)
                .OrderByDescending(f => f.VersionNo)
                .Select(f => (long?)f.ID)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Yangi asosiy versiya qo'shadi: keyingi VersionNo SQL'da MAX bilan, avvalgi IsMain bitta UPDATE bilan.
        /// UPDATE darhol bajariladi — atomiklik uchun chaqiruvchi tranzaksiya ochishi kerak; SaveChanges chaqiruvchida.
        /// </summary>
        public static async Task<DOC_FILES> AddMainVersionAsync(DBTHSNEntities db, long documentId, byte[] content,
            string fileName, string extension, string mimeType, int? userId, int? versionNo = null)
        {
            int nextVersion = versionNo ?? await NextVersionNoAsync(db, documentId);

            await db.Database.ExecuteSqlCommandAsync(
                "UPDATE DOC_FILES SET IsMain = 0 WHERE DocumentID = @p0 AND IsMain = 1", documentId);

            var file = new DOC_FILES
            {
                DocumentID = documentId,
                VersionNo = nextVersion,
                FileName = fileName,
                FileExtension = extension,
                MimeType = mimeType,
                FileSizeBytes = content.Length,
                // Blob alohida entity'da (DOC-P02 table splitting) — metadata so'rovlari uni o'qimaydi
                DOC_FILE_CONTENTS = new DOC_FILE_CONTENTS { FileContent = content },
                IsMain = true,
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now
            };
            db.DOC_FILES.Add(file);
            return file;
        }

        /// <summary>Fayl baytlari (faqat yuklab berish/konvertatsiya uchun); topilmasa null.</summary>
        public static Task<byte[]> ContentAsync(DBTHSNEntities db, long fileId)
        {
            return db.DOC_FILE_CONTENTS.AsNoTracking()
                .Where(c => c.ID == fileId)
                .Select(c => c.FileContent)
                .FirstOrDefaultAsync();
        }

        public static async Task<int> NextVersionNoAsync(DBTHSNEntities db, long documentId)
        {
            int? max = await db.DOC_FILES
                .Where(f => f.DocumentID == documentId)
                .MaxAsync(f => (int?)f.VersionNo);
            return (max ?? 0) + 1;
        }

        /// <summary>Hujjat fayllarini bitta UPDATE bilan soft-delete qiladi (avval har fayl blob'i bilan yuklanardi).</summary>
        public static Task<int> SoftDeleteAllAsync(DBTHSNEntities db, long documentId)
        {
            return db.Database.ExecuteSqlCommandAsync(
                "UPDATE DOC_FILES SET IsDeleted = 1 WHERE DocumentID = @p0 AND (IsDeleted IS NULL OR IsDeleted = 0)", documentId);
        }

        private static IQueryable<DocFileListItem> Metadata(IQueryable<DOC_FILES> query)
        {
            return query.Select(f => new DocFileListItem
            {
                ID = f.ID,
                VersionNo = f.VersionNo,
                FileName = f.FileName,
                FileExtension = f.FileExtension,
                MimeType = f.MimeType,
                FileSizeBytes = f.FileSizeBytes,
                IsMain = f.IsMain,
                CreatedDT = f.CreatedDT
            });
        }
    }
}
