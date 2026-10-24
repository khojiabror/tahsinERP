# tahsinERP Control Panel — reja va arxitektura

Holat: 2026-10-18, asosiy qarorlar qabul qilingan (11-bo'lim); domen va narxlar ochiq. 2026-10-18: Payme/Click integratsiyasi rejadan olib tashlandi — to'lovlar qo'lda belgilanadi. Bog'liq: `Docs/Audit-2026-10/00_PLAN.md`, litsenziya v2 (`Helpers/LicenseHelper.cs`).

## 1. Maqsad

Bitta joydan boshqarish:
- **Tashkilotlar (tenant)** — SaaS (bizning AWS serverda) va on-premise (mijoz serverida).
- **Tariflar va obunalar** — foydalanuvchi soni, modullar, oylik/yillik.
- **To'lovlar** — onlayn to'lov integratsiyasi YO'Q: xodim to'lov tushganini (bank, naqd, karta orqali qo'lda) panelda belgilaydi.
- **Litsenziya kalitlari** — to'lov tushishi bilan avtomatik chiqadi va ERP'ga yetkaziladi.
- **Provisioning** — yangi tashkilot uchun baza, subdomen, admin foydalanuvchi bir tugma bilan.
- **Demo/trial** — 14 kunlik sinov tenanti, demo tenant har kecha qayta tiklanadi.

WinForms generator (`tahsinERP.LicenseGen`) zaxira/favqulodda vosita sifatida qoladi.

## 2. Umumiy arxitektura

```
                        ┌──────────────────────────────────────────┐
  Vendor xodimi ──────► │  admin.tahsinerp.uz   Control Panel      │
  Mijoz admini ───────► │  my.tahsinerp.uz      Mijoz portali      │
                        │  (ASP.NET Core, .NET 10, C#)              │
                        └──────┬──────────────┬─────────────┬──────┘
                               │              │             │
                   tahsin_control DB     AWS KMS        Telegram / SES / Eskiz SMS
                   (tenant, tarif,      (RSA imzo —     (eslatmalar)
                    to'lov, kalit)       private key
                               │          chiqmaydi)
          ┌────────────────────┼─────────────────────────────┐
          │ license pull (6 soatda)                          │ provisioning
          ▼                    ▼                             ▼
  firma1.tahsinerp.uz   firma2.tahsinerp.uz      On-prem ERP (mijoz serveri)
  ERP (IIS, bitta app,  → DB_firma1, DB_firma2   → internet bo'lsa pull,
  host bo'yicha tenant)                            bo'lmasa kalit qo'lda
```

Asosiy qarorlar:
1. **Alohida ilova, ERP'dan mustaqil.** Panel yiqilsa ham ERP'lar ishlayveradi — ular saqlangan imzoli kalit bilan offline ishlaydi.
2. **Til: C# / ASP.NET Core (.NET 10).** Jamoa bitta, kod bitta tilda; ERP keyinchalik Core'ga o'tganda umumiy kutubxonalar bo'ladi.
3. **Kalit formati o'zgarmaydi (v2).** `InstallationId` = tenant'ning o'rnatma GUID'i. ERP kodi deyarli o'zgarmaydi.
4. **Pull modeli.** ERP o'zi panelga murojaat qilib eng yangi kalitni oladi (SaaS ham, internetli on-prem ham bir xil yo'l). Panel ERP serverlariga kira olishi shart emas.
5. **Private key serverda fayl bo'lib turmaydi** — AWS KMS ichida, faqat `Sign` API orqali ishlatiladi.

## 3. Litsenziya oqimi

### 3.1 Kalit juftlari
- `k1` — mavjud juft (WinForms generator, on-prem). Joyida qoladi.
- `k2` — AWS KMS'da yangi RSA_2048 `SIGN_VERIFY` kalit (private qism KMS'dan hech qachon chiqmaydi). Panel shu bilan imzolaydi.
- ERP `LicenseHelper` bir nechta public key'ga ishonadi: `PublicKeyXml[] = { k1, k2 }`, tekshiruvda birortasi mos kelsa yetarli. Kalit formati o'zgarmaydi.
- Imzo: KMS `RSASSA_PKCS1_V1_5_SHA_256` = .NET `SignData(..., SHA256, Pkcs1)` — bir xil natija. Bu moslikni birinchi navbatda test bilan tasdiqlash kerak.
- Narx: KMS kaliti ~$1/oy + so'rovlar (kuniga bir necha o'nlab imzo — amalda $0).

### 3.2 Ro'yxatdan o'tish (bir martalik bog'lash)
1. Panel tenant yaratadi va bir martalik **ro'yxat tokeni** (`reg_token`, 7 kun amal qiladi) chiqaradi.
2. SaaS: provisioning skripti token'ni tenant bazasiga (`LICENSE_STATE.RegToken`) yozadi.
   On-prem: admin ERP'ning License sahifasiga token'ni kiritadi.
3. ERP birinchi ishga tushganda `InstallationId` ni o'zi yaratadi (hozirgi xatti-harakat, HMAC muhri buzilmaydi) va
   `POST /api/license/register {reg_token, installation_id, version}` yuboradi.
4. Panel `installation_id` ni tenant'ga bog'laydi, token'ni yopadi, javobda joriy imzoli kalitni qaytaradi.

> Muhim: provisioning `InstallationId` ni bazaga oldindan YOZMASIN — `LicenseService` buni "muhrsiz InstallationId = qo'lda aralashuv" deb bloklaydi.

### 3.3 Yangilash (pull)
- ERP har 6 soatda (va License sahifasida "Tekshirish" tugmasi bilan): `GET /api/license/{installation_id}` + HMAC imzoli so'rov (tenant sirri ro'yxatdan o'tishda beriladi).
- Javob: eng yangi imzoli kalit yoki `304 Not Modified`. ERP kalitni `ActivateCompanyLicense` orqali saqlaydi — barcha mavjud tekshiruvlar (imzo, InstallationId, muddat) o'z-o'zidan ishlaydi.
- Internet yo'q bo'lsa — saqlangan kalit bilan ishlaydi (offline). On-prem siyosati (4-bosqich, 08_STAGE4.md 4): tenant bo'yicha `Optional` — 30 kun pull bo'lmasa banner, kalit muddati baribir o'zi tugaydi; `Required` — kalit qisqa (standart 30 kun) va panel uni pull orqali yangilab turadi.
- ERP tomonda kod: `Services/LicenseSyncService.cs` (+ `Application_Start` da timer yoki `HostingEnvironment.QueueBackgroundWorkItem`), ~200 qator.

### 3.4 Kalit muddati siyosati
- SaaS: kalit muddati = to'langan davr oxiri **+ 3 kun** (to'lov kechikishi uchun). Har to'lovda yangi kalit.
- On-prem: kalit = shartnoma davri (odatda 1 yil). Qo'shimcha seat sotilsa — yangi kalit, xuddi shu muddat bilan.
- Bekor qilish: SaaS'da yangi qisqa muddatli kalit chiqariladi va keyingi pull'da yetib boradi. On-prem'da offline kalitni qaytarib olib bo'lmaydi — muddat tugashi kutiladi (shuning uchun on-prem uchun 1 yildan uzun kalit chiqarmang).

## 4. Ma'lumotlar modeli (`tahsin_control` DB)

| Jadval | Asosiy ustunlar |
|---|---|
| `Plans` | Id, Code (START/BIZNES/KORP/TRIAL) = Edition, Name, PriceMonthUzs, PriceYearUzs, MaxUsers, Modules (PERMISSIONMODULE ro'yxati), VatRate (12%), IsActive |
| `Tenants` | Id, Slug (subdomen), LegalName, Inn (STIR), ContactName, Phone, Email, TelegramChatId, Deployment (SaaS/OnPrem), Status (Trial/Active/Grace/Suspended/Archived), DbName, ServerId, InstallationId, RegToken (hash), RegTokenExpires, ApiSecret (shifrlangan), CreatedAt |
| `Servers` | Id, Name, Host, Region, SqlInstance, MaxTenants, CurrentTenants — tenantlarni serverlarga taqsimlash uchun |
| `Subscriptions` | Id, TenantId, PlanId, Users, Period (Month/Year), StartDate, EndDate, AutoRenew, Status |
| `Invoices` | Id, Number (TE-2026-000123), TenantId, SubscriptionId, AmountUzs, Status (Draft/Issued/PartiallyPaid/Paid/Cancelled), PaidAmountTiyin, DueDate, Lines (JSON: nomi, miqdor, narx, QQS), DidoxId, DidoxStatus, DidoxSentAtUtc, DidoxCheckedAtUtc, DidoxError (e-hisob-faktura) |
| `BankImports`, `BankImportLines` | Bank ko'chirmasi importi (4-bosqich): fayl, qatorlar, moslash, AppliedKey (noyob — bir tranzaksiya bir marta) — 08_STAGE4.md 2 |
| `Payments` | Id, InvoiceId, Method (Bank/Cash/Card/Other), AmountTiyin, PaidAt, Reference (to'lov topshirig'i raqami), Note, RecordedBy (StaffUserId), RecordedAt, IsReversed, ReversedBy, ReversedAt, ReverseReason |
| `Licenses` | Id, TenantId, InstallationId, KeyId (k1/k2), LicenseKey, MaxUsers, StartDate, ExpiryDate, Edition, IssuedBy (user/auto), IssuedAt, Reason, SupersededById |
| `ProvisioningJobs` | Id, TenantId, Type (Create/Reset/Suspend/Resume/Archive/Delete), Status, Attempts, Lease, Payload, Secrets (shifrlangan), Log, Result, StartedAt, FinishedAt — 05_PROVISIONING.md |
| `Notifications` | Id, TenantId, Channel (Telegram/Email/SMS), Template, SentAt, Status |
| `PortalUsers`, `PortalInvites`, `PortalOtps`, `PortalRequests` | Mijoz portali (3-bosqich): tenant foydalanuvchilari (Owner/Viewer), taklif xeshlari, bir martalik kodlar, tarif so'rovlari — 06_CUSTOMER_PORTAL.md |
| `StaffUsers` | Id, Email, PasswordHash (PBKDF2), Role (Owner/Sales/Support), TotpSecret |
| `AuditLog` | Id, StaffUserId, Action, Entity, EntityId, Before/After (JSON), Ip, At |

Pul summasi bazada **tiyin** (`bigint`) da saqlanadi — yaxlitlash xatosi bo'lmaydi.

## 5. To'lovlar (faqat qo'lda)

Onlayn to'lov tizimlari (Payme, Click va h.k.) bilan integratsiya **qilinmaydi** (2026-10-18 qarori). To'lov qayerdan kelmasin — bank, naqd, karta — panelda xodim qo'lda belgilaydi.

### 5.1 Oqim
1. Panel hisob-faktura (PDF) va shartnoma shablonini chiqaradi (ixtiyoriy keyinroq: Didox orqali ESF).
2. Mijoz bank orqali (yoki boshqa yo'l bilan) to'laydi.
3. Xodim panelda **"To'lov qabul qilindi"** ni belgilaydi: summa, sana, usul, to'lov topshirig'i raqami, izoh.
4. Hisob-faktura summasi to'liq yopilsa → invoice `Paid` → obuna uzaytiriladi → yangi kalit chiqariladi → ERP keyingi pull'da oladi.
5. Qisman to'lov mumkin: invoice `PartiallyPaid` bo'lib turadi, kalit chiqmaydi.

### 5.2 Qoidalar
- Belgilangan to'lovni o'chirib bo'lmaydi — faqat **storno** (sabab bilan, `IsReversed`); storno obunani avtomatik qisqartirmaydi, xodim qaror qiladi.
- Har bir belgilash va storno `AuditLog` ga yoziladi (kim, qachon, oldin/keyin).
- Faqat `Owner` va `Sales` rollari to'lov belgilay oladi; storno — faqat `Owner`.
- Kunlik/oylik hisobot: tushumlar, qarzdor tenantlar, muddati yaqinlashganlar — bank ko'chirmasi bilan qo'lda solishtirish uchun.

### 5.3 Biznes talablar
- To'lovni qabul qiluvchi yuridik shaxs (MChJ, QQS to'lovchi) va bank hisob raqami.
- Ommaviy oferta / SaaS shartnomasi, maxfiylik siyosati.

## 6. Obuna hayot sikli

```
Trial (14 kun) ──to'lov──► Active ──muddat tugadi──► Grace (3 kun, faqat o'qish)
     │                       ▲                             │
     └──muddat tugadi──┐     └──────────to'lov─────────────┤
                       ▼                                   ▼
                   Suspended (kirish yopiq, ma'lumot saqlanadi) ──90 kun──► Archived (DB → S3, serverdan o'chadi)
```
- Eslatmalar: tugashiga 14, 7, 3, 1 kun qolganda va tugagan kuni — Telegram + email (+ SMS Eskiz.uz orqali, ixtiyoriy).
- Grace/blok ERP'da allaqachon bor (`LicenseState.Grace`, `GraceDays = 3`) — panel faqat kalit muddatini boshqaradi.
- Arxivdan tiklash: S3'dan `.bak` → yangi kalit.

## 7. Provisioning (SaaS)

Yangi tenant = **yangi baza, umumiy ilova**:
1. `Servers` dan bo'sh joyi bor serverni tanlash.
2. Shablon bazadan (`DB_TEMPLATE.bak` — toza sxema + spravochniklar + admin roli) `RESTORE DATABASE DB_<slug>`.
3. Tenant admin foydalanuvchisini yaratish (vaqtinchalik parol, birinchi kirishda almashtirish majburiy).
4. `LICENSE_STATE.RegToken` ni yozish (3.2).
5. ERP tenant reestriga `slug → DbName` yozish (ERP `DbConfig` host bo'yicha shu reestrdan o'qiydi).
6. DNS ishi YO'Q: `*.tahsinerp.uz` wildcard A-yozuv + wildcard TLS sertifikat (Let's Encrypt DNS-01, win-acme) + IIS wildcard host binding.
7. Tenant'ga xush kelibsiz xabari (URL, login, vaqtinchalik parol).

Ishlash usuli: panel `ProvisioningJobs` ga yozadi; ERP serverida kichik **agent** (Windows Service yoki rejalashtirilgan PowerShell, `sqlcmd`) navbatni oladi va bajaradi. Panel SQL serverga to'g'ridan-to'g'ri `sysadmin` bilan ulanmaydi.

Demo tenant: `demo.tahsinerp.uz`, har kecha 03:00 da `DB_DEMO_TEMPLATE.bak` dan qayta tiklanadi; login sahifasida demo foydalanuvchi ko'rsatiladi.

## 8. Mijoz portali (`my.<domen>`, 3-bosqich — TAYYOR, 06_CUSTOMER_PORTAL.md)
- O'sha ControlPanel ilovasi, host bo'yicha ajratilgan: xodimlar `admin.<domen>`, mijozlar `my.<domen>`; alohida sxema, cookie,
  siyosat; boshqa zonaning sahifasi 404.
- Tarif va muddat, foydalanuvchi limiti, qolgan kunlar, ogohlantirishlar; hisob-fakturalar (holat, PDF), to'lov rekvizitlari.
- Tarifni oshirish/uzaytirish so'rovi → xodim tasdiqlaydi (qoralama hisob-faktura avtomatik) yoki rad etadi → Telegram xabar.
  Pasaytirish — faqat joriy davr oxiridan.
- Kirish: ERP SSO emas — taklif havolasi → parol (PBKDF2) → **Telegram** orqali bir martalik kod (SMS/email — 4-bosqich,
  `IOtpSender` orqali qo'shiladi).
- On-prem mijoz: o'z InstallationId'lari bo'yicha amaldagi kalitni ko'rish va yuklab olish (faqat Owner).

## 9. AWS deploy

**1-bosqich (arzon, tez):** panelni ERP bilan bitta Windows Lightsail serverga, alohida IIS sayti sifatida (ASP.NET Core Hosting Bundle). Qo'shimcha server narxi: $0.
- `tahsin_control` DB — o'sha SQL Server Express'da (alohida baza, alohida login).
- KMS kaliti (~$1/oy), SSM Parameter Store (bepul tier), S3 backup (~$1–3/oy), SES email (~$0.10 / 1000 xat).

**Keyinroq (ERP Core + Linux'ga o'tgach yoki 2+ ERP server bo'lganda):** panelni alohida Linux Lightsail 1–2 GB ($7–12/oy) ga Docker bilan ko'chirish; DB — o'sha serverda PostgreSQL yoki Lightsail managed DB. Panel ERP serverlaridan mustaqil bo'lgani uchun ko'chirish oson.

Xavfsizlik:
- `admin.` — faqat xodimlar, TOTP 2FA majburiy, ixtiyoriy IP allow-list.
- Har bir pul va kalit amali `AuditLog` ga.
- Kunlik `tahsin_control` backup S3 ga (versioning yoqilgan) — bu bazani yo'qotish = kim qancha to'laganini yo'qotish.

## 10. Bosqichlar va muddatlar

| # | Bosqich | Ish | Muddat |
|---|---|---|---|
| 0 | Tayyorgarlik (ERP) | `DbConfig` host bo'yicha tenant DB; `LicenseHelper` → `netstandard2.0` umumiy kutubxona (`tahsinERP.Licensing`), bir nechta public key; `LicenseSyncService` (register + pull); Edition bo'yicha modul cheklovi | 2–2.5 hafta |
| 1 | Panel MVP | Loyiha skeleti, StaffUsers + 2FA, Plans, Tenants, Subscriptions, Invoices, bank to'lovini qo'lda belgilash, KMS bilan kalit chiqarish, License API (register/pull), Telegram eslatmalar | 2–3 hafta |
| 2 | Provisioning | Shablon baza, agent, wildcard DNS/TLS, demo tenant kechki reset, trial | 1–1.5 hafta |
| 3 | Mijoz portali ✅ | Hisob-fakturalar, rekvizitlar, tarifni oshirish so'rovi, Telegram OTP, on-prem kalit (06_CUSTOMER_PORTAL.md) | 1–2 hafta |
| 4 | Qo'shimcha ✅ | Didox e-hisob-faktura (Didox API haqiqiy hisob bilan tekshirilmagan), bank ko'chirmasi importi (1C/CSV/XLSX), on-prem online tekshiruv siyosati, DISTRIBUTSIYA paketi, portalga SMS/email kod va o'z-o'ziga parol tiklash (08_STAGE4.md); hisobotlar (MRR, churn) — 07_TRACTION.md | 1–2 hafta |

Jami: **~7–10 hafta** bitta dasturchi + AI yordamida. 0–2 bosqichdan keyin birinchi pullik SaaS mijozni ulash mumkin (to'lov qo'lda belgilanadi).

## 11. Qarorlar (2026-10-17)

| # | Savol | Qaror |
|---|---|---|
| 1 | Tarif tuzilmasi | **Qat'iy paketlar.** Foydalanuvchi soni paket chegarasidan oshsa — keyingi paketga o'tiladi (qo'shimcha user sotilmaydi). Korporativdan ortig'i — individual shartnoma. |
| 2 | Modul cheklovi | **Ha, modullar bo'yicha.** Kalitdagi `Edition` = paket kodi; ERP shu bo'yicha modul menyusi va controllerlarni yopadi. |
| 3 | Trial | **14 kun, to'liq** (Korporativ modullari, 5 foydalanuvchigacha). Tugagach → Suspended, ma'lumot 30 kun saqlanadi, keyin arxiv. |
| 4 | Yuridik shaxs | **MChJ, QQS to'lovchi.** Hisob-fakturada QQS 12% alohida qator; narxlar QQS bilan ko'rsatiladi. |
| 5 | On-prem tekshiruv | **Ixtiyoriy.** Internet bo'lsa pull, bo'lmasa offline kalit; on-prem kalit muddati ≤ 1 yil. |
| 6 | Domen | **Boshqa nom** — nomi hali aniqlanmagan (rejadagi `tahsinerp.uz` vaqtinchalik nom). |

### Paketlar

| Paket (`Edition`) | Modullar | Foydalanuvchi | Narx (oy / yil, QQS bilan) |
|---|---|---|---|
| `START` | SRM (xarid + ombor), CRM, Topshiriqlar | 5 | ochiq |
| `BIZNES` | Start + MES, Muhandislik (BOM / Norma / Tannarx), Moliya | 20 | ochiq |
| `KORP` | Biznes + HR (oylik, tabel), Logistika, Rasmiy hujjatlar, Distributsiya, ustuvor qo'llab-quvvatlash | 50 | ochiq |
| `TRIAL` | = KORP, yoki xodim tanlagan modullar (`TRIAL:Srm,Crm`, 05_PROVISIONING.md 11) | 5 | 0, 14 kun |
| `DIST` | Distributsiya: SRM, CRM, Topshiriqlar, Moliya, Distributsiya (MES/BOM yo'q) — asosiy zanjirdan tashqari yon paket (08_STAGE4.md 1) | 20 | ochiq |

Modul cheklovi ERP tomonda (0-bosqichga qo'shilgan, ~1 hafta): `Edition → PERMISSIONMODULE` xaritasi; global filtr paketga kirmagan modul controllerlarini "Paketingizda yo'q — tarifni oshiring" sahifasiga yo'naltiradi, menyu bandlari yashiriladi.

### Hali ochiq
- Domen nomi.
- Paket narxlari va yillik chegirma (masalan yillikda 2 oy bepul).

## 12. Xavflar
- **KMS ↔ .NET imzo mosligi** — 0-bosqichning birinchi testi; mos kelmasa panel imzosini lokal (SSM'dan shifrlangan private key) bilan qilish zaxira varianti.
- **SQL Server Express 1.4 GB bufer chegarasi** barcha tenant bazalari uchun umumiy — ~15–20 faol tenantdan keyin ikkinchi server (`Servers` jadvali shuning uchun boshidanoq bor).
- **ERP multi-tenant'ga o'tish** — `HttpContext.Current` bo'lmagan joylar (fon ishlari, `Task.Run`) tenant'ni yo'qotmasligi kerak; 0-bosqichda tekshiriladi.
