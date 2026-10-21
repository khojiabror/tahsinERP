# Qo'llanma yozilganda topilgan kamchiliklar (2026-10-21)

Chuqur qo'llanma (`Qollanma/`) kod bo'yicha yozilganda aniqlangan nomuvofiqliklar. Hali tuzatilmagan. Daraja: **K1** — pul, ruxsat yoki ma'lumotga xavf; **K2** — jarayon noto'g'ri yoki chalkash; **K3** — qulaylik.
Qo'llanmada har biri "Muhim/Eslatma" sifatida qayd qilingan (foydalanuvchi vaqtinchalik qanday ishlashi kerakligi bilan).

## K1

| # | Kamchilik | Joy |
|---|---|---|
| 1 | Yangi rolga hamma modulga "ko'rish" berilsa (admin qo'llanmasidagi tartib), maxsus huquqlar ham beriladi: `DstSeeCost`, `DstOverrideCredit`, `DstDiscountUnlimited`, `DstWriteOffApprove` — chunki ular View huquqi bilan tekshiriladi | `DstPermissions.Has` |
| 2 | Qarz limiti faqat ledger qoldig'ini tekshiradi — tasdiqlangan, lekin jo'natilmagan buyurtmalar hisobga olinmaydi: bir nechta katta buyurtma alohida-alohida o'tib ketadi | `OrderRules.CheckCredit`, `Confirm` |
| 3 | Kassa: agent botdan topshirgan bo'lsa, web modalidagi hisob e'tiborsiz; "e'lon qilingan" summa — topshirishdan keyingi to'lovlarni ham qo'shib ko'rsatadi → soxta ortiqcha | `DstPayments/Cash` |
| 4 | Kassaga topshirilgan to'lovni bekor qilib bo'lmaydi (faqat Moliya'da qo'lda) | `PaymentService` |
| 5 | Tannarx huquqisiz ko'rinadigan summalar: boshlang'ich inventarizatsiyadagi "Tannarx" ustuni, ko'chirma farqi Telegram xabaridagi summalar; buyurtma taklifida narx ustuni yo'q → buyurtmalar 0 narxda yaratiladi | `DstInventory`, `TransferActService`, `DstReorder` |
| 6 | Minimal narx narx turi qatorida — faqat Chakana'ga qo'yilgan minimum Diler mijozida tekshirilmaydi; maxsus narx minimumdan butunlay ozod | `PricingService` |

## K2

| # | Kamchilik | Joy |
|---|---|---|
| 7 | Ekspeditorda "Kunni boshlash" tugmasi yo'q, jonli joylashuv esa faqat ochiq ish kunida yoziladi → ekspeditor izi yozilmaydi | bot `SendMenuAsync`, `FieldService.Track` |
| 8 | Reysni botdan yopishda yetkazilmagan hammasi "omborga qaytdi" deb olinadi — kamomad qayd qilinmaydi | 2-bosqich reys yopish |
| 9 | Yetkazish natijasini qaytarish (↺) buyurtma holatini, foto/imzoni va mijozga ketgan xabarni qaytarmaydi | 2-bosqich |
| 10 | Rad etilgan / qisman qaytgan jo'natma yopilgan reysga bog'liq qoladi — qayta yetkazish oqimi aniq emas | 2-bosqich |
| 11 | Qisman jo'natilgan va to'liq to'langan buyurtma "Yopildi" bo'lmaydi | `DebtService.SettleOrders` |
| 12 | "Yig'ilmoqda" → "Tasdiqlangan" qaytarish tugmasi yo'q; yangi kirimdan keyin rezerv avtomatik yangilanmaydi | `DstOrders` |
| 13 | Hudud scope'i quyi hududlarni qamramaydi; buyurtmalar ro'yxati hudud scope'ini umuman hisobga olmaydi | `ScopeService` |
| 14 | "Edit" bilan boshlanadigan sahifalar faqat ko'rish huquqi bilan ochilmaydi (mijoz/tovar kartasi, buyurtma) | `PermissionAttribute` |
| 15 | Kassa kamomadi uchun agentdan debitorlik yaratilmaydi — faqat xarajat ("agent hisobida qoladi" matni noto'g'ri) | `CashHandoverService` |
| 16 | Kirish qoldig'ini (opening balance) bekor qilish/tuzatish UI'si yo'q | `ImportService` |
| 17 | Moliya'da distributsiya mijozlari "avans to'lagan" ko'rinadi (to'lov bor, jo'natish mirror yo'q) | Moliya integratsiyasi |
| 18 | "Tushum" ikki ma'noda: kunlik Telegram xulosasida — yig'ilgan pul, panel/hisobotlarda — jo'natish − qaytarish | `DailySummaryService` |
| 19 | Minimal qoldiq xabari haqiqiy qoldiqdan, buyurtma taklifi — mavjud (rezervsiz) qoldiqdan | `StockAlerts`, `ReorderService` |
| 20 | Inventarizatsiya: web sahifada "Tizimda" ustuni ko'rinadi ("ko'r" sanoq emas); sanoq ochiqligida jo'natish to'xtamaydi; boshlang'ich va "farqni yozish"li nazorat sanog'i bir oyda — oylik qulf xatosi | `DstInventory` |
| 21 | Ko'chirma dalolatnomasi: bir qator kamomadini yo'qotish va qaytarishga bo'lib bo'lmaydi; dalolatnomani bekor qilib bo'lmaydi; yo'qotish uchun qarz/moliya yozuvi yo'q | `TransferActService` |
| 22 | Landed cost: sotilgan tovarga tushgan qism hech qaysi tannarx/foyda hisobiga tushmaydi | `LandedCostService` |
| 23 | Mini App: yig'ish natijasi oyna yopilsa yo'qoladi (sanoq esa har skanerda saqlanadi); savat `sessionStorage`da | `store.js`, `app.js` |
| 24 | Mijoz bildirishnomalari doim o'zbekcha; xodim boti menyu tili faqat qayta ulashda o'zgaradi | `NotificationService.ToCustomer` |
| 25 | "Keldim" statik joylashuvni ham qabul qiladi (faqat belgilaydi) — rejada rad etilishi yozilgan | `FieldService.CheckIn` |
| 26 | Ekspeditor faqat kechagi kundan boshlab rejalangan reysni ko'radi | bot `CurrentTripId` |
| 27 | Qarz limiti oshgani haqidagi rahbar xabari faqat buyurtma yaratilganda (keyin muddat o'tsa — yo'q) | `NotifyNewOrder` |
| 28 | Rahbar paneli o'tgan sanada ham "tasdiq/jo'natish kutmoqda" va agent naqdini joriy holatda ko'rsatadi | `DstDashboard` |

## K3

| # | Kamchilik |
|---|---|
| 29 | Inventarizatsiya va Xatolar jurnali chap menyuda yo'q (faqat bosh sahifa plitkalari) |
| 30 | Saqlanadigan, lekin ishlatilmaydigan sozlamalar: "Minusga ruxsat", "Yetkazish zonasi", tovarda "Katta o'lchamli"/"Tortiladigan" |
| 31 | "Brak" qaytarish faqat belgi — tovar brak omboriga avtomatik yuborilmaydi |
| 32 | Hisobdan chiqarishda foto maydoni bor, web formada biriktirish yo'q |
| 33 | Audit jurnali (`dst.AUDIT_TRAIL`) uchun ekran yo'q |
| 34 | Migratsiya standart narx turi sifatida "Ulgurji"ni qo'yadi (demo — Chakana) |
| 35 | Mini App savat jami aksiyasiz; rahbar Mini App'ga kira oladi, lekin tugmasi yo'q; ovozli buyurtma ekspeditorda ham ishlaydi |
| 36 | KPI: muddati o'tgan qarz jarimasi agentning joriy mijozlari bo'yicha, faqat 30+ kun; KPI bonusi HR formulasida soliqsiz |
| 37 | Ishlatilmaydigan resx kaliti `Settings_AgentAccount` |
