using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;

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
            throw new DomainException($"'{plan.Name}' paketining {(sub.Period == BillingPeriod.Year ? "yillik" : "oylik")} narxi hali belgilanmagan (Tariflar sahifasi).");
        string unit = sub.Period == BillingPeriod.Year ? "yil" : "oy";
        return new InvoiceLineInput($"tahsinERP «{plan.Name}» paketi, {sub.Users} foydalanuvchi — {periodCount} {unit}",
            periodCount, price.Value, plan.VatRatePercent);
    }

    public async Task<Invoice> CreateDraftAsync(int tenantId, int? subscriptionId, int periodCount, IReadOnlyList<InvoiceLineInput> lines,
        string? note, int staffId, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct) ?? throw new DomainException("Tenant topilmadi.");
        if (tenant.Status == TenantStatus.Archived) throw new DomainException("Arxivdagi tenantga hisob-faktura chiqarilmaydi.");
        if (subscriptionId != null)
        {
            var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId && s.TenantId == tenantId, ct)
                ?? throw new DomainException("Obuna shu tenantga tegishli emas.");
            if (sub.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired || sub.Period == BillingPeriod.Trial)
                throw new DomainException("Bu obunaga hisob-faktura chiqarib bo'lmaydi (bekor/muddati o'tgan/sinov).");
            if (periodCount < 1 || periodCount > (sub.Period == BillingPeriod.Year ? 3 : 36))
                throw new DomainException("Davrlar soni noto'g'ri.");
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
        if (lines.Count == 0) throw new DomainException("Kamida bitta qator bo'lsin.");
        invoice.Lines.Clear();
        int no = 1;
        foreach (var l in lines)
        {
            if (string.IsNullOrWhiteSpace(l.Description)) throw new DomainException($"{no}-qator: nomi bo'sh.");
            if (l.Quantity <= 0) throw new DomainException($"{no}-qator: miqdor musbat bo'lsin.");
            if (l.UnitPriceTiyin < 0) throw new DomainException($"{no}-qator: narx manfiy.");
            if (l.VatRatePercent < 0 || l.VatRatePercent > 100) throw new DomainException($"{no}-qator: QQS stavkasi noto'g'ri.");
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
        if (invoice.Status != InvoiceStatus.Draft) throw new DomainException("Faqat qoralama (Draft) tahrirlanadi.");
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
        if (invoice.Status != InvoiceStatus.Draft) throw new DomainException("Hisob-faktura allaqachon chiqarilgan.");
        if (invoice.TotalTiyin <= 0) throw new DomainException("Nol summali hisob-faktura chiqarilmaydi.");
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
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Bekor qilish sababi majburiy.");
        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Paid) throw new DomainException("Bu hisob-fakturani bekor qilib bo'lmaydi.");
        if (invoice.PaidTiyin > 0) throw new DomainException("To'lov bor — avval to'lovlarni storno qiling.");
        var before = Snapshot(invoice);
        invoice.Status = InvoiceStatus.Cancelled;
        audit.Add("invoice.cancel", "Invoice", invoice.Id, before, new { invoice.Status, Reason = reason.Trim() });
        await db.SaveChangesAsync(ct);
    }

    private async Task<Invoice> LoadAsync(int invoiceId, CancellationToken ct) =>
        await db.Invoices.Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct) ?? throw new DomainException("Hisob-faktura topilmadi.");

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
                throw new DomainException(invoice.Status == InvoiceStatus.Paid ? "Hisob-faktura to'liq to'langan." : "To'lov faqat chiqarilgan hisob-fakturaga belgilanadi.");
            if (amountTiyin <= 0) throw new DomainException("Summa musbat bo'lsin.");
            if (amountTiyin > invoice.RemainingTiyin)
                throw new DomainException($"Summa qoldiqdan ({Money.FormatSom(invoice.RemainingTiyin)}) katta. Ortiqcha to'lovni alohida hal qiling.");
            if (paidAt > clock.Today) throw new DomainException("To'lov sanasi kelajakda bo'lmasin.");
            if (paidAt < invoice.IssueDate.AddDays(-60)) throw new DomainException("To'lov sanasi hisob-fakturadan ancha oldin — tekshiring.");

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
                    warning = "Tenant hali ERP'ga bog'lanmagan — kalit ro'yxatdan o'tishda (register) avtomatik beriladi.";
            }
            catch (Exception ex) when (ex is DomainException or System.Security.Cryptography.CryptographicException or InvalidOperationException or NotSupportedException)
            {
                log.LogError(ex, "To'lovdan keyin kalit chiqmadi (invoice {Invoice})", invoice.Number);
                warning = "To'lov saqlandi, lekin kalit chiqmadi: " + ex.Message + " — tenant sahifasidan qayta chiqaring.";
            }
        }
        return new PaymentResult(payment, invoice, issued, warning);
    }

    /// <summary>Storno: o'chirmaydi, IsReversed + sabab. Obuna avtomatik qisqartirilmaydi — xodim qaror qiladi (kalitni bekor qilish alohida).</summary>
    public async Task<Invoice> ReversePaymentAsync(int paymentId, string reason, int staffId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Storno sababi majburiy.");
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct) ?? throw new DomainException("To'lov topilmadi.");
        if (payment.IsReversed) throw new DomainException("To'lov allaqachon storno qilingan.");
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

    public static bool IsConcurrencyConflict(Exception ex) =>
        ex is DbUpdateConcurrencyException || ex.InnerException is SqlException { Number: 1205 or 2601 or 2627 };
}
