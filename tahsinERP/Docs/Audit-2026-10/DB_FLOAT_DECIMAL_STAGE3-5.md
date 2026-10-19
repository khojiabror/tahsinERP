# float → decimal: 3–5-bosqich hisoboti (2026-10-19)

Reja: [DB_FLOAT_DECIMAL_PLAN.md](DB_FLOAT_DECIMAL_PLAN.md); 1–2-bosqich: [DB_FLOAT_DECIMAL_STAGE1-2.md](DB_FLOAT_DECIMAL_STAGE1-2.md).
Branch `claude/float-decimal-logistics` (master `5a05d8ae` merge qilingan). Bu bosqichdan keyin sxemada **float qolmadi**
(faqat IPACK'dagi bir martalik `*_BAK_*` / `DataFix_*` jadvallari — ular EDMX'da yo'q, tegilmadi).

## Migratsiyalar

| Fayl | Bosqich | Jadvallar |
|---|---|---|
| `014_Docs_FloatToDecimal.sql` | 3 | P_CONTRACTS, P_ORDERS, P_INVOICES, P_REQUISITION_PRODUCTS, S_CONTRACTS, S_ORDERS va *_PRODUCTS (23 ustun) |
| `015_StockProduction_FloatToDecimal.sql` | 4 | INCOMES/INCOME_PRODUCTS, EXPENSES/EXPENSE_PRODUCTS, PRODUCT_STOCKS, WASTE_STOCKS, DAMAGED_PRODUCTS, PRODUCT_PRODUCTION, PRODUCTPLANS(_DAILY), NORM_INPUT_LINES (19) |
| `017_ReferenceDims_FloatToDecimal.sql` | 5 | PRODUCTS o'lcham/vazn, F_TRANSPORT_TYPES (13) |

**016 raqami ataylab o'tkazib yuborilgan** — u distributsiya ishiga tegishli (`016_DST_Directories.sql`, `.claude/worktrees/distribution`
da, IPACK'da qo'llangan, hali master'da emas). Master'dagi `013_DST_Foundation.sql` bilan ham to'qnashuv yo'q.

Turlar: pul summasi `decimal(19,4)`, birlik narxi (FIFO tannarxi ham) `decimal(19,6)`, miqdor/MOQ/smena `decimal(18,6)`,
o'lcham `decimal(18,4)`, vazn `decimal(18,6)` (IPACK'da 5 xonali og'irliklar bor), QQS stavkasi `decimal(9,4)`.

Har faylda bir xil yordamchi (`#FloatToDecimal`, vaqtinchalik protsedura):
- **Hisoblangan ustunlar** (`TotalPrice = Amount*PiecePrice`, `TotalAmount`, `Total = DayShift+NightShift` …) `sys.sql_expression_dependencies`
  orqali topiladi, o'chiriladi va ALTER'dan keyin **aynan o'sha ta'rif** bilan qayta qo'shiladi — natija turi decimal bo'ladi
  (`decimal(38,12)`, smena jami `decimal(19,6)`). Ustun tartibi o'zgaradi (hisoblangan ustun oxiriga) — EF'ga ta'siri yo'q.
- Ularga va target ustunlarga tayangan indekslar (INCLUDE ham), default'lar, `_WA_Sys_*` statistikalar — skriptlanib qayta yaratiladi.
- Qiymat tekshiruvi: sig'masa yoki yaxlitlash yo'qotishi **> 0.0001** bo'lsa `THROW`; undan kichik float "dumi"
  (`9053571.6000000015`) yaxlitlanadi va soni `PRINT` qilinadi (IPACK: 4–31 ta qiymat/ustun, eng katta yo'qotish 4.5·10⁻⁷).
- Idempotent ("hamma ustunlar allaqachon decimal"). Triggerlar (`Update_Contract_Amount_After_*`) va `sp_*` o'zgarishsiz ishlaydi
  (protseduralarda bu ustunlar uchun float o'zgaruvchi yo'q — faqat `sp_Fin_CashSummary`, u FIN jadvallarida).

IPACK'da bir xil 319 indeks va hisoblangan ustun ta'riflari migratsiyadan oldin/keyin aynan bir xil (tekshirildi); toza bazadagi
va migratsiya qilingan IPACK'dagi 257 decimal ustun turi bir xil.

## Kod

- **EDMX + 26 ta `Models/*.cs`** — migratsiyadan keyingi bazaning `sys.columns` idan avtomatik (SSDL jadval bo'yicha, CSDL MSL xaritasi
  orqali): 65 xossa, hisoblangan ustunlar ham. CSDL'da `Double` qolmadi.
- **Chegara konversiyasi** (reja 4-bosqich tavsiyasi): entity decimal bo'ldi, ichki hisob-kitob quvurlari (StockLedger, DlocFifo,
  UnitConversionService, VatMath, view model'lar) avvalgidek `double` — qiymat entity'ga yozilganda/o'qilganda aniq `(decimal)`/`(double)`.
  316 kompilyatsiya xatosi: 278 tasi mexanik (bir ifodali) konversiya, qolganlari qo'lda.
- `StockCostService.StoredUnitCost` — FIFO dona tannarxi 6 xonaga yaxlitlanadi (EF ortiqcha xonani kesadi); hujjat jami (C#)
  va qator `TotalPrice` (bazada) bir xil narxdan hisoblanadi.
- **Faqat ish vaqtida ko'rinadigan xatolar** (build ko'rmaydi) — topilgan va tuzatilgan:
  - raw SQL `SqlQuery<T>` da decimal ustun `double` xossaga: `StockCostService.FifoLayerCost` (FIFO qatlam), `StockLedger.ReverseIncomeReceipt`
    — `CAST(... AS FLOAT)`; aks holda kirimni bekor qilish va FIFO tannarxi yiqilardi;
  - `ViewBag.X as double?` — decimal bo'lgach jim 0: S-shartnoma (`ContractedVat/Qty`, `OrderedTotal`), P-shartnoma (`ContractedTotal`);
  - `SContract/Details` — `ViewBag.ProductList` dinamik tsiklida `decimal? ?? 0d` → 500; tsikl tiplandi;
  - matn: `TannarxMapper.Dimensions` ("1470,0000x1140,0000"), `ProductionToSalesTransfer` qoldiq matni, Product/FTransportType/
    DamagedProduct/SContract view'larida `.ToString()` / `DisplayFor` / `TextBoxFor` → `0.######` formati.
- Aniq tartib: Inventar dashboard (`OrderBy(WHID, ProductID)`) — ALTER'dan keyin SQL tasodifiy tartibi o'zgardi (2-bosqichdagi
  forwarder to'lovlari kabi).

## Yangi vositalar

- `Database/tools/RegressionProbe/` — harness 391 bo'lim: + xarid/sotuv hujjatlari (ro'yxat va tafsilotlar), kirim/chiqim, inventar
  dashboard, MES dashboard/hisobot, reja/ishlab chiqarish, norma/BOM material xulosasi, mahsulot o'lchamlari, transport turlari.
- `Run-RazorCheck.ps1` / `RazorCheck.cs` — **barcha 464 view**ni (Areas ham, har papkaning `web.config` namespace'lari bilan) MVC Razor
  host orqali generatsiya qilib Roslyn bilan kompilyatsiya qiladi. Model turi o'zgarganda majburiy (bu bosqichda 27 xato topdi).

## Tekshiruv

| Tekshiruv | Natija |
|---|---|
| Build | xatosiz; RazorCheck 464 view — 0 xato |
| IPACK harness: oldin (master `ad8b339a`, float) ↔ keyin (014–017, merge'dan keyin ham) | **391 bo'lim, 0 farq**; 57 yaxlitlash farqi, max **0.00006** so'm (Tannarx manba summalari, ombor oylik summa qoldig'i) |
| Ko'rinadigan sahifa matni: master build + float nusxa ↔ branch + decimal nusxa (bir xil ma'lumot) | **68 sahifa, matn va input qiymatlari aynan bir xil** (hujjatlar, ombor, MES, norma, mahsulot, hisobotlar, balans, logistika) |
| Yozish yo'li: ikkala saytda bir xil 8 tahrir formasi + MES "xomashyo chiqimini qayta hisoblash" (FIFO bilan 24 chiqim hujjati) | ~9 075 qator: faqat vaqt belgilari va ≤ **0.0002** raqam farqi; 24 hujjat jami 55 291 875.06 ↔ .0592 |
| `New-TenantDb` toza / `-Demo` (master merge'dan keyin, 013 DST → 014 → 015 → 017) | ikkalasi kod 0; demo bazada harness 0 xato |
| Smoke (IIS Express): ~100 sahifa — ro'yxat, tafsilot, tahrir, yaratish, hisobotlar | 200 (404 lar — o'chirilgan/yo'q yozuvlar va avvaldan yo'q `Reports/Index`); `.0000` dumi yo'q |

Lokal IPACK: 014/015/017 qo'llangan. Zaxiralar: `MSSQL\Backup\DBTHSN-IPACK_pre_013_float_decimal_20261019.bak` (float holati),
`..._pre_014_renumber_20261019.bak`. Oraliq nomlar (013_Docs…, 016_Reference…) `SCHEMA_VERSIONS` dan tozalandi.

## Ochiq qolgan (keyingi ish)

- **Bajarildi** ([DB_FLOAT_DECIMAL_INTERNAL.md](DB_FLOAT_DECIMAL_INTERNAL.md)): ~~Ichki hisob-kitoblar hali `double`: StockLedger/FIFO (`IssueLine`, `GetOnHand` qaytaradi `double`), UnitConversionService, VatMath,
  SRM/CRM/MES view model'lari. Natija 6 xonaga yaxlitlab saqlanadi, lekin to'liq decimal quvur alohida bosqich (FIFO lot hisobi
  bilan ehtiyotkor, harness bilan).~~
- Hisoblangan `TotalPrice` turlari `decimal(38,12)` — SUM 7.9·10¹⁶ dan oshsa System.Decimal'ga sig'maydi (amalda yo'q). Xohlansa
  ta'rifni `CAST(... AS decimal(19,4))` ga o'tkazish — yaxlitlash qoidasi biznes bilan kelishiladi.
- `DisplayFor(Amount)` (FContract) MVC decimal shabloni bilan `0.00` ko'rsatadi (2-bosqichdan).
