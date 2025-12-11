using System;

namespace tahsinERP.ViewModels
{
    public class CompanyOrderIndexVm
    {
        public long DocumentId { get; set; }
        public string OrderNo { get; set; }
        public DateTime OrderDate { get; set; }
        public string OrderTypeCode { get; set; }
        public string OrderTypeName { get; set; }
        public string OrgUnitName { get; set; }
        public string StatusName { get; set; }
        public DateTime CreatedDT { get; set; }
    }
}

