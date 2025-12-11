using System;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Result of transport load calculation
    /// </summary>
    public class TransportLoadResult
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductPNo { get; set; }
        public decimal ProductLengthMm { get; set; }
        public decimal ProductWidthMm { get; set; }
        public decimal ProductHeightMm { get; set; }

        public int TransportTypeId { get; set; }
        public string TransportTypeName { get; set; }
        public decimal TransportLengthMm { get; set; }
        public decimal TransportWidthMm { get; set; }
        public decimal TransportHeightMm { get; set; }

        public int MaxQuantity { get; set; }
        public decimal VolumePerUnitCbm { get; set; }
        public decimal TotalCbm { get; set; }
        public decimal TransportVolumeCbm { get; set; }
        public decimal UtilizationPercent { get; set; }

        public string BestOrientation { get; set; } // "LxW" or "WxL"

        public int CountLength { get; set; }
        public int CountWidth { get; set; }
        public int CountHeight { get; set; }

        // Alternative orientation details for comparison
        public int AlternativeQuantity { get; set; }
        public string AlternativeOrientation { get; set; }
        public int AlternativeCountLength { get; set; }
        public int AlternativeCountWidth { get; set; }
    }
}
