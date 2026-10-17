-- =====================================================================
-- CRM-B18: valyuta kurslari jadvali (foydalanuvchi qarori 2026-10-17: manba — CBU + jadval,
-- hisobotlar so'mda, hujjat sanasidagi kurs bilan).
--
-- Rate — 1 birlik valyuta necha so'm (CBU "Rate / Nominal").
-- Source — 'CBU' (cbu.uz arxivdan avtomatik) yoki 'MANUAL' (CurrencyRates sahifasidan).
-- Ilova jadvalni raw SQL bilan o'qiydi/yozadi (EDMX o'zgarmaydi); jadval bo'lmasa kurslar
-- faqat CBU'dan olinib xotirada keshlanadi.
--
-- Xavfsiz, qayta ishga tushirish mumkin. Ma'lumot o'zgarmaydi.
-- =====================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.CURRENCY_RATES', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CURRENCY_RATES
    (
        ID          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CURRENCY_RATES PRIMARY KEY,
        CurrencyID  INT            NOT NULL CONSTRAINT FK_CURRENCY_RATES_CURRENCIES REFERENCES dbo.CURRENCIES(ID),
        RateDate    DATE           NOT NULL,
        Rate        DECIMAL(18, 6) NOT NULL CONSTRAINT CK_CURRENCY_RATES_Rate CHECK (Rate > 0),
        Source      NVARCHAR(20)   NOT NULL CONSTRAINT DF_CURRENCY_RATES_Source DEFAULT (N'MANUAL'),
        CreatedDT   DATETIME       NOT NULL CONSTRAINT DF_CURRENCY_RATES_CreatedDT DEFAULT (GETDATE()),
        UpdatedDT   DATETIME       NULL
    );

    CREATE UNIQUE INDEX UX_CURRENCY_RATES_Currency_Date ON dbo.CURRENCY_RATES(CurrencyID, RateDate) INCLUDE (Rate);
    PRINT 'CURRENCY_RATES yaratildi.';
END
ELSE
    PRINT 'CURRENCY_RATES allaqachon mavjud.';
GO
