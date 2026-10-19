# Boshqaruv paneli — 1-bosqich (Panel MVP)

Holat: 2026-10-18. Loyiha: `tahsinERP.ControlPanel` (ASP.NET Core, .NET 10 LTS, Razor Pages, EF Core 10, SQL Server),
testlar: `tahsinERP.ControlPanel.Tests` (xUnit, 49 ta). Baza: `tahsin_control` (ERP bazasiga — DBTHSN — tegmaydi).
Bog'liq: `00_PLAN.md` (3–6, 10–11), `01_LICENSE_API.md` (ERP bilan kontrakt).

## 1. Nima bor

| Bo'lim | Tafsilot |
|---|---|
| Xodimlar | PBKDF2-SHA256 (600k), TOTP 2FA **majburiy** (birinchi kirishda QR bilan ulanadi), rollar Owner/Sales/Support, 5 xato → 15 daq blok, SecurityStamp (parol/rol/2FA o'zgarsa sessiyalar yopiladi). Birinchi Owner — CLI. |
| Tariflar | START/BIZNES/KORP/TRIAL seed (5/20/50/5, modullar, QQS 12%, narxlar bo'sh — Owner kiritadi). Kod = kalitdagi Edition. |
| Tashkilotlar | slug (ERP `TenantRegistry` qoidasi + rezerv nomlar), STIR/JShShIR, kontaktlar, Telegram chat, SaaS/OnPrem, holat, CompanyID, DbName, Server, InstallationId, reg_token (faqat SHA-256 xeshi), tenant_secret (AES-GCM shifrlangan). |
| Serverlar | Nom, host, hudud, SQL instance, MaxTenants (to'lsa yangi tenant joylanmaydi). Login/parol saqlanmaydi. |
| Obunalar | TRIAL avtomatik (14 kun); pullik — Pending → to'liq to'lovda Active; oy/yil; qat'iy paket (Users ≤ MaxUsers). |
| Hisob-fakturalar | Qoralama → Chiqarish (raqam `TE-YYYY-000001`, yil bo'yicha uzluksiz, `UPDLOCK/HOLDLOCK`), qatorlar, QQS (narx QQS bilan: QQS = summa×12/112), PDF. |
| To'lovlar | Faqat qo'lda (Payme/Click YO'Q): summa, sana, usul, hujjat №, izoh. Qisman to'lov; ortiqcha summa rad. Storno — faqat Owner, sabab majburiy, o'chirmaydi, obunani qisqartirmaydi; qayta to'lovda obuna ikki marta uzaytirilmaydi. |
| Kalitlar | v2 format (`tahsinERP.Licensing`), `ILicenseSigner` → `LocalRsaSigner` (shifrlangan fayl) / `KmsSigner` (skelet). Har kalit ERP qiladigan tekshiruvdan o'tkaziladi. To'liq to'lovda avtomatik; qo'lda chiqarish/bekor qilish (Owner, sabab bilan); tarix o'chmaydi (`SupersededById`). |
| License API | `POST /api/license/register`, `GET /api/license/{id}` (HMAC, ±300 s, nonce 10 daq, ETag/304, 401/404/410). Rate limit 60/daq/IP. |
| Eslatmalar | Kunlik fon ishi (09:00 Toshkent): 14/7/3/1/0 kun → Telegram (tenant chat + xodimlar chati), token yo'q bo'lsa — faqat log. Takrorlanmaydi (DedupKey). Shu ish hayot siklini ham yuritadi: Active → Grace (3 kun) → Suspended, Trial → Suspended. |
| AuditLog | Pul (hisob-faktura, to'lov, storno), kalit (chiqarish, bekor), status, tarif, server, xodim, kirish — kim/qachon/IP/oldin/keyin JSON, amal bilan bitta tranzaksiyada. |
| Dashboard | Faol/sinov/grace/suspended, qarzdorlar (muddati o'tgan), muddati yaqinlashganlar (14 kun), oylik tushum (6 oy), bog'lanmaganlar. |

**2-bosqichda qo'shildi:** provisioning (ProvisioningJobs, agent, `tenants.json`, demo reset, sinov ochish — namuna ma'lumotlar bilan/bo'sh, sinovni erta yakunlash), tenant foydalanuvchi faolligi (Faollik sahifasi, «Faol emas sinovlar») — `05_PROVISIONING.md` (10-bo'limda maxfiylik: parol, F.I.Sh., email, IP/MAC panelga kelmaydi; amal matni maskalanadi; 90 kun). **Yo'q:** mijoz portali, Didox, email/SMS.

## 2. Lokal ishga tushirish

Talab: .NET SDK 10, lokal SQL Server (`.`; Windows auth). 192.168.100.200 — ESKIRGAN, ishlatilmaydi.

```bash
cd tahsinERP.ControlPanel
dotnet build
```

Sirlar git'ga KIRMAYDI — user-secrets (lokal) yoki env (server):

```bash
dotnet run -- gen-master-key
```
```bash
dotnet user-secrets set "ControlPanel:Security:MasterKey" "<yuqoridagi qiymat>"
```

Imzolash kaliti (panelning o'z jufti, k2). Fayl repodan TASHQARIDA turadi:

```bash
dotnet run -- keygen --out C:\ProgramData\tahsinERP\panel-signing-k2.json
```
```bash
dotnet user-secrets set "ControlPanel:Signing:KeyFile" "C:\ProgramData\tahsinERP\panel-signing-k2.json"
```
```bash
dotnet user-secrets set "ControlPanel:Signing:Passphrase" "<keygen'da kiritilgan parol>"
```

Baza va birinchi Owner (parol so'raladi yoki `CP_OWNER_PASSWORD` env):

```bash
dotnet run -- migrate
```
```bash
dotnet run -- create-owner --email owner@example.uz --name "Ism Familiya"
```

Ishga tushirish (Development'da migratsiya o'zi ham qo'llanadi):

```bash
dotnet run
```

`https://localhost:7243` → login → QR kodni autentifikator ilovasida skanerlash → panel.

Testlar (lokal SQL'da vaqtinchalik `tahsin_control_test_*` baza yaratib, oxirida o'chiradi; boshqa server: `CP_TEST_SQL` env):

```bash
dotnet test tahsinERP.ControlPanel.Tests
```

### Sozlamalar (`appsettings.json`, `ControlPanel:*`)

| Kalit | Ma'nosi |
|---|---|
| `ConnectionStrings:Control` | `tahsin_control` ulanishi (lokal: `Server=.;...`) |
| `Security:MasterKey` | 32 bayt Base64 — tenant_secret va TOTP sirlarini shifrlaydi. **Faqat env/user-secrets.** Yo'qolsa: tenantlar qayta ro'yxatdan o'tadi, xodimlar 2FA'ni qayta ulaydi → baza zaxirasidan ALOHIDA saqlang. |
| `Signing:Provider/KeyId/KeyFile/Passphrase` | `Local` + `k2` + shifrlangan fayl + parol (env). `Kms` — hozircha skelet, `NotSupportedException`. |
| `Telegram:BotToken/StaffChatId` | Bo'sh — xabarlar faqat logga. Token faqat env (`ControlPanel__Telegram__BotToken`). |
| `Vendor:*` | Hisob-fakturadagi sotuvchi rekvizitlari (MChJ, STIR, QQS kodi, bank, h/r, MFO) — **to'ldirilishi kerak**. |
| `Panel:*` | TimeZone (Asia/Tashkent), PollHours 6, RegTokenDays 7, SaasKeyExtraDays 3, GraceDays 3, InvoiceDueDays 5, DailyJobHour 9, MigrateOnStartup, rate limitlar. |
| `Pdf:FontsDir` | Linux'da shrift papkasi (Windows'da Arial avtomatik). |
| `DataProtection:KeysDir` | Cookie/antiforgery kalitlari papkasi (serverda majburiy; Windows'da DPAPI LocalMachine bilan shifrlanadi). Bo'sh — standart joy (dev). |

## 2a. Serverga joylash (IIS)

Skript: `tahsinERP.ControlPanel/deploy/Install-PanelIis.ps1` (Administrator, ASP.NET Core 10 Hosting Bundle o'rnatilgan bo'lsin).
U publish qiladi, app pool (No Managed Code, AlwaysRunning — kunlik fon ishi to'xtamasin) va https saytni yaratadi, sirlarni
app pool `environmentVariables`ga **yashirin so'rov** orqali yozadi, `%ProgramData%\tahsinERP\ControlPanel\keys` ga ruxsat beradi
va `migrate` ni ishga tushiradi. Yangilashda: `-KeepSecrets`. Keyin bir marta `create-owner`.

```bash
powershell -ExecutionPolicy Bypass -File tahsinERP.ControlPanel\deploy\Install-PanelIis.ps1 -HostName admin.<domen> -CertThumbprint <thumbprint>
```

Eslatma: skript sintaksisi tekshirilgan, lekin haqiqiy IIS serverda hali ishga tushirilmagan.
App pool identity (`IIS AppPool\tahsinERP.ControlPanel`) SQL'da `tahsin_control` ga login/ruxsat olishi kerak.
`tahsin_control` ni har kuni zaxiralang (S3, versioning) — bu baza yo'qolsa, kim qancha to'laganini yo'qotasiz.

## 3. Panel kalitini ERP qabul qilishi (k2)

ERP `tahsinERP.Licensing/LicenseHelper.cs` dagi `TrustedPublicKeys` (k1, k2) bilan tekshiradi. **Hozir `K2PublicKeyXml = ""`** —
panel chiqargan kalit ERP'da rad etiladi (`SigningTests.PanelKey_AcceptedByErpVerifier_OnlyWhenPublicKeyTrusted` shuni isbotlaydi).

Prod'ga chiqishdan oldin (bir martalik):
1. Prod serverda `keygen` → shifrlangan fayl serverda qoladi (zaxirasi — xavfsiz joyda, parol alohida).
2. `print-public-key` (yoki keygen chiqishi) → `<RSAKeyValue>...` XML.
3. `tahsinERP.Licensing/LicenseHelper.cs` → `K2PublicKeyXml = "<shu XML>"`, ERP'ni qayta yig'ib deploy qilish.
4. Mavjud k1 private key panelga KO'CHIRILMAYDI — k1 WinForms generatorda qoladi (zaxira/on-prem).
5. KMS'ga o'tishda: KMS'dagi yangi kalitning public qismi `K2PublicKeyXml` ga (yoki yangi `k3` sloti), `KmsSigner.SignAsync` ni
   `AWSSDK.KeyManagementService` bilan to'ldirish (`MessageType=RAW`, `RSASSA_PKCS1_V1_5_SHA_256`), `Signing:Provider=Kms`.

Testlar ERP tomonini qanday isbotlaydi:
- `LicenseApiTests` / `BillingTests` — ERP'ning **haqiqiy** `Services/LicenseSync/LicensePanelClient.cs` fayli test loyihasiga ulangan
  (`<Compile Include ... Link>`) va haqiqiy panelga (WebApplicationFactory) so'rov yuboradi: register, idempotentlik, 409/404/400,
  imzoli pull, ETag/304, noto'g'ri sir, soat farqi, noma'lum o'rnatma, replay, 410 revoked, to'lovdan keyin yangi kalit.
- Har kalit `LicenseHelper.TryParseKey(k1 + test k2)` + `IsBoundTo` + CompanyID + Edition + muddat bilan tekshiriladi — ERP
  `LicenseService.ActivateCompanyLicense` ishlatadigan aynan shu kutubxona.

## 4. Kalit muddati qoidalari (00_PLAN.md 3.4)

| Holat | Kalit |
|---|---|
| TRIAL | Edition TRIAL, 5 foyd., sinov oxirigacha |
| SaaS, to'langan | Edition = paket, Users = obuna, muddat = davr oxiri **+ 3 kun** |
| On-prem | muddat = davr oxiri, lekin **bugundan ≤ 1 yil** (2 yil to'lansa — ikkinchi yil keyin qayta chiqariladi) |
| Bekor qilish | Owner, sabab bilan; ixtiyoriy «shu sanagacha qisqa kalit» (SaaS'da pull'da yetib boradi); bo'lmasa pull → 410 |
| Paket o'zgarishi | Obuna tahrirlangach tenant sahifasida «Kalitni obunadan yangilash» |

Register'da `company_id` ERP'dan olinadi (kalitdagi CompanyID ERP kutganiga teng bo'lishi shart); `installation_id` boshqa tenantga
bog'langan bo'lsa — 409. Yangi reg_token chiqarilsa (ERP qayta o'rnatilganda) keyingi register bog'lanishni almashtiradi.

## 5. Provisioning (2-bosqich) uchun ma'lumotlar

`Tenants`: `Slug` (ERP `tenants.json` `slug`), `DbName` (SaaS'da standart `DB_<slug>`, `-` → `_`; `tenants.json` `database`),
`CompanyId` (`companyId`), `ServerId` → `Servers.SqlInstance` (`connectionString` uchun asos), `Status` (Suspended/Archived →
`enabled: false`). reg_token panelda faqat xesh: provisioning token'ni yaratish paytida oladi (UI'da bir marta ko'rsatiladi) va
`LICENSE_STATE.RegToken` ga yozadi; `InstallationId` ni oldindan YOZMAYDI.

## 6. Ma'lum cheklovlar / keyingi qadamlar

- Nonce'lar bazada (`ApiNonces`, noyob indeks) — bir nechta panel instansida ham replay ushlanadi; kunlik ish eskilarini o'chiradi.
- `tenant_secret` rotatsiyasi (`/api/license/rotate`) — yo'q (01_LICENSE_API.md ochiq savol).
- Email/SMS eslatmalar, Didox, bank ko'chirmasi importi — 4-bosqich.
- PDF kutubxonasi: PDFsharp/MigraDoc **6.2.4 (MIT)**. 6.5.0 dan boshlab AGPL-3.0 — **versiyani oshirmang**.
  QuestPDF Community — yillik daromad < $1M bo'lsa bepul, MIT emas (daromad oshsa pullik) — shuning uchun tanlanmadi.
