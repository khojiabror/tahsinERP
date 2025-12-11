using System;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for calculating maximum load capacity of products in transport types
    /// </summary>
    public static class TransportLoadCalculator
    {
        private const decimal MM_TO_M_CUBE = 1_000_000_000m; // 1 m³ = 1,000,000,000 mm³

        /// <summary>
        /// Calculate maximum load for a product in a transport type
        /// </summary>
        public static TransportLoadResult CalculateMaxLoad(int productId, int transportTypeId, DBTHSNEntities db)
        {
            // Load product
            var product = db.PRODUCTS.FirstOrDefault(p => p.ID == productId && p.IsDeleted == false);
            if (product == null)
            {
                throw new ArgumentException($"Product with ID {productId} not found or is deleted");
            }

            // Load transport type
            var transportType = db.F_TRANSPORT_TYPES.FirstOrDefault(t => t.ID == transportTypeId);
            if (transportType == null)
            {
                throw new ArgumentException($"Transport type with ID {transportTypeId} not found");
            }

            // Get dimensions (assuming they are in mm as per requirement)
            decimal productLengthMm = (decimal)(product.Length ?? 0);
            decimal productWidthMm = (decimal)(product.Width ?? 0);
            decimal productHeightMm = (decimal)(product.Height ?? 0);

            decimal transportLengthMm = (decimal)transportType.IntLgth;
            decimal transportWidthMm = (decimal)transportType.IntWdth;
            decimal transportHeightMm = (decimal)transportType.IntHght;

            // Validate dimensions
            if (productLengthMm <= 0 || productWidthMm <= 0 || productHeightMm <= 0)
            {
                throw new ArgumentException("Product dimensions must be greater than zero");
            }

            if (transportLengthMm <= 0 || transportWidthMm <= 0 || transportHeightMm <= 0)
            {
                throw new ArgumentException("Transport internal dimensions must be greater than zero");
            }

            // Calculate using raw dimensions
            return CalculateMaxLoadFromDimensions(
                productLengthMm,
                productWidthMm,
                productHeightMm,
                transportLengthMm,
                transportWidthMm,
                transportHeightMm,
                productId,
                product.Name,
                product.PNo,
                transportTypeId,
                transportType.TransportType
            );
        }

        /// <summary>
        /// Calculate maximum load from raw dimensions (reusable without DB)
        /// </summary>
        public static TransportLoadResult CalculateMaxLoadFromDimensions(
            decimal productLengthMm,
            decimal productWidthMm,
            decimal productHeightMm,
            decimal transportLengthMm,
            decimal transportWidthMm,
            decimal transportHeightMm,
            int productId = 0,
            string productName = "",
            string productPNo = "",
            int transportTypeId = 0,
            string transportTypeName = "")
        {
            // Orientation A: LxW (product length along transport length, product width along transport width)
            int countLengthA = (int)Math.Floor(transportLengthMm / productLengthMm);
            int countWidthA = (int)Math.Floor(transportWidthMm / productWidthMm);

            // Orientation B: WxL (product width along transport length, product length along transport width)
            int countLengthB = (int)Math.Floor(transportLengthMm / productWidthMm);
            int countWidthB = (int)Math.Floor(transportWidthMm / productLengthMm);

            // Height is always aligned "height to height"
            int countHeight = (int)Math.Floor(transportHeightMm / productHeightMm);

            // Calculate quantities
            int quantityA = countLengthA * countWidthA * countHeight;
            int quantityB = countLengthB * countWidthB * countHeight;

            // Determine best orientation
            bool useOrientationA = quantityA >= quantityB;
            int maxQuantity = useOrientationA ? quantityA : quantityB;

            // Calculate volumes
            decimal productVolumeMm3 = productLengthMm * productWidthMm * productHeightMm;
            decimal volumePerUnitCbm = productVolumeMm3 / MM_TO_M_CUBE;
            decimal totalCbm = maxQuantity * volumePerUnitCbm;

            decimal transportVolumeMm3 = transportLengthMm * transportWidthMm * transportHeightMm;
            decimal transportVolumeCbm = transportVolumeMm3 / MM_TO_M_CUBE;
            decimal utilizationPercent = transportVolumeCbm > 0 ? (totalCbm / transportVolumeCbm) * 100 : 0;

            // Build result
            var result = new TransportLoadResult
            {
                ProductId = productId,
                ProductName = productName,
                ProductPNo = productPNo,
                ProductLengthMm = productLengthMm,
                ProductWidthMm = productWidthMm,
                ProductHeightMm = productHeightMm,

                TransportTypeId = transportTypeId,
                TransportTypeName = transportTypeName,
                TransportLengthMm = transportLengthMm,
                TransportWidthMm = transportWidthMm,
                TransportHeightMm = transportHeightMm,

                MaxQuantity = maxQuantity,
                VolumePerUnitCbm = volumePerUnitCbm,
                TotalCbm = totalCbm,
                TransportVolumeCbm = transportVolumeCbm,
                UtilizationPercent = utilizationPercent,

                BestOrientation = useOrientationA ? "LxW" : "WxL",
                CountLength = useOrientationA ? countLengthA : countLengthB,
                CountWidth = useOrientationA ? countWidthA : countWidthB,
                CountHeight = countHeight,

                AlternativeQuantity = useOrientationA ? quantityB : quantityA,
                AlternativeOrientation = useOrientationA ? "WxL" : "LxW",
                AlternativeCountLength = useOrientationA ? countLengthB : countLengthA,
                AlternativeCountWidth = useOrientationA ? countWidthB : countWidthA
            };

            return result;
        }
    }
}
