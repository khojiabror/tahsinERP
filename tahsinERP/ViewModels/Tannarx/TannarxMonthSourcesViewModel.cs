using System.Collections.Generic;

namespace tahsinERP.ViewModels.Tannarx
{
    /// <summary>
    /// Oy xarajatlari manbalari: har xarajat elementi uchun manbadan tortilgan jami, mahsulotlarga taqsimlangan
    /// jami va taqsimlanmay qolgan qoldiq; mahsulot bo'yicha tanlangan algoritm; Finance kategoriyalari.
    /// </summary>
    public class TannarxMonthSourcesViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthString { get; set; }
        public decimal MonthProducedQty { get; set; }
        public string BaseCurrencyCode { get; set; }
        public List<TannarxCostElementSourceRow> Elements { get; set; } = new List<TannarxCostElementSourceRow>();
        public List<TannarxProductSourceRow> Products { get; set; } = new List<TannarxProductSourceRow>();
        public List<TannarxFinanceSourceRow> FinanceLines { get; set; } = new List<TannarxFinanceSourceRow>();
        public List<TannarxProducedWithoutTannarxRow> ProducedWithoutTannarx { get; set; } = new List<TannarxProducedWithoutTannarxRow>();
        public Dictionary<string, int> MaterialPathCounts { get; set; } = new Dictionary<string, int>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class TannarxCostElementSourceRow
    {
        public string Element { get; set; }
        public string Source { get; set; }
        /// <summary>Manbadagi oy jami (null — manba mahsulotga bog'liq, alohida jami yo'q).</summary>
        public decimal? SourceTotal { get; set; }
        /// <summary>TANNARX qatorlariga taqsimlangan: Σ (1 dona narx × ishlab chiqarilgan miqdor).</summary>
        public decimal AllocatedTotal { get; set; }
        public decimal? Unallocated => SourceTotal.HasValue ? SourceTotal.Value - AllocatedTotal : (decimal?)null;
        public string Note { get; set; }
    }

    public class TannarxProductSourceRow
    {
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public decimal ProducedQty { get; set; }
        public string MaterialPath { get; set; }
        public string MaterialMethod { get; set; }
        public string PathAttempts { get; set; }
        public decimal Material { get; set; }
        public decimal MaterialTransport { get; set; }
        public decimal Labor { get; set; }
        public decimal TaxBank { get; set; }
        public decimal Admin { get; set; }
        public decimal Other { get; set; }
        public decimal FinishedTransport { get; set; }
        public decimal TotalCostPerUnit => Material + MaterialTransport + Labor + TaxBank + Admin + Other;
    }

    public class TannarxFinanceSourceRow
    {
        public string CategoryName { get; set; }
        public string Bucket { get; set; }
        public string Reason { get; set; }
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
        public bool Included { get; set; }
    }

    /// <summary>Finance xarajat kategoriyalari → tannarx guruhi (qo'lda belgilash sahifasi).</summary>
    public class TannarxFinanceCategoryMapViewModel
    {
        public bool TableExists { get; set; }
        public List<TannarxFinanceCategoryMapRow> Rows { get; set; } = new List<TannarxFinanceCategoryMapRow>();
        /// <summary>Tanlov qiymatlari: "" = avtomatik, 0 = chiqarilsin, 1 = Material, 2 = Soliq va bank, 3 = Boshqa.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Options = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("", "Avtomatik (nom bo'yicha)"),
            new KeyValuePair<string, string>("1", "Material (PATH3)"),
            new KeyValuePair<string, string>("2", "B — Soliq va bank"),
            new KeyValuePair<string, string>("3", "D — Boshqa"),
            new KeyValuePair<string, string>("0", "Tannarxga kirmaydi")
        };
    }

    public class TannarxFinanceCategoryMapRow
    {
        public int CategoryID { get; set; }
        public string Name { get; set; }
        public string ParentName { get; set; }
        public bool IsActive { get; set; }
        public string AutoBucket { get; set; }
        public string AutoReason { get; set; }
        /// <summary>null — avtomatik.</summary>
        public int? ManualBucket { get; set; }
        public string EffectiveBucket { get; set; }
        public int TransactionCount { get; set; }
    }

    public class TannarxProducedWithoutTannarxRow
    {
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public decimal ProducedQty { get; set; }
    }
}
