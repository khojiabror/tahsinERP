using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Areas.Distribution.Services.Warehouse
{
    public sealed class WriteOffLineInput
    {
        public int ProductID { get; set; }
        public decimal QtyBase { get; set; }
        public string Note { get; set; }
    }

    public sealed class WriteOffInput
    {
        public int? ID { get; set; }
        public int WarehouseID { get; set; }
        public int? ReasonID { get; set; }
        public string Note { get; set; }
        public string PhotoKey { get; set; }
        public List<WriteOffLineInput> Lines { get; set; } = new List<WriteOffLineInput>();
    }

    /// <summary>
    /// Hisobdan chiqarish rahbar tasdig'i bilan (TZ 5-bo'lim): qoralama → "tasdiqqa yuborish" (rahbarlarga Telegram xabari,
    /// botda tasdiq/rad tugmalari) → tasdiq: ombor chiqimi FIFO tannarxda (o'tkazildi) yoki rad: bekor (sabab bilan).
    /// Tasdiqlovchi — DstWriteOffApprove huquqi; o'z hujjatini faqat Admin tasdiqlaydi. Rezervdagi tovar chiqarilmaydi.
    /// </summary>
    public static class WriteOffService
    {
        public const string Prefix = "DW";
        public const string NotifyType = "WRITE_OFF";

        /// <summary>Yangi qoralama yoki uni qayta yozish (yuborilgani ham — qayta yuborish kerak bo'ladi). Chaqiruvchi Commit qiladi.</summary>
        public static int SaveDraft(DistributionUnitOfWork uow, WriteOffInput input, int userId, DateTime now)
        {
            var db = uow.Dst;
            var lines = (input.Lines ?? new List<WriteOffLineInput>()).Where(l => l != null && l.ProductID > 0 && l.QtyBase != 0).ToList();
            if (lines.Count == 0) throw new DstValidationException(DstLocalization.T("Order_NoLines"));
            if (lines.Any(l => l.QtyBase < 0)) throw new DstValidationException(DstLocalization.T("Order_QtyPositive"));
            if (lines.GroupBy(l => l.ProductID).Any(g => g.Count() > 1)) throw new DstValidationException(DstLocalization.T("Import_DuplicateInFile", ""));
            if (!db.Warehouses.Any(w => w.ID == input.WarehouseID && w.IsDeleted != true)) throw new DstValidationException(DstLocalization.T("Order_NoWarehouse"));
            if (input.ReasonID.HasValue && !db.Reasons.Any(r => r.ID == input.ReasonID && r.Type == ReasonTypes.WriteOff))
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var ids = lines.Select(l => l.ProductID).ToList();
            if (db.Products.Count(p => ids.Contains(p.ID) && !p.IsDeleted) != ids.Count) throw new DstValidationException(DstLocalization.T("Error_NotFound"));

            WriteOff doc;
            if (input.ID.HasValue)
            {
                doc = RequireDraft(db, input.ID.Value);
                db.WriteOffLines.RemoveRange(db.WriteOffLines.Where(l => l.WriteOffID == doc.ID).ToList());
                doc.SubmittedAt = null;     // o'zgargan hujjat qayta yuboriladi
                doc.SubmittedBy = null;
            }
            else
            {
                doc = new WriteOff
                {
                    DocNo = DstNumbering.Next(db, "WRITE_OFFS", Prefix, now), DocDate = now.Date, Status = DocumentStatus.Draft,
                    CreatedBy = userId, CreatedAt = now
                };
                db.WriteOffs.Add(doc);
            }
            doc.WarehouseID = input.WarehouseID;
            doc.ReasonID = input.ReasonID;
            doc.Note = Trim(input.Note, 300);
            if (input.PhotoKey != null) doc.PhotoKey = Trim(input.PhotoKey, 200);
            foreach (var l in lines)
                db.WriteOffLines.Add(new WriteOffLine { WriteOff = doc, ProductID = l.ProductID, QtyBase = l.QtyBase, Note = Trim(l.Note, 200) });
            db.SaveChanges();
            return doc.ID;
        }

        public static WriteOff RequireDraft(DistributionDbContext db, int id)
        {
            var doc = db.WriteOffs.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (doc.Status != DocumentStatus.Draft) throw new DstValidationException(DstLocalization.T("Doc_NotDraft"));
            return doc;
        }

        /// <summary>Tasdiqqa yuborish: rahbarlarga xabar (botda "tasdiqlash / rad etish").</summary>
        public static void Submit(DistributionDbContext db, int id, int userId, DateTime now)
        {
            var doc = RequireDraft(db, id);
            if (doc.SubmittedAt.HasValue) throw new DstValidationException(DstLocalization.T("WriteOff_AlreadySubmitted"));
            var lines = db.WriteOffLines.Where(l => l.WriteOffID == id).ToList();
            if (lines.Count == 0) throw new DstValidationException(DstLocalization.T("Order_NoLines"));
            doc.SubmittedAt = now;
            doc.SubmittedBy = userId;

            var warehouse = db.Warehouses.Where(w => w.ID == doc.WarehouseID).Select(w => w.WHName).FirstOrDefault();
            var reason = doc.ReasonID.HasValue ? db.Reasons.Where(r => r.ID == doc.ReasonID).Select(r => r.Name).FirstOrDefault() : null;
            var ids = lines.Select(l => l.ProductID).ToList();
            var names = db.Products.Where(p => ids.Contains(p.ID)).ToDictionary(p => p.ID, p => p.PNo + " " + p.Name);
            var author = db.Users.Where(u => u.ID == userId).Select(u => u.Uname).FirstOrDefault();
            NotificationService.ToManagers(db, NotifyType,
                l => DstLocalization.TFor(l, "Notify_WriteOff", doc.DocNo, warehouse, reason ?? "—", author) + Environment.NewLine
                     + string.Join(Environment.NewLine, lines.Take(15).Select(x => "• " + names[x.ProductID] + " — " + x.QtyBase.ToString("0.####")))
                     + (lines.Count > 15 ? Environment.NewLine + "…" : "")
                     + (string.IsNullOrEmpty(doc.Note) ? "" : Environment.NewLine + doc.Note),
                "{\"action\":\"writeoff\",\"id\":" + doc.ID + "}", now, userId);
            db.SaveChanges();
        }

        /// <summary>Tasdiq: ombor chiqimi (FIFO tannarx), hujjat o'tkazildi. Chaqiruvchi Commit qiladi.</summary>
        public static void Approve(DistributionUnitOfWork uow, int id, int approverId, bool canApprove, bool isAdmin, DateTime now)
        {
            var db = uow.Dst;
            var doc = RequireDraft(db, id);
            if (!canApprove) throw new DstValidationException(DstLocalization.T("WriteOff_NoRight"));
            if (!doc.SubmittedAt.HasValue) throw new DstValidationException(DstLocalization.T("WriteOff_NotSubmitted"));
            if (doc.CreatedBy == approverId && !isAdmin) throw new DstValidationException(DstLocalization.T("WriteOff_SelfApprove"));

            var lines = db.WriteOffLines.Where(l => l.WriteOffID == id).OrderBy(l => l.ID).ToList();
            var before = StockDocs.LockAndCheckAvailable(db, doc.WarehouseID, lines.Select(l => (l.ProductID, l.QtyBase)));
            var reason = doc.ReasonID.HasValue ? db.Reasons.Where(r => r.ID == doc.ReasonID).Select(r => r.Name).FirstOrDefault() : null;
            var issue = lines.Select(l => new StockDocs.IssueLine { ProductID = l.ProductID, QtyBase = l.QtyBase, Comment = doc.DocNo + (reason == null ? "" : " " + reason) }).ToList();
            doc.ExpenseID = StockDocs.Issue(uow, doc.WarehouseID, doc.DocNo,
                "Hisobdan chiqarish " + doc.DocNo + (reason == null ? "" : " (" + reason + ")") + " URL: /Distribution/DstWriteOffs/Details/" + doc.ID, issue, now);
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i].UnitCost = issue[i].UnitCost;
                lines[i].CostAmount = issue[i].Cost;
            }
            doc.CostTotal = issue.Sum(x => x.Cost);
            DocumentLifecycle.Post(doc, approverId, now);
            if (doc.CreatedBy != approverId)
                NotificationService.ToUser(db, doc.CreatedBy, NotifyType, l => DstLocalization.TFor(l, "Notify_WriteOffApproved", doc.DocNo), null, now);
            StockAlerts.AfterDecrease(db, doc.WarehouseID, before, now, approverId);
            db.SaveChanges();
        }

        /// <summary>Rad etish (tasdiqlovchi) yoki qoralamani bekor qilish (muallif): hujjat bekor, ombor o'zgarmaydi.</summary>
        public static void Reject(DistributionDbContext db, int id, int userId, bool canApprove, string reason, DateTime now)
        {
            var doc = RequireDraft(db, id);
            if (doc.CreatedBy != userId && !canApprove) throw new DstValidationException(DstLocalization.T("WriteOff_NoRight"));
            DocumentLifecycle.Cancel(doc, userId, reason, now);
            if (doc.CreatedBy != userId)
                NotificationService.ToUser(db, doc.CreatedBy, NotifyType, l => DstLocalization.TFor(l, "Notify_WriteOffRejected", doc.DocNo, doc.CancelReason), null, now);
            db.SaveChanges();
        }

        /// <summary>O'tkazilgan hisobdan chiqarishni bekor qilish (xato): tovar omborga qaytadi.</summary>
        /// <summary>
        /// Foto (dalil) — yuborilgunga qadar qo'shiladi/almashtiriladi: tasdiqlovchi ko'rgan holatdan keyin o'zgarmasin (05_FINDINGS K3-32).
        /// </summary>
        public static void SetPhoto(DistributionDbContext db, int id, string photoKey)
        {
            var doc = db.WriteOffs.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (doc.Status != DocumentStatus.Draft || doc.SubmittedAt.HasValue)
                throw new DstValidationException(DstLocalization.T("WriteOff_AlreadySubmitted"));
            doc.PhotoKey = Trim(photoKey, 200);
            db.SaveChanges();
        }

        public static void CancelPosted(DistributionUnitOfWork uow, int id, int userId, bool canApprove, string reason, DateTime now)
        {
            var db = uow.Dst;
            var doc = db.WriteOffs.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (doc.Status != DocumentStatus.Posted) throw new DstValidationException(DstLocalization.T("Doc_NotPosted"));
            if (!canApprove) throw new DstValidationException(DstLocalization.T("WriteOff_NoRight"));
            DocumentLifecycle.Cancel(doc, userId, reason, now);
            if (doc.ExpenseID.HasValue) StockDocs.ReverseIssue(uow, doc.ExpenseID.Value, doc.WarehouseID);
            db.SaveChanges();
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
