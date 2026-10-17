/* =====================================================================
   Tasks + Documents indekslari (audit 2026-10). Idempotent.
   2026-10-17: lokal DBTHSN-IPACK bazasida ishga tushirildi.
   Mavjud indekslar tekshirildi va ular yetarli deb topildi:
     - IX_DOC_FILES_Document (DocumentID, VersionNo) — fayl ro'yxatlari va set-based UPDATE'lar uchun;
     - UX_COM_OFFICIAL_LETTERS_No_Date_Dir, UX_ADM_COMPANY_ORDERS_No_Date — raqam unikalligi
       (o'chirilganlarni ham o'z ichiga oladi — DocNumbering shunga moslangan);
     - IX_APP_TASKS_Assignee_Status, IX_APP_TASKS_Project_Status_Due.
   Faqat raqamlash qidiruvlari (prefiks LIKE + MAX) uchun quyidagilar qo'shildi.
   ===================================================================== */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.APP_TASKS') AND name = 'IX_APP_TASKS_KeyCode')
    CREATE INDEX IX_APP_TASKS_KeyCode ON dbo.APP_TASKS (KeyCode);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.DOC_DOCUMENTS') AND name = 'IX_DOC_DOCUMENTS_Code')
    CREATE INDEX IX_DOC_DOCUMENTS_Code ON dbo.DOC_DOCUMENTS (Code) INCLUDE (IsDeleted);
GO

/* Sxema takliflari (EDMX yangilashni talab qiladi — keyingi bosqich):
   - DOC_FILES.FileContent'ni alohida jadvalga ajratish yoki EF table splitting (DOC-P02); uzoq muddatda S3/disk.
   - APP_TASK_STATUS_REF / APP_TASK_PRIORITY_REF: IsFinal BIT, ColorHex NVARCHAR(7) (MNT-01, TSK-B09).
   - COM_OFFICIAL_LETTERS: BodyHtml NVARCHAR(MAX), GreetingLine; mahsulot satrlari uchun bola jadval (DOC-P10). */
