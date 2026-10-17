# CRM skriptlari — qo'llanish jurnali

Baza: lokal `localhost / DBTHSN-IPACK` (ulanish `DbConfig.GetSqlConnectionString()`), 2026-10-17, foydalanuvchi ko'rsatmasi bilan.

| Skript | Natija |
|---|---|
| `CRM_2026_10_Indexes.sql` | 7 ta FK/filtr indeksi yaratildi. `UX_SALES_INVOICES_InvoiceNo` bazada oldindan bor edi (takroriy raqam yo'q). SF/TTNK sarlavha/qator indekslari mavjud `UX_*` bilan qoplangani uchun olib tashlandi va skriptdan chiqarildi. |
| `CRM_2026_10_CurrencyRates.sql` | `CURRENCY_RATES` jadvali yaratildi. Hozir barcha CRM hujjatlari UZS'da (62 shartnoma, 93 buyurtma, 211 invoys, 44 to'lov) — kurs talab qilinmaydi. |
| `DataFix_CRM_B02_DuplicateInvoiceNo.sql` | Takroriy raqam yo'q — tuzatish kerak bo'lmadi. |
| `DataFix_CRM_M02_InvoiceCurrencyID.sql` | 211 ta invoysga `CurrencyID = 5 (UZS)` yozildi. Noto'g'ri valyutadagi posting yo'q edi. |
| `DataFix_CRM_B06_B10_DeliverySchedule.sql` | 1 ta yetim reja qatori (ID 27, buyurtma 77) yangi buyurtma qatoriga (200 → 201) bog'landi. |
| `DataFix_CRM_B04_CustomerImportSuppliers.sql` | Import orqali yaratilgan yetkazib beruvchi yo'q. |
| `DataFix_CRM_B01_InvoiceStockFinance.sql` | Quyida. |

## B01 natijasi
- **Sabab topildi:** `SALES_INVOICES.CurrencyID` bo'sh edi → `FinancePostingService` `CurrencyID ?? 1` bilan hisob ochmoqchi bo'lardi, bazada esa ID=1 valyuta yo'q (2–5) → FK xatosi har safar jimgina yutilgan. Natijada **hech bir sotuv invoysi finance'ga post qilinmagan**. Kod tuzatildi (CurrencyID kod bo'yicha, bo'lmasa UZS).
- 22 ta APPROVED invoys (2026-04-08 … 2026-05-23, jami 224 732 971,71 so'm) ilovaning o'z `FinancePostingService.PostSalesInvoice` mantiqi bilan post qilindi → `RECEIVABLES_5 "Debitorlar qarzi"` hisobi (ID 34), toifa "Sotuv" (11).
- Bekor qilingan 203-invoys (№359) avto-chiqimi qaytarilmagan edi (iPack 3.4.1, 5 dona) — `WarehouseFlowService.RollbackSalesInvoiceStockAndDeleteAutoExpenses` bilan qaytarildi (marker to'qnashuvi yo'qligi tekshirildi).
- **Tegilmagan (eski oqim):** 187 ta DRAFT invoys (176 tasi 2025 yil importi, 11 tasi 2026-mart) — ombor chiqimi va posting yo'q; 2026-martdagi 4 tasida chiqim bor. Ular yangi oqimdan oldingi hujjatlar; ularni post qilish/chiqim qilish buxgalteriya qarori bilan alohida.

Ilova mantiqini chaqirish uchun vaqtinchalik konsol vosita (CrmFix: `post`, `cancel --id=N`, `--apply`) ishlatildi; repoga qo'shilmadi.

## 2026-10-18
| Skript | Natija |
|---|---|
| `CRM_2026_10_M02_DropCurrencyColumns.sql` | Foydalanuvchi qarori (M02): `SALES_INVOICES.CurrencyCode` va `S_ORDERS.Currency` olib tashlandi; yagona manba `CurrencyID`. Zaxira: `SALES_INVOICES_CurrencyCode_BAK_20261018` (211), `S_ORDERS_Currency_BAK_20261018` (95). EDMX bir vaqtda yangilangan — eski EDMX'li branch bu jadvallarda yiqiladi, master'ni olishi kerak. |

187 ta eski DRAFT invoys — foydalanuvchi qarori: tegilmaydi.
