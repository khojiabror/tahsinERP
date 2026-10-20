using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Sotuv shartnomasi QQS hisobi — S_CONTRACT_PRODUCTS -> (baza = PiecePrice*Amount, QQS) adapteri.
    /// Matematikaning o'zi umumiy <see cref="VatMath"/>'da (PContractVatHelper bilan bir xil, CRM-D3).
    /// </summary>
    public static class SContractVatHelper
    {
        public static decimal GetLineBaseAmount(S_CONTRACT_PRODUCTS product)
        {
            if (product == null)
            {
                return 0;
            }

            return product.PiecePrice * product.Amount;
        }

        public static decimal GetLineBaseAmount(decimal piecePrice, decimal quantity)
        {
            return piecePrice * quantity;
        }

        public static decimal GetVatPercentFromLine(decimal lineBaseAmount, decimal? vatAmount)
        {
            return VatMath.PercentFromLine(lineBaseAmount, vatAmount);
        }

        public static decimal CalculateVatAmount(decimal lineBaseAmount, decimal vatPercent)
        {
            return VatMath.AmountFromPercent(lineBaseAmount, vatPercent);
        }

        public static decimal GetLineTotalWithVat(S_CONTRACT_PRODUCTS product)
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

        public static decimal CalculateContractTotalWithVat(IEnumerable<S_CONTRACT_PRODUCTS> products)
        {
            if (products == null)
            {
                return 0;
            }

            return Math.Round(products.Sum(GetLineTotalWithVat), MidpointRounding.AwayFromZero);
        }

        public static decimal CalculateContractVatRate(IEnumerable<S_CONTRACT_PRODUCTS> products)
        {
            if (products == null)
            {
                return 0;
            }

            return VatMath.RateFromLines(products.Select(p => new VatLine(p.ContractID, GetLineBaseAmount(p), p.vatamount)));
        }

        public static decimal CalculateContractVatRate(DBTHSNEntities db, int? contractId)
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
        public static Dictionary<int, decimal> GetProductVatRates(DBTHSNEntities db, int? contractId)
        {
            if (!contractId.HasValue || contractId.Value <= 0)
            {
                return new Dictionary<int, decimal>();
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

        public static decimal CalculateContractVatRateFromLines(IEnumerable<ContractProductVatLine> products)
        {
            if (products == null)
            {
                return 0;
            }

            return VatMath.RateFromLines(products.Select(ToVatLine));
        }

        public static Dictionary<int, decimal> BuildContractVatRates(IEnumerable<ContractProductVatLine> contractProducts)
        {
            return VatMath.RatesByContract((contractProducts ?? Enumerable.Empty<ContractProductVatLine>()).Select(ToVatLine));
        }

        private static VatLine ToVatLine(ContractProductVatLine line)
        {
            return new VatLine(line.ContractID, line.LineBaseAmount, line.VATAmount);
        }

        public class ContractProductVatLine
        {
            public int ContractID { get; set; }
            public decimal LineBaseAmount { get; set; }
            public decimal? VATAmount { get; set; }
        }
    }
}
