using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using System.Security.Cryptography;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Servers;

public record ServerRow(Server Server, int Tenants);

/// <summary>ERP serverlari (tenantlarni taqsimlash). Faqat Owner (Program.cs: AuthorizeFolder).</summary>
public class IndexModel(ControlDbContext db) : PanelPageModel
{
    public List<ServerRow> Rows { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var counts = await db.Tenants.Where(t => t.ServerId != null && t.Status != TenantStatus.Archived)
            .GroupBy(t => t.ServerId!.Value).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
        Rows = (await db.Servers.OrderBy(s => s.Name).ToListAsync()).Select(s => new ServerRow(s, counts.GetValueOrDefault(s.Id))).ToList();
    }
}

public class EditModel(ControlDbContext db, AuditLogger audit, Clock clock, ISecretProtector protector) : PanelPageModel
{
    /// <summary>Yangi agent siri — faqat shu javobda bir marta ko'rsatiladi (bazada shifrlangan).</summary>
    public string? NewAgentSecret { get; private set; }
    public Server? Existing { get; private set; }

    [BindProperty, Required(ErrorMessage = "Nomi majburiy"), StringLength(100)] public string Name { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Host majburiy"), StringLength(253)] public string Host { get; set; } = "";
    [BindProperty, StringLength(50)] public string? Region { get; set; }
    [BindProperty, Required(ErrorMessage = "SQL instance majburiy"), StringLength(200)] public string SqlInstance { get; set; } = "";
    [BindProperty, Range(1, 1000)] public int MaxTenants { get; set; } = 15;
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty, StringLength(1000)] public string? Notes { get; set; }
    public bool IsNew { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        IsNew = id is null;
        if (id is null) return Page();
        var s = await db.Servers.FindAsync(id);
        if (s is null) return NotFound();
        Existing = s;
        Name = s.Name; Host = s.Host; Region = s.Region; SqlInstance = s.SqlInstance; MaxTenants = s.MaxTenants; IsActive = s.IsActive; Notes = s.Notes;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        IsNew = id is null;
        if (SqlInstance.Contains("password", StringComparison.OrdinalIgnoreCase) || SqlInstance.Contains("pwd=", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(SqlInstance), "Faqat instance nomi — login/parol bu yerda saqlanmaydi.");
        if (await db.Servers.AnyAsync(s => s.Name == Name.Trim() && s.Id != (id ?? 0)))
            ModelState.AddModelError(nameof(Name), "Bu nom band.");
        if (!ModelState.IsValid) return Page();
        Server s;
        object? before = null;
        if (id is null)
        {
            s = new Server { CreatedAtUtc = clock.UtcNow };
            db.Servers.Add(s);
        }
        else
        {
            s = await db.Servers.FindAsync(id) ?? throw new DomainException("Server topilmadi.");
            before = new { s.Name, s.Host, s.Region, s.SqlInstance, s.MaxTenants, s.IsActive };
        }
        s.Name = Name.Trim(); s.Host = Host.Trim(); s.Region = Region?.Trim(); s.SqlInstance = SqlInstance.Trim();
        s.MaxTenants = MaxTenants; s.IsActive = IsActive; s.Notes = Notes;
        await db.SaveChangesAsync();
        audit.Add(id is null ? "server.create" : "server.update", "Server", s.Id, before, new { s.Name, s.Host, s.Region, s.SqlInstance, s.MaxTenants, s.IsActive });
        await db.SaveChangesAsync();
        FlashOk = $"Server «{s.Name}» saqlandi.";
        return RedirectToPage("Index");
    }

    /// <summary>Provisioning agenti uchun yangi HMAC siri. Eskisi darhol bekor bo'ladi (agent sozlamasini yangilang).</summary>
    public async Task<IActionResult> OnPostAgentSecretAsync(int id)
    {
        var s = await db.Servers.FindAsync(id);
        if (s is null) return NotFound();
        string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        bool rotated = s.AgentSecretProtected != null;
        s.AgentSecretProtected = protector.Protect(secret, SecretPurposes.AgentSecret);
        audit.Add("server.agent-secret", "Server", s.Id, null, new { Rotated = rotated });
        await db.SaveChangesAsync();
        IsNew = false;
        Existing = s;
        Name = s.Name; Host = s.Host; Region = s.Region; SqlInstance = s.SqlInstance; MaxTenants = s.MaxTenants; IsActive = s.IsActive; Notes = s.Notes;
        NewAgentSecret = secret;
        ModelState.Clear();
        return Page();
    }
}
