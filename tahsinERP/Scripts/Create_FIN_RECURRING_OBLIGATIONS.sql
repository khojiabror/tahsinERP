-- Doimiy harajatlar (buxgalteriya, ijara va h.k.) uchun jadval.
-- Balans hisobotida har oy shakllanadigan qarz va to'lovlar orqali kamayishi ko'rsatiladi.
-- Loyihani bazaga ulagach bu skriptni bitta marta bajarishingiz kerak.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FIN_RECURRING_OBLIGATIONS')
BEGIN
    CREATE TABLE dbo.FIN_RECURRING_OBLIGATIONS (
        ID int IDENTITY(1,1) NOT NULL,
        Name nvarchar(200) NOT NULL,
        MonthlyAmount decimal(18,2) NOT NULL,
        CurrencyID int NOT NULL,
        StartYear int NOT NULL,
        StartMonth tinyint NOT NULL,
        BalanceCategoryID int NULL,
        IsActive bit NOT NULL CONSTRAINT DF_FIN_RECURRING_OBLIGATIONS_IsActive DEFAULT 1,
        CreatedByUserID int NOT NULL,
        CreatedDT datetime2(0) NOT NULL CONSTRAINT DF_FIN_RECURRING_OBLIGATIONS_CreatedDT DEFAULT GETDATE(),
        IsDeleted bit NULL,
        CONSTRAINT PK_FIN_RECURRING_OBLIGATIONS PRIMARY KEY (ID),
        CONSTRAINT FK_FIN_RECURRING_OBLIGATIONS_Currency FOREIGN KEY (CurrencyID) REFERENCES dbo.CURRENCy(ID),
        CONSTRAINT FK_FIN_RECURRING_OBLIGATIONS_BalanceCategory FOREIGN KEY (BalanceCategoryID) REFERENCES dbo.FIN_BALANCE_CATEGORIES(ID),
        CONSTRAINT FK_FIN_RECURRING_OBLIGATIONS_User FOREIGN KEY (CreatedByUserID) REFERENCES dbo.[USER](ID)
    );

    PRINT 'FIN_RECURRING_OBLIGATIONS jadvali yaratildi.';
END
ELSE
    PRINT 'FIN_RECURRING_OBLIGATIONS jadvali allaqachon mavjud.';
GO

-- "Doimiy harajatlar qarzi" balans kategoriyasi yo'q bo'lsa qo'shish (Type=2 passiv, CalculationType=3 hujjat/recurring)
IF NOT EXISTS (SELECT 1 FROM dbo.FIN_BALANCE_CATEGORIES WHERE Name = N'Doimiy harajatlar qarzi')
BEGIN
    DECLARE @SortOrder int = (SELECT ISNULL(MAX(SortOrder), 0) + 1 FROM dbo.FIN_BALANCE_CATEGORIES WHERE Type IN (2,3));
    INSERT INTO dbo.FIN_BALANCE_CATEGORIES (Name, Type, ParentID, SortOrder, IsActive, CalculationType, IsDeleted)
    VALUES (N'Doimiy harajatlar qarzi', 2, NULL, @SortOrder, 1, 3, NULL);
    PRINT 'Balans kategoriyasi "Doimiy harajatlar qarzi" qo''shildi.';
END
GO
