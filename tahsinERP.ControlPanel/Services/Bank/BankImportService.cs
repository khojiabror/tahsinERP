using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Services.Bank;

public sealed record BankApplyOutcome(long LineId, int LineNo, bool Ok, string Message, PaymentResult? Payment);

/// <summary>
/// Bank ko'chirmasi importi (00_PLAN.md 10, 4-bosqich; 08_STAGE4.md 2):
/// fayl → qatorlar (<see cref="BankStatementParser"/>) → kiruvchi to'lovlarni ochiq hisob-fakturalarga moslash (avval to'lov maqsadidagi
/// TE-YYYY-NNNNNN raqami, keyin to'lovchi STIR + aniq qoldiq summa) → xodim ko'rib tasdiqlaydi → har tasdiqlangan qator
/// <see cref="BillingService.RecordPaymentAsync"/> orqali (qo'lda «To'lov qabul qilindi» bilan aynan bir xil: obuna uzaytirish, kalit, audit).
/// Idempotent: bitta bank tranzaksiyasi (sana + summa + hujjat № + to'lovchi hisobi) ikki marta qo'llanmaydi — AppliedKey noyob indeksi.
/// </summary>
public sealed partial class BankImportService(ControlDbContext db, AuditLogger audit, Clock clock, IServiceScopeFactory scopes,
    IOptions<VendorOptions> vendor, ILogger<BankImportService> log)
{
    [GeneratedRegex(@"(?<![A-Za-zА-Яа-я])[TТ]\s*[EЕ]\s*[-–—_/ ]?\s*(20\d{2})\s*[-–—_/ ]\s*(\d{1,6})(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex InvoiceNumberRegex();

    /// <summary>To'lov maqsadidagi hisob-faktura raqamlari, normallashtirilgan (TE-2026-000123). Kirill T/E ham tanib olinadi.</summary>
    public static IReadOnlyList<string> FindInvoiceNumbers(string? purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose)) return [];
        return InvoiceNumberRegex().Matches(purpose)
            .Select(m => BillingService.FormatNumber(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)))
            .Distinct().ToList();
    }

    /// <summary>Bank tranzaksiyasi kaliti: SHA-256(sana|summa|hujjat №|to'lovchi hisobi).</summary>
    public static string DedupKey(DateOnly date, long amountTiyin, string? docNumber, string? payerAccount)
    {
        string raw = $"{date:yyyy-MM-dd}|{amountTiyin}|{(docNumber ?? "").Trim().ToUpperInvariant()}|{new string((payerAccount ?? "").Where(char.IsAsciiDigit).ToArray())}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static string? Cut(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length > max ? s.Trim()[..max] : s.Trim());

    // ── Import ───────────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<(BankImport Import, IReadOnlyList<string> Warnings)> ImportAsync(string fileName, byte[] content, int staffId, CancellationToken ct = default)
    {
        var parsed = BankStatementParser.Parse(fileName, content);
        if (parsed.Lines.Count == 0)
            throw new DomainException("Faylda to'lov qatori topilmadi. " + string.Join(" ", parsed.Warnings.Take(3)));
        var warnings = parsed.Warnings.ToList();
        string hash = Convert.ToHexStringLower(SHA256.HashData(content));
        if (await db.BankImports.AnyAsync(i => i.FileHash == hash, ct))
            warnings.Insert(0, "Bu fayl avval ham yuklangan — qo'llangan qatorlar «Takror» deb belgilanadi.");

        var ours = OurAccounts(parsed.Accounts);
        string? ourInn = Digits(vendor.Value.Inn);
        var import = new BankImport
        {
            FileName = Cut(Path.GetFileName(fileName), 260) ?? "ko'chirma", Format = parsed.Format, FileHash = hash,
            Account = Cut(string.Join(", ", parsed.Accounts), 200), DateFrom = parsed.From, DateTo = parsed.To,
            UploadedByStaffId = staffId, UploadedAtUtc = clock.UtcNow,
        };
        var seen = new HashSet<string>();
        foreach (var p in parsed.Lines)
        {
            var line = new BankImportLine
            {
                LineNo = p.LineNo, DocNumber = Cut(p.DocNumber, 50), DocDate = p.Date, AmountTiyin = p.AmountTiyin,
                PayerName = Cut(p.PayerName, 300), PayerInn = Cut(p.PayerInn, 14), PayerAccount = Cut(p.PayerAccount, 34), Purpose = Cut(p.Purpose, 1000),
                DedupKey = DedupKey(p.Date, p.AmountTiyin, p.DocNumber, p.PayerAccount),
            };
            if (!IsIncoming(p, ours, ourInn))
                Set(line, BankMatchStatus.NotIncoming, BankMatchMethod.None, null, "Chiquvchi to'lov (bizning hisobdan) — e'tiborsiz.");
            else if (!seen.Add(line.DedupKey))
                Set(line, BankMatchStatus.Duplicate, BankMatchMethod.None, null, "Faylda shu tranzaksiya takrorlangan.");
            else
                await MatchAsync(line, ct);
            import.Lines.Add(line);
        }
        db.BankImports.Add(import);
        await db.SaveChangesAsync(ct);
        audit.Add("bank.import", "BankImport", import.Id, after: new
        {
            import.FileName, import.Format, Lines = import.Lines.Count,
            Matched = import.Lines.Count(l => l.MatchStatus == BankMatchStatus.Matched),
            Total = import.Lines.Where(l => l.MatchStatus != BankMatchStatus.NotIncoming).Sum(l => l.AmountTiyin),
        }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        return (import, warnings);
    }

    private HashSet<string> OurAccounts(IEnumerable<string> fromFile)
    {
        var set = new HashSet<string>(fromFile.Select(Digits).Where(a => a is not null)!);
        if (Digits(vendor.Value.BankAccount) is string v) set.Add(v);
        return set;
    }

    private static bool IsIncoming(ParsedBankLine p, HashSet<string> ours, string? ourInn)
    {
        if (p.IsIncoming is bool known) return known;
        if (p.PayerAccount is not null && ours.Contains(p.PayerAccount)) return false;
        if (ourInn is not null && p.PayerInn == ourInn) return false;
        return true; // qabul qiluvchi ustuni yo'q (CSV) yoki bizning hisob — kirim
    }

    private static void Set(BankImportLine l, BankMatchStatus status, BankMatchMethod method, int? invoiceId, string? note)
    {
        l.MatchStatus = status;
        l.MatchMethod = method;
        l.InvoiceId = invoiceId;
        l.MatchNote = Cut(note, 300);
    }

    /// <summary>Moslash: takror → raqam bo'yicha → STIR + aniq qoldiq. Qatorni o'zgartiradi (saqlamaydi).</summary>
    public async Task MatchAsync(BankImportLine line, CancellationToken ct = default)
    {
        if (await db.BankImportLines.AnyAsync(l => l.AppliedKey == line.DedupKey, ct))
        {
            Set(line, BankMatchStatus.Duplicate, BankMatchMethod.None, null, "Bu bank tranzaksiyasi avval qo'llangan.");
            return;
        }
        // Qo'lda belgilangan to'lov (shu sana, summa va to'lov topshirig'i raqami) — ikkinchi marta qo'llanmasin.
        if (!string.IsNullOrWhiteSpace(line.DocNumber))
        {
            var manual = await db.Payments.Where(p => !p.IsReversed && p.PaidAt == line.DocDate && p.AmountTiyin == line.AmountTiyin && p.Reference == line.DocNumber)
                .Select(p => new { p.Id, p.Invoice!.Number }).FirstOrDefaultAsync(ct);
            if (manual is not null)
            {
                Set(line, BankMatchStatus.Duplicate, BankMatchMethod.None, null, $"Qo'lda belgilangan to'lov #{manual.Id} ({manual.Number}) bilan bir xil — takror.");
                return;
            }
        }

        var numbers = FindInvoiceNumbers(line.Purpose);
        if (numbers.Count > 0)
        {
            var found = await db.Invoices.Include(i => i.Tenant).Where(i => i.Number != null && numbers.Contains(i.Number)).ToListAsync(ct);
            var open = found.Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid).ToList();
            if (open.Count == 1)
            {
                var inv = open[0];
                string innNote = line.PayerInn is not null && inv.Tenant?.Inn is not null && inv.Tenant.Inn != line.PayerInn
                    ? $" Diqqat: to'lovchi STIR {line.PayerInn}, tenant STIR {inv.Tenant.Inn}." : "";
                if (line.AmountTiyin > inv.RemainingTiyin)
                    Set(line, BankMatchStatus.Blocked, BankMatchMethod.InvoiceNumber, inv.Id,
                        $"{inv.Number}: summa qoldiqdan ({Money.FormatSom(inv.RemainingTiyin)}) katta — ortiqcha to'lovni qo'lda hal qiling.{innNote}");
                else
                    Set(line, BankMatchStatus.Matched, BankMatchMethod.InvoiceNumber, inv.Id,
                        $"{inv.Number} raqami bo'yicha{(line.AmountTiyin < inv.RemainingTiyin ? " (qisman to'lov)" : "")}.{innNote}");
                return;
            }
            if (open.Count > 1)
            {
                Set(line, BankMatchStatus.Ambiguous, BankMatchMethod.InvoiceNumber, null, "Maqsadda bir nechta ochiq hisob-faktura: " + string.Join(", ", open.Select(i => i.Number)));
                return;
            }
            if (found.Count > 0 && line.PayerInn is null)
            {
                var f = found[0];
                Set(line, BankMatchStatus.Blocked, BankMatchMethod.InvoiceNumber, f.Id, $"{f.Number}: {Pages.Labels.Of(f.Status).ToLowerInvariant()} — to'lov qabul qilinmaydi.");
                return;
            }
        }

        if (line.PayerInn is null)
        {
            Set(line, BankMatchStatus.Unmatched, BankMatchMethod.None, null, numbers.Count > 0 ? $"{string.Join(", ", numbers)} topilmadi yoki ochiq emas." : "Maqsadda hisob-faktura raqami yo'q, STIR yo'q.");
            return;
        }
        var byInn = await db.Invoices.Include(i => i.Tenant)
            .Where(i => i.Tenant!.Inn == line.PayerInn && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
                && i.TotalTiyin - i.PaidTiyin == line.AmountTiyin)
            .ToListAsync(ct);
        if (byInn.Count == 1)
            Set(line, BankMatchStatus.Matched, BankMatchMethod.InnAmount, byInn[0].Id, $"STIR {line.PayerInn} + summa bo'yicha: {byInn[0].Number}.");
        else if (byInn.Count > 1)
            Set(line, BankMatchStatus.Ambiguous, BankMatchMethod.InnAmount, null, "Shu STIR va summali bir nechta hisob-faktura: " + string.Join(", ", byInn.Select(i => i.Number)));
        else
        {
            bool tenantKnown = await db.Tenants.AnyAsync(t => t.Inn == line.PayerInn, ct);
            Set(line, BankMatchStatus.Unmatched, BankMatchMethod.None, null,
                (numbers.Count > 0 ? $"{string.Join(", ", numbers)} ochiq emas. " : "")
                + (tenantKnown ? $"STIR {line.PayerInn} tenant'i bor, lekin shu summali ochiq hisob-faktura yo'q." : $"STIR {line.PayerInn} bo'yicha tenant yo'q."));
        }
    }

    /// <summary>Xodim qatorga hisob-fakturani qo'lda tanlaydi (Ambiguous/Unmatched/Blocked). Bo'sh raqam — avtomatik moslashga qaytaradi.</summary>
    public async Task AssignAsync(long lineId, string? invoiceNumber, int staffId, CancellationToken ct = default)
    {
        var line = await db.BankImportLines.FirstOrDefaultAsync(l => l.Id == lineId, ct) ?? throw new DomainException("Qator topilmadi.");
        if (line.State is BankLineState.Applied or BankLineState.Applying) throw new DomainException("Qator allaqachon qo'llangan.");
        if (line.MatchStatus is BankMatchStatus.NotIncoming or BankMatchStatus.Duplicate) throw new DomainException("Bu qatorni qo'llab bo'lmaydi (chiquvchi yoki takror).");
        var before = new { line.MatchStatus, line.InvoiceId };
        string number = (invoiceNumber ?? "").Trim().ToUpperInvariant();
        if (number.Length == 0)
        {
            await MatchAsync(line, ct);
        }
        else
        {
            var normalized = FindInvoiceNumbers(number).FirstOrDefault() ?? number;
            var inv = await db.Invoices.FirstOrDefaultAsync(i => i.Number == normalized, ct) ?? throw new DomainException($"{normalized} hisob-faktura topilmadi.");
            if (inv.Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid))
                throw new DomainException($"{inv.Number}: {Pages.Labels.Of(inv.Status).ToLowerInvariant()} — to'lov qabul qilinmaydi.");
            if (line.AmountTiyin > inv.RemainingTiyin)
                throw new DomainException($"{inv.Number}: summa qoldiqdan ({Money.FormatSom(inv.RemainingTiyin)}) katta.");
            Set(line, BankMatchStatus.Matched, BankMatchMethod.Manual, inv.Id, $"Xodim tanladi: {inv.Number}.");
        }
        line.State = BankLineState.Pending;
        line.Error = null;
        audit.Add("bank.assign", "BankImportLine", line.Id, before, new { line.MatchStatus, line.InvoiceId, line.MatchMethod }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
    }

    public async Task SkipAsync(long lineId, int staffId, CancellationToken ct = default)
    {
        var line = await db.BankImportLines.FirstOrDefaultAsync(l => l.Id == lineId, ct) ?? throw new DomainException("Qator topilmadi.");
        if (line.State is BankLineState.Applied or BankLineState.Applying) throw new DomainException("Qator allaqachon qo'llangan.");
        line.State = BankLineState.Skipped;
        audit.Add("bank.skip", "BankImportLine", line.Id, after: new { line.ImportId, line.LineNo }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
    }

    // ── Qo'llash ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tanlangan qatorlarni qo'llaydi. Har qator alohida: avval AppliedKey band qilinadi (noyob indeks — parallel xodim yoki
    /// takroriy bosish ikkinchi marta o'tkaza olmaydi), keyin to'lov alohida DI doirasida (BillingService o'z tranzaksiyasi bilan)
    /// belgilanadi. To'lov rad etilsa — band bo'shatiladi, qator «Xato».
    /// </summary>
    public async Task<IReadOnlyList<BankApplyOutcome>> ApplyAsync(int importId, IReadOnlyCollection<long> lineIds, int staffId, CancellationToken ct = default)
    {
        var lines = await db.BankImportLines.Where(l => l.ImportId == importId && lineIds.Contains(l.Id)).OrderBy(l => l.LineNo).ToListAsync(ct);
        var results = new List<BankApplyOutcome>();
        foreach (var line in lines)
            results.Add(await ApplyLineAsync(line, staffId, ct));
        return results;
    }

    private async Task<BankApplyOutcome> ApplyLineAsync(BankImportLine line, int staffId, CancellationToken ct)
    {
        BankApplyOutcome Fail(string m) => new(line.Id, line.LineNo, false, m, null);
        if (line.State is BankLineState.Applied or BankLineState.Applying) return Fail("allaqachon qo'llangan");
        if (line.State == BankLineState.Skipped) return Fail("o'tkazib yuborilgan");
        if (line.MatchStatus != BankMatchStatus.Matched || line.InvoiceId is null) return Fail("hisob-faktura tanlanmagan");

        // 1) Band qilish (idempotentlik kafolati — bazada).
        line.AppliedKey = line.DedupKey;
        line.State = BankLineState.Applying;
        line.Error = null;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await db.Entry(line).ReloadAsync(ct);
            Set(line, BankMatchStatus.Duplicate, BankMatchMethod.None, null, "Bu bank tranzaksiyasi boshqa qatorda allaqachon qo'llangan.");
            line.State = BankLineState.Pending;
            await db.SaveChangesAsync(ct);
            return Fail("takror — allaqachon qo'llangan");
        }

        // 2) To'lov — qo'lda belgilash bilan aynan bir xil yo'l, alohida kontekstda (xato bo'lsa bu kontekstga qoldiq qolmaydi).
        PaymentResult? result = null;
        string? error = null;
        try
        {
            using var scope = scopes.CreateScope();
            var billing = scope.ServiceProvider.GetRequiredService<BillingService>();
            result = await billing.RecordPaymentAsync(line.InvoiceId.Value, line.AmountTiyin, line.DocDate, PaymentMethod.Bank,
                line.DocNumber, Cut($"Bank ko'chirmasi #{line.ImportId}, {line.LineNo}-qator" + (line.PayerName is null ? "" : ", " + line.PayerName), 1000), staffId, ct);
        }
        catch (DomainException ex) { error = ex.Message; }
        catch (Exception ex) when (BillingService.IsConcurrencyConflict(ex)) { error = "Hisob-faktura shu orada o'zgardi — qayta urinib ko'ring."; }

        if (result is null)
        {
            line.AppliedKey = null;
            line.State = BankLineState.Failed;
            line.Error = Cut(error, 1000);
            await db.SaveChangesAsync(ct);
            log.LogWarning("Bank qatori #{Line} qo'llanmadi: {Error}", line.Id, error);
            return Fail(error ?? "xato");
        }
        line.State = BankLineState.Applied;
        line.PaymentId = result.Payment.Id;
        line.AppliedByStaffId = staffId;
        line.AppliedAtUtc = clock.UtcNow;
        audit.Add("bank.apply", "BankImportLine", line.Id, after: new
        {
            line.ImportId, line.LineNo, line.DocNumber, line.DocDate, line.AmountTiyin, line.PayerInn, InvoiceId = line.InvoiceId, result.Invoice.Number,
            PaymentId = result.Payment.Id, InvoiceStatus = result.Invoice.Status, LicenseId = result.IssuedLicense?.Id,
        }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        string msg = $"{result.Invoice.Number}: {Money.FormatSom(line.AmountTiyin)} → {Pages.Labels.Of(result.Invoice.Status)}"
                     + (result.IssuedLicense is not null ? $", yangi kalit #{result.IssuedLicense.Id}" : "")
                     + (result.LicenseWarning is not null ? $" ({result.LicenseWarning})" : "");
        return new BankApplyOutcome(line.Id, line.LineNo, true, msg, result);
    }

    private static string? Digits(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string d = new string(s.Where(char.IsAsciiDigit).ToArray());
        return d.Length == 0 ? null : d;
    }
}
