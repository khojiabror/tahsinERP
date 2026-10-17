/* =====================================================================
   TAKLIF (ishga tushirilmagan) — Tasks + Documents indekslari, audit 2026-10.
   Avval jonli bazada sys.indexes bilan mavjudlarini tekshiring; UNIQUE'lardan oldin
   DataFix_Docs_01_Find.sql (5-bo'lim) takroriy raqamlarni topmasligi kerak.
   ===================================================================== */

-- DOC_FILES: metadata ro'yxatlari (ListMain/ListVersions/LatestMainFileId) va set-based UPDATE'lar
CREATE INDEX IX_DOC_FILES_Document ON dbo.DOC_FILES (DocumentID, IsMain, FileExtension)
    INCLUDE (VersionNo, FileName, MimeType, FileSizeBytes, CreatedDT, IsDeleted);

-- Raqamlash (DocNumbering / DocumentNumberService MAX so'rovlari)
CREATE INDEX IX_COM_OFFICIAL_LETTERS_Dir_No ON dbo.COM_OFFICIAL_LETTERS (DirectionCode, LetterNo) INCLUDE (IsDeleted, LetterDate);
CREATE INDEX IX_ADM_COMPANY_ORDERS_No ON dbo.ADM_COMPANY_ORDERS (OrderNo) INCLUDE (IsDeleted, OrderDate);
CREATE INDEX IX_DOC_DOCUMENTS_Code ON dbo.DOC_DOCUMENTS (Code) INCLUDE (IsDeleted);

-- Topshiriqlar (CORE.md'dagi IX_APP_TASKS_Assignee'ga qo'shimcha)
CREATE INDEX IX_APP_TASKS_Project ON dbo.APP_TASKS (ProjectID, IsDeleted) INCLUDE (StatusID, AssigneeUserID, CreatedDT);
CREATE INDEX IX_APP_TASKS_KeyCode ON dbo.APP_TASKS (KeyCode);

-- Ixtiyoriy, takrorlar tozalangandan keyin — raqam takrorini baza darajasida ham taqiqlash:
-- CREATE UNIQUE INDEX UX_APP_TASKS_KeyCode ON dbo.APP_TASKS (KeyCode) WHERE KeyCode IS NOT NULL AND IsDeleted = 0;
-- CREATE UNIQUE INDEX UX_COM_OFFICIAL_LETTERS_OutNo ON dbo.COM_OFFICIAL_LETTERS (LetterNo) WHERE DirectionCode = 'OUT' AND IsDeleted IS NULL;  -- IsDeleted nullable: filtrni moslang
-- CREATE UNIQUE INDEX UX_ADM_COMPANY_ORDERS_No ON dbo.ADM_COMPANY_ORDERS (OrderNo) WHERE IsDeleted IS NULL;

/* Sxema takliflari (EDMX yangilashni talab qiladi — qaror sizda):
   - DOC_FILES.FileContent'ni alohida DOC_FILE_CONTENT (DocumentFileID PK, FileContent) jadvaliga ajratish
     yoki EF table splitting (DOC-P02); uzoq muddatda S3/disk.
   - APP_TASK_STATUS_REF / APP_TASK_PRIORITY_REF: IsFinal BIT, ColorHex NVARCHAR(7) (MNT-01, TSK-B09).
   - COM_OFFICIAL_LETTERS: BodyHtml NVARCHAR(MAX), GreetingLine; mahsulot satrlari uchun bola jadval —
     hozir DOC_FILES ichida "_letterdata.json" blob (DOC-P10).
   - Kompaniya sozlamalarida direktor F.I.Sh. (DOC-B11 — hozir kodda "X.M. Maxmudov"). */
