/* =====================================================================
   tahsinERP — ixcham namuna: Sotuvlar (Crm) — bitta mijoz (shartnomali savdo uchun).
   Idempotent: CRM mijozi (Type 'DST' dan boshqa) bor bo'lsa o'tkazib yuboriladi.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Crm') = 0 OR EXISTS (SELECT 1 FROM dbo.CUSTOMERS WHERE ISNULL(Type, '') <> 'DST')
BEGIN
    PRINT 'Namuna mijoz: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

INSERT INTO dbo.CUSTOMERS (Name, Address, Country, Telephone, Email, ContactPersonName, DirectorName, IsDeleted, DUNS, Type, City)
VALUES ('"Namuna Mijoz" MChJ', 'Mustaqillik ko''chasi 1', 'O''zbekiston', '+998 71 000-00-02', NULL, 'Aloqa shaxsi', 'Direktor', 0, NULL, 'MChJ', 'Toshkent');
