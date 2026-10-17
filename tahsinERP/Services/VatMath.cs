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
        public static double PercentFromLine(double baseAmount, double? vatAmount)
        {
            if (baseAmount <= 0 || !vatAmount.HasValue || vatAmount.Value <= 0)
            {
                return 0;
            }

            return Math.Round(vatAmount.Value / baseAmount * 100d, 2);
        }

        /// <summary>Foizdan QQS summasi: baza * foiz / 100 (yaxlitlanmaydi).</summary>
        public static double AmountFromPercent(double baseAmount, double vatPercent)
        {
            if (baseAmount <= 0 || vatPercent <= 0)
            {
                return 0;
            }

            return baseAmount * vatPercent / 100d;
        }

        /// <summary>Bir nechta qator bo'yicha o'rtacha (vaznli) QQS stavkasi: ΣQQS / Σbaza * 100.</summary>
        public static double RateFromLines(IEnumerable<VatLine> lines)
        {
            if (lines == null)
            {
                return 0;
            }

            double baseAmount = 0;
            double vatAmount = 0;
            foreach (var line in lines)
            {
                baseAmount += line.Base;
                vatAmount += line.Vat ?? 0;
            }

            if (baseAmount <= 0 || vatAmount <= 0)
            {
                return 0;
            }

            return Math.Round(vatAmount / baseAmount * 100d, 2);
        }

        /// <summary>Shartnoma bo'yicha guruhlangan stavkalar (ContractID -> foiz).</summary>
        public static Dictionary<int, double> RatesByContract(IEnumerable<VatLine> lines)
        {
            return (lines ?? Enumerable.Empty<VatLine>())
                .GroupBy(l => l.ContractID)
                .ToDictionary(g => g.Key, g => RateFromLines(g));
        }

        /// <summary>Net summaga QQS qo'shadi (2 xona, AwayFromZero). Manfiy stavka 0 deb olinadi.</summary>
        public static double ApplyVat(double baseAmount, double vatRate)
        {
            if (vatRate <= 0)
            {
                return Math.Round(baseAmount, 2, MidpointRounding.AwayFromZero);
            }

            return Math.Round(baseAmount * (1 + vatRate / 100d), 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>Saqlangan brutto va netto summadan qo'llangan stavkani tiklaydi (0 dan kichik bo'lmaydi).</summary>
        public static double RateFromGrossNet(double netAmount, double grossAmount)
        {
            if (netAmount <= 0)
            {
                return 0;
            }

            var rate = Math.Round((grossAmount / netAmount - 1d) * 100d, 2, MidpointRounding.AwayFromZero);
            return rate < 0 ? 0 : rate;
        }

        /// <summary>Foizni 0..100 oralig'iga keltiradi.</summary>
        public static double ClampPercent(double vatPercent)
        {
            return Math.Max(0, Math.Min(100, vatPercent));
        }

        /// <summary>Formadan kelgan matnni foizga aylantiradi (bo'sh/noto'g'ri bo'lsa 0, 0..100 ga cheklanadi).</summary>
        public static double ParsePercent(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0;
            }

            double value;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return 0;
            }

            return ClampPercent(value);
        }
    }

    /// <summary>QQS hisobi uchun qator: shartnoma, baza (QQSsiz summa) va QQS summasi.</summary>
    public struct VatLine
    {
        public VatLine(int contractId, double baseAmount, double? vat)
        {
            ContractID = contractId;
            Base = baseAmount;
            Vat = vat;
        }

        public int ContractID { get; }
        public double Base { get; }
        public double? Vat { get; }
    }
}
