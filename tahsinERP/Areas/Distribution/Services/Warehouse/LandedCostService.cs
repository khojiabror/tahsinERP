using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Services.Stock;

namespace tahsinERP.Areas.Distribution.Services.Warehouse
{
    /// <summary>
    /// Landed cost (TZ 5-bo'lim): yetkazib beruvchidan qabul qilingan kirim(lar)ga qo'shimcha xarajat — yo'l haqi, bojxona,
    /// boshqa — kirim qatorlariga qiymat / miqdor / og'irlik ulushida taqsimlanadi va dbo.INCOME_PRODUCTS.PiecePrice oshiriladi.
    /// FIFO tannarx (StockCostService, qoldiq qiymati hisobotlari) shu narxni o'qiydi, shuning uchun keyingi chiqimlar yangi
    /// tannarxda bo'ladi. Allaqachon chiqarilgan miqdor qayta baholanmaydi — unga tushgan qism alohida ko'rsatiladi
    /// (<see cref="LandedCost.ConsumedAmount"/>). Bekor qilinganda qo'shilgan narx ayiriladi.
    /// Kirim: qabul qilingan, o'chirilmagan, ko'chirma/ishlab chiqarish/DLOC-ASN emas.
    /// </summary>
    public static class LandedCostService
    {
        public const string Prefix = "DL";

        public sealed class IncomeRow
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public DateTime IssueDateTime { get; set; }
            public int WarehouseID { get; set; }
            public string Warehouse { get; set; }
            public string Supplier { get; set; }
            public decimal Value { get; set; }
            public int Lines { get; set; }
        }

        public sealed class Allocation
        {
            public int IncomeID { get; set; }
            public string IncomeDocNo { get; set; }
            public int IncomeProductID { get; set; }
            public int ProductID { get; set; }
            public string Product { get; set; }
            public string Unit { get; set; }
            public decimal Qty { get; set; }
            public decimal PiecePrice { get; set; }
            public decimal BaseValue { get; set; }
            public decimal? WeightKg { get; set; }
            public decimal Allocated { get; set; }
            public decimal UnitAdd { get; set; }
            public decimal ConsumedQty { get; set; }
            public decimal NewPiecePrice => PiecePrice + UnitAdd;
        }

        // ---------- kirimlar ----------

        /// <summary>Xarajat qo'shish mumkin bo'lgan kirimlar (so'nggi 120 kun, qidiruv bo'yicha).</summary>
        public static List<IncomeRow> EligibleIncomes(DBTHSNEntities erp, string q, DateTime now, int take = 50)
        {
            var from = now.Date.AddDays(-120);
            var query = Eligible(erp).Where(i => i.IssueDateTime >= from);
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(i => i.DocNo.Contains(q) || (i.P_INVOICES != null && i.P_INVOICES.SUPPLIER.Name.Contains(q)));
            }
            return query.OrderByDescending(i => i.IssueDateTime).Take(take).Select(i => new IncomeRow
            {
                ID = i.ID, DocNo = i.DocNo, IssueDateTime = i.IssueDateTime, WarehouseID = i.ReceivingWHID,
                Warehouse = erp.WAREHOUSES.Where(w => w.ID == i.ReceivingWHID).Select(w => w.WHName).FirstOrDefault(), Supplier = i.P_INVOICES != null ? i.P_INVOICES.SUPPLIER.Name : null,
                Value = i.INCOME_PRODUCTS.Sum(p => (decimal?)(p.Amount * (p.PiecePrice ?? 0))) ?? 0,
                Lines = i.INCOME_PRODUCTS.Count()
            }).ToList();
        }

        private static IQueryable<INCOME> Eligible(DBTHSNEntities erp)
        {
            string asn = LegacyDlocAssignmentService.AssignmentDocNoPrefix;
            return erp.INCOMES.Where(i => i.IsDeleted != true && i.RecieveStatus == true && i.SenderWHID == null && i.ProductionID == null
                                          && (i.DocNo == null || !i.DocNo.StartsWith(asn)) && i.INCOME_PRODUCTS.Any());
        }

        public static IncomeRow Income(DBTHSNEntities erp, int incomeId) =>
            Eligible(erp).Where(i => i.ID == incomeId).Select(i => new IncomeRow
            {
                ID = i.ID, DocNo = i.DocNo, IssueDateTime = i.IssueDateTime, WarehouseID = i.ReceivingWHID, Warehouse = erp.WAREHOUSES.Where(w => w.ID == i.ReceivingWHID).Select(w => w.WHName).FirstOrDefault(),
                Supplier = i.P_INVOICES != null ? i.P_INVOICES.SUPPLIER.Name : null,
                Value = i.INCOME_PRODUCTS.Sum(p => (decimal?)(p.Amount * (p.PiecePrice ?? 0))) ?? 0, Lines = i.INCOME_PRODUCTS.Count()
            }).FirstOrDefault();

        // ---------- qoralama ----------

        public static int Create(DistributionUnitOfWork uow, LandedCostBasis basis, string note, int userId, DateTime now)
        {
            if (!Enum.IsDefined(typeof(LandedCostBasis), basis)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var doc = new LandedCost
            {
                DocNo = DstNumbering.Next(uow.Dst, "LANDED_COSTS", Prefix, now), DocDate = now.Date, Status = DocumentStatus.Draft,
                Basis = basis, Note = Clean(note, 300), CreatedBy = userId, CreatedAt = now
            };
            uow.Dst.LandedCosts.Add(doc);
            uow.Dst.SaveChanges();
            return doc.ID;
        }

        public static LandedCost RequireDraft(DistributionDbContext db, int id)
        {
            var doc = db.LandedCosts.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (doc.Status != DocumentStatus.Draft) throw new DstValidationException(DstLocalization.T("Doc_NotDraft"));
            return doc;
        }

        public static void SetHeader(DistributionDbContext db, int id, LandedCostBasis basis, string note)
        {
            var doc = RequireDraft(db, id);
            if (!Enum.IsDefined(typeof(LandedCostBasis), basis)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            doc.Basis = basis;
            doc.Note = Clean(note, 300);
            db.SaveChanges();
        }

        public static void AddIncome(DistributionDbContext db, DBTHSNEntities erp, int id, int incomeId)
        {
            RequireDraft(db, id);
            if (Income(erp, incomeId) == null) throw new DstValidationException(DstLocalization.T("LandedCost_IncomeNotEligible"));
            if (db.LandedCostIncomes.Any(x => x.LandedCostID == id && x.IncomeID == incomeId)) return;
            db.LandedCostIncomes.Add(new LandedCostIncome { LandedCostID = id, IncomeID = incomeId });
            db.SaveChanges();
        }

        public static void RemoveIncome(DistributionDbContext db, int id, int incomeId)
        {
            RequireDraft(db, id);
            db.LandedCostIncomes.RemoveRange(db.LandedCostIncomes.Where(x => x.LandedCostID == id && x.IncomeID == incomeId).ToList());
            db.SaveChanges();
        }

        public static void AddItem(DistributionDbContext db, int id, string kind, string description, decimal amount)
        {
            RequireDraft(db, id);
            if (!LandedCostKinds.All.Contains(kind)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (amount <= 0) throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));
            db.LandedCostItems.Add(new LandedCostItem { LandedCostID = id, Kind = kind, Description = Clean(description, 200), Amount = Math.Round(amount, 2) });
            db.SaveChanges();
        }

        public static void RemoveItem(DistributionDbContext db, int id, int itemId)
        {
            RequireDraft(db, id);
            db.LandedCostItems.RemoveRange(db.LandedCostItems.Where(x => x.LandedCostID == id && x.ID == itemId).ToList());
            db.SaveChanges();
        }

        // ---------- taqsimlash ----------

        /// <summary>Taqsimlash (saqlanmaydi): o'tkazilgan hujjatda — saqlangan qatorlar.</summary>
        public static List<Allocation> Allocate(DistributionDbContext db, DBTHSNEntities erp, LandedCost doc)
        {
            if (doc.Status != DocumentStatus.Draft)
            {
                var saved = db.LandedCostLines.Where(l => l.LandedCostID == doc.ID).ToList();
                var info = LineInfo(erp, saved.Select(s => s.IncomeProductID).ToList());
                return saved.Select(s => new Allocation
                {
                    IncomeID = s.IncomeID, IncomeProductID = s.IncomeProductID, ProductID = s.ProductID, Qty = s.Qty, BaseValue = s.BaseValue,
                    WeightKg = s.WeightKg, Allocated = s.Allocated, UnitAdd = s.UnitAdd, ConsumedQty = s.ConsumedQty,
                    IncomeDocNo = info.TryGetValue(s.IncomeProductID, out var i) ? i.IncomeDocNo : null, Product = i?.Product, Unit = i?.Unit,
                    PiecePrice = s.Qty == 0 ? 0 : Math.Round(s.BaseValue / s.Qty, 6)
                }).ToList();
            }

            var incomeIds = db.LandedCostIncomes.Where(x => x.LandedCostID == doc.ID).Select(x => x.IncomeID).ToList();
            decimal total = db.LandedCostItems.Where(x => x.LandedCostID == doc.ID).Sum(x => (decimal?)x.Amount) ?? 0;
            var lines = (from ip in erp.INCOME_PRODUCTS
                         where incomeIds.Contains(ip.IncomeID) && ip.Amount > 0
                         orderby ip.IncomeID, ip.ID
                         select new Allocation
                         {
                             IncomeID = ip.IncomeID, IncomeDocNo = ip.INCOME.DocNo, IncomeProductID = ip.ID, ProductID = ip.ProductID,
                             Product = ip.PRODUCT.PNo + " " + ip.PRODUCT.Name, Unit = ip.UNIT.ShortName, Qty = ip.Amount, PiecePrice = ip.PiecePrice ?? 0
                         }).ToList();
            foreach (var l in lines) l.BaseValue = Math.Round(l.Qty * l.PiecePrice, 2, MidpointRounding.AwayFromZero);

            if (doc.Basis == LandedCostBasis.Weight)
            {
                var ids = lines.Select(l => l.ProductID).Distinct().ToList();
                var weights = db.ProductProfiles.Where(p => ids.Contains(p.ProductID)).ToDictionary(p => p.ProductID, p => p.BaseWeightKg);
                var baseUnits = db.Products.Where(p => ids.Contains(p.ID)).ToDictionary(p => p.ID, p => p.UnitID);
                var lineUnits = erp.INCOME_PRODUCTS.Where(ip => incomeIds.Contains(ip.IncomeID)).ToDictionary(ip => ip.ID, ip => ip.UnitID);
                foreach (var l in lines)
                {
                    // og'irlik asosiy birlik uchun berilgan — boshqa birlikdagi kirim qatorida noma'lum
                    if (weights.TryGetValue(l.ProductID, out var w) && w.HasValue && lineUnits[l.IncomeProductID] == baseUnits[l.ProductID])
                        l.WeightKg = Math.Round(l.Qty * w.Value, 4);
                }
            }

            Distribute(lines, total, doc.Basis);
            foreach (var g in lines.GroupBy(l => l.IncomeID))
            {
                var consumed = ConsumedByLine(erp, g.Key);
                foreach (var l in g) l.ConsumedQty = consumed.TryGetValue(l.IncomeProductID, out var c) ? c : 0;
            }
            return lines;
        }

        /// <summary>Ulush bo'yicha taqsimlash: yaxlitlash qoldig'i eng katta qatorga (jami aynan teng bo'lsin).</summary>
        public static void Distribute(IList<Allocation> lines, decimal total, LandedCostBasis basis)
        {
            foreach (var l in lines) { l.Allocated = 0; l.UnitAdd = 0; }
            if (lines.Count == 0 || total <= 0) return;
            Func<Allocation, decimal> weightOf;
            switch (basis)
            {
                case LandedCostBasis.Quantity: weightOf = l => l.Qty; break;
                case LandedCostBasis.Weight: weightOf = l => l.WeightKg ?? 0; break;
                default: weightOf = l => l.BaseValue; break;
            }
            decimal sum = lines.Sum(weightOf);
            if (sum <= 0) return;
            foreach (var l in lines)
                l.Allocated = Math.Round(total * weightOf(l) / sum, 2, MidpointRounding.AwayFromZero);
            decimal diff = total - lines.Sum(l => l.Allocated);
            if (diff != 0) lines.OrderByDescending(weightOf).First().Allocated += diff;
            foreach (var l in lines)
                l.UnitAdd = l.Qty == 0 ? 0 : Math.Round(l.Allocated / l.Qty, 6, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Kirim qatoridan allaqachon chiqarilgan miqdor: DLOC — lotlar bo'yicha aniq; oddiy omborda — FIFO joylashuvi
        /// (StockCostService bilan bir xil: qoldiq eng yangi kirimlardan iborat).
        /// </summary>
        private static Dictionary<int, decimal> ConsumedByLine(DBTHSNEntities erp, int incomeId)
        {
            var income = erp.INCOMES.Find(incomeId);
            var result = new Dictionary<int, decimal>();
            if (income == null) return result;
            var lines = erp.INCOME_PRODUCTS.Where(ip => ip.IncomeID == incomeId).Select(ip => new { ip.ID, ip.ProductID, ip.Amount }).ToList();

            if (StockLedger.IsDlocWarehouse(erp, income.ReceivingWHID))
            {
                var lots = erp.Database.SqlQuery<LotUse>(
                    "SELECT IncomeProductID, SUM(QtyConsumed) AS Consumed FROM dbo.INCOME_LOTS WHERE IncomeID = @p0 GROUP BY IncomeProductID", incomeId).ToList();
                foreach (var l in lines) result[l.ID] = lots.FirstOrDefault(x => x.IncomeProductID == l.ID)?.Consumed ?? 0;
                return result;
            }

            foreach (var g in lines.GroupBy(l => l.ProductID))
            {
                decimal onHand = StockLedger.GetOnHand(erp, income.ReceivingWHID, g.Key);
                var layers = erp.Database.SqlQuery<Layer>(@"
SELECT ip.ID, ip.Amount FROM dbo.INCOME_PRODUCTS ip JOIN dbo.INCOMES i ON i.ID = ip.IncomeID
WHERE i.ReceivingWHID = @p0 AND ip.ProductID = @p1 AND ip.Amount > 0 AND ISNULL(i.IsDeleted, 0) = 0 AND i.RecieveStatus = 1
  AND (i.DocNo IS NULL OR i.DocNo NOT LIKE @p2)
ORDER BY i.IssueDateTime DESC, i.ID DESC, ip.ID DESC", income.ReceivingWHID, g.Key, LegacyDlocAssignmentService.AssignmentDocNoPrefix + "%").ToList();
                decimal left = Math.Max(0, onHand);
                foreach (var layer in layers)
                {
                    decimal remaining = Math.Min(layer.Amount, left);
                    left -= remaining;
                    if (g.Any(l => l.ID == layer.ID)) result[layer.ID] = layer.Amount - remaining;
                }
                foreach (var l in g) if (!result.ContainsKey(l.ID)) result[l.ID] = l.Amount;
            }
            return result;
        }

        private sealed class LotUse { public int IncomeProductID { get; set; } public decimal Consumed { get; set; } }
        private sealed class Layer { public int ID { get; set; } public decimal Amount { get; set; } }

        private static Dictionary<int, Allocation> LineInfo(DBTHSNEntities erp, List<int> incomeProductIds) =>
            erp.INCOME_PRODUCTS.Where(ip => incomeProductIds.Contains(ip.ID)).Select(ip => new Allocation
            {
                IncomeProductID = ip.ID, IncomeDocNo = ip.INCOME.DocNo, Product = ip.PRODUCT.PNo + " " + ip.PRODUCT.Name, Unit = ip.UNIT.ShortName
            }).ToList().ToDictionary(a => a.IncomeProductID);

        // ---------- o'tkazish / bekor qilish ----------

        /// <summary>O'tkazish: PiecePrice oshiriladi, taqsimlash saqlanadi. Chaqiruvchi Commit qiladi.</summary>
        public static void Post(DistributionUnitOfWork uow, int id, int userId, DateTime now)
        {
            var db = uow.Dst;
            var erp = uow.Erp;
            var doc = RequireDraft(db, id);
            var incomeIds = db.LandedCostIncomes.Where(x => x.LandedCostID == id).Select(x => x.IncomeID).OrderBy(x => x).ToList();
            if (incomeIds.Count == 0) throw new DstValidationException(DstLocalization.T("LandedCost_NoIncomes"));
            if (!db.LandedCostItems.Any(x => x.LandedCostID == id)) throw new DstValidationException(DstLocalization.T("LandedCost_NoItems"));
            foreach (var incomeId in incomeIds)
            {
                LockIncome(db, incomeId);
                if (Income(erp, incomeId) == null) throw new DstValidationException(DstLocalization.T("LandedCost_IncomeNotEligible"));
            }

            var lines = Allocate(db, erp, doc);
            if (doc.Basis == LandedCostBasis.Weight && lines.Any(l => l.WeightKg == null))
                throw new DstValidationException(DstLocalization.T("LandedCost_NoWeight", string.Join(", ", lines.Where(l => l.WeightKg == null).Select(l => l.Product).Distinct().Take(5))));
            if (lines.Sum(l => l.Allocated) <= 0) throw new DstValidationException(DstLocalization.T("LandedCost_NothingToAllocate"));

            var byId = erp.INCOME_PRODUCTS.Where(ip => incomeIds.Contains(ip.IncomeID)).ToList().ToDictionary(ip => ip.ID);
            foreach (var l in lines)
            {
                var ip = byId[l.IncomeProductID];
                ip.PiecePrice = Math.Round((ip.PiecePrice ?? 0) + l.UnitAdd, 6);
                db.LandedCostLines.Add(new LandedCostLine
                {
                    LandedCostID = id, IncomeID = l.IncomeID, IncomeProductID = l.IncomeProductID, ProductID = l.ProductID, Qty = l.Qty,
                    BaseValue = l.BaseValue, WeightKg = l.WeightKg, Allocated = l.Allocated, UnitAdd = l.UnitAdd, ConsumedQty = l.ConsumedQty
                });
            }
            erp.SaveChanges();
            doc.TotalAmount = lines.Sum(l => l.Allocated);
            doc.ConsumedAmount = Math.Round(lines.Sum(l => l.UnitAdd * l.ConsumedQty), 2);
            DocumentLifecycle.Post(doc, userId, now);
            db.SaveChanges();
        }

        /// <summary>Bekor qilish: o'tkazilgan bo'lsa qo'shilgan narx kirim qatorlaridan ayiriladi.</summary>
        public static void Cancel(DistributionUnitOfWork uow, int id, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var doc = db.LandedCosts.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            bool posted = doc.Status == DocumentStatus.Posted;
            DocumentLifecycle.Cancel(doc, userId, reason, now);
            if (posted)
            {
                var lines = db.LandedCostLines.Where(l => l.LandedCostID == id).ToList();
                foreach (var incomeId in lines.Select(l => l.IncomeID).Distinct().OrderBy(x => x)) LockIncome(db, incomeId);
                var ids = lines.Select(l => l.IncomeProductID).ToList();
                var byId = uow.Erp.INCOME_PRODUCTS.Where(ip => ids.Contains(ip.ID)).ToList().ToDictionary(ip => ip.ID);
                foreach (var l in lines)
                    if (byId.TryGetValue(l.IncomeProductID, out var ip))
                        ip.PiecePrice = Math.Max(0, Math.Round((ip.PiecePrice ?? 0) - l.UnitAdd, 6));
                uow.Erp.SaveChanges();
            }
            db.SaveChanges();
        }

        private static void LockIncome(DistributionDbContext db, int incomeId)
        {
            db.Database.ExecuteSqlCommand(System.Data.Entity.TransactionalBehavior.DoNotEnsureTransaction,
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @p0, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
                "IF @r < 0 THROW 51003, 'income lock timeout', 1;", "dst.landed:" + incomeId);
        }

        private static string Clean(string s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
