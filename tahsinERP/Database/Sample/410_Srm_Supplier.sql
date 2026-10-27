/* =====================================================================
   tahsinERP — ixcham namuna: Ta'minot (Srm) — bitta ta'minotchi. Idempotent: SUPPLIERS bo'sh bo'lmasa o'tkazib yuboriladi.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Srm') = 0 OR EXISTS (SELECT 1 FROM dbo.SUPPLIERS)
BEGIN
    PRINT 'Namuna ta''minotchi: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

INSERT INTO dbo.SUPPLIERS (Name, Address, Country, Telephone, Email, ContactPersonName, DirectorName, IsDeleted, DUNS, Type, City)
VALUES ('"Namuna Ta''minotchi" MChJ', 'Sanoat ko''chasi 1', 'O''zbekiston', '+998 71 000-00-01', NULL, 'Aloqa shaxsi', 'Direktor', 0, NULL, 'MChJ', 'Toshkent');
