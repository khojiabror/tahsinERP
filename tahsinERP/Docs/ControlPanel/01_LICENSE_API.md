# License API — ERP ↔ markaziy panel kontrakti (v1)

Holat: 2026-10-18, ERP tomoni (`Services/LicenseSync/*`) va panel tomoni (`tahsinERP.ControlPanel/Api/LicenseApi.cs`, `04_PANEL_MVP.md`) tayyor.
Bog'liq: `00_PLAN.md` 3.2–3.3, `tahsinERP.Licensing/LicenseHelper.cs` (kalit formati v2).

## Umumiy

- Bazaviy URL: ERP sozlamasi `LicensePanelUrl` (Web.config appSettings) yoki `TAHSINERP_LICENSE_PANEL_URL` env.
  Masalan `https://admin.<domen>`. **Bo'sh bo'lsa sinxronlash o'chiq** (on-prem offline rejim) — ERP faqat qo'lda kiritilgan kalit bilan ishlaydi.
- Faqat HTTPS (ERP `http://` ni faqat `localhost` uchun qabul qiladi — test).
- Tana: `application/json; charset=utf-8`, maydonlar `snake_case`.
- Vaqt chegarasi: ERP har so'rovni 30 soniyada uzadi.
- **Ishonch modeli:** javobdagi `license_key` o'zi RSA imzoli (k1/k2) — ERP uni `ActivateCompanyLicense` orqali saqlaydi va barcha
  mavjud tekshiruvlar (imzo, CompanyID, InstallationId, muddat, soat muhri) ishlaydi. Panel javobini soxtalashtirgan odam yaroqli kalit yasay olmaydi.
  HMAC esa so'rovchini tasdiqlaydi (begona odam boshqa o'rnatmaning kalitini/holatini so'ray olmasin).

## 1. Ro'yxatdan o'tish

```
POST /api/license/register
{
  "reg_token": "RT-....",                                  // panel chiqargan bir martalik token (7 kun)
  "installation_id": "3F2504E0-4F89-11D3-9A0C-0305E82C3301", // ERP o'zi yaratgan (LICENSE_STATE), katta harf, D format
  "company_id": 1,                                         // ERP'dagi CompanyID (tenant reestri yoki Web.config)
  "version": "1.0.0.0",                                    // ERP assembly versiyasi
  "host": "firma1.<domen>"                                 // ixtiyoriy, SaaS tenant hosti
}
```

Javoblar:

| Kod | Tana | Ma'nosi |
|---|---|---|
| 200 | `{ "tenant_secret": "<base64, ≥32 bayt>", "license_key": "<v2 kalit yoki null>", "poll_hours": 6, "online_check": "optional", "offline_grace_days": 30 }` | Bog'landi. Token yopildi. |
| 400 | `{ "error": "bad_request", "message": "..." }` | Maydon yetishmaydi / installation_id GUID emas |
| 404 | `{ "error": "token_not_found" }` | Token yo'q yoki muddati o'tgan |
| 409 | `{ "error": "token_used" }` | Token boshqa installation_id bilan ishlatilgan |

- Shu `reg_token` + shu `installation_id` bilan qayta so'rov (javob yo'qolgan bo'lsa) — **idempotent**: o'sha `tenant_secret` qaytariladi.
- `tenant_secret` faqat shu javobda bir marta keladi; ERP uni tenant bazasida `LICENSE_STATE.PanelSecret` ga yozadi va `RegToken` ni o'chiradi.
- `license_key` bo'lsa ERP darhol faollashtiradi (trial/obuna kaliti). Panel bu kalitni AYNAN shu `installation_id` ga chiqaradi.

> Provisioning `InstallationId` ni bazaga oldindan YOZMAYDI — faqat `LICENSE_STATE.RegToken` ni. InstallationId'ni ERP o'zi yaratadi
> (HMAC soat muhri bilan birga); muhrsiz InstallationId `LicenseService` tomonidan qo'lda aralashuv deb bloklanadi.

## 2. Joriy kalitni olish (pull)

```
GET /api/license/{installation_id}
X-Tahsin-Timestamp: 1792224000          // Unix soniya, UTC
X-Tahsin-Nonce: 6f1c...                 // 16 bayt tasodifiy, hex (32 belgi)
X-Tahsin-Signature: <base64>
If-None-Match: "<etag>"                 // ERP'dagi faol kalitning SHA-256 hex'i (kichik harf), kalit bo'lmasa yuborilmaydi
```

Imzo:

```
string_to_sign = "GET" + "\n" + "/api/license/{INSTALLATION_ID}" + "\n" + timestamp + "\n" + nonce + "\n" + etag_or_empty
signature      = Base64( HMAC-SHA256( key = Base64Decode(tenant_secret), data = UTF8(string_to_sign) ) )
```

- `{INSTALLATION_ID}` — katta harfli GUID (yo'lda ham aynan shunday).
- `etag_or_empty` — `If-None-Match` qiymati qo'shtirnoqsiz, bo'lmasa bo'sh qator.
- Panel: `|now - timestamp| ≤ 300 s`, nonce 10 daqiqa ichida takrorlanmasin, imzo doimiy-vaqtli taqqoslash.

Javoblar:

| Kod | Tana | ERP nima qiladi |
|---|---|---|
| 200 | `{ "license_key": "<v2>", "etag": "<sha256 hex>", "poll_hours": 6, "online_check": "required", "offline_grace_days": 30 }` | Kalit faoldan farq qilsa `ActivateCompanyLicense`. Rad etilsa — sababini `LastSyncResult` ga yozadi. |
| 304 | — | O'zgarish yo'q |
| 401 | `{ "error": "bad_signature" }` / `"clock_skew"` / `"replay"` | Logga yozadi, keyingi urinishda qayta |
| 404 | `{ "error": "unknown_installation" }` / `"no_license"` | Logga yozadi (tenant panelda o'chirilgan/arxivda yoki hali kalit chiqmagan — faol obuna yo'q) |
| 410 | `{ "error": "revoked" }` | Logga yozadi. Kalit muddati o'zi tugaydi (bekor qilish = panel qisqa muddatli yangi kalit chiqaradi, 3.4) |

- `etag` = `lowercase(hex(SHA256(UTF8(license_key))))`. ERP ham shunday hisoblaydi.
- `poll_hours` ixtiyoriy (1–24); berilmasa 6.
- `online_check` / `offline_grace_days` (4-bosqich, ixtiyoriy; eski ERP e'tiborsiz qoldiradi): on-prem siyosati — `"required"` bo'lsa
  panel kalitni ≤ `offline_grace_days` kunga chiqaradi va pull'da yangilaydi; ERP ularni `LICENSE_STATE` ga yozadi va uzoq aloqa
  bo'lmasa (`required` — 3 kun, `optional` — grace kun) admin'ga banner ko'rsatadi. 304 javobida yo'q — oxirgi qiymat qoladi. 08_STAGE4.md 4.

## 3. ERP tomoni

| Narsa | Qayerda |
|---|---|
| HTTP klient (imzo, javoblar) | `Services/LicenseSync/LicensePanelClient.cs` |
| Orkestratsiya (register → pull → activate), taymer | `Services/LicenseSync/LicenseSyncService.cs` |
| Holat | `LICENSE_STATE`: `RegToken`, `PanelSecret`, `LastSyncUtc`, `LastSyncResult` |
| UI | License sahifasi: "Panelga ulash" (reg_token) va "Hozir tekshirish" |

- Taymer `Application_Start` da, birinchi ishga tushish 2 daqiqadan keyin, keyin har `poll_hours` (6). Ko'p-tenant rejimida reestrdagi
  har tenant uchun `TenantContext.Use(tenant)` ichida alohida.
- Panel ishlamasa — hech narsa bloklanmaydi, saqlangan kalit bilan ishlash davom etadi.
- **Panel ustun:** panelga ulangan o'rnatmada pull qaytargan kalit faoldan farq qilsa, u faollashtiriladi (qo'lda kiritilgan
  kalit ham keyingi pull'da panel kaliti bilan almashadi). Bekor qilish/pasaytirish shu yo'l bilan yetkaziladi (3.4).
- Yangi kalit faollashganda xodimlar seat'lari yangi kalitga ko'chiriladi (joy kam bo'lsa — birinchi berilganlar qoladi).
- 2026-10-17 lokal E2E: soxta panel (PowerShell HttpListener) bilan register → BIZNES kalit faollashdi, imzoli pull → 304.
- Testlar: `tahsinERP.Tests/LicenseSync/*` — soxta panel (HttpMessageHandler) shu hujjatdagi imzo qoidasini mustaqil tekshiradi.

## Ochiq savollar (panel 1-bosqichi)
- `tenant_secret` ni almashtirish (rotate) endpointi — keyinroq (`POST /api/license/rotate`, eski secret bilan imzolanadi).
- Push-signal (to'lovdan keyin darhol) — hozircha yo'q, ERP "Hozir tekshirish" yoki 6 soatlik pull.
