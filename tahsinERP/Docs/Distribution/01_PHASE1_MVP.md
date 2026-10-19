# Distributsiya — 1-bosqich (MVP): batafsil reja

Holat: 2026-10-19 — reja tasdiqlangan (8-bo'lim takliflari qabul qilindi), S0 (10-bo'lim) va S1 (11-bo'lim) bajarildi. Umumiy reja: `00_PLAN.md`.
Qabul qilingan qarorlar: **yangi distributsiya modeli** (alohida `D_ORDERS`, mavjud `S_ORDERS` ga tegilmaydi); mobil ilova o'rniga **Telegram bot + Mini App**; bot AWS'dagi o'z serverimiz uchun to'g'ri arxitekturada quriladi (cPanel vaqtinchalik yechimlari ko'chirilmaydi).

## 1. Maqsad va chegaralar

**Natija:** agent do'konda Telegram orqali buyurtma oladi → ofis tasdiqlaydi (limit, narx, rezerv) → ombor yig'adi va jo'natadi → mijoz qarzi va to'lovlari real vaqtda ko'rinadi → rahbar kunlik yakunni oladi.

**Qabul mezoni (TZ):** 5 agent bir hafta faqat tizimda ishlaydi; qoldiqlar inventarizatsiya bilan mos keladi.

**Kiradi:** ma'lumotnomalar (hudud, mijoz kartasi, o'lchov birliklari, shtrix-kod, narx turlari), buyurtma va holatlari, rezerv, jo'natish, o'zi olib ketish, qaytarish, mijoz qarzi va muddati, to'lovlar, agent pulini kassaga topshirish, kassalar o'rtasida o'tkazma, agent boti (kun boshi, tashrif, buyurtma, to'lov), rahbar bildirishnomalari, asosiy hisobotlar.

**Kirmaydi (keyingi bosqichlar):** reys/mashina/ekspeditor (2), marshrutni xaritada rejalash (2), KPI ish haqi (2), ovozdan buyurtma (2), aksiyalar (3), landed cost va pereso'rtitsa (3), POS (5), SaaS paneli (4), mijozning o'zi buyurtma berishi (6). 1-bosqichda yetkazish «Jo'natildi → Yetkazildi» ni operator yoki agent belgilaydi.

---

## 2. Texnik qarorlar

### 2.1 Branch
`feature/distribution` — `master`dan (optimization/core to'liq master ichida). Har kichik qadam alohida commit; master'ga merge faqat sizning ruxsatingiz bilan.

### 2.2 Modul joylashuvi
`Areas/Distribution/` (MVC Area): `Controllers`, `Views`, `Models`, `Services`, `Resources`. Mavjud modullardan ajralgan, menyu va ruxsatlar alohida — keyin `Edition` bo'yicha yoqish/o'chirish oson.

### 2.3 Ma'lumotlar: `dst` sxemasi + EF6 Code-First kontekst
EDMX (275 entity) ga yangi jadval qo'shish og'ir va parallel branch'larda konflikt beradi. Shuning uchun:
- Yangi jadvallar **`dst` sxemasida**, `DistributionDbContext` (EF6 Code-First, migratsiyasiz — SQL skriptlar `Database_Migrations/DST_*.sql`).
- Mavjud jadvallarga (`PRODUCTS`, `CUSTOMERS`, `WAREHOUSES`, `USERS`, `FIN_ACCOUNTS`) faqat ID orqali bog'lanadi; `CUSTOMERS` va `PRODUCTS` o'zgarmaydi — qo'shimcha maydonlar 1:1 «profil» jadvallarida (`dst.CUSTOMER_PROFILES`, `dst.PRODUCT_PROFILES`).
- Ombor (FIFO) va moliya amallari **mavjud servislar orqali** (`WarehouseFlowService`, `FifoAllocatorService`, `FinancePostingService`) — ular `DBTHSNEntities` bilan ishlaydi. Ikki kontekst **bitta `SqlConnection` va bitta tranzaksiyani** baham ko'radi (`Database.UseTransaction`). Bu S0'da birinchi spike sifatida tekshiriladi; ishlamasa — distributsiya jadvallari EDMX'ga qo'shiladi (B rejasi).
- Foyda: keyin EF Core'ga ko'chish oson (Code-First), EDMX konfliktlari yo'q.

### 2.4 Hujjat holati va audit
- Barcha `dst` hujjatlari: `Draft → Posted → Cancelled` (`CancelReason`, `CancelledBy`, `CancelledAt`). Posted hujjat o'chirilmaydi.
- `DistributionDbContext.SaveChanges` override'i `ChangeTracker` dan **eski → yangi qiymatlarni** `dst.AUDIT_TRAIL` ga yozadi (Entity, Key, Field, Old, New, UserID, Time, Source = web/bot/api). Mavjud `AuditLogQueue` orqali, so'rovni sekinlashtirmasdan. Keyinroq EDMX kontekstiga ham shu mexanizm ulanadi.

### 2.5 Ruxsatlar
- Yangi controllerlar `PERMISSIONMODULES` ga yoziladi (mavjud `[Permission]` filtri ishlaydi).
- **Qator darajasi:** `dst.USER_SCOPES` (UserID, ScopeType = Warehouse | Territory | Team | Customer, ScopeID). Agent — o'z mijozlari, supervayzer — o'z jamoasi, omborchi — o'z ombori. Bitta `ScopeService` barcha so'rovlarga filtr qo'shadi (web, API, bot bir xil).
- **Maydon darajasi:** maxsus ruxsatlar `Dst.SeeCost` (xarid narxi, tannarx, foyda), `Dst.OverrideCredit`, `Dst.DiscountUnlimited`. View'larda va API javoblarida shu bo'yicha yashiriladi.

### 2.6 Til
`Areas/Distribution/Resources/*.resx` (uz-Latn asosiy, ru). Foydalanuvchi tilini profildan oladi; bot ham shu resurslardan foydalanadi. Eski modullar 1-bosqichda o'girilmaydi.

### 2.7 ERP API
`/api/v1/dst/*` — ASP.NET Web API 2 shu loyihada. Faqat bot servisi chaqiradi (servisdan-servisga): `Authorization: Bearer <service JWT>` + `X-Acting-User: <ERP UserID>`; ERP shu foydalanuvchi ruxsati va scope'i bilan ishlaydi. Idempotentlik: yozuvchi so'rovlarda `Idempotency-Key` (bot qayta urinsa ikki buyurtma bo'lmasin).

### 2.8 Fon ishlari (ERP tomoni)
**Hangfire** (SQL Server storage, .NET 4.8 da ishlaydi): bildirishnoma outbox'ini yuborish, kunlik yakun (Toshkent 20:00), muddati o'tgan qarzlarni qayta hisoblash. cPanel cron endpointlari kabi yechimlar yo'q.

---

## 3. Telegram bot arxitekturasi (AWS)

```
 Telegram ──HTTPS webhook (secret_token)──► tahsinERP.Bot  (ASP.NET Core .NET 10)
                                              │ 1) update → bot.INBOX (update_id UNIQUE) → darhol 200
                                              │ 2) BackgroundService ishchilari INBOX'ni oladi
                                              │    (chat bo'yicha ketma-ket, chatlar o'rtasida parallel)
                                              │ 3) handler → ERP /api/v1 (tenant URL reestrdan)
                                              │ 4) javoblar → bot.OUTBOX → rate-limiter → Telegram
                                              │
  Mini App (SPA, app.<domen>) ──initData──►  BFF endpointlari (initData HMAC → qisqa JWT) ──► ERP /api/v1
                                              │
  ERP (har tenant) ── tranzaksion outbox (dst.NOTIFICATIONS) ──Hangfire──► POST /internal/notify ──► OUTBOX
                                              │
  Rasmlar (selfi, tokcha, imzo) ── getFile ──► S3 (tenant/sana/...) — ERP faqat kalitni saqlaydi
```

Asosiy tamoyillar:
- **Webhook qoladi**, lekin faqat qabul qiladi: yangilanish bazaga yoziladi va darhol `200`. Qayta ishlash fon ishchilarida — uzoq amal (PDF, ERP chaqiruvi) Telegram timeout'iga ta'sir qilmaydi; server qayta ishga tushsa, ishlanmagan yangilanishlar yo'qolmaydi. `update_id` UNIQUE — dublikat yo'q.
- **Outbox + rate-limiter:** global ~30 xabar/s, bitta chatga ~1 xabar/s; 429 da `retry_after`. ERP bildirishnomalari biznes tranzaksiyasi bilan bir vaqtda `dst.NOTIFICATIONS` ga yoziladi (tranzaksion outbox) — buyurtma saqlanmasa xabar ham ketmaydi, saqlansa albatta ketadi.
- **Suhbat holati** (savat, qadam) — `bot.SESSIONS` jadvalida (keyin kerak bo'lsa Redis). Fayl yoki xotira emas.
- **Bitta umumiy bot, ko'p tenant:** markaziy `tahsin_bot` bazasi — `BOT_LINKS` (TelegramUserId → TenantSlug, ERP UserID yoki CustomerID, rol). Bog'lash: ERP'da foydalanuvchi kartasida «Telegram'ga ulash» → bir martalik kod → `t.me/<bot>?start=<kod>` → bot ERP API orqali kodni tekshiradi.
- **Mini App** — bot servisining o'zida statik SPA (katalog, savat, buyurtma, to'lov, imzo). Backend bot servisi (BFF): `initData` imzosini tekshiradi, qisqa JWT beradi, ERP'ga o'z servis tokeni bilan murojaat qiladi. ERP API'si tashqariga ochilmaydi.
- **Deploy:** 1-bosqichda ERP bilan bitta AWS serverda alohida sayt (`bot.<domen>`, `app.<domen>`), HTTPS wildcard sertifikat. `tahsin_bot` — o'sha SQL Server'da alohida baza. Linux/Docker'ga keyin ko'chiriladi (Control Panel bilan birga).
- **Taskly'dan olinadi:** tasdiqlash/tahrirlash oqimi, markaziy rol tekshiruvi, 429 qayta urinish, edit-yoki-yangi xabar, Toshkent vaqti/dam olish kuni mantiqi. **Olinmaydi:** javobdan oldin ishni tugatish, cron URL, JSON fayl store, Sheets.

---

## 4. Ma'lumotlar modeli (`dst` sxemasi)

| Jadval | Asosiy maydonlar |
|---|---|
| `TERRITORIES` | ID, ParentID, Level (Viloyat/Tuman/Hudud), Name, NameRu |
| `CUSTOMER_PROFILES` | CustomerID (PK, `dbo.CUSTOMERS`), INN, Phone2, OwnerName, Landmark, Lat, Lng, PhotoKey, PointType, Category (A/B/C), TerritoryID, AgentUserID, PriceTypeID, VisitDays (bitmask), CreditLimit, PaymentDays, IsApproved, CreatedVia |
| `PRODUCT_PROFILES` | ProductID (PK), NameRu, BrandID, ManufacturerID, SubCategoryID, IsSaleActive, IsBulky, ImageKey |
| `PRODUCT_UNITS` | ID, ProductID, UnitID, Factor (asosiy birlikka), Weight, Volume, IsDefaultSale |
| `PRODUCT_BARCODES` | Barcode (UNIQUE), ProductID, ProductUnitID |
| `BRANDS`, `MANUFACTURERS`, `SUB_CATEGORIES` | oddiy ma'lumotnomalar |
| `PRICE_TYPES` | ID, Name, IsDefault |
| `PRICE_LIST_ITEMS` | PriceTypeID, ProductID, Price (asosiy birlik uchun), MinPrice, ValidFrom, CreatedBy — tarix ValidFrom bo'yicha |
| `CUSTOMER_PRICES` | CustomerID, ProductID, Price, ValidFrom, ValidTo |
| `WAREHOUSE_SETTINGS` | WarehouseID, AllowNegative, MinStockAlert |
| `PRODUCT_STOCK_LIMITS` | WarehouseID, ProductID, MinQty |
| `STOCK_RESERVATIONS` | WarehouseID, ProductID, Qty (asosiy birlik), OrderLineID |
| `SALES_TEAMS`, `USER_SCOPES` | jamoa (supervayzer) va ko'rinish doirasi |
| `DISCOUNT_LIMITS` | RoleID, MaxPercent |
| `D_ORDERS` | ID, DocNo, Date, CustomerID, AgentUserID, WarehouseID, PriceTypeID, Status, DeliveryDate, Comment, Subtotal, DiscountTotal, Total, WeightKg, CreditOverrideBy, Source (web/bot), SignatureKey, CancelReason… |
| `D_ORDER_LINES` | OrderID, ProductID, ProductUnitID, Qty, QtyBase, Price, DiscountPct, DiscountSum, Amount, QtyShipped |
| `D_ORDER_STATUS_LOG` | OrderID, From, To, UserID, Time, Note |
| `D_SHIPMENTS` (+ lines) | jo'natish hujjati; mavjud ombor chiqimiga (`EXPENSES`) bog'lanadi, FIFO tannarxni saqlaydi (foyda hisobi uchun) |
| `D_RETURNS` (+ lines) | OrderID (ixtiyoriy), ReasonID, WarehouseID (yaroqli yoki brak), PhotoKey, Amount |
| `CUSTOMER_LEDGER` | CustomerID, Date, DocType (Shipment/Payment/Return/Adjustment/Opening), DocID, Debit, Credit, DueDate, AgentUserID |
| `D_PAYMENTS` | CustomerID, Amount, Method (naqd/Click/Payme/Uzum/o'tkazma), ReceivedBy (agent yoki kassa), FinAccountID (kassada bo'lsa), HandoverID |
| `CASH_HANDOVERS` | AgentUserID, Amount, FinAccountID, Status (Sent/Confirmed/Disputed), ConfirmedBy, Difference |
| `VISITS` | AgentUserID, CustomerID, PlannedDate, CheckInAt, Lat, Lng, DistanceM, IsLive, Result (Order/Payment/NoOrder), NoOrderReasonID, Photos |
| `AGENT_DAYS` | AgentUserID, Date, StartAt, StartLat/Lng, SelfieKey, EndAt |
| `REASONS` | Type (Return/NoOrder/Cancel/WriteOff), Name, NameRu |
| `NOTIFICATIONS` | tranzaksion outbox: Type, Recipient, Payload, Status, Attempts |
| `AUDIT_TRAIL` | 2.4 ga qarang |

Kassalar o'rtasida o'tkazma — mavjud `FIN_TRANSACTIONS` da juft yozuv (chiqim + kirim, bitta `TransferGroupID`), alohida jadval shart emas.

---

## 5. Buyurtma holat mashinasi

| Holat | Kim | Tekshiruv / natija |
|---|---|---|
| **Yangi** | agent (bot), operator | Narx avtomatik: mijoz maxsus narxi → mijoz narx turi → standart. Min narx va chegirma limiti shu yerda ham tekshiriladi |
| **Tasdiqlangan** | operator, supervayzer | Qarz + buyurtma ≤ limit va muddati o'tgan qarz yo'q, aks holda blok → `Dst.OverrideCredit` egasi botdagi tugma yoki web orqali ruxsat beradi. Rezerv qo'yiladi (mavjud qoldiq yetmasa — qator «kutmoqda»). Ombor: barcha tovar bor ombor taklif qilinadi |
| **Yig'ilmoqda** | omborchi | Yig'ish varaqasi (PDF) |
| **Jo'natildi** | omborchi | `D_SHIPMENT` → FIFO bo'yicha ombordan chiqim, rezerv yechiladi, `CUSTOMER_LEDGER` ga debit (DueDate = sana + PaymentDays). Qisman jo'natish mumkin, qolgani buyurtmada qoladi |
| **Yetkazildi** | operator / agent | 1-bosqichda qo'lda; 2-bosqichda ekspeditor. O'zi olib ketishda «Jo'natildi» bilan birga |
| **Yopildi** | tizim | Buyurtma bo'yicha qarz to'liq yopilganda |
| **Bekor** | supervayzer, rahbar | Sabab majburiy; Jo'natilgangacha — rezerv yechiladi; jo'natilgandan keyin faqat qaytarish hujjati orqali |

Parallellik: tasdiqlashda rezerv `sp_getapplock` (ombor+tovar) bilan — ikki operator bir vaqtda oxirgi qoldiqni ikki marta rezerv qila olmaydi.

---

## 6. Ish qadamlari

| # | Qadam | Ichida | Muddat | Tayyor bo'lgani |
|---|---|---|---|---|
| **S0** | Poydevor | Branch, Area skeleti, `DistributionDbContext`, `DST_001` skript, **bitta tranzaksiya spike'i**, AUDIT_TRAIL, hujjat holati bazasi, ruxsat modullari, `ScopeService`, resx, Hangfire | 1 hafta | Spike testi: bitta tranzaksiyada `dst` yozuvi + FIFO chiqim, xatoda ikkalasi ham bekor |
| **S1** | Ma'lumotnomalar | Hududlar, mijoz profili (+ Excel import, xaritada nuqta), tovar birliklari / shtrix-kod / brend (+ Excel import), narx turlari va narx varag'i (+ Excel, kategoriya bo'yicha %), maxsus narx, jamoa va scope, chegirma limitlari, sabablar, ombor sozlamalari | 1.5 hafta | 500+ tovar va 200+ mijoz Excel'dan yuklanadi |
| **S2** | Buyurtma va ombor | `D_ORDERS` web ekranlari, narx mexanizmi, limit/override, rezerv, ombor taklifi, yig'ish varaqasi, jo'natish (FIFO), qisman jo'natish, o'zi olib ketish, bekor qilish, nakladnoy PDF, haqiqiy/rezerv/mavjud qoldiq hisoboti | 2 hafta | Buyurtma Yangi → Yopildi to'liq yo'lni bosadi, qoldiq va rezerv to'g'ri |
| **S3** | Pul va qarz | `CUSTOMER_LEDGER`, kassada va agent orqali to'lov, agent qo'lidagi pul, kassaga topshirish va tasdiq, kassalar o'rtasida o'tkazma, qaytarish (yaroqli/brak ombor, qarzni kamaytiradi), qarz muddati (0–7/8–30/31–60/60+), akt sverki PDF, kirish qoldig'i (opening balance) importi | 1.5 hafta | Mijoz balansi = buyurtma − to'lov − qaytarish, akt sverki bilan mos |
| **S4** | API, bot, Mini App | `/api/v1/dst`, `tahsinERP.Bot` skeleti (inbox/outbox, ishchilar, sessiya), bog'lash, **agent**: kun boshi (live location + selfi), bugungi mijozlar (tashrif kuni bo'yicha), «keldim» (radius sozlamasi, 4.4 cheklovlari bilan), mijoz kartasi (qarz, oxirgi buyurtmalar, tavsiya — oxirgi 90 kun), Mini App'da buyurtma va imzo, to'lov qabul qilish, «buyurtmasiz» sababi, tokcha rasmi, yangi nuqta qo'shish (tasdiq bilan), kun yakuni; **rahbar/supervayzer**: limit oshishi (ruxsat tugmasi), katta buyurtma, bekor qilish, kunlik yakun; **omborchi**: yangi tasdiqlangan buyurtma xabari | 3 hafta | Agent do'konda 3 daqiqada buyurtma beradi, ofisda darhol ko'rinadi |
| **S5** | Hisobotlar | Rahbar paneli (tushum, yalpi foyda FIFO tannarxda, mijoz qarzi va muddati o'tgani, kassalardagi pul, qoldiq pulda), savdo: agent/mijoz/tovar/hudud/kategoriya kesimida, tashrif reja/fakt va samaradorlik, agentlar topshirgan pul, Excel eksport | 1 hafta | Rahbar bir ekranda kunlik holatni ko'radi |
| **S6** | Pilot | Real ma'lumot bilan sinov, 5 agent bilan bir hafta, tuzatishlar, uz/ru qo'llanma (qisqa) | 1 hafta + pilot haftasi | TZ qabul mezoni |

**Jami: ~11 hafta** + pilot haftasi.

---

## 7. Telegram cheklovlari (mijozga aytiladi)

| TZ talabi | 1-bosqichda | Izoh |
|---|---|---|
| Oflayn ishlash | ❌ | Mini App savatni lokal saqlaydi, aloqa kelganda yuboradi; buyurtma tasdig'i internet talab qiladi |
| «Keldim» 100–200 m radiusda | 🟡 | Faqat **live location** qabul qilinadi, forward qilingan joylashuv rad etiladi, masofa hisoblanadi |
| Soxta GPS'ni aniqlash | ❌ | Telegram'da imkonsiz; faqat bilvosita belgilar (live, aniqlik, vaqt, selfi) |
| Kunlik GPS-trek | 🟡 | Live location yangilanishlari yoziladi (2-bosqichda xaritada) |
| Shtrix-kod | 🟡 | Mini App'da kamera + JS kutubxona |
| Ekranda imzo | ✅ | Mini App canvas |

---

## 8. Boshlashdan oldin tasdiqlash kerak

1. **Agentlar litsenziya o'rnini egallaydimi?** Har agent ERP `USERS` yozuvi bo'ladi (scope, ruxsat, audit uchun). Taklif: faqat bot/Mini App ishlatadigan foydalanuvchilar uchun alohida, arzonroq «mobil o'rin» turi (Control Panel paketlariga qo'shiladi).
2. **Tannarx:** foyda FIFO partiya tannarxi bo'yicha (mavjud mexanizm) — TZ'dagi «o'rtacha tortilgan» o'rniga.
3. **1-bosqichda yetkazish:** reyssiz, «Yetkazildi» ni operator yoki agent belgilaydi — ma'qulmi?
4. **Mavjud mijozlar:** distributsiya mijozlari `dbo.CUSTOMERS` ga yoziladi (profil `dst` da) — ishlab chiqarish mijozlari bilan bitta ro'yxat. Ma'qulmi, yoki faqat distributsiya mijozlari alohida filtrlansinmi (`CUSTOMER_PROFILES` bor-yo'qligi bo'yicha — taklif)?
5. **Bot nomi va domen** — Control Panel'dagi ochiq domen savoli bilan bir vaqtda hal bo'ladi; ishlab chiqishda test bot va lokal tunnel ishlatiladi.

---

## 9. Tasdiqlangan qarorlar (2026-10-19)

8-bo'limdagi barcha takliflar qabul qilindi: agentlar uchun alohida «mobil o'rin» turi (Control Panel paketlariga), foyda FIFO tannarxda, 1-bosqichda reyssiz yetkazish, distributsiya mijozlari `dbo.CUSTOMERS` da (`dst.CUSTOMER_PROFILES` bor-yo'qligi bo'yicha filtr), bot nomi/domen keyin.

## 10. S0 natijasi (2026-10-19, branch `feature/distribution`)

| Ish | Joyi |
|---|---|
| `dst` sxemasi: `AUDIT_TRAIL`, `USER_SCOPES`, `USER_SETTINGS`; ruxsat modullari `DstHome`, `DstSeeCost`, `DstOverrideCredit`, `DstDiscountUnlimited` (Admin'ga beriladi) | `Database/Migrations/013_DST_Foundation.sql` (idempotent; lokal bazada qo'llangan, zaxira `DBTHSN-IPACK_pre_012_DST_20261019.bak`) |
| Area va marshrut `/Distribution/{controller}/...`, controllerlar `Dst*` prefiksi bilan | `Areas/Distribution/DistributionAreaRegistration.cs` |
| `DistributionDbContext` (EF6 Code-First, EDMX'siz) — SaveChanges'da audit (I/U/D, eski→yangi) va Posted hujjatni o'chirish/Cancelled'ni tahrirlash taqiqi | `Areas/Distribution/Data/DistributionDbContext.cs` |
| **Bitta tranzaksiya:** `DistributionUnitOfWork` — `Dst` + `Erp` (EDMX) bitta SqlConnection/tranzaksiyada; `DBTHSNEntities.CreateOnConnection` | `Areas/Distribution/Data/DistributionUnitOfWork.cs`, `Models/DBTHSNModel.Context.SharedConnection.cs` |
| Hujjat holati `Draft → Posted → Cancelled(sabab)` | `Data/Entities/DstDocument.cs`, `Services/DocumentLifecycle.cs` |
| Qator darajasidagi ko'rinish (`ScopeFilter`: yozuv yo'q — cheklovsiz; bor — faqat ro'yxat; Admin — cheklovsiz) | `Services/ScopeService.cs` |
| Maxsus huquqlar | `Services/DstPermissions.cs` |
| uz/ru: `DstStrings.resx` + `.ru.resx`, foydalanuvchi tili `dst.USER_SETTINGS` (faqat UI tili almashadi) | `Services/DstLocalization.cs`, `Resources/` |
| Edition: `ProductModule.Distribution` (KORP va TRIAL ichida) | `Services/Editions/EditionModules.cs` |
| Testlar: hujjat holati, scope, lokalizatsiya kalitlari, **spike** (bitta tranzaksiyada `dst` yozuvi + `PRODUCT_STOCKS` o'zgarishi → rollback ikkalasini bekor qiladi), audit I/U/D | `tahsinERP.Tests/Distribution/*` — 101/101 o'tdi |

B rejasi (distributsiya jadvallarini EDMX'ga qo'shish) kerak bo'lmadi — spike ishladi.

Ko'chirildi: **Hangfire** S4'ga (bildirishnoma outbox'i bilan birga keladi, undan oldin fon ishi yo'q). Menyu bandi S1'da (birinchi haqiqiy ekranlar bilan) qo'shiladi.
Tekshirilmagan: `/Distribution` sahifasi brauzerda ochib ko'rilmagan (login kerak) — build va testlar o'tdi.

## 11. S1 natijasi (2026-10-19, branch `feature/distribution`)

Migratsiya `Database/Migrations/016_DST_Directories.sql` (014/015 raqamlari float→decimal ishiga band — lokal bazada `014_StockProduction_FloatToDecimal`, `015_ReferenceDims_FloatToDecimal` qo'llangan, hali master'da yo'q). Lokal bazada qo'llangan (zaxira `DBTHSN-IPACK_pre_014_DST_20261019.bak`), bo'sh baza uchun ruxsat modullari `Seed/105` da.

| Sahifa (`/Distribution/...`) | Nima qiladi |
|---|---|
| `DstTerritories` | Viloyat → tuman → hudud daraxti; tsikl va chuqurlik (3) tekshiruvi; ishlatilgan hudud o'chirilmaydi |
| `DstCustomers` | Mijozlar ro'yxati (server-side, hudud/agent/toifa/tasdiq filtri), karta: `dbo.CUSTOMERS` + `dst.CUSTOMER_PROFILES` bitta tranzaksiyada (STIR, GPS, nuqta turi, A/B/C, agent, narx turi, tashrif kunlari, limit, kechiktirish); agent qo'shgan nuqtani tasdiqlash; CRM mijozini distributsiyaga ulash. Ko'rinish: CUSTOMER/TERRITORY scope + agent ierarxiyasi (agent — o'zi, supervayzer — jamoasi) |
| `DstProducts` | Tovar ro'yxati (shtrix-kod bo'yicha qidiruv), distributsiya kartasi: rus nomi, brend, ishlab chiqaruvchi, subkategoriya, og'irlik/hajm, sotuvda/katta/tortiladigan; qo'shimcha birliklar (Factor, odatiy sotish birligi); shtrix-kodlar (EAN/UPC nazorat raqami, takror taqiqlanadi). Tovarning o'zi — mavjud Product modulida |
| `DstPrices` | Tovar × narx turi jadvali, katakchada tahrirlash (narx + min narx), tarix, kategoriya bo'yicha ommaviy % (yaxlitlash bilan, bitta tranzaksiya); mijozning maxsus narxlari (muddatli, to'xtatish tarixni saqlaydi) |
| `DstTeams` | Jamoalar (supervayzer), agentlar/ekspeditorlar (ERP foydalanuvchisi, ombor, telefon), foydalanuvchi scope'lari (ombor/hudud/mijoz) |
| `DstDictionaries` | Brendlar, ishlab chiqaruvchilar, subkategoriyalar, narx turlari (standart — bittagina), sabablar (qaytarish / buyurtmasiz / bekor / hisobdan chiqarish) |
| `DstSettings` | Rol bo'yicha chegirma limiti, ombor sozlamalari (savdoda, minusga ruxsat, zona), minimal qoldiqlar |
| `DstImport` | Excel: mijozlar (STIR/nom bo'yicha yangilash), tovarlar (birlik 2/3 + shtrix-kodlar, brend avtomatik), narxlar (narx turi ustunlari + "min"). Shablon yuklab olinadi; avval hamma qator tekshiriladi, xato bo'lsa hech narsa yozilmaydi; "faqat tekshirish" rejimi |

Servislar: `PriceResolver`/`PricingService` (maxsus narx → mijoz narx turi → standart; o'zgarmagan narx tarixga yozilmaydi), `UnitMath`/`UnitService`, `DirectoryRules` (STIR 9/14, telefon +998, GPS, A/B/C, shtrix-kod GTIN, hudud), `CustomerService`, `ImportService`, `ScopeService.AgentsFor`.

Umumiy tuzatishlar:
- Area ichidan global filtrlar (litsenziya, paket, ruxsat, parol almashtirish), `BaseController.NotAuthorized` va `_Layout` havolalari `/Distribution/...` ga noto'g'ri yo'naltirardi — `area = ""` qo'shildi.
- `uz-UZ` madaniyatida o'nlik ajratkich `,`, `<input type=number>` esa `.` yuboradi — distributsiya so'rovlarida `FormCulture` (uz-UZ, ajratkich `.`) va `DstModelBinder` (ikkala ko'rinish).
- `ProductRef` da `Weight` yo'q, import `PRODUCTS.Weight` ga yozmaydi (float→decimal ishi bilan to'qnashmaslik uchun; og'irlik `PRODUCT_PROFILES.BaseWeightKg` da).

Testlar: 120/120 (yangi: narx qoidalari, birliklar, STIR/telefon/GPS/shtrix-kod, hudud, tashrif kunlari, decimal parsing, resx kalitlari to'liqligi; integratsiya — har DbSet ↔ sxema, faqat-o'qish himoyasi, mijoz saqlash + audit, narx aniqlash, Excel import tekshirish rejimi va xato qatorlari).

Tekshirilmagan: sahifalar brauzerda ochilmadi — test bazasida litsenziya yo'q (kalit RSA bilan imzolanadi, chetlab o'tilmadi). View'lar `aspnet_compiler` bilan kompilyatsiya qilib tekshirildi.

## 12. S2 natijasi (2026-10-19, branch `feature/distribution`)

Migratsiya `Database/Migrations/018_DST_Orders.sql` (017 master'da float→decimal'ga band): `D_ORDERS`, `D_ORDER_LINES`, `D_ORDER_STATUS_LOG`, `STOCK_RESERVATIONS`, `D_SHIPMENTS` (+lines), `CUSTOMER_LEDGER`, `SETTINGS` (`MinOrderAmount`); ruxsat modullari `DstOrders`, `DstStock` (+ Seed/105). Lokal bazada qo'llangan (zaxira `DBTHSN-IPACK_pre_018_DST_20261019.bak`).

| Qism | Nima qiladi |
|---|---|
| `OrderService.SaveDraft` | Narx har doim narx varag'idan (maxsus → narx turi → standart), birlik × Factor; chegirma (qator × buyurtma samarali %) rol limiti bilan; chegirmadan keyingi narx min narxdan past bo'lmasin (`DstDiscountUnlimited` ozod); sotuvdan olingan tovar rad; og'irlik/hajm; `ClientRequestId` — bot qayta yuborsa ikki buyurtma bo'lmaydi. Hamma tekshiruv o'zgarishdan oldin |
| `Confirm` | Mijoz tasdiqlangan, yetkazish uchun minimal summa, qarz + buyurtma ≤ limit va muddati o'tgan qarz yo'q (bo'lmasa `CreditBlockedException`; `DstOverrideCredit` + izoh bilan ruxsat); ombor tanlanmasa — savdo omborlaridan to'liq (yoki eng ko'p) qoplaydigani; rezerv `sp_getapplock` (ombor+tovar) ostida, yetmagani "kirimni kutmoqda" |
| `Ship` | `dbo.EXPENSES` + `EXPENSE_PRODUCTS` → `StockLedger.IssueLine` (sof FIFO, DLOC ham) → `StockCostService` tannarx; boshqa buyurtmalar rezervi daxlsiz; rezerv kamayadi; `CUSTOMER_LEDGER` debet (muddat = sana + PaymentDays); qisman/to'liq; o'zi olib ketishda darhol "yetkazildi" |
| Boshqa | `Unconfirm` (rezerv yechiladi), `Rereserve`, `StartPicking`, `CloseRemainder` (sabab bilan), `MarkDelivered`, `Cancel` (faqat jo'natilmagan, sabab majburiy), `DeleteDraft` |
| Sahifalar | `DstOrders`: ro'yxat (holat/agent/ombor/sana filtri; agent — o'zi, supervayzer — jamoasi, mijoz/ombor scope), forma (mijoz → qarz/limit paneli, tovar qidiruvi va shtrix-kod skaneri, birlik, narx, chegirma, mavjud qoldiq), karta (holatga qarab amallar, limitdan oshganda ruxsat oynasi, jo'natish miqdorlari, jo'natishlar, yalpi foyda — `DstSeeCost` bilan, tarix), chop etish: yig'ish varaqasi va nakladnoy (brauzer → PDF). `DstStock`: haqiqiy / rezerv / mavjud, ombor yoki jami, minimaldan past |

Testlar 128/128 (yangi: holat mashinasi, hisob-kitob, limit/muddati o'tgan qarz, chegirma/min narx, ombor taklifi; integratsiya — kirim (DLOC joylashuv bilan) → qoralama → tasdiq/rezerv → 12 + 18 dona jo'natish (FIFO tannarx 40 000, qarz 1 567 500, muddat +10 kun) → yetkazildi; limit bloki va ruxsat; bekor rezervni yechadi; idempotentlik; hammasi rollback). View'lar: `Run-RazorCheck.ps1` — 485 view, 0 xato.

Testlar topgan va tuzatilgan xatolar: EF to'plamni `null` bilan solishtira olmaydi (`StockService.Levels`); qoralamani o'chirishda jurnal FK tartibi; rad etilgan `SaveDraft` kontekstda yarim o'zgarish qoldirardi.

Qolgan (S3'ga): jo'natishni bekor qilish (`ReverseIssueLine`) va qaytarish hujjati, to'lovlar va `Closed` holati (qarz yopilganda), moliyaga (FIN_TRANSACTIONS) yozish. Sahifalar brauzerda ochilmagan (test bazasida litsenziya yo'q).
