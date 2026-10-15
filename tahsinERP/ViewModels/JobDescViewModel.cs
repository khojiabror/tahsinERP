using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    public class JobDescIndexViewModel
    {
        public long DocumentID { get; set; }
        public string JobDescriptionCode { get; set; }
        public string JobTitle { get; set; }
        public string Department { get; set; }
        public decimal VersionNo { get; set; }
        public string Notes { get; set; }
        public string DocumentTitle { get; set; }
        public DateTime? CreatedDT { get; set; }
        public Nullable<long> HRJobDescriptionID { get; set; }
    }

    public class JobDescCreateViewModel
    {
        public string JobDescriptionCode { get; set; }
        public string JobTitle { get; set; }
        public string Department { get; set; }
        public string Notes { get; set; }
        public string DocumentContent { get; set; } // Base64 encoded .docx content or HTML content
        public Nullable<long> HRJobDescriptionID { get; set; }
    }

    public class JobDescEditViewModel
    {
        public long DocumentID { get; set; }
        public string JobDescriptionCode { get; set; }
        public string JobTitle { get; set; }
        public string Department { get; set; }
        public decimal CurrentVersionNo { get; set; }
        public decimal NewVersionNo { get; set; }
        public string Notes { get; set; }
        public string DocumentContent { get; set; } // Base64 encoded .docx content or HTML content
        public byte[] ExistingDocumentContent { get; set; }
        public Nullable<long> HRJobDescriptionID { get; set; }
    }

    public class JobDescDetailsViewModel
    {
        public long DocumentID { get; set; }
        public string JobDescriptionCode { get; set; }
        public string JobTitle { get; set; }
        public string Department { get; set; }
        public decimal VersionNo { get; set; }
        public string Notes { get; set; }
        public string DocumentTitle { get; set; }
        public DateTime CreatedDT { get; set; }
        public DateTime? UpdatedDT { get; set; }
        public List<VersionHistoryItem> VersionHistory { get; set; }
        public bool HasDocumentFile { get; set; }
        public Nullable<long> HRJobDescriptionID { get; set; }
        public string HRJobDescriptionTitle { get; set; }
    }

    // VersionHistoryItem allaqachon STDCompanyStandardsViewModel da mavjud, lekin qayta ishlatish mumkin
}

