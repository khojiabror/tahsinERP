using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.Entity;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    /// <summary>
    /// Ombor oylik hisoboti — kirim/chiqim va qoldiqlar.
    /// Ma'lumotlar INCOME va EXPENSE jadvalaridan dynamic hisoblanadi, DB ga yozish shart emas.
    /// </summary>
    public static class WarehouseMonthlyReportService
    {
        private const string InventarizatsiyaToken = "inventarizatsiya";

        /// <summary>
        /// Oy oralig'i: [periodStart, periodEndExclusive) — oxirgi kunning 23:59:59 ham kiradi.
        /// </summary>
        private static void GetPeriodBounds(int year, int month, out DateTime periodStart, out DateTime periodEndExclusive)
        {
            periodStart = new DateTime(year, month, 1);
            periodEndExclusive = periodStart.AddMonths(1);
        }

        private static bool ContainsInventarizatsiya(string text)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(InventarizatsiyaToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Inventory/Adjust yaratgan hujjatlar: Description/Comment yoki INV_MM_DD_IN|OUT DocNo.
        /// </summary>
        private static bool IsInventarizatsiyaIncome(INCOME income, string lineComment)
        {
            if (income == null)
            {
                return false;
            }

            if (ContainsInventarizatsiya(income.Description) || ContainsInventarizatsiya(lineComment))
            {
                return true;
            }

            return IsInventoryAdjustmentDocNo(income.DocNo, isIncome: true);
        }

        private static bool IsInventarizatsiyaExpense(EXPENS expense, string lineComment)
        {
            if (expense == null)
            {
                return false;
            }

            if (ContainsInventarizatsiya(expense.Description) || ContainsInventarizatsiya(lineComment))
            {
                return true;
            }

            return IsInventoryAdjustmentDocNo(expense.DocNo, isIncome: false);
        }

        private static bool IsInventoryAdjustmentDocNo(string docNo, bool isIncome)
        {
            if (string.IsNullOrWhiteSpace(docNo) || !docNo.StartsWith("INV_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var marker = isIncome ? "_IN_" : "_OUT_";
            return docNo.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static async Task<WarehouseMonthlyReportViewModel> BuildReportAsync(
            DBTHSNEntities db,
            int warehouseId,
            int year,
            int month,
            string companyName = null,
            string viewMode = "quantity")
        {
            var warehouse = await db.WAREHOUSES
                .FirstOrDefaultAsync(w => w.ID == warehouseId && w.IsDeleted != true);
            if (warehouse == null)
                return null;

            GetPeriodBounds(year, month, out var periodStart, out var periodEndExclusive);

            // Oldingi qoldiq = faqat oldingi oydan qolib o'tgan (oy 1-kuni oldidagi barcha harakatlardan hisoblangan qoldiq)
            // Ya'ni: oy boshidagi qoldiq = (1-kungacha bo'lgan barcha kirim) − (1-kungacha bo'lgan barcha chiqim)
            var incomeBefore = await (from ip in db.INCOME_PRODUCTS
                                     join i in db.INCOMES on ip.IncomeID equals i.ID
                                     where i.ReceivingWHID == warehouseId
                                           && i.IssueDateTime < periodStart
                                           && (i.IsDeleted == null || i.IsDeleted == false)
                                           && i.RecieveStatus == true
                                     group ip by ip.ProductID into g
                                     select new { ProductId = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync();

            var expenseBefore = await (from ep in db.EXPENSE_PRODUCTS
                                      join e in db.EXPENSES on ep.ExpenseID equals e.ID
                                      where e.SenderWHID == warehouseId
                                            && e.IssueDateTime < periodStart
                                            && e.IsDeleted == false
                                      group ep by ep.ProductID into g
                                      select new { ProductId = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync();

            var openingByProduct = new Dictionary<int, double>();
            foreach (var x in incomeBefore)
                openingByProduct[x.ProductId] = x.Total;
            foreach (var x in expenseBefore)
            {
                if (!openingByProduct.ContainsKey(x.ProductId))
                    openingByProduct[x.ProductId] = 0;
                openingByProduct[x.ProductId] -= x.Total;
            }

            // Oy ichidagi barcha kirim/chiqim (keyin operatsion vs inventarizatsiya ajratiladi)
            var incomeLinesInPeriod = await (from ip in db.INCOME_PRODUCTS
                                            join i in db.INCOMES on ip.IncomeID equals i.ID
                                            where i.ReceivingWHID == warehouseId
                                                  && i.IssueDateTime >= periodStart
                                                  && i.IssueDateTime < periodEndExclusive
                                                  && (i.IsDeleted == null || i.IsDeleted == false)
                                                  && i.RecieveStatus == true
                                            select new { ip.ProductID, ip.Amount, ip.Comment, ip.TotalPrice, ip.PiecePrice, Income = i })
                .ToListAsync();

            var expenseLinesInPeriod = await (from ep in db.EXPENSE_PRODUCTS
                                           join e in db.EXPENSES on ep.ExpenseID equals e.ID
                                           where e.SenderWHID == warehouseId
                                                 && e.IssueDateTime >= periodStart
                                                 && e.IssueDateTime < periodEndExclusive
                                                 && e.IsDeleted == false
                                           select new { ep.ProductID, ep.Amount, ep.Comment, ep.TotalPrice, Expense = e })
                .ToListAsync();

            var incomeInPeriod = incomeLinesInPeriod
                .Where(x => !IsInventarizatsiyaIncome(x.Income, x.Comment))
                .GroupBy(x => x.ProductID)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(x => x.Amount) })
                .ToList();

            var expenseInPeriod = expenseLinesInPeriod
                .Where(x => !IsInventarizatsiyaExpense(x.Expense, x.Comment))
                .GroupBy(x => x.ProductID)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(x => x.Amount) })
                .ToList();

            // Inventarizatsiya: shu oy ichidagi adjustment (net = qo'shimcha kirim - kamaytirish chiqimi)
            var adjustmentIncomeInPeriod = incomeLinesInPeriod
                .Where(x => IsInventarizatsiyaIncome(x.Income, x.Comment))
                .GroupBy(x => x.ProductID)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(x => x.Amount) })
                .ToList();

            var adjustmentExpenseInPeriod = expenseLinesInPeriod
                .Where(x => IsInventarizatsiyaExpense(x.Expense, x.Comment))
                .GroupBy(x => x.ProductID)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(x => x.Amount) })
                .ToList();

            var incomeByProduct = incomeInPeriod.ToDictionary(x => x.ProductId, x => x.Total);
            var expenseByProduct = expenseInPeriod.ToDictionary(x => x.ProductId, x => x.Total);
            var adjustmentInByProduct = adjustmentIncomeInPeriod.ToDictionary(x => x.ProductId, x => x.Total);
            var adjustmentOutByProduct = adjustmentExpenseInPeriod.ToDictionary(x => x.ProductId, x => x.Total);

            // Summa bo'yicha: har bir harakatda TotalPrice (yoki Amount*PiecePrice) yig'indisi
            var incomeBeforeSum = await (from ip in db.INCOME_PRODUCTS
                                        join i in db.INCOMES on ip.IncomeID equals i.ID
                                        where i.ReceivingWHID == warehouseId
                                              && i.IssueDateTime < periodStart
                                              && (i.IsDeleted == null || i.IsDeleted == false)
                                              && i.RecieveStatus == true
                                        group ip by ip.ProductID into g
                                        select new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice ?? x.Amount * (x.PiecePrice ?? 0)) }).ToListAsync();
            var expenseBeforeSum = await (from ep in db.EXPENSE_PRODUCTS
                                         join e in db.EXPENSES on ep.ExpenseID equals e.ID
                                         where e.SenderWHID == warehouseId && e.IssueDateTime < periodStart && e.IsDeleted == false
                                         group ep by ep.ProductID into g
                                         select new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice) }).ToListAsync();
            var incomeInPeriodSum = incomeLinesInPeriod
                .Where(x => !IsInventarizatsiyaIncome(x.Income, x.Comment))
                .GroupBy(x => x.ProductID)
                .Select(g => new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice ?? x.Amount * (x.PiecePrice ?? 0)) })
                .ToList();

            var expenseInPeriodSum = expenseLinesInPeriod
                .Where(x => !IsInventarizatsiyaExpense(x.Expense, x.Comment))
                .GroupBy(x => x.ProductID)
                .Select(g => new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice) })
                .ToList();

            var adjustmentIncomeInPeriodSum = incomeLinesInPeriod
                .Where(x => IsInventarizatsiyaIncome(x.Income, x.Comment))
                .GroupBy(x => x.ProductID)
                .Select(g => new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice ?? x.Amount * (x.PiecePrice ?? 0)) })
                .ToList();

            var adjustmentExpenseInPeriodSum = expenseLinesInPeriod
                .Where(x => IsInventarizatsiyaExpense(x.Expense, x.Comment))
                .GroupBy(x => x.ProductID)
                .Select(g => new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice) })
                .ToList();

            var openingSumByProduct = new Dictionary<int, decimal>();
            foreach (var x in incomeBeforeSum)
                openingSumByProduct[x.ProductId] = (decimal)x.TotalSum;
            foreach (var x in expenseBeforeSum)
            {
                if (!openingSumByProduct.ContainsKey(x.ProductId))
                    openingSumByProduct[x.ProductId] = 0;
                openingSumByProduct[x.ProductId] -= (decimal)x.TotalSum;
            }
            var incomeSumByProduct = incomeInPeriodSum.ToDictionary(x => x.ProductId, x => (decimal)x.TotalSum);
            var expenseSumByProduct = expenseInPeriodSum.ToDictionary(x => x.ProductId, x => (decimal)x.TotalSum);
            var adjustmentInSumByProduct = adjustmentIncomeInPeriodSum.ToDictionary(x => x.ProductId, x => (decimal)x.TotalSum);
            var adjustmentOutSumByProduct = adjustmentExpenseInPeriodSum.ToDictionary(x => x.ProductId, x => (decimal)x.TotalSum);

            var allProductIds = new HashSet<int>();
            foreach (var k in openingByProduct.Keys) allProductIds.Add(k);
            foreach (var k in incomeByProduct.Keys) allProductIds.Add(k);
            foreach (var k in expenseByProduct.Keys) allProductIds.Add(k);
            foreach (var k in adjustmentInByProduct.Keys) allProductIds.Add(k);
            foreach (var k in adjustmentOutByProduct.Keys) allProductIds.Add(k);

            if (allProductIds.Count == 0)
            {
                return new WarehouseMonthlyReportViewModel
                {
                    SelectedWarehouseId = warehouseId,
                    SelectedWarehouseName = warehouse.WHName,
                    Year = year,
                    Month = month,
                    ViewMode = viewMode ?? "quantity",
                    ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                    CompanyName = companyName ?? "",
                    Rows = new List<WarehouseMonthlyReportRowViewModel>(),
                    Totals = new WarehouseMonthlyReportTotalsViewModel(),
                    Warehouses = new List<System.Web.Mvc.SelectListItem>()
                };
            }

            var products = await db.PRODUCTS
                .Include(p => p.UNIT)
                .Where(p => allProductIds.Contains(p.ID))
                .ToListAsync();
            var productDict = products.ToDictionary(p => p.ID);

            var rows = new List<WarehouseMonthlyReportRowViewModel>();
            double sumOpen = 0, sumAdj = 0, sumIn = 0, sumOut = 0, sumClose = 0;
            decimal sumOpenSum = 0, sumAdjSum = 0, sumInSum = 0, sumOutSum = 0, sumCloseSum = 0;

            foreach (var pid in allProductIds.OrderBy(id => productDict.ContainsKey(id) ? productDict[id].PNo ?? "" : ""))
            {
                openingByProduct.TryGetValue(pid, out var open);
                incomeByProduct.TryGetValue(pid, out var inc);
                expenseByProduct.TryGetValue(pid, out var exp);
                adjustmentInByProduct.TryGetValue(pid, out var adjIn);
                adjustmentOutByProduct.TryGetValue(pid, out var adjOut);
                var adj = adjIn - adjOut;
                var close = open + adj + inc - exp;

                openingSumByProduct.TryGetValue(pid, out var openSum);
                incomeSumByProduct.TryGetValue(pid, out var incSum);
                expenseSumByProduct.TryGetValue(pid, out var expSum);
                adjustmentInSumByProduct.TryGetValue(pid, out var adjInSum);
                adjustmentOutSumByProduct.TryGetValue(pid, out var adjOutSum);
                var adjSum = adjInSum - adjOutSum;
                var closeSum = openSum + adjSum + incSum - expSum;

                sumOpen += open;
                sumAdj += adj;
                sumIn += inc;
                sumOut += exp;
                sumClose += close;
                sumOpenSum += openSum;
                sumAdjSum += adjSum;
                sumInSum += incSum;
                sumOutSum += expSum;
                sumCloseSum += closeSum;

                productDict.TryGetValue(pid, out var prod);
                rows.Add(new WarehouseMonthlyReportRowViewModel
                {
                    ProductId = pid,
                    ProductCode = prod?.PNo ?? pid.ToString(),
                    ProductName = prod?.Name ?? "",
                    UnitName = prod?.UNIT?.ShortName ?? prod?.UNIT?.UnitName ?? "",
                    OpeningBalance = open,
                    AdjustmentQty = adj,
                    IncomeQty = inc,
                    ExpenseQty = exp,
                    ClosingBalance = close,
                    OpeningBalanceSum = openSum,
                    AdjustmentSum = adjSum,
                    IncomeSum = incSum,
                    ExpenseSum = expSum,
                    ClosingBalanceSum = closeSum
                });
            }

            var warehouses = await db.WAREHOUSES
                .Where(w => w.IsDeleted != true)
                .OrderBy(w => w.WHName)
                .Select(w => new System.Web.Mvc.SelectListItem
                {
                    Value = w.ID.ToString(),
                    Text = w.WHName,
                    Selected = w.ID == warehouseId
                })
                .ToListAsync();

            return new WarehouseMonthlyReportViewModel
            {
                SelectedWarehouseId = warehouseId,
                SelectedWarehouseName = warehouse.WHName,
                Year = year,
                Month = month,
                ViewMode = viewMode ?? "quantity",
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                CompanyName = companyName ?? "",
                Rows = rows,
                Totals = new WarehouseMonthlyReportTotalsViewModel
                {
                    TotalOpening = sumOpen,
                    TotalAdjustment = sumAdj,
                    TotalIncome = sumIn,
                    TotalExpense = sumOut,
                    TotalClosing = sumClose,
                    TotalOpeningSum = sumOpenSum,
                    TotalAdjustmentSum = sumAdjSum,
                    TotalIncomeSum = sumInSum,
                    TotalExpenseSum = sumOutSum,
                    TotalClosingSum = sumCloseSum
                },
                Warehouses = warehouses
            };
        }
    }
}
