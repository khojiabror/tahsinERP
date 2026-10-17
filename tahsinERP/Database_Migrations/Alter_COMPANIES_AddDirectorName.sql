/* =====================================================================
   COMPANIES.DirectorName — hujjat shablonlaridagi DIRECTOR_NAME uchun (audit DOC-B11).
   Avval xat/buyruq generatsiyasida "X.M. Maxmudov" kodda qattiq yozilgan edi.
   Ustun EDMX'ga qo'shilmaydi: Services/Documents/CompanySettings uni raw SQL bilan o'qiydi
   (ustun hali yo'q bo'lsa — eski qiymatga qaytadi). Idempotent.
   ===================================================================== */
IF COL_LENGTH('dbo.COMPANIES', 'DirectorName') IS NULL
BEGIN
    ALTER TABLE dbo.COMPANIES ADD DirectorName NVARCHAR(200) NULL;
END
GO

-- Hozirgi xulqni saqlash: faol kompaniyaga joriy direktor ismi (boshqa tenant'da o'zgartiring)
UPDATE dbo.COMPANIES
SET DirectorName = N'X.M. Maxmudov'
WHERE DirectorName IS NULL
  AND ID = (SELECT TOP 1 ID FROM dbo.COMPANIES WHERE IsActive = 1 ORDER BY ID);
GO
