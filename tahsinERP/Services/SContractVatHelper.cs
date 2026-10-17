using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    public static class SContractVatHelper
    {
        public const double DefaultVatPercent = 12d;

        public static double GetLineBaseAmount(S_CONTRACT_PRODUCTS product)
        {
            if (product == null)
            {
                return 0;
            }

            return product.PiecePrice * product.Amount;
        }

        public static double GetLineBaseAmount(double piecePrice, double quantity)
        {
            return piecePrice * quantity;
        }

        public static double GetVatPercentFromLine(double lineBaseAmount, double? vatAmount)
        {
            if (lineBaseAmount <= 0 || !vatAmount.HasValue || vatAmount.Value <= 0)
            {
                return 0;
            }

            return Math.Round(vatAmount.Value / lineBaseAmount * 100d, 2);
        }

        public static double CalculateVatAmount(double lineBaseAmount, double vatPercent)
        {
            if (lineBaseAmount <= 0 || vatPercent <= 0)
            {
                return 0;
            }

            return lineBaseAmount * vatPercent / 100d;
        }

        public static double GetLineTotalWithVat(S_CONTRACT_PRODUCTS product)
        {
            if (product == null)
            {
                return 0;
            }

            if (product.totalamount.HasValue && product.totalamount.Value > 0)
            {
                return product.totalamount.Value;
            }

            var baseAmount = GetLineBaseAmount(product);
            return baseAmount + (product.vatamount ?? 0);
        }

        public static double CalculateContractTotalWithVat(IEnumerable<S_CONTRACT_PRODUCTS> products)
        {
            if (products == null)
            {
                return 0;
            }

            return Math.Round(products.Sum(GetLineTotalWithVat), MidpointRounding.AwayFromZero);
        }

        public static double CalculateContractVatRate(IEnumerable<S_CONTRACT_PRODUCTS> products)
        {
            if (products == null)
            {
                return 0;
            }

            var list = products.ToList();
            var baseAmount = list.Sum(GetLineBaseAmount);
            if (baseAmount <= 0)
            {
                return 0;
            }

            var vatAmount = list.Sum(p => p.vatamount ?? 0);
            if (vatAmount <= 0)
            {
                return 0;
            }

            return Math.Round(vatAmount / baseAmount * 100d, 2);
        }

        public static double CalculateContractVatRate(DBTHSNEntities db, int? contractId)
        {
            if (!contractId.HasValue || contractId.Value <= 0)
            {
                return 0;
            }

            var products = db.S_CONTRACT_PRODUCTS
                .Where(p => p.ContractID == contractId.Value)
                .ToList();

            return CalculateContractVatRate(products);
        }

        /// <summary>
        /// Shartnoma bo'yicha ProductID → QQS %. Bir mahsulot shartnomada takrorlansa, qatorlar
        /// yig'iladi (avval 5 joydagi ToDictionary ArgumentException tashlardi — CRM-B13, D4).
        /// </summary>
        public static Dictionary<int, double> GetProductVatRates(DBTHSNEntities db, int? contractId)
        {
            if (!contractId.HasValue || contractId.Value <= 0)
            {
                return new Dictionary<int, double>();
            }

            return db.S_CONTRACT_PRODUCTS.AsNoTracking()
                .Where(p => p.ContractID == contractId.Value)
                .Select(p => new { p.ProductID, p.PiecePrice, p.Amount, p.vatamount })
                .ToList()
                .GroupBy(p => p.ProductID)
                .ToDictionary(
                    g => g.Key,
                    g => GetVatPercentFromLine(
                        g.Sum(p => GetLineBaseAmount(p.PiecePrice, p.Amount)),
                        g.Sum(p => p.vatamount ?? 0)));
        }

        public static async Task<Dictionary<int, double>> GetProductVatRatesByContractAsync(DBTHSNEntities db, int contractId)
        {
            var products = await db.S_CONTRACT_PRODUCTS
                .Where(p => p.ContractID == contractId)
                .Select(p => new { p.ProductID, p.PiecePrice, p.Amount, p.vatamount })
                .ToListAsync();

            return products.ToDictionary(
                p => p.ProductID,
                p => GetVatPercentFromLine(p.PiecePrice * p.Amount, p.vatamount));
        }

        public static double CalculateContractVatRateFromLines(IEnumerable<ContractProductVatLine> products)
        {
            if (products == null)
            {
                return 0;
            }

            var list = products.ToList();
            var baseAmount = list.Sum(x => x.LineBaseAmount);
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

        public static double CalculateContractTotalWithVatFromForm(NameValueCollection form, string listPrefix = "ProductList")
        {
            double total = 0;
            int index = 0;
            while (form[$"{listPrefix}[{index}].ProductID"] != null)
            {
                var productIdStr = form[$"{listPrefix}[{index}].ProductID"];
                if (!string.IsNullOrEmpty(productIdStr) && productIdStr != "0")
                {
                    double piecePrice;
                    double quantity;
                    double.TryParse(form[$"{listPrefix}[{index}].PiecePrice"], out piecePrice);
                    double.TryParse(form[$"{listPrefix}[{index}].Amount"], out quantity);
                    var vatPercent = ParseVatPercentFromForm(form, index, listPrefix);
                    var lineBase = GetLineBaseAmount(piecePrice, quantity);
                    total += lineBase + CalculateVatAmount(lineBase, vatPercent);
                }

                index++;
            }

            return Math.Round(total, MidpointRounding.AwayFromZero);
        }

        public static double ParseVatPercentFromForm(NameValueCollection form, int index, string listPrefix = "ProductList")
        {
            var raw = form[$"{listPrefix}[{index}].VatPercent"];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0;
            }

            double vatPercent;
            if (!double.TryParse(raw, out vatPercent))
            {
                return 0;
            }

            return Math.Max(0, Math.Min(100, vatPercent));
        }

        public class ContractProductVatLine
        {
            public int ContractID { get; set; }
            public double LineBaseAmount { get; set; }
            public double? VATAmount { get; set; }
        }
    }
}
