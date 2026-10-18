# Finance moduli — optimizatsiya natijasi (2026-10-17)

Branch: `claude/opt-finance-22068b` (push/merge qilinmagan). Audit: `FINANCE.md`.
Build: MSBuild Debug — xatosiz. Finance view'lari `aspnet_compiler` bilan alohida kompilyatsiya qilib tekshirildi (xatosiz).

## Commitlar

| Commit | Mazmuni |
|---|---|
| e3ca0c29 | FinPlPlans Delete/BulkEdit view'larida Razor kompilyatsiya xatolari (ikkala sahifa runtime'da yiqilardi) |
| 41550497 | FinDashboard servislarga bo'lindi, yagona balans engine (P01–P04, P06–P10, P13, B02, B03) |
| b575dc93 | OpeningBalance: GET'dagi DDL olib tashlandi, saqlash tranzaksiyada (P11, B06, B26) |
| 97da4a9a | FinTransactions: server-side sahifalash + standart sana filtri (P05, B14, B22) |
| d979802a | FinAccounts: 20 tadan keyingi hisoblar ko'rinmasdi (B13) |
| b1fba8e9 | Stored XSS (B19) |
| 7bd31c87 | EBITDA/DCF formulalari (B04, B05) — sizning qaroringiz |
| 61fe60d3 | Kontragent qarzi uchun yagona summa qoidasi (B08) — sizning qaroringiz |
| 9219b27c | Akt-sverki: opening + keyingi harakatlar (B07, B09) — sizning qaroringiz |
| 5e21846a | Data-fix skriptlari (ishga tushirilmagan) |
| 4e754284 | Kichik buglar va o'lik kod (B10, B11, B12, B21, B25, B28, P15) |

## Yangi tuzilma

`FinDashboardController` 3672 → ~330 qator. URL'lar o'zgarmagan.
Ruxsat: `[Permission("FinDashboard")]`, `ShareholderValue` — `"Shareholder"`. Popup JSON qaytarishi uchun ichki tekshiruv saqlangan.

`Services/Finance/`:

| Fayl | Vazifasi |
|---|---|
| `FinQueries` | Real-cash filtri (D1), hisob qoldiqlari bitta GROUP BY bilan (D2), oylik kategoriyalari/payroll (D5, D6), asosiy valyuta (D12) |
| `CounterpartyBalanceService` | AR/AP (D9, D10). Ma'lumot bir marta yuklanib, sanalar bo'yicha xotirada kesiladi va keshlanadi. Yagona summa qoidasi (B08) |
| `BalanceSheetService` | Yagona balans engine (D3): BalanceSheet, details, DCF, Liabilities; popup harakatlari |
| `FinLiabilities` | Oylik, doimiy harajatlar va logistika qarzi (D6–D8) |
| `BalanceInventoryValuation` | Tayyor maxsulot va xomashyo bahosi (P03) |
| `FinAnalyticsService` | Cashflow, Overview, analitika, AccountBalances, P&L |
| `LiabilityReportsService` | ReportsHub, LiabilitiesDashboard |
| `ValuationService` | EBITDA, DCF, aksiyadorlar ulushi |

DTO'lar `ViewModels/Finance/FinDashboardModels.cs` ga ko'chdi (namespace `tahsinERP.ViewModels.Finance`).

## Raqamlar: refaktordan oldin va keyin (lokal DBTHSN-IPACK, faqat o'qish)

Usul: eski va yangi `tahsinERP.dll` ustida harness barcha FinDashboard action'larini chaqirdi. 44 stsenariy: 3 sana, 11 popup va 4 Akt-sverki. Har bir ViewBag/Model JSON'da maydonma-maydon solishtirildi. Harness yozish buyruqlarini bloklaydi; bironta ham yozish urinishi bo'lmadi.

1. **Sof refaktor (41550497 … 4e754284, qarorlardan tashqari):** barcha raqamlar bir xil (33/33 dashboard fayli).
   - Farq faqat view ishlatmaydigan va olib tashlangan ViewBag'larda: `DailyTrend`, `DailyAccountActivity`, CategoryAnalytics `CurrencyShortname`, Liabilities `TotalPayables`/`TotalReceivables`/`PayrollLiability`.
   - Balansda bir xil SortOrder'li kategoriyalar tartibi endi deterministik (SortOrder, keyin ID). Avval SQL tasodifiy tartibda qaytarardi.
2. **Qarorlar bo'yicha o'zgargan raqamlar (30.06.2026):**

| Ko'rsatkich | Oldin | Keyin |
|---|---|---|
| EBITDA, 12 oy | 140 611 503,58 | 154 592 282,10 |
| Operatsion daromad | 2 182 181 402,32 | 1 898 150 636,07 |
| Operatsion xarajat | 2 181 902 419,29 | 1 743 558 353,97 |
| DCF sof aktiv | 93 635 192,98 | 864 560 831,98 |
| DCF passivlar | 2 473 373 108,85 | 1 702 447 469,85 |
| Kompaniya qiymati (DCF) | 747 198 522,97 | 1 433 056 947,48 |
| Kompaniya qiymati (EBITDA × 4) | 562 446 014,32 | 618 369 128,40 |
| Akt-sverki: ta'minotchi 1 / shartnoma 107 | 925 975 586,39 (27 qator, to'liq tarix) | 937 091 601,47 (opening + keyingi; dashboard bilan bir xil) |
| Akt-sverki: mijozlar 4/374 va 5/01-08-25 | yakuniy qoldiq bir xil | yakuniy qoldiq bir xil; qatorlar faqat opening'dan keyin |

So'rovlar soni (bitta sahifa yuklanishi):

| Sahifa | Oldin | Keyin |
|---|---|---|
| LiabilitiesDashboard, 12 oy | 296 | 47 |
| LiabilitiesDashboard, 1 oy | 162 | 43 |
| BalanceSheet | 124–189 | 32–35 |
| Passivlar details | 121–188 | 25–29 |
| DCF | 151 | 36 |
| ReportsHub | 40–43 | 19 |
| FinancialOverview | 28 | 9 |
| PL | 14 | 4 |
| Cashflow | 11–14 | 8 |

FinTransactions ro'yxati endi butun jadval o'rniga sahifama-sahifa yuklanadi (5 so'rov, 25 qator).

## Sizning qarorlaringiz (2026-10-17)

| ID | Qaror |
|---|---|
| B04 | DCF: aktiv ustuni − Type 2 majburiyatlar; kapital (Type 3) ayirilmaydi |
| B05 | EBITDA/DCF daromad va xarajati P&L bandlari bo'yicha (dividend bandisiz) |
| B08 | Dashboard qoidasi hamma joyda: xarid = INCOMES.TotalPrice, bo'lmasa P_INVOICES.Amount; sotuv = TotalWithVAT ?? TotalAmount |
| B07/B09 | Akt-sverki: opening sanasi shartnoma opening jadvalidan, so'ng faqat keyingi harakatlar |

## Data-fix skriptlari (`Database_Migrations/`)

Baza faqat lokal (localhost / DBTHSN-IPACK); boshqa server yo'q. 2026-10-17 da lokal bazada bajarildi:
- `Finance_OpeningContractBalances_Schema.sql`;
- `DataFix_Finance_OpeningContractTotals_Fix.sql` (0 qator);
- `DataFix_Finance_BalanceCategoryTypes_Fix.sql` (4 kategoriya Type=1 ga o'tdi; DCF sof aktivi endi 1 635,5 mln).

`DataFix_Finance_InvoiceMirrorAmounts_Fix.sql` hali bajarilmagan — SRM skriptidan keyin bajariladi (pastga qarang).

| Skript | Lokal natija |
|---|---|
| `Finance_OpeningContractBalances_Schema.sql` — avval har GET'da bajariladigan DDL + (BalanceType, SnapshotDate) indeksi | — |
| `DataFix_Finance_OpeningContractTotals_Check/Fix.sql` — eski har-so'rov UPDATE'i | 0 qator |
| `DataFix_Finance_InvoiceMirrorAmounts_Check/Fix.sql` — eski mirror yozuvlarini B08 qoidasiga keltirish | 39 xarid yozuvi, 1 542,1 → 1 705,0 mln |
| `DataFix_Finance_BalanceCategoryTypes_Check/Fix.sql` — ombor va asosiy vosita kategoriyalari | Xomashyo, Tayyor maxsulot, Jihozlar, Bino Type=3 (kapital) bo'lib qolgan. Shu sabab balansda passiv ustunida, DCF'da aktivdan tashqarida |

Kategoriya turlari tuzatilsa, DCF sof aktivi lokal bazada taxminan 1 635,5 mln bo'ladi.

**Ishga tushirish tartibi (SRM xarid bilan bog'liq):** SRM-B25 (`claude/opt-srm-procurement-760fa4`, 391b2cd6) dan keyin `P_INVOICES.Amount` QQS bilan (brutto) saqlanadi. Shu sabab:
1. Avval `DataFix_SRM_Procurement_01_InvoiceAmount_Fix.sql` ishga tushiriladi (eski invoyslar bruttoga keltiriladi).
2. Keyin `DataFix_Finance_InvoiceMirrorAmounts_Fix.sql`.

Finance kodi Amount'ga QQS qo'shmaydi (61fe60d3), shuning uchun ikki marta QQS bo'lmaydi.

SRM xarid branch'i `FinancePostingService.cs` ni o'zgartirmagan, shuning uchun merge ziddiyati bo'lmaydi (SRM sessiyasi tasdiqladi).

## Tuzatilgan ID'lar

- **Performance:** P01, P02, P03, P04, P05, P06, P07, P08 (DailyTrend/DailyAccountActivity olib tashlandi), P09, P10, P11, P13, P15, P17 (yangi kodda AsNoTracking).
- **Bug va xavfsizlik:** B02, B03, B04, B05, B06, B07, B08, B09, B10, B11, B12, B13, B14, B19, B21, B22, B25, B26, B28. B20 qisman: dashboarddagi `catch {}` fallback'lari olib tashlandi, FIN_RECURRING_OBLIGATIONS uchun faqat SqlException ushlanadi va loglanadi.
- **Qo'shimcha:** FinPlPlans Delete va BulkEdit view'lari kompilyatsiya bo'lmasdi (auditda yo'q edi).
- **Dublikatlar:** D1–D12 (servislarga yig'ildi), D23 (FinTransactions Index fix-up).
- **O'lik kod:** EnsureOpeningContractBalanceTable, UpsertDetailedOpeningSnapshots, ishlatilmagan ViewBag hisoblari, GetLatestOpeningDate/CategorySnapshotInfo, raw-SQL fallback'lar, FinTransactions.GetContractsBySupplier, sortOrder/page/pageSize, GetAllContractBalancesAsync va bo'sh if, `parentID == 0` tarmog'i, loadAccountCurrency, OpeningContractBalanceLite.TotalAmount/PaidAmount (Reconciliation).

## 2-bosqich (2026-10-18): qolgan ishlar

Asosiy audit sessiyasi ro'yxati bo'yicha. Har qadam alohida commit, master va main'ga merge qilingan (oxirgisi `bd84593c`).

| Commit | Nima qilindi |
|---|---|
| `2931b371` | **Ruxsat:** 9 ta Finance controller class darajasida `[Permission("Modul")]`; 90+ inline `RoleHelper` tekshiruvi olib tashlandi. GET bo'lsa ham o'zgartirish talab qiladigan amallar `Change = true`. `OpeningBalance.Edit` GET ko'rish rejimi uchun `[SkipPermission]` va avvalgi tekshiruv bilan qoldi. |
| `3946bd0b` | **M04:** 62 ta view'dagi `@if (RoleHelper…) { … } else { Response.Redirect(…) }` o'rami olib tashlandi. |
| `13d2ace9` | **B15:** Create/Edit uchun umumiy `ValidateTransaction()`; hujjatdan yaratilgan tranzaksiyani Edit GET/POST rad etadi; SourceDocType faqat qo'lda tanlanadigan turlardan. **B23:** import'da takroriy kod/nom import'ni yiqitmaydi, hisob valyutasi tekshiriladi, bazadagi aynan bir xil qator o'tkazib yuboriladi. **M03:** import va shablon `FinTransactionImportService`'ga ko'chdi. **D14, D13:** `FinSelectLists`. **D22:** Export `ExcelExport` orqali. |
| `12d05143` | **B18:** `FinLiabilities.PayrollByEmployee` balans va ReportsHub uchun bitta funksiya. **B17:** kategoriyasi tanlanmagan doimiy majburiyat "Doimiy harajatlar qarzi"ga tushadi. |
| `2a304f8a` | **B20:** FinBalanceCategories'dagi `catch {}` va xom SQL fallback olib tashlandi; `ReconciliationAdjustmentService` EF'ga o'tdi (jadval EDMX'da bor); FinRecurringObligations Edit/Delete xatolari ko'rsatiladi. **P16:** daraxt avlodlari xotirada hisoblanadi. **D15:** `FinCategoryTree`. **D16:** MapCategories uchun bitta view. |
| `c247d6c0` | origin/master merge: EPPlus o'rniga ClosedXML. Finance import ClosedXML'ga ko'chirildi. |
| `90790770` | **B24:** ulush davri xatosi TempData orqali ko'rsatiladi; 100% tekshiruviga aksiyadorning o'z davrlari ham kiradi; AddContribution bitta DB tranzaksiyasida. **B29:** PostPurchaseInvoice hisobni `PAYABLES_{valyuta}` kodi bo'yicha topadi (Sales kabi); valyutasiz invoys jimgina o'tkazib yuborilmaydi. |
| `76b1d496` | **P12:** Opening tahririda har qatorga shartnoma so'rovi yuborilmaydi (birinchi ochilganda yuklanadi, keshlanadi); har qatorda to'liq kontragent ro'yxati chizilmaydi. |
| `3f88e2d4` | **M02:** OpeningBalanceController 1353 → 280 qator: `OpeningBalanceService` va `OpeningBalanceExcelService`. **D17:** `FinCounterpartyLookupService`, uni OpeningBalance va FinTransactions ishlatadi. **D11:** subledger nomi testi bitta funksiyada. |
| `46acc329` | **D13, D22:** FinPlPlans va Dashboard'dagi yil/oy ro'yxatlari; P&L reja eksporti. |
| `db32bfef` | **D18:** FinTransactions Create/Edit → `_Form` + `_FormScripts`. |
| `85351c43` | **D21:** FinRecurringObligations va Shareholder Create/Edit → `_Form`. |
| `bd84593c` | **D19:** aktiv va passiv tafsilot sahifalari bitta `BalanceSheetDetails.cshtml` view'ida (URL'lar o'zgarmagan). |

**Raqamlar (lokal baza, faqat o'qish, harness):**
- **B18:** 2026-06-30 holatiga oylik qarzi balansda 0 edi, Hub'da 16,6 mln (opening ikki marta sanalgan). Endi ikkalasida **8 298 141,99**: opening'ning o'zi, chunki undan keyin harakat yo'q.
- DCF sof aktivlar shunga mos ravishda 8,3 mln ga kamaydi.
- 2025 yildagi sanalar o'zgarmagan.
- **M02, D17:** OpeningBalance Details (2 sana), 4 ta shartnoma summasi, 3 ta shartnoma ro'yxati va FinTransactions'ning 7 ta lookup'i eski va yangi kodda bir xil.

**Eslatma:** CRM sessiyasi lokal bazada `S_ORDERS.Currency` ustunini olib tashlagan (`CRM_2026_10_M02_DropCurrencyColumns.sql`). Uning EDMX o'zgarishi hali master'da emas. Shu sababli 2026-10-18 holatida balans sahifalari lokal bazada `Invalid column name 'Currency'` bilan yiqiladi. Bu Finance kodi bilan bog'liq emas. Balans bo'yicha oxirgi harness solishtiruvi shu ustun olib tashlanishidan oldin qilingan (B18).

## 3-bosqich (2026-10-18): akt-sverki, qarz, balans va opening mosligi

Bu bosqich tekshiruvdan boshlandi. Lokal bazada, faqat o'qish rejimida, 5 ta sana (2025-12-31 dan 2026-06-30 gacha) bo'yicha ikki narsa solishtirildi:
- har bir kontragentning akt-sverkisi;
- balans va qarzlar hisoboti.

Natija: akt-sverki va qarz hisoboti turli raqam berardi, balans esa teng emasdi. Tuzatilganlari:

- **Akt-sverki** ("Barcha shartnomalar" rejimi bilan): kontragentning hamma to'lovi olinadi, shartnomaga bog'lanmagani ham (masalan mijoz 4 bo'yicha 83,8 mln). Shartnoma rejimida bog'lanmagan to'lovlar haqida ogohlantirish chiqadi.
- **Shartnoma aktidagi xatolar:**
  - opening'gacha chiqarilgan invoysga keyin qilingan to'lov aktga kirmay qolardi;
  - bir xil raqamli ikki shartnomada opening va to'lovlar ikki marta sanalardi;
  - opening'gacha kiritilgan tuzatish opening bilan birga yana sanalardi (mijoz 4 da 60 mln).
- **Kontragent qarzi:** opening sanasi kategoriya snapshot'idan olinardi va opening 0 bo'lib qolardi. Masalan, 2026-04-30 holatiga ta'minotchilar qarzi 1,7 mlrd o'rniga 122 mln chiqardi. Opening'gacha chiqarilgan invoysning summasi ham qayta qo'shilardi (+37,9 mln).
- **Logistika qarzi** endi ekspeditor aktlari yig'indisiga teng. Avval kategoriya nomi bo'yicha hisoblanardi va 2025-12-31 da soxta 83 mln qarz chiqardi.
- **Balans tengligi:** majburiyatlar musbat ko'rsatiladi va "Taqsimlanmagan foyda (hisoblangan)" balanslovchi qatori qo'shildi. Endi aktiv = majburiyatlar + kapital + taqsimlanmagan foyda.
- **Opening AutoFill** akt-sverki bilan bir xil hisobdan foydalanadi: oldingi opening + harakatlar, shartnoma bo'yicha, shartnomasiz qoldiq va avans ham.

**Natija (lokal baza):**
- 5 ta sananing hammasida barcha ta'minotchi, mijoz va ekspeditor aktlari qarzga 0,00 farq bilan teng.
- Opening'ni saqlab qayta o'qish bir xil natija beradi.
- AutoFill jami AP, AR, logistika va oylik qarziga teng (2026-05-31 va 2026-06-30).
- Save va AutoFill tranzaksiya ichida sinaldi, ROLLBACK qilindi, ya'ni baza o'zgarmadi.

**Ma'lumot masalalari:**
- 17 ta to'lovda kontragent yoki shartnoma ko'rsatilmagan: kontragentsiz to'lovlar hech qaysi aktga tushmaydi, shartnomasizlar faqat "Barcha shartnomalar" aktida ko'rinadi.
- Bir xil raqamli shartnomalar bor.
- Birinchi oylik opening'idan (21.05.2026) oldin to'langan oylik HR'dagi hisoblangan oylikdan 26 mln ko'p: balans manfiy oylik qarzini 0 deb ko'rsatadi.

## Yakuniy tur (2026-10-19)

**Holat (audit bandlari bo'yicha):**

| Toifa | Bajarildi |
|---|---|
| Bug | 26/26 |
| Xavfsizlik | 3/3 |
| Perf | 18/18 |
| Dublikat | 23/23 |
| O'lik kod | 16/16 (FinAccounts.Transactions — ataylab qoldirildi, view havolalari ishlatadi) |
| Maint | 4/4 |

**Bu turdagi commitlar:**

- **B01, valyuta.** Finance hisobotlari endi so'mda. Har hujjat va to'lov o'z sanasidagi CBU kursi bilan o'giriladi (`Services/Finance/FinMoney`, Core'dagi `UzsConverter` ustida). Qamrov:
  - dashboardlar, P&L, EBITDA/DCF, balans;
  - kontragent qarzlari, akt-sverki, oylik va doimiy harajatlar, logistika;
  - opening tahriri.

  Kurs topilmasa, sahifada ogohlantirish chiqadi.
- **Opening valyutasi.** `DataFix_Finance_OpeningCurrency_Check/Fix` bilan so'mda kiritilgan, lekin USD deb belgilangan 112 ta opening qatori va 20 ta snapshot so'mga o'tkazildi. Lokal bazada oldin BACKUP olindi. Opening'ning standart valyutasi endi asosiy valyuta (avval qattiq "ID 5" edi).
- **P14.** Shartnoma qoldig'ida mahsulotlar bitta so'rovda yuklanadi.
- **P17.** Faqat o'qiladigan 30 ta joyga `AsNoTracking` qo'shildi.
- **P18.** Chart.js bitta qat'iy versiyada (`_ChartJs`); `.stat-card` CSS'i `Content/fin-dashboard.css` faylida.
- **D12, D15, D20, D23.** Eski asosiy-valyuta tanlovi olib tashlandi; daraxt ro'yxatlarining umumiy skripti; kirim va chiqim tahlili bitta view'da; Include'dan keyingi "fix-up" olib tashlandi.
- **O'lik kod.** EDMX'dan `sp_Fin_CashSummary` va `V_FIN_PL_PLAN_VS_FACT_MONTHLY` olib tashlandi.
- **Qo'llanmadagi cheklov.** Qo'lda kiritilgan ta'minotchi to'lovi va doimiy harajat to'lovini endi tahrirlash va o'chirish mumkin.

**Tekshiruv:**
- Lokal bazada hamma hujjat so'mda: B01'dan keyin 61 ta hisobot raqami o'zgarmadi.
- Tranzaksiya ichida qo'shilgan USD to'lovlar (ROLLBACK) to'g'ri o'girildi: cashflow +100 USD = 1 180 876 so'm; ta'minotchi qarzi va akt-sverki −10 USD = −118 087,60 so'm.
- Akt va qarz tengligi, opening AutoFill tengligi saqlangan.

**Qolganlar:**
- **Chart.js'ni lokal faylga o'tkazish:** fayl yuklab olishni talab qiladi, hozir versiyalangan CDN ishlatiladi.
- **Balans popup'laridagi hujjat qatorlari** hujjat valyutasida ko'rsatiladi; jami so'mda.
- **Brauzerda tekshirish:** login kerak.

## Qolganlar (bajarilmadi)

- **B01 (valyuta):** qaror bor: konvertatsiya qilinadi (CBU + CURRENCY_RATES). Core `Services/Common/CurrencyRateService`'ni tayyorlagach Finance dashboardlari ulanadi.
- **B16 (bajarildi, `8ee0fa27`):** hisob kesimidagi opening sxemasiz saqlanadi: FIN_BALANCE_SNAPSHOTS, SourceType `OpeningBalanceAccount`, SourceID = AccountID. AccountBalances undan foydalanadi; eski opening uchun hisoblar soniga bo'lish ≈ belgisi bilan qoldi; `>` ishlatiladi.
- **B27 (bajarildi, `249b8590`):** `[Permission("FinRecurringObligations")]`. `DataFix_Finance_RecurringObligationsPermission_Fix.sql` lokal bazada bajarildi: modul ID 478, 9 rolga FinDashboard'dagi huquqlar berildi.
- **Ekspeditor akt-sverkisi (bajarildi, `17be6f35`):** opening (BalanceType 4) mijoz va ta'minotchidagidek ishlatiladi.
- **P14:** SRM sessiyasi allaqachon qilgan (SRM-P05). **P18:** Chart.js CDN layout'da (core), unga tegilmadi.
- **D15 qoldig'i:** uchta daraxt Index view'i hali alohida, umumiy `_TreeIndex` partial yo'q.
- **D20:** auditda Finance tashqarisidagi qism (bajarilmadi).
- **InvoiceMirrorAmounts_Fix:** SRM Xarid sessiyasining data-fix'i lokal bazada bajarilishini kutmoqda.
- **Nom bo'yicha qidiruv → SystemCode:** db-foundation sessiyasi bajaradi. Finance fayllari master'ga tushgach o'z branch'ida qiladi.
- **Brauzerda tekshirilmadi:** foydalanuvchidan login kerak. Faqat build, aspnet_compiler (barcha Finance view'lari) va harness orqali tekshirildi.

## Umumiy qatlam uchun takliflar (o'zgartirilmadi)

1. `EDMX`: `FIN_RECURRING_OBLIGATIONS` jadvali modelda yo'q, xom SQL ishlatiladi. `sp_Fin_CashSummary` va `V_FIN_PL_PLAN_VS_FACT_MONTHLY` ishlatilmaydi.
2. `PERMISSIONMODULES` ga `FinRecurringObligations` moduli qo'shilishi kerak (B27).
3. FinancePosting mirror yozuvlari chaqiruvchining tranzaksiyasida bo'lishi kerak (CRM, SRM, Logistika chaqiradi).
4. FIN_TRANSACTIONS'ga asosiy valyutadagi summa (`AmountBase`/`Rate`) — B01 qarori.
5. CRM/SRM/HR dashboardlari FIN_OPENING_CONTRACT_BALANCES'ni xom SQL bilan o'qiydi; ular `CounterpartyBalanceService` dan foydalanishi mumkin.
