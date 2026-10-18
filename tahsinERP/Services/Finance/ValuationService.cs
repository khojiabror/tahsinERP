using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.ViewModels.Finance;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// EBITDA va kompaniya qiymati (EBITDA × multiplikator yoki "DCF": sof aktivlar + N yillik foyda).
    /// DCF balansni <see cref="BalanceSheetService"/> dan bir marta oladi (avval ikki marta to'liq hisoblanardi — FIN-P04).
    /// </summary>
    public sealed class ValuationService
    {
        private readonly DBTHSNEntities _db;
        private readonly BalanceSheetService _balanceSheet;

        public ValuationService(DBTHSNEntities db, BalanceSheetService balanceSheet = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _balanceSheet = balanceSheet ?? new BalanceSheetService(db);
        }

        /// <summary>
        /// P&amp;L bandlariga bog'langan kategoriyalar: daromad (Sign=1) va operatsion xarajat (Sign=2, dividendsiz).
        /// FIN-B05: avval barcha kirimlar daromad edi (qarz olish, ichki o'tkazma, aksiyador hissasi ham).
        /// Dividend — kategoriyaning tahliliy guruhi (FIN_CATEGORIES.AnalyticsGroup = DIVIDEND), band/kategoriya nomi emas.
        /// </summary>
        private void OperatingCategories(out HashSet<int> incomeCategoryIds, out HashSet<int> expenseCategoryIds)
        {
            var items = _db.FIN_PL_ITEMS
                .Where(i => i.IsActive && (i.IsDeleted == null || i.IsDeleted == false))
                .Select(i => new { i.Sign, Cats = i.FIN_CATEGORIES.Select(c => new { c.ID, c.AnalyticsGroup }) })
                .ToList();

            incomeCategoryIds = new HashSet<int>(items.Where(i => i.Sign == 1).SelectMany(i => i.Cats).Select(c => c.ID));
            var income = incomeCategoryIds;
            expenseCategoryIds = new HashSet<int>(items
                .Where(i => i.Sign != 1)
                .SelectMany(i => i.Cats)
                .Where(c => c.AnalyticsGroup != SystemCodes.FinAnalyticsGroup.Dividend && !income.Contains(c.ID))
                .Select(c => c.ID));
        }

        /// <summary>Davrdagi operatsion daromad: P&amp;L daromad kategoriyalari bo'yicha (kirim +, chiqim −).</summary>
        public decimal OperatingIncome(DateTime fromDate, DateTime toDate)
        {
            OperatingCategories(out var incomeIds, out _);
            if (incomeIds.Count == 0) return 0m;
            var ids = incomeIds.ToList();
            // FIN-B01: so'mda (kun kursi)
            return FinQueries.Active(_db).InPeriod(fromDate, toDate).RealCash()
                .Where(t => ids.Contains(t.CategoryID))
                .NetUzs(_db);
        }

        /// <summary>
        /// EBITDA = operatsion foyda + foiz + soliq + amortizatsiya.
        /// Daromad/xarajat — P&amp;L bandlari bo'yicha (FIN-B05); soliq (TAX, VAT) / foiz (INTEREST) / amortizatsiya (DEPRECIATION) —
        /// xarajat kategoriyasining tahliliy guruhidan (FIN_CATEGORIES.AnalyticsGroup).
        /// </summary>
        public EbitdaData Ebitda(DateTime fromDate, DateTime toDate)
        {
            OperatingCategories(out var incomeIds, out var expenseIds);
            var allIds = incomeIds.Concat(expenseIds).ToList();

            // FIN-B01: so'mda (kun kursi)
            var groups = allIds.Count == 0
                ? new Dictionary<int, string>()
                : _db.FIN_CATEGORIES.AsNoTracking().Where(c => allIds.Contains(c.ID)).Select(c => new { c.ID, c.AnalyticsGroup }).ToList().ToDictionary(c => c.ID, c => c.AnalyticsGroup);
            var sums = allIds.Count == 0
                ? new List<CategorySum>()
                : FinQueries.Active(_db).InPeriod(fromDate, toDate).RealCash()
                    .Where(t => allIds.Contains(t.CategoryID))
                    .UzsBuckets(_db)
                    .GroupBy(b => b.CategoryID)
                    .Select(g => new CategorySum
                    {
                        CategoryID = g.Key,
                        AnalyticsGroup = groups.TryGetValue(g.Key, out var grp) ? grp : null,
                        In = g.Where(b => b.Direction == 1).Sum(b => b.Uzs),
                        Out = g.Where(b => b.Direction != 1).Sum(b => b.Uzs)
                    })
                    .ToList();

            var operatingIncome = sums.Where(s => incomeIds.Contains(s.CategoryID)).Sum(s => s.In - s.Out);

            decimal totalExpenses = 0m, taxExpense = 0m, interestExpense = 0m, depreciationExpense = 0m;
            foreach (var e in sums.Where(s => expenseIds.Contains(s.CategoryID)))
            {
                var amount = e.Out - e.In;
                totalExpenses += amount;
                switch (e.AnalyticsGroup)
                {
                    case SystemCodes.FinAnalyticsGroup.Tax:
                    case SystemCodes.FinAnalyticsGroup.Vat:
                        taxExpense += amount;
                        break;
                    case SystemCodes.FinAnalyticsGroup.Interest:
                        interestExpense += amount;
                        break;
                    case SystemCodes.FinAnalyticsGroup.Depreciation:
                        depreciationExpense += amount;
                        break;
                }
            }

            return new EbitdaData
            {
                FromDate = fromDate,
                ToDate = toDate,
                OperatingIncome = operatingIncome,
                OperatingExpenses = totalExpenses,
                TaxExpense = taxExpense,
                InterestExpense = interestExpense,
                DepreciationExpense = depreciationExpense
            };
        }

        private sealed class CategorySum
        {
            public int CategoryID { get; set; }
            public string AnalyticsGroup { get; set; }
            public decimal In { get; set; }
            public decimal Out { get; set; }
        }

        /// <summary>1-uslub: oxirgi 12 oy EBITDA × multiplikator.</summary>
        public CompanyValuationBreakdown EbitdaValuation(DateTime asOfDate, decimal multiple)
        {
            var ebitda = Ebitda(asOfDate.AddYears(-1).AddDays(1), asOfDate);
            return new CompanyValuationBreakdown
            {
                Method = "ebitda",
                Ebitda = ebitda,
                EbitdaMultiple = multiple,
                CompanyValue = ebitda.Ebitda * multiple
            };
        }

        /// <summary>
        /// 2-uslub: sof aktivlar (aktiv ustuni − Type 2 majburiyatlar) + (yillik daromad × o'rtacha Tannarx marjasi) × N yil.
        /// </summary>
        public CompanyValuationBreakdown DcfValuation(DateTime asOfDate, int years)
        {
            // FIN-B04: aktivlar = balansning aktiv ustuni (Type 1 va aktiv nomli Type 2),
            // passivlar = Type 2 majburiyatlar; kapital (Type 3) sof aktivdan ayirilmaydi.
            var balance = _balanceSheet.Compute(asOfDate);
            var totalAssets = balance.Where(d => d.IsAssetColumn).Sum(d => d.Amount);
            var totalLiabilities = balance.Where(d => d.Category.Type == 2 && !d.IsAssetColumn).Sum(d => d.Amount);
            var netAssets = totalAssets - totalLiabilities;

            var avgMarginPercent = AverageTannarxMarginPercent();

            // Yillik daromad — EBITDA bilan bir xil asosda (P&L daromad kategoriyalari, FIN-B05)
            var annualRevenue = OperatingIncome(asOfDate.AddYears(-1).AddDays(1), asOfDate);

            var annualProfit = annualRevenue * avgMarginPercent / 100m;
            var futureProfit = annualProfit * years;

            return new CompanyValuationBreakdown
            {
                Method = "dcf",
                TotalAssets = totalAssets,
                TotalLiabilities = totalLiabilities,
                NetAssets = netAssets,
                AvgMarginPercent = avgMarginPercent,
                AnnualRevenue = annualRevenue,
                AnnualProfit = annualProfit,
                DcfYears = years,
                FutureProfit = futureProfit,
                CompanyValue = netAssets + futureProfit
            };
        }

        /// <summary>Tannarx modulidagi ProfitPercent o'rtachasi; ma'lumot bo'lmasa 10%.</summary>
        public decimal AverageTannarxMarginPercent()
        {
            var avg = _db.TANNARXes
                .Where(t => !t.IsDeleted && t.ProfitPercent != null)
                .Select(t => t.ProfitPercent.Value)
                .ToList();
            return avg.Count == 0 ? 10m : avg.Average();
        }

        /// <summary>Faol aksiyadorlar va asOf sanasidagi ulushlari bo'yicha qiymat (ulush davrlari bitta so'rov bilan).</summary>
        public List<ShareholderValueData> ShareholderValues(DateTime asOfDate, decimal companyValue)
        {
            // FIN-B12: o'chirilgan aksiyador qiymatdan ulush olmaydi
            var shareholders = _db.SHAREHOLDERS.AsNoTracking().Where(s => s.IsActive && s.IsDeleted != true).ToList();
            var ids = shareholders.Select(s => s.ID).ToList();
            var periods = ids.Count == 0
                ? new List<SHAREHOLDER_SHARE_PERIODS>()
                : _db.SHAREHOLDER_SHARE_PERIODS.AsNoTracking()
                    .Where(sp => ids.Contains(sp.ShareholderID) &&
                                 sp.FromDate <= asOfDate &&
                                 (sp.ToDate == null || sp.ToDate >= asOfDate))
                    .ToList();

            var result = new List<ShareholderValueData>();
            foreach (var shareholder in shareholders)
            {
                var currentShare = periods
                    .Where(sp => sp.ShareholderID == shareholder.ID)
                    .OrderByDescending(sp => sp.FromDate)
                    .FirstOrDefault();
                if (currentShare == null) continue;

                result.Add(new ShareholderValueData
                {
                    Shareholder = shareholder,
                    SharePercent = currentShare.SharePercent,
                    Value = companyValue * currentShare.SharePercent / 100
                });
            }
            return result;
        }
    }
}
