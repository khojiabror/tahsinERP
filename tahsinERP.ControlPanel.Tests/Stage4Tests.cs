using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Services;
using tahsinERP.ControlPanel.Services.Bank;
using tahsinERP.Services.Editions;
using tahsinERP.Services.LicenseSync;

namespace tahsinERP.ControlPanel.Tests;

// 4-bosqich (Docs/ControlPanel/08_STAGE4.md): DIST paketi, bank ko'chirmasi, Didox, on-prem onlayn tekshiruv, portal SMS/email va parol tiklash.

public class DistPlanTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    [Fact]
    public async Task DistPlan_Seeded_AndKeyCarriesDistEdition()
    {
        await PortalTestKit.SetPricesAsync(f);
        var plan = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Plans.AsNoTracking().SingleAsync(p => p.Code == PlanCodes.Dist));
        Assert.Equal(20, plan.MaxUsers);
        Assert.False(plan.IsAddOn);
        Assert.Equal(string.Join(",", EditionModules.ModulesOf(EditionModules.Dist)), plan.Modules);
        var korp = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Plans.AsNoTracking().SingleAsync(p => p.Code == PlanCodes.Korp));
        Assert.Contains("Distribution", korp.Modules);

        var tenant = await f.CreateTenantAsync("dist-a");
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), Guid.NewGuid().ToString().ToUpperInvariant(), 1, "1.0", null);
        Assert.True(reg.Ok, reg.Error);
        await PortalTestKit.PaidAsync(f, tenant.Id, PlanCodes.Dist, 1, new DateOnly(2026, 10, 18));
        var lic = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            return await sp.GetRequiredService<LicenseIssuer>().GetCurrentAsync(await db.Tenants.FirstAsync(t => t.Id == tenant.Id));
        });
        Assert.Equal("DIST", lic!.Edition);
        Assert.Equal(20, lic.MaxUsers);
        Assert.True(EditionModules.IsControllerAllowed(lic.Edition, "DstOrders"));
        Assert.False(EditionModules.IsControllerAllowed(lic.Edition, "Bom"));
    }

    [Fact]
    public void Classify_DistIsSideBranch()
    {
        var start = new Plan { Id = 1, Code = PlanCodes.Start, SortOrder = 1, MaxUsers = 5 };
        var biznes = new Plan { Id = 2, Code = PlanCodes.Biznes, SortOrder = 2, MaxUsers = 20 };
        var korp = new Plan { Id = 3, Code = PlanCodes.Korp, SortOrder = 3, MaxUsers = 50 };
        var dist = new Plan { Id = 6, Code = PlanCodes.Dist, SortOrder = 5, MaxUsers = 20 };
        var today = new DateOnly(2026, 10, 20);
        Subscription Cur(Plan p) => new() { PlanId = p.Id, Plan = p, Period = BillingPeriod.Month, StartDate = new(2026, 10, 1), EndDate = new(2026, 10, 31) };
        var periodEnd = new DateOnly(2026, 11, 1);

        Assert.Equal((PortalRequestKind.Upgrade, today), PortalRequestService.Classify(Cur(start), dist, BillingPeriod.Month, today));
        Assert.Equal((PortalRequestKind.Upgrade, today), PortalRequestService.Classify(Cur(dist), korp, BillingPeriod.Month, today));
        Assert.Equal((PortalRequestKind.ChangeAtPeriodEnd, periodEnd), PortalRequestService.Classify(Cur(dist), biznes, BillingPeriod.Month, today));
        Assert.Equal((PortalRequestKind.ChangeAtPeriodEnd, periodEnd), PortalRequestService.Classify(Cur(biznes), dist, BillingPeriod.Month, today));
        Assert.Equal((PortalRequestKind.ChangeAtPeriodEnd, periodEnd), PortalRequestService.Classify(Cur(korp), dist, BillingPeriod.Month, today));
        Assert.Equal((PortalRequestKind.ChangeAtPeriodEnd, periodEnd), PortalRequestService.Classify(Cur(dist), start, BillingPeriod.Month, today));
        Assert.Equal((PortalRequestKind.Renew, periodEnd), PortalRequestService.Classify(Cur(dist), dist, BillingPeriod.Month, today));
        // Asosiy zanjir o'zgarmagan
        Assert.Equal((PortalRequestKind.Upgrade, today), PortalRequestService.Classify(Cur(start), biznes, BillingPeriod.Month, today));
    }
}

public class BankParserTests
{
    internal static byte[] OneC(params (string No, string Date, string Sum, string PayerInn, string PayerAcc, string Purpose, bool Incoming)[] docs)
    {
        var sb = new StringBuilder();
        sb.Append("1CClientBankExchange\r\nВерсияФормата=1.03\r\nКодировка=Windows\r\nДатаНачала=01.10.2026\r\nДатаКонца=18.10.2026\r\nРасчСчет=20208000900111222001\r\n");
        sb.Append("СекцияРасчСчет\r\nРасчСчет=20208000900111222001\r\nКонецРасчСчет\r\n");
        foreach (var d in docs)
        {
            sb.Append("СекцияДокумент=Платежное поручение\r\n");
            sb.Append($"Номер={d.No}\r\nДата={d.Date}\r\nСумма={d.Sum}\r\n");
            if (d.Incoming)
                sb.Append($"ПлательщикСчет={d.PayerAcc}\r\nПлательщик1=\"Mijoz\" MChJ\r\nПлательщикИНН={d.PayerInn}\r\nПолучательСчет=20208000900111222001\r\nПолучательИНН=300000001\r\nДатаПоступило={d.Date}\r\n");
            else
                sb.Append($"ПлательщикСчет=20208000900111222001\r\nПлательщикИНН=300000001\r\nПолучательСчет={d.PayerAcc}\r\nПолучательИНН={d.PayerInn}\r\nДатаСписано={d.Date}\r\n");
            sb.Append($"НазначениеПлатежа={d.Purpose}\r\nКонецДокумента\r\n");
        }
        sb.Append("КонецФайла\r\n");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1251).GetBytes(sb.ToString());
    }

    [Fact]
    public void OneC_Windows1251_IncomingAndOutgoing()
    {
        var bytes = OneC(("17", "15.10.2026", "1120000.00", "301234567", "20208000500999888001", "TE-2026-000005 hisob-faktura bo'yicha to'lov", true),
                         ("18", "16.10.2026", "50000", "309999999", "20208000500777666001", "Ijara", false));
        var s = BankStatementParser.Parse("vypiska.txt", bytes);
        Assert.Equal(BankImportFormat.OneC, s.Format);
        Assert.Equal(["20208000900111222001"], s.Accounts);
        Assert.Equal(new DateOnly(2026, 10, 1), s.From);
        Assert.Equal(2, s.Lines.Count);
        var a = s.Lines[0];
        Assert.Equal(("17", new DateOnly(2026, 10, 15), 112_000_000L, "301234567", "20208000500999888001", true),
            (a.DocNumber, a.Date, a.AmountTiyin, a.PayerInn, a.PayerAccount, a.IsIncoming));
        Assert.Equal("\"Mijoz\" MChJ", a.PayerName);
        Assert.Contains("TE-2026-000005", a.Purpose);
        Assert.False(s.Lines[1].IsIncoming);
    }

    [Fact]
    public void Csv_HeaderSynonyms_QuotesAndAmounts()
    {
        string csv = "Sana;Hujjat №;Summa;To'lovchi;STIR;To'lov maqsadi\r\n" +
                     "15.10.2026;101;\"1 120 000,00\";\"Mijoz; MChJ\";301234567;\"TE 2026 5 uchun\"\r\n" +
                     "16.10.2026;102;-5000;Biz;300000001;chiqim\r\n" +
                     ";;;;;\r\n";
        var s = BankStatementParser.Parse("v.csv", Encoding.UTF8.GetBytes(csv));
        Assert.Equal(BankImportFormat.Csv, s.Format);
        Assert.Equal(2, s.Lines.Count);
        Assert.Equal(112_000_000L, s.Lines[0].AmountTiyin);
        Assert.Equal("Mijoz; MChJ", s.Lines[0].PayerName);
        Assert.Equal("101", s.Lines[0].DocNumber);
        Assert.False(s.Lines[1].IsIncoming);
        Assert.Equal(["TE-2026-000005"], BankImportService.FindInvoiceNumbers(s.Lines[0].Purpose));
    }

    internal static byte[] Xlsx(string[][] rows, int dateSerialRow = -1)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            for (int r = 0; r < rows.Length; r++)
            {
                sb.Append($"<row r=\"{r + 1}\">");
                for (int c = 0; c < rows[r].Length; c++)
                {
                    string cell = (char)('A' + c) + (r + 1).ToString();
                    if (r == dateSerialRow && c == 0)
                        sb.Append($"<c r=\"{cell}\"><v>{new DateTime(2026, 10, 15).ToOADate()}</v></c>");
                    else
                        sb.Append($"<c r=\"{cell}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(rows[r][c])}</t></is></c>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            var e = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
            w.Write(sb.ToString());
        }
        return ms.ToArray();
    }

    [Fact]
    public void Xlsx_InlineStringsAndSerialDate()
    {
        var bytes = Xlsx([["Дата", "Сумма", "ИНН плательщика", "Назначение платежа"], ["", "1120000.5", "301234567", "оплата по счету ТЕ-2026-000005"]], dateSerialRow: 1);
        var s = BankStatementParser.Parse("v.xlsx", bytes);
        Assert.Equal(BankImportFormat.Xlsx, s.Format);
        var l = Assert.Single(s.Lines);
        Assert.Equal(new DateOnly(2026, 10, 15), l.Date);
        Assert.Equal(112_000_050L, l.AmountTiyin);
        Assert.Equal("301234567", l.PayerInn);
        Assert.Equal(["TE-2026-000005"], BankImportService.FindInvoiceNumbers(l.Purpose)); // kirill «ТЕ»
    }

    [Fact]
    public void Amounts_AndNumbers()
    {
        foreach (var (text, tiyin) in new[] { ("1 234 567,89", 123_456_789L), ("1234567.89", 123_456_789L), ("1,234,567", 123_456_700L), ("1.234.567,5", 123_456_750L), ("-500", -50_000L) })
        {
            Assert.True(BankStatementParser.TryParseAmount(text, out long v), text);
            Assert.Equal(tiyin, v);
        }
        Assert.Empty(BankImportService.FindInvoiceNumbers("SITE-2026-000001 emas, ATE-2026-1 ham emas"));
        Assert.Equal(["TE-2026-000123", "TE-2026-000007"], BankImportService.FindInvoiceNumbers("te-2026-000123 va TE/2026/7"));
        Assert.Throws<DomainException>(() => BankStatementParser.Parse("v.csv", Encoding.UTF8.GetBytes("a;b\r\n1;2")));
        Assert.NotEqual(BankImportService.DedupKey(new(2026, 10, 15), 100, "17", "2020"), BankImportService.DedupKey(new(2026, 10, 15), 100, "18", "2020"));
    }
}

public class BankImportTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private async Task<(Tenant Tenant, Invoice Invoice)> IssuedAsync(string slug, string inn, int months = 1)
    {
        await PortalTestKit.SetPricesAsync(f);
        int staff = await f.EnsureStaffAsync();
        var tenant = await f.WithScopeAsync(sp => sp.GetRequiredService<TenantService>()
            .CreateAsync(new Tenant { Slug = slug, LegalName = slug + " MChJ", Inn = inn, Deployment = DeploymentKind.SaaS, CompanyId = 1 }, true));
        return await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
            var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Biznes);
            var sub = await sp.GetRequiredService<SubscriptionService>().CreateAsync(t, plan.Id, BillingPeriod.Month, null, new DateOnly(2026, 10, 18), false);
            var billing = sp.GetRequiredService<BillingService>();
            var inv = await billing.CreateDraftAsync(t.Id, sub.Id, months, [BillingService.DefaultLine(sub, plan, months)], null, staff);
            await billing.IssueAsync(inv);
            return (t, inv);
        });
    }

    private Task<(BankImport Import, IReadOnlyList<string> Warnings)> ImportAsync(byte[] bytes, string name = "v.txt") => f.WithScopeAsync(async sp =>
        await sp.GetRequiredService<BankImportService>().ImportAsync(name, bytes, await f.EnsureStaffAsync()));

    private Task<IReadOnlyList<BankApplyOutcome>> ApplyAsync(BankImport import, params long[] ids) => f.WithScopeAsync(async sp =>
        await sp.GetRequiredService<BankImportService>().ApplyAsync(import.Id, ids, await f.EnsureStaffAsync()));

    private static string Sum(long tiyin) => (tiyin / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task ByNumber_Apply_ExtendsSubscription_Idempotent()
    {
        var (tenant, inv) = await IssuedAsync("bank-a", "301234501");
        var file = BankParserTests.OneC(
            ("501", "18.10.2026", Sum(inv.TotalTiyin), "309000001", "20208000500999000001", $"{inv.Number} bo'yicha to'lov (uchinchi shaxs)", true),
            ("502", "18.10.2026", "999.00", "309000002", "20208000500999000002", "boshqa to'lov", true),
            ("503", "18.10.2026", "1000.00", "309000003", "20208000500999000003", "ijara", false));
        var (import, _) = await ImportAsync(file);
        var lines = import.Lines.OrderBy(l => l.LineNo).ToList();
        Assert.Equal(BankMatchStatus.Matched, lines[0].MatchStatus);
        Assert.Equal(BankMatchMethod.InvoiceNumber, lines[0].MatchMethod);
        Assert.Equal(inv.Id, lines[0].InvoiceId);
        Assert.Contains("STIR", lines[0].MatchNote); // to'lovchi STIR tenantnikiga mos emas — ogohlantirish
        Assert.Equal(BankMatchStatus.Unmatched, lines[1].MatchStatus);
        Assert.Equal(BankMatchStatus.NotIncoming, lines[2].MatchStatus);

        var res = await ApplyAsync(import, lines.Select(l => l.Id).ToArray());
        Assert.True(res.Single(r => r.LineId == lines[0].Id).Ok);
        Assert.False(res.Single(r => r.LineId == lines[1].Id).Ok);

        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var i = await db.Invoices.Include(x => x.Payments).AsNoTracking().FirstAsync(x => x.Id == inv.Id);
            Assert.Equal(InvoiceStatus.Paid, i.Status);
            Assert.NotNull(i.SubscriptionAppliedUtc);
            var p = Assert.Single(i.Payments);
            Assert.Equal(("501", PaymentMethod.Bank, new DateOnly(2026, 10, 18)), (p.Reference, p.Method, p.PaidAt));
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == i.SubscriptionId);
            Assert.Equal(SubscriptionStatus.Active, sub.Status);
            Assert.Equal(TenantStatus.Active, (await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id)).Status);
            var line = await db.BankImportLines.AsNoTracking().FirstAsync(l => l.Id == lines[0].Id);
            Assert.Equal((BankLineState.Applied, p.Id), (line.State, line.PaymentId));
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "bank.apply" && a.EntityId == line.Id.ToString()));
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "payment.record" && a.EntityId == p.Id.ToString()));
        });

        // Xuddi shu faylni qayta yuklash → qo'llangan tranzaksiya «Takror»; qayta qo'llash ham bo'lmaydi.
        var (again, warnings) = await ImportAsync(file);
        Assert.Contains(warnings, w => w.Contains("avval ham yuklangan"));
        var dup = again.Lines.Single(l => l.DocNumber == "501");
        Assert.Equal(BankMatchStatus.Duplicate, dup.MatchStatus);
        Assert.False((await ApplyAsync(again, dup.Id)).Single().Ok);
        // Birinchi importdagi qatorni ikkinchi marta qo'llab bo'lmaydi.
        Assert.False((await ApplyAsync(import, lines[0].Id)).Single().Ok);
        Assert.Equal(1, await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Payments.CountAsync(p => p.InvoiceId == inv.Id)));
    }

    [Fact]
    public async Task TwoImportsBeforeApply_UniqueKeyBlocksSecond()
    {
        var (_, inv) = await IssuedAsync("bank-b", "301234502");
        var file = BankParserTests.OneC(("601", "18.10.2026", Sum(inv.TotalTiyin), "301234502", "20208000500999000011", inv.Number!, true));
        var (a, _) = await ImportAsync(file);
        var (b, _) = await ImportAsync(file); // a hali qo'llanmagan — ikkalasi ham «Mos»
        Assert.Equal(BankMatchStatus.Matched, b.Lines.Single().MatchStatus);
        Assert.True((await ApplyAsync(a, a.Lines.Single().Id)).Single().Ok);
        var second = (await ApplyAsync(b, b.Lines.Single().Id)).Single();
        Assert.False(second.Ok);
        Assert.Contains("takror", second.Message);
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            Assert.Equal(1, await db.Payments.CountAsync(p => p.InvoiceId == inv.Id));
            Assert.Equal(BankMatchStatus.Duplicate, (await db.BankImportLines.AsNoTracking().FirstAsync(l => l.Id == b.Lines.Single().Id)).MatchStatus);
        });
    }

    [Fact]
    public async Task ByInnAndExactAmount_Csv_PartialAndManualDuplicate()
    {
        var (_, inv) = await IssuedAsync("bank-c", "301234503", months: 2);
        long half = inv.TotalTiyin / 2;
        // 1) STIR + aniq qoldiq: qoldiqqa teng bo'lmagan summa moslanmaydi.
        string csv = "Sana;Summa;STIR;Maqsad;Raqam\r\n" +
                     $"18.10.2026;{Sum(half)};301234503;dasturiy ta'minot uchun;701\r\n" +
                     $"18.10.2026;{Sum(inv.TotalTiyin)};301234503;dasturiy ta'minot uchun;702\r\n";
        var (import, _) = await ImportAsync(Encoding.UTF8.GetBytes(csv), "v.csv");
        var l1 = import.Lines.Single(l => l.DocNumber == "701");
        var l2 = import.Lines.Single(l => l.DocNumber == "702");
        Assert.Equal(BankMatchStatus.Unmatched, l1.MatchStatus);
        Assert.Contains("tenant'i bor", l1.MatchNote);
        Assert.Equal((BankMatchStatus.Matched, BankMatchMethod.InnAmount, inv.Id), (l2.MatchStatus, l2.MatchMethod, l2.InvoiceId));

        // 2) Xodim l1 ni qo'lda tanlaydi (qisman to'lov) va qo'llaydi.
        await f.WithScopeAsync(async sp => await sp.GetRequiredService<BankImportService>().AssignAsync(l1.Id, inv.Number!.ToLowerInvariant(), await f.EnsureStaffAsync()));
        Assert.True((await ApplyAsync(import, l1.Id)).Single().Ok);
        Assert.Equal(InvoiceStatus.PartiallyPaid, await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Invoices.Where(i => i.Id == inv.Id).Select(i => i.Status).FirstAsync()));
        // l2 endi qoldiqdan katta — qo'llanmaydi, qator «Xato», kalit bo'shatiladi.
        var r2 = (await ApplyAsync(import, l2.Id)).Single();
        Assert.False(r2.Ok);
        Assert.Contains("qoldiq", r2.Message);
        var reloaded = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().BankImportLines.AsNoTracking().FirstAsync(l => l.Id == l2.Id));
        Assert.Equal((BankLineState.Failed, (string?)null), (reloaded.State, reloaded.AppliedKey));

        // 3) Qo'lda belgilangan to'lov (sana + summa + hujjat №) — ko'chirmada takror deb topiladi.
        await f.WithScopeAsync(async sp => await sp.GetRequiredService<BillingService>()
            .RecordPaymentAsync(inv.Id, 100_00, new DateOnly(2026, 10, 18), PaymentMethod.Bank, "777", null, await f.EnsureStaffAsync()));
        var (manual, _) = await ImportAsync(Encoding.UTF8.GetBytes($"Sana;Summa;STIR;Maqsad;Raqam\r\n18.10.2026;100;301234503;{inv.Number};777\r\n"), "m.csv");
        Assert.Equal(BankMatchStatus.Duplicate, manual.Lines.Single().MatchStatus);
    }
}

public class DidoxTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private sealed class FakeDidox : IDidoxClient
    {
        public bool IsConfigured { get; set; } = true;
        public List<JsonObject> Created { get; } = [];
        public int Status { get; set; }
        public bool Fail { get; set; }

        public Task<string> CreateInvoiceDraftAsync(JsonObject payload, CancellationToken ct = default)
        {
            if (Fail) throw new DidoxException("Didox: STIR topilmadi");
            Created.Add(payload);
            return Task.FromResult("DX" + Created.Count);
        }

        public Task<DidoxDocumentState> GetDocumentAsync(string documentId, CancellationToken ct = default) =>
            Task.FromResult(new DidoxDocumentState(Status, DidoxDocumentState.LabelOf(Status)));
    }

    private static readonly DidoxOptions Opts = new() { PartnerToken = "p", Password = "x", Inn = "300000001", CatalogCode = "10305001001000000", PackageCode = "1501", PackageName = "xizmat" };

    private static DidoxService Service(IServiceProvider sp, IDidoxClient client) => new(sp.GetRequiredService<ControlDbContext>(), client,
        sp.GetRequiredService<AuditLogger>(), sp.GetRequiredService<Clock>(), Options.Create(Opts),
        Options.Create(new VendorOptions { Inn = "300000001", LegalName = "Vendor MChJ" }), NullLogger<DidoxService>.Instance);

    [Fact]
    public async Task Send_StoresId_RefreshUpdatesStatus_Disabled_WhenNotConfigured()
    {
        await PortalTestKit.SetPricesAsync(f);
        int staff = await f.EnsureStaffAsync();
        var t = await f.WithScopeAsync(sp => sp.GetRequiredService<TenantService>()
            .CreateAsync(new Tenant { Slug = "dx-a", LegalName = "DX MChJ", Inn = "301111111", Deployment = DeploymentKind.SaaS, CompanyId = 1 }, true));
        var inv = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Biznes);
            var sub = await sp.GetRequiredService<SubscriptionService>().CreateAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), plan.Id, BillingPeriod.Month, null, new DateOnly(2026, 10, 18), false);
            var billing = sp.GetRequiredService<BillingService>();
            var i = await billing.CreateDraftAsync(t.Id, sub.Id, 1, [BillingService.DefaultLine(sub, plan, 1)], null, staff);
            await billing.IssueAsync(i);
            return i;
        });
        var fake = new FakeDidox();

        // Standart (testda sozlanmagan) — o'chiq.
        await f.WithScopeAsync(async sp =>
        {
            var real = sp.GetRequiredService<DidoxService>();
            Assert.False(real.Enabled);
            Assert.Contains("Didox:PartnerToken", real.Missing);
            await Assert.ThrowsAsync<DomainException>(() => real.SendAsync(inv.Id, staff));
        });

        await f.WithScopeAsync(async sp =>
        {
            var svc = Service(sp, fake);
            var sent = await svc.SendAsync(inv.Id, staff);
            Assert.Equal("DX1", sent.DidoxId);
            await Assert.ThrowsAsync<DomainException>(() => svc.SendAsync(inv.Id, staff)); // ikkinchi marta — yo'q
        });
        var payload = Assert.Single(fake.Created);
        Assert.Equal(inv.Number, (string?)payload["FacturaDoc"]!["FacturaNo"]);
        Assert.Equal("301111111", (string?)payload["BuyerTin"]);
        var product = payload["ProductList"]!["Products"]!.AsArray().Single()!;
        decimal total = (decimal)product["DeliverySumWithVat"]!, net = (decimal)product["DeliverySum"]!, vat = (decimal)product["VatSum"]!;
        Assert.Equal(inv.TotalTiyin / 100m, total);
        Assert.Equal(total, net + vat);
        Assert.Equal(12m, (decimal)product["VatRate"]!);

        fake.Status = 2;
        await f.WithScopeAsync(async sp =>
        {
            var svc = Service(sp, fake);
            Assert.Equal(1, await svc.RefreshPendingAsync());
            var i = await sp.GetRequiredService<ControlDbContext>().Invoices.AsNoTracking().FirstAsync(x => x.Id == inv.Id);
            Assert.StartsWith("2 ", i.DidoxStatus);
            Assert.Equal(0, await Service(sp, fake).RefreshPendingAsync()); // yakuniy holat — qayta so'ralmaydi
            Assert.True(await sp.GetRequiredService<ControlDbContext>().AuditLog.AnyAsync(a => a.Action == "didox.send" && a.EntityId == inv.Id.ToString()));
        });
    }

    [Fact]
    public async Task HttpClient_UsesPartnerAuthAndUserKey()
    {
        var calls = new List<(string Method, string Path, string? Partner, string? UserKey, string Body)>();
        var handler = new StubHandler(async req =>
        {
            string body = req.Content is null ? "" : await req.Content.ReadAsStringAsync();
            calls.Add((req.Method.Method, req.RequestUri!.PathAndQuery, req.Headers.TryGetValues("Partner-Authorization", out var p) ? p.Single() : null,
                req.Headers.TryGetValues("user-key", out var u) ? u.Single() : null, body));
            string path = req.RequestUri.AbsolutePath;
            if (path == "/v1/auth/300000001/password/ru") return Json("{\"token\":\"TKN\"}");
            if (path == "/v1/documents/002/create") return Json("{\"_id\":\"ABC123\"}");
            if (path == "/v1/documents/ABC123") return Json("{\"data\":{\"doc_status\":1}}");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var client = new DidoxHttpClient(new HttpClient(handler), Options.Create(Opts), Options.Create(new VendorOptions()), new DidoxTokenCache(), TimeProvider.System);
        Assert.True(client.IsConfigured);
        Assert.Equal("ABC123", await client.CreateInvoiceDraftAsync(new JsonObject { ["Version"] = 1 }));
        var state = await client.GetDocumentAsync("ABC123");
        Assert.Equal(1, state.Code);
        Assert.Equal(3, calls.Count); // token keshlangan — bitta login
        Assert.Contains("\"password\":\"x\"", calls[0].Body);
        Assert.All(calls, c => Assert.Equal("p", c.Partner));
        Assert.Equal("TKN", calls[1].UserKey);
        Assert.Equal("/v1/documents/ABC123?owner=1", calls[2].Path);
        Assert.False(new DidoxHttpClient(new HttpClient(handler), Options.Create(new DidoxOptions()), Options.Create(new VendorOptions()), new DidoxTokenCache(), TimeProvider.System).IsConfigured);
    }

    private static HttpResponseMessage Json(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => f(request);
    }
}

public class OnlineCheckTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    [Fact]
    public async Task Required_RollingKey_RenewedByPull_PolicyInResponses()
    {
        await PortalTestKit.SetPricesAsync(f);
        var tenant = await f.CreateTenantAsync("oc-req", trial: false, deployment: DeploymentKind.OnPrem);
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
            t.OnlineCheck = OnlineCheckMode.Required;
            t.OfflineGraceDays = 10;
            await db.SaveChangesAsync();
        });
        string inst = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), inst, 1, "1.0", null);
        Assert.True(reg.Ok, reg.Error);
        Assert.Equal(("required", (int?)10), (reg.OnlineCheck, reg.OfflineGraceDays));

        await PortalTestKit.PaidAsync(f, tenant.Id, PlanCodes.Biznes, 12, Today);
        License? Current() => f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            return await sp.GetRequiredService<LicenseIssuer>().GetCurrentAsync(await db.Tenants.FirstAsync(t => t.Id == tenant.Id));
        }).GetAwaiter().GetResult();
        var first = Current()!;
        Assert.Equal(Today.AddDays(9), first.ExpiryDate); // 12 oy to'langan, lekin kalit faqat 10 kun

        var pull = await f.ErpClient().PullAsync(inst, reg.TenantSecret, first.LicenseKey);
        Assert.Equal(LicensePanelClient.PullStatus.NotModified, pull.Status);

        // Yarmidan oldin (5 kun) — yangilanmaydi; 6-kuni pull yangi kalit oladi.
        f.Time.Advance(TimeSpan.FromDays(5));
        pull = await f.ErpClient().PullAsync(inst, reg.TenantSecret, first.LicenseKey);
        Assert.Equal(LicensePanelClient.PullStatus.NotModified, pull.Status);
        f.Time.Advance(TimeSpan.FromDays(1));
        pull = await f.ErpClient().PullAsync(inst, reg.TenantSecret, first.LicenseKey);
        Assert.Equal(LicensePanelClient.PullStatus.Updated, pull.Status);
        Assert.Equal(("required", (int?)10), (pull.OnlineCheck, pull.OfflineGraceDays));
        var second = Current()!;
        Assert.Equal(Today.AddDays(6 + 9), second.ExpiryDate);
        Assert.Equal(second.LicenseKey, pull.LicenseKey);

        // Panel ERP'dan uzoq xabar olmasa — bosh sahifada ro'yxatda.
        f.Time.Advance(TimeSpan.FromDays(4));
        var offline = await f.WithScopeAsync(sp => sp.GetRequiredService<DashboardService>().OfflineOnPremAsync());
        Assert.Contains(offline, r => r.TenantId == tenant.Id && r.Required && r.DaysSilent >= 3);
        f.Time.SetUtcNow(PanelFactory.Start);
    }

    [Fact]
    public async Task Optional_KeyCappedAtOneYear_SwitchToRequiredShortens()
    {
        f.Time.SetUtcNow(PanelFactory.Start);
        await PortalTestKit.SetPricesAsync(f);
        var tenant = await f.CreateTenantAsync("oc-opt", trial: false, deployment: DeploymentKind.OnPrem);
        string inst = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), inst, 1, "1.0", null);
        Assert.Equal(("optional", (int?)30), (reg.OnlineCheck, reg.OfflineGraceDays));
        await PortalTestKit.PaidAsync(f, tenant.Id, PlanCodes.Biznes, 18, Today);
        var lic = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var issuer = sp.GetRequiredService<LicenseIssuer>();
            var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
            var optional = await issuer.GetCurrentAsync(t);
            Assert.Equal(Today.AddYears(1).AddDays(-1), optional!.ExpiryDate);
            t.OnlineCheck = OnlineCheckMode.Required; // standart grace = 30
            await db.SaveChangesAsync();
            return await issuer.EnsureForTenantAsync(t);
        });
        Assert.NotNull(lic);
        Assert.Equal(Today.AddDays(29), lic!.ExpiryDate);
    }

    [Fact]
    public void ErpWarningPolicy()
    {
        var now = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        Assert.Null(OnlineCheckPolicy.Warning("optional", 30, now.AddDays(-29), now));
        Assert.Contains("30 kundan", OnlineCheckPolicy.Warning("optional", 30, now.AddDays(-30), now));
        Assert.Contains("kundan beri", OnlineCheckPolicy.Warning(null, null, now.AddDays(-31), now)); // eski panel — standart 30
        Assert.Null(OnlineCheckPolicy.Warning("required", 10, now.AddDays(-2), now));
        Assert.Contains("majburiy", OnlineCheckPolicy.Warning("REQUIRED", 10, now.AddDays(-3), now));
        Assert.Null(OnlineCheckPolicy.Warning("required", 10, null, now));
    }
}

public class PortalChannelTests : IClassFixture<PanelFactory>
{
    private readonly PanelFactory f;
    public PortalChannelTests(PanelFactory factory) { f = factory; f.Telegram.Enabled = true; f.Sms.Enabled = true; f.Email.Enabled = true; }

    [Fact]
    public async Task SmsChannel_SelectedInProfile_CodeGoesToSms()
    {
        var tenant = await f.CreateTenantAsync("ch-a");
        var (user, token) = await PortalTestKit.InviteAsync(f, tenant.Id, "sms@ch.uz", phone: "+998901110001");
        await PortalTestKit.AcceptInviteAsync(f, token, "880001");
        var (c, _) = await PortalTestKit.LoginAsync(f, "sms@ch.uz", "880001");

        var page = PortalTestKit.Html(await c.GetAsync("/Profile"));
        Assert.Contains("SMS (", page);
        Assert.Contains("email (", page);
        var r = await PortalTestKit.PostAsync(c, "/Profile?handler=Channel", new() { ["Channel"] = "sms" }, "/Profile");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        f.Time.Advance(TimeSpan.FromSeconds(61)); // kod qayta yuborish oralig'i
        var (c2, _) = PortalTestKit.Client(f, PortalTestKit.PortalBase);
        r = await PortalTestKit.PostAsync(c2, "/Login", new() { ["Login"] = "sms@ch.uz", ["Password"] = PortalTestKit.Password });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var sms = f.Sms.Sent.Last(m => m.Phone == "+998901110001");
        string code = System.Text.RegularExpressions.Regex.Match(sms.Text, @"kirish kodi (\d{6})").Groups[1].Value;
        Assert.Equal(6, code.Length);
        Assert.Contains("SMS", PortalTestKit.Html(await c2.GetAsync("/Otp")));
        var done = await PortalTestKit.PostAsync(c2, "/Otp", new() { ["Code"] = code });
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.True(await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().PortalOtps.AnyAsync(o => o.PortalUserId == user.Id && o.Channel == "sms")));

        // SMS sozlanmasa — Telegram'ga qaytadi.
        f.Sms.Enabled = false;
        await f.WithScopeAsync(async sp =>
        {
            var u = await sp.GetRequiredService<ControlDbContext>().PortalUsers.Include(x => x.Tenant).FirstAsync(x => x.Id == user.Id);
            Assert.Equal("telegram", sp.GetRequiredService<PortalAuthService>().ResolveTarget(u, u.Tenant!, out _)!.Channel);
        });
        f.Sms.Enabled = true;
    }

    [Fact]
    public async Task ForgotPassword_SameResponse_LinkToPersonalChannel_RateLimited()
    {
        var tenant = await f.CreateTenantAsync("ch-b");
        var (user, token) = await PortalTestKit.InviteAsync(f, tenant.Id, "fg@ch.uz");
        await PortalTestKit.AcceptInviteAsync(f, token, "880002");
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            (await db.PortalUsers.FirstAsync(u => u.Id == user.Id)).OtpChannel = "email";
            await db.SaveChangesAsync();
        });

        var (c, _) = PortalTestKit.Client(f, PortalTestKit.PortalBase);
        Assert.Contains("/Forgot", PortalTestKit.Html(await c.GetAsync("/Login")));
        string Body(HttpResponseMessage r) => System.Text.RegularExpressions.Regex.Match(PortalTestKit.Html(r), "<main>(.*)</main>", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        var known = await PortalTestKit.PostAsync(c, "/Forgot", new() { ["Login"] = "FG@ch.uz" });
        var unknown = await PortalTestKit.PostAsync(c, "/Forgot", new() { ["Login"] = "nobody@ch.uz" });
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(Body(known), Body(unknown)); // hisob borligi oshkor qilinmaydi
        Assert.Contains("Agar shu login bilan", Body(known));

        var mail = f.Email.Sent.Single(m => m.To == "fg@ch.uz");
        string link = System.Text.RegularExpressions.Regex.Match(mail.Body, @"https://\S+/Invite\?token=(\S+)").Groups[1].Value;
        string resetToken = Uri.UnescapeDataString(link);
        Assert.StartsWith("PR-", resetToken);
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            Assert.False(await db.PortalInvites.AnyAsync(i => i.TokenHash == resetToken)); // faqat xesh
            Assert.True(await db.PortalInvites.AnyAsync(i => i.TokenHash == PortalTokens.Hash(resetToken) && i.CreatedByStaffId == null));
        });

        // 2 daqiqa ichida qayta so'rov — jim rad (javob o'sha).
        var again = await PortalTestKit.PostAsync(c, "/Forgot", new() { ["Login"] = "fg@ch.uz" });
        Assert.Equal(Body(known), Body(again));
        Assert.Single(f.Email.Sent.Where(m => m.To == "fg@ch.uz"));

        // Havola → yangi parol → eski parol ishlamaydi.
        const string newPassword = "YangiParol-2026zz";
        var set = await PortalTestKit.PostAsync(c, "/Invite?token=" + Uri.EscapeDataString(resetToken),
            new() { ["Token"] = resetToken, ["Password"] = newPassword, ["Confirm"] = newPassword });
        Assert.Contains("Parol o'rnatildi", PortalTestKit.Html(set));
        await f.WithScopeAsync(async sp =>
        {
            var auth = sp.GetRequiredService<PortalAuthService>();
            Assert.Null((await auth.CheckPasswordAsync("fg@ch.uz", PortalTestKit.Password)).User);
            Assert.NotNull((await auth.CheckPasswordAsync("fg@ch.uz", newPassword)).User);
            Assert.True(await sp.GetRequiredService<ControlDbContext>().AuditLog.AnyAsync(a => a.Action == "portal.reset-request" && a.PortalUserId == user.Id));
        });
    }

    [Fact]
    public async Task ForgotPassword_NoPersonalChannel_NothingSent_GroupChatNotUsed()
    {
        var tenant = await f.WithScopeAsync(sp => sp.GetRequiredService<TenantService>()
            .CreateAsync(new Tenant { Slug = "ch-c", LegalName = "C MChJ", TelegramChatId = "-100777", Deployment = DeploymentKind.SaaS, CompanyId = 1 }, true));
        var (_, token) = await PortalTestKit.InviteAsync(f, tenant.Id, "grp@ch.uz");
        await PortalTestKit.AcceptInviteAsync(f, token, null); // shaxsiy Telegram bog'lanmagan
        f.Email.Enabled = false;
        try
        {
            bool sent = await f.WithScopeAsync(sp => sp.GetRequiredService<PortalAuthService>().RequestPasswordResetAsync("grp@ch.uz", "1.2.3.4", PortalTestKit.PortalBase));
            Assert.False(sent);
            Assert.DoesNotContain(f.Telegram.To("-100777"), m => m.Text.Contains("parolni tiklash"));
        }
        finally { f.Email.Enabled = true; }
    }
}
