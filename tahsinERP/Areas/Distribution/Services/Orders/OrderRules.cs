using System;
using System.Collections.Generic;
using System.Linq;

namespace tahsinERP.Areas.Distribution.Services.Orders
{
    /// <summary>
    /// Buyurtma holatlari (01_PHASE1_MVP.md, 5-bo'lim). Raqamlar bazada saqlanadi — o'zgartirilmaydi.
    /// </summary>
    public enum OrderStatus : byte
    {
        New = 0,              // yaratildi, rezerv yo'q, tahrirlanadi
        Confirmed = 1,        // limit/narx tekshirildi, tovar rezervda
        Picking = 2,          // omborda yig'ilmoqda
        PartiallyShipped = 3, // bir qismi jo'natildi, qolgani rezervda / kirimni kutmoqda
        Shipped = 4,          // to'liq jo'natildi (qarz yozildi)
        Delivered = 5,        // mijoz qabul qildi
        Closed = 6,           // qarz to'liq yopildi
        Cancelled = 9
    }

    /// <summary>Holat o'tishlari — yagona joy (web, bot, API bir xil qoidadan o'tadi).</summary>
    public static class OrderStateMachine
    {
        private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new Dictionary<OrderStatus, OrderStatus[]>
        {
            { OrderStatus.New, new[] { OrderStatus.Confirmed, OrderStatus.Cancelled } },
            { OrderStatus.Confirmed, new[] { OrderStatus.New, OrderStatus.Picking, OrderStatus.PartiallyShipped, OrderStatus.Shipped, OrderStatus.Cancelled } },
            { OrderStatus.Picking, new[] { OrderStatus.Confirmed, OrderStatus.PartiallyShipped, OrderStatus.Shipped, OrderStatus.Cancelled } },
            // jo'natish bekor qilinsa: qisman/to'liq -> qisman yoki tasdiqlangan
            { OrderStatus.PartiallyShipped, new[] { OrderStatus.PartiallyShipped, OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Confirmed, OrderStatus.Closed } },
            { OrderStatus.Shipped, new[] { OrderStatus.Delivered, OrderStatus.Closed, OrderStatus.PartiallyShipped, OrderStatus.Confirmed } },
            // yetkazish natijasi bekor qilinsa (reys yopilguncha) — yana "jo'natildi" (05_FINDINGS K2-9)
            { OrderStatus.Delivered, new[] { OrderStatus.Closed, OrderStatus.Shipped } },
            { OrderStatus.Closed, new OrderStatus[0] },
            { OrderStatus.Cancelled, new OrderStatus[0] },
        };

        public static bool CanMove(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

        public static void EnsureCanMove(OrderStatus from, OrderStatus to)
        {
            if (!CanMove(from, to))
                throw new DstValidationException(DstLocalization.T("Order_BadTransition",
                    DstLocalization.T("OrderStatus_" + from), DstLocalization.T("OrderStatus_" + to)));
        }

        /// <summary>Qatorlar va sarlavhani tahrirlash faqat yangi buyurtmada.</summary>
        public static bool IsEditable(OrderStatus s) => s == OrderStatus.New;

        /// <summary>Rezerv ushlab turadigan holatlar.</summary>
        public static bool HoldsReservation(OrderStatus s) =>
            s == OrderStatus.Confirmed || s == OrderStatus.Picking || s == OrderStatus.PartiallyShipped;

        public static bool CanShip(OrderStatus s) =>
            s == OrderStatus.Confirmed || s == OrderStatus.Picking || s == OrderStatus.PartiallyShipped;
    }

    /// <summary>Buyurtma qatori hisob-kitobi uchun kiritma.</summary>
    public sealed class LineCalc
    {
        public decimal Qty { get; set; }              // sotish birligida
        public decimal Factor { get; set; } = 1m;     // 1 birlik = Factor asosiy birlik
        public decimal UnitPrice { get; set; }        // sotish birligi narxi (chegirmasiz)
        public decimal DiscountPct { get; set; }      // qator chegirmasi, %
        public decimal? UnitWeightKg { get; set; }
        public decimal? UnitVolumeM3 { get; set; }

        public decimal QtyBase => UnitMath.ToBase(Qty, Factor);
        public decimal Gross => Math.Round(Qty * UnitPrice, 2, MidpointRounding.AwayFromZero);
        public decimal DiscountSum => Math.Round(Gross * DiscountPct / 100m, 2, MidpointRounding.AwayFromZero);
        public decimal Amount => Gross - DiscountSum;
        /// <summary>Chegirmadan keyingi bitta birlik narxi (minimal narx bilan solishtirish uchun).</summary>
        public decimal NetUnitPrice => Qty == 0 ? 0 : Math.Round(Amount / Qty, 2, MidpointRounding.AwayFromZero);
    }

    public sealed class OrderTotals
    {
        public decimal Subtotal { get; set; }      // qatorlar summasi (qator chegirmasidan keyin)
        public decimal OrderDiscountSum { get; set; }
        public decimal Total { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        public bool WeightComplete { get; set; }   // har qatorda og'irlik bor-yo'qligi
    }

    public static class OrderCalculator
    {
        /// <summary>Buyurtma umumiy chegirmasi (%) qatorlar summasidan keyin qo'llanadi.</summary>
        public static OrderTotals Totals(IReadOnlyCollection<LineCalc> lines, decimal orderDiscountPct)
        {
            DirectoryRules.ValidateDiscountPercent(orderDiscountPct);
            var subtotal = lines.Sum(l => l.Amount);
            var orderDiscount = Math.Round(subtotal * orderDiscountPct / 100m, 2, MidpointRounding.AwayFromZero);
            return new OrderTotals
            {
                Subtotal = subtotal,
                OrderDiscountSum = orderDiscount,
                Total = subtotal - orderDiscount,
                WeightKg = lines.Sum(l => (l.UnitWeightKg ?? 0) * l.Qty),
                VolumeM3 = lines.Sum(l => (l.UnitVolumeM3 ?? 0) * l.Qty),
                WeightComplete = lines.All(l => l.UnitWeightKg.HasValue)
            };
        }

        /// <summary>Qator chegirmasidan aksiya qismini ajratish: qo'lda berilgan foiz (DiscountPct = aksiya + qo'lda, ketma-ket).</summary>
        public static decimal ManualPct(decimal totalPct, decimal promoPct)
        {
            if (promoPct <= 0) return totalPct;
            if (promoPct >= 100) return 0;
            return Math.Max(0, Math.Round(100m - (100m - totalPct) * 100m / (100m - promoPct), 2));
        }

        /// <summary>Qator + buyurtma chegirmasi birgalikdagi samarali foizi (limit tekshiruvi uchun).</summary>
        public static decimal EffectiveDiscountPct(decimal linePct, decimal orderPct)
        {
            return Math.Round(100m - (100m - linePct) * (100m - orderPct) / 100m, 4);
        }
    }

    /// <summary>Mijoz qarzi holati (CUSTOMER_LEDGER dan).</summary>
    public sealed class CreditState
    {
        public decimal CreditLimit { get; set; }
        public decimal Balance { get; set; }       // > 0 — mijoz qarzdor
        public decimal Overdue { get; set; }       // muddati o'tgan qism
        /// <summary>Tasdiqlangan, hali jo'natilmagan buyurtmalar qoldig'i (qarzga hali yozilmagan, lekin limitni band qiladi).</summary>
        public decimal Committed { get; set; }
        public bool HasProfile { get; set; }
    }

    public sealed class CreditCheck
    {
        public bool Ok => Problems.Count == 0;
        public List<string> Problems { get; } = new List<string>();
        public decimal Available { get; set; }     // limitdan qolgan
    }

    public static class OrderRules
    {
        /// <summary>
        /// Qarz limiti: qarz + tasdiqlangan-jo'natilmagan buyurtmalar + shu buyurtma &gt; limit yoki muddati o'tgan qarz
        /// bo'lsa — tasdiqlab bo'lmaydi (DstOverrideCredit huquqi bilan ruxsat beriladi). Limit 0 — faqat oldindan to'lov.
        /// </summary>
        public static CreditCheck CheckCredit(CreditState credit, decimal orderTotal)
        {
            var result = new CreditCheck { Available = credit.CreditLimit - credit.Balance - credit.Committed };
            if (credit.Overdue > 0)
                result.Problems.Add(DstLocalization.T("Credit_Overdue", credit.Overdue.ToString("N0")));
            decimal exposure = credit.Balance + credit.Committed + orderTotal;
            if (exposure > credit.CreditLimit)
                result.Problems.Add(DstLocalization.T("Credit_LimitExceeded", exposure.ToString("N0"), credit.CreditLimit.ToString("N0"))
                    + (credit.Committed > 0 ? " " + DstLocalization.T("Credit_Committed", credit.Committed.ToString("N0")) : ""));
            return result;
        }

        /// <summary>
        /// Muddati o'tgan qarz: to'lovlar eng eski qarzlarni yopadi (FIFO), shuning uchun
        /// muddati o'tgan = max(0, muddati o'tgan debetlar − barcha kreditlar).
        /// </summary>
        public static decimal Overdue(decimal overdueDebits, decimal totalCredits)
        {
            return Math.Max(0, overdueDebits - totalCredits);
        }

        /// <summary>Chegirma limiti: foydalanuvchi rollaridagi eng katta limit; null — chegirma berolmaydi.</summary>
        public static void CheckDiscount(decimal effectivePct, decimal? maxAllowedPct, bool unlimited)
        {
            if (effectivePct <= 0 || unlimited) return;
            if (maxAllowedPct == null || effectivePct > maxAllowedPct.Value)
                throw new DstValidationException(DstLocalization.T("Discount_OverLimit",
                    effectivePct.ToString("0.##"), (maxAllowedPct ?? 0).ToString("0.##")));
        }

        /// <summary>Minimal narx: chegirmadan keyingi birlik narxi min narxdan past bo'lmasin.</summary>
        public static void CheckMinPrice(string product, decimal netUnitPrice, decimal? minUnitPrice, bool unlimited)
        {
            if (unlimited || minUnitPrice == null) return;
            if (netUnitPrice < minUnitPrice.Value)
                throw new DstValidationException(DstLocalization.T("MinPrice_Violation",
                    product, netUnitPrice.ToString("N0"), minUnitPrice.Value.ToString("N0")));
        }
    }
}
