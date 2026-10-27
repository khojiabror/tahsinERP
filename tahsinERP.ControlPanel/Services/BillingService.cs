using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.I18n;

namespace tahsinERP.ControlPanel.Services;

public record InvoiceLineInput(string Description, decimal Quantity, long UnitPriceTiyin, decimal VatRatePercent);

public record PaymentResult(Payment Payment, Invoice Invoice, License? IssuedLicense, string? LicenseWarning);

/// <summary>
/// Hisob-fakturalar va qo'lda belgilanadigan to'lovlar (00_PLAN.md, 5-bo'lim). Onlayn to'lov integratsiyasi YO'Q.
/// Qoidalar: to'lov o'chirilmaydi — faqat storno (sabab bilan); storno obunani qisqartirmaydi; har amal AuditLog'da.
/// </summary>
public sealed class BillingService(ControlDbContext db, AuditLogger audit, Clock clock, SubscriptionService subscriptions,
    LicenseIssuer licenses, IOptions<PanelOptions> options, ILogger<BillingService> log)
{
    public const string NumberPrefix = "TE";

    public static string FormatNumber(int year, int value) => $"{NumberPrefix}-{year}-{value:D6}";

    private static object Snapshot(Invoice i) => new
    {
        i.Number, i.TenantId, i.SubscriptionId, i.PeriodCount, i.IssueDate, i.DueDate, i.Status, i.TotalTiyin, i.VatTiyin, i.PaidTiyin,
        Lines = i.Lines.OrderBy(l => l.LineNo).Select(l => new { l.Description, l.Quantity, l.UnitPriceTiyin, l.VatRatePercent, l.AmountTiyin, l.VatTiyin })
    };

    /// <summary>Obuna uchun standart qator: paket narxi × davrlar soni. Narx belgilanmagan bo'lsa — xato.</summary>
    public static InvoiceLineInput DefaultLine(Subscription sub, Plan plan, int periodCount)
    {
        long? price = sub.Period == BillingPeriod.Year ? plan.PriceYearTiyin : plan.PriceMonthTiyin;
        if (price is null)
            throw new DomainException(L.T(plan.IsAddOn
                ? (sub.Period == BillingPeriod.Year ? "Pnl_Bill_NoPriceAddOnYear" : "Pnl_Bill_NoPriceAddOnMonth")
                : (sub.Period == BillingPeriod.Year ? "Pnl_Bill_NoPricePlanYear" : "Pnl_Bill_NoPricePlanMonth"), plan.Name));
        string unit = sub.Period == BillingPeriod.Year ? "yil" : "oy";
        string what = plan.IsAddOn ? $"tahsinERP «{plan.Name}» qo'shimcha xizmati" : $"tahsinERP «{plan.Name}» paketi, {sub.Users} foydalanuvchi";
        return new InvoiceLineInput($"{what} — {periodCount} {unit}", periodCount, price.Value, plan.VatRatePercent);
    }

    public async Task<Invoice> CreateDraftAsync(int tenantId, int? subscriptionId, int periodCount, IReadOnlyList<InvoiceLineInput> lines,
        string? note, int staffId, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct) ?? throw new DomainException(L.T("Pnl_Err_TenantNotFound"));
        if (tenant.Status == TenantStatus.Archived) throw new DomainException(L.T("Pnl_Bill_ArchivedTenant"));
        if (subscriptionId != null)
        {
            var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId && s.TenantId == tenantId, ct)
                ?? throw new DomainException(L.T("Pnl_Bill_SubNotOfTenant"));
            if (sub.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired || sub.Period == BillingPeriod.Trial)
                throw new DomainException(L.T("Pnl_Bill_SubNotBillable"));
            if (periodCount < 1 || periodCount > (sub.Period == BillingPeriod.Year ? 3 : 36))
                throw new DomainException(L.T("Pnl_Bill_PeriodCountInvalid"));
        }
        DateOnly today = clock.Today;
        var invoice = new Invoice
        {
            TenantId = tenantId, SubscriptionId = subscriptionId, PeriodCount = subscriptionId is null ? 0 : periodCount,
            IssueDate = today, DueDate = today.AddDays(options.Value.InvoiceDueDays), Status = InvoiceStatus.Draft,
            Note = note, CreatedByStaffId = staffId, CreatedAtUtc = clock.UtcNow,
        };
        SetLines(invoice, lines);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        audit.Add("invoice.create", "Invoice", invoice.Id, after: Snapshot(invoice));
        await db.SaveChangesAsync(ct);
        return invoice;
    }

    public static void SetLines(Invoice invoice, IReadOnlyList<InvoiceLineInput> lines)
    {
        if (lines.Count == 0) throw new DomainException(L.T("Pnl_Bill_AtLeastOneLine"));
        invoice.Lines.Clear();
        int no = 1;
        foreach (var l in lines)
        {
            if (string.IsNullOrWhiteSpace(l.Description)) throw new DomainException(L.T("Pnl_Bill_RowNameEmpty", no));
            if (l.Quantity <= 0) throw new DomainException(L.T("Pnl_Bill_RowQtyPositive", no));
            if (l.UnitPriceTiyin < 0) throw new DomainException(L.T("Pnl_Bill_RowPriceNegative", no));
            if (l.VatRatePercent < 0 || l.VatRatePercent > 100) throw new DomainException(L.T("Pnl_Bill_RowVatInvalid", no));
            long amount = Money.LineAmount(l.Quantity, l.UnitPriceTiyin);
            invoice.Lines.Add(new InvoiceLine
            {
                LineNo = no++, Description = l.Description.Trim(), Quantity = l.Quantity, UnitPriceTiyin = l.UnitPriceTiyin,
                VatRatePercent = l.VatRatePercent, AmountTiyin = amount, VatTiyin = Money.VatIncluded(amount, l.VatRatePercent),
            });
        }
        invoice.TotalTiyin = invoice.Lines.Sum(x => x.AmountTiyin);
        invoice.VatTiyin = invoice.Lines.Sum(x => x.VatTiyin);
    }

    public async Task UpdateDraftAsync(Invoice invoice, IReadOnlyList<InvoiceLineInput> lines, DateOnly dueDate, string? note, CancellationToken ct = default)
    {
        if (invoice.Status != InvoiceStatus.Draft) throw new DomainException(L.T("Pnl_Bill_OnlyDraftEditable"));
        var before = Snapshot(invoice);
        db.InvoiceLines.RemoveRange(invoice.Lines);
        SetLines(invoice, lines);
        invoice.DueDate = dueDate;
        invoice.Note = note;
        audit.Add("invoice.update", "Invoice", invoice.Id, before, Snapshot(invoice));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Raqam beradi (TE-YYYY-000001, yil bo'yicha uzluksiz) va Issued holatiga o'tkazadi.</summary>
    public async Task IssueAsync(Invoice invoice, CancellationToken ct = default)
    {
        if (invoice.Status != InvoiceStatus.Draft) throw new DomainException(L.T("Pnl_Bill_AlreadyIssued"));
        if (invoice.TotalTiyin <= 0) throw new DomainException(L.T("Pnl_Bill_ZeroTotal"));
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        DateOnly today = clock.Today;
        int value = await NextNumberAsync(today.Year, ct);
        var before = Snapshot(invoice);
        invoice.Number = FormatNumber(today.Year, value);
        invoice.IssueDate = today;
        if (invoice.DueDate < today) invoice.DueDate = today.AddDays(options.Value.InvoiceDueDays);
        invoice.Status = InvoiceStatus.Issued;
        audit.Add("invoice.issue", "Invoice", invoice.Id, before, Snapshot(invoice));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<int> NextNumberAsync(int year, CancellationToken ct)
    {
        // UPDLOCK/HOLDLOCK: parallel chiqarishda bir xil raqam berilmaydi (tranzaksiya oxirigacha qulf).
        var updated = await db.Database.SqlQuery<int>(
            $"UPDATE InvoiceSequences WITH (UPDLOCK, HOLDLOCK) SET LastValue = LastValue + 1 OUTPUT inserted.LastValue AS [Value] WHERE [Year] = {year}")
            .ToListAsync(ct);
        if (updated.Count == 1)
            return updated[0];
        await db.Database.ExecuteSqlAsync($"INSERT INTO InvoiceSequences ([Year], LastValue) VALUES ({year}, 1)", ct);
        return 1;
    }

    public async Task CancelAsync(Invoice invoice, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException(L.T("Pnl_Bill_CancelReasonRequired"));
        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Paid) throw new DomainException(L.T("Pnl_Bill_CannotCancel"));
        if (invoice.PaidTiyin > 0) throw new DomainException(L.T("Pnl_Bill_HasPayments"));
        var before = Snapshot(invoice);
        invoice.Status = InvoiceStatus.Cancelled;
        audit.Add("invoice.cancel", "Invoice", invoice.Id, before, new { invoice.Status, Reason = reason.Trim() });
        await db.SaveChangesAsync(ct);
    }

    private async Task<Invoice> LoadAsync(int invoiceId, CancellationToken ct) =>
        await db.Invoices.Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct) ?? throw new DomainException(L.T("Pnl_Err_InvoiceNotFound"));

    private static void Recalculate(Invoice invoice)
    {
        invoice.PaidTiyin = invoice.Payments.Where(p => !p.IsReversed).Sum(p => p.AmountTiyin);
        if (invoice.PaidTiyin >= invoice.TotalTiyin)
            invoice.Status = InvoiceStatus.Paid;
        else
        {
            invoice.Status = invoice.PaidTiyin > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Issued;
            invoice.PaidAtUtc = null;
        }
    }

    /// <summary>
    /// "To'lov qabul qilindi". Summa qoldiqdan oshmasin. To'liq yopilsa: Paid → obuna uzaytiriladi → tenant Active
    /// (bitta tranzaksiyada) → keyin yangi kalit chiqariladi. Kalit chiqmasa ham to'lov saqlanib qoladi (ogohlantirish qaytadi).
    /// </summary>
    public async Task<PaymentResult> RecordPaymentAsync(int invoiceId, long amountTiyin, DateOnly paidAt, PaymentMethod method,
        string? reference, string? note, int staffId, CancellationToken ct = default)
    {
        Invoice invoice;
        Payment payment;
        bool becamePaid;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            invoice = await LoadAsync(invoiceId, ct);
            if (invoice.Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid))
                throw new DomainException(invoice.Status == InvoiceStatus.Paid ? L.T("Pnl_Bill_FullyPaid") : L.T("Pnl_Bill_PayOnlyIssued"));
            if (amountTiyin <= 0) throw new DomainException(L.T("Pnl_Bill_AmountPositive"));
            if (amountTiyin > invoice.RemainingTiyin)
                throw new DomainException(L.T("Pnl_Bill_AmountExceeds", Money.FormatSom(invoice.RemainingTiyin)));
            if (paidAt > clock.Today) throw new DomainException(L.T("Pnl_Bill_PaidAtFuture"));
            if (paidAt < invoice.IssueDate.AddDays(-60)) throw new DomainException(L.T("Pnl_Bill_PaidAtTooEarly"));

            var before = Snapshot(invoice);
            payment = new Payment
            {
                InvoiceId = invoice.Id, Method = method, AmountTiyin = amountTiyin, PaidAt = paidAt,
                Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(), Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                RecordedByStaffId = staffId, RecordedAtUtc = clock.UtcNow,
            };
            invoice.Payments.Add(payment);
            Recalculate(invoice);
            becamePaid = invoice.Status == InvoiceStatus.Paid;
            if (becamePaid)
            {
                invoice.PaidAtUtc = clock.UtcNow;
                await subscriptions.ApplyPaidInvoiceAsync(invoice, ct);
            }
            await db.SaveChangesAsync(ct);
            audit.Add("payment.record", "Payment", payment.Id, before: new { Invoice = before },
                after: new { payment.InvoiceId, invoice.Number, payment.AmountTiyin, payment.PaidAt, payment.Method, payment.Reference, payment.Note, InvoiceStatus = invoice.Status, invoice.PaidTiyin },
                staffIdOverride: staffId);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        License? issued = null;
        string? warning = null;
        if (becamePaid && invoice.SubscriptionId != null)
        {
            try
            {
                var tenant = invoice.Tenant!;
                issued = await licenses.EnsureForTenantAsync(tenant, ct);
                if (issued is null && tenant.InstallationId is null)
                    warning = L.T("Pnl_Bill_NotBoundWarning");
            }
            catch (Exception ex) when (ex is DomainException or System.Security.Cryptography.CryptographicException or InvalidOperationException or NotSupportedException)
            {
                log.LogError(ex, "To'lovdan keyin kalit chiqmadi (invoice {Invoice})", invoice.Number);
                warning = L.T("Pnl_Bill_KeyFailedWarning", ex.Message);
            }
        }
        return new PaymentResult(payment, invoice, issued, warning);
    }

    /// <summary>Storno: o'chirmaydi, IsReversed + sabab. Obuna avtomatik qisqartirilmaydi — xodim qaror qiladi (kalitni bekor qilish alohida).</summary>
    public async Task<Invoice> ReversePaymentAsync(int paymentId, string reason, int staffId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException(L.T("Pnl_Bill_ReverseReasonRequired"));
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct) ?? throw new DomainException(L.T("Pnl_Bill_PaymentNotFound"));
        if (payment.IsReversed) throw new DomainException(L.T("Pnl_Bill_AlreadyReversed"));
        var invoice = await LoadAsync(payment.InvoiceId, ct);
        var before = new { invoice.Status, invoice.PaidTiyin };
        payment.IsReversed = true;
        payment.ReversedByStaffId = staffId;
        payment.ReversedAtUtc = clock.UtcNow;
        payment.ReverseReason = reason.Trim();
        Recalculate(invoice);
        audit.Add("payment.reverse", "Payment", payment.Id, before,
            new { payment.AmountTiyin, invoice.Number, Reason = payment.ReverseReason, invoice.Status, invoice.PaidTiyin, SubscriptionTouched = false },
            staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        return invoice;
    }

    /// <summary>
    /// Xato kiritilgan to'lovni butunlay o'chirish (faqat Owner; storno qilingani ham). Auditga to'liq nusxasi yoziladi, bank ko'chirmasi
    /// qatori qayta "kutilmoqda" bo'ladi. Obuna avtomatik qisqartirilmaydi — storno kabi, kerak bo'lsa obunani alohida bekor qiling.
    /// </summary>
    public async Task<Invoice> DeletePaymentAsync(int paymentId, string reason, int staffId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException(L.T("Pnl_Bill_DeleteReasonRequired"));
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct) ?? throw new DomainException(L.T("Pnl_Bill_PaymentNotFound"));
        var invoice = await LoadAsync(payment.InvoiceId, ct);
        var before = new
        {
            payment.InvoiceId, invoice.Number, payment.AmountTiyin, payment.PaidAt, payment.Method, payment.Reference, payment.Note,
            payment.RecordedByStaffId, payment.RecordedAtUtc, payment.IsReversed, payment.ReverseReason, invoice.Status, invoice.PaidTiyin,
        };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var line in await db.BankImportLines.Where(l => l.PaymentId == payment.Id).ToListAsync(ct))
        {
            line.PaymentId = null;
            line.AppliedKey = null;
            line.State = BankLineState.Pending;
        }
        invoice.Payments.Remove(payment);
        db.Payments.Remove(payment);
        Recalculate(invoice);
        audit.Add("payment.delete", "Payment", paymentId, before,
            new { Reason = reason.Trim(), invoice.Status, invoice.PaidTiyin, SubscriptionTouched = false }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return invoice;
    }

    /// <summary>
    /// Xato yaratilgan hisob-fakturani butunlay o'chirish (faqat Owner): to'lovlari avval o'chirilgan bo'lishi, Didox'ga yuborilmagan bo'lishi
    /// kerak. Qatorlari bilan o'chadi, portal so'rovi va bank qatoridagi havolalar bo'shatiladi; raqam qayta ishlatilmaydi.
    /// </summary>
    public async Task DeleteInvoiceAsync(int invoiceId, string reason, int staffId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException(L.T("Pnl_Bill_DeleteReasonRequired"));
        var invoice = await LoadAsync(invoiceId, ct);
        if (invoice.Payments.Count > 0) throw new DomainException(L.T("Pnl_Bill_DeletePaymentsFirst"));
        if (invoice.DidoxId != null) throw new DomainException(L.T("Pnl_Bill_DeleteDidox"));
        var before = Snapshot(invoice);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var r in await db.PortalRequests.Where(r => r.InvoiceId == invoiceId).ToListAsync(ct)) r.InvoiceId = null;
        foreach (var l in await db.BankImportLines.Where(l => l.InvoiceId == invoiceId).ToListAsync(ct)) l.InvoiceId = null;
        db.RemoveRange(invoice.Lines);
        db.Invoices.Remove(invoice);
        audit.Add("invoice.delete", "Invoice", invoiceId, before, new { Reason = reason.Trim() }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public static bool IsConcurrencyConflict(Exception ex) =>
        ex is DbUpdateConcurrencyException || ex.InnerException is SqlException { Number: 1205 or 2601 or 2627 };
}
