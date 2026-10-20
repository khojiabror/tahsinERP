using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Distribution.Services.Warehouse;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers.Api
{
    /// <summary>
    /// 3-bosqich: omborchi Mini App'i (shtrix-kod bilan inventarizatsiya va buyurtma yig'ish) va rahbarning botdagi
    /// hisobdan chiqarish tasdig'i. Ombor scope'i — web bilan bir xil (ScopeTypes.Warehouse).
    /// </summary>
    public partial class DstApiController
    {
        private ScopeFilter WarehouseScope(DistributionDbContext db) => new ScopeService(db).For(UserName, ScopeTypes.Warehouse);

        private static readonly byte[] PickStatuses = { (byte)OrderStatus.Confirmed, (byte)OrderStatus.Picking, (byte)OrderStatus.PartiallyShipped };

        /// <summary>Shtrix-kod yoki artikul → tovar va birlik koeffitsienti (qutidagi shtrix-kod — quti miqdori).</summary>
        private static (int ProductId, decimal Factor, string Name)? ResolveCode(DistributionDbContext db, string code)
        {
            code = code?.Trim();
            if (string.IsNullOrEmpty(code)) return null;
            var bc = (from b in db.ProductBarcodes
                      where b.Barcode == code
                      join pu in db.ProductUnits on b.ProductUnitID equals pu.ID into puj
                      from pu in puj.DefaultIfEmpty()
                      join p in db.Products on b.ProductID equals p.ID
                      where !p.IsDeleted
                      select new { b.ProductID, Factor = (decimal?)pu.Factor, p.PNo, p.Name }).FirstOrDefault();
            if (bc != null) return (bc.ProductID, bc.Factor ?? 1m, bc.PNo + " " + bc.Name);
            var byPno = db.Products.Where(p => p.PNo == code && !p.IsDeleted).Select(p => new { p.ID, p.PNo, p.Name }).FirstOrDefault();
            return byPno == null ? ((int, decimal, string)?)null : (byPno.ID, 1m, byPno.PNo + " " + byPno.Name);
        }

        [HttpGet]
        public ActionResult StoreResolve(string code)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var r = ResolveCode(db, code) ?? throw new DstValidationException(DstLocalization.T("Store_CodeNotFound", code));
                    return new { productId = r.ProductId, factor = r.Factor, name = r.Name };
                }
            });
        }

        // ---------- inventarizatsiya ----------

        [HttpGet]
        public ActionResult StoreCounts()
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var scope = WarehouseScope(db);
                    var rows = (from c in db.InventoryCounts
                                where c.Status == DocumentStatus.Draft
                                join w in db.Warehouses on c.WarehouseID equals w.ID
                                orderby c.ID descending
                                select new
                                {
                                    c.ID, c.DocNo, c.WarehouseID, Warehouse = w.WHName, c.Kind,
                                    Lines = db.InventoryCountRows.Count(l => l.CountID == c.ID),
                                    Counted = db.InventoryCountRows.Count(l => l.CountID == c.ID && l.CountedQty != null)
                                }).ToList();
                    return rows.Where(r => scope.Allows(r.WarehouseID)).ToList();
                }
            });
        }

        private InventoryCount RequireCount(DistributionDbContext db, int id)
        {
            var count = InventoryCountService.RequireDraft(db, id);
            if (!WarehouseScope(db).Allows(count.WarehouseID)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            return count;
        }

        private static object CountLines(DistributionDbContext db, InventoryCount count)
        {
            var rows = InventoryCountService.Rows(db, count);
            var ids = rows.Select(r => r.ProductID).ToList();
            var barcodes = db.ProductBarcodes.Where(b => ids.Contains(b.ProductID)).Select(b => new { b.ProductID, b.Barcode }).ToList()
                .ToLookup(b => b.ProductID, b => b.Barcode);
            // "ko'r" sanoq: tizimdagi qoldiq Mini App'da ko'rsatilmaydi
            return rows.Select(r => new { lineId = r.LineID, productId = r.ProductID, pno = r.PNo, name = r.Name, unit = r.Unit, counted = r.Counted, barcodes = barcodes[r.ProductID].ToList() }).ToList();
        }

        [HttpGet]
        public ActionResult StoreCount(int id)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var count = RequireCount(db, id);
                    var wh = db.Warehouses.Where(w => w.ID == count.WarehouseID).Select(w => w.WHName).FirstOrDefault();
                    return new { count.ID, count.DocNo, warehouse = wh, count.Kind, lines = CountLines(db, count) };
                }
            });
        }

        /// <summary>Skaner: sanalganga qo'shadi (qty × birlik koeffitsienti); ro'yxatda yo'q tovar qo'shiladi.</summary>
        [HttpPost]
        public ActionResult StoreCountScan(int id, string code, decimal qty = 1)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    RequireCount(db, id);
                    if (qty <= 0 || qty > 100000) throw new DstValidationException(DstLocalization.T("Order_QtyPositive"));
                    var r = ResolveCode(db, code) ?? throw new DstValidationException(DstLocalization.T("Store_CodeNotFound", code));
                    InventoryCountService.AddProduct(db, id, r.ProductId);
                    var line = db.InventoryCountRows.First(l => l.CountID == id && l.ProductID == r.ProductId);
                    line.CountedQty = (line.CountedQty ?? 0) + Math.Round(qty * r.Factor, 4);
                    db.SaveChanges();
                    return new { lineId = line.ID, productId = r.ProductId, name = r.Name, counted = line.CountedQty, added = Math.Round(qty * r.Factor, 4) };
                }
            });
        }

        /// <summary>Qatorga aniq miqdor (tuzatish); null — sanalmagan.</summary>
        [HttpPost]
        public ActionResult StoreCountSet(int id, int lineId, decimal? counted)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    RequireCount(db, id);
                    var line = db.InventoryCountRows.FirstOrDefault(l => l.ID == lineId && l.CountID == id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    InventoryCountService.SaveCounts(db, id, new[] { (lineId, counted, line.UnitCost, line.Note) });
                    return new { lineId, counted };
                }
            });
        }

        // ---------- buyurtma yig'ish ----------

        [HttpGet]
        public ActionResult StoreOrders()
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var scope = WarehouseScope(db);
                    var rows = (from o in db.Orders
                                where PickStatuses.Contains(o.OrderStatus) && o.WarehouseID != null
                                join c in db.Customers on o.CustomerID equals c.ID
                                orderby o.DeliveryDate, o.ID
                                select new { o.ID, o.DocNo, o.WarehouseID, o.OrderStatus, o.DeliveryDate, o.IsPickup, o.WeightKg, Customer = c.Name }).Take(200).ToList();
                    return rows.Where(r => scope.Allows(r.WarehouseID)).Select(r => new
                    {
                        r.ID, r.DocNo, r.Customer, status = ((OrderStatus)r.OrderStatus).ToString(), delivery = r.DeliveryDate?.ToString("dd.MM.yyyy"), r.IsPickup, r.WeightKg
                    }).ToList();
                }
            });
        }

        private DstOrder RequireStoreOrder(DistributionDbContext db, int id)
        {
            var order = db.Orders.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (!PickStatuses.Contains(order.OrderStatus) || !WarehouseScope(db).Allows(order.WarehouseID))
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            return order;
        }

        [HttpGet]
        public ActionResult StoreOrder(int id)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var order = RequireStoreOrder(db, id);
                    var model = OrderDetailsModel.Load(db, order, false, DstLocalization.LanguageOf(userId) == DstLocalization.Russian);
                    var ids = model.Lines.Select(l => l.ProductID).Distinct().ToList();
                    var barcodes = db.ProductBarcodes.Where(b => ids.Contains(b.ProductID)).Select(b => new { b.ProductID, b.Barcode }).ToList()
                        .ToLookup(b => b.ProductID, b => b.Barcode);
                    return new
                    {
                        order.ID, order.DocNo, customer = model.Customer?.Name, status = ((OrderStatus)order.OrderStatus).ToString(), warehouse = model.Warehouse,
                        lines = model.Lines.Where(l => l.RemainingBase > 0).Select(l => new
                        {
                            lineId = l.ID, productId = l.ProductID, pno = l.PNo, name = l.Product, unit = l.BaseUnit, remaining = l.RemainingBase,
                            reserved = l.ReservedBase, saleUnit = l.Unit, factor = l.Factor, barcodes = barcodes[l.ProductID].ToList()
                        }).ToList()
                    };
                }
            });
        }

        [HttpPost]
        public ActionResult StorePickStart(int id)
        {
            return Run(userId =>
            {
                using (var uow = Uow())
                {
                    var order = RequireStoreOrder(uow.Dst, id);
                    if (order.OrderStatus == (byte)OrderStatus.Confirmed) OrderService.StartPicking(uow, id, userId, DateTime.Now);
                    uow.Commit();
                    return new { ok = true };
                }
            });
        }

        public sealed class StoreShipLine
        {
            public int LineId { get; set; }
            public decimal QtyBase { get; set; }
        }

        /// <summary>Yig'ilgan miqdorlar bilan jo'natish (asosiy birlikda).</summary>
        [HttpPost]
        public ActionResult StoreShip(int id, List<StoreShipLine> lines)
        {
            return Run(userId =>
            {
                using (var uow = Uow())
                {
                    RequireStoreOrder(uow.Dst, id);
                    var qty = (lines ?? new List<StoreShipLine>()).Where(l => l != null && l.QtyBase > 0)
                        .GroupBy(l => l.LineId).ToDictionary(g => g.Key, g => g.Sum(x => x.QtyBase));
                    var shipmentId = OrderService.Ship(uow, id, qty, userId, DateTime.Now);
                    uow.Commit();
                    var s = uow.Dst.Shipments.Find(shipmentId);
                    return new { shipmentId, s.DocNo, s.Total };
                }
            });
        }

        // ---------- rahbar: hisobdan chiqarish tasdig'i (bot tugmasi) ----------

        [HttpPost]
        public ActionResult WriteOffDecision(int id, bool approve, string reason)
        {
            return Run(userId =>
            {
                bool can = DstPermissions.Has(UserName, DstPermissions.WriteOffApprove);
                using (var uow = Uow())
                {
                    if (approve) WriteOffService.Approve(uow, id, userId, can, RoleHelper.IsAdminOrDeveloper(UserName), DateTime.Now);
                    else WriteOffService.Reject(uow.Dst, id, userId, can, string.IsNullOrWhiteSpace(reason) ? "Telegram" : reason, DateTime.Now);
                    uow.Commit();
                    var doc = uow.Dst.WriteOffs.Find(id);
                    return new { doc.ID, doc.DocNo, status = doc.Status.ToString(), doc.CostTotal };
                }
            });
        }
    }
}
