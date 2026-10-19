using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.Entity;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;
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

        /// <summary>
        /// Kirim qiymati QQS'siz: miqdor × dona narxi (INCOME_PRODUCTS.TotalPrice QQS bilan saqlanadi, chiqim esa
        /// QQS'siz — avval ikkalasi aralashib, "summa" rejimidagi yakuniy qoldiq ma'nosiz edi, SRM-B19).
        /// Dona narxi yo'q eski qatorlarda — TotalPrice.
        /// </summary>
        private static double IncomeValueWithoutVat(double amount, double? piecePrice, double? totalPrice)
        {
            return piecePrice.HasValue ? amount * piecePrice.Value : (totalPrice ?? 0);
        }

        private static bool ContainsInventarizatsiya(string text)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(InventarizatsiyaToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Inventory/Adjust yaratgan hujjatlar: Description/Comment'da "inventarizatsiya" yoki INV_..._IN_|_OUT_ DocNo.
        /// </summary>
        private static bool IsInventarizatsiyaDoc(string docNo, string description, string lineComment, bool isIncome)
        {
            if (ContainsInventarizatsiya(description) || ContainsInventarizatsiya(lineComment))
            {
                return true;
            }

            return IsInventoryAdjustmentDocNo(docNo, isIncome);
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

            // Oldingi qoldiq = oy 1-kunigacha bo'lgan barcha kirim − chiqim. Miqdor va summa bitta agregatda
            // (avval bir xil satrlar ustidan 4 ta alohida to'liq tarix so'rovi — SRM-P09).
            // "DLOC-ASN-" kirimlari qoldiqni oshirmaydi (mavjud qoldiqni joyga bog'lash) — hisobga olinmaydi.
            var assignmentPrefix = LegacyDlocAssignmentService.AssignmentDocNoPrefix;

            var incomeBefore = await (from ip in db.INCOME_PRODUCTS
                                     join i in db.INCOMES on ip.IncomeID equals i.ID
                                     where i.ReceivingWHID == warehouseId
                                           && i.IssueDateTime < periodStart
                                           && (i.IsDeleted == null || i.IsDeleted == false)
                                           && i.RecieveStatus == true
                                           && (i.DocNo == null || !i.DocNo.StartsWith(assignmentPrefix))
                                     group ip by ip.ProductID into g
                                     select new
                                     {
                                         ProductId = g.Key,
                                         Total = g.Sum(x => x.Amount),
                                         TotalSum = g.Sum(x => x.PiecePrice != null ? x.Amount * x.PiecePrice.Value : (x.TotalPrice ?? 0))
                                     }).ToListAsync();

            var expenseBefore = await (from ep in db.EXPENSE_PRODUCTS
                                      join e in db.EXPENSES on ep.ExpenseID equals e.ID
                                      where e.SenderWHID == warehouseId
                                            && e.IssueDateTime < periodStart
                                            && e.IsDeleted == false
                                      group ep by ep.ProductID into g
                                      select new { ProductId = g.Key, Total = g.Sum(x => x.Amount), TotalSum = g.Sum(x => x.TotalPrice) })
                .ToListAsync();

            var openingByProduct = new Dictionary<int, double>();
            var openingSumByProduct = new Dictionary<int, decimal>();
            foreach (var x in incomeBefore)
            {
                openingByProduct[x.ProductId] = (double)x.Total;
                openingSumByProduct[x.ProductId] = (decimal)x.TotalSum;
            }
            foreach (var x in expenseBefore)
            {
                double q;
                openingByProduct.TryGetValue(x.ProductId, out q);
                openingByProduct[x.ProductId] = q - (double)x.Total;
                decimal s;
                openingSumByProduct.TryGetValue(x.ProductId, out s);
                openingSumByProduct[x.ProductId] = s - (decimal)x.TotalSum;
            }

            // Oy ichidagi kirim/chiqim qatorlari: to'liq INCOME/EXPENS entity o'rniga faqat kerakli ustunlar
            var incomeLinesInPeriod = (await (from ip in db.INCOME_PRODUCTS
                                             join i in db.INCOMES on ip.IncomeID equals i.ID
                                             where i.ReceivingWHID == warehouseId
                                                   && i.IssueDateTime >= periodStart
                                                   && i.IssueDateTime < periodEndExclusive
                                                   && (i.IsDeleted == null || i.IsDeleted == false)
                                                   && i.RecieveStatus == true
                                                   && (i.DocNo == null || !i.DocNo.StartsWith(assignmentPrefix))
                                             select new { ip.ProductID, ip.Amount, ip.Comment, ip.TotalPrice, ip.PiecePrice, i.DocNo, i.Description })
                .ToListAsync())
                .Select(x => new
                {
                    x.ProductID,
                    x.Amount,
                    Sum = (decimal)IncomeValueWithoutVat((double)x.Amount, (double?)x.PiecePrice, (double?)x.TotalPrice),
                    IsAdjustment = IsInventarizatsiyaDoc(x.DocNo, x.Description, x.Comment, isIncome: true)
                })
                .ToList();

            var expenseLinesInPeriod = (await (from ep in db.EXPENSE_PRODUCTS
                                              join e in db.EXPENSES on ep.ExpenseID equals e.ID
                                              where e.SenderWHID == warehouseId
                                                    && e.IssueDateTime >= periodStart
                                                    && e.IssueDateTime < periodEndExclusive
                                                    && e.IsDeleted == false
                                              select new { ep.ProductID, ep.Amount, ep.Comment, ep.TotalPrice, e.DocNo, e.Description })
                .ToListAsync())
                .Select(x => new
                {
                    x.ProductID,
                    x.Amount,
                    Sum = (decimal)x.TotalPrice,
                    IsAdjustment = IsInventarizatsiyaDoc(x.DocNo, x.Description, x.Comment, isIncome: false)
                })
                .ToList();

            // Operatsion harakat va inventarizatsiya (net = qo'shimcha kirim − kamaytirish chiqimi) alohida
            var incomeByProduct = incomeLinesInPeriod.Where(x => !x.IsAdjustment).GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => (double)g.Sum(x => x.Amount));
            var expenseByProduct = expenseLinesInPeriod.Where(x => !x.IsAdjustment).GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => (double)g.Sum(x => x.Amount));
            var adjustmentInByProduct = incomeLinesInPeriod.Where(x => x.IsAdjustment).GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => (double)g.Sum(x => x.Amount));
            var adjustmentOutByProduct = expenseLinesInPeriod.Where(x => x.IsAdjustment).GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => (double)g.Sum(x => x.Amount));

            var incomeSumByProduct = incomeLinesInPeriod.Where(x => !x.IsAdjustment).GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.Sum));
            var expenseSumByProduct = expenseLinesInPeriod.Where(x => !x.IsAdjustment).GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.Sum));
            var adjustmentInSumByProduct = incomeLinesInPeriod.Where(x => x.IsAdjustment).GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.Sum));
            var adjustmentOutSumByProduct = expenseLinesInPeriod.Where(x => x.IsAdjustment).GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.Sum));

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
                    ExpenseQty = (double)exp,
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
