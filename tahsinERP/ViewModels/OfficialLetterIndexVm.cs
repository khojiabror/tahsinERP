using System;

namespace tahsinERP.ViewModels
{
    public class OfficialLetterIndexVm
    {
        public long DocumentId { get; set; }
        public string LetterNo { get; set; }
        public DateTime LetterDate { get; set; }
        public string DirectionCode { get; set; } // OUT/IN
        public string ExternalCompanyName { get; set; }
        public string ExternalContactName { get; set; }
        public string Topic { get; set; }
        public string StatusName { get; set; }
        public DateTime CreatedDT { get; set; }
    }
}

