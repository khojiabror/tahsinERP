// Ombor/FIFO va QQS ichki hisoblari: ikki build (masalan eski double API va yangi decimal API) natijalarini solishtirish uchun.
//   FifoProbe.exe <out.txt> [write]   (Run-FifoProbe.ps1 orqali)
// OLD belgisi bilan kompilyatsiya — double API. "write" — sinov nusxa bazasida, tranzaksiya oxirida ROLLBACK.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Stock;
#if OLD
using Q = System.Double;
#else
using Q = System.Decimal;
#endif

class FifoProbe
{
    static StreamWriter _w;
    static void Put(string key, object v)
    {
        string s;
        if (v == null) s = "null";
        else if (v is double || v is decimal || v is float) s = Convert.ToDecimal(v).ToString("0.##########", CultureInfo.InvariantCulture);
        else s = Convert.ToString(v, CultureInfo.InvariantCulture);
        _w.WriteLine(key + "\t" + s);
    }
    static void Try(string key, Action a)
    {
        try { a(); }
        catch (Exception ex)
        {
            var e = ex; while (e.InnerException != null) e = e.InnerException;
            Put(key + " EXC", e.GetType().Name + ": " + e.Message);
        }
    }

    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        bool write = args.Length > 1 && args[1] == "write";
        using (_w = new StreamWriter(args[0]))
        using (var db = new DBTHSNEntities())
        {
            var lines = db.EXPENSE_PRODUCTS.AsNoTracking()
                .Where(ep => ep.EXPENS.IsDeleted != true)
                .Select(ep => new { ep.ID, ep.ExpenseID, ep.EXPENS.SenderWHID, ep.ProductID, ep.Amount, ep.UnitID, ep.EXPENS.SendStatus, ep.EXPENS.IssueDateTime })
                .OrderBy(x => x.ID).ToList();

            // A) FIFO tannarxi va birlik konversiyasi — faqat o'qish
            foreach (var l in lines)
            {
                Try("cost " + l.ID, () =>
                {
                    var c = StockCostService.IssuedLineUnitCost(db, l.SenderWHID, l.ProductID, (Q)l.Amount, l.UnitID, l.ID);
                    Put("cost " + l.ID, c);
                    Put("stored " + l.ID, StockCostService.StoredUnitCost(c));
                });
                Try("unit " + l.ID, () =>
                {
                    int su;
                    var q = StockLedger.ToStockUnit(db, l.SenderWHID, l.ProductID, (Q)l.Amount, l.UnitID, out su, allowReunit: false);
                    Put("unit " + l.ID, q + " u" + su);
                });
            }

            // C) BOM/norma sarfi
            var prods = db.PRODUCT_PRODUCTION.AsNoTracking().Where(p => p.IsDeleted != true && p.Total > 0)
                .Select(p => new { p.ID, p.ProductID, p.Total }).OrderBy(p => p.ID).ToList();
            var pcs = new ProductionConsumptionService(db);
            foreach (var p in prods)
            {
                Try("req " + p.ID, () =>
                {
                    foreach (var r in pcs.CalculateRequiredRawMaterialsAsync(p.ProductID, (Q)p.Total).Result.OrderBy(r => r.MaterialID))
                        Put("req " + p.ID + " m" + r.MaterialID, r.TotalConsumption + " u" + r.UnitID + " " + r.RequiredQtyPieces);
                });
            }

            // E) QQS: xarid/sotuv shartnomalari, buyurtmalar, kirim qatorlari
            foreach (var cid in db.P_CONTRACTS.AsNoTracking().Select(c => c.ID).OrderBy(x => x).ToList())
                Try("pvat " + cid, () =>
                {
                    Put("pvat " + cid, PContractVatHelper.CalculateContractVatRateAsync(db, cid).Result);
                    foreach (var kv in PContractVatHelper.GetProductVatRatesByContractAsync(db, cid).Result.OrderBy(k => k.Key))
                        Put("pvat " + cid + " p" + kv.Key, kv.Value);
                });
            foreach (var cid in db.S_CONTRACTS.AsNoTracking().Select(c => c.ID).OrderBy(x => x).ToList())
                Try("svat " + cid, () =>
                {
                    Put("svat " + cid, SContractVatHelper.CalculateContractVatRate(db, cid));
                    var prods = db.S_CONTRACT_PRODUCTS.AsNoTracking().Where(p => p.ContractID == cid).ToList();
                    Put("stot " + cid, SContractVatHelper.CalculateContractTotalWithVat(prods));
                    foreach (var p in prods.OrderBy(p => p.ID))
                        Put("sline " + p.ID, SContractVatHelper.GetLineTotalWithVat(p) + " " + SContractVatHelper.GetVatPercentFromLine(SContractVatHelper.GetLineBaseAmount(p), (Q?)p.vatamount)
                            + " " + SContractVatHelper.CalculateVatAmount(SContractVatHelper.GetLineBaseAmount(p), 12));
                    foreach (var kv in SContractVatHelper.GetProductVatRates(db, cid).OrderBy(k => k.Key))
                        Put("svat " + cid + " p" + kv.Key, kv.Value);
                });
            var orders = db.P_ORDERS.AsNoTracking().Select(o => new { o.ID, o.ContractID, o.Amount, Net = o.P_ORDER_PRODUCTS.Sum(p => (decimal?)p.TotalPrice) }).OrderBy(o => o.ID).ToList();
            foreach (var o in orders)
                Try("pord " + o.ID, () =>
                {
                    var rate = PContractVatHelper.CalculateContractVatRateAsync(db, o.ContractID).Result;
                    var net = o.Net ?? 0;
                    Put("pord " + o.ID, VatMath.ApplyVat((Q)net, rate) + " " + VatMath.RateFromGrossNet((Q)net, (Q)(o.Amount ?? net)));
                });
            foreach (var ip in db.INCOME_PRODUCTS.AsNoTracking().OrderBy(x => x.ID).Select(x => new { x.ID, x.Amount, x.PiecePrice, x.VATRate }).ToList())
                Try("igross " + ip.ID, () => Put("igross " + ip.ID,
                    IncomeDocumentService.GrossTotal((Q)ip.Amount, (Q?)ip.PiecePrice, (Q)(ip.VATRate ?? 0))));
            foreach (var raw in new[] { "12", "12,5", "12.5", "", "x", "150", "-3", "7.125" })
                Put("parse [" + raw + "]", VatMath.ParsePercent(raw));

            if (write)
            {
                // B) yozish yo'li: oxirgi 60 ta chiqim hujjatini bekor qilib qayta chiqarish, keyin ROLLBACK
                using (var tx = db.Database.BeginTransaction())
                {
                    var docs = lines.Where(l => l.SendStatus == true).Select(l => l.ExpenseID).Distinct().OrderByDescending(x => x).Take(60).ToList();
                    var sel = lines.Where(l => docs.Contains(l.ExpenseID)).OrderBy(l => l.ID).ToList();
                    foreach (var l in sel)
                        Try("rev " + l.ID, () =>
                        {
                            StockLedger.ReverseIssueLine(db, l.SenderWHID, l.ProductID, (Q)l.Amount, l.UnitID > 0 ? l.UnitID : 1, l.ID);
                            Put("rev " + l.ID, StockLedger.GetOnHand(db, l.SenderWHID, l.ProductID));
                        });
                    foreach (var l in sel.OrderBy(x => x.IssueDateTime).ThenBy(x => x.ID))
                        Try("iss " + l.ID, () =>
                        {
                            StockLedger.IssueLine(db, l.SenderWHID, l.ProductID, (Q)l.Amount, l.UnitID, l.ID);
                            var c = StockCostService.IssuedLineUnitCost(db, l.SenderWHID, l.ProductID, (Q)l.Amount, l.UnitID, l.ID);
                            Put("iss " + l.ID, StockLedger.GetOnHand(db, l.SenderWHID, l.ProductID));
                            Put("isscost " + l.ID, StockCostService.StoredUnitCost(c));
                        });
                    var alloc = db.Database.SqlQuery<AllocRow>(
                        "SELECT ExpenseProductID, LotID, CAST(Qty AS DECIMAL(18,6)) AS Qty FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS ORDER BY ExpenseProductID, LotID").ToList();
                    foreach (var a in alloc) Put("alloc " + a.ExpenseProductID + " l" + a.LotID, a.Qty);

                    // D) kasr delta bilan Adjust (kirim + chiqim)
                    var some = db.PRODUCT_STOCKS.AsNoTracking().Where(s => s.Amount > 1).OrderBy(s => s.ID).Take(30).ToList();
                    foreach (var s in some)
                        Try("adj " + s.ID, () =>
                        {
                            var a1 = StockLedger.Increase(db, s.WHID, s.ProductID, (Q)1.23456789m);
                            var a2 = StockLedger.Decrease(db, s.WHID, s.ProductID, (Q)0.333333333m);
                            Put("adj " + s.ID, a1 + " " + a2);
                        });
                    Try("over", () => StockLedger.Decrease(db, some[0].WHID, some[0].ProductID, (Q)1000000000m));
                    var stock = db.Database.SqlQuery<StockRow>("SELECT WHID, ProductID, CAST(SUM(Amount) AS DECIMAL(38,6)) AS Amount FROM dbo.PRODUCT_STOCKS GROUP BY WHID, ProductID ORDER BY WHID, ProductID").ToList();
                    foreach (var s in stock) Put("stock " + s.WHID + "/" + s.ProductID, s.Amount);
                    tx.Rollback();
                }
            }
        }
        return 0;
    }

    class AllocRow { public int ExpenseProductID { get; set; } public int LotID { get; set; } public decimal Qty { get; set; } }
    class StockRow { public int WHID { get; set; } public int ProductID { get; set; } public decimal Amount { get; set; } }
}
