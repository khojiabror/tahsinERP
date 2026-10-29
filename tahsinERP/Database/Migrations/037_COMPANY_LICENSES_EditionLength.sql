/* =====================================================================
   036 — COMPANY_LICENSES.Edition: NVARCHAR(50) -> NVARCHAR(200).
   Sinovda tanlangan modullar kalitning Edition qatoriga yoziladi ("TRIAL:Srm,Crm,Tasks,Finance,Commerce,Distribution+SITE"),
   bog'liqliklar (Savdo -> Moliya, Distributsiya -> Savdo) qo'shilgach 50 belgidan oshadi — kalitni faollashtirish
   EF validatsiyasida yiqilardi. Panel tomonida ham Licenses.Edition kengaytirildi (EF migratsiya LicenseEditionLength).
   Idempotent.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COMPANY_LICENSES') AND name = 'Edition' AND max_length < 400)
    ALTER TABLE dbo.COMPANY_LICENSES ALTER COLUMN Edition NVARCHAR(200) NULL;
GO
