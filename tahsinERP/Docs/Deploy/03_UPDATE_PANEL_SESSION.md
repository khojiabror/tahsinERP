# Serverda yangilash: panel seansi 15 daqiqa, tab nomi va ikonka (2026-10-22)

**Nima o'zgaradi (faqat xodim paneli `admin.tahsinerp.uz`):**
- 15 daqiqa harakatsizlikdan keyin avtomatik chiqish. Server cookie'si har so'rovda yangilanadi va 15 daqiqada tugaydi;
  brauzerda 1 daqiqa oldin "Seans tugayapti" ogohlantirishi, so'ng kirish sahifasi "Seans 15 daqiqa harakatsizlik sababli yopildi".
  Sichqoncha/klaviatura harakati faol deb hisoblanadi (seans fonda `/Account/KeepAlive` bilan uzaytiriladi); bir nechta tab bir-birini hisobga oladi.
- Muddatni o'zgartirish (ixtiyoriy): pool env `ControlPanel__Security__StaffIdleMinutes` (5–480, standart 15).
- Tab nomi: `<Sahifa> · tahsinERP Admin` (portalda `· tahsinERP Kabinet`), ikonka `favicon.ico` / `favicon.svg`.

Baza migratsiyasi yo'q, sirlar o'zgarmaydi, 2FA o'chiq qoladi.

## Yangilash

Paket: `tahsinERP-panel-session.zip` (`app\` + `Update-Panel.ps1`). RDP'da `C:\deploy\panel-session\` ga oching, Administrator PowerShell:

```powershell
cd C:\deploy\panel-session
Set-ExecutionPolicy -Scope Process Bypass -Force
.\Update-Panel.ps1
```

Kutilgan: `[ok] pool: Started`, `[ok] health: ok`. Orqaga qaytarish: `.\Update-Panel.ps1 -Rollback`.

## Tekshiruv

| # | Qadam | Kutilgan natija |
|---|-------|-----------------|
| 1 | `https://admin.tahsinerp.uz` (`Ctrl+F5`) | tabda ko'k qalqon ikonkasi, nomi "Kirish · tahsinERP Admin" |
| 2 | Kirib, 14 daqiqa hech narsa bosmang | o'ng pastda "Seans tugayapti … soniyadan keyin chiqasiz" |
| 3 | "Davom etish" | ogohlantirish yo'qoladi, seans davom etadi |
| 4 | Yana 15 daqiqa harakatsiz | kirish sahifasi, "Seans 15 daqiqa harakatsizlik sababli yopildi" |
