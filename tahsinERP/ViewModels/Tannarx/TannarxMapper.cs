using tahsinERP.Models;

namespace tahsinERP.ViewModels.Tannarx
{
    /// <summary>Tannarx Index/Edit/Details view model'lari uchun umumiy narx maydonlari.</summary>
    public interface ITannarxCostFields
    {
        int ID { get; set; }
        int ProductID { get; set; }
        string ProductPNo { get; set; }
        string ProductName { get; set; }
        string ProductType { get; set; }
        string Dimensions { get; set; }
        int Year { get; set; }
        int Month { get; set; }
        string MonthString { get; set; }
        decimal MaterialPrice { get; set; }
        decimal MaterialTransportCost { get; set; }
        decimal LaborCost { get; set; }
        decimal TaxAndBank { get; set; }
        decimal AdministrativeExpenses { get; set; }
        decimal OtherExpenses { get; set; }
        decimal Profit { get; set; }
        decimal SalesPriceEXWWithoutVAT { get; set; }
        decimal SalesPriceEXWWithVAT { get; set; }
        decimal? SetPriceEXWWithVAT { get; set; }
        decimal? FinishedProductTransportCost { get; set; }
        decimal SalesPriceCIPWithoutVAT { get; set; }
        decimal SalesPriceCIPWithVAT { get; set; }
        decimal? SetPriceCIPWithVAT { get; set; }
    }

    /// <summary>
    /// TANNARX → view model (avval TannarxController'da Index/Edit/Details uchun to'rt marta nusxalangan, dublikat #22).
    /// <c>t.PRODUCT</c> yuklangan bo'lishi kerak (Include yoki proyeksiya).
    /// </summary>
    public static class TannarxMapper
    {
        public static T Map<T>(TANNARX t) where T : ITannarxCostFields, new()
        {
            var p = t.PRODUCT;
            return new T
            {
                ID = t.ID,
                ProductID = t.ProductID,
                ProductPNo = p?.PNo ?? "",
                ProductName = p?.Name ?? "",
                ProductType = p?.Type ?? "",
                // decimal(18,4): "1470.0000" emas, float davridagidek "1470"
                Dimensions = $"{p?.Length ?? 0:0.####}x{p?.Width ?? 0:0.####}x{p?.Height ?? 0:0.####}",
                Year = t.Year,
                Month = t.Month,
                MonthString = t.MonthString,
                MaterialPrice = t.MaterialPrice,
                MaterialTransportCost = t.MaterialTransportCost,
                LaborCost = t.LaborCost,
                TaxAndBank = t.TaxAndBank,
                AdministrativeExpenses = t.AdministrativeExpenses,
                OtherExpenses = t.OtherExpenses,
                Profit = t.Profit,
                SalesPriceEXWWithoutVAT = t.SalesPriceEXWWithoutVAT,
                SalesPriceEXWWithVAT = t.SalesPriceEXWWithVAT,
                SetPriceEXWWithVAT = t.SetPriceEXWWithVAT,
                FinishedProductTransportCost = t.FinishedProductTransportCost,
                SalesPriceCIPWithoutVAT = t.SalesPriceCIPWithoutVAT,
                SalesPriceCIPWithVAT = t.SalesPriceCIPWithVAT,
                SetPriceCIPWithVAT = t.SetPriceCIPWithVAT
            };
        }
    }
}
