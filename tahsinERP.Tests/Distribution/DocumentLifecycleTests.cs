using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;

namespace tahsinERP.Tests.Distribution
{
    [TestClass]
    public class DocumentLifecycleTests
    {
        private sealed class TestDoc : DstDocument
        {
        }

        private static readonly DateTime Now = new DateTime(2026, 10, 19, 10, 30, 0);

        [TestMethod]
        public void Post_Draft_SetsPostedFields()
        {
            var doc = new TestDoc { DocNo = "T-1" };
            DocumentLifecycle.Post(doc, 7, Now);

            Assert.AreEqual(DocumentStatus.Posted, doc.Status);
            Assert.AreEqual(7, doc.PostedBy);
            Assert.AreEqual(Now, doc.PostedAt);
        }

        [TestMethod]
        public void Post_NotDraft_Throws()
        {
            var doc = new TestDoc { DocNo = "T-1", Status = DocumentStatus.Posted };
            Assert.ThrowsException<DstValidationException>(() => DocumentLifecycle.Post(doc, 1, Now));

            doc.Status = DocumentStatus.Cancelled;
            Assert.ThrowsException<DstValidationException>(() => DocumentLifecycle.Post(doc, 1, Now));
        }

        [TestMethod]
        public void Cancel_RequiresReason()
        {
            var doc = new TestDoc { DocNo = "T-1", Status = DocumentStatus.Posted };
            Assert.ThrowsException<DstValidationException>(() => DocumentLifecycle.Cancel(doc, 1, null, Now));
            Assert.ThrowsException<DstValidationException>(() => DocumentLifecycle.Cancel(doc, 1, "   ", Now));
            Assert.ThrowsException<DstValidationException>(() => DocumentLifecycle.Cancel(doc, 1, "ab", Now));
            Assert.AreEqual(DocumentStatus.Posted, doc.Status, "xato bo'lsa holat o'zgarmaydi");
        }

        [TestMethod]
        public void Cancel_SetsCancelFields_AndTrimsReason()
        {
            var doc = new TestDoc { DocNo = "T-1", Status = DocumentStatus.Posted };
            DocumentLifecycle.Cancel(doc, 3, "  mijoz rad etdi  ", Now);

            Assert.AreEqual(DocumentStatus.Cancelled, doc.Status);
            Assert.AreEqual(3, doc.CancelledBy);
            Assert.AreEqual(Now, doc.CancelledAt);
            Assert.AreEqual("mijoz rad etdi", doc.CancelReason);
        }

        [TestMethod]
        public void Cancel_Twice_Throws()
        {
            var doc = new TestDoc { DocNo = "T-1", Status = DocumentStatus.Cancelled };
            Assert.ThrowsException<DstValidationException>(() => DocumentLifecycle.Cancel(doc, 1, "takror", Now));
        }

        [TestMethod]
        public void Cancel_LongReason_Truncated()
        {
            var doc = new TestDoc { DocNo = "T-1" };
            DocumentLifecycle.Cancel(doc, 1, new string('x', 700), Now);
            Assert.AreEqual(500, doc.CancelReason.Length);
        }

        [TestMethod]
        public void CanDelete_OnlyDraft()
        {
            Assert.IsTrue(DocumentLifecycle.CanDelete(new TestDoc { Status = DocumentStatus.Draft }));
            Assert.IsFalse(DocumentLifecycle.CanDelete(new TestDoc { Status = DocumentStatus.Posted }));
            Assert.IsFalse(DocumentLifecycle.CanDelete(new TestDoc { Status = DocumentStatus.Cancelled }));
            Assert.IsFalse(DocumentLifecycle.CanDelete(null));
        }
    }
}
