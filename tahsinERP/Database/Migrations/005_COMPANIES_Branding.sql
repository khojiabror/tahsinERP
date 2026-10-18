/* =====================================================================
   005 — Korxona brendi: _Layout dagi nom/logo/rang endi COMPANIES dan (avval "iPack" qattiq yozilgan edi).
   NULL = standart ko'rinish (yashil #198754 va app-assets logosi) — iPack ko'rinishi o'zgarmaydi.
   Tahrirlash: Sozlamalar -> Korxona ma'lumotlari. Kod: Services/Common/BrandingService.cs.
   ===================================================================== */
IF COL_LENGTH('dbo.COMPANIES', 'BrandColor') IS NULL
    ALTER TABLE dbo.COMPANIES ADD BrandColor varchar(7) NULL
        CONSTRAINT CK_COMPANIES_BrandColor CHECK (BrandColor IS NULL OR BrandColor LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]');
IF COL_LENGTH('dbo.COMPANIES', 'LogoImage') IS NULL
    ALTER TABLE dbo.COMPANIES ADD LogoImage varbinary(max) NULL;
IF COL_LENGTH('dbo.COMPANIES', 'LogoContentType') IS NULL
    ALTER TABLE dbo.COMPANIES ADD LogoContentType varchar(50) NULL;
GO
