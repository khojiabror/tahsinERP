using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>Ma'lumotnomalar tekshiruvlari (sof funksiyalar — controller, import va bot bir xil qoidadan o'tadi).</summary>
    public static class DirectoryRules
    {
        /// <summary>
        /// STIR: yuridik shaxs — 9 raqam, jismoniy shaxs (JShShIR/ПИНФЛ) — 14 raqam. Bo'sh — ruxsat.
        /// Qaytaradi: normallashtirilgan qiymat (bo'shliqsiz) yoki null.
        /// </summary>
        public static string NormalizeInn(string inn)
        {
            if (string.IsNullOrWhiteSpace(inn)) return null;
            var digits = new string(inn.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
            if (!digits.All(char.IsDigit) || (digits.Length != 9 && digits.Length != 14))
                throw new DstValidationException(DstLocalization.T("Error_InnFormat"));
            return digits;
        }

        /// <summary>Telefon: faqat raqam va boshida '+'; O'zbekiston raqami 9 xonali bo'lsa +998 qo'shiladi.</summary>
        public static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 9) digits = "998" + digits;
            if (digits.Length < 9 || digits.Length > 15)
                throw new DstValidationException(DstLocalization.T("Error_PhoneFormat"));
            return "+" + digits;
        }

        public static void ValidateGeo(decimal? lat, decimal? lng)
        {
            if (lat.HasValue != lng.HasValue)
                throw new DstValidationException(DstLocalization.T("Error_GeoPair"));
            if (lat.HasValue && (lat < -90 || lat > 90 || lng < -180 || lng > 180))
                throw new DstValidationException(DstLocalization.T("Error_GeoRange"));
        }

        public static string NormalizeCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return null;
            var c = category.Trim().ToUpperInvariant();
            if (c != "A" && c != "B" && c != "C")
                throw new DstValidationException(DstLocalization.T("Error_CategoryAbc"));
            return c;
        }

        public static void ValidateCredit(decimal creditLimit, int paymentDays)
        {
            if (creditLimit < 0 || paymentDays < 0 || paymentDays > 365)
                throw new DstValidationException(DstLocalization.T("Error_CreditRange"));
        }

        /// <summary>Hudud ierarxiyasi: viloyatning ota-onasi yo'q, tuman — viloyat ostida, hudud — tuman ostida.</summary>
        public static byte TerritoryLevel(int? parentId, Func<int, Territory> find)
        {
            if (parentId == null) return Territory.Region;
            var parent = find(parentId.Value) ?? throw new DstValidationException(DstLocalization.T("Error_TerritoryParent"));
            if (parent.Level >= Territory.Area)
                throw new DstValidationException(DstLocalization.T("Error_TerritoryDepth"));
            return (byte)(parent.Level + 1);
        }

        /// <summary>Tsikl tekshiruvi: hududni o'z avlodi ostiga ko'chirib bo'lmaydi.</summary>
        public static void EnsureNoCycle(int territoryId, int? newParentId, Func<int, int?> parentOf)
        {
            var seen = new HashSet<int>();
            for (int? p = newParentId; p.HasValue; p = parentOf(p.Value))
            {
                if (p.Value == territoryId || !seen.Add(p.Value))
                    throw new DstValidationException(DstLocalization.T("Error_TerritoryCycle"));
            }
        }

        /// <summary>
        /// Shtrix-kod: 4–50 belgi, harf/raqam. EAN-8/EAN-13/UPC-A raqamli kodlarda nazorat raqami tekshiriladi.
        /// </summary>
        public static string NormalizeBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) throw new DstValidationException(DstLocalization.T("Error_BarcodeEmpty"));
            var code = barcode.Trim();
            if (code.Length < 4 || code.Length > 50 || !code.All(ch => char.IsLetterOrDigit(ch) || ch == '-'))
                throw new DstValidationException(DstLocalization.T("Error_BarcodeFormat"));
            if (code.All(char.IsDigit) && (code.Length == 8 || code.Length == 12 || code.Length == 13) && !GtinChecksumOk(code))
                throw new DstValidationException(DstLocalization.T("Error_BarcodeChecksum"));
            return code;
        }

        public static bool GtinChecksumOk(string digits)
        {
            int sum = 0;
            // o'ngdan: nazorat raqamidan oldingisi ×3, keyingisi ×1, ...
            for (int i = digits.Length - 2, w = 3; i >= 0; i--, w = w == 3 ? 1 : 3)
                sum += (digits[i] - '0') * w;
            int check = (10 - sum % 10) % 10;
            return check == digits[digits.Length - 1] - '0';
        }

        public static void ValidateDiscountPercent(decimal percent)
        {
            if (percent < 0 || percent > 100)
                throw new DstValidationException(DstLocalization.T("Error_PercentRange"));
        }
    }
}
