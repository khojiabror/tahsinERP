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

        /// <summary>Standart sanoat palleti (ISO, 1200×1000), mm — avvalgi hisob bilan bir xil.</summary>
        public const double PalletLengthMm = 1200;
        public const double PalletWidthMm = 1000;

        /// <summary>
        /// F_TRANSPORT_TYPES o'lchamlari — mm (LOG-B10, foydalanuvchi qarori). Avval FTransportType
        /// sahifalari ularni sm deb (÷100) hisoblardi, simulyator esa mm deb — natija 1000 marta farq qilardi.
        /// </summary>
        public static double? CbmFromMm(double lengthMm, double widthMm, double heightMm)
        {
            if (lengthMm <= 0 || widthMm <= 0 || heightMm <= 0) return null;
            return (double)((decimal)lengthMm * (decimal)widthMm * (decimal)heightMm / MM_TO_M_CUBE);
        }

        /// <summary>Polga bir qatlamda sig'adigan palletlar soni (ikki yo'nalishning yaxshisi).</summary>
        public static int? PalletsOnFloor(double lengthMm, double widthMm,
            double palletLengthMm = PalletLengthMm, double palletWidthMm = PalletWidthMm)
        {
            if (lengthMm <= 0 || widthMm <= 0) return null;
            int along = (int)Math.Floor(lengthMm / palletLengthMm) * (int)Math.Floor(widthMm / palletWidthMm);
            int across = (int)Math.Floor(lengthMm / palletWidthMm) * (int)Math.Floor(widthMm / palletLengthMm);
            return Math.Max(along, across);
        }

        public static double? InternalCbm(F_TRANSPORT_TYPES t) => t == null ? null : CbmFromMm((double)t.IntLgth, (double)t.IntWdth, (double)t.IntHght);

        public static double? ExternalCbm(F_TRANSPORT_TYPES t) => t == null ? null : CbmFromMm((double)t.ExtLgth, (double)t.ExtWdth, (double)t.ExtHght);

        public static int? Pallets(F_TRANSPORT_TYPES t) => t == null ? null : PalletsOnFloor((double)t.IntLgth, (double)t.IntWdth);

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
            var transportType = db.F_TRANSPORT_TYPES.FirstOrDefault(t => t.ID == transportTypeId && t.IsDeleted != true);
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
