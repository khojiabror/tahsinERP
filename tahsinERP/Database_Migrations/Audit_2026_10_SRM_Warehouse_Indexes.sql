/* =====================================================================
   Audit_2026_10_SRM_Warehouse_Indexes.sql — SRM Ombor/Inventar indeks taklifi
   (sxemaga yangi ustun/jadval qo'shilmaydi, faqat indekslar).

   Tartib:
     1. DataFix_SRM_Warehouse_02_Fix.sql (@Commit = 1) — PRODUCT_STOCKS dublikatlari birlashtirilgan
        bo'lishi SHART, aks holda UX_PRODUCT_STOCKS_WH_Product yaratilmaydi.
     2. Shu skript (avval test nusxada; ish vaqtidan tashqari).

   Core'ning Audit_2026_10_Indexes.sql dagi IX_PRODUCT_STOCKS_Wh_Product (unique emas) o'rniga
   shu yerdagi UNIQUE indeks tavsiya etiladi — ikkalasi bir vaqtda kerak emas.
   ===================================================================== */
SET NOCOUNT ON;

/* 1. PRODUCT_STOCKS: (ombor, mahsulot) bo'yicha yagona qator — SRM-B01.
      StockLedger.Adjust shu kalit bo'yicha UPDLOCK+HOLDLOCK bilan ishlaydi; unique indeks
      dublikat paydo bo'lishini baza darajasida taqiqlaydi va qulfni bitta kalit oralig'iga toraytiradi. */
IF EXISTS (SELECT 1 FROM dbo.PRODUCT_STOCKS GROUP BY WHID, ProductID HAVING COUNT(*) > 1)
    PRINT 'XATO: PRODUCT_STOCKS da dublikatlar bor — avval DataFix_SRM_Warehouse_02_Fix.sql (F1).';
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PRODUCT_STOCKS_WH_Product' AND object_id = OBJECT_ID('dbo.PRODUCT_STOCKS'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PRODUCT_STOCKS_Wh_Product' AND object_id = OBJECT_ID('dbo.PRODUCT_STOCKS'))
        DROP INDEX IX_PRODUCT_STOCKS_Wh_Product ON dbo.PRODUCT_STOCKS;
    CREATE UNIQUE INDEX UX_PRODUCT_STOCKS_WH_Product ON dbo.PRODUCT_STOCKS(WHID, ProductID) INCLUDE (Amount, UnitID);
    PRINT 'UX_PRODUCT_STOCKS_WH_Product yaratildi.';
END

/* 2. Hujjat raqami: DocumentNumberService "DocNo LIKE 'prefiks%'" bo'yicha MAX oladi; Details/
      Reject eski DocNo zaxirasi ham shu ustun bo'yicha qidiradi. (UNIQUE emas: tarixda takroriy
      raqamlar bor — 01_Check §7.) */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_INCOMES_DocNo' AND object_id = OBJECT_ID('dbo.INCOMES'))
    CREATE INDEX IX_INCOMES_DocNo ON dbo.INCOMES(DocNo) INCLUDE (IsDeleted, ReceivingWHID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EXPENSES_DocNo' AND object_id = OBJECT_ID('dbo.EXPENSES'))
    CREATE INDEX IX_EXPENSES_DocNo ON dbo.EXPENSES(DocNo) INCLUDE (IsDeleted, SenderWHID, ReceiverWhID);

/* 3. DLOC: kirimni bekor qilish (StockLedger.ReverseIncomeReceipt) lotlarni IncomeID bo'yicha topadi;
      chiqimni bekor qilish ajratmalarni ExpenseProductID bo'yicha (Core skriptida IX_EPA_ExpenseProductID bor). */
IF OBJECT_ID('dbo.INCOME_LOTS', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IL_IncomeID' AND object_id = OBJECT_ID('dbo.INCOME_LOTS'))
    CREATE INDEX IX_IL_IncomeID ON dbo.INCOME_LOTS(IncomeID) INCLUDE (ProductID, QtyReceived, QtyConsumed, IsDeleted);

IF OBJECT_ID('dbo.EXPENSE_PRODUCT_ALLOCATIONS', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EPA_LotID' AND object_id = OBJECT_ID('dbo.EXPENSE_PRODUCT_ALLOCATIONS'))
    CREATE INDEX IX_EPA_LotID ON dbo.EXPENSE_PRODUCT_ALLOCATIONS(LotID) INCLUDE (ExpenseProductID, Qty);

PRINT 'SRM ombor indekslari tayyor.';
