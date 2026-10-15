/*
  Xarid (P_INVOICES) va sotuv (SALES_INVOICES) uchun 2024–2025 yillar bo‘yicha
  "to‘langan / pul kelib tushgan" ko‘rinishi.

  OPTIMAL YECHIM (agar allaqachon FIN da "Material" va hokazo bilan to'lovlar bo'lsa):
  - Yangi INSERT qo'shmasdan, mavjud FIN_TRANSACTIONS qatorlarida CounterpartyID,
    CounterpartyName, ContractNo (P_CONTRACTS.ContractNo), SourceDocID (invoys ID)
    ni UPDATE qiling — summalar o'zgarmaydi, moliyaviy jami buzilmaydi, faqat
    akt-sverki va invoys bilan bog'lanish to'g'rilanadi.
  - Har bir invoys uchun QO'SHIMCHA to'lov qatori yaratish — agar xuddi shu pul
    allaqachon Material kabi kategoriyada yozilgan bo'lsa, kassa/chiqimni IKKI marta
    hisoblaydi (FinDashboard CategoryAnalytics va boshqalar).
  - SourceDocType: maxsus nom shart emas — tizimda Manual qatorlar ham qabul qilinadi
    (FinTransactionsController). Akt-sverkida muhim: Direction + Counterparty +
    SourceDocType <> PurchaseInvoice mirror.

  Modellar:
  - Alohida to'lovlar jadvali yo'q; haqiqiy to'lovlar FIN_TRANSACTIONS da.
  - PurchaseInvoice / SalesInvoice qatorlari — "oyna" (ReconciliationService).

  DIQQAT:
  - BEGIN TRANSACTION; tekshiring, keyin COMMIT.
  - @CreatedByUserID ni o'zgartiring.
  - SourceDocID INT; SALES_INVOICES.ID > 2147483647 bo'lsa INSERT tushmaydi.
*/

SET NOCOUNT ON;

DECLARE @CreatedByUserID int = 1;  -- <-- o'zgartiring

/* Bank (kirim/chiqim) — sotuv to'lovi yoki zaxira */
DECLARE @DefaultBankAccountID int =
(
    SELECT TOP (1) a.ID
    FROM dbo.FIN_ACCOUNTS a
    WHERE a.IsActive = 1 AND a.IsBank = 1
    ORDER BY a.ID
);

IF @DefaultBankAccountID IS NULL
BEGIN
    RAISERROR(N'Faol bank hisobi (FIN_ACCOUNTS, IsBank=1) topilmadi. Avval yarating.', 16, 1);
    RETURN;
END

/* Xarid: avvalo "Material", bo'lmasa Xarid xarajatlari (PostingService) */
DECLARE @CatMaterial int =
(
    SELECT TOP (1) c.ID FROM dbo.FIN_CATEGORIES c
    WHERE c.IsActive = 1 AND c.Type = 2 AND (c.Name = N'Material' OR c.Name LIKE N'%Material%')
    ORDER BY CASE WHEN c.Name = N'Material' THEN 0 ELSE 1 END, c.ID
);
DECLARE @CatPurchaseExpense int =
(
    SELECT TOP (1) c.ID FROM dbo.FIN_CATEGORIES c
    WHERE c.IsActive = 1 AND c.Type = 2 AND c.Name LIKE N'%Xarid%'
    ORDER BY c.ID
);
DECLARE @CatPurchase int = COALESCE(@CatMaterial, @CatPurchaseExpense);
DECLARE @CatSalesIncome int =
(
    SELECT TOP (1) c.ID FROM dbo.FIN_CATEGORIES c
    WHERE c.IsActive = 1 AND c.Type = 1 AND c.Name LIKE N'%Sotuv%'
    ORDER BY c.ID
);

IF @CatPurchase IS NULL OR @CatSalesIncome IS NULL
BEGIN
    RAISERROR(N'FIN_CATEGORIES: Material/Xarid (Type=2) yoki Sotuv (Type=1) topilmadi.', 16, 1);
    RETURN;
END

/* ========== 1) Xarid: ta'minotchiga to'lov (biz to'lagan) ========== */
INSERT INTO dbo.FIN_TRANSACTIONS
(
    TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID,
    CounterpartyType, CounterpartyID, CounterpartyName, Purpose,
    SourceDocType, SourceDocID, ContractNo, Comment,
    CreatedByUserID, CreatedDT, IsDeleted
)
SELECT
    p.InvoiceDate,
    COALESCE(pa.ID, ba.ID, @DefaultBankAccountID),
    @CatPurchase,
    2,  -- chiqim
    CAST(ISNULL(p.Amount, 0) AS decimal(18, 2)),
    ISNULL(p.CurrencyID, 1),
    2,
    p.SupplierID,
    s.Name,
    N'Xarid invoysi #' + ISNULL(p.InvoiceNo, N'') + N' — to''lov (SQL 2024–2025)',
    N'Manual',
    p.ID,
    pc.ContractNo,
    NULL,
    @CreatedByUserID,
    SYSUTCDATETIME(),
    0
FROM dbo.P_INVOICES p
INNER JOIN dbo.SUPPLIER s ON s.ID = p.SupplierID
LEFT JOIN dbo.P_CONTRACTS pc ON pc.ID = p.ContractID
OUTER APPLY
(
    /* Payables-5 va shu kabi kreditor/payables hisoblari (valyuta bo'yicha) */
    SELECT TOP (1) a.ID
    FROM dbo.FIN_ACCOUNTS a
    WHERE a.IsActive = 1
      AND (a.Name LIKE N'%Payables%' OR a.Code LIKE N'%Payables%' OR a.Name LIKE N'%Kreditor%')
      AND a.CurrencyID = ISNULL(p.CurrencyID, 1)
    ORDER BY a.ID
) pa
OUTER APPLY
(
    SELECT TOP (1) a.ID
    FROM dbo.FIN_ACCOUNTS a
    WHERE a.IsActive = 1 AND a.IsBank = 1 AND a.CurrencyID = ISNULL(p.CurrencyID, 1)
    ORDER BY a.ID
) ba
WHERE YEAR(p.InvoiceDate) IN (2024, 2025)
  AND ISNULL(p.IsDeleted, 0) = 0
  AND ISNULL(p.Amount, 0) > 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.FIN_TRANSACTIONS t
      WHERE t.SourceDocID = p.ID
        AND t.CounterpartyType = 2
        AND t.Direction = 2
        AND ISNULL(t.IsDeleted, 0) = 0
        AND (t.SourceDocType IS NULL OR t.SourceDocType <> N'PurchaseInvoice')
  );

DECLARE @PurchasePaymentsInserted int = @@ROWCOUNT;

/* ========== 2) Sotuv: Status PAID + mijozdan kirim yozuvi ========== */
UPDATE dbo.SALES_INVOICES
SET StatusCode = N'PAID',
    UpdatedDT = SYSUTCDATETIME()
WHERE YEAR(InvoiceDate) IN (2024, 2025)
  AND StatusCode IN (N'APPROVED', N'FINALIZED')
  AND TotalAmount > 0;

DECLARE @SalesInvoicesMarkedPaid int = @@ROWCOUNT;

INSERT INTO dbo.FIN_TRANSACTIONS
(
    TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID,
    CounterpartyType, CounterpartyID, CounterpartyName, Purpose,
    SourceDocType, SourceDocID, ContractNo, Comment,
    CreatedByUserID, CreatedDT, IsDeleted
)
SELECT
    si.InvoiceDate,
    COALESCE(ba.ID, @DefaultBankAccountID),
    @CatSalesIncome,
    1,  -- kirim
    si.TotalAmount,
    ISNULL(si.CurrencyID, 1),
    1,
    si.CustomerID,
    c.Name,
    N'Sotuv invoysi #' + ISNULL(si.InvoiceNo, N'') + N' — to''lov qabul (SQL 2024–2025)',
    N'Manual',
    CAST(si.ID AS int),
    sc.ContractNo,
    NULL,
    @CreatedByUserID,
    SYSUTCDATETIME(),
    0
FROM dbo.SALES_INVOICES si
INNER JOIN dbo.CUSTOMER c ON c.ID = si.CustomerID
LEFT JOIN dbo.S_CONTRACTS sc ON sc.ID = si.ContractID
OUTER APPLY
(
    SELECT TOP (1) a.ID
    FROM dbo.FIN_ACCOUNTS a
    WHERE a.IsActive = 1 AND a.IsBank = 1 AND a.CurrencyID = ISNULL(si.CurrencyID, 1)
    ORDER BY a.ID
) ba
WHERE YEAR(si.InvoiceDate) IN (2024, 2025)
  AND si.StatusCode = N'PAID'
  AND si.TotalAmount > 0
  AND si.ID <= 2147483647
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.FIN_TRANSACTIONS t
      WHERE t.SourceDocID = CAST(si.ID AS int)
        AND t.CounterpartyType = 1
        AND t.Direction = 1
        AND ISNULL(t.IsDeleted, 0) = 0
        AND (t.SourceDocType IS NULL OR t.SourceDocType <> N'SalesInvoice')
  );

DECLARE @CustomerPaymentsInserted int = @@ROWCOUNT;

SELECT
    @PurchasePaymentsInserted AS PurchasePaymentRowsInserted,
    @SalesInvoicesMarkedPaid AS SalesInvoicesSetToPaid,
    @CustomerPaymentsInserted AS CustomerPaymentRowsInserted;
