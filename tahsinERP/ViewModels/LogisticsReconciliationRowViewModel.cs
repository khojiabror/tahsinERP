using System;

namespace tahsinERP.ViewModels
{
    /// <summary>Solishtirish hisoboti qatori (LOG-P06: entity + Include o'rniga SQL proyeksiya).</summary>
    public class LogisticsReconciliationRowViewModel
    {
        public long ID { get; set; }
        public string DocumentNo { get; set; }
        public string PartyName { get; set; }
        public DateTime? Date { get; set; }
        public double? Amount { get; set; }
        public string Currency { get; set; }
    }
}
