using System.Collections.Generic;

namespace tahsinERP.ViewModels.BOM
{
    public class BomTreeNodeViewModel
    {
        public int ProductId { get; set; }
        public string ProductPno { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; } // quantity relative to parent
        public string UnitShortname { get; set; } // unit short name (e.g., "m", "kg")
        public int UnitId { get; set; }
        public string ImageUrl { get; set; } // product image URL
        public List<BomTreeNodeViewModel> Children { get; set; } = new List<BomTreeNodeViewModel>();
    }
}
