/* =====================================================================
   Audit 2026-10 — Tasks + Documents: TUZATISH (DataFix_Docs_01_Find.sql natijasidan keyin).
   ISHGA TUSHIRISHDAN OLDIN: bazaning zaxira nusxasini oling va 01_Find natijasini ko'rib chiqing.
   Standart holatda ROLLBACK bilan tugaydi — natijani tekshirib, oxirgi qatorni COMMIT'ga almashtiring.
   Xat/buyruq/STD takroriy raqamlari bu yerda TUZATILMAYDI (tashqariga yuborilgan raqam); topshiriq kodlari — 5-bo'lim.
   2026-10-17: lokal DBTHSN-IPACK bazasida COMMIT bilan ishga tushirildi (zaxira:
   ...\MSSQL\Backup\DBTHSN-IPACK_before_tasks_docs_2026-10-17.bak). Natija: 1 ta yetim kiruvchi xat
   sarlavhasi (ID 181) soft-delete, 13 ta takroriy TASK-10 -> TASK-26..TASK-38; qolgan bo'limlarda o'zgarish yo'q.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

/* 1. STD: o'chirilgan hujjatning standart qatorini ham o'chirilgan deb belgilash (DOC-B01). */
UPDATE s SET s.IsDeleted = 1
FROM dbo.STD_COMPANY_STANDARDS s
JOIN dbo.DOC_DOCUMENTS d ON d.ID = s.DocumentID
WHERE d.IsDeleted = 1 AND (s.IsDeleted IS NULL OR s.IsDeleted = 0);
PRINT CONCAT('STD standartlar belgilandi: ', @@ROWCOUNT);

/* 2. O'chirilgan hujjatlarning fayllari. */
UPDATE f SET f.IsDeleted = 1
FROM dbo.DOC_FILES f
JOIN dbo.DOC_DOCUMENTS d ON d.ID = f.DocumentID
WHERE d.IsDeleted = 1 AND (f.IsDeleted IS NULL OR f.IsDeleted = 0);
PRINT CONCAT('Fayllar belgilandi: ', @@ROWCOUNT);

/* 3. Yetim xat/buyruq sarlavhalarini soft-delete (fayllari bilan). Ular hech qaysi ro'yxatda
      ko'rinmasdi. Jismoniy o'chirilmaydi — kerak bo'lsa IsDeleted=0 qilib qaytarish mumkin. */
DECLARE @orphans TABLE (ID BIGINT PRIMARY KEY);
INSERT INTO @orphans (ID)
SELECT d.ID
FROM dbo.DOC_DOCUMENTS d
JOIN dbo.DOC_DOCUMENT_TYPES t ON t.ID = d.DocTypeID
WHERE (d.IsDeleted IS NULL OR d.IsDeleted = 0)
  AND (   (t.Code = 'OFFICIAL_LETTER' AND NOT EXISTS (SELECT 1 FROM dbo.COM_OFFICIAL_LETTERS l WHERE l.DocumentID = d.ID))
       OR (t.Code = 'COMP_ORDER'      AND NOT EXISTS (SELECT 1 FROM dbo.ADM_COMPANY_ORDERS o WHERE o.DocumentID = d.ID)));

UPDATE d SET d.IsDeleted = 1, d.UpdatedDT = GETDATE()
FROM dbo.DOC_DOCUMENTS d JOIN @orphans o ON o.ID = d.ID;
PRINT CONCAT('Yetim hujjatlar belgilandi: ', @@ROWCOUNT);

UPDATE f SET f.IsDeleted = 1
FROM dbo.DOC_FILES f JOIN @orphans o ON o.ID = f.DocumentID
WHERE f.IsDeleted IS NULL OR f.IsDeleted = 0;

/* 4. Bir nechta IsMain=1: eng katta VersionNo (teng bo'lsa eng katta ID) asosiy qoladi. */
;WITH ranked AS (
    SELECT ID, ROW_NUMBER() OVER (PARTITION BY DocumentID, FileExtension ORDER BY VersionNo DESC, ID DESC) AS rn
    FROM dbo.DOC_FILES
    WHERE IsMain = 1 AND (IsDeleted IS NULL OR IsDeleted = 0)
)
UPDATE f SET f.IsMain = 0
FROM dbo.DOC_FILES f JOIN ranked r ON r.ID = f.ID
WHERE r.rn > 1;
PRINT CONCAT('Ortiqcha IsMain olib tashlandi: ', @@ROWCOUNT);

/* 5. Takroriy topshiriq kodlari (TSK-B05: KeyCode qulfsiz hisoblanardi — masalan, 14 ta TASK-10).
      Har guruhda eng birinchi (eng kichik ID) kod saqlanadi, qolganlari shu prefiksdagi
      navbatdagi bo'sh raqamni oladi. Topshiriq kodi ichki identifikator — tashqariga yuborilmaydi. */
;WITH dup AS (
    SELECT ID, KeyCode,
           LEFT(KeyCode, LEN(KeyCode) - CHARINDEX('-', REVERSE(KeyCode)) + 1) AS Prefix,
           ROW_NUMBER() OVER (PARTITION BY KeyCode ORDER BY ID) AS rn
    FROM dbo.APP_TASKS
    WHERE KeyCode IS NOT NULL AND KeyCode IN (SELECT KeyCode FROM dbo.APP_TASKS WHERE KeyCode IS NOT NULL GROUP BY KeyCode HAVING COUNT(*) > 1)
), renum AS (
    SELECT d.ID, d.Prefix,
           ROW_NUMBER() OVER (PARTITION BY d.Prefix ORDER BY d.ID) AS seq,
           m.MaxNo
    FROM dup d
    CROSS APPLY (
        SELECT MAX(TRY_CAST(SUBSTRING(t.KeyCode, p.Len + 1, 50) AS BIGINT)) AS MaxNo
        FROM dbo.APP_TASKS t
        CROSS APPLY (SELECT LEN(d.Prefix) AS Len) p
        WHERE t.KeyCode LIKE REPLACE(d.Prefix, '_', '[_]') + '%'
    ) m
    WHERE d.rn > 1
)
UPDATE t SET t.KeyCode = r.Prefix + CAST(r.MaxNo + r.seq AS NVARCHAR(20)), t.UpdatedDT = GETDATE()
FROM dbo.APP_TASKS t JOIN renum r ON r.ID = t.ID;
PRINT CONCAT('Takroriy topshiriq kodlari qayta raqamlandi: ', @@ROWCOUNT);

-- Tekshirib bo'lgach: ROLLBACK ni COMMIT ga almashtiring
ROLLBACK TRANSACTION;
