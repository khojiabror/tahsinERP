using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.Tannarx
{
    /// <summary>
    /// Fakt tannarx hisoblash dvigateli (yagona engine — SP_TANNARX_UPDATE_ACTUAL_COSTS endi chaqirilmaydi).
    /// Bir oy uchun barcha manbalar (ishlab chiqarish, BOM, SO→PO "Buy#" zanjiri, PI, kirim, yo'l varaqalari,
    /// ish haqi, Finance tranzaksiyalari) OLDINDAN, mahsulot soniga bog'liq bo'lmagan miqdordagi so'rovlar bilan
    /// yuklanadi; keyin har mahsulot xotirada hisoblanadi.
    /// </summary>
    internal class TannarxActualCostEngine
    {
        internal const decimal VatMultiplier = 1.12m;
        private const decimal Path0CoverageThreshold = 0.50m;
        private const decimal Path1CoverageThreshold = 0.85m;
        private const decimal Path2CoverageThreshold = 0.80m;
        private const decimal Path3CoverageThreshold = 0.60m;
        private const decimal ReconcileThreshold = 0.03m;

        private static readonly Regex ProductionIdRegex = new Regex(@"ProductionID:\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex BuyTokenRegex = new Regex(@"(?:Buy#|B:)\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly string[] AccrualSourceDocTypes = { "PurchaseInvoice", "SalesInvoice" };
        private static readonly string[] CoveredElsewhereSourceDocTypes = { "PurchaseInvoicePayment", "WaybillPayment", "HR_PAYROLL", "ShareholderContribution" };

        private readonly DBTHSNEntities _db;

        // Bitta ishga tushirish davomida o'zgarmaydigan ma'lumotlar (bir marta yuklanadi).
        private Dictionary<string, HashSet<int>> _poIdsByBuyToken;
        private Dictionary<int, TannarxFinanceClass> _financeClasses;
        private Dictionary<int, string> _financeCategoryNames;
        private Dictionary<int, string> _currencyCodes;
        private int? _baseCurrencyId;
        private bool _referenceLoaded;

        internal TannarxActualCostEngine(DBTHSNEntities db)
        {
            _db = db;
        }

        internal async Task<TannarxMonthActualResult> CalculateMonthAsync(int year, int month, IList<TANNARX> rowsInMonth)
        {
            await EnsureReferenceDataAsync();

            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);
            var result = new TannarxMonthActualResult { Year = year, Month = month, Start = start, End = end };

            // 1) Ishlab chiqarish: shu oyda barcha mahsulotlar (overhead maxraji ham shundan).
            var production = await _db.PRODUCT_PRODUCTION
                .Where(p => p.IsDeleted != true && p.Date >= start && p.Date < end)
                .GroupBy(p => p.ProductID)
                .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => x.Total) })
                .ToListAsync();
            var producedQty = production.ToDictionary(x => x.ProductID, x => Convert.ToDecimal(x.Qty));
            result.MonthProducedQty = producedQty.Values.Where(q => q > 0).Sum();

            decimal Qty(int productId) => producedQty.TryGetValue(productId, out var q) && q > 0 ? q : 0m;

            var tannarxProductIds = rowsInMonth.Select(r => r.ProductID).Distinct().ToList();
            var producedIds = tannarxProductIds.Where(id => Qty(id) > 0).ToList();
            // Ishlab chiqarilgan, lekin shu oy TANNARX qatori yo'q mahsulotlar: overhead ulushi ularga ham hisoblanadi,
            // lekin hech qayerga yozilmaydi — oy manbalari sahifasida ogohlantiriladi.
            var tannarxSet = new HashSet<int>(tannarxProductIds);
            foreach (var kv in producedQty.Where(kv => kv.Value > 0 && !tannarxSet.Contains(kv.Key)))
                result.ProducedWithoutTannarx[kv.Key] = kv.Value;

            // 2) Finance (soliq+bank, boshqa, material) — oy jami.
            result.Finance = await LoadFinanceTotalsAsync(start, end);

            // 2b) PATH0: ishlab chiqarish uchun ombordan haqiqiy xomashyo chiqimi (EXPENSES, FIFO narx, "ProductionID:{id}").
            var warehouseIssue = await LoadWarehouseIssuesAsync(start, end, producedIds);

            // 3) BOM komponentlari (har mahsulotning aktiv, bo'lmasa oxirgi BOM i).
            var bomByProduct = await LoadBomComponentsAsync(producedIds);

            // 4) SO → PO (Buy# token) zanjiri.
            var soRows = producedIds.Any()
                ? await _db.S_ORDER_PRODUCTS
                    .Where(sop => producedIds.Contains(sop.ProductID)
                        && sop.S_ORDERS.IsDeleted != true
                        && sop.S_ORDERS.IssuedDate >= start
                        && sop.S_ORDERS.IssuedDate < end)
                    .Select(sop => new { sop.ProductID, sop.OrderID, sop.S_ORDERS.OrderNo })
                    .Distinct()
                    .ToListAsync()
                : new[] { new { ProductID = 0, OrderID = 0, OrderNo = "" } }.Take(0).ToList();

            var soIdsByProduct = soRows.GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => g.Select(x => x.OrderID).Distinct().ToList());
            var soNoById = soRows.GroupBy(x => x.OrderID).ToDictionary(g => g.Key, g => g.First().OrderNo);
            var poIdsByProduct = new Dictionary<int, HashSet<int>>();
            foreach (var productId in producedIds)
            {
                var set = new HashSet<int>();
                if (soIdsByProduct.TryGetValue(productId, out var soIds))
                {
                    foreach (var soId in soIds)
                    {
                        var token = ExtractBuyTokenFromOrderNo(soNoById[soId]);
                        if (token != null && _poIdsByBuyToken.TryGetValue(token, out var poIds))
                            set.UnionWith(poIds);
                    }
                }
                poIdsByProduct[productId] = set;
            }
            var allPoIds = poIdsByProduct.Values.SelectMany(x => x).Distinct().ToList();

            var poNoById = allPoIds.Any()
                ? await _db.P_ORDERS.Where(p => allPoIds.Contains(p.ID)).Select(p => new { p.ID, p.OrderNo }).ToDictionaryAsync(p => p.ID, p => p.OrderNo)
                : new Dictionary<int, string>();

            // 5) PO larga bog'langan xarid invoyslari (barcha sanalar — PATH2 kirimi oyda bo'lsa yetarli).
            var piHeaders = allPoIds.Any()
                ? await _db.P_INVOICES
                    .Where(i => i.IsDeleted != true && i.OrderID.HasValue && allPoIds.Contains(i.OrderID.Value))
                    .Select(i => new PiHeader { ID = i.ID, OrderID = i.OrderID.Value, InvoiceDate = i.InvoiceDate, Amount = i.Amount, InvoiceNo = i.InvoiceNo })
                    .ToListAsync()
                : new List<PiHeader>();
            var piHeadersByPo = piHeaders.ToLookup(h => h.OrderID);
            var piInMonthIds = piHeaders.Where(h => h.InvoiceDate >= start && h.InvoiceDate < end).Select(h => h.ID).ToList();
            var allPiIds = piHeaders.Select(h => h.ID).ToList();

            var piLines = piInMonthIds.Any()
                ? await _db.P_INVOICE_PRODUCTS
                    .Where(l => piInMonthIds.Contains(l.InvoiceID))
                    .Select(l => new DocLine { ID = l.ID, DocID = l.InvoiceID, ProductID = l.ProductID, Qty = l.Quantity, Amount = l.TotalPrice })
                    .ToListAsync()
                : new List<DocLine>();
            var piLinesByInvoice = piLines.ToLookup(l => l.DocID);

            // 6) Kirimlar (PATH2): shu oyda, PO invoyslariga bog'langan.
            var incomes = allPiIds.Any()
                ? await _db.INCOMES
                    .Where(i => i.IsDeleted != true
                        && i.InvoiceID.HasValue
                        && allPiIds.Contains(i.InvoiceID.Value)
                        && i.IssueDateTime >= start
                        && i.IssueDateTime < end)
                    .Select(i => new IncomeHeader { ID = i.ID, InvoiceID = i.InvoiceID.Value, TotalPrice = i.TotalPrice })
                    .ToListAsync()
                : new List<IncomeHeader>();
            var incomesByInvoice = incomes.ToLookup(i => i.InvoiceID);
            var incomeIds = incomes.Select(i => i.ID).ToList();
            var incomeLines = incomeIds.Any()
                ? await _db.INCOME_PRODUCTS
                    .Where(x => incomeIds.Contains(x.IncomeID))
                    .Select(x => new DocLine
                    {
                        ID = x.ID,
                        DocID = x.IncomeID,
                        ProductID = x.ProductID,
                        Qty = x.Amount,
                        Amount = x.TotalPrice ?? ((x.PiecePrice ?? 0) * x.Amount)
                    })
                    .ToListAsync()
                : new List<DocLine>();
            var incomeLinesByIncome = incomeLines.ToLookup(l => l.DocID);

            // 7) Material tashuvi: shu oydagi PI larga bog'langan, shu oyda berilgan yo'l varaqalari.
            var materialWaybills = piInMonthIds.Any()
                ? await _db.F_WAYBILLS
                    .Where(w => w.IsDeleted != true
                        && w.InvoiceID.HasValue
                        && piInMonthIds.Contains(w.InvoiceID.Value)
                        && w.IssueDate.HasValue
                        && w.IssueDate.Value >= start
                        && w.IssueDate.Value < end)
                    .Select(w => new WaybillRow { DocID = w.InvoiceID.Value, WaybillNo = w.WaybillNo, Amount = w.Amount })
                    .ToListAsync()
                : new List<WaybillRow>();
            var materialWaybillsByInvoice = materialWaybills.ToLookup(w => w.DocID);

            // 8) Tayyor mahsulot tashuvi: sotuv invoyslari → yo'l varaqalari.
            var salesLineInvoices = producedIds.Any()
                ? await _db.SALES_INVOICE_LINES
                    .Where(l => producedIds.Contains(l.ProductID)
                        && l.SALES_INVOICES.InvoiceDate >= start
                        && l.SALES_INVOICES.InvoiceDate < end
                        && l.SALES_INVOICES.StatusCode != "CANCELED"
                        && l.SALES_INVOICES.StatusCode != "DRAFT")
                    .Select(l => new { l.ProductID, l.InvoiceID })
                    .Distinct()
                    .ToListAsync()
                : new[] { new { ProductID = 0, InvoiceID = 0L } }.Take(0).ToList();
            var salesInvoiceIdsByProduct = salesLineInvoices.GroupBy(x => x.ProductID)
                .ToDictionary(g => g.Key, g => g.Select(x => x.InvoiceID).Distinct().ToList());

            // Satr bog'lanmagan mahsulot uchun zaxira: uning SO lariga bog'langan sotuv invoyslari.
            var fallbackSoIds = producedIds
                .Where(id => !salesInvoiceIdsByProduct.ContainsKey(id) && soIdsByProduct.ContainsKey(id))
                .SelectMany(id => soIdsByProduct[id])
                .Distinct()
                .ToList();
            if (fallbackSoIds.Any())
            {
                var bySo = await _db.SALES_INVOICES
                    .Where(i => i.OrderID.HasValue
                        && fallbackSoIds.Contains(i.OrderID.Value)
                        && i.InvoiceDate >= start
                        && i.InvoiceDate < end
                        && i.StatusCode != "CANCELED"
                        && i.StatusCode != "DRAFT")
                    .Select(i => new { i.ID, OrderID = i.OrderID.Value })
                    .ToListAsync();
                var bySoLookup = bySo.ToLookup(x => x.OrderID, x => x.ID);
                foreach (var productId in producedIds.Where(id => !salesInvoiceIdsByProduct.ContainsKey(id) && soIdsByProduct.ContainsKey(id)))
                {
                    var ids = soIdsByProduct[productId].SelectMany(so => bySoLookup[so]).Distinct().ToList();
                    if (ids.Any())
                        salesInvoiceIdsByProduct[productId] = ids;
                }
            }

            var allSalesInvoiceIds = salesInvoiceIdsByProduct.Values.SelectMany(x => x).Distinct().ToList();
            var finishedWaybills = allSalesInvoiceIds.Any()
                ? await _db.F_WAYBILLS
                    .Where(w => w.IsDeleted != true
                        && w.SalesInvoiceID.HasValue
                        && allSalesInvoiceIds.Contains(w.SalesInvoiceID.Value)
                        && (w.Direction == null || w.Direction == 0 || w.Direction == 1)
                        && w.IssueDate.HasValue
                        && w.IssueDate.Value >= start
                        && w.IssueDate.Value < end)
                    .Select(w => new { InvoiceID = w.SalesInvoiceID.Value, w.WaybillNo, w.Amount })
                    .ToListAsync()
                : new[] { new { InvoiceID = 0L, WaybillNo = "", Amount = (double?)null } }.Take(0).ToList();
            var finishedWaybillsByInvoice = finishedWaybills.ToLookup(w => w.InvoiceID);

            var waybillSalesInvoiceIds = finishedWaybills.Select(w => w.InvoiceID).Distinct().ToList();
            var salesLines = waybillSalesInvoiceIds.Any()
                ? await _db.SALES_INVOICE_LINES
                    .Where(l => waybillSalesInvoiceIds.Contains(l.InvoiceID))
                    .Select(l => new { l.InvoiceID, l.ProductID, l.Qty, l.LineAmount })
                    .ToListAsync()
                : new[] { new { InvoiceID = 0L, ProductID = 0, Qty = 0m, LineAmount = (decimal?)null } }.Take(0).ToList();
            var salesLinesByInvoice = salesLines.ToLookup(l => l.InvoiceID);
            var salesInvoiceNos = allSalesInvoiceIds.Any()
                ? await _db.SALES_INVOICES.Where(i => allSalesInvoiceIds.Contains(i.ID)).Select(i => new { i.ID, i.InvoiceNo }).ToDictionaryAsync(i => i.ID, i => i.InvoiceNo)
                : new Dictionary<long, string>();

            // 9) Ish haqi (ishbay) va admin (soatbay ish haqi).
            var laborAmountByProduct = await LoadPieceRateLaborAsync(start, end, producedIds);
            var adminTotal = await LoadAdministrativeTotalAsync(year, month);
            result.AdminTotal = adminTotal;
            result.PieceRateLaborTotal = laborAmountByProduct.Values.Sum();

            // ---- Mahsulot bo'yicha zanjirlar ----
            var chains = new Dictionary<int, ProductChain>();
            foreach (var productId in producedIds)
            {
                var chain = new ProductChain { ProductID = productId, ProducedQty = Qty(productId) };
                bomByProduct.TryGetValue(productId, out var bom);
                chain.BomComponents = bom ?? new HashSet<int>();
                chain.PoIds = poIdsByProduct[productId];

                var chainPis = chain.PoIds.SelectMany(po => piHeadersByPo[po]).ToList();
                chain.PiInMonth = chainPis.Where(h => h.InvoiceDate >= start && h.InvoiceDate < end).ToList();
                chain.PiMatchedLines = chain.PiInMonth
                    .SelectMany(h => piLinesByInvoice[h.ID])
                    .Where(l => chain.BomComponents.Contains(l.ProductID))
                    .ToList();

                chain.Incomes = chainPis.SelectMany(h => incomesByInvoice[h.ID]).ToList();
                chain.IncomeMatchedLines = chain.Incomes
                    .SelectMany(i => incomeLinesByIncome[i.ID])
                    .Where(l => chain.BomComponents.Contains(l.ProductID))
                    .ToList();

                chains[productId] = chain;
            }

            // Bitta PI/kirim qatori bir nechta mahsulotga (bir SO → bir PO, umumiy material) to'g'ri kelsa,
            // u har biriga TO'LIQ yozilmasin: qator summasi da'vogar mahsulotlar orasida ishlab chiqarilgan miqdor ulushida bo'linadi.
            var piLineClaimQty = BuildClaimWeights(chains.Values, c => c.PiMatchedLines);
            var incomeLineClaimQty = BuildClaimWeights(chains.Values, c => c.IncomeMatchedLines);

            // PATH3 haydovchisi: reja material × ishlab chiqarilgan miqdor (faqat ishlab chiqarilganlar).
            var plannedMaterialByProduct = rowsInMonth.GroupBy(r => r.ProductID).ToDictionary(g => g.Key, g => g.First().MaterialPrice);
            var path3Weights = producedIds.ToDictionary(id => id, id => Math.Max(0m, plannedMaterialByProduct[id]) * Qty(id));
            var path3WeightTotal = path3Weights.Values.Sum();
            var producedQtyTotalInRows = producedIds.Sum(id => Qty(id));
            var producedQtyWithPlannedMaterial = producedIds.Where(id => plannedMaterialByProduct[id] > 0).Sum(id => Qty(id));
            var path3DriverCoverage = producedQtyTotalInRows > 0 ? producedQtyWithPlannedMaterial / producedQtyTotalInRows : 0m;

            var monthQty = result.MonthProducedQty;
            var taxBankPerUnit = monthQty > 0 ? result.Finance.TaxBank / monthQty : 0m;
            var otherPerUnit = monthQty > 0 ? result.Finance.Other / monthQty : 0m;
            var adminPerUnit = monthQty > 0 ? adminTotal / monthQty : 0m;

            foreach (var productId in tannarxProductIds)
            {
                var qty = Qty(productId);
                var r = new TannarxProductActualResult { ProductID = productId, ProducedQty = qty };
                result.Products[productId] = r;
                if (qty <= 0)
                {
                    r.Material = MaterialDecision.Empty("NO_PRODUCTION_SKIPPED");
                    continue;
                }

                var chain = chains[productId];
                r.BomComponentCount = chain.BomComponents.Count;
                r.SalesOrderNos = soIdsByProduct.TryGetValue(productId, out var soList) ? soList.Select(id => soNoById[id]).ToList() : new List<string>();
                r.PurchaseOrderNos = chain.PoIds.Where(poNoById.ContainsKey).Select(id => poNoById[id]).ToList();
                r.PurchaseInvoiceNos = chain.PiInMonth.Select(h => h.InvoiceNo).ToList();
                r.PiHeaderAmount = chain.PiInMonth.Sum(h => Convert.ToDecimal(h.Amount ?? 0d));
                r.PiLinesAmount = chain.PiInMonth.SelectMany(h => piLinesByInvoice[h.ID]).Sum(l => Convert.ToDecimal(l.Amount ?? 0d));
                r.PiMatchedAmount = chain.PiMatchedLines.Sum(l => Convert.ToDecimal(l.Amount ?? 0d));
                r.PiMatchedAllocatedAmount = Allocate(chain.PiMatchedLines, piLineClaimQty, qty);
                r.PiSharedLineCount = chain.PiMatchedLines.Count(l => piLineClaimQty[l.ID] > qty);

                // Material algoritmlari ustuvorlik tartibida; birinchi o'tgani olinadi, barcha urinishlar izohga yoziladi:
                // PATH0 ombordan haqiqiy chiqim (FIFO) → PATH2 SO→PO→PI→kirim → PATH1 SO→PO→PI → PATH3 Finance "Material" → reja.
                warehouseIssue.TryGetValue(productId, out var issue);
                r.WarehouseIssue = issue;
                var evaluators = new Func<MaterialDecision>[]
                {
                    () => EvaluatePath0(issue, qty),
                    () => EvaluatePath2(chain, incomeLinesByIncome, incomeLineClaimQty),
                    () => EvaluatePath1(chain, r),
                    () => EvaluatePath3(qty, result.Finance.Material, path3Weights[productId], path3WeightTotal, path3DriverCoverage)
                };
                foreach (var evaluate in evaluators)
                {
                    var decision = evaluate();
                    r.PathAttempts.Add($"{decision.Path}: {(decision.Passed ? "OK (" + decision.MethodCode + ")" : decision.ReasonCode)}");
                    if (decision.Passed)
                    {
                        r.Material = decision;
                        break;
                    }
                }
                if (r.Material == null)
                {
                    var planned = plannedMaterialByProduct[productId];
                    // NONE bo'lmasin: manba topilmasa reja material narxi olinadi (policy).
                    r.Material = planned > 0
                        ? MaterialDecision.Pass("PLAN", "FORCED_PLANNED", planned, planned * qty, qty, 0m, 0m)
                        : MaterialDecision.Pass("PLAN", "FORCED_ZERO", 0m, 0m, qty, 0m, 0m);
                }

                // Material tashuvi
                var (mtAmount, mtWaybillNos, mtWaybillTotal) = AllocateMaterialTransport(chain, piLinesByInvoice, materialWaybillsByInvoice, piLineClaimQty);
                r.MaterialTransportAllocated = mtAmount;
                r.MaterialWaybillNos = mtWaybillNos;
                r.MaterialWaybillAmount = mtWaybillTotal;
                r.PiTotalQty = chain.PiInMonth.SelectMany(h => piLinesByInvoice[h.ID]).Sum(l => Convert.ToDecimal(l.Qty));
                r.PiMatchedQty = chain.PiMatchedLines.Sum(l => Convert.ToDecimal(l.Qty));
                r.MaterialTransportPerUnit = mtAmount > 0 ? mtAmount / qty : 0m;

                // Tayyor mahsulot tashuvi
                if (salesInvoiceIdsByProduct.TryGetValue(productId, out var invoiceIds))
                {
                    r.SalesInvoiceNos = invoiceIds.Where(salesInvoiceNos.ContainsKey).Select(id => salesInvoiceNos[id]).ToList();
                    decimal allocated = 0m;
                    foreach (var invoiceId in invoiceIds)
                    {
                        var wbSum = finishedWaybillsByInvoice[invoiceId].Sum(w => Convert.ToDecimal(w.Amount ?? 0d));
                        if (wbSum <= 0)
                            continue;
                        r.FinishedWaybillAmount += wbSum;
                        r.FinishedWaybillNos.AddRange(finishedWaybillsByInvoice[invoiceId].Select(w => w.WaybillNo));

                        var invLines = salesLinesByInvoice[invoiceId].ToList();
                        if (!invLines.Any())
                            continue;

                        var totalQty = invLines.Sum(x => x.Qty);
                        var productQty = invLines.Where(x => x.ProductID == productId).Sum(x => x.Qty);
                        if (totalQty > 0 && productQty > 0)
                        {
                            allocated += wbSum * (productQty / totalQty); // V1 (miqdor ulushi)
                            continue;
                        }

                        var totalAmount = invLines.Sum(x => x.LineAmount ?? 0m);
                        var productAmount = invLines.Where(x => x.ProductID == productId).Sum(x => x.LineAmount ?? 0m);
                        if (totalAmount > 0 && productAmount > 0)
                        {
                            allocated += wbSum * (productAmount / totalAmount); // V2 (qiymat ulushi)
                            continue;
                        }

                        allocated += wbSum / invLines.Count; // V3 (teng bo'lish)
                    }
                    r.FinishedTransportAllocated = allocated;
                    r.FinishedTransportPerUnit = allocated > 0 ? allocated / qty : 0m;
                    r.FinishedWaybillNos = r.FinishedWaybillNos.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                }

                r.LaborAmount = laborAmountByProduct.TryGetValue(productId, out var labor) ? labor : 0m;
                r.LaborPerUnit = r.LaborAmount > 0 ? r.LaborAmount / qty : 0m;
                r.AdminPerUnit = adminPerUnit;
                r.TaxBankPerUnit = taxBankPerUnit;
                r.OtherPerUnit = otherPerUnit;
            }

            return result;
        }

        // ---------------- Material yo'llari ----------------

        /// <summary>
        /// PATH0: shu oydagi ishlab chiqarish yozuvlari uchun ombordan avtomatik chiqarilgan xomashyo (FIFO kirim narxida).
        /// SO/PO bog'lanishi kerak emas. Chiqim hujjati bo'lmagan ishlab chiqarish qismi uchun birlik narx
        /// hujjatli qism bo'yicha olinadi (qamrov izohda ko'rsatiladi).
        /// </summary>
        private static MaterialDecision EvaluatePath0(WarehouseIssueInfo issue, decimal producedQty)
        {
            if (issue == null || issue.ExpenseCount == 0)
                return MaterialDecision.Fail("PATH0", "NO_WAREHOUSE_ISSUE");
            if (issue.Amount <= 0 || issue.CoveredQty <= 0)
                return MaterialDecision.Fail("PATH0", "WAREHOUSE_ISSUE_ZERO_PRICE");

            var coverage = Math.Min(1m, issue.CoveredQty / producedQty);
            if (coverage < Path0CoverageThreshold)
                return MaterialDecision.Fail("PATH0", "WAREHOUSE_ISSUE_LOW_COVERAGE", coverage, 0m);

            var unitCost = issue.Amount / issue.CoveredQty;
            // Qamrov to'liq bo'lmasa ham butun ishlab chiqarilgan miqdorga shu birlik narx qo'llanadi.
            return MaterialDecision.Pass("PATH0", coverage >= 1m ? "WAREHOUSE_ISSUE_FIFO" : "WAREHOUSE_ISSUE_FIFO_PARTIAL",
                unitCost, unitCost * producedQty, producedQty, coverage, 0m);
        }

        private static MaterialDecision EvaluatePath1(ProductChain chain, TannarxProductActualResult r)
        {
            if (!chain.BomComponents.Any())
                return MaterialDecision.Fail("PATH1", "NO_BOM_COMPONENTS");
            if (!chain.PoIds.Any())
                return MaterialDecision.Fail("PATH1", "NO_PO_LINK");
            if (!chain.PiInMonth.Any())
                return MaterialDecision.Fail("PATH1", "NO_PI_DOCS");
            if (!chain.PiMatchedLines.Any())
                return MaterialDecision.Fail("PATH1", "NO_BOM_MATCH_IN_PI");

            var coveredInvoiceIds = new HashSet<int>(chain.PiMatchedLines.Select(l => l.DocID));
            var coverage = (decimal)chain.PiInMonth.Count(h => coveredInvoiceIds.Contains(h.ID)) / chain.PiInMonth.Count;
            var reconcile = r.PiHeaderAmount != 0 ? Math.Abs(r.PiMatchedAmount - r.PiHeaderAmount) / Math.Abs(r.PiHeaderAmount) : 0m;
            var amount = r.PiMatchedAllocatedAmount;
            if (amount <= 0)
                return MaterialDecision.Fail("PATH1", "COVERAGE_OR_RECONCILE_FAIL", coverage, reconcile);

            var strict = coverage >= Path1CoverageThreshold && reconcile <= ReconcileThreshold;
            // Policy: PATH1 zanjirida summa bo'lsa, qat'iy chegaradan o'tmasa ham PATH1 (RELAXED) tanlanadi.
            return MaterialDecision.Pass("PATH1", strict ? "SO_PO_PI" : "SO_PO_PI_RELAXED", amount / chain.ProducedQty, amount, chain.ProducedQty, coverage, reconcile);
        }

        private static MaterialDecision EvaluatePath2(ProductChain chain, ILookup<int, DocLine> incomeLinesByIncome, Dictionary<int, decimal> claimQty)
        {
            if (!chain.BomComponents.Any())
                return MaterialDecision.Fail("PATH2", "NO_BOM_COMPONENTS");
            if (!chain.PoIds.Any())
                return MaterialDecision.Fail("PATH2", "NO_PO_LINK");
            if (!chain.Incomes.Any())
                return MaterialDecision.Fail("PATH2", "NO_INCOME_DOCS");
            if (!chain.IncomeMatchedLines.Any())
                return MaterialDecision.Fail("PATH2", "NO_BOM_MATCH_INCOME");

            var coveredIncomeIds = new HashSet<int>(chain.IncomeMatchedLines.Select(l => l.DocID));
            var coverage = (decimal)chain.Incomes.Count(i => coveredIncomeIds.Contains(i.ID)) / chain.Incomes.Count;
            var headerAmount = chain.Incomes.Sum(i => Convert.ToDecimal(i.TotalPrice ?? 0d));
            var linesAmount = chain.IncomeMatchedLines.Sum(l => Convert.ToDecimal(l.Amount ?? 0d));
            var reconcile = headerAmount != 0 ? Math.Abs(linesAmount - headerAmount) / Math.Abs(headerAmount) : 0m;
            if (coverage < Path2CoverageThreshold || reconcile > ReconcileThreshold)
                return MaterialDecision.Fail("PATH2", "COVERAGE_OR_RECONCILE_FAIL", coverage, reconcile);

            var allocated = Allocate(chain.IncomeMatchedLines, claimQty, chain.ProducedQty);
            return MaterialDecision.Pass("PATH2", "INCOME_FALLBACK", allocated / chain.ProducedQty, allocated, chain.ProducedQty, coverage, reconcile);
        }

        /// <summary>
        /// PATH3: oy bo'yicha Finance "Material" xarajatlari mahsulotlarga (reja material × ishlab chiqarilgan miqdor) ulushida.
        /// Oldin ulush (bitta mahsulot / BARCHA mahsulotlar reja narxlari yig'indisi) 60% chegarasi bilan solishtirilardi —
        /// bir nechta mahsulot bo'lsa hech qachon o'tmasdi va Finance materiali umuman ishlatilmasdi.
        /// Endi 60% — haydovchi qamrovi: ishlab chiqarilgan miqdorning qancha qismida reja material narxi bor.
        /// </summary>
        private static MaterialDecision EvaluatePath3(decimal producedQty, decimal monthMaterialFinTotal, decimal weight, decimal weightTotal, decimal driverCoverage)
        {
            if (producedQty <= 0 || monthMaterialFinTotal <= 0 || weightTotal <= 0 || weight <= 0)
                return MaterialDecision.Fail("PATH3", "NO_DRIVER_OR_NO_FIN");
            if (driverCoverage < Path3CoverageThreshold)
                return MaterialDecision.Fail("PATH3", "INSUFFICIENT_DRIVER_COVERAGE", driverCoverage, 0m);

            var share = weight / weightTotal;
            var allocated = monthMaterialFinTotal * share;
            return MaterialDecision.Pass("PATH3", "FIN_ALLOC_BY_PLANNED_SHARE", allocated / producedQty, allocated, producedQty, driverCoverage, 0m);
        }

        private static Dictionary<int, decimal> BuildClaimWeights(IEnumerable<ProductChain> chains, Func<ProductChain, List<DocLine>> lines)
        {
            var weights = new Dictionary<int, decimal>();
            foreach (var chain in chains)
            {
                foreach (var lineId in lines(chain).Select(l => l.ID).Distinct())
                {
                    weights.TryGetValue(lineId, out var w);
                    weights[lineId] = w + chain.ProducedQty;
                }
            }
            return weights;
        }

        private static decimal Allocate(IEnumerable<DocLine> lines, Dictionary<int, decimal> claimQty, decimal producedQty)
        {
            decimal total = 0m;
            foreach (var line in lines)
            {
                var claim = claimQty.TryGetValue(line.ID, out var c) && c > 0 ? c : producedQty;
                total += Convert.ToDecimal(line.Amount ?? 0d) * (producedQty / claim);
            }
            return total;
        }

        /// <summary>
        /// Yo'l varaqasi summasi (faqat shu oyda berilganlari) PI qatorlariga miqdor (V1), bo'lmasa qiymat (V2),
        /// bo'lmasa qatorlar soni (V3) ulushida bo'linadi; BOM ga mos qator ulushi da'vogar mahsulotlar orasida bo'linadi.
        /// </summary>
        private static (decimal Amount, List<string> WaybillNos, decimal WaybillTotal) AllocateMaterialTransport(
            ProductChain chain,
            ILookup<int, DocLine> piLinesByInvoice,
            ILookup<int, WaybillRow> waybillsByInvoice,
            Dictionary<int, decimal> claimQty)
        {
            decimal allocated = 0m;
            decimal waybillTotal = 0m;
            var waybillNos = new List<string>();
            if (!chain.BomComponents.Any())
                return (0m, waybillNos, 0m);

            foreach (var invoice in chain.PiInMonth)
            {
                var wbSum = waybillsByInvoice[invoice.ID].Sum(w => Convert.ToDecimal(w.Amount ?? 0d));
                if (wbSum <= 0)
                    continue;
                waybillTotal += wbSum;
                waybillNos.AddRange(waybillsByInvoice[invoice.ID].Select(w => w.WaybillNo));

                var lines = piLinesByInvoice[invoice.ID].ToList();
                if (!lines.Any())
                    continue;

                var totalQty = lines.Sum(x => Convert.ToDecimal(x.Qty));
                var totalAmount = lines.Sum(x => Convert.ToDecimal(x.Amount ?? 0d));
                foreach (var line in lines.Where(l => chain.BomComponents.Contains(l.ProductID)))
                {
                    decimal lineShare;
                    if (totalQty > 0)
                        lineShare = Convert.ToDecimal(line.Qty) / totalQty;
                    else if (totalAmount > 0)
                        lineShare = Convert.ToDecimal(line.Amount ?? 0d) / totalAmount;
                    else
                        lineShare = 1m / lines.Count;

                    var claim = claimQty.TryGetValue(line.ID, out var c) && c > 0 ? c : chain.ProducedQty;
                    allocated += wbSum * lineShare * (chain.ProducedQty / claim);
                }
            }

            return (allocated, waybillNos.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList(), waybillTotal);
        }

        // ---------------- Yuklovchilar ----------------

        private async Task EnsureReferenceDataAsync()
        {
            if (_referenceLoaded)
                return;

            // SO → PO bog'lanishi: P_ORDER_PRODUCTS.Priority dagi "Buy#123" / "B:123" tokenlar (butun ishga tushirish uchun bir marta).
            var priorities = await _db.P_ORDER_PRODUCTS
                .Where(pop => pop.P_ORDERS.IsDeleted != true
                    && pop.Priority != null
                    && (pop.Priority.Contains("Buy#") || pop.Priority.Contains("B:")))
                .Select(pop => new { pop.OrderID, pop.Priority })
                .Distinct()
                .ToListAsync();
            _poIdsByBuyToken = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in priorities)
            {
                foreach (Match m in BuyTokenRegex.Matches(p.Priority))
                {
                    var token = m.Groups[1].Value;
                    if (!_poIdsByBuyToken.TryGetValue(token, out var set))
                        _poIdsByBuyToken[token] = set = new HashSet<int>();
                    set.Add(p.OrderID);
                }
            }

            var categories = await _db.FIN_CATEGORIES
                .Where(c => c.IsDeleted != true)
                .Select(c => new TannarxFinanceCategoryClassifier.CategoryRow { ID = c.ID, Name = c.Name, Type = c.Type, ParentID = c.ParentID })
                .ToListAsync();
            _financeClasses = TannarxFinanceCategoryClassifier.Classify(categories);
            _financeCategoryNames = categories.ToDictionary(c => c.ID, c => c.Name);

            var currencies = await _db.CURRENCIES.Select(c => new { c.ID, c.Shortname }).ToListAsync();
            _currencyCodes = currencies.ToDictionary(c => c.ID, c => (c.Shortname ?? "").Trim());
            _baseCurrencyId = currencies
                .Where(c => c.Shortname != null && c.Shortname.Trim().ToUpper() == "UZS")
                .Select(c => (int?)c.ID)
                .FirstOrDefault();

            _referenceLoaded = true;
        }

        /// <summary>
        /// Finance xarajatlari (oy bo'yicha): BARCHA xarajat kategoriyalari (Type = 2) olinadi va har biri
        /// TannarxFinanceCategoryClassifier bo'yicha guruhga tushadi yoki sababi bilan chiqariladi.
        /// - Direction 2 (chiqim) qo'shiladi, Direction 1 (shu xarajat kategoriyasiga qaytgan pul) ayiriladi — Finance P&amp;L bilan bir xil;
        /// - "PurchaseInvoice"/"SalesInvoice" hisoblash (accrual) yozuvlari olinmaydi — ular pul harakati emas,
        ///   to'lovi alohida tranzaksiya bo'lib keladi (aks holda ikki marta sanalardi);
        /// - aniqlanmagan ("Boshqa"ga avtomatik tushadigan) kategoriyada boshqa joyda hisoblanadigan to'lovlar
        ///   (ta'minotchiga, yo'l varaqasi, ish haqi, aksiyador) olinmaydi;
        /// - faqat milliy valyuta (UZS); boshqa valyutadagilar konvertatsiya qilinmaydi, alohida ko'rsatiladi.
        /// </summary>
        private async Task<TannarxFinanceTotals> LoadFinanceTotalsAsync(DateTime start, DateTime end)
        {
            var totals = new TannarxFinanceTotals
            {
                BaseCurrencyCode = _baseCurrencyId.HasValue ? _currencyCodes[_baseCurrencyId.Value] : null
            };
            var categoryIds = _financeClasses.Keys.ToList();
            if (!categoryIds.Any())
                return totals;

            var rows = await _db.FIN_TRANSACTIONS
                .Where(t => t.IsDeleted != true
                    && t.TxnDate >= start
                    && t.TxnDate < end
                    && categoryIds.Contains(t.CategoryID)
                    && (t.Direction == 1 || t.Direction == 2))
                .GroupBy(t => new { t.CategoryID, t.CurrencyID, t.Direction, t.SourceDocType })
                .Select(g => new { g.Key.CategoryID, g.Key.CurrencyID, g.Key.Direction, g.Key.SourceDocType, Amount = g.Sum(x => x.Amount), Count = g.Count() })
                .ToListAsync();

            var lines = new Dictionary<string, TannarxFinanceCategoryLine>();
            void AddLine(int categoryId, string bucketName, string reason, decimal amount, int count)
            {
                var key = categoryId + "|" + reason;
                if (!lines.TryGetValue(key, out var line))
                {
                    line = new TannarxFinanceCategoryLine
                    {
                        CategoryID = categoryId,
                        CategoryName = _financeCategoryNames[categoryId],
                        Bucket = bucketName,
                        Reason = reason
                    };
                    lines[key] = line;
                }
                line.Amount += amount;
                line.TransactionCount += count;
            }

            foreach (var row in rows)
            {
                var signed = row.Direction == 2 ? row.Amount : -row.Amount;
                var cls = _financeClasses[row.CategoryID];

                if (row.SourceDocType != null && AccrualSourceDocTypes.Contains(row.SourceDocType))
                {
                    AddLine(row.CategoryID, "Chiqarildi", "Invoys hisoblash yozuvi (to'lovi alohida keladi)", signed, row.Count);
                    continue;
                }
                if (_baseCurrencyId.HasValue && row.CurrencyID != _baseCurrencyId.Value)
                {
                    var code = _currencyCodes.TryGetValue(row.CurrencyID, out var c) && c.Length > 0 ? c : ("#" + row.CurrencyID);
                    totals.SkippedForeignCount += row.Count;
                    totals.SkippedForeignByCurrency.TryGetValue(code, out var prev);
                    totals.SkippedForeignByCurrency[code] = prev + signed;
                    AddLine(row.CategoryID, "Chiqarildi", "Boshqa valyuta (" + code + ") — konvertatsiya qilinmaydi", signed, row.Count);
                    continue;
                }

                var name = _financeCategoryNames[row.CategoryID];
                switch (cls.Bucket)
                {
                    case TannarxFinanceBucket.Material:
                        totals.Material += signed;
                        totals.MaterialCategories.Add(name);
                        AddLine(row.CategoryID, "Material (PATH3)", cls.Reason, signed, row.Count);
                        break;
                    case TannarxFinanceBucket.TaxBank:
                        totals.TaxBank += signed;
                        totals.TaxBankCategories.Add(name);
                        AddLine(row.CategoryID, "B — Soliq va bank", cls.Reason, signed, row.Count);
                        break;
                    case TannarxFinanceBucket.Other:
                        totals.Other += signed;
                        totals.OtherCategories.Add(name);
                        AddLine(row.CategoryID, "D — Boshqa", cls.Reason, signed, row.Count);
                        break;
                    case TannarxFinanceBucket.OtherAuto:
                        if (row.SourceDocType != null && CoveredElsewhereSourceDocTypes.Contains(row.SourceDocType))
                        {
                            AddLine(row.CategoryID, "Chiqarildi", "Boshqa joyda hisoblanadi (" + row.SourceDocType + ")", signed, row.Count);
                            break;
                        }
                        totals.Other += signed;
                        totals.OtherCategories.Add(name);
                        AddLine(row.CategoryID, "D — Boshqa", cls.Reason, signed, row.Count);
                        break;
                    default:
                        AddLine(row.CategoryID, "Chiqarildi", cls.Reason, signed, row.Count);
                        break;
                }
                if (cls.Bucket != TannarxFinanceBucket.Excluded)
                    totals.TransactionCount += row.Count;
            }

            // Qaytarishlar chiqimdan oshib ketsa, manfiy xarajat tannarxga yozilmaydi.
            totals.Material = Math.Max(0m, totals.Material);
            totals.TaxBank = Math.Max(0m, totals.TaxBank);
            totals.Other = Math.Max(0m, totals.Other);
            totals.Lines.AddRange(lines.Values.OrderBy(l => l.Bucket == "Chiqarildi").ThenBy(l => l.Bucket).ThenByDescending(l => l.Amount));
            return totals;
        }


        /// <summary>
        /// Ishlab chiqarish uchun avtomatik yaratilgan xomashyo chiqimlari (ProductionConsumptionService):
        /// EXPENSES.Description da "ProductionID:{PRODUCT_PRODUCTION.ID}", EXPENSE_PRODUCTS.TotalPrice — FIFO kirim narxi (UZS).
        /// Chiqim hujjati ishlab chiqarish sanasida emas, kiritilgan/qayta hisoblangan kunda yaratiladi,
        /// shuning uchun sana bo'yicha emas, ProductionID bo'yicha bog'lanadi (oy boshidan 1 oy oldingi hujjatlardan qidiriladi).
        /// </summary>
        private async Task<Dictionary<int, WarehouseIssueInfo>> LoadWarehouseIssuesAsync(DateTime start, DateTime end, List<int> productIds)
        {
            var result = new Dictionary<int, WarehouseIssueInfo>();
            if (!productIds.Any())
                return result;

            var productions = await _db.PRODUCT_PRODUCTION
                .Where(p => p.IsDeleted != true && p.Date >= start && p.Date < end && productIds.Contains(p.ProductID))
                .Select(p => new { p.ID, p.ProductID, p.Total })
                .ToListAsync();
            if (!productions.Any())
                return result;
            var productionById = productions.ToDictionary(p => p.ID);

            var searchFrom = start.AddMonths(-1);
            var expenseHeaders = await _db.EXPENSES
                .Where(e => !e.IsDeleted
                    && e.IssueDateTime >= searchFrom
                    && e.Description != null
                    && e.Description.Contains("ProductionID:"))
                .Select(e => new { e.ID, e.Description })
                .ToListAsync();

            var expenseToProduction = new Dictionary<int, int>();
            foreach (var e in expenseHeaders)
            {
                var m = ProductionIdRegex.Match(e.Description);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var productionId) && productionById.ContainsKey(productionId))
                    expenseToProduction[e.ID] = productionId;
            }
            if (!expenseToProduction.Any())
                return result;

            var expenseIds = expenseToProduction.Keys.ToList();
            var lineSums = await _db.EXPENSE_PRODUCTS
                .Where(l => expenseIds.Contains(l.ExpenseID))
                .GroupBy(l => l.ExpenseID)
                .Select(g => new
                {
                    ExpenseID = g.Key,
                    Amount = g.Sum(x => x.TotalPrice),
                    Lines = g.Count(),
                    ZeroPriceLines = g.Count(x => x.TotalPrice <= 0)
                })
                .ToListAsync();

            foreach (var group in lineSums.GroupBy(s => productionById[expenseToProduction[s.ExpenseID]].ProductID))
            {
                var coveredProductionIds = group.Select(s => expenseToProduction[s.ExpenseID]).Distinct().ToList();
                result[group.Key] = new WarehouseIssueInfo
                {
                    ExpenseCount = group.Count(),
                    LineCount = group.Sum(s => s.Lines),
                    ZeroPriceLineCount = group.Sum(s => s.ZeroPriceLines),
                    Amount = group.Sum(s => Convert.ToDecimal(s.Amount)),
                    CoveredQty = coveredProductionIds.Sum(id => Convert.ToDecimal(productionById[id].Total)),
                    ProductionCount = productions.Count(p => p.ProductID == group.Key),
                    CoveredProductionCount = coveredProductionIds.Count
                };
            }
            return result;
        }

        private async Task<Dictionary<int, HashSet<int>>> LoadBomComponentsAsync(List<int> productIds)
        {
            var result = new Dictionary<int, HashSet<int>>();
            if (!productIds.Any())
                return result;

            var headers = await _db.BOM_HEADERS
                .Where(h => h.ProductID.HasValue && productIds.Contains(h.ProductID.Value))
                .Select(h => new { ProductID = h.ProductID.Value, h.ID, h.IsActive })
                .ToListAsync();
            var chosen = headers
                .GroupBy(h => h.ProductID)
                .ToDictionary(
                    g => g.Key,
                    g => (g.Where(h => h.IsActive == true).OrderByDescending(h => h.ID).FirstOrDefault()
                          ?? g.OrderByDescending(h => h.ID).First()).ID);
            var headerIds = chosen.Values.ToList();
            if (!headerIds.Any())
                return result;

            var lines = await _db.BOM_LINES
                .Where(l => l.BOMID.HasValue && headerIds.Contains(l.BOMID.Value) && l.ComponentID.HasValue)
                .Select(l => new { BOMID = l.BOMID.Value, ComponentID = l.ComponentID.Value })
                .Distinct()
                .ToListAsync();
            var byHeader = lines.ToLookup(l => l.BOMID, l => l.ComponentID);
            foreach (var kv in chosen)
                result[kv.Key] = new HashSet<int>(byHeader[kv.Value]);
            return result;
        }

        private async Task<Dictionary<int, decimal>> LoadPieceRateLaborAsync(DateTime start, DateTime end, List<int> productIds)
        {
            var result = new Dictionary<int, decimal>();
            if (!productIds.Any())
                return result;

            var entries = await _db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.IsDeleted != true
                    && e.WorkDate >= start
                    && e.WorkDate < end
                    && productIds.Contains(e.ProductID))
                .Select(e => new { e.ProductID, e.OperationID, e.WorkDate, e.Quantity, PositionID = (int?)e.HR_EMPLOYEES.PositionID })
                .ToListAsync();
            if (!entries.Any())
                return result;

            var entryProductIds = entries.Select(e => e.ProductID).Distinct().ToList();
            var entryOpIds = entries.Select(e => (int?)e.OperationID).Distinct().ToList();
            var tariffs = await _db.HR_PIECE_RATE_TARIFFS
                .Where(t => t.IsDeleted != true
                    && t.IsActive
                    && entryProductIds.Contains(t.ProductID)
                    && entryOpIds.Contains(t.OperationID))
                .Select(t => new { t.ProductID, t.OperationID, t.PositionID, t.EffectiveFrom, t.EffectiveTo, t.RatePerUnit })
                .ToListAsync();
            var tariffsByKey = tariffs.ToLookup(t => new { t.ProductID, OperationID = t.OperationID ?? 0 });

            foreach (var e in entries)
            {
                var candidates = tariffsByKey[new { e.ProductID, OperationID = e.OperationID }]
                    .Where(t => t.EffectiveFrom <= e.WorkDate && (t.EffectiveTo == null || t.EffectiveTo >= e.WorkDate))
                    .ToList();
                var direct = candidates
                    .Where(t => e.PositionID.HasValue ? t.PositionID == e.PositionID.Value : t.PositionID == null)
                    .OrderByDescending(t => t.EffectiveFrom)
                    .FirstOrDefault();
                var rate = direct?.RatePerUnit
                    ?? candidates.Where(t => t.PositionID == null).OrderByDescending(t => t.EffectiveFrom).Select(t => (decimal?)t.RatePerUnit).FirstOrDefault()
                    ?? 0m;

                result.TryGetValue(e.ProductID, out var sum);
                result[e.ProductID] = sum + e.Quantity * rate;
            }
            return result;
        }

        private async Task<decimal> LoadAdministrativeTotalAsync(int year, int month)
        {
            var approvedHourly = await _db.HR_PAYROLL_LINES
                .Where(l => l.IsDeleted != true
                    && l.HR_PAYROLL_HEADERS.IsDeleted != true
                    && l.HR_PAYROLL_HEADERS.Year == year
                    && l.HR_PAYROLL_HEADERS.Month == month
                    && l.HR_PAYROLL_HEADERS.PayrollType != "PieceRate"
                    && (l.HR_PAYROLL_HEADERS.Status == "Approved" || l.HR_PAYROLL_HEADERS.Status == "Posted"))
                .SumAsync(l => l.GrossSalary ?? l.NetSalary) ?? 0m;
            if (approvedHourly > 0)
                return approvedHourly;

            // Tasdiqlangan oylik yo'q bo'lsa: tabel soatlari × (oklad / 160).
            var timesheet = await _db.HR_TIMESHEET_LINES
                .Where(l => l.IsDeleted != true
                    && l.HR_TIMESHEET_HEADERS.IsDeleted != true
                    && l.HR_TIMESHEET_HEADERS.Year == year
                    && l.HR_TIMESHEET_HEADERS.Month == month)
                .Select(l => new { Hours = l.TotalWorkedHours, BaseSalary = (decimal?)l.HR_EMPLOYEES.BaseSalary })
                .ToListAsync();
            return timesheet.Sum(l =>
            {
                var baseSalary = l.BaseSalary ?? 0m;
                return baseSalary > 0 ? (l.Hours ?? 0m) * (baseSalary / 160m) : 0m;
            });
        }

        internal static string ExtractBuyTokenFromOrderNo(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return null;
            var digits = new string(orderNo.Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
                return null;
            return digits.Length <= 6 ? digits : digits.Substring(digits.Length - 6);
        }

        // ---------------- Ichki tiplar ----------------

        private class PiHeader
        {
            public int ID { get; set; }
            public int OrderID { get; set; }
            public DateTime InvoiceDate { get; set; }
            public double? Amount { get; set; }
            public string InvoiceNo { get; set; }
        }

        private class IncomeHeader
        {
            public int ID { get; set; }
            public int InvoiceID { get; set; }
            public double? TotalPrice { get; set; }
        }

        private class DocLine
        {
            public int ID { get; set; }
            public int DocID { get; set; }
            public int ProductID { get; set; }
            public double Qty { get; set; }
            public double? Amount { get; set; }
        }

        private class WaybillRow
        {
            public int DocID { get; set; }
            public string WaybillNo { get; set; }
            public double? Amount { get; set; }
        }

        private class ProductChain
        {
            public int ProductID { get; set; }
            public decimal ProducedQty { get; set; }
            public HashSet<int> BomComponents { get; set; }
            public HashSet<int> PoIds { get; set; }
            public List<PiHeader> PiInMonth { get; set; }
            public List<DocLine> PiMatchedLines { get; set; }
            public List<IncomeHeader> Incomes { get; set; }
            public List<DocLine> IncomeMatchedLines { get; set; }
        }
    }

    internal class WarehouseIssueInfo
    {
        public int ExpenseCount { get; set; }
        public int LineCount { get; set; }
        public int ZeroPriceLineCount { get; set; }
        public decimal Amount { get; set; }
        public decimal CoveredQty { get; set; }
        public int ProductionCount { get; set; }
        public int CoveredProductionCount { get; set; }
    }

    internal class TannarxFinanceTotals
    {
        public decimal Material { get; set; }
        public decimal TaxBank { get; set; }
        public decimal Other { get; set; }
        public int TransactionCount { get; set; }
        public HashSet<string> MaterialCategories { get; } = new HashSet<string>();
        public HashSet<string> TaxBankCategories { get; } = new HashSet<string>();
        public HashSet<string> OtherCategories { get; } = new HashSet<string>();
        public string BaseCurrencyCode { get; set; }
        public int SkippedForeignCount { get; set; }
        public Dictionary<string, decimal> SkippedForeignByCurrency { get; } = new Dictionary<string, decimal>();
        /// <summary>Kategoriya bo'yicha: qaysi guruhga tushdi yoki nima uchun chiqarildi (oy manbalari sahifasi uchun).</summary>
        public List<TannarxFinanceCategoryLine> Lines { get; } = new List<TannarxFinanceCategoryLine>();
    }

    internal class TannarxFinanceCategoryLine
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public string Bucket { get; set; }
        public string Reason { get; set; }
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }

    internal class TannarxMonthActualResult
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public decimal MonthProducedQty { get; set; }
        public decimal AdminTotal { get; set; }
        public TannarxFinanceTotals Finance { get; set; }
        public Dictionary<int, TannarxProductActualResult> Products { get; } = new Dictionary<int, TannarxProductActualResult>();
        public Dictionary<int, decimal> ProducedWithoutTannarx { get; } = new Dictionary<int, decimal>();
        public decimal PieceRateLaborTotal { get; set; }
    }

    internal class TannarxProductActualResult
    {
        public int ProductID { get; set; }
        public decimal ProducedQty { get; set; }
        public MaterialDecision Material { get; set; }
        public decimal MaterialTransportPerUnit { get; set; }
        public decimal FinishedTransportPerUnit { get; set; }
        public decimal LaborAmount { get; set; }
        public decimal LaborPerUnit { get; set; }
        public decimal AdminPerUnit { get; set; }
        public decimal TaxBankPerUnit { get; set; }
        public decimal OtherPerUnit { get; set; }

        // Izoh (calculation note) uchun iz
        public List<string> PathAttempts { get; } = new List<string>();
        public WarehouseIssueInfo WarehouseIssue { get; set; }
        public int BomComponentCount { get; set; }
        public List<string> SalesOrderNos { get; set; } = new List<string>();
        public List<string> PurchaseOrderNos { get; set; } = new List<string>();
        public List<string> PurchaseInvoiceNos { get; set; } = new List<string>();
        public decimal PiHeaderAmount { get; set; }
        public decimal PiLinesAmount { get; set; }
        public decimal PiMatchedAmount { get; set; }
        public decimal PiMatchedAllocatedAmount { get; set; }
        public int PiSharedLineCount { get; set; }
        public decimal PiTotalQty { get; set; }
        public decimal PiMatchedQty { get; set; }
        public List<string> MaterialWaybillNos { get; set; } = new List<string>();
        public decimal MaterialWaybillAmount { get; set; }
        public decimal MaterialTransportAllocated { get; set; }
        public List<string> SalesInvoiceNos { get; set; } = new List<string>();
        public List<string> FinishedWaybillNos { get; set; } = new List<string>();
        public decimal FinishedWaybillAmount { get; set; }
        public decimal FinishedTransportAllocated { get; set; }
    }

    internal class MaterialDecision
    {
        public bool Passed { get; private set; }
        public string Path { get; private set; }
        public string MethodCode { get; private set; }
        public string ConfidenceScore { get; private set; }
        public decimal CoveragePercent { get; private set; }
        public decimal ReconcileDelta { get; private set; }
        public decimal SourceAmount { get; private set; }
        public decimal DenominatorQty { get; private set; }
        public decimal UnitCost { get; private set; }
        public string ReasonCode { get; private set; }

        public static MaterialDecision Pass(string path, string methodCode, decimal unitCost, decimal sourceAmount, decimal denominatorQty, decimal coverage, decimal reconcile)
        {
            return new MaterialDecision
            {
                Passed = true,
                Path = path,
                MethodCode = methodCode,
                CoveragePercent = coverage,
                ReconcileDelta = reconcile,
                SourceAmount = sourceAmount,
                DenominatorQty = denominatorQty,
                UnitCost = unitCost,
                ReasonCode = "OK",
                ConfidenceScore = (coverage >= 0.8m && reconcile <= 0.02m) ? "HIGH" : "MEDIUM"
            };
        }

        public static MaterialDecision Fail(string path, string reasonCode, decimal coverage = 0m, decimal reconcile = 0m)
        {
            return new MaterialDecision
            {
                Passed = false,
                Path = path,
                MethodCode = path,
                CoveragePercent = coverage,
                ReconcileDelta = reconcile,
                ReasonCode = reasonCode,
                ConfidenceScore = "LOW"
            };
        }

        public static MaterialDecision Empty(string reasonCode)
        {
            return new MaterialDecision
            {
                Passed = false,
                Path = "NONE",
                MethodCode = "NONE",
                ReasonCode = reasonCode,
                ConfidenceScore = "LOW"
            };
        }
    }
}
