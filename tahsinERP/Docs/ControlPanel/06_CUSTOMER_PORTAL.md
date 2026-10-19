# Boshqaruv paneli — 3-bosqich (Mijoz portali)

Holat: 2026-10-19. Loyiha: `tahsinERP.ControlPanel` (alohida loyiha EMAS — o'sha ilova, `my.<domen>` host'i).
Testlar: `tahsinERP.ControlPanel.Tests` — `PortalTests.cs` (19 ta yangi; jami 84). Baza: `tahsin_control`, EF migratsiya
`20261019064135_CustomerPortal`. ERP bazasiga va ERP loyihasiga tegilmagan. Bog'liq: `00_PLAN.md` (5, 8, 10, 11), `04_PANEL_MVP.md`.

## 1. Joylashuv va izolyatsiya

| | Xodimlar paneli | Mijoz portali |
|---|---|---|
| Host | `admin.<domen>` (`Hosts:AdminHost`) | `my.<domen>` (`Hosts:PortalHost`) |
| Sahifalar | `Pages/*` (Portal'dan tashqari) | `Pages/Portal/*` — URL'da `Portal/` yo'q (`my.<domen>/Invoices`) |
| Authentication scheme | `Staff` + `TwoFactor` | `Portal` + `PortalTwoFactor` |
| Cookie | `tcp.auth`, `tcp.2fa` (SameSite=Strict) | `__Host-tcp.my`, `__Host-tcp.my2fa` (dev: `tcp.my`) |
| Cookie domeni / path | Domain berilmaydi (faqat shu host), `/` | Domain berilmaydi (faqat `my.*`), `/`; `__Host-` prefiksi buni brauzerda majburlaydi |
| Avtorizatsiya | `FallbackPolicy` (Staff), `Sales`, `Owner` | `Portal` siyosati (faqat `Portal` sxemasi + `portal_tenant` claim) |
| Foydalanuvchilar | `StaffUsers` | `PortalUsers` (tenant'ga bog'liq) |

Qanday ishlaydi:
- `ZonePageRouteConvention` har sahifa endpoint'iga zona belgisini (`ZoneMetadata`) qo'yadi; `ZoneMatcherPolicy` so'rov host'i
  zonasiga mos kelmagan endpoint'larni nomzodlikdan chiqaradi. Bir xil yo'l (`/Invoices`, `/Requests`, `/`) ikki zonada
  ziddiyatsiz ishlaydi; boshqa zonaning sahifasi **404** (login'ga yo'naltirilmaydi — sahifa borligi oshkor bo'lmaydi).
- Standart sxema — `Zone` policy scheme: `HttpContext.User` host'ga qarab `Staff` yoki `Portal` cookie'sidan olinadi.
  Siyosatlar o'z sxemasini aniq ko'rsatadi, shuning uchun mijoz cookie'si admin host'ga ataylab yuborilsa ham xodim
  sahifasi ochilmaydi va aksincha (`PortalHostIsolationTests` buni cookie'ni boshqa host'ga qo'lda yuborib isbotlaydi).
- `Hosts:OtherHosts`: boshqa host (`localhost`, IP) qaysi zonaga tushadi. Lokal — `Admin` (eski havolalar ishlaydi),
  serverda — `None` (faqat API va `/health`). `Install-PanelIis.ps1` serverda `None` qo'yadi.
- Zonasiz: `/api/license/*`, `/api/agent/*`, `/api/telegram/webhook`, `/health`, statik fayllar, `/Error`.

## 2. Kirish

1. **Taklif** (xodim, tenant sahifasi → «Mijoz portali»): ism, email va/yoki telefon, rol (`Owner` — so'rov, kalit;
   `Viewer` — faqat ko'rish). Bir martalik havola `https://my.<domen>/Invite?token=PI-…` faqat shu javobda ko'rsatiladi
   (bazada SHA-256 xeshi), 72 soat (`Portal:InviteHours`). Yangi havola eskisini yopadi.
2. **Parol o'rnatish**: PBKDF2-SHA256 600k (xodimlar bilan bir xil siyosat: ≥12 belgi, harf+raqam). Havola yopiladi,
   SecurityStamp almashadi (eski sessiyalar bekor), blok olib tashlanadi.
3. **Telegram bog'lash**: foydalanuvchining shaxsiy chati yo'q bo'lsa, parol sahifasida `t.me/<bot>?start=L…` havolasi
   (24 soat, bir martalik, xeshi saqlanadi). Bot webhook'i `/start L…` ni olib chatni bog'laydi. Keyin Profil sahifasidan
   boshqa hisobga o'tkazish mumkin. Faqat parol bilan Telegram bog'lab bo'lmaydi (aks holda parolni bilgan kishi 2-omilni
   o'ziga olardi) — bog'lash faqat taklif havolasi egasi yoki to'liq kirgan foydalanuvchi uchun.
4. **Kirish**: email yoki telefon (`+998…`, `90 123 45 67` ham bo'ladi) + parol → 6 xonali kod Telegram'ga (shaxsiy chat,
   bo'lmasa `Tenant.TelegramChatId`) → kod. Kod 5 daqiqa, bazada tuz+SHA-256, faqat oxirgisi amal qiladi, 5 urinish.
5. **Cheklovlar**: 5 xato (parol yoki kod) → 15 daqiqa blok; kod qayta yuborish ≥60 s oralig'ida va 15 daqiqada ≤5 ta;
   IP bo'yicha `login` rate limit (daqiqasiga 20, `Panel:LoginRateLimitPerMinute`); noma'lum login — umumiy xato
   (login borligi oshkor bo'lmaydi, xesh baribir hisoblanadi).
6. **Parolni tiklash**: o'z-o'ziga xizmat YO'Q — xodim yangi taklif (parolni tiklash) havolasini chiqaradi; shaxsiy
   Telegram bog'langan bo'lsa havola o'sha chatga ham yuboriladi (tenant guruh chatiga — yo'q).
7. **Xodim boshqaruvi**: rol almashtirish, o'chirish/yoqish, Telegram'ni uzish — har biri foydalanuvchi sessiyalarini yopadi.
   Arxivlangan/o'chirilgan tenant mijozi kira olmaydi; to'xtatilgan (Suspended) — kira oladi (to'lash uchun).

**SMS/email — 4-bosqich.** Kod kanali `IOtpSender` interfeysi orqali: yangi kanal (masalan Eskiz SMS) shu interfeysni
amalga oshirib `services.AddScoped<IOtpSender, ...>()` bilan ro'yxatdan o'tadi; `PortalAuthService` ro'yxatdagi
birinchi mos kanalni tanlaydi (`Resolve` null qaytarsa — keyingisi). `PortalOtp.Channel` qaysi kanal ishlatilganini saqlaydi.

## 3. Sahifalar (`my.<domen>`)

| Yo'l | Nima |
|---|---|
| `/` | Paket, holat, amal qilish muddati, qolgan kunlar, foydalanuvchilar limiti (ERP'dagi haqiqiy son panelga kelmaydi — faqat limit), keyingi to'langan davr, ogohlantirishlar: Grace, Suspended, ≤14 kun qoldi, muddati o'tgan hisob-faktura |
| `/Invoices` | Faqat chiqarilgan (raqamli) hisob-fakturalar; holat: To'langan / Qisman to'langan / To'lanmagan / Bekor; PDF (panelning `InvoicePdfRenderer`i) |
| `/Payment` | Rekvizitlar (`ControlPanel:Vendor:*`), to'lov maqsadiga hisob-faktura raqamini yozish ko'rsatmasi, har to'lanmagan hisob-faktura uchun tayyor «to'lov maqsadi» matni (nusxa olish). Onlayn to'lov YO'Q |
| `/Requests` | Tarif so'rovi (faqat Owner) va tarix; o'z ochiq so'rovini bekor qilish |
| `/Licenses` | Faqat On-prem tenant: har InstallationId bo'yicha amaldagi kalit; «Kalitni ko'rsatish» (nusxa olish) va `.txt` yuklab olish — faqat Owner. SaaS tenant'da 404 |
| `/Profile` | Parolni almashtirish, Telegram bog'lash |
| `/Login`, `/Otp`, `/Invite`, `/Logout` | Kirish oqimi |

UI o'zbekcha (lotin), mobil ekranga moslashgan (375 px'da tekshirildi). CSP o'zgarmagan: inline JS/CSS yo'q (`wwwroot/js/portal.js`).

## 4. Tarifni oshirish / uzaytirish so'rovi

| Tur | Qachon | Tasdiqlanganda |
|---|---|---|
| `Renew` | Joriy pullik obuna, o'sha paket va davr | Joriy obunaga qoralama hisob-faktura (to'langach oxiridan uzayadi) |
| `Upgrade` | Yuqoriroq paket (`Plan.SortOrder`) | Yangi Pending obuna **bugundan** + qoralama; to'langach eskisi yopiladi, yangi kalit darhol |
| `ChangeAtPeriodEnd` | Pastroq paket yoki davr (oy↔yil) o'zgarishi | Yangi Pending obuna **joriy davr oxiri + 1** dan; to'langach joriy obuna o'z muddatigacha ishlaydi |
| `New` | Pullik obuna yo'q (sinov, to'xtatilgan) | Yangi Pending obuna bugundan |

- Bir tenant'da bitta ochiq so'rov; keyingi davr oldindan to'langan bo'lsa yangi so'rov qabul qilinmaydi.
- Tur tasdiqlash paytida qayta hisoblanadi (orada to'lov bo'lgan bo'lishi mumkin). Tenant'da boshqa to'lanmagan obuna
  bo'lsa — xato (xodim avval uni bekor qiladi); o'sha paket/davrdagi Pending obuna bo'lsa — o'shanga hisob-faktura.
- Narx — `Plans` dan (`BillingService.DefaultLine`); narx belgilanmagan bo'lsa tasdiqlash xato beradi va hech narsa saqlanmaydi
  (bitta tranzaksiya). Joriy davr qoldig'i avtomatik hisoblanmaydi — xodim qoralamani tahrirlab kelishilgan summani qo'yadi.
- Qoralama mijozga ko'rinmaydi; xodim tekshirib «Chiqarish» bosgach portalda paydo bo'ladi. To'lov — har doimgidek qo'lda.
- Xodim navbati: `admin.<domen>/Requests` (menyuda «So'rovlar (N)»); tasdiqlash/rad etish — Owner va Sales; rad etish sababi
  majburiy. Mijozga (shaxsiy chatlar + tenant chati) Telegram xabar; yangi so'rov — xodimlar chatiga (`Telegram:StaffChatId`).
  Hammasi `Notifications` jadvalida (takrorlanmaydi; Telegram sozlanmagan bo'lsa — faqat log).

Panel o'zgarishlari (minimal, 2-bosqich kodiga tegmasdan):
- `SubscriptionService.ApplyPaidInvoiceAsync`: kelajakdan boshlanadigan obuna to'langanda boshqa faol obunalar yopilmaydi.
- `LicenseIssuer.EnsureForTenantAsync`: faqat boshlangan (`StartDate ≤ bugun`) obuna kalitga asos bo'ladi.
- Kunlik ish: `EnsureKeysForStartedPeriodsAsync` — oxirgi 3 kunda boshlangan to'langan davrlar uchun kalitni moslaydi
  (pasaytirishdan keyin START kaliti yangi davr birinchi kuni chiqadi; SaaS'da ERP keyingi pull'da oladi).
- Signing/k2 va Provisioning kodiga tegilmagan.

## 5. Audit

`AuditLog.PortalUserId` (yangi ustun) — mijoz amallari. Yoziladiganlar: `portal.user.create/update`, `portal.invite`,
`portal.reset-invite`, `portal.password-set/reset/change`, `portal.telegram-link(-code)/unlink`, `portal.otp-send`,
`portal.login`, `portal.logout`, `portal.lockout`, `portal.invoice.pdf`, `portal.license.view`, `portal.license.download`,
`portal.request.create/cancel` (mijoz), `portal.request.approve/reject` (xodim). Audit sahifasida «mijoz #id».

## 6. Sozlamalar

| Kalit | Ma'nosi |
|---|---|
| `ControlPanel:Hosts:Domain` | Asosiy domen (hali tanlanmagan). Lokal: `localhost` → `admin.localhost`, `my.localhost` |
| `Hosts:AdminHost` / `Hosts:PortalHost` | Aniq host nomlari (bo'sh — `admin.`/`my.` + Domain) |
| `Hosts:OtherHosts` | `Admin` (lokal) yoki `None` (server) |
| `Hosts:PortalPublicUrl` | Taklif havolasi bazasi (`https://my.<domen>`); bo'sh — joriy so'rov sxemasi/porti |
| `Portal:InviteHours` / `OtpMinutes` / `TelegramLinkHours` / `SessionHours` | 72 / 5 / 24 / 4 |
| `Portal:OtpLogFallback` | Faqat lokal (`appsettings.Development.json`da `true`): bot yo'q bo'lsa kod server log'iga. **Serverda yoqmang** |
| `Telegram:BotUsername` | `t.me/<bot>?start=` havolasi uchun (bo'sh — `/start <kod>` matni ko'rsatiladi) |
| `Telegram:WebhookSecret` | `setWebhook` dagi `secret_token`; webhook shu sarlavhasiz 404. Faqat env/user-secrets |

Bot webhook'ini ulash (bir marta, serverda; `<TOKEN>` va `<SECRET>` — env'dagi qiymatlar):

```bash
curl -s "https://api.telegram.org/bot<TOKEN>/setWebhook" -d "url=https://my.<domen>/api/telegram/webhook" -d "secret_token=<SECRET>" -d "allowed_updates=[\"message\"]"
```

Botga `/start` (kodsiz) yozilsa — chat ID'sini qaytaradi: xodim uni tenant'ning «Telegram chat» maydoniga yozishi mumkin.

## 7. Lokal ishga tushirish

`04_PANEL_MVP.md` 2-bo'limidagi kabi (user-secrets, `migrate`, `create-owner`), keyin `dotnet run`:

- Xodimlar: `https://admin.localhost:7243` (yoki eskicha `https://localhost:7243`).
- Mijozlar: `https://my.localhost:7243` — Chrome/Edge `*.localhost` ni o'zi 127.0.0.1 ga yo'naltiradi (hosts fayli shart emas;
  boshqa brauzerda ishlamasa — hosts fayliga `127.0.0.1 admin.localhost my.localhost`). Path-prefiks rejimi yo'q — ikki host bilan sinang.
- Tenant sahifasi → «Mijoz portali» → foydalanuvchi qo'shish → havolani ochish → parol → Telegram bot sozlanmagan bo'lsa
  kod konsol log'ida (`PORTAL OTP (OtpLogFallback...)`).

Testlar (lokal SQL'da vaqtinchalik baza):

```bash
dotnet test tahsinERP.ControlPanel.Tests
```

Serverga: `Install-PanelIis.ps1 -HostName admin.<domen> -PortalHostName my.<domen> -CertThumbprint <wildcard>` — ikkinchi
https binding, `Hosts:*` env va webhook sirini so'raydi. DNS: `my.<domen>` → panel serveri (wildcard bo'lsa avtomatik).

## 8. Testlar nimani isbotlaydi

| Test | Nima |
|---|---|
| `PortalHostIsolationTests` | Xodim sahifalari my.* da 404, portal sahifalari admin.* va localhost'da 404; cookie nomi/flaglari; mijoz cookie'si admin host'ga, xodim cookie'si portalga qo'lda yuborilganda ham ochilmaydi; `OtherHosts=None`; webhook sirsiz 404 |
| `PortalLoginTests` | Taklif (faqat xesh) → zaif parol rad → parol → Telegram bog'lash (webhook) → bir martalik havola → parol + kod (noto'g'ri kod rad, ishlatilgan kod qayta ishlamaydi) → chiqish; telefon bilan kirish va tenant chati; kanal yo'q — rad; xodim UI orqali foydalanuvchi qo'shish, havola bir marta, rol o'zgarsa sessiya yopiladi |
| `PortalLockoutTests` | 5 xato → blok, umumiy xato, kod qayta yuborish chastotasi, eski kod bekor, tiklash havolasi Telegram'ga va eski sessiyani yopadi, muddati o'tgan havola, o'chirilgan foydalanuvchi |
| `PortalRequestTests` | So'rov → xodim navbati (UI) → tasdiqlash → qoralama (narx × davr) → Telegram → qoralama mijozga ko'rinmaydi → chiqarish + to'lov → yangi paket faol, eskisi yopiq → portalda «To'langan» + PDF + audit; rad etish sababi; bekor qilish; Viewer cheklovi; boshqa tenant hisob-fakturasi 404 |
| `PortalDowngradeTests` | Pasaytirish faqat davr oxirida: to'langach joriy KORP davom etadi, kalit o'zgarmaydi; yangi davr kuni kunlik ish START kalitini chiqaradi (idempotent) |
| `PortalOnPremTests` | On-prem Owner kalitni ko'radi/yuklab oladi (ERP register qilgan haqiqiy kalit), Viewer — yo'q, SaaS — 404, audit |
| `PortalUnitTests` | Telefon normalizatsiyasi, tokenlar, so'rov turini aniqlash |

## 9. Tekshirilmagan / ochiq

- Haqiqiy Telegram bot bilan (webhook, `setWebhook`, deep-link) — faqat soxta `ITelegramSender` bilan sinalgan.
- Haqiqiy IIS'da ikki host binding va `__Host-` cookie — skript sintaksisi tekshirilgan, serverda ishga tushirilmagan.
- ERP'dagi haqiqiy foydalanuvchilar soni panelga kelmaydi (License API'da yo'q) — portal faqat limitni ko'rsatadi.
  Kerak bo'lsa: pull so'roviga `active_users` maydoni (01_LICENSE_API.md o'zgarishi, ERP commit).
- Pasaytirishda joriy davr qoldig'i / oshirishda proratsiya avtomatik emas — xodim qoralamani tahrirlaydi.
- Mijoz o'zi parolni tiklay olmaydi (email/SMS kanali 4-bosqichda) — xodim yangi havola beradi.
- Portal foydalanuvchilarini o'chirish (DELETE) yo'q — faqat faolsizlantirish (audit izi saqlanadi).
