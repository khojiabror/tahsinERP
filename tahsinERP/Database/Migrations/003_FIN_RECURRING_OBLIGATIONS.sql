/* =====================================================================
   003 — FIN_RECURRING_OBLIGATIONS (doimiy harajatlar: ijara, buxgalteriya va h.k.).
   Kod (FinRecurringObligationsController, FinLiabilities) jadvalni xom SQL bilan o'qiydi, lekin
   eski Scripts/Create_FIN_RECURRING_OBLIGATIONS.sql noto'g'ri FK nomlari (CURRENCy, [USER]) sababli
   ishlamas edi — jadval bazalarda yo'q. Bu yerda to'g'ri jadvallarga (CURRENCIES, USERS).
   "Doimiy harajatlar qarzi" balans kategoriyasi (SystemCode = RECURRING_OBLIGATIONS) yangi bazalarga
   Seed/100_Reference.sql dan keladi; mavjud bazaga avtomatik qo'shilmaydi (balans ko'rinishi o'zgarmasin).
   ===================================================================== */
IF OBJECT_ID(N'dbo.FIN_RECURRING_OBLIGATIONS', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FIN_RECURRING_OBLIGATIONS (
        ID                int IDENTITY(1,1) NOT NULL,
        Name              nvarchar(200) NOT NULL,
        MonthlyAmount     decimal(18,2) NOT NULL,
        CurrencyID        int NOT NULL,
        StartYear         int NOT NULL,
        StartMonth        tinyint NOT NULL,
        BalanceCategoryID int NULL,
        IsActive          bit NOT NULL CONSTRAINT DF_FIN_RECURRING_OBLIGATIONS_IsActive DEFAULT (1),
        CreatedByUserID   int NOT NULL,
        CreatedDT         datetime2(0) NOT NULL CONSTRAINT DF_FIN_RECURRING_OBLIGATIONS_CreatedDT DEFAULT (GETDATE()),
        IsDeleted         bit NULL,
        CONSTRAINT PK_FIN_RECURRING_OBLIGATIONS PRIMARY KEY (ID),
        CONSTRAINT FK_FIN_RECURRING_OBLIGATIONS_Currency FOREIGN KEY (CurrencyID) REFERENCES dbo.CURRENCIES (ID),
        CONSTRAINT FK_FIN_RECURRING_OBLIGATIONS_BalanceCategory FOREIGN KEY (BalanceCategoryID) REFERENCES dbo.FIN_BALANCE_CATEGORIES (ID),
        CONSTRAINT FK_FIN_RECURRING_OBLIGATIONS_User FOREIGN KEY (CreatedByUserID) REFERENCES dbo.USERS (ID)
    );
END
GO
