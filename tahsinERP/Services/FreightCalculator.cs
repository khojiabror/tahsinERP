using System;

namespace tahsinERP.Services
{
    /// <summary>
    /// Navlun summasini hisoblash — server (Create/Edit/Excel import) va forma JS'i bir xil qoidada
    /// (LOG-B06: avval server MinimumCharge'ni e'tiborsiz qoldirardi, JS esa qo'llardi).
    /// FreightBasis: 0 — mashina uchun, 1 — ton uchun (GrWeight), 2 — km uchun (DistanceKm), 3 — belgilangan narx.
    /// </summary>
    public static class FreightCalculator
    {
        public static decimal? Calculate(byte? basis, decimal? rate, decimal? grWeight, decimal? distanceKm, decimal? minimumCharge = null)
        {
            if (!rate.HasValue || !basis.HasValue)
                return null;

            decimal? amount;
            switch (basis.Value)
            {
                case 0: // PerTruck
                case 3: // Flat
                    amount = rate.Value;
                    break;
                case 1: // PerTon
                    amount = grWeight.HasValue ? rate.Value * grWeight.Value : (decimal?)null;
                    break;
                case 2: // PerKm
                    amount = distanceKm.HasValue ? rate.Value * distanceKm.Value : (decimal?)null;
                    break;
                default:
                    return null;
            }

            if (amount.HasValue && minimumCharge.HasValue && amount.Value < minimumCharge.Value)
                amount = minimumCharge.Value;

            // F_WAYBILLS.Amount — decimal(19,4): EF ortiqcha xonalarni kesadi, shuning uchun bu yerda yaxlitlanadi
            return amount.HasValue ? Math.Round(amount.Value, 4, MidpointRounding.AwayFromZero) : (decimal?)null;
        }
    }
}
