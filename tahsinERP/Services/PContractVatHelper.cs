using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    public static class PContractVatHelper
    {
        public static double GetVatPercentFromLine(double amount, double? vatAmount)
        {
            if (amount <= 0 || !vatAmount.HasValue || vatAmount.Value <= 0)
            {
                return 0;
            }

            return Math.Round(vatAmount.Value / amount * 100d, 2);
        }

        public static double CalculateVatAmount(double lineAmount, double vatPercent)
        {
            if (lineAmount <= 0 || vatPercent <= 0)
            {
                return 0;
            }

            return lineAmount * vatPercent / 100d;
        }

        public static double CalculateLineTotalWithVat(double lineAmount, double? vatAmount)
        {
            return lineAmount + (vatAmount ?? 0);
        }

        public static double CalculateContractVatRate(IEnumerable<P_CONTRACT_PRODUCTS> products)
        {
            if (products == null)
            {
                return 0;
            }

            var list = products.ToList();
            var baseAmount = list.Sum(p => p.Amount);
            if (baseAmount <= 0)
            {
                return 0;
            }

            var vatAmount = list.Sum(p => p.VATAmount ?? 0);
            if (vatAmount <= 0)
            {
                return 0;
            }

            return Math.Round(vatAmount / baseAmount * 100d, 2);
        }

        public static async Task<double> CalculateContractVatRateAsync(DBTHSNEntities db, int? contractId)
        {
            if (!contractId.HasValue || contractId.Value <= 0)
            {
                return 0;
            }

            var products = await db.P_CONTRACT_PRODUCTS
                .Where(p => p.ContractID == contractId.Value)
                .ToListAsync();

            return CalculateContractVatRate(products);
        }

        public static async Task<Dictionary<int, double>> GetProductVatRatesByContractAsync(DBTHSNEntities db, int contractId)
        {
            var products = await db.P_CONTRACT_PRODUCTS
                .Where(p => p.ContractID == contractId)
                .Select(p => new { p.ProductID, p.Amount, p.VATAmount })
                .ToListAsync();

            return products.ToDictionary(
                p => p.ProductID,
                p => GetVatPercentFromLine(p.Amount, p.VATAmount));
        }

        public static double CalculateContractVatRateFromLines(IEnumerable<ContractProductVatLine> products)
        {
            if (products == null)
            {
                return 0;
            }

            var list = products.ToList();
            var baseAmount = list.Sum(x => x.Amount);
            if (baseAmount <= 0)
            {
                return 0;
            }

            var vatAmount = list.Sum(x => x.VATAmount ?? 0);
            if (vatAmount <= 0)
            {
                return 0;
            }

            return Math.Round(vatAmount / baseAmount * 100d, 2);
        }

        public static Dictionary<int, double> BuildContractVatRates(IEnumerable<ContractProductVatLine> contractProducts)
        {
            return contractProducts
                .GroupBy(p => p.ContractID)
                .ToDictionary(
                    g => g.Key,
                    g => CalculateContractVatRateFromLines(g));
        }

        public class ContractProductVatLine
        {
            public int ContractID { get; set; }
            public double Amount { get; set; }
            public double? VATAmount { get; set; }
        }

        public static double ParseVatPercentFromForm(System.Collections.Specialized.NameValueCollection form, int index)
        {
            var raw = form[$"ContractProducts[{index}].VatPercent"];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0;
            }

            if (!double.TryParse(raw, out var vatPercent))
            {
                return 0;
            }

            return Math.Max(0, Math.Min(100, vatPercent));
        }
    }
}
