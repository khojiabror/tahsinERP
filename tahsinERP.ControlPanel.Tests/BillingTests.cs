using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Services.LicenseSync;
using ErpLicense = tahsinERP.Licensing.LicenseHelper;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Hisob-faktura → qo'lda to'lov → obuna uzaytirish → yangi kalit → ERP pull. Storno va eslatmalar.</summary>
public class BillingTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private const long MonthPrice = 112_000_000; // 1 120 000 so'm, QQS bilan

    private Task SetBiznesPriceAsync() => f.WithScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ControlDbContext>();
        var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Biznes);
        plan.PriceMonthTiyin = MonthPrice;
        await db.SaveChangesAsync();
    });

    private async Task<(Tenant Tenant, Subscription Sub, Invoice Invoice)> PaidSubscriptionSetupAsync(string slug, int months, bool register = true, string? inst = null)
    {
        await SetBiznesPriceAsync();
        int staff = await f.EnsureStaffAsync();
        var tenant = await f.CreateTenantAsync(slug);
        if (register)
        {
            var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), inst ?? Guid.NewGuid().ToString().ToUpperInvariant(), 1, "1.0", null);
            Assert.True(reg.Ok, reg.Error);
        }
        return await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
            var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Biznes);
            var sub = await sp.GetRequiredService<SubscriptionService>().CreateAsync(t, plan.Id, BillingPeriod.Month, null, new DateOnly(2026, 10, 18), false);
            var billing = sp.GetRequiredService<BillingService>();
            var inv = await billing.CreateDraftAsync(t.Id, sub.Id, months, [BillingService.DefaultLine(sub, plan, months)], null, staff);
            await billing.IssueAsync(inv);
            return (t, sub, inv);
        });
    }

    [Fact]
    public async Task Numbering_IsSequentialPerYear()
    {
        var (_, _, a) = await PaidSubscriptionSetupAsync("num-a", 1, register: false);
        var (_, _, b) = await PaidSubscriptionSetupAsync("num-b", 1, register: false);
        int na = int.Parse(a.Number![^6..]), nb = int.Parse(b.Number![^6..]);
        Assert.StartsWith("TE-2026-", a.Number);
        Assert.Equal(na + 1, nb);
        Assert.Equal(3 * MonthPrice, (await PaidSubscriptionSetupAsync("num-c", 3, register: false)).Invoice.TotalTiyin);
    }

    [Fact]
    public async Task PartialThenFullPayment_ExtendsSubscription_IssuesKey_DeliveredByPull()
    {
        string inst = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var (tenant, sub, inv) = await PaidSubscriptionSetupAsync("pay-flow", 2, inst: inst);
        int staff = await f.EnsureStaffAsync();
        Assert.Equal(2 * MonthPrice, inv.TotalTiyin);
        Assert.Equal(Money.VatIncluded(2 * MonthPrice, 12m), inv.VatTiyin);

        // Qisman — PartiallyPaid, kalit chiqmaydi.
        var partial = await f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>()
            .RecordPaymentAsync(inv.Id, MonthPrice, new DateOnly(2026, 10, 18), PaymentMethod.Bank, "PT-001", null, staff));
        Assert.Equal(InvoiceStatus.PartiallyPaid, partial.Invoice.Status);
        Assert.Null(partial.IssuedLicense);

        // Ortiqcha summa rad etiladi.
        await Assert.ThrowsAsync<DomainException>(() => f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>()
            .RecordPaymentAsync(inv.Id, MonthPrice + 1, new DateOnly(2026, 10, 18), PaymentMethod.Bank, null, null, staff)));

        // To'liq — Paid → obuna 2 oyga → tenant Active → BIZNES kaliti (oxir + 3 kun).
        var full = await f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>()
            .RecordPaymentAsync(inv.Id, MonthPrice, new DateOnly(2026, 10, 18), PaymentMethod.Cash, "KV-77", "kassa", staff));
        Assert.Equal(InvoiceStatus.Paid, full.Invoice.Status);
        Assert.Null(full.LicenseWarning);
        Assert.NotNull(full.IssuedLicense);
        Assert.Equal("BIZNES", full.IssuedLicense!.Edition);
        Assert.Equal(20, full.IssuedLicense.MaxUsers);
        Assert.Equal(new DateOnly(2026, 12, 20), full.IssuedLicense.ExpiryDate);

        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var s = await db.Subscriptions.FirstAsync(x => x.Id == sub.Id);
            Assert.Equal(SubscriptionStatus.Active, s.Status);
            Assert.Equal(new DateOnly(2026, 10, 18), s.StartDate);
            Assert.Equal(new DateOnly(2026, 12, 17), s.EndDate);
            Assert.Equal(TenantStatus.Active, (await db.Tenants.FirstAsync(x => x.Id == tenant.Id)).Status);
            Assert.Equal(SubscriptionStatus.Expired, (await db.Subscriptions.FirstAsync(x => x.TenantId == tenant.Id && x.Period == BillingPeriod.Trial)).Status);
            var actions = await db.AuditLog.Select(a => a.Action).ToListAsync();
            Assert.Contains("payment.record", actions);
            Assert.Contains("subscription.extend", actions);
            Assert.Contains("license.issue", actions);
            Assert.Contains("invoice.issue", actions);
        });

        // ERP: register vaqtidagi TRIAL kaliti bilan pull → yangi BIZNES kaliti.
        var secret = await f.WithScopeAsync(async sp =>
        {
            var t = await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(x => x.Id == tenant.Id);
            return sp.GetRequiredService<Security.ISecretProtector>().Unprotect(t.ApiSecretProtected!, Security.SecretPurposes.TenantApiSecret);
        });
        var pull = await f.ErpClient().PullAsync(inst, secret, "eski-trial-kalit");
        Assert.Equal(LicensePanelClient.PullStatus.Updated, pull.Status);
        Assert.True(ErpLicense.TryParseKey(pull.LicenseKey, f.ErpTrusted, out var info));
        Assert.True(info.IsBoundTo(inst));
        Assert.Equal("BIZNES", info.Edition);
        Assert.Equal(new DateTime(2026, 12, 20), info.ExpiryDate);
    }

    [Fact]
    public async Task DeletePayment_ThenInvoice_RemovesRows_FreesBankLine_Audits()
    {
        var (tenant, sub, inv) = await PaidSubscriptionSetupAsync("pay-delete", 1, register: false);
        int staff = await f.EnsureStaffAsync();
        var res = await f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>()
            .RecordPaymentAsync(inv.Id, MonthPrice, new DateOnly(2026, 10, 18), PaymentMethod.Bank, "PT-DEL", null, staff));
        Assert.Equal(InvoiceStatus.Paid, res.Invoice.Status);
        var bankLineId = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var imp = new BankImport { FileName = "t.txt", FileHash = Guid.NewGuid().ToString("N"), UploadedByStaffId = staff, UploadedAtUtc = DateTime.UtcNow };
            db.BankImports.Add(imp);
            await db.SaveChangesAsync();
            var line = new BankImportLine { ImportId = imp.Id, LineNo = 1, DocDate = new DateOnly(2026, 10, 18), AmountTiyin = MonthPrice, DedupKey = "del-" + inv.Id,
                AppliedKey = "del-" + inv.Id, InvoiceId = inv.Id, PaymentId = res.Payment.Id, State = BankLineState.Applied };
            db.BankImportLines.Add(line);
            await db.SaveChangesAsync();
            return line.Id;
        });

        await Assert.ThrowsAsync<DomainException>(() => f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>().DeleteInvoiceAsync(inv.Id, "xato", staff)));
        await Assert.ThrowsAsync<DomainException>(() => f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>().DeletePaymentAsync(res.Payment.Id, " ", staff)));
        var after = await f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>().DeletePaymentAsync(res.Payment.Id, "Sinov to'lovi", staff));
        Assert.Equal(InvoiceStatus.Issued, after.Status);
        Assert.Equal(0, after.PaidTiyin);

        await f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>().DeleteInvoiceAsync(inv.Id, "Sinov hisob-fakturasi", staff));
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            Assert.False(await db.Payments.AnyAsync(x => x.Id == res.Payment.Id));
            Assert.False(await db.Invoices.AnyAsync(x => x.Id == inv.Id));
            var line = await db.BankImportLines.FirstAsync(x => x.Id == bankLineId);
            Assert.Equal((BankLineState.Pending, (int?)null, (int?)null, (string?)null), (line.State, line.PaymentId, line.InvoiceId, line.AppliedKey));
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "payment.delete" && a.EntityId == res.Payment.Id.ToString() && a.StaffUserId == staff));
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "invoice.delete" && a.EntityId == inv.Id.ToString()));
            Assert.Equal(new DateOnly(2026, 11, 17), (await db.Subscriptions.FirstAsync(x => x.Id == sub.Id)).EndDate); // obunaga tegilmaydi
        });
    }

    [Fact]
    public async Task Reverse_RequiresReason_DoesNotShortenSubscription_AndNoDoubleExtension()
    {
        var (tenant, sub, inv) = await PaidSubscriptionSetupAsync("pay-reverse", 1);
        int staff = await f.EnsureStaffAsync();
        var res = await f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>()
            .RecordPaymentAsync(inv.Id, MonthPrice, new DateOnly(2026, 10, 18), PaymentMethod.Bank, "PT-9", null, staff));
        Assert.Equal(InvoiceStatus.Paid, res.Invoice.Status);

        await Assert.ThrowsAsync<DomainException>(() => f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>().ReversePaymentAsync(res.Payment.Id, " ", staff)));
        var after = await f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>().ReversePaymentAsync(res.Payment.Id, "Bank qaytardi", staff));
        Assert.Equal(InvoiceStatus.Issued, after.Status);
        Assert.Equal(0, after.PaidTiyin);

        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var p = await db.Payments.FirstAsync(x => x.Id == res.Payment.Id);
            Assert.True(p.IsReversed); // o'chirilmagan
            Assert.Equal("Bank qaytardi", p.ReverseReason);
            Assert.Equal(new DateOnly(2026, 11, 17), (await db.Subscriptions.FirstAsync(x => x.Id == sub.Id)).EndDate); // qisqarmadi
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "payment.reverse" && a.EntityId == p.Id.ToString() && a.StaffUserId == staff));
        });

        // Qayta to'lansa — obuna IKKINCHI marta uzaytirilmaydi.
        await f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>()
            .RecordPaymentAsync(inv.Id, MonthPrice, new DateOnly(2026, 10, 18), PaymentMethod.Bank, "PT-10", null, staff));
        await f.WithScopeAsync(async sp =>
            Assert.Equal(new DateOnly(2026, 11, 17), (await sp.GetRequiredService<ControlDbContext>().Subscriptions.FirstAsync(x => x.Id == sub.Id)).EndDate));
    }

    [Fact]
    public async Task Payment_ForUnboundTenant_WarnsAndKeyComesAtRegister()
    {
        var (tenant, _, inv) = await PaidSubscriptionSetupAsync("pay-unbound", 1, register: false);
        int staff = await f.EnsureStaffAsync();
        var res = await f.WithScopeAsync(sp => sp.GetRequiredService<BillingService>()
            .RecordPaymentAsync(inv.Id, MonthPrice, new DateOnly(2026, 10, 18), PaymentMethod.Bank, null, null, staff));
        Assert.Equal(InvoiceStatus.Paid, res.Invoice.Status);
        Assert.Null(res.IssuedLicense);
        Assert.NotNull(res.LicenseWarning);

        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), Guid.NewGuid().ToString().ToUpperInvariant(), 1, "1.0", null);
        Assert.True(ErpLicense.TryParseKey(reg.LicenseKey, f.ErpTrusted, out var info));
        Assert.Equal("BIZNES", info.Edition);
    }

    [Fact]
    public async Task OnPrem_KeyCappedToOneYear()
    {
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Korp);
            plan.PriceYearTiyin = 1_000_000_000;
            await db.SaveChangesAsync();
        });
        int staff = await f.EnsureStaffAsync();
        var tenant = await f.CreateTenantAsync("onprem-2y", trial: false, deployment: DeploymentKind.OnPrem);
        await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), Guid.NewGuid().ToString().ToUpperInvariant(), 1, "1.0", null);
        var res = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
            var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Korp);
            var sub = await sp.GetRequiredService<SubscriptionService>().CreateAsync(t, plan.Id, BillingPeriod.Year, null, new DateOnly(2026, 10, 18), false);
            var billing = sp.GetRequiredService<BillingService>();
            var inv = await billing.CreateDraftAsync(t.Id, sub.Id, 2, [BillingService.DefaultLine(sub, plan, 2)], null, staff);
            await billing.IssueAsync(inv);
            return await billing.RecordPaymentAsync(inv.Id, inv.TotalTiyin, new DateOnly(2026, 10, 18), PaymentMethod.Bank, null, null, staff);
        });
        Assert.NotNull(res.IssuedLicense);
        Assert.Equal(new DateOnly(2027, 10, 17), res.IssuedLicense!.ExpiryDate); // 2 yil to'langan, kalit ≤ 1 yil
    }

    [Fact]
    public async Task Lifecycle_And_Reminders()
    {
        var tenant = await f.CreateTenantAsync("life-trial", trial: true);
        // Sinov 31.10 da tugaydi → 24.10 da "7 kun qoldi" eslatmasi (Telegram sozlanmagan → Log).
        f.Time.SetUtcNow(new DateTimeOffset(2026, 10, 24, 5, 0, 0, TimeSpan.Zero));
        try
        {
            int sent = await f.WithScopeAsync(sp => sp.GetRequiredService<ExpiryReminderService>().RunAsync());
            Assert.True(sent >= 1);
            int again = await f.WithScopeAsync(sp => sp.GetRequiredService<ExpiryReminderService>().RunAsync());
            Assert.Equal(0, again); // takrorlanmaydi
            await f.WithScopeAsync(async sp =>
            {
                var n = await sp.GetRequiredService<ControlDbContext>().Notifications.SingleAsync(x => x.TenantId == tenant.Id);
                Assert.Equal("expiry-7", n.Template);
                Assert.Equal(NotificationStatus.Logged, n.Status);
            });

            // Sinov tugagach → Suspended (audit bilan).
            f.Time.SetUtcNow(new DateTimeOffset(2026, 11, 1, 5, 0, 0, TimeSpan.Zero));
            await f.WithScopeAsync(sp => sp.GetRequiredService<SubscriptionService>().RunLifecycleAsync());
            await f.WithScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<ControlDbContext>();
                Assert.Equal(TenantStatus.Suspended, (await db.Tenants.FirstAsync(x => x.Id == tenant.Id)).Status);
                Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "tenant.status" && a.EntityId == tenant.Id.ToString() && a.StaffUserId == null));
            });
        }
        finally { f.Time.SetUtcNow(PanelFactory.Start); }
    }
}
