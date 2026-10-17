# tahsinERP — To'liq audit va optimizatsiya rejasi (2026-10-17)

Audit faqat o'qish rejimida o'tdi: kodga hech qanday o'zgartirish kiritilmagan.
9 ta modul bo'yicha batafsil hisobotlar shu papkada (`CORE.md`, `SRM_PROCUREMENT.md`, `SRM_WAREHOUSE.md`, `MES.md`, `FINANCE.md`, `CRM.md`, `LOGISTICS.md`, `HRM.md`, `TASKS_DOCUMENTS.md`).
Har bir topilmada ID, fayl:qator, sabab, aniq tuzatish, hajm va xavf ko'rsatilgan.

## Umumiy natija

| Modul | Jami | Critical | High | Med | Low |
|---|---|---|---|---|---|
| Core (layout, ruxsatlar, admin) | 39 | 2 | 11 | 15 | 11 |
| SRM — Xarid | 44 | 0 | 11 | 22 | 11 |
| SRM — Ombor/Inventar | 58 | 8 | 14 | 26 | 10 |
| MES + Engineering | 47 | 3 | 14 | 21 | 9 |
| Finance | 51 | 1 | 15 | 21 | 14 |
| CRM | 40 | 1 | 13 | 21 | 5 |
| Logistika | 50 | 0 | 7 | 20 | 23 |
| HRM | 48 | 2 | 14 | 23 | 9 |
| Tasks + Documents | 52 | 2 | 17 | 22 | 11 |
| **Jami** | **429** | **19** | **116** | **191** | **103** |

Bulardan tashqari ~170 ta dublikat guruh va ~130 ta o'lik kod elementi topildi.

## Tizim nima uchun sekin — 6 ta asosiy sabab

1. **Har bir sahifa og'ir.** `_Layout` har bir so'rovda 4–5 ta keshsiz SQL so'rov yuboradi. Login'dan keyin va har 10 daqiqada ~215 ta so'rov ketadi, chunki ruxsat keshi hamma uchun birdaniga eskiradi. Brauzerga ~3.5 MB JS yuklanadi: jQuery 3 marta, Bootstrap 2 marta, PDF kutubxonalari esa har sahifada. Statik fayllar keshlanmaydi.
2. **Ro'yxatlar to'liq yuklanadi.** Deyarli barcha Index sahifalari butun jadvalni Include'lar bilan, `AsNoTracking`siz va sahifalashsiz yuklaydi. Bunga FinTransactions, Waybill, Incomes, PieceRate va boshqalar kiradi.
3. **N+1 so'rovlar.** Ko'p joyda tsikl ichida qatorma-qator so'rov ketadi:

   | Joy | So'rovlar soni |
   |---|---|
   | MES dashboard | har buyurtma qatoriga 4 ta |
   | HR piece-rate | har yozuvga tarif so'rovi |
   | Finance | har hisobga alohida SUM |
   | Tasks | har qatorda yangi DbContext |
   | CRM mahsulot dropdown | har mahsulotga 2–3 ta |
   | BOM daraxti | har tugunga alohida |

4. **Dashboardlar xotirada hisoblaydi.** Ular SQL'da emas, xotirada hisoblanadi. Liabilities dashboard ledgerni ~28 marta qayta quradi. Balans ikki xil "engine"da hisoblanib, ikki xil natija beradi.
5. **Blob'lar metadata bilan birga yuklanadi.** `DOC_FILES`, `*_DOCS` va PDF fayllar ro'yxat yoki tafsilot ko'rsatish uchungina to'liq yuklanadi.
6. **Spravochniklar keshlanmaydi.** Unit, Currency, Warehouse, Product va boshqa dropdownlar har safar bazadan quriladi: ~608 ta joyda.

## Eng xavfli topilmalar (avval shular tuzatilishi kerak)

**Xavfsizlik:**
- `AccountController` `[AllowAnonymous]` bilan belgilangan. Undagi `SaveSettings` POST qilingan username'ga ishonadi, shuning uchun istalgan odam Admin parolini almashtira oladi (tasdiqlandi).
- `Koreshok` login talab qilmaydi: `employeeId` ni almashtirib, xodimlarning maoshini ko'rish mumkin.
- 20 ta controllerda server tomonida ruxsat tekshiruvi yo'q (SOrder, SalesInvoices, ProductPlan, Warehouse va boshqalar).
- Stored XSS: OfficialLetters, CompanyOrders va Finance sahifalarida.
- GET orqali o'chirish (`SContract.DeleteProduct`), open redirect, SSRF stub, Developer rolini himoya qiluvchi tekshiruv buzilgan.

**Pul va ombor xatolari (ma'lumot buziladi):**
- Ish haqi Finance'ga `direction=1` (kirim) bilan yoziladi (tasdiqlandi).
- Ombor qoldig'i:
  - Race condition tufayli o'zgarishlar yo'qoladi, manfiy qoldiq esa 0 ga tushirilib yashiriladi.
  - Income edit va Receive birga ishlaganda qoldiq ikki marta qo'shiladi.
  - Qabul qilinmagan income o'chirilsa, qo'shilmagan qoldiq ayriladi.
  - `LIKE '%EXPENSE_ID:12%'` filtri 120 va undan katta ID'larga ham mos keladi, shuning uchun boshqa hujjat o'chirib yuboriladi.
- Sotuv invoysi bitta tranzaksiyada saqlanmaydi. Hujjat raqamlari takrorlanadi.
- Yuk xati to'lovi bir nechta waybill'ga noto'g'ri taqsimlanadi.
- Soft-delete eski hujjatlardagi FK'larni NULL qiladi, shuning uchun qarz va bog'lanish yo'qoladi. Recover bilan stock va finance qayta tiklanmaydi.
- PInvoice tahrirlanganda summa qayta hisoblanmaydi. OpeningBalance o'chirib qayta yoziladi, lekin tranzaksiyasiz.
- Payroll: bir oyni ikki marta to'lash mumkin, tasdiqlanmagan tabel ham to'lanadi.

## Optimizatsiya rejasi — 6 bosqich

### 0-bosqich. Xavfsizlik (shoshilinch, kichik o'zgarishlar)
- CORE-S01, CORE-S03, HRM-S01, HRM-S02, CRM-S02, DOC-S01/S02/S03 hamda HSCode, Log va Warehouse/Unit/Currency ruxsatlari.
- Natija: tashqaridan yoki oddiy foydalanuvchi tomonidan buzib bo'lmaydigan tizim.

### 1-bosqich. Core infratuzilma (keyingi barcha ishlar shunga tayanadi)
1. **Ruxsat arxitekturasi.** `[RequirePermission("Modul","Amal")]` atributi va global `PermissionFilter` kiritiladi. Ruxsat keshi har foydalanuvchi uchun snapshot + versiya bilan ishlaydi. Natijada 586 ta copy-paste tekshiruv bitta qatorga aylanadi, cold-cache'dagi ~215 so'rov 1–2 taga tushadi.
2. **`BaseController`.** Unda `CurrentUserId` (keshlangan; hozir 87 joyda keshsiz), umumiy xato xabari va Delete boilerplate bo'ladi.
3. **Umumiy servislar:**
   - `LookupCache`: spravochnik dropdownlari, MemoryCache va invalidatsiya bilan.
   - `DocumentNumberService`: lock bilan, takrorlanmaydigan raqam va unique index. Hozir uning 8+ varianti bor.
   - `ExcelHelper`: 20 ta nusxa o'rniga bitta.
   - Bitta async `AuditLogger`: `LogHelper` + `UserActionLogger` birlashtiriladi, har log yozilishidagi DNS so'rovi olib tashlanadi.
   - `VatMath`: P/S VAT helperlar uchun umumiy hisob.
4. **`_Layout` dietasi:**
   - jQuery 1 marta va `<head>`da (yoki inline skriptlar keyinga ko'chiriladi), Bootstrap 5, LivIcons va dark CSS olib tashlanadi.
   - PDF va Excel kutubxonalari faqat kerak bo'lgan sahifada yuklanadi, inline CSS/JS tashqi fayllarga chiqariladi.
   - Badge'lar AJAX yoki kesh orqali olinadi.
   - Kutilgan natija: ~3.5 MB → ~1 MB JS.
5. **`web.config`.** `compilation debug=false`, statik kontent uchun `clientCache` va siqish yoqiladi, TempData uchun cookie provider ishlatiladi (parallel AJAX so'rovlari navbatda turmasligi uchun).
6. **Umumiy JS komponentlar:**
   - Line-editor (~2000 qator, 7 ta view'da), qator o'chirilganda index bo'shlig'i bug'i bilan birga tuzatiladi.
   - DataTables server-side + export konfiguratsiyasi (hozir 49 ta view'da nusxa).
7. **SQL indekslar skripti.** Issiq FK va filtr ustunlari uchun ~27 ta indeks taklif qilingan. Ularni ishga tushirishdan oldin jonli bazada tekshirish kerak.
8. **Soft-delete siyosati.** FK'lar NULL qilinmaydi, Recover qoidalari belgilanadi.

### 2-bosqich. Ma'lumot yaxlitligi (Critical/High bug'lar, har modulda)
- Har bir ko'p jadvalli yozuv `TransactionScope` ichiga olinadi.
- Stock uchun bitta `StockLedger` / `StockDocumentService` yaratiladi. Unda atomik `UPDATE … SET Qty = Qty + @d` va `(WHID, ProductID)` bo'yicha unique bo'ladi.
- Matnli bog'lanishlar (`LIKE '%EXPENSE_ID:%'`, izohdagi lotlar) haqiqiy FK ustunlarga o'tkaziladi.
- Har bir tuzatishga **eski noto'g'ri ma'lumotni topuvchi SELECT skripti** va alohida tuzatuvchi skript yoziladi. Ularni prod'da siz ishga tushirasiz.

### 3-bosqich. Modul bo'yicha performance
- Index sahifalariga server-side paging, standart sana filtri, `AsNoTracking` va proyeksiya qo'shiladi.
- N+1 so'rovlar oldindan yuklash (lookup dictionary) yoki bitta GROUP BY bilan almashtiriladi.
- Dashboardlar SQL'da agregatsiya qiladi (view yoki SP). Mavjud, lekin ishlatilmayotgan `sp_Fin_CashSummary` ishga solinadi.
- Blob ustunlar alohida yuklanadi (proyeksiya yoki table-splitting).

### 4-bosqich. Dublikatlarni birlashtirish va god-controller'larni bo'lish
- **God-controller'lar.** FinDashboard (3672 qator) 6 ta servisga bo'linadi; URL'lar o'zgarmaydi. Norm, Incomes, PieceRate, Payroll, Waybill, SalesInvoices, SOrder ham shu tartibda bo'linadi.
- **Create/Edit view juftlari** bitta `_Form` partial'ga birlashtiriladi: Norm (~1300+1300 qator), Waybill, FinTransactions va boshqalar.
- **Bir xil hisob-kitoblar** bitta joyga yig'iladi:
  - Kontragent qarzi: hozir 5 xil usulda hisoblanadi.
  - Waybill to'langan/qarz summasi: 8 xil.
  - Piece-rate tarif: 6 xil.
  - Norm sarfi: 6 xil.
- **CRUD scaffold'lar** generic `RefDataController<T>` ga o'tkaziladi: TaskPriority/Status, DefectType, ProductionProcess, Shop, FinCategories.

### 5-bosqich. O'lik kodni olib tashlash
- `Views/Manual` (2875 qator) va `assets/manual` (27 MB): hech qayerda ishlatilmaydi.
- POrderPayment view'lari (menyudagi havola 404 qaytaradi), PartStock, PartWRHS, ProductStock va ProductionControl controller'lari, DamagedPartController.
- Home'dagi 10 ta ishlatilmaydigan JSON endpoint, PermissionController, `UsersRoleProvider`, `Helper.cs`, `BundleConfig2`, POrderGenerationService'dagi eski BOM kodi (~350 qator).
- Har bir o'chirishdan oldin grep va menyu bo'yicha tekshiriladi.

## Session va agentlarga taqsimlash

Fayllar ustma-ust tushmasligi uchun ishlar quyidagicha bo'linadi:

| # | Session | Bosqichlar | Egalik qiladigan fayllar |
|---|---|---|---|
| A | **Core** (shu session, ketma-ket) | 0 + 1 + 5 (core qismi) | `_Layout`, `web.config`, `App_Start`, `Filters`, `Helpers`, `AdminSetting`, `RoleHelper`, `SoftDeleteReferenceService`, yangi umumiy servislar |
| B | SRM — Ombor + StockLedger | 2–5 | `Controllers/SRM` (Incomes, Expenses, Inventory*), `Settings`, `DlocFifo`, `WarehouseFlow` |
| C | SRM — Xarid | 2–5 | POrders, PContracts, PInvoices, PRequisitions, Supplier* |
| D | MES + Engineering | 2–5 | `MES`, `Engineering`, Tannarx/BOM servislari |
| E | Finance | 2–5 | `Finance`, Reconciliation, FinancePosting |
| F | CRM | 2–5 | `CRM` |
| G | Logistika | 2–5 | `Logistic` |
| H | HRM | 2–5 | `HR`, JobDesc, Koreshok |
| I | Tasks + Documents | 2–5 | Task*, OfficialLetters, STD, CompanyOrders, Doc servislari |

- **Tartib.** A tugamaguncha B–I boshlanmaydi, chunki ular A yaratadigan `RequirePermission`, `LookupCache`, `DocumentNumberService` va `StockLedger` interfeysiga tayanadi. A tugagach B–I parallel ishlaydi, har biri o'z worktree/branch'ida.
- **Ustunlik.** B (StockLedger) F (CRM) va D (MES) dan oldinroq tugashi kerak, chunki ular ombor yozuvidan foydalanadi.
- **Qoidalar:**
  - Umumiy fayllar (`_Layout`, `SoftDeleteReferenceService`, `web.config`) faqat A'da o'zgartiriladi.
  - Har bir modul branch'i alohida PR'ga chiqadi va siz ko'rib chiqasiz.
  - Har bir bosqichdan keyin build qilinadi va smoke-test o'tkaziladi. Avtomatik testlar kam bo'lgani uchun asosiy oqimlar brauzerda tekshiriladi.

## Sizdan qaror talab qiladigan masalalar
1. **Prod'dagi eski noto'g'ri ma'lumot.** Payroll direction, waybill to'lov taqsimoti, ikki marta hisoblangan qoldiq: men faqat tekshiruv va tuzatish skriptlarini yozamanmi, yoki siz ham ishga tushirishni xohlaysizmi?
2. **Valyutalar.** Hozir summalar valyuta konvertatsiyasisiz qo'shiladi. Kurs bo'yicha konvertatsiya kiritamizmi (biznes qarori), yoki hozircha faqat belgilab qo'yamizmi?
3. **DB sxema o'zgarishlari.** Unique index, FK ustunlar va `CustomerBase` + `SupplierBase` birlashtirish EDMX yangilashni talab qiladi. Ruxsat berasizmi, yoki faqat kod darajasida qolamizmi?
4. **Akt-sverki (FIN-B07).** Ochilish qoldig'idan oldingi tarix qasddan qo'shilyaptimi? Bu niyatni tasdiqlash kerak.
