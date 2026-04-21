namespace tahsinERP.ViewModels.BOM
{
    using System.Collections.Generic;

    public class BomDetailsViewModel
    {
        public int ProductId { get; set; }
        public string ProductPno { get; set; }
        public string ProductName { get; set; }
        public string NormVersion { get; set; }
        public string ImageUrl { get; set; } // from PRODUCTIMAGES if exists
        public BomTreeNodeViewModel RootNode { get; set; }
        public List<BomMaterialSummaryViewModel> MaterialSummaries { get; set; } = new List<BomMaterialSummaryViewModel>();
    }
}
