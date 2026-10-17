# tahsinERP Control Panel — reja va arxitektura

Holat: 2026-10-17, asosiy qarorlar qabul qilingan (11-bo'lim); domen va narxlar ochiq. Bog'liq: `Docs/Audit-2026-10/00_PLAN.md`, litsenziya v2 (`Helpers/LicenseHelper.cs`).

## 1. Maqsad

Bitta joydan boshqarish:
- **Tashkilotlar (tenant)** — SaaS (bizning AWS serverda) va on-premise (mijoz serverida).
- **Tariflar va obunalar** — foydalanuvchi soni, modullar, oylik/yillik.
- **To'lovlar** — Payme, Click (karta), bank o'tkazmasi (yuridik shaxslar uchun asosiy yo'l).
- **Litsenziya kalitlari** — to'lov tushishi bilan avtomatik chiqadi va ERP'ga yetkaziladi.
- **Provisioning** — yangi tashkilot uchun baza, subdomen, admin foydalanuvchi bir tugma bilan.
- **Demo/trial** — 14 kunlik sinov tenanti, demo tenant har kecha qayta tiklanadi.

WinForms generator (`tahsinERP.LicenseGen`) zaxira/favqulodda vosita sifatida qoladi.

## 2. Umumiy arxitektura

```
                        ┌──────────────────────────────────────────┐
  Vendor xodimi ──────► │  admin.tahsinerp.uz   Control Panel      │
  Mijoz admini ───────► │  my.tahsinerp.uz      Mijoz portali      │
  Payme / Click ──────► │  pay.tahsinerp.uz     To'lov webhooklari │
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
- Internet yo'q bo'lsa — saqlangan kalit bilan ishlaydi (offline). Ixtiyoriy siyosat on-prem uchun: 30 kun pull bo'lmasa banner, kalit muddati baribir o'zi tugaydi.
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
| `Subscriptions` | Id, TenantId, PlanId, Users, Period (Month/Year), StartDate, EndDate, AutoRenew, CardTokenId, Status |
| `Invoices` | Id, Number (TE-2026-000123), TenantId, SubscriptionId, AmountUzs, Status (Draft/Issued/Paid/Cancelled), DueDate, Lines (JSON: nomi, MXIK, miqdor, narx, QQS), DidoxId (e-hisob-faktura) |
| `Payments` | Id, InvoiceId, Provider (Payme/Click/Bank/Cash), ProviderTxnId, AmountTiyin, State, CreatedAt, PerformedAt, CancelledAt, CancelReason, Raw (JSON) |
| `CardTokens` | Id, TenantId, Provider, Token (shifrlangan), MaskedPan, Expire, IsActive |
| `Licenses` | Id, TenantId, InstallationId, KeyId (k1/k2), LicenseKey, MaxUsers, StartDate, ExpiryDate, Edition, IssuedBy (user/auto), IssuedAt, Reason, SupersededById |
| `ProvisioningJobs` | Id, TenantId, Type (Create/Reset/Suspend/Archive/Delete), Status, Log, StartedAt, FinishedAt |
| `Notifications` | Id, TenantId, Channel (Telegram/Email/SMS), Template, SentAt, Status |
| `StaffUsers` | Id, Email, PasswordHash (PBKDF2), Role (Owner/Sales/Support), TotpSecret |
| `AuditLog` | Id, StaffUserId, Action, Entity, EntityId, Before/After (JSON), Ip, At |

Pul summasi bazada **tiyin** (`bigint`) da saqlanadi — Payme ham tiyinda ishlaydi, yaxlitlash xatosi bo'lmaydi.

## 5. To'lovlar

### 5.1 Bank o'tkazmasi (yuridik shaxslar — asosiy)
O'zbekistonda korxonalar asosan shartnoma + hisob-faktura bo'yicha bank orqali to'laydi.
- Panel hisob-faktura (PDF) va shartnoma shablonini chiqaradi; ixtiyoriy Didox API orqali elektron hisob-faktura (ESF).
- To'lov tushganini xodim qo'lda belgilaydi (keyinchalik: bank ko'chirmasini import qilish). Belgilash = kalit avtomatik chiqadi.

### 5.2 Payme (Merchant API)
- Endpoint: `POST pay.tahsinerp.uz/payme` — JSON-RPC, `Authorization: Basic Paycom:<KEY>` tekshiruvi (test va prod kalitlari alohida).
- Metodlar: `CheckPerformTransaction`, `CreateTransaction`, `PerformTransaction`, `CancelTransaction`, `CheckTransaction`, `GetStatement`.
- `account` maydoni: `invoice_id`. Summa tiyinda, hisob-fakturaga aynan teng bo'lishi shart.
- Holatlar: `1` yaratildi → `2` bajarildi; `-1` bajarilmasdan bekor, `-2` bajarilgandan keyin bekor. Barcha metodlar idempotent (bir xil `id` qayta kelsa — o'sha natija), DB tranzaksiyada.
- `PerformTransaction` → invoice `Paid` → obuna uzaytiriladi → kalit chiqariladi → ERP keyingi pull'da oladi (yoki darhol push-signal).
- Fiskalizatsiya: chek uchun xizmatning **MXIK (IKPU) kodi** va o'lchov birligi kodi kerak bo'ladi — aniq maydonlarni Payme Business bilan shartnoma tuzishda tasdiqlash kerak.
- Takroriy to'lov (avtomatik uzaytirish): Payme Subscribe API — `cards.create` / `cards.get_verify_code` / `cards.verify` → token; `receipts.create` / `receipts.pay`. 2-bosqichda.
- Sinov: Payme sandbox (test.paycom.uz) — barcha holatlar ssenariylari o'tishi shart, aks holda prod kalit berilmaydi.

### 5.3 Click (SHOP API)
- Endpointlar: `POST pay.tahsinerp.uz/click/prepare` (action=0) va `/click/complete` (action=1).
- Imzo: `sign_string = md5(click_trans_id + service_id + SECRET_KEY + merchant_trans_id [+ merchant_prepare_id] + amount + action + sign_time)` — har so'rovda tekshiriladi.
- `merchant_trans_id` = invoice raqami. Xato kodlari Click spetsifikatsiyasi bo'yicha (-1 imzo, -2 summa, -4 allaqachon to'langan, -5 topilmadi, -9 bekor qilingan...).
- Karta tokeni (avtomatik uzaytirish) — Click Merchant API `card_token`. 2-bosqichda.

### 5.4 Umumiy qoidalar
- Webhook endpointlari faqat HTTPS, kalit/sir AWS SSM Parameter Store'da.
- Har webhook so'rovi `Payments.Raw` ga to'liq yoziladi (nizolar uchun).
- Kunlik sverka: Payme `GetStatement` va Click hisobotlari bilan solishtirish (ish).

### 5.5 Biznes talablar (kod emas, lekin oldindan kerak)
- To'lovni qabul qiluvchi yuridik shaxs (MChJ yoki YaTT) va bank hisob raqami.
- Payme Business va Click bilan merchant shartnomasi (1–3 hafta).
- tasnif.soliq.uz dan dasturiy ta'minot xizmati uchun MXIK kodi.
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

## 8. Mijoz portali (`my.tahsinerp.uz`, 2-bosqich)
- Tarif va muddat, foydalanuvchi soni, hisob-fakturalar ro'yxati, "Payme/Click bilan to'lash" tugmalari.
- Seat qo'shish / tarifni oshirish (proporsional hisob), karta bog'lash (avto-uzaytirish).
- Kirish: ERP admin hisobi orqali SSO emas — alohida, telefon + SMS kod (oddiy va xavfsiz).
- On-prem mijoz: InstallationId ni kiritib kalitni yuklab olish.

## 9. AWS deploy

**1-bosqich (arzon, tez):** panelni ERP bilan bitta Windows Lightsail serverga, alohida IIS sayti sifatida (ASP.NET Core Hosting Bundle). Qo'shimcha server narxi: $0.
- `tahsin_control` DB — o'sha SQL Server Express'da (alohida baza, alohida login).
- KMS kaliti (~$1/oy), SSM Parameter Store (bepul tier), S3 backup (~$1–3/oy), SES email (~$0.10 / 1000 xat).

**Keyinroq (ERP Core + Linux'ga o'tgach yoki 2+ ERP server bo'lganda):** panelni alohida Linux Lightsail 1–2 GB ($7–12/oy) ga Docker bilan ko'chirish; DB — o'sha serverda PostgreSQL yoki Lightsail managed DB. Panel ERP serverlaridan mustaqil bo'lgani uchun ko'chirish oson.

Xavfsizlik:
- `admin.` — faqat xodimlar, TOTP 2FA majburiy, ixtiyoriy IP allow-list.
- `pay.` — faqat webhook endpointlari, Payme/Click IP'lari ro'yxati bilan cheklash (ular e'lon qilgan bo'lsa).
- Har bir pul va kalit amali `AuditLog` ga.
- Kunlik `tahsin_control` backup S3 ga (versioning yoqilgan) — bu bazani yo'qotish = kim qancha to'laganini yo'qotish.

## 10. Bosqichlar va muddatlar

| # | Bosqich | Ish | Muddat |
|---|---|---|---|
| 0 | Tayyorgarlik (ERP) | `DbConfig` host bo'yicha tenant DB; `LicenseHelper` → `netstandard2.0` umumiy kutubxona (`tahsinERP.Licensing`), bir nechta public key; `LicenseSyncService` (register + pull); Edition bo'yicha modul cheklovi | 2–2.5 hafta |
| 1 | Panel MVP | Loyiha skeleti, StaffUsers + 2FA, Plans, Tenants, Subscriptions, Invoices, bank to'lovini qo'lda belgilash, KMS bilan kalit chiqarish, License API (register/pull), Telegram eslatmalar | 2–3 hafta |
| 2 | Provisioning | Shablon baza, agent, wildcard DNS/TLS, demo tenant kechki reset, trial | 1–1.5 hafta |
| 3 | Payme + Click | Merchant API endpointlari, sandbox ssenariylari, fiskal ma'lumotlar, kunlik sverka | 2 hafta (+ shartnoma kutish) |
| 4 | Mijoz portali | Hisob-fakturalar, onlayn to'lov, seat qo'shish, karta tokeni / avto-uzaytirish | 2–3 hafta |
| 5 | Qo'shimcha | Didox e-hisob-faktura, bank ko'chirmasi importi, on-prem online tekshiruv siyosati, hisobotlar (MRR, churn) | 1–2 hafta |

Jami: **~10–14 hafta** bitta dasturchi + AI yordamida. 0–2 bosqichdan keyin (≈5 hafta) birinchi pullik SaaS mijozni bank o'tkazmasi orqali ulash mumkin; Payme/Click shartnomasini 0-bosqich bilan parallel boshlang.

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
| `KORP` | Biznes + HR (oylik, tabel), Logistika, Rasmiy hujjatlar, ustuvor qo'llab-quvvatlash | 50 | ochiq |
| `TRIAL` | = KORP | 5 | 0, 14 kun |

Modul cheklovi ERP tomonda (0-bosqichga qo'shilgan, ~1 hafta): `Edition → PERMISSIONMODULE` xaritasi; global filtr paketga kirmagan modul controllerlarini "Paketingizda yo'q — tarifni oshiring" sahifasiga yo'naltiradi, menyu bandlari yashiriladi.

### Hali ochiq
- Domen nomi.
- Paket narxlari va yillik chegirma (masalan yillikda 2 oy bepul).

## 12. Xavflar
- **KMS ↔ .NET imzo mosligi** — 0-bosqichning birinchi testi; mos kelmasa panel imzosini lokal (SSM'dan shifrlangan private key) bilan qilish zaxira varianti.
- **SQL Server Express 1.4 GB bufer chegarasi** barcha tenant bazalari uchun umumiy — ~15–20 faol tenantdan keyin ikkinchi server (`Servers` jadvali shuning uchun boshidanoq bor).
- **Payme/Click sertifikatsiyasi** kod tayyor bo'lsa ham 1–3 hafta cho'zilishi mumkin — bank o'tkazmasi yo'li buni to'smaydi.
- **ERP multi-tenant'ga o'tish** — `HttpContext.Current` bo'lmagan joylar (fon ishlari, `Task.Run`) tenant'ni yo'qotmasligi kerak; 0-bosqichda tekshiriladi.
