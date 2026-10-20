using System;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Bot;

namespace tahsinERP.Areas.Distribution.Services
{
    public sealed class IssueInput
    {
        public string Place { get; set; }
        public string DocRef { get; set; }
        public string Description { get; set; }
        public string Expected { get; set; }
        public byte? Severity { get; set; }
        public string PhotoKey { get; set; }
    }

    /// <summary>
    /// Pilot xatolar jurnali (03_PILOT.md, 7-bo'lim): har kim web'dan yoki botdan yozadi, raqami P-001 …;
    /// rahbarlarga botda xabar boradi. Daraja va holatni mas'ul (DstIssues o'zgartirish huquqi) qo'yadi.
    /// </summary>
    public static class IssueService
    {
        public const int DescriptionMax = 2000;

        public static int Create(DistributionDbContext db, IssueInput input, int? userId, string source, DateTime now)
        {
            var text = input.Description?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length < 5) throw new DstValidationException(DstLocalization.T("Issue_DescriptionRequired"));
            if (input.Severity.HasValue && (input.Severity < 1 || input.Severity > 3)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Xato raqami faqat tranzaksiya ichida olinadi.");

            db.Database.ExecuteSqlCommand(System.Data.Entity.TransactionalBehavior.DoNotEnsureTransaction,
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = 'dst.issues', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
                "IF @r < 0 THROW 51001, 'issue lock timeout', 1;");
            int next = (db.Database.SqlQuery<int?>("SELECT MAX(TRY_CAST(SUBSTRING(IssueNo, 3, 18) AS int)) FROM dst.ISSUES").FirstOrDefault() ?? 0) + 1;

            var issue = new Issue
            {
                IssueNo = "P-" + next.ToString("000"), CreatedAt = now, CreatedBy = userId, Source = source ?? AuditActor.Web,
                Place = Cut(input.Place, 150), DocRef = Cut(input.DocRef, 100), Description = Cut(text, DescriptionMax),
                Expected = Cut(input.Expected, 1000), Severity = input.Severity, Status = IssueStatus.New, PhotoKey = Cut(input.PhotoKey, 200)
            };
            db.Issues.Add(issue);
            db.SaveChanges();

            var author = userId.HasValue ? db.Users.Where(u => u.ID == userId).Select(u => u.FullName ?? u.Uname).FirstOrDefault() : null;
            NotificationService.ToManagers(db, NotificationService.IssueNew,
                l => DstLocalization.TFor(l, "Notify_IssueNew", issue.IssueNo, author ?? "—", Cut(text, 300)), null, now, userId);
            db.SaveChanges();
            return issue.ID;
        }

        public static void Update(DistributionDbContext db, int id, IssueStatus status, byte? severity, string resolution, int userId, DateTime now)
        {
            var issue = db.Issues.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (severity.HasValue && (severity < 1 || severity > 3)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (status == IssueStatus.Closed && string.IsNullOrWhiteSpace(resolution))
                throw new DstValidationException(DstLocalization.T("Issue_ResolutionRequired"));
            issue.Severity = severity;
            issue.Resolution = Cut(resolution, 1000);
            if (status == IssueStatus.Closed && issue.Status != IssueStatus.Closed)
            {
                issue.ClosedBy = userId;
                issue.ClosedAt = now;
            }
            else if (status != IssueStatus.Closed)
            {
                issue.ClosedBy = null;
                issue.ClosedAt = null;
            }
            issue.Status = status;
            issue.UpdatedAt = now;
            db.SaveChanges();
        }

        private static string Cut(string s, int max)
        {
            s = s?.Trim();
            if (string.IsNullOrEmpty(s)) return null;
            return s.Length > max ? s.Substring(0, max) : s;
        }
    }
}
