# Traction — tenantlar faolligi va mahsulot analitikasi

Holat: 2026-10-19. Sahifa: panel → **Traction** (`/Traction`, barcha xodimlar o'qiydi; amal yo'q — faqat CSV eksport).
Kod: `Services/TractionService.cs` (metrikalar), `Services/UsageService.cs` (agregatlar), agent `UsageCollector.cs`.
Bog'liq: `05_PROVISIONING.md` 10-bo'lim (yig'ish oqimi va maxfiylik).

## 1. Ma'lumot qatlami

```
ERP tenant bazasi ──(agent, faqat SELECT)──► /api/agent/usage ──► UsageEvents (xom, 90 kun)
  USER_ENTRIES (kirish)                                              │ har yangi yozuvdan keyin o'sha kun qayta quriladi
  USERLOGS (amal) → controller → modul → amal turi                   ▼
  S_ORDERS, P_ORDERS, INCOMES, SALES_INVOICES,          UsageDailyUsers   (tenant × kun × foydalanuvchi: kirish, amal)
  PRODUCT_PRODUCTION, F_WAYBILLS, HR_PAYROLL_HEADERS    UsageDailyModules (tenant × kun × modul × controller × amal turi)
  (hujjat sanasi bo'yicha kunlik soni, oxirgi 35 kun)   UsageDailyDocs    (tenant × kun × hujjat turi)   ← ≥ 2 yil (760 kun)
```

- **Modul xaritasi** — `tahsinERP.Licensing/Editions/EditionModules.cs` (ERP'dan umumiy kutubxonaga ko'chirildi, namespace o'sha:
  `tahsinERP.Services.Editions`). ERP (EditionCheck, menyu), agent va panel bitta xaritadan foydalanadi; ERP testlari
  (`EditionModulesTests`) o'zgarishsiz ishlaydi. `USERLOGS.ControllerName` ba'zan controller emas (`"SOrderController"`,
  `"ProductProductionController.Details"`, o'zbekcha `"Foydalanuvchilar"`) — `EditionModules.NormalizeLogController` (qo'shimcha va
  `LogAliases`). Xaritada yo'q nom sahifada «xaritada yo'q» belgisi bilan chiqadi — ERP log nomlarini tartibga solish uchun.
- **Amal turi** (`UsageClassifier`, matndagi o'zbekcha fe'l): o'chir… → Delete, eksport/excel/pdf → Export, yarat/qo'sh/import → Create,
  tahrir/yangila/o'zgar/tasdiq/bekor/status → Edit, ko'r → View, qolgani — Other. **ERP ko'rishni (GET) loglamaydi**, shuning uchun
  View deyarli yo'q; «sahifalar» reytingi o'zgartiruvchi amallar bo'yicha.
- **Biznes hajmi** — USERLOGS'dagi Create'ga emas, jadvallardagi hujjat sanasiga tayanadi (orqa sana bilan kiritilgan va importlar
  ham to'g'ri; Create logi hamma joyda yo'q). Agent har safar oxirgi `Usage:DocsWindowDays` (35) kunni qayta hisoblaydi — panel shu
  oraliqni almashtiradi. O'chirilgan (`IsDeleted`) hujjatlar kirmaydi; `SALES_INVOICES` da IsDeleted yo'q — bekor qilinganlari ham kiradi.
- **ERP xatolari** — ERP xatolarni bazaga yozmaydi (Trace), shuning uchun «modul bo'yicha xatolar» metrikasi **hozircha yo'q**.
  Qo'shish uchun ERP'da xato jadvali (migratsiya) kerak bo'ladi.
- Sanalar Toshkent vaqti bo'yicha. Demo tenant (`Provisioning:DemoSlug`) Traction hisoblariga kirmaydi; o'chirilgan tenantlar ham.

## 2. Filtrlar

Davr: 7 / 30 / 90 kun yoki «dan–gacha» (ko'pi bilan 2 yil). **Oldingi davr** — shuncha kun undan oldin (trend uchun).
Paket — tenantning joriy faol obunasi paketi (pullik bo'lsa pullik, aks holda TRIAL). Holat — tenant holati.

## 3. Metrikalar

| Metrika | Ta'rif |
|---|---|
| Faol tenantlar | davrda kamida bitta faol foydalanuvchisi (kirish yoki amal) bo'lgan tenantlar / arxivlanmagan tenantlar |
| DAU | davrning har kunidagi faol (tenant, foydalanuvchi) juftliklari sonining o'rtachasi |
| WAU / MAU | davr oxirigacha oxirgi 7 / 30 kunda kamida bir marta faol bo'lgan (tenant, foydalanuvchi) juftliklari |
| Stickiness | oxirgi 30 kun o'rtacha DAU / MAU |
| Sinov → pullik | davrda TRIAL obunasi boshlangan tenantlardan pullik obunasi (Active yoki Expired — ya'ni bir marta to'langan) borlar ulushi |
| MRR | faol pullik obunalar yig'indisi: obunaning oxirgi to'langan hisob-fakturasi summasi / (davrlar soni × oylar); hisob-faktura yo'q bo'lsa — tarif narxi (oylik yoki yillik/12). QQS bilan, so'm |
| Davr tushumi | davrdagi storno qilinmagan to'lovlar (to'lov sanasi bo'yicha) |
| Churn | davr boshida pullik obuna (EndDate + GraceDays) bilan qoplangan tenantlardan davr oxirida (yoki bugun) qoplanmaganlar ulushi |
| Asosiy hujjatlar | davrdagi `UsageDailyDocs` yig'indisi |

### Modullar
- **Tenantlar / Amallar** — davrda shu modulda amal qilgan tenantlar va amallar soni; **Trend** — oldingi davrga nisbatan % (oldingi 0 bo'lsa «yangi»).
- **Paketda bor, ishlatilmagan** — faol (Trial/Active/Grace) tenantlardan nechtasining paketida (`EditionModules.ModulesOf`) modul
  bor, lekin davrda amal yo'q. Bu «nimaning ustida ishlash kerak» ko'rsatkichi: o'qitish, onboarding, UX yoki modul sifati.
- **Eng ko'p amallar** — controller × amal turi reytingi (top 25; CSV'da hammasi).

### Tenantlar jadvali — health score (0–100)

| Qism | Ball |
|---|---|
| Yaqinlik (oxirgi kirish) | ≤ 1 kun 40 · ≤ 3 kun 30 · ≤ 7 kun 20 · ≤ 14 kun 10 · undan ko'p/hech qachon 0 |
| O'rinlar | 25 × min(1, davrdagi faol foydalanuvchilar / obuna foydalanuvchi limiti) |
| Modul qamrovi | 20 × ishlatilgan / paketdagi modullar (Core hisobga olinmaydi) |
| Trend | davr amallari ≥ oldingi (yoki oldingi 0, hozir > 0) 15 · ≥ 70% 10 · ≥ 40% 5 · aks holda (yoki 0 amal) 0 |

**Churn xavfi**: ball < 40 — Yuqori, < 60 — O'rta, aks holda Past (sinov tenanti uchun — konversiya xavfi). Saralash: ball (standart, xavflilari
tepada), MRR, oxirgi kirish, amallar, nom. Tenant nomi → uning Faollik sahifasi.

### Kohortalar va retensiya
Qator — tenant **yaratilgan oy** (oxirgi 6 oy); ustun H0…H11 — yaratilgandan keyingi 7 kunlik haftalar; qiymat — o'sha haftada
kamida bir marta faol bo'lgan tenantlar ulushi (hafta hali kelmagan tenantlar maxrajga kirmaydi; bo'sh katak — hali kelmagan).

### Funnel
Davrda TRIAL obunasi boshlangan tenantlar: **Sinov ochildi** → **Birinchi kirish** (kirish yozuvi bor) → **Birinchi hujjat** (hujjat
jadvallarida yozuv yoki Create amali; kirgan bo'lishi shart) → **3+ kun faol** (kamida 3 xil kunda faol, hujjat qadamidan o'tgan) →
**To'ladi** (pullik obuna bir marta to'langan). Namuna ma'lumotli sinovda demo hujjatlar birinchi yig'ishdayoq «hujjat» deb sanaladi —
shuning uchun bu qadamni namuna ma'lumotsiz sinovlar bo'yicha talqin qiling (paket/holat filtri bilan).

## 4. CSV
`?handler=Csv&section=tenants|modules|pages|cohorts` (joriy filtrlar bilan), UTF-8 BOM, `;` ajratuvchi (Excel uchun).

## 5. Tezlik va saqlash
Sahifa faqat agregat jadvallardan o'qiydi (bir necha ming qator); xom `UsageEvents` 90 kun, agregatlar `Usage:AggregateRetentionDays`
(760, kamida 730) — kunlik ish o'chiradi. Agregat kun har yangi yozuvdan keyin xom yozuvlardan qayta quriladi (idempotent).
Grafiklar: Chart.js 4.4.6 (MIT), `wwwroot/lib/chartjs` (CDN emas — CSP `script-src 'self'`).
