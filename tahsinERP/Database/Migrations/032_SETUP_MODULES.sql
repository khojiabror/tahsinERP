/* =====================================================================
   032 — SETUP_MODULES: baza qaysi mahsulot modullari uchun tayyorlangan (sinov ochishda xodim tanlagan modullar).
   Module = EditionModules.ProductModule nomi (Srm, Crm, Tasks, Mes, Engineering, Finance, Hr, Logistics, Docs,
   Distribution, Website). Jadvalni New-TenantDb.ps1 -Modules to'ldiradi; Setup/300_ModuleProfile.sql (rollar, omborlar)
   va Demo fayllari (namuna ma'lumot) shunga qarab o'z qismini qo'llaydi yoki o'tkazib yuboradi.
   Bo'sh jadval = hamma modul (eski bazalar, demo shabloni). Modul ruxsatining o'zi — litsenziya kalitidagi Edition
   ("TRIAL:Srm,Crm", EditionGate); bu jadval faqat boshlang'ich sozlama tarixi, ERP EF modeli uni o'qimaydi.
   ===================================================================== */
IF OBJECT_ID(N'dbo.SETUP_MODULES', N'U') IS NULL
    CREATE TABLE dbo.SETUP_MODULES (
        Module    varchar(30)  NOT NULL CONSTRAINT PK_SETUP_MODULES PRIMARY KEY,
        CreatedDT datetime2(0) NOT NULL CONSTRAINT DF_SETUP_MODULES_CreatedDT DEFAULT (SYSDATETIME())
    );
GO
-- 1 — modul tanlangan yoki jadval bo'sh (hamma modul); 0 — tanlanmagan.
CREATE OR ALTER FUNCTION dbo.fn_SetupHasModule (@Module varchar(30))
RETURNS bit
AS
BEGIN
    RETURN CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.SETUP_MODULES)
                  OR EXISTS (SELECT 1 FROM dbo.SETUP_MODULES WHERE Module = @Module) THEN 1 ELSE 0 END;
END
GO
