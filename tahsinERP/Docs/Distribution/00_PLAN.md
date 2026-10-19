# tahsinERP — Distributsiya moduli: gap-tahlil va reja

Holat: 2026-10-18. Manba: «ТЗ Дистрибуция #1 от 15.10.2026» (qurilish/xo'jalik mollari ulgurji savdosi, agentlar + ekspeditorlar + do'konlar + SaaS).
Bog'liq: `Docs/ControlPanel/00_PLAN.md` (tenant, tarif, to'lov), `Docs/Audit-2026-10/00_PLAN.md` (core optimizatsiya).

Qarorlar (foydalanuvchi, 2026-10-18): TZ'dagi **alohida mobil ilova qilinmaydi** — o'rniga Telegram bot + Mini App (4-bo'lim); **yangi distributsiya modeli** (`D_ORDERS`, 3.1); bot AWS'dagi o'z serverimiz uchun quriladi (webhook → navbat → fon ishchilari, `01_PHASE1_MVP.md` 3-bo'lim); xavflardan faqat Telegram cheklovlari hisobga olinadi. Batafsil 1-bosqich: `01_PHASE1_MVP.md`.

---

## 1. Qisqa xulosa

**Tizim hozir bu TZ'ga tayyor emas — taxminan 25–30% asos bor.** Lekin bu asos eng qiyin qism: ko'p ombor, FIFO partiyalar, ombordan omborga ko'chirish (qabul tasdig'i bilan), moliya hisoblari va P&L, ruxsatlar, soft-delete, Excel import, litsenziya va tenant poydevori.

Asosiy farq — **savdo modeli**. tahsinERP savdosi B2B shartnoma asosida qurilgan (`S_CONTRACTS` → `S_ORDERS` → `SALES_INVOICES`, sifat nazoratidan o'tgan miqdor bilan). Distributsiya esa — shartnomasiz tez buyurtma, minglab mayda mijoz, narx turlari, qarz limiti, agent, marshrut, reys. Bu qism deyarli noldan quriladi.

Yo'nalish: **alohida loyiha emas, tahsinERP ichida yangi «Distributsiya» moduli + umumiy yadroni kengaytirish.** Mahsulot birliklari, narx turlari, mijoz kartasi, rezerv, audit, bildirishnomalar, API — ishlab chiqarish mijozlariga ham foyda beradi. Control Panel'dagi `Edition` orqali «DISTRIBUTSIYA» paketi chiqadi (MES/BOM yashiriladi).

Taxminiy hajm: **MVP (agent buyurtmasi + ombor + kassa + qarz) ~3–3.5 oy**, to'liq TZ (POS va fiskal bilan) **~8–10 oy** bitta dasturchi + AI bilan.

---

## 2. TZ bo'yicha tayyorlik jadvali

Belgilar: ✅ bor · 🟡 qisman / moslashtirish kerak · ❌ yo'q

### 3. Rollar va huquqlar
| Talab | Holat | Izoh |
|---|---|---|
| Rollarni rahbar o'zi sozlaydi | ✅ | `PERMISSIONS` (View/Change/Delete) + `[Permission]` filtri |
| «Faqat o'z ombori» | 🟡 | Faqat `WAREHOUSES.MRP` (mas'ul) bo'yicha bir nechta controllerda |
| «Faqat o'z mijozlari / jamoasi» (agent, supervayzer) | ❌ | Qator darajasidagi ruxsat yo'q |
| Xarid narxi/foydani yashirish | ❌ | Maydon darajasidagi ruxsat yo'q |
| Jurnal: kim, qachon, eski → yangi qiymat | ❌ | `USERLOGS` faqat controller/action/IP yozadi |
| O'tkazilgan hujjat o'chirilmaydi, sabab bilan bekor qilinadi | 🟡 | Soft-delete bor; «Posted/Cancelled + sabab» namunasi yo'q |
| Rahbar uchun 2FA (Telegram) | ❌ | Bot orqali oson qo'shiladi |

### 4. Ma'lumotnomalar
| Talab | Holat | Izoh |
|---|---|---|
| Tovar: rus/o'zb nom | ❌ | Bitta `Name` |
| Artikul | ✅ | `PNo`..`PNo4` |
| Bir nechta shtrix-kod | ❌ | Shtrix-kod umuman yo'q |
| Kategoriya → subkategoriya, brend, ishlab chiqaruvchi | 🟡 | Kategoriya bir darajali; brend/ishlab chiqaruvchi yo'q |
| Rasm | ✅ | `PRODUCTIMAGES` |
| **Bir nechta o'lchov birligi koeffitsient bilan** (m²/quti/poddon) | ❌ | Tovarda bitta `UnitID`; `UnitConversionService` faqat metr→dona |
| Og'irlik / hajm | 🟡 | Og'irlik va o'lchamlar bor, birlik bo'yicha emas |
| Minimal qoldiq, faol/sotuvdan olingan, katta o'lchamli | ❌ | `CalculateByWeight` bor (tortiladigan tovar o'rnida) |
| Narx turlari (chakana/ulgurji/diler/maxsus) | ❌ | Narx faqat shartnoma qatorida |
| Minimal sotuv narxi, narx tarixi, ommaviy % o'zgartirish | ❌ | |
| Tannarx (o'rtacha tortilgan) | 🟡 | FIFO partiya tannarxi bor — distributsiya uchun ham yaroqli, «o'rtacha» talabini FIFO bilan qoplash taklif qilinadi |
| USD xarid, sana bo'yicha kurs | 🟡 | `CurrencyRateService` + `CURRENCY_RATES` **master'da** bor (CBU) |
| Mijoz: STIR, telefonlar, GPS, rasm, tur, A/B/C, agent, narx turi, tashrif kuni | ❌ | `CUSTOMERS` da faqat nom/manzil/telefon/email/DUNS |
| Kredit limiti, kechiktirish muddati | ❌ | Faqat shartnomadagi `DueDate` / matnli `PaymentTerms` |
| Agent yangi nuqta qo'shadi → rahbar tasdiqlaydi | ❌ | |
| Viloyat → tuman → hudud | ❌ | |
| Mashinalar (raqam, yuk ko'tarish), qaytarish sabablari, xarajat moddalari | 🟡 | Xarajat moddalari = `FIN_CATEGORIES`; mashina/sabab yo'q |
| Excel'dan yuklash | 🟡 | Mijoz va ba'zi hujjatlar uchun bor; tovar/narx uchun kerak |

### 5. Omborlar
| Talab | Holat | Izoh |
|---|---|---|
| Ko'p ombor, har biriga mas'ul | ✅ | `WAREHOUSES.MRP` |
| Ombor turlari (asosiy, filial, agent mashinasi, brak) | 🟡 | SRM-ombor branch'ida `WarehouseType` qaror qilingan; mashina/brak turi qo'shiladi |
| Yetkazib beruvchidan kirim, valyuta | ✅ | `INCOMES` |
| Qo'shimcha xarajatni tannarxga taqsimlash (landed cost) | ❌ | |
| Ko'chirish: «yo'lda», qabul tasdig'i | ✅ | `EXPENSES` → `INCOMES(RecieveStatus=false)` → Receive/Reject |
| Farqlar dalolatnomasi | ❌ | |
| Mijozdan qaytarish (yaroqli/brak) | ❌ | |
| Hisobdan chiqarish (rahbar tasdig'i bilan) | 🟡 | `DAMAGED_PRODUCTS`/`WASTE` bor, tasdiq oqimi yo'q |
| Inventarizatsiya | 🟡 | Excel orqali farq hujjatlari; master'da `InventoryAdjustment` |
| Pereso'rtitsa | ❌ | |
| Haqiqiy / rezerv / mavjud qoldiq | ❌ | Faqat `PRODUCT_STOCKS.Amount` |
| Minusga tushishni taqiqlash | ✅ | FIFO allocator `INSUFFICIENT_STOCK` |
| Minimal qoldiq ogohlantirishi, yetkazib beruvchiga buyurtma | ❌ | `POrderGenerationService` bor (ishlab chiqarish MRP uchun) |
| Partiya va yaroqlilik muddati | ✅ | `INCOME_LOTS.LotNo/ExpiryDate` |
| Shtrix-kod skanerlash | ❌ | |

### 6. Buyurtmalar va savdo
| Talab | Holat | Izoh |
|---|---|---|
| Shartnomasiz tez buyurtma | ❌ | `S_ORDERS.ContractID` majburiy |
| Holatlar: Yangi → Tasdiqlangan → Yig'ilmoqda → Jo'natildi → Yo'lda → Yetkazildi → To'langan/Qarzga, Bekor | ❌ | Buyurtmada holat yo'q |
| Qarz limiti / muddati o'tgan qarz tekshiruvi, rahbar ruxsati | ❌ | |
| Bir necha ombordan jo'natish, qisman jo'natish | 🟡 | Qisman jo'natish (bir nechta invoice) bor |
| Og'irlik va mashina sig'imi | 🟡 | `TransportLoadCalculator` bor (kiruvchi yuk uchun) |
| Chegirma (qator/buyurtma/kategoriya), rol bo'yicha limit | ❌ | |
| Aksiyalar (10+1, hajmga qarab, muddatli) | ❌ | |
| Qaytarish va almashtirish | ❌ | |
| Chop etish: nakladnoy, hisob-faktura, yig'ish varaqasi, marshrut varaqasi, akt sverki | 🟡 | Invoice print, SF/TTN, akt sverki bor (shartnoma bo'yicha); yig'ish/marshrut varaqasi yo'q |
| Nakladnoyni Telegram'da PDF | ❌ | |

### 7–8. Agent va ekspeditor (mobil o'rniga Telegram)
| Talab | Holat |
|---|---|
| Agent, tashrif, marshrut, GPS, selfi, foto-hisobot, reja/fakt | ❌ |
| Reys, mashina, haydovchi, yetkazildi/rad, reysni yopish, reys xarajati | ❌ (logistika moduli kiruvchi ekspeditorlar haqida) |
| Agent/ekspeditor pul topshirishi (kassir tasdig'i) | ❌ |

### 9. Moliya
| Talab | Holat | Izoh |
|---|---|---|
| Bir nechta kassa va bank hisobi | ✅ | `FIN_ACCOUNTS` |
| Kirim/chiqim moddalar bo'yicha | ✅ | `FIN_TRANSACTIONS`, `FIN_CATEGORIES`, avto-posting |
| Kassalar o'rtasida o'tkazma, inkassatsiya | ❌ | |
| Click/Payme/Uzum to'lov turlari | 🟡 | Hisob sifatida ochish mumkin; integratsiya yo'q |
| Mijoz balansi (buyurtma − to'lov − qaytarish) real vaqtda | 🟡 | Shartnoma bo'yicha akt sverki bor; mijoz bo'yicha yagona ledger yo'q |
| Qarz muddati bo'yicha (0–7, 8–30, 31–60, 60+) | ❌ | |
| Kreditorlik, to'lov jadvali | 🟡 | `ContractBalanceService`, `FinRecurringObligations` |
| Agent ish haqi (oklad + % savdo + bonus − jarima) | 🟡 | HR payroll bor (oklad, bonus, jarima); % savdo yo'q |

### 10. Do'konlar va SaaS
| Talab | Holat | Izoh |
|---|---|---|
| Ko'p ijarachilik, ro'yxatdan o'tish, tarif, obuna, xizmat egasi paneli | 🟡 | **Control Panel rejasi to'liq qoplaydi**; ERP tomoni `TenantContext/TenantRegistry`, `LicenseSyncService` master'da |
| POS (kassa), smena, Z-hisobot, fiskal chek, oflayn | ❌ | Eng og'ir qism, 7-bo'lim |
| Do'kon → omborga so'rov, turli do'konda turli narx | ❌ | |

### 11–12. Hisobotlar, integratsiyalar, NFT
| Talab | Holat | Izoh |
|---|---|---|
| Rahbar paneli, P&L | 🟡 | `FinDashboard` (cashflow, P&L, balans), `CRMDashboard` bor; agent/do'kon kesimi yo'q |
| ABC-tahlil, likvid emas tovar, aylanuvchanlik | ❌ | |
| Excel/PDF eksport | 🟡 | `ExcelExport` bor; PDF — DOCX shablonlardan |
| Telegram xabarnomalari | ❌ | Kod bazada umuman yo'q |
| Rus + o'zbek interfeys | ❌ | Hamma matn kodda o'zbekcha, `.resx` yo'q |
| Ochiq API (1C, Mini App, bot uchun) | ❌ | Faqat cookie bilan ishlaydigan bitta JSON controller |
| Click/Payme/Uzum | ❌ | Control Panel'da obuna to'lovi uchun rejalangan — o'sha kod qayta ishlatiladi |
| Didox / e-hisob-faktura | 🟡 | `SALES_SF` da XML payload maydonlari bor, integratsiya yo'q |
| Kunlik zaxira, 99.5%, HTTPS | 🟡 | AWS deploy rejasida |

---

## 3. Arxitektura qarorlari

### 3.1 Savdo: yangi hujjat oqimi, umumiy yadro
Mavjud `S_ORDERS` ni distributsiyaga moslashtirish (shartnomani ixtiyoriy qilish, holat qo'shish) — ishlab chiqarish savdosini (sifat nazorati, yetkazish jadvali) buzish xavfi katta. Taklif:

- **Yangi `D_ORDERS` / `D_ORDER_LINES`** — distributsiya buyurtmasi, o'z holat mashinasi bilan.
- Lekin **ombor, moliya va qarz umumiy**: jo'natish `WarehouseFlowService`/FIFO orqali, pul `FinancePostingService` orqali, qarz yangi **`CUSTOMER_LEDGER`** ga yoziladi. Shartnomali savdo (`SALES_INVOICES`) ham shu ledgerga yozadigan qilinadi — shunda mijoz qarzi bitta joyda.
- Holat o'zgarishlari bitta servisda (`DistributionOrderService`) — web, bot va API bir xil qoidadan o'tadi (limit, min narx, rezerv).

### 3.2 Yadroga qo'shiladigan umumiy narsalar (ishlab chiqarishga ham foydali)
1. **`PRODUCT_UNITS`** (ProductID, UnitID, Factor, Barcode, Weight, Volume, IsDefaultSale) — asosiy birlik `PRODUCTS.UnitID` qoladi. `UnitConversionService` shu jadvalga o'tadi (SRM-B14 qarori bilan mos).
2. **`PRODUCT_BARCODES`**, `NameRu`, `BrandID`, `ManufacturerID`, `ParentCategoryID`, `MinStock` (ombor bo'yicha — `PRODUCT_STOCK_SETTINGS`), `IsActive`.
3. **Narx tizimi:** `PRICE_TYPES`, `PRICE_LIST_ITEMS` (PriceTypeID, ProductID, UnitID, Price, MinPrice, ValidFrom) — tarix `ValidFrom` orqali tabiiy saqlanadi; `CUSTOMER_PRICES` (maxsus narx).
4. **Rezerv:** `STOCK_RESERVATIONS` (WHID, ProductID, Qty, SourceDoc) — mavjud = `PRODUCT_STOCKS.Amount − SUM(rezerv)`.
5. **Audit (eski → yangi):** DbContext `SaveChanges` override'ida `ChangeTracker` orqali `AUDIT_TRAIL` (Entity, Key, Field, Old, New, User, Time) — `AuditLogQueue` ga yoziladi. Bitta joyda qilinadi, hamma modulga ishlaydi.
6. **Hujjat holati namunasi:** `Draft → Posted → Cancelled(sabab, kim, qachon)`; Posted hujjat o'chirilmaydi. Avval distributsiya hujjatlarida, keyin bosqichma-bosqich boshqa modullarda.
7. **Qator va maydon darajasidagi ruxsat:** `USER_SCOPES` (UserID, ScopeType = Warehouse/Territory/Team/Shop, ScopeID) + `RoleHelper.CanSeeCost()` kabi maxsus ruxsatlar (`PERMISSIONMODULES` ga «Xarid narxini ko'rish» yozuvi).
8. **Bildirishnomalar:** `NOTIFICATIONS` navbati + `INotificationSender` (Telegram, keyin SMS Eskiz). Hamma modul shu orqali yuboradi.
9. **REST API `/api/v1`** (Web API 2, token/JWT, tenant bo'yicha) — bot, Mini App va 1C uchun yagona kirish.
10. **Lokalizatsiya:** `.resx` (uz-Latn, ru) infratuzilmasi; avval distributsiya ekranlari, _Layout va menyu; eski modullar keyinroq.

### 3.3 Distributsiya modulining o'z jadvallari
`TERRITORIES` (ierarxik), `CUSTOMERS` ga yangi ustunlar (INN, Lat/Lng, Category ABC, AgentID, PriceTypeID, CreditLimit, PaymentDays, TerritoryID, IsApproved, Phone2, Landmark, PhotoID), `SALES_AGENTS`/jamoa (HR xodimiga bog'langan), `VISIT_PLANS`, `VISITS` (GPS, vaqt, natija, sabab), `VISIT_PHOTOS`, `SALES_PLANS`, `DISCOUNT_LIMITS` (rol bo'yicha), `PROMOTIONS` + qoidalar, `D_RETURNS`, `VEHICLES`, `TRIPS` (reys), `TRIP_ORDERS`, `TRIP_EXPENSES`, `CASH_HANDOVERS` (agent/ekspeditor → kassa, tasdiq bilan), `RETURN_REASONS`, `NOREORDER_REASONS`.

---

## 4. Telegram: bot + Mini App (mobil ilova o'rniga)

### 4.1 Nima uchun faqat bot yetmaydi
5000 tovarli katalogdan 3 daqiqada buyurtma tuzish chat tugmalari bilan noqulay. Yechim — **Telegram Mini App** (bot ichida ochiladigan veb-sahifa): katalog, qidiruv, rasm, birlik tanlash, savat, qarz/limit, imzo (canvas), kamera orqali shtrix-kod (JS kutubxona). Kirish Telegram `initData` HMAC imzosi orqali — parol kerak emas. Bot esa: bildirishnoma, tezkor amallar, joylashuv, rasm, ovoz, PDF.

### 4.2 Taskly'ni ulash, moslashtirish yoki yangi bot?
**Tavsiya: yangi bot, C# da, tahsinERP ekotizimi ichida; Taskly g'oyalari ko'chiriladi. Taskly alohida mahsulot bo'lib qoladi.**

Sabablar:
- Taskly — Node.js + Google Sheets, cPanel'da; ma'lumoti vazifalar atrofida. Distributsiya boti ERP bazasi bilan tranzaksion ishlashi kerak (rezerv, limit, qarz) — Sheets bu uchun yaramaydi, Node'dan ERP'ga har amal uchun API chaqirish esa ikkinchi stack qo'shadi.
- Control Panel allaqachon ASP.NET Core (.NET 10) da rejalangan, ERP ham keyin Core'ga o'tadi — bot shu stackda bo'lsa bitta jamoa, bitta deploy, umumiy kutubxonalar.
- Multi-tenant: bitta umumiy bot, foydalanuvchi taklif havolasi (`/start <kod>`) orqali tenant va xodimga bog'lanadi (`TELEGRAM_LINKS`: ChatId, TenantSlug, UserID/CustomerID, Role). Keyinchalik yirik mijozga o'z bot tokeni (white-label).

Taskly'dan ko'chiriladigan g'oyalar (`app.js`, `telegram.js`, `ai.js`, `permissions.js`, `handlers.js`):
- Webhook + maxfiy header, 429 `retry_after` bilan qayta urinish, edit-yoki-yangi xabar. («Javobdan oldin ishni tugatish», cron URL, JSON fayl store — cPanel vaqtinchalik yechimlari, ko'chirilmaydi; AWS arxitekturasi `01_PHASE1_MVP.md` 3-bo'limda.)
- Ko'rib chiqish → tasdiqlash → tahrirlash oqimi inline tugmalar bilan (buyurtma, yetkazish tasdig'i).
- **Ovozdan buyurtma:** Groq Whisper (uz/ru) + JSON ajratish — «Anvar do'koniga 20 qop sement M400, 5 quti kafel» → buyurtma qoralamasi → tasdiq. Raqobatchilarda yo'q kuchli farqlovchi.
- Rol tekshiruvi markazlashgan modul; Toshkent vaqti, dam olish kunlari bilan eslatma/digest.

Yangidan quriladigan: `sendDocument` (PDF), joylashuv tekshiruvi, Mini App, ERP API bilan ishlash.

Kelajakda: Taskly'ni tahsinERP «Tasks» moduliga ulash (vazifa boti ERP vazifalarini ko'rsatadi) — alohida ish.

### 4.3 Bot rollari va funksiyalari
| Rol | Bot / Mini App'da |
|---|---|
| **Agent** | Kun boshi (joylashuv + selfi), bugungi marshrut, «keldim» (joylashuv radius tekshiruvi), mijoz kartasi (qarz, tarix, tavsiya), buyurtma (Mini App yoki ovoz), to'lov qabul qilish, qaytarish, «buyurtmasiz» sababi, tokcha rasmi, yangi nuqta qo'shish, kun yakuni va qo'limdagi pul |
| **Ekspeditor** | Bugungi reys, yetkazmalar ro'yxati, qo'ng'iroq/navigatsiya havolasi, «yetkazildi» (rasm/imzo), qisman/rad (sabab), qaytarish, to'lov, reysni yopish |
| **Omborchi** | Yig'ishga tushgan buyurtmalar, yig'ish varaqasi PDF, «yig'ildi», ko'chirmani qabul qilish |
| **Rahbar / supervayzer** | Kunlik yakun, katta buyurtma, limitdan oshish (bot ichida «ruxsat berish» tugmasi), minimaldan past qoldiq, hujjat bekor qilinishi, 2FA kodi |
| **Mijoz (do'kon)** | Buyurtma holati, «yo'lga chiqdi/yetkazildi», nakladnoy PDF, qarz eslatmasi, akt sverki; keyin — o'zi buyurtma berish (Mini App katalog) |

### 4.4 Telegram cheklovlari — mijoz bilan oldindan kelishish kerak
| TZ talabi | Telegram'da | Taklif |
|---|---|---|
| 3 kun oflayn ishlash | ❌ Imkonsiz | Mini App'da savatni lokal saqlash + aloqa paydo bo'lganda yuborish (qisman). To'liq oflayn kerak bo'lsa — keyinchalik PWA |
| Soxta GPS'ni aniqlash | ❌ Imkonsiz | Faqat «live location» qabul qilish, forward qilingan joylashuvni rad etish, vaqt/aniqlik tekshiruvi, rasm EXIF/vaqt. To'liq himoya faqat native ilovada |
| Kunlik GPS-trek | 🟡 | Live location (8 soatgacha) yangilanishlarini yozib borish |
| Shtrix-kod skaneri | 🟡 | Mini App'da kamera + JS (EAN/Code128); Telegram'ning o'z skaneri faqat QR |
| Ekranda imzo | ✅ | Mini App canvas |
| Android 8+ / iOS | ✅ | Telegram ishlagan hamma joyda |

---

## 5. Bosqichlar

Bitta dasturchi + AI. TZ bosqichlariga mos, lekin 0-bosqich (poydevor) qo'shildi.

| # | Bosqich | Ichida | Muddat |
|---|---|---|---|
| **0** | Poydevor (yadro) | Audit core'ni yakunlash va master'ga birlashtirish; `PRODUCT_UNITS`, shtrix-kod, narx turlari, rezerv; `AUDIT_TRAIL` (eski→yangi); Posted/Cancelled namunasi; `USER_SCOPES` + xarid narxini yashirish; `NOTIFICATIONS`; `/api/v1` + token; `.resx` infratuzilma | 3–4 hafta |
| **1** | MVP: savdo + ombor + pul | Hudud, mijoz kartasi (limit, agent, narx turi, GPS), tovar/narx Excel importi; `D_ORDERS` holatlari, limit/min narx/chegirma limiti, rezerv; yig'ish varaqasi, nakladnoy; qaytarish; `CUSTOMER_LEDGER`, qarz muddati hisobotlari; kassalar o'rtasida o'tkazma, agent pul topshirishi; **Telegram bot + Mini App: agent buyurtmasi, to'lov, tashrif (joylashuv)**; rahbar kunlik yakuni | 8–10 hafta |
| **2** | Yetkazish va nazorat | Mashina, reys (og'irlik/hajm), marshrut xaritada (Yandex), ekspeditor rejimi botda, reysni yopish va farq dalolatnomasi, reys xarajatlari; marshrut/tashrif rejasi, reja/fakt; agent KPI ish haqi (HR payroll'ga ulash); ovozdan buyurtma; mijozga holat xabarlari va PDF | 6–7 hafta |
| **3** | Ombor kengaytmasi | Landed cost taqsimoti, pereso'rtitsa, hisobdan chiqarish tasdig'i, ko'chirma farq dalolatnomasi, Mini App'da shtrix-kod bilan inventarizatsiya/yig'ish, minimal qoldiq → yetkazib beruvchiga buyurtma, aksiyalar | 3–4 hafta |
| **4** | SaaS | Control Panel rejasining 0–3 bosqichlari (`Docs/ControlPanel/00_PLAN.md`), DISTRIBUTSIYA paketi (`Edition`) | rejaga ko'ra 7–9 hafta, qisman parallel |
| **5** | Do'konlar (POS) | Do'kon = `WarehouseType.Shop`; smena, chek, aralash to'lov, qarzga sotish, chek bo'yicha qaytarish; do'kon → ombor so'rovi; do'kon bo'yicha narx; nuqtalar kesimida hisobotlar; **fiskal chek (onlayn-NKM/OFD)** | 8–10 hafta |
| **6** | Rivojlantirish | Mijoz o'zi buyurtma beradigan Mini App, ABC-tahlil, 1C API, Didox e-hisob-faktura, Click/Payme/Uzum mijoz to'lovlari, rus tiliga eski modullarni o'girish | rejaga ko'ra |

MVP qabul mezoni TZ'dagidek: 5 agent bir hafta faqat tizimda (bot orqali) ishlaydi, qoldiqlar inventarizatsiya bilan mos.

---

## 6. Xavflar

Qaror (2026-10-18): Telegram cheklovlaridan (4.4) boshqa xavflar hozircha e'tiborga olinmaydi.

---

## 7. Sizdan qaror kerak

1. Bu TZ aniq buyurtmachidanmi (pullik loyiha) yoki bozor uchun mahsulot yo'nalishimi? Bosqichlar tartibi shunga bog'liq.
2. Buyurtmachi mobil ilova o'rniga Telegram bot + Mini App'ga roziligi.
3. ~~3.1 qarori~~ — **hal qilindi:** yangi distributsiya modeli (`D_ORDERS` + `CUSTOMER_LEDGER`).
4. Tannarx: FIFO (bor) bilan qolish yoki TZ'dagi «o'rtacha tortilgan»ni qo'shish (tavsiya — FIFO).
5. Bot: umumiy bitta bot (tavsiya qilingan, `01_PHASE1_MVP.md` 3-bo'lim) yoki har kompaniyaga alohida.
6. Fiskal chek va Didox MVP'ga kerakmi (TZ ham ochiq savol qilib qo'ygan).
7. Rus tili: faqat yangi ekranlarmi yoki butun tizim (butun tizim — qo'shimcha ~3–4 hafta).
