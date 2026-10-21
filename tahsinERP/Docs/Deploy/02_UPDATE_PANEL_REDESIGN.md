# Serverda yangilash: boshqaruv paneli — yangi dizayn (2026-10-21)

**Nima o'zgaradi:** `admin.tahsinerp.uz` va `my.tahsinerp.uz` interfeysi — chap sidebar, ikonlar, qorong'i rejim,
kirish ekranlari "xavfsiz kirish terminali" uslubida. Faqat ko'rinish (Razor sahifalar, `site.css`, `app.js`, `theme.js`).
**Baza migratsiyasi yo'q, sirlar o'zgarmaydi, 2FA o'chiq qoladi** (`ControlPanel__Security__RequireStaffTotp=false`, pool env).
Panel ~10 soniya ochilmaydi.

Serverda repo va .NET SDK yo'q — paket ishlab chiquvchi kompyuterida yasaladi:
`dotnet publish -c Release -r win-x64 --self-contained false` → `_deploy\tahsinERP-panel-redesign.zip`
(`app\` + `Update-Panel.ps1`, skript faqat ASCII — PowerShell 5.1).

## 1. Paketni serverga ko'chirish (RDP)

`tahsinERP-panel-redesign.zip` → serverda masalan `C:\deploy\` → o'ng tugma → **Extract All** → `C:\deploy\panel-redesign\`.

## 2. Yangilash (Administrator PowerShell)

```powershell
cd C:\deploy\panel-redesign
Set-ExecutionPolicy -Scope Process Bypass -Force
.\Update-Panel.ps1
```

Skript: pool to'xtatiladi → joriy versiya zaxirasi `C:\inetpub\_backup\ControlPanel-<sana>` → yangi fayllar →
2FA o'chiq sozlamasi tekshiriladi → pool yoqiladi → `/health` tekshiruvi. Hech narsa so'ramaydi (parol/kod kiritilmaydi).
Kutilgan oxirgi qatorlar: `[ok] pool: Started`, `[ok] health: ok`.

## 3. Tekshiruv

| # | Qayerda | Kutilgan natija |
|---|---------|-----------------|
| 1 | `https://admin.tahsinerp.uz/Account/Login` (`Ctrl+F5`) | qorong'i "XAVFSIZ KIRISH" ekrani, radar, o'ng yuqorida UTC soat yuradi |
| 2 | Email + parol bilan kirish | 2FA kodi **so'ralmaydi**; qadamlar "01 PAROL → 02 KIRISH" |
| 3 | Bosh sahifa | chap sidebar (Asosiy, Moliya, Texnik, Tahlil, Boshqaruv), KPI plitkalari ikonlar bilan |
| 4 | Tashkilot sahifasi | "Telegram bot va sayt-katalog" bo'limi joyida, tugmalar ishlaydi |
| 5 | O'ng yuqoridagi oy/quyosh tugmasi | yorug' ↔ qorong'i, yangilanganda saqlanadi |
| 6 | Telefonda | ☰ tugmasi menyuni ochadi, fon bosilsa yopiladi |
| 7 | `https://my.tahsinerp.uz/Login` | "MIJOZ KABINETI · XAVFSIZ KIRISH" ekrani |

## 4. Muammo bo'lsa — orqaga qaytarish

```powershell
cd C:\deploy\panel-redesign
.\Update-Panel.ps1 -Rollback
```

Eng oxirgi zaxirani tiklaydi (baza o'zgarmagani uchun fayllarni qaytarish yetarli).
