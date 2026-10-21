# Boshqaruv paneli — 4-bosqich (Qo'shimcha)

Holat: 2026-10-20, branch `feature/cp-stage4`. Loyiha: `tahsinERP.ControlPanel` (+ kichik o'zgarishlar umumiy kutubxona va ERP'da — 7-bo'lim).
EF migratsiya: `20261020163510_Stage4`. Testlar: `tahsinERP.ControlPanel.Tests/Stage4Tests.cs` (17 ta; jami 145) va
`tahsinERP.Tests/Editions/EditionModulesTests.cs` (+2). MRR/churn hisobotlari 07_TRACTION.md da (bu bosqichda qayta qilinmadi);
Payme/Click — rejadan olib tashlangan (to'lovlar qo'lda va bank ko'chirmasi orqali).

## 1. DISTRIBUTSIYA paketi (`DIST`)

`Distribution/00_PLAN.md` 1-bo'lim, 4-faza: distribyutorlar uchun alohida paket, MES/BOM yashirin.

| Paket | Modullar | Foydalanuvchi |
|---|---|---|
| `DIST` | SRM (xarid + ombor), CRM, Topshiriqlar, Moliya, Distributsiya | 20 |

- **Nega 20:** BIZNES bilan bir pog'ona (narxi ham shu atrofda bo'lishi kutiladi). Ofis (operator, omborchi, kassir, buxgalter) +
  10–15 agent/ekspeditor sig'adi; kattaroq distribyutor — KORP (50), undan ortig'i — individual shartnoma (00_PLAN.md 11 #1).
  Narx ochiq (boshqa paketlar kabi Tariflar sahifasida belgilanadi).
- KORP va TRIAL'da Distributsiya avvalgidek bor. `Plans` seed'ida KORP/TRIAL `Modules` qatoriga `Distribution` qo'shildi (ma'lumot uchun —
  cheklovni ERP `EditionModules` bo'yicha qiladi).
- `EditionModules` (tahsinERP.Licensing): `Dist = "DIST"`, `DistModules`, `SellableEditions = START, DIST, BIZNES, KORP` (taxminiy narx
  tartibi). `UpgradeOrder` (START→BIZNES→KORP zanjiri) va `CheapestEditionWith` o'zgarmadi.
- **Yon tarmoq:** DIST zanjirda emas. Yangi `SuggestEditionsFor(joriy, modul)` — modulni o'z ichiga olgan paketlar, avval joriy
  paketning hamma modullarini saqlaydiganlari: START + Distributsiya → «Distributsiya yoki Korporativ»; DIST + MES → «Korporativ yoki
  Biznes» (Biznes'da Distributsiya yo'q); BIZNES + Distributsiya → «Korporativ yoki Distributsiya». ERP'da `EditionCheckAttribute`
  (AJAX xabari) va `License/NotInPlan` shundan foydalanadi.
- **Portal tarif so'rovi** (`PortalRequestService.Classify`): «oshirish» endi `SortOrder` emas, modul to'plami bo'yicha — yangi paket
  joriy paketning hamma modullarini saqlasa va ko'proq bersa (modul yoki foydalanuvchi) — darhol; aks holda (BIZNES ↔ DIST, KORP → DIST)
  faqat davr oxiridan. Noma'lum kodlarda eski qoida (SortOrder).

## 2. Bank ko'chirmasi importi

Panel → **Bank** (`/Bank`, Owner va Sales — to'lov belgilash huquqi bilan bir xil).

1. Fayl yuklanadi (≤ 5 MB, ≤ 5000 qator):
   - **1C «Клиент-Банк»** (`1CClientBankExchange`, Windows-1251 yoki UTF-8): `СекцияДокумент … КонецДокумента`; maydonlar `Номер`, `Дата`
     (`ДатаПоступило` ustun), `Сумма`, `Плательщик(1)`, `ПлательщикИНН`, `ПлательщикСчет`, `ПолучательСчет/ИНН`, `НазначениеПлатежа`.
     Kirim/chiqim: `ДатаПоступило`/`ДатаСписано`, bo'lmasa to'lovchi hisobi = bizning hisob (`РасчСчет` yoki `Vendor:BankAccount`) yoki
     to'lovchi STIR = `Vendor:Inn` → chiqim.
   - **CSV** (`;`, `,` yoki TAB, qo'shtirnoqlar) va **XLSX** (birinchi varaq; tashqi kutubxonasiz): sarlavha qatori (birinchi 15 qatordan)
     o'zbek/rus/ingliz nomlari bilan tanib olinadi — kerakli: sana, summa; moslash uchun: STIR, to'lov maqsadi; ixtiyoriy: hujjat №,
     to'lovchi, hisob. Manfiy summa — chiqim. Summa formatlari: `1 234 567,89`, `1234567.89`, `1,234,567`.
2. Har kiruvchi qator moslanadi (`BankImportService.MatchAsync`):
   1. **Takror**: shu bank tranzaksiyasi (SHA-256 `sana|summa|hujjat №|to'lovchi hisobi`) avval qo'llangan, yoki shu sana+summa+hujjat №
      bilan qo'lda belgilangan to'lov bor, yoki faylda ikki marta.
   2. **Raqam bo'yicha**: maqsaddagi `TE-YYYY-NNNNNN` (bo'shliq/`/`, kirill «ТЕ», qisqa raqam ham) → ochiq (Issued/PartiallyPaid)
      hisob-faktura. Qoldiqdan katta summa — «To'sib qo'yilgan» (ortiqcha to'lov qo'lda hal qilinadi). To'lovchi STIR tenantnikiga mos
      kelmasa — ogohlantirish (uchinchi shaxs to'lashi mumkin).
   3. **STIR + aniq qoldiq**: tenant STIR'i va qoldig'i aynan shu summaga teng ochiq hisob-faktura: bitta — «Mos», bir nechta — «Bir nechta».
   4. Aks holda «Topilmadi» (sababi izohda). Xodim istalgan qatorga raqamni qo'lda yozib tanlashi yoki o'tkazib yuborishi mumkin.
3. Ko'rib chiqish sahifasida «Mos» qatorlar belgilangan holda; **«Tanlanganlarni qo'llash»** — har qator
   `BillingService.RecordPaymentAsync` orqali (usul: Bank, sana: hujjat sanasi, Reference: hujjat №) — qo'lda «To'lov qabul qilindi» bilan
   aynan bir xil natija: qisman/to'liq, obuna uzaytirish, tenant Active, yangi kalit, `payment.record` audit. Qo'lda belgilash yo'lida
   Telegram xabari yo'q — import ham xabar yubormaydi.
4. **Idempotentlik**: qo'llashdan oldin qator `AppliedKey = DedupKey` bilan band qilinadi — `BankImportLines.AppliedKey` noyob (filtrlangan)
   indeks. Shu fayl ikki marta yuklansa ham, ikki xodim bir vaqtda bossa ham bitta tranzaksiya bir marta qo'llanadi. To'lov rad etilsa
   (masalan qoldiq yetmaydi) — band bo'shatiladi, qator «Xato». To'lov alohida DI doirasida yoziladi (xato bo'lsa importning DbContext'ida
   qoldiq qolmaydi). Jarayon band qilish va to'lov orasida uzilsa qator «Jarayonda» qoladi — to'lovlar ro'yxatini tekshiring (avtomatik
   tiklash yo'q).
5. Audit: `bank.import`, `bank.assign`, `bank.skip`, `bank.apply` (+ `payment.record`).

Jadvallar: `BankImports` (fayl, format, SHA-256, hisob, davr, kim/qachon), `BankImportLines` (qator, moslash holati/usuli/izohi, invoice,
holat, PaymentId, AppliedKey).

## 3. Didox e-hisob-faktura

- `IDidoxClient` + `DidoxHttpClient` (Services/Didox.cs), `DidoxService` (yuborish, holat), `DidoxRefreshHostedService` (har
  `RefreshMinutes`, faqat sozlangan bo'lsa).
- Hisob-faktura sahifasi → «Didox (e-hisob-faktura)»: chiqarilgan (raqamli, bekor qilinmagan) hisob-faktura uchun **«Didox'ga
  yuborish»** (Owner/Sales) → Didox'da **qoralama** (hujjat turi 002) yaratiladi, `Invoice.DidoxId`, `DidoxStatus`, `DidoxSentAtUtc`
  saqlanadi. Imzo — Didox kabinetida E-IMZO bilan (API orqali ERI imzosi qilinmaydi). «Holatni yangilash» va fon ishi holatni
  oladi (yakuniy: imzolangan / rad / bekor / muddati o'tgan — keyin so'ralmaydi). Xato `DidoxError`ga yoziladi. Audit: `didox.send`,
  `didox.send-failed`, `didox.status`.
- Sozlanmagan bo'lsa tugma ko'rinmaydi, o'rniga nima yetishmasligi yoziladi.
- So'rovlar: `POST /v1/auth/{STIR}/password/{locale}` (`{password}`) → `token`; keyingi so'rovlarda `user-key: <token>` va
  `Partner-Authorization: <partner token>`; `POST /v1/documents/002/create`; `GET /v1/documents/{id}?owner=1`. Tana: `Version`,
  `FacturaType=0`, `FacturaDoc`, `ContractDoc`, `SellerTin/Seller` (Vendor:*), `BuyerTin/Buyer` (tenant STIR, nomi, manzili),
  `ProductList` (MXIK `CatalogCode`, `PackageCode`, `Count`, `Summa` = birlik narxi QQSsiz, `DeliverySum`, `VatRate`, `VatSum`,
  `DeliverySumWithVat`). Bizda narxlar QQS bilan — Didox'ga QQSsiz summa + QQS alohida (`InvoiceLine.VatTiyin`).
- **Tekshirilmagan** (haqiqiy Didox hisobi va partner tokeni yo'q): endpoint va maydonlar ochiq manbalardan (Didox partner API,
  `didox` npm SDK v1.0.6 — rasmiy emas) olingan; javob tuzilishi (hujjat ID: `_id`/`doc_id`/`id`, holat: `doc_status`/`status`),
  holat kodlari (0 qoralama, 1 yuborilgan, 2 imzolangan, 3 rad, 4 bekor, …), token muddati (6 soatda yangilanadi, 401 da qayta kirish),
  `Summa` maydonining ma'nosi (birlik narxi deb olindi) va `ContractDoc`/`Buyer` majburiy maydonlari — birinchi ulashda **stage**
  muhitida (`https://stage.goodsign.biz`) tekshirilishi kerak. Testlar faqat soxta klient va soxta HTTP handler bilan.

## 4. On-prem onlayn tekshiruv siyosati

Qaror (00_PLAN.md 11 #5): onlayn tekshiruv ixtiyoriy; internet bo'lsa pull, bo'lmasa offline kalit; on-prem kalit ≤ 1 yil.
Avval bor edi: ERP pull (`LicenseSync`), on-prem kalit ≤ 1 yil (`LicenseIssuer.OnPremMaxDays`, `DesiredFromSubscription`).
Qo'shildi — tenant bo'yicha sozlama (Tashkilot → Tahrirlash, faqat on-prem):

| `Tenant.OnlineCheck` | Kalit | ERP pull qilmasa |
|---|---|---|
| `Optional` (standart) | shartnoma davri, ≤ 1 yil (o'zgarmagan) | ishlayveradi; `OfflineGraceDays` (standart 30) kundan keyin ERP admin'ga banner, panel bosh sahifasida «aloqa yo'q» |
| `Required` | **aylanma**: ≤ `OfflineGraceDays` kun (7–90, standart 30), grace'ning yarmi o'tganda yangisi | kalit o'zi tugaydi → ERP Grace (3 kun, faqat o'qish) → blok. 3+ kun aloqa yo'q — ERP banneri va panel ro'yxati |

- Aylanma kalit imzolangan kalitning o'zi bilan ta'minlanadi — ERP'da yangi bloklash mantiqi yo'q, mavjud muddat tekshiruvi ishlaydi.
  Yangilash: kunlik ish (`EnsureKeysForStartedPeriodsAsync`) va har pull (`LicenseApi.PullAsync` → `EnsureForTenantAsync`).
  Optional → Required o'tilganda uzun kalit darhol qisqasiga almashtiriladi (ERP keyingi pull'da oladi; allaqachon berilgan offline
  kalitni qaytarib olib bo'lmaydi — u o'z muddatigacha amal qiladi).
- License API: register va pull 200 javobida `online_check` (`"required"`/`"optional"`) va `offline_grace_days` (01_LICENSE_API.md).
  Eski ERP bu maydonlarni e'tiborsiz qoldiradi.
- ERP: `LICENSE_STATE` ga `OnlineCheck`, `OfflineGraceDays`, `LastSyncOkUtc` (oxirgi muvaffaqiyatli sinxronlash) yoziladi;
  `LicenseSync.OnlineCheckPolicy.Warning` (sof funksiya, panel testlari ham tekshiradi) → `_LicenseBanner` (admin'larga) va License
  sahifasida siyosat + ogohlantirish. Panelga ulanmagan (offline) o'rnatmada hech narsa o'zgarmaydi.
- Sozlama: `Panel:OnPremOfflineGraceDays` (standart 30), tenant'da `OfflineGraceDays` (bo'sh — standart).

## 5. Portal: SMS / email kod va parolni o'zi tiklash

- Kanallar (`Portal/Channels.cs`): `EskizSmsSender` (`ISmsSender`; `POST {BaseUrl}/auth/login` → token, `POST /message/sms/send`
  `mobile_phone`, `message`, `from`, Bearer; 401 da qayta kirish) va `SmtpEmailSender` (`IEmailSender`, System.Net.Mail — AWS SES SMTP
  ham bo'ladi). `SmsOtpSender` / `EmailOtpSender` — `IOtpSender`. Sozlanmagan bo'lsa kanal yo'q.
- **Kanal tanlash**: Profil → «Kirish kodi kanali» (Telegram standart; SMS — telefon bo'lsa, email — email bo'lsa, sozlangan bo'lsa).
  `PortalUser.OtpChannel`. Tanlangan kanal ishlamasa — Telegram, keyin qolganlari (`PortalAuthService.ResolveTarget`). Audit `portal.otp-channel`.
- **Parolni unutdingizmi?** (`/Forgot`, Login sahifasidan havola): email yoki telefon → bir martalik havola (`PR-…`, `PortalInvites`'da
  faqat SHA-256 xeshi, `Portal:ResetMinutes` = 30 daqiqa) foydalanuvchining **shaxsiy** kanaliga (tanlagani birinchi): shaxsiy Telegram
  chat, SMS yoki email — tenant guruh chatiga hech qachon. Havola mavjud `/Invite` sahifasida yangi parol o'rnatadi (eski parol va
  sessiyalar bekor, blok olinadi).
  - Foydalanuvchilarni sanab bo'lmaydi: javob doim bir xil («Agar shu login bilan faol hisob bo'lsa…»), xato ham ko'rsatilmaydi.
  - Cheklov: IP bo'yicha `login` rate limit; foydalanuvchiga 2 daqiqada bitta va soatiga 3 ta (ortig'i jim rad, audit `portal.reset-throttled`).
  - Parolsiz (taklifni qabul qilmagan), o'chirilgan yoki arxiv tenant foydalanuvchisi — hech narsa yuborilmaydi; shaxsiy kanal yo'q — ham
    (audit `portal.reset-request` `Delivered=false`) — bunday holda xodim avvalgidek yangi havola beradi.
  - Ma'lum cheklov: yuborish so'rov ichida bajariladi, shuning uchun mavjud va mavjud bo'lmagan login javob vaqti biroz farq qilishi
    mumkin (matn bir xil). IP rate limit buni sekinlashtiradi; to'liq yechim — navbat orqali yuborish (qilinmagan).
  - Kanal egasi ham parolni tiklay oladi, ham kodni oladi — portal uchun qabul qilingan (bank darajasidagi himoya emas).
- **Tekshirilmagan**: haqiqiy Eskiz va SMTP hisoblari bilan (faqat soxta sender'lar). Eskiz matn shablonlarini moderatsiya qiladi —
  kod va havola matnlari (`kirish kodi …`, `parolni tiklash havolasi …`) Eskiz kabinetida tasdiqlangan bo'lishi kerak; `From` —
  tasdiqlangan nik (standart `4546` — Eskiz test nomi).

## 6. Sozlamalar (`ControlPanel:*`, maxfiylari faqat env/user-secrets)

| Kalit | Ma'nosi |
|---|---|
| `Panel:OnPremOfflineGraceDays` | 30 — on-prem internetsiz muddat (tenant'da berilmasa) |
| `Portal:ResetMinutes` | 30 — parol tiklash havolasi muddati |
| `Eskiz:BaseUrl`, `Eskiz:Email`, `Eskiz:Password`, `Eskiz:From` | SMS (Email+Password bo'sh — o'chiq) |
| `Smtp:Host`, `Port` (587), `EnableSsl`, `User`, `Password`, `From`, `FromName` | Email (Host yoki From bo'sh — o'chiq) |
| `Didox:BaseUrl` | `https://api-partners.didox.uz` (sinov: `https://stage.goodsign.biz`) |
| `Didox:PartnerToken`, `Didox:Password` | Partner tokeni va kabinet paroli — maxfiy |
| `Didox:Inn` | Sotuvchi STIR (bo'sh — `Vendor:Inn`) |
| `Didox:CatalogCode`, `CatalogName`, `PackageCode`, `PackageName` | MXIK va o'lchov birligi (tasnif.soliq.uz) — majburiy |
| `Didox:ContractNo`, `ContractDate` | Shartnoma (bo'sh — «Oferta», tenant yaratilgan sana) |
| `Didox:Locale`, `Didox:RefreshMinutes` | `ru`, 60 |
| `Vendor:Inn`, `Vendor:BankAccount` | Bank importida chiqim/kirimni ajratish uchun ham ishlatiladi |

## 7. Panel loyihasidan tashqari o'zgarishlar

| Fayl | Nima |
|---|---|
| `tahsinERP.Licensing/Editions/EditionModules.cs` | `Dist`, `DistModules`, `SellableEditions`, `Covers`, `SuggestEditionsFor`, `SuggestionText`, DIST nomi. `ProductModule.Distribution` controller ro'yxati va `ControllerMap` bloki **o'zgartirilmagan** |
| `tahsinERP/Filters/EditionCheckAttribute.cs`, `Controllers/AdminSetting/LicenseController.cs`, `Views/License/NotInPlan.cshtml` | Tavsiya — `SuggestionText` |
| `tahsinERP/Services/LicenseSync/LicensePanelClient.cs` | `online_check`, `offline_grace_days` o'qish; `OnlineCheckPolicy` |
| `tahsinERP/Services/LicenseSync/LicenseSyncService.cs`, `Services/LicenseService.cs` | `LastSyncOkUtc`, siyosatni saqlash, `OnlineWarningFor`, `LicenseStatus.OnlineWarning` |
| `tahsinERP/ViewModels/LicenseViewModels.cs`, `Views/License/Index.cshtml`, `Views/Shared/_LicenseBanner.cshtml` | Siyosat va ogohlantirish |
| `tahsinERP.Tests/Editions/EditionModulesTests.cs` | DIST va tavsiya testlari |

ERP bazasiga o'zgarish yo'q (`LICENSE_STATE` — nom/qiymat jadvali, yangi qatorlar).
