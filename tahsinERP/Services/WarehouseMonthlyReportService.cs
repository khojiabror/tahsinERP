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

            // Tanlangan oy: 1-kunidan oxirigacha (shu oydagi kirim/chiqim)
            var periodStart = new DateTime(year, month, 1);  // oyning birinchi kuni 00:00
            var periodEnd = periodStart.AddMonths(1).AddDays(-1); // oyning oxirgi kuni

            // Oldingi qoldiq = faqat oldingi oydan qolib o'tgan (oy 1-kuni oldidagi barcha harakatlardan hisoblangan qoldiq)
            // Ya'ni: oy boshidagi qoldiq = (1-kungacha bo'lgan barcha kirim) − (1-kungacha bo'lgan barcha chiqim)
            var incomeBefore = await (from ip in db.INCOME_PRODUCTS
                                     join i in db.INCOMES on ip.IncomeID equals i.ID
                                     where i.ReceivingWHID == warehouseId
                                           && i.IssueDateTime < periodStart
                                           && (i.IsDeleted == null || i.IsDeleted == false)
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

            // Kirim: faqat shu oy ichidagi kirim (oy 1-kunidan oxirigacha)
            var incomeInPeriod = await (from ip in db.INCOME_PRODUCTS
                                       join i in db.INCOMES on ip.IncomeID equals i.ID
                                       where i.ReceivingWHID == warehouseId
                                             && i.IssueDateTime >= periodStart
                                             && i.IssueDateTime <= periodEnd
                                             && (i.IsDeleted == null || i.IsDeleted == false)
                                       group ip by ip.ProductID into g
                                       select new { ProductId = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync();

            // Chiqim: faqat shu oy ichidagi chiqim (oy 1-kunidan oxirigacha)
            var expenseInPeriod = await (from ep in db.EXPENSE_PRODUCTS
                                        join e in db.EXPENSES on ep.ExpenseID equals e.ID
                                        where e.SenderWHID == warehouseId
                                              && e.IssueDateTime >= periodStart
                                              && e.IssueDateTime <= periodEnd
                                              && e.IsDeleted == false
                                        group ep by ep.ProductID into g
                                        select new { ProductId = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync();

            var incomeByProduct = incomeInPeriod.ToDictionary(x => x.ProductId, x => x.Total);
            var expenseByProduct = expenseInPeriod.ToDictionary(x => x.ProductId, x => x.Total);

            // Summa bo'yicha: har bir harakatda TotalPrice (yoki Amount*PiecePrice) yig'indisi
            var incomeBeforeSum = await (from ip in db.INCOME_PRODUCTS
                                        join i in db.INCOMES on ip.IncomeID equals i.ID
                                        where i.ReceivingWHID == warehouseId
                                              && i.IssueDateTime < periodStart
                                              && (i.IsDeleted == null || i.IsDeleted == false)
                                        group ip by ip.ProductID into g
                                        select new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice ?? x.Amount * (x.PiecePrice ?? 0)) }).ToListAsync();
            var expenseBeforeSum = await (from ep in db.EXPENSE_PRODUCTS
                                         join e in db.EXPENSES on ep.ExpenseID equals e.ID
                                         where e.SenderWHID == warehouseId && e.IssueDateTime < periodStart && e.IsDeleted == false
                                         group ep by ep.ProductID into g
                                         select new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice) }).ToListAsync();
            var incomeInPeriodSum = await (from ip in db.INCOME_PRODUCTS
                                          join i in db.INCOMES on ip.IncomeID equals i.ID
                                          where i.ReceivingWHID == warehouseId
                                                && i.IssueDateTime >= periodStart
                                                && i.IssueDateTime <= periodEnd
                                                && (i.IsDeleted == null || i.IsDeleted == false)
                                          group ip by ip.ProductID into g
                                          select new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice ?? x.Amount * (x.PiecePrice ?? 0)) }).ToListAsync();
            var expenseInPeriodSum = await (from ep in db.EXPENSE_PRODUCTS
                                            join e in db.EXPENSES on ep.ExpenseID equals e.ID
                                            where e.SenderWHID == warehouseId
                                                  && e.IssueDateTime >= periodStart
                                                  && e.IssueDateTime <= periodEnd
                                                  && e.IsDeleted == false
                                            group ep by ep.ProductID into g
                                            select new { ProductId = g.Key, TotalSum = g.Sum(x => x.TotalPrice) }).ToListAsync();

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

            var allProductIds = new HashSet<int>();
            foreach (var k in openingByProduct.Keys) allProductIds.Add(k);
            foreach (var k in incomeByProduct.Keys) allProductIds.Add(k);
            foreach (var k in expenseByProduct.Keys) allProductIds.Add(k);

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
            double sumOpen = 0, sumIn = 0, sumOut = 0, sumClose = 0;
            decimal sumOpenSum = 0, sumInSum = 0, sumOutSum = 0, sumCloseSum = 0;

            foreach (var pid in allProductIds.OrderBy(id => productDict.ContainsKey(id) ? productDict[id].PNo ?? "" : ""))
            {
                openingByProduct.TryGetValue(pid, out var open);
                incomeByProduct.TryGetValue(pid, out var inc);
                expenseByProduct.TryGetValue(pid, out var exp);
                var close = open + inc - exp;

                openingSumByProduct.TryGetValue(pid, out var openSum);
                incomeSumByProduct.TryGetValue(pid, out var incSum);
                expenseSumByProduct.TryGetValue(pid, out var expSum);
                var closeSum = openSum + incSum - expSum;

                sumOpen += open;
                sumIn += inc;
                sumOut += exp;
                sumClose += close;
                sumOpenSum += openSum;
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
                    IncomeQty = inc,
                    ExpenseQty = exp,
                    ClosingBalance = close,
                    OpeningBalanceSum = openSum,
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
                    TotalIncome = sumIn,
                    TotalExpense = sumOut,
                    TotalClosing = sumClose,
                    TotalOpeningSum = sumOpenSum,
                    TotalIncomeSum = sumInSum,
                    TotalExpenseSum = sumOutSum,
                    TotalClosingSum = sumCloseSum
                },
                Warehouses = warehouses
            };
        }
    }
}
