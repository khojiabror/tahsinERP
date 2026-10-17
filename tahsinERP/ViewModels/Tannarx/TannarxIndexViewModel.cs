namespace tahsinERP.ViewModels.Tannarx
{
    public class TannarxIndexViewModel : ITannarxCostFields
    {
        public int ID { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public string ProductType { get; set; }
        public string Dimensions { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthString { get; set; }
        
        // Material costs
        public decimal MaterialPrice { get; set; }
        public decimal MaterialTransportCost { get; set; }
        
        // Labor and expenses
        public decimal LaborCost { get; set; }
        public decimal TaxAndBank { get; set; }
        public decimal AdministrativeExpenses { get; set; }
        public decimal OtherExpenses { get; set; }
        public decimal Profit { get; set; }
        
        // EXW Prices
        public decimal SalesPriceEXWWithoutVAT { get; set; }
        public decimal SalesPriceEXWWithVAT { get; set; }
        public decimal? SetPriceEXWWithVAT { get; set; }
        
        // Finished product transportation
        public decimal? FinishedProductTransportCost { get; set; }
        
        // CIP Prices
        public decimal SalesPriceCIPWithoutVAT { get; set; }
        public decimal SalesPriceCIPWithVAT { get; set; }
        public decimal? SetPriceCIPWithVAT { get; set; }
    }
}
