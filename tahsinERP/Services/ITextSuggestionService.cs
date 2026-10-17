using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace tahsinERP.Services
{
    public interface ITextSuggestionService
    {
        Task<string> SuggestOrderBodyAsync(CompanyOrderMeta meta);
        Task<string> SuggestLetterBodyAsync(OfficialLetterMeta meta);
    }

    public class CompanyOrderMeta
    {
        public string OrderTypeCode { get; set; }
        public DateTime OrderDate { get; set; }
        public string OrgUnitName { get; set; }
        public IList<string> ResponsibleEmployeeNames { get; set; }
        public string Topic { get; set; }
    }

    public class OfficialLetterMeta
    {
        public string DirectionCode { get; set; } // OUT / IN
        public string ExternalCompanyName { get; set; }
        public string ExternalContactName { get; set; }
        public string Topic { get; set; }
        public DateTime LetterDate { get; set; }
        public string OurRefNo { get; set; }
        public string TheirRefNo { get; set; }
    }
}
