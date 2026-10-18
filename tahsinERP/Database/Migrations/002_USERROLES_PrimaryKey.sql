/* =====================================================================
   002 — USERROLES ga PRIMARY KEY (UserID, RoleID).
   Avval jadvalda kalit yo'q edi: bir xil rol ikki marta yozilishi mumkin edi va EF uni faqat
   o'qish uchun (DefiningQuery) ko'rardi. Takrorlar avval olib tashlanadi.
   ===================================================================== */
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('dbo.USERROLES') AND type = 'PK')
BEGIN
    ;WITH d AS (
        SELECT ROW_NUMBER() OVER (PARTITION BY UserID, RoleID ORDER BY (SELECT 0)) AS rn
        FROM dbo.USERROLES
    )
    DELETE FROM d WHERE rn > 1;

    ALTER TABLE dbo.USERROLES ADD CONSTRAINT PK_USERROLES PRIMARY KEY CLUSTERED (UserID, RoleID);
END
GO
