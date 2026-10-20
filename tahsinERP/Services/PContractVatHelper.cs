using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Xarid shartnomasi (P_CONTRACT_PRODUCTS) QQS adapteri. Hisob-kitob <see cref="VatMath"/>da;
    /// bu yerda faqat qator bazasi = P_CONTRACT_PRODUCTS.Amount (DB'da Price*Quantity).
    /// </summary>
    public static class PContractVatHelper
    {
        public static decimal GetVatPercentFromLine(decimal amount, decimal? vatAmount)
        {
            return VatMath.PercentFromLine(amount, vatAmount);
        }

        public static decimal CalculateLineTotalWithVat(decimal lineAmount, decimal? vatAmount)
        {
            return lineAmount + (vatAmount ?? 0);
        }

        public static async Task<decimal> CalculateContractVatRateAsync(DBTHSNEntities db, int? contractId)
        {
            if (!contractId.HasValue || contractId.Value <= 0)
            {
                return 0;
            }

            // Faqat ikki ustun kerak — butun entity'ni kuzatuv bilan yuklash shart emas (SRM-P10)
            var lines = await db.P_CONTRACT_PRODUCTS
                .AsNoTracking()
                .Where(p => p.ContractID == contractId.Value)
                .Select(p => new ContractProductVatLine
                {
                    ContractID = p.ContractID,
                    Amount = p.Amount,
                    VATAmount = p.VATAmount
                })
                .ToListAsync();

            return CalculateContractVatRateFromLines(lines);
        }

        public static async Task<Dictionary<int, decimal>> GetProductVatRatesByContractAsync(DBTHSNEntities db, int contractId)
        {
            var products = await db.P_CONTRACT_PRODUCTS
                .Where(p => p.ContractID == contractId)
                .Select(p => new { p.ProductID, p.Amount, p.VATAmount })
                .ToListAsync();

            return products.ToDictionary(
                p => p.ProductID,
                p => VatMath.PercentFromLine(p.Amount, p.VATAmount));
        }

        private static decimal CalculateContractVatRateFromLines(IEnumerable<ContractProductVatLine> products)
        {
            if (products == null)
            {
                return 0;
            }

            return VatMath.RateFromLines(products.Select(ToVatLine));
        }

        private static Dictionary<int, decimal> BuildContractVatRates(IEnumerable<ContractProductVatLine> contractProducts)
        {
            return VatMath.RatesByContract(contractProducts.Select(ToVatLine));
        }

        /// <summary>Bir nechta shartnomaning QQS stavkalari (bitta so'rov).</summary>
        public static async Task<Dictionary<int, decimal>> GetContractVatRatesAsync(DBTHSNEntities db, ICollection<int> contractIds)
        {
            if (contractIds == null || contractIds.Count == 0)
            {
                return new Dictionary<int, decimal>();
            }

            var lines = await db.P_CONTRACT_PRODUCTS
                .AsNoTracking()
                .Where(p => contractIds.Contains(p.ContractID))
                .Select(p => new ContractProductVatLine
                {
                    ContractID = p.ContractID,
                    Amount = p.Amount,
                    VATAmount = p.VATAmount
                })
                .ToListAsync();

            return BuildContractVatRates(lines);
        }

        private static VatLine ToVatLine(ContractProductVatLine p)
        {
            return new VatLine(p.ContractID, p.Amount, p.VATAmount);
        }

        public sealed class ContractProductVatLine
        {
            public int ContractID { get; set; }
            public decimal Amount { get; set; }
            public decimal? VATAmount { get; set; }
        }
    }
}
