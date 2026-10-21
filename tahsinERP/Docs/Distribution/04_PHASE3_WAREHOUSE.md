# Distributsiya — 3-bosqich: ombor kengaytmasi va aksiyalar

Holat: 2026-10-20, branch `feature/dst-phase3`. Umumiy reja: `00_PLAN.md` (5-bo'lim, 3-qator). Migratsiya `Database/Migrations/025_DST_Warehouse.sql`
(lokal `DBTHSN-IPACK` va `DB_DST_UI` da qo'llangan, zaxira `*_pre_025_DST_20261020.bak`, qayta ishga tushirish xatosiz). Bo'sh baza ruxsatlari — `Seed/105`.

## 1. Nima qilindi

| Ish | Qanday ishlaydi | Kod |
|---|---|---|
| **Landed cost** | Yetkazib beruvchidan qabul qilingan kirim(lar)ga yo'l haqi / bojxona / boshqa xarajat. Taqsimlash qiymat, miqdor yoki og'irlik (`PRODUCT_PROFILES.BaseWeightKg`) bo'yicha; yaxlitlash qoldig'i eng katta qatorga. O'tkazilganda `INCOME_PRODUCTS.PiecePrice` oshadi — FIFO tannarx, qoldiq qiymati va moliya hisobotlari shu narxni o'qiydi. Allaqachon chiqarilgan qism qayta baholanmaydi, unga tushgan summa ko'rsatiladi (`ConsumedAmount`). Bekor qilish narxni qaytaradi. Faqat tannarx huquqi (`DstSeeCost`) bilan | `Services/Warehouse/LandedCostService.cs`, `DstLandedCosts` |
| **Hisobdan chiqarish rahbar tasdig'i bilan** | Qoralama → "tasdiqqa yuborish" (rahbarlarga Telegram xabari, botda ✅/❌ tugmalari) → tasdiq: ombor chiqimi (EXPENSES, FIFO tannarx) yoki rad (sabab bilan). Tasdiqlovchi — `DstWriteOffApprove` huquqi; o'z hujjatini faqat Admin tasdiqlaydi. Buyurtma rezervidagi tovar chiqarilmaydi. O'tkazilganini bekor qilish tovarni qaytaradi. Sabablar — `REASONS` (WRITE_OFF) | `WriteOffService.cs`, `DstWriteOffs`, `DstApi/WriteOffDecision`, bot `wo:` |
| **Pereso'rtitsa** | Bir omborda A tovar chiqim (FIFO), B tovar kirim — B tannarxi = A chiqim summasi / B miqdori; ombor qiymati o'zgarmaydi. Bekor qilish (B hali chiqarilmagan bo'lsa) | `RegradeService.cs`, `DstRegrades` |
| **Ko'chirma farq dalolatnomasi** | Kutilayotgan ko'chirma kirimi (ombor modulida "hammasi yoki hech narsa") har qator bo'yicha haqiqiy miqdor bilan qabul qilinadi. Kamomad: **yo'qotish** (yo'lda yo'qolgan — summasi dalolatnomada) yoki **jo'natuvchiga qaytarish** (o'sha tannarxda kirim). Ortiqcha — kirimga. Atomik "band qilish" (ikki marta qabul yo'q), jo'natuvchi chiqimi "yuborilgan". Farq bo'lsa rahbarlarga xabar. Qabul — omborning mas'uli (`WAREHOUSES.MRP`) yoki Admin. Dalolatnoma chop etiladi | `TransferActService.cs`, `DstTransfers` |
| **Minimal qoldiq → yetkazib beruvchiga buyurtma** | Minimal qoldiq ostiga **tushganda** (jo'natish, hisobdan chiqarish, pereso'rtitsa) rahbarlar va o'sha ombor omborchilariga bir marta xabar. "Buyurtma taklifi" sahifasi: mavjud (rezervdan tashqari) qoldiq minimaldan past tovarlar, taklif = maqsad (`MaxQty`, bo'lmasa minimal × 2) − mavjud, yetkazib beruvchi va narx oxirgi xarid hisob-fakturasidan. Tanlanganlar yetkazib beruvchi bo'yicha SRM xarid buyurtmasiga (`P_ORDERS`, mavjud `POrderService` orqali) | `StockAlerts` (`StockDocs.cs`), `ReorderService.cs`, `DstReorder`, `DstSettings` (maksimal) |
| **Aksiyalar** | Chegirma: tovar / kategoriya / hamma tovar, qatorda kamida N (0 — muddatli chegirma; bosqichli — bir nechta aksiya, eng kattasi). Sovg'a: har N ga M (o'sha yoki boshqa tovar, takrorlanadi). Narx turi bo'yicha cheklash. Buyurtma saqlanganda avtomatik (web, bot, Mini App); aksiya chegirmasi rol limiti va minimal narxdan ozod, qo'lda berilgan chegirma avvalgidek tekshiriladi. Sovg'a — narxsiz alohida qator, rezervga olinadi va jo'natiladi; har saqlashda qayta hisoblanadi. Katalogda (web va Mini App) aksiya belgisi | `PromotionService.cs`, `OrderService.SaveDraft`, `DstPromotions` |
| **Mini App: shtrix-kod bilan inventarizatsiya va yig'ish** | Omborchi (va rahbar) botda "📦 Ombor" → Mini App (`/app/store.html`): ochiq sanoqlar — skanerlash sanalganga qo'shadi (qutidagi shtrix-kod — quti koeffitsienti bilan), "ko'r" sanoq (tizim qoldig'i ko'rsatilmaydi); buyurtmalar — yig'ishni boshlash, skanerlangan miqdor qatorlarga taqsimlanadi (ortiqcha/buyurtmada yo'q — ogohlantirish), "Jo'natish" yig'ilgan miqdor bilan. Ombor scope'i web bilan bir xil | `DstApiController.Warehouse.cs`, bot `MiniAppEndpoints` (`/store/*`), `wwwroot/app/store.*` |

Ombor yozuvlari bitta yo'ldan (`Services/Warehouse/StockDocs.cs`): `dbo.EXPENSES/INCOMES` + `StockLedger` (DLOC lotlari bilan) + FIFO tannarx,
(ombor, tovar) qulfi buyurtma rezervi bilan bir xil resursda; hammasi `DistributionUnitOfWork` tranzaksiyasida.

## 2. Rejadan farqlar

- Hisobdan chiqarish `DAMAGED_PRODUCTS`/`WASTE` jadvallariga emas — ular omborga ta'sir qilmaydi (MES sifat nazorati). Distributsiya hujjati to'g'ridan-to'g'ri ombor chiqimi.
- Xarid buyurtmasida yetkazib beruvchi maydoni bo'sh qoladi: SRM qoidasi bo'yicha to'g'ridan-to'g'ri xaridda yetkazib beruvchi/shartnoma yozilmaydi — taklif qilingan yetkazib beruvchi izohda (`DST_REORDER`).
- Landed cost valyutasiz (asosiy valyutada kiritiladi) — mavjud FIFO tannarx ham valyutani hisobga olmaydi.
- Mini App'da yig'ishdan keyin jo'natish omborchining o'zida (ofisdagi "Jo'natish" bilan bir xil servis).

## 3. Tekshiruv

ERP testlari 182/186 (4 — shartli o'tkazib yuborilgan; yangi 11: aksiya bosqichi va 10+1, qo'lda/aksiya foizini ajratish, taqsimlash
qiymat/miqdor/og'irlik; integratsiya (rollback) — hisobdan chiqarish yuborish/huquq/o'zini tasdiqlash/FIFO 600 000/bekor; rezervdagi tovar himoyasi;
pereso'rtitsa 10 → 8 (B tannarxi 50 000) va bekor; landed cost 500 000 → PiecePrice 40 000 → 45 000, sotilgan qism 100 000, keyingi jo'natish
45 000 da, bekor — 40 000; ko'chirma: yo'qotish 80 000, qaytarish 3 dona, ortiqcha 5 000, ikki marta qabul yo'q (DLOC omborlarda);
aksiya 10% + qo'lda 2% = 11,8% limit 5% bilan, sovg'a 2 → 1, jo'natishda narxsiz; buyurtma taklifi 300 − 100 = 200). Bot testlari 57/57.
RazorCheck: 531 view, 0 xato.
