/* =====================================================================
   tahsinERP — ixcham namuna: Logistika — bitta ekspeditor (tashuvchi). Idempotent: FORWARDERS bo'sh bo'lmasa o'tkazib yuboriladi.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Logistics') = 0 OR EXISTS (SELECT 1 FROM dbo.FORWARDERS)
BEGIN
    PRINT 'Namuna ekspeditor: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

INSERT INTO dbo.FORWARDERS (ForwarderName, Director, ContactPerson, Description, IsDeleted)
VALUES ('"Namuna Logistik" MChJ', 'Direktor', 'Aloqa shaxsi', 'Namuna — o''z tashuvchingiz bilan almashtiring', 0);
