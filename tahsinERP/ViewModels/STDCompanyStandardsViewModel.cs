using System;
using System.Collections.Generic;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class STDCompanyStandardsIndexViewModel
    {
        public long DocumentID { get; set; }
        public string StandardCode { get; set; }
        public string Area { get; set; }
        public decimal VersionNo { get; set; }
        public string Notes { get; set; }
        public string DocumentTitle { get; set; }
        public DateTime? CreatedDT { get; set; }
    }

    public class STDCompanyStandardsCreateViewModel
    {
        public string StandardCode { get; set; }
        public string Area { get; set; }
        public string Notes { get; set; }
        public string DocumentContent { get; set; } // Base64 encoded .docx content
    }

    public class STDCompanyStandardsEditViewModel
    {
        public long DocumentID { get; set; }
        public string StandardCode { get; set; }
        public string Area { get; set; }
        public decimal CurrentVersionNo { get; set; }
        public decimal NewVersionNo { get; set; }
        public string Notes { get; set; }
        public string DocumentContent { get; set; } // Base64 encoded .docx content
    }

    public class STDCompanyStandardsDetailsViewModel
    {
        public long DocumentID { get; set; }
        public string StandardCode { get; set; }
        public string Area { get; set; }
        public decimal VersionNo { get; set; }
        public string Notes { get; set; }
        public string DocumentTitle { get; set; }
        public DateTime CreatedDT { get; set; }
        public DateTime? UpdatedDT { get; set; }
        public List<VersionHistoryItem> VersionHistory { get; set; }
        public bool HasDocumentFile { get; set; }
        public List<DocumentImageItem> Images { get; set; }
    }

    public class DocumentImageItem
    {
        /// <summary>Rasm joylashgan .docx faylining ID'si.</summary>
        public long FileID { get; set; }
        /// <summary>.docx ichidagi rasm tartib raqami (WordDocumentImageService.GetImage).</summary>
        public int Index { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public long FileSizeBytes { get; set; }
        public int VersionNo { get; set; }
    }

    public class VersionHistoryItem
    {
        public int VersionNo { get; set; }
        public decimal VersionDecimal { get; set; }
        public DateTime CreatedDT { get; set; }
        public string FileName { get; set; }
        public long FileID { get; set; }
    }
}

