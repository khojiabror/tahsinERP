using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace tahsinERP.Services
{
    /// <summary>
    /// QQS (VAT) bo'yicha sof hisob-kitob — entity'larga bog'lanmagan.
    /// PContractVatHelper (xarid) va SContractVatHelper (sotuv) bir xil matematikani
    /// alohida-alohida takrorlardi (SRM-D11); ikkalasi faqat qator bazasi qayerdan
    /// olinishi bilan farq qiladi (P: Amount, S: PiecePrice*Amount). Endi hisob shu yerda,
    /// helperlar esa entity -> (baza, QQS) moslashtiruvchi yupqa qatlam.
    /// </summary>
    public static class VatMath
    {
        /// <summary>Qatordagi QQS foizi: vat / baza * 100 (2 xona). Baza yoki QQS bo'lmasa 0.</summary>
        public static decimal PercentFromLine(decimal baseAmount, decimal? vatAmount)
        {
            if (baseAmount <= 0 || !vatAmount.HasValue || vatAmount.Value <= 0)
            {
                return 0;
            }

            return Math.Round(vatAmount.Value / baseAmount * 100m, 2);
        }

        /// <summary>Foizdan QQS summasi: baza * foiz / 100 (yaxlitlanmaydi).</summary>
        public static decimal AmountFromPercent(decimal baseAmount, decimal vatPercent)
        {
            if (baseAmount <= 0 || vatPercent <= 0)
            {
                return 0;
            }

            return baseAmount * vatPercent / 100m;
        }

        /// <summary>Bir nechta qator bo'yicha o'rtacha (vaznli) QQS stavkasi: ΣQQS / Σbaza * 100.</summary>
        public static decimal RateFromLines(IEnumerable<VatLine> lines)
        {
            if (lines == null)
            {
                return 0;
            }

            decimal baseAmount = 0;
            decimal vatAmount = 0;
            foreach (var line in lines)
            {
                baseAmount += line.Base;
                vatAmount += line.Vat ?? 0;
            }

            if (baseAmount <= 0 || vatAmount <= 0)
            {
                return 0;
            }

            return Math.Round(vatAmount / baseAmount * 100m, 2);
        }

        /// <summary>Shartnoma bo'yicha guruhlangan stavkalar (ContractID -> foiz).</summary>
        public static Dictionary<int, decimal> RatesByContract(IEnumerable<VatLine> lines)
        {
            return (lines ?? Enumerable.Empty<VatLine>())
                .GroupBy(l => l.ContractID)
                .ToDictionary(g => g.Key, g => RateFromLines(g));
        }

        /// <summary>Net summaga QQS qo'shadi (2 xona, AwayFromZero). Manfiy stavka 0 deb olinadi.</summary>
        public static decimal ApplyVat(decimal baseAmount, decimal vatRate)
        {
            if (vatRate <= 0)
            {
                return Math.Round(baseAmount, 2, MidpointRounding.AwayFromZero);
            }

            return Math.Round(baseAmount * (1 + vatRate / 100m), 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>Saqlangan brutto va netto summadan qo'llangan stavkani tiklaydi (0 dan kichik bo'lmaydi).</summary>
        public static decimal RateFromGrossNet(decimal netAmount, decimal grossAmount)
        {
            if (netAmount <= 0)
            {
                return 0;
            }

            var rate = Math.Round((grossAmount / netAmount - 1m) * 100m, 2, MidpointRounding.AwayFromZero);
            return rate < 0 ? 0 : rate;
        }

        /// <summary>Foizni 0..100 oralig'iga keltiradi.</summary>
        public static decimal ClampPercent(decimal vatPercent)
        {
            return Math.Max(0m, Math.Min(100m, vatPercent));
        }

        /// <summary>Formadan kelgan matnni foizga aylantiradi (bo'sh/noto'g'ri bo'lsa 0, 0..100 ga cheklanadi).</summary>
        public static decimal ParsePercent(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0;
            }

            decimal value;
            if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                !decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return 0;
            }

            return ClampPercent(value);
        }
    }

    /// <summary>QQS hisobi uchun qator: shartnoma, baza (QQSsiz summa) va QQS summasi.</summary>
    public struct VatLine
    {
        public VatLine(int contractId, decimal baseAmount, decimal? vat)
        {
            ContractID = contractId;
            Base = baseAmount;
            Vat = vat;
        }

        public int ContractID { get; }
        public decimal Base { get; }
        public decimal? Vat { get; }
    }
}
