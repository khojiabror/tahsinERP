using System;

namespace tahsinERP.Services.DlocFifo
{
    public class WarehouseLocationDto
    {
        public int ID { get; set; }
        public int WarehouseID { get; set; }
        public string LocationCode { get; set; }
        public string LocationName { get; set; }
        public decimal MinCapacity { get; set; }
        public decimal MaxCapacity { get; set; }
        public bool AllowMixedLots { get; set; }
        public bool AllowMixedProducts { get; set; }
        public bool IsActive { get; set; }
        public string WarehouseName { get; set; }
    }

    public class IncomeLotDto
    {
        public int ID { get; set; }
        public int IncomeID { get; set; }
        public int IncomeProductID { get; set; }
        public int ProductID { get; set; }
        public int WarehouseID { get; set; }
        public string LotNo { get; set; }
        public DateTime ReceivedDateTime { get; set; }
        public decimal QtyReceived { get; set; }
        public decimal QtyConsumed { get; set; }
        public decimal QtyAvailable { get; set; }
    }

    public class LotLocationBalanceDto
    {
        public int ID { get; set; }
        public int LotID { get; set; }
        public int LocationID { get; set; }
        public int ProductID { get; set; }
        public decimal QtyAvailable { get; set; }
        public string LocationCode { get; set; }
        public string LotNo { get; set; }
        public DateTime ReceivedDateTime { get; set; }
        public decimal MinCapacity { get; set; }
        public decimal MaxCapacity { get; set; }
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public string UnitName { get; set; }
    }

    public class FifoAllocationLine
    {
        public int LotID { get; set; }
        public int LocationID { get; set; }
        public string LotNo { get; set; }
        public string LocationCode { get; set; }
        public DateTime ReceivedDateTime { get; set; }
        public decimal Qty { get; set; }
    }

    /// <summary>PRODUCT_STOCKS da bor, DLOC lot/joyda aks etmayotgan qoldiq.</summary>
    public class UnassignedDlocStockRow
    {
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public double StockQty { get; set; }
        public double LedgerQty { get; set; }
        public double UnassignedQty { get; set; }
        public int UnitID { get; set; }
        public string UnitName { get; set; }
    }

    public class InventoryDlocStockRow
    {
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public int LocationID { get; set; }
        public string LocationCode { get; set; }
        public decimal QtyAvailable { get; set; }
        public decimal MinCapacity { get; set; }
        public decimal MaxCapacity { get; set; }
        public string UnitName { get; set; }
        public string LotNo { get; set; }
        public DateTime? ReceivedDateTime { get; set; }
    }
}
