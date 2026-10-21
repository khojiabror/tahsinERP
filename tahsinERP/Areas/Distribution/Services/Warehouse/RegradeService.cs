using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Areas.Distribution.Services.Warehouse
{
    public sealed class RegradeLineInput
    {
        public int FromProductID { get; set; }
        public int ToProductID { get; set; }
        public decimal FromQtyBase { get; set; }
        public decimal ToQtyBase { get; set; }
    }

    public sealed class RegradeInput
    {
        public int? ID { get; set; }
        public int WarehouseID { get; set; }
        public int? ReasonID { get; set; }
        public string Note { get; set; }
        public List<RegradeLineInput> Lines { get; set; } = new List<RegradeLineInput>();
    }

    /// <summary>
    /// Pereso'rtitsa (TZ 5-bo'lim): bir omborda A tovar chiqim (FIFO tannarx), B tovar kirim — o'sha tannarx bilan
    /// (B ning birlik tannarxi = A chiqim summasi / B miqdori). Ombor qiymati o'zgarmaydi, faqat tovar almashadi.
    /// </summary>
    public static class RegradeService
    {
        public const string Prefix = "DR";

        public static int SaveDraft(DistributionUnitOfWork uow, RegradeInput input, int userId, DateTime now)
        {
            var db = uow.Dst;
            var lines = (input.Lines ?? new List<RegradeLineInput>()).Where(l => l != null && l.FromProductID > 0 && l.ToProductID > 0).ToList();
            if (lines.Count == 0) throw new DstValidationException(DstLocalization.T("Order_NoLines"));
            if (lines.Any(l => l.FromQtyBase <= 0 || l.ToQtyBase <= 0)) throw new DstValidationException(DstLocalization.T("Order_QtyPositive"));
            if (lines.Any(l => l.FromProductID == l.ToProductID)) throw new DstValidationException(DstLocalization.T("Regrade_SameProduct"));
            if (!db.Warehouses.Any(w => w.ID == input.WarehouseID && w.IsDeleted != true)) throw new DstValidationException(DstLocalization.T("Order_NoWarehouse"));
            var ids = lines.SelectMany(l => new[] { l.FromProductID, l.ToProductID }).Distinct().ToList();
            if (db.Products.Count(p => ids.Contains(p.ID) && !p.IsDeleted) != ids.Count) throw new DstValidationException(DstLocalization.T("Error_NotFound"));

            Regrade doc;
            if (input.ID.HasValue)
            {
                doc = RequireDraft(db, input.ID.Value);
                db.RegradeLines.RemoveRange(db.RegradeLines.Where(l => l.RegradeID == doc.ID).ToList());
            }
            else
            {
                doc = new Regrade
                {
                    DocNo = DstNumbering.Next(db, "REGRADES", Prefix, now), DocDate = now.Date, Status = DocumentStatus.Draft,
                    CreatedBy = userId, CreatedAt = now
                };
                db.Regrades.Add(doc);
            }
            doc.WarehouseID = input.WarehouseID;
            doc.ReasonID = input.ReasonID;
            doc.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim().Length > 300 ? input.Note.Trim().Substring(0, 300) : input.Note.Trim();
            foreach (var l in lines)
                db.RegradeLines.Add(new RegradeLine { Regrade = doc, FromProductID = l.FromProductID, ToProductID = l.ToProductID, FromQtyBase = l.FromQtyBase, ToQtyBase = l.ToQtyBase });
            db.SaveChanges();
            return doc.ID;
        }

        public static Regrade RequireDraft(DistributionDbContext db, int id)
        {
            var doc = db.Regrades.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (doc.Status != DocumentStatus.Draft) throw new DstValidationException(DstLocalization.T("Doc_NotDraft"));
            return doc;
        }

        /// <summary>O'tkazish: bitta chiqim (A tovarlar) + bitta kirim (B tovarlar). DLOC omborda kirim joyi — <paramref name="locationId"/>.</summary>
        public static void Post(DistributionUnitOfWork uow, int id, int? locationId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var doc = RequireDraft(db, id);
            var lines = db.RegradeLines.Where(l => l.RegradeID == id).OrderBy(l => l.ID).ToList();
            if (lines.Count == 0) throw new DstValidationException(DstLocalization.T("Order_NoLines"));

            var before = StockDocs.LockAndCheckAvailable(db, doc.WarehouseID, lines.Select(l => (l.FromProductID, l.FromQtyBase)));
            foreach (var to in lines.Select(l => l.ToProductID).Distinct().Where(p => !before.ContainsKey(p)).OrderBy(x => x))
                StockDocs.Lock(db, doc.WarehouseID, to);

            var issue = lines.Select(l => new StockDocs.IssueLine { ProductID = l.FromProductID, QtyBase = l.FromQtyBase, Comment = doc.DocNo }).ToList();
            string url = " URL: /Distribution/DstRegrades/Details/" + doc.ID;
            doc.ExpenseID = StockDocs.Issue(uow, doc.WarehouseID, doc.DocNo, "Pereso'rtitsa (chiqim) " + doc.DocNo + url, issue, now);

            var receipt = new List<StockDocs.ReceiptLine>();
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i].CostAmount = issue[i].Cost;
                receipt.Add(new StockDocs.ReceiptLine
                {
                    ProductID = lines[i].ToProductID, QtyBase = lines[i].ToQtyBase,
                    UnitCost = Math.Round(issue[i].Cost / lines[i].ToQtyBase, 6), Comment = doc.DocNo
                });
            }
            doc.IncomeID = StockDocs.Receive(uow, doc.WarehouseID, doc.DocNo + "-IN", "Pereso'rtitsa (kirim) " + doc.DocNo + url, receipt, locationId, now);
            doc.CostTotal = issue.Sum(x => x.Cost);
            DocumentLifecycle.Post(doc, userId, now);
            StockAlerts.AfterDecrease(db, doc.WarehouseID, before, now, userId);
            db.SaveChanges();
        }

        /// <summary>Bekor qilish: qoralama — shunchaki; o'tkazilgan — kirim (B tovar hali chiqarilmagan bo'lsa) va chiqim qaytariladi.</summary>
        public static void Cancel(DistributionUnitOfWork uow, int id, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var doc = db.Regrades.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            bool posted = doc.Status == DocumentStatus.Posted;
            DocumentLifecycle.Cancel(doc, userId, reason, now);
            if (posted)
            {
                var lines = db.RegradeLines.Where(l => l.RegradeID == id).ToList();
                foreach (var pid in lines.SelectMany(l => new[] { l.FromProductID, l.ToProductID }).Distinct().OrderBy(x => x))
                    StockDocs.Lock(db, doc.WarehouseID, pid);
                // B tovar rezervga olingan bo'lsa ham kirimni qaytarib bo'lmaydi (mavjud qoldiq yetishi kerak)
                StockDocs.LockAndCheckAvailable(db, doc.WarehouseID, lines.Select(l => (l.ToProductID, l.ToQtyBase)));
                if (doc.IncomeID.HasValue) StockDocs.ReverseReceive(uow, doc.IncomeID.Value, doc.WarehouseID);
                if (doc.ExpenseID.HasValue) StockDocs.ReverseIssue(uow, doc.ExpenseID.Value, doc.WarehouseID);
            }
            db.SaveChanges();
        }
    }
}
