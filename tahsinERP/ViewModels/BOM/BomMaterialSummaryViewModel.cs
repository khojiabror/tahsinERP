namespace tahsinERP.ViewModels.BOM
{
    public class BomMaterialSummaryViewModel
    {
        public int MaterialId { get; set; }
        public string MaterialPno { get; set; }
        public string MaterialName { get; set; }
        public string UnitShortname { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
