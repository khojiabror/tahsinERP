using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// DOC_FILES faylini faqat o'z moduli hujjatiga tegishli va o'chirilmagan bo'lsa qaytaradi.
    /// Avval har bir controller DOC_FILES.FindAsync(fileId) qilardi — istalgan modul fayli
    /// (masalan, xat ruxsati bilan lavozim yo'riqnomasi) va o'chirilgan fayllar ham berilardi.
    /// </summary>
    public static class DocFileAccess
    {
        public enum Owner
        {
            OfficialLetter,
            CompanyOrder,
            CompanyStandard,
            JobDescription
        }

        public static Task<DOC_FILES> FindAsync(DBTHSNEntities db, long fileId, Owner owner)
        {
            var query = db.DOC_FILES.Where(f =>
                f.ID == fileId &&
                f.IsDeleted != true &&
                f.DOC_DOCUMENTS.IsDeleted != true);

            switch (owner)
            {
                case Owner.OfficialLetter:
                    query = query.Where(f => db.COM_OFFICIAL_LETTERS.Any(x => x.DocumentID == f.DocumentID && x.IsDeleted != true));
                    break;
                case Owner.CompanyOrder:
                    query = query.Where(f => db.ADM_COMPANY_ORDERS.Any(x => x.DocumentID == f.DocumentID && x.IsDeleted != true));
                    break;
                case Owner.CompanyStandard:
                    query = query.Where(f => db.STD_COMPANY_STANDARDS.Any(x => x.DocumentID == f.DocumentID && x.IsDeleted != true));
                    break;
                case Owner.JobDescription:
                    query = query.Where(f => db.JOB_DESC.Any(x => x.DocumentID == f.DocumentID && x.IsDeleted != true));
                    break;
            }

            return query.FirstOrDefaultAsync();
        }
    }
}
