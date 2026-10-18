using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Staff;

// Butun papka faqat Owner (Program.cs: AuthorizeFolder("/Staff", Owner)).

public class IndexModel(ControlDbContext db) : PanelPageModel
{
    public List<StaffUser> Rows { get; private set; } = [];
    public async Task OnGetAsync() => Rows = await db.StaffUsers.OrderBy(s => s.Email).ToListAsync();
}

public class CreateModel(StaffAuthService auth) : PanelPageModel
{
    [BindProperty, Required(ErrorMessage = "Email majburiy"), EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Ism majburiy")] public string FullName { get; set; } = "";
    [BindProperty] public StaffRole Role { get; set; } = StaffRole.Sales;
    [BindProperty, Required(ErrorMessage = "Vaqtinchalik parol"), DataType(DataType.Password)] public string Password { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        if (!await TryAsync(() => auth.CreateStaffAsync(Email, FullName, Role, Password, StaffId))) return Page();
        FlashOk = $"Xodim qo'shildi: {Email}. Parolni xavfsiz kanal orqali bering; birinchi kirishda 2FA ulanadi.";
        return RedirectToPage("Index");
    }
}

public class EditModel(ControlDbContext db, AuditLogger audit) : PanelPageModel
{
    public StaffUser Staff { get; private set; } = null!;
    [BindProperty] public StaffRole Role { get; set; }
    [BindProperty] public bool IsActive { get; set; }
    [BindProperty, DataType(DataType.Password)] public string? NewPassword { get; set; }

    private async Task<bool> LoadAsync(int id)
    {
        var s = await db.StaffUsers.FindAsync(id);
        if (s is null) return false;
        Staff = s;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Role = Staff.Role; IsActive = Staff.IsActive;
        return Page();
    }

    private async Task<bool> GuardLastOwnerAsync(bool removingOwner)
    {
        if (!removingOwner || Staff.Role != StaffRole.Owner) return true;
        bool other = await db.StaffUsers.AnyAsync(u => u.Id != Staff.Id && u.Role == StaffRole.Owner && u.IsActive);
        if (!other) ModelState.AddModelError("", "Oxirgi faol Owner'ni o'chirib/pasaytirib bo'lmaydi.");
        return other;
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!await GuardLastOwnerAsync(Role != StaffRole.Owner || !IsActive)) return Page();
        var before = new { Staff.Role, Staff.IsActive };
        if (Staff.Role != Role || Staff.IsActive != IsActive)
        {
            Staff.Role = Role; Staff.IsActive = IsActive;
            Staff.SecurityStamp = Guid.NewGuid().ToString("N"); // ochiq sessiyalar yopiladi
            audit.Add("staff.update", "StaffUser", Staff.Id, before, new { Staff.Role, Staff.IsActive });
            await db.SaveChangesAsync();
        }
        FlashOk = "Saqlandi.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostResetTotpAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Staff.TotpSecretProtected = null;
        Staff.TotpLastStep = 0;
        Staff.SecurityStamp = Guid.NewGuid().ToString("N");
        audit.Add("staff.totp-reset", "StaffUser", Staff.Id);
        await db.SaveChangesAsync();
        FlashOk = "2FA tiklandi — xodim keyingi kirishda yangi QR kodni skanerlaydi.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPasswordAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (PasswordHasher.ValidatePolicy(NewPassword) is string err) { ModelState.AddModelError(nameof(NewPassword), err); return Page(); }
        Staff.PasswordHash = PasswordHasher.Hash(NewPassword!);
        Staff.SecurityStamp = Guid.NewGuid().ToString("N");
        Staff.FailedLogins = 0; Staff.LockedUntilUtc = null;
        audit.Add("staff.password-reset", "StaffUser", Staff.Id);
        await db.SaveChangesAsync();
        FlashOk = "Parol almashtirildi, blok olib tashlandi.";
        return RedirectToPage(new { id });
    }
}
