# Serverda yangilash: boshqaruv paneli — yangi dizayn (2026-10-21)

**Nima o'zgaradi:** `admin.tahsinerp.uz` va `my.tahsinerp.uz` interfeysi — chap sidebar, ikonlar, qorong'i rejim,
kirish ekranlari "xavfsiz kirish terminali" uslubida. Faqat ko'rinish (Razor sahifalar, `site.css`, `app.js`, `theme.js`).
**Baza migratsiyasi yo'q, sozlama/sir o'zgarmaydi.** Yangilash ~2–3 daqiqa, panel shu vaqt ichida ochilmaydi.

Bajaruvchi: serverda (RDP), **Administrator** PowerShell oynasida.

## 1. Kodni olish

```powershell
cd C:\src\tahsinERP          # serverdagi repo papkasi (boshqa bo'lsa — o'shani yozing)
git fetch origin
git checkout main
git pull --ff-only origin main
git log --oneline -3         # tepada "Panel: yangi dizayn" commit'i bo'lishi kerak
```

## 2. Zaxira (orqaga qaytish uchun)

```powershell
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
Copy-Item C:\inetpub\tahsinERP.ControlPanel "C:\inetpub\_backup\ControlPanel-$stamp" -Recurse
```

## 3. Yangilash (sirlarga tegilmaydi)

```powershell
.\tahsinERP.ControlPanel\deploy\Install-PanelIis.ps1 -HostName admin.tahsinerp.uz -PortalHostName my.tahsinerp.uz `
  -ConnectionString "Server=.\SQLEXPRESS;Database=tahsin_control;Trusted_Connection=True;TrustServerCertificate=True;Application Name=tahsinERP.ControlPanel" `
  -CertThumbprint <*.tahsinerp.uz sertifikat thumbprint> -KeepSecrets
```

Skript pool'ni to'xtatadi → `dotnet publish` → pool'ni qayta yoqadi → `migrate` (o'zgarish yo'q, "Baza yangilandi" chiqadi).
Thumbprint: `Get-ChildItem Cert:\LocalMachine\WebHosting | ? Subject -like '*tahsinerp.uz*' | select Thumbprint, NotAfter`.

## 4. Tekshiruv

| # | Qayerda | Kutilgan natija |
|---|---------|-----------------|
| 1 | `https://admin.tahsinerp.uz/health` | `ok` |
| 2 | `https://admin.tahsinerp.uz/Account/Login` | qorong'i "XAVFSIZ KIRISH" ekrani, radar, o'ng yuqorida UTC soat yuradi |
| 3 | Email + parol bilan kirish | 2FA kodi **so'ralmaydi** (serverda `ControlPanel__Security__RequireStaffTotp=false`); qadamlar "01 PAROL → 02 KIRISH", TOTP haqida yozuv yo'q |
| 4 | Bosh sahifa | chap sidebar (bo'limlar: Asosiy, Moliya, Texnik, Tahlil, Boshqaruv), KPI plitkalari ikonlar bilan |
| 5 | O'ng yuqoridagi oy/quyosh tugmasi | yorug' ↔ qorong'i almashadi, sahifa yangilanganda saqlanadi |
| 6 | Telefonda (yoki brauzer oynasini toraytirib) | ☰ tugmasi sidebar'ni ochadi, fon bosilsa yopiladi |
| 7 | `https://my.tahsinerp.uz/Login` | "MIJOZ KABINETI · XAVFSIZ KIRISH" ekrani |
| 8 | Brauzer konsoli (F12) | CSP xatolari yo'q |

Eski ko'rinish chiqsa — `Ctrl+F5` (CSS/JS fayllar `?v=` xesh bilan, odatda kerak emas).

## 5. Muammo bo'lsa — orqaga qaytarish

```powershell
Stop-WebAppPool tahsinERP.ControlPanel
Remove-Item C:\inetpub\tahsinERP.ControlPanel -Recurse -Force
Copy-Item "C:\inetpub\_backup\ControlPanel-$stamp" C:\inetpub\tahsinERP.ControlPanel -Recurse
Start-WebAppPool tahsinERP.ControlPanel
```

(Baza o'zgarmagani uchun faqat fayllarni qaytarish yetarli.)
