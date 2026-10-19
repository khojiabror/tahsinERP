using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ControlPanel.Services;

public class UsageOptions
{
    /// <summary>Sinov va demo tenantlar faolligi shuncha daqiqada bir yig'iladi.</summary>
    public int TrialIntervalMinutes { get; set; } = 15;
    /// <summary>Qolgan (pullik) tenantlar.</summary>
    public int IntervalMinutes { get; set; } = 60;
    /// <summary>Kirish/amal yozuvlari panelda shuncha kun saqlanadi (kunlik ish eskilarini o'chiradi).</summary>
    public int RetentionDays { get; set; } = 90;
    /// <summary>Dashboard: shuncha kun kirmagan sinov tenanti «faol emas».</summary>
    public int InactiveTrialDays { get; set; } = 3;
}

/// <summary>
/// Tenant foydalanuvchi faolligi (05_PROVISIONING.md, 10-bo'lim). Panel tenant bazalariga ULANMAYDI: agent USER_ENTRIES/USERLOGS dan
/// yig'ib /api/agent/usage ga yuboradi. Panelga faqat login, vaqt, modul va maskalangan amal matni keladi — parol, F.I.Sh., email, IP, MAC kelmaydi.
/// </summary>
public sealed class UsageService(ControlDbContext db, Clock clock, ProvisioningService provisioning, IOptions<UsageOptions> options)
{
    private UsageOptions O => options.Value;

    private static bool Collectable(Tenant t) => t.Deployment == DeploymentKind.SaaS && t.ProvisionedAtUtc != null && t.DeletedAtUtc == null
                                                 && t.Status != TenantStatus.Archived && !string.IsNullOrEmpty(t.DbName);

    /// <summary>Shu serverning faolligi yig'ilishi kerak bo'lgan tenantlari (muddati kelgan yoki oldingi yig'ish chala qolgan).</summary>
    public async Task<UsageTargetsResponse> TargetsAsync(Server server, CancellationToken ct = default)
    {
        DateTime now = clock.UtcNow;
        var tenants = await db.Tenants.AsNoTracking().Where(t => t.ServerId == server.Id && t.Deployment == DeploymentKind.SaaS
                && t.ProvisionedAtUtc != null && t.DeletedAtUtc == null && t.Status != TenantStatus.Archived && t.DbName != null)
            .ToListAsync(ct);
        var ids = tenants.Select(t => t.Id).ToList();
        var states = await db.UsageStates.AsNoTracking().Where(s => ids.Contains(s.TenantId)).ToDictionaryAsync(s => s.TenantId, ct);
        var res = new UsageTargetsResponse();
        foreach (var t in tenants)
        {
            states.TryGetValue(t.Id, out var st);
            int interval = t.Status == TenantStatus.Trial || provisioning.IsDemo(t) ? O.TrialIntervalMinutes : O.IntervalMinutes;
            bool due = st?.SyncedAtUtc is null || st.More || now - st.SyncedAtUtc.Value >= TimeSpan.FromMinutes(Math.Max(1, interval));
            if (due)
                res.Targets.Add(new UsageTarget { Slug = t.Slug, DbName = t.DbName!, EntryCursor = st?.EntryCursor ?? 0, LogCursor = st?.LogCursor ?? 0 });
        }
        return res;
    }

    public async Task IngestAsync(Server server, UsageReport r, CancellationToken ct = default)
    {
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == r.Slug, ct) ?? throw new DomainException("Tenant topilmadi.");
        if (t.ServerId != server.Id) throw new DomainException("Tenant bu serverda emas.");
        if (!Collectable(t)) throw new DomainException("Tenant faolligi yig'ilmaydi (arxiv/o'chirilgan/baza yo'q).");
        DateTime now = clock.UtcNow;
        var st = await db.UsageStates.FirstOrDefaultAsync(s => s.TenantId == t.Id, ct);
        if (st is null) { st = new UsageState { TenantId = t.Id }; db.UsageStates.Add(st); }
        st.SyncedAtUtc = now;
        st.Error = r.Error is null ? null : (r.Error.Length > 1000 ? r.Error[..1000] : r.Error);
        if (r.Error != null) { await db.SaveChangesAsync(ct); return; }
        st.EntryCursor = r.EntryCursor;
        st.LogCursor = r.LogCursor;
        st.More = r.More;

        // Foydalanuvchilar ro'yxati — to'liq almashtiriladi (oxirgi faollik saqlanadi)
        var users = await db.UsageUsers.Where(u => u.TenantId == t.Id).ToDictionaryAsync(u => u.UserId, ct);
        var incoming = r.Users.GroupBy(u => u.UserId).Select(g => g.First()).ToList();
        foreach (var u in incoming)
        {
            if (!users.TryGetValue(u.UserId, out var row)) { row = new UsageUser { TenantId = t.Id, UserId = u.UserId }; db.UsageUsers.Add(row); users[u.UserId] = row; }
            row.Login = Trim(u.Login, 50) ?? "";
            row.IsActive = u.IsActive;
        }
        if (r.ResetDetected || incoming.Count > 0)
            foreach (var gone in users.Values.Where(x => incoming.All(i => i.UserId != x.UserId)).ToList())
            {
                db.UsageUsers.Remove(gone);
                users.Remove(gone.UserId);
            }

        // Hodisalar: takrorlanmasin (TenantId, Kind, SourceId, AtUtc), saqlash muddatidan eskilari olinmaydi
        DateTime cutoff = now.AddDays(-O.RetentionDays);
        var events = r.Events.Where(e => e.AtUtc >= cutoff && e.AtUtc <= now.AddDays(1))
            .GroupBy(e => (e.Kind, e.SourceId, e.AtUtc)).Select(g => g.First()).ToList();
        if (events.Count > 0)
        {
            var minAt = events.Min(e => e.AtUtc);
            var sourceIds = events.Select(e => e.SourceId).ToList();
            var existing = (await db.UsageEvents.Where(e => e.TenantId == t.Id && e.AtUtc >= minAt && sourceIds.Contains(e.SourceId))
                    .Select(e => new { e.Kind, e.SourceId, e.AtUtc }).ToListAsync(ct))
                .Select(e => ((int)e.Kind, e.SourceId, e.AtUtc)).ToHashSet();
            foreach (var e in events)
            {
                var kind = (Data.UsageEventKind)e.Kind;
                if (existing.Contains(((int)kind, e.SourceId, e.AtUtc))) continue;
                db.UsageEvents.Add(new UsageEvent
                {
                    TenantId = t.Id, Kind = kind, SourceId = e.SourceId, UserId = e.UserId, AtUtc = e.AtUtc,
                    Module = Trim(e.Module, 50), Action = Trim(e.Action, 300),
                });
                if (users.TryGetValue(e.UserId, out var u))
                {
                    if (kind == Data.UsageEventKind.Login) u.LastLoginUtc = Max(u.LastLoginUtc, e.AtUtc);
                    else u.LastActionUtc = Max(u.LastActionUtc, e.AtUtc);
                }
                if (kind == Data.UsageEventKind.Login) st.LastLoginUtc = Max(st.LastLoginUtc, e.AtUtc);
                else st.LastActionUtc = Max(st.LastActionUtc, e.AtUtc);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Kunlik ish: saqlash muddatidan eski hodisalarni o'chiradi.</summary>
    public Task<int> PurgeAsync(CancellationToken ct = default)
    {
        DateTime cutoff = clock.UtcNow.AddDays(-O.RetentionDays);
        return db.UsageEvents.Where(e => e.AtUtc < cutoff).ExecuteDeleteAsync(ct);
    }

    public sealed record DayRow(DateOnly Date, int Logins, int Actions, int ActiveUsers);
    public sealed record ModuleRow(string Module, int Actions);

    /// <summary>Tenant sahifasi «Faollik»: kunlik grafik (Toshkent sanasi), modullar, foydalanuvchilar.</summary>
    public async Task<(List<DayRow> Days, List<ModuleRow> Modules, List<UsageUser> Users, UsageState? State)> SummaryAsync(int tenantId, int days, CancellationToken ct = default)
    {
        DateTime from = clock.UtcNow.AddDays(-days);
        var raw = await db.UsageEvents.AsNoTracking().Where(e => e.TenantId == tenantId && e.AtUtc >= from)
            .Select(e => new { e.Kind, e.AtUtc, e.UserId, e.Module }).ToListAsync(ct);
        var today = clock.Today;
        var byDay = raw.GroupBy(e => DateOnly.FromDateTime(clock.ToLocal(e.AtUtc))).ToDictionary(g => g.Key, g => g.ToList());
        var list = new List<DayRow>();
        for (int i = days - 1; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            var evs = byDay.GetValueOrDefault(d) ?? [];
            list.Add(new DayRow(d, evs.Count(e => e.Kind == Data.UsageEventKind.Login), evs.Count(e => e.Kind == Data.UsageEventKind.Action),
                evs.Select(e => e.UserId).Distinct().Count()));
        }
        var modules = raw.Where(e => e.Kind == Data.UsageEventKind.Action).GroupBy(e => e.Module ?? "—")
            .Select(g => new ModuleRow(g.Key, g.Count())).OrderByDescending(m => m.Actions).ToList();
        var users = await db.UsageUsers.AsNoTracking().Where(u => u.TenantId == tenantId)
            .OrderByDescending(u => u.LastActionUtc ?? u.LastLoginUtc).ThenBy(u => u.Login).ToListAsync(ct);
        var state = await db.UsageStates.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        return (list, modules, users, state);
    }

    /// <summary>So'nggi hodisalar (filtr: foydalanuvchi, modul, tur, matn).</summary>
    public async Task<List<UsageEvent>> EventsAsync(int tenantId, int? userId, string? module, Data.UsageEventKind? kind, string? q, int take, CancellationToken ct = default)
    {
        var e = db.UsageEvents.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (userId is not null) e = e.Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(module)) e = e.Where(x => x.Module == module);
        if (kind is not null) e = e.Where(x => x.Kind == kind);
        if (!string.IsNullOrWhiteSpace(q)) e = e.Where(x => x.Action != null && x.Action.Contains(q.Trim()));
        return await e.OrderByDescending(x => x.AtUtc).ThenByDescending(x => x.Id).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct);
    }

    public sealed record InactiveTrial(Tenant Tenant, DateTime? LastLoginUtc, int DaysIdle);

    /// <summary>Dashboard: baza yaratilganiga InactiveTrialDays dan ko'p bo'lgan, shuncha kun (yoki umuman) kirmagan sinov tenantlari.</summary>
    public async Task<List<InactiveTrial>> InactiveTrialsAsync(CancellationToken ct = default)
    {
        DateTime now = clock.UtcNow;
        DateTime threshold = now.AddDays(-O.InactiveTrialDays);
        var rows = await db.Tenants.AsNoTracking()
            .Where(t => t.Status == TenantStatus.Trial && t.ProvisionedAtUtc != null && t.ProvisionedAtUtc < threshold && t.DeletedAtUtc == null)
            .Select(t => new { Tenant = t, Last = db.UsageStates.Where(s => s.TenantId == t.Id).Select(s => s.LastLoginUtc).FirstOrDefault() })
            .Where(x => x.Last == null || x.Last < threshold)
            .ToListAsync(ct);
        return rows.Where(x => !provisioning.IsDemo(x.Tenant))
            .Select(x => new InactiveTrial(x.Tenant, x.Last, (int)(now - (x.Last ?? x.Tenant.ProvisionedAtUtc!.Value)).TotalDays))
            .OrderByDescending(x => x.DaysIdle).ToList();
    }

    private static DateTime? Max(DateTime? a, DateTime b) => a is null || b > a ? b : a;
    private static string? Trim(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : (s.Length > max ? s[..max] : s.Trim());
}
