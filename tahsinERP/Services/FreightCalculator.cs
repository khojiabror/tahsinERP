namespace tahsinERP.Services
{
    /// <summary>
    /// Navlun summasini hisoblash — server (Create/Edit/Excel import) va forma JS'i bir xil qoidada
    /// (LOG-B06: avval server MinimumCharge'ni e'tiborsiz qoldirardi, JS esa qo'llardi).
    /// FreightBasis: 0 — mashina uchun, 1 — ton uchun (GrWeight), 2 — km uchun (DistanceKm), 3 — belgilangan narx.
    /// </summary>
    public static class FreightCalculator
    {
        public static double? Calculate(byte? basis, double? rate, double? grWeight, double? distanceKm, double? minimumCharge = null)
        {
            if (!rate.HasValue || !basis.HasValue)
                return null;

            double? amount;
            switch (basis.Value)
            {
                case 0: // PerTruck
                case 3: // Flat
                    amount = rate.Value;
                    break;
                case 1: // PerTon
                    amount = grWeight.HasValue ? rate.Value * grWeight.Value : (double?)null;
                    break;
                case 2: // PerKm
                    amount = distanceKm.HasValue ? rate.Value * distanceKm.Value : (double?)null;
                    break;
                default:
                    return null;
            }

            if (amount.HasValue && minimumCharge.HasValue && amount.Value < minimumCharge.Value)
                amount = minimumCharge.Value;

            return amount;
        }
    }
}
