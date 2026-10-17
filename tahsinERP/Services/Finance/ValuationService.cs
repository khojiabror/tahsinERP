using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
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

        /// <summary>EBITDA = operatsion foyda + foiz + soliq + amortizatsiya (xarajat kategoriyasi nomi bo'yicha).</summary>
        public EbitdaData Ebitda(DateTime fromDate, DateTime toDate)
        {
            var period = FinQueries.Active(_db).InPeriod(fromDate, toDate).RealCash();

            var operatingIncome = period
                .Where(t => t.Direction == 1)
                .Sum(t => (decimal?)t.Amount) ?? 0m;

            var expensesByCategory = period
                .Where(t => t.Direction == 2)
                .GroupBy(t => t.FIN_CATEGORIES.Name)
                .Select(g => new { Name = g.Key, Amount = g.Sum(x => (decimal?)x.Amount) ?? 0m })
                .ToList();

            decimal totalExpenses = 0m, taxExpense = 0m, interestExpense = 0m, depreciationExpense = 0m;
            foreach (var e in expensesByCategory)
            {
                totalExpenses += e.Amount;
                var name = (e.Name ?? string.Empty).ToLower();
                if (name.Contains("soliq") || name.Contains("qqs") || name.Contains("nds"))
                {
                    taxExpense += e.Amount;
                }
                else if (name.Contains("foiz") || name.Contains("kredit") || name.Contains("ssuda"))
                {
                    interestExpense += e.Amount;
                }
                else if (name.Contains("amortizatsiya") || name.Contains("eskirish") || name.Contains("iznos"))
                {
                    depreciationExpense += e.Amount;
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
        /// 2-uslub: sof aktivlar (Type 1 aktivlar − Type 2/3 passiv ustuni) + (yillik daromad × o'rtacha Tannarx marjasi) × N yil.
        /// </summary>
        public CompanyValuationBreakdown DcfValuation(DateTime asOfDate, int years)
        {
            var balance = _balanceSheet.Compute(asOfDate);
            var totalAssets = balance.Where(d => d.Category.Type == 1 && d.IsAssetColumn).Sum(d => d.Amount);
            var totalLiabilities = balance.Where(d => (d.Category.Type == 2 || d.Category.Type == 3) && !d.IsAssetColumn).Sum(d => d.Amount);
            var netAssets = totalAssets - totalLiabilities;

            var avgMarginPercent = AverageTannarxMarginPercent();

            var yearStart = asOfDate.AddYears(-1).AddDays(1);
            var annualRevenue = FinQueries.Active(_db)
                .Where(t => t.Direction == 1)
                .InPeriod(yearStart, asOfDate)
                .RealCash()
                .Sum(t => (decimal?)t.Amount) ?? 0m;

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
            var shareholders = _db.SHAREHOLDERS.AsNoTracking().Where(s => s.IsActive).ToList();
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
