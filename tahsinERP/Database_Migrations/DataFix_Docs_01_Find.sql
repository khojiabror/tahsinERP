/* =====================================================================
   Audit 2026-10 — Tasks + Documents: eski noto'g'ri ma'lumotni TOPISH (faqat SELECT).
   Hech narsa o'zgartirmaydi. Natijani ko'rib chiqib, kerak bo'lsa
   DataFix_Docs_02_Fix.sql ni (alohida, zaxira nusxadan keyin) ishga tushiring.
   ===================================================================== */
SET NOCOUNT ON;

/* 1. STD: hujjat o'chirilgan, lekin STD_COMPANY_STANDARDS.IsDeleted qo'yilmagan (DOC-B01).
      Bunday standartlar Index'da ko'rinib, tahrirlanardi. */
SELECT s.DocumentID, s.StandardCode, d.Code, d.UpdatedDT
FROM dbo.STD_COMPANY_STANDARDS s
JOIN dbo.DOC_DOCUMENTS d ON d.ID = s.DocumentID
WHERE d.IsDeleted = 1 AND (s.IsDeleted IS NULL OR s.IsDeleted = 0);

/* 2. O'chirilgan hujjatlarning o'chirilmagan fayllari (soft delete faqat sarlavhani belgilagan holatlar). */
SELECT f.DocumentID, COUNT(*) AS FileCount, SUM(ISNULL(f.FileSizeBytes, 0)) AS Bytes
FROM dbo.DOC_FILES f
JOIN dbo.DOC_DOCUMENTS d ON d.ID = f.DocumentID
WHERE d.IsDeleted = 1 AND (f.IsDeleted IS NULL OR f.IsDeleted = 0)
GROUP BY f.DocumentID;

/* 3. Yetim hujjatlar (DOC-B04/B05): xat/buyruq turidagi DOC_DOCUMENTS, lekin kichik tur qatori yo'q.
      Sabab: avval sarlavha alohida SaveChanges bilan yozilib, keyingi qadam (fayl validatsiyasi,
      xato) muvaffaqiyatsiz bo'lsa, sarlavha qolardi. */
SELECT d.ID, t.Code AS DocType, d.Code, d.Title, d.CreatedDT, d.CreatedByUserID,
       (SELECT COUNT(*) FROM dbo.DOC_FILES f WHERE f.DocumentID = d.ID) AS FileCount
FROM dbo.DOC_DOCUMENTS d
JOIN dbo.DOC_DOCUMENT_TYPES t ON t.ID = d.DocTypeID
WHERE (d.IsDeleted IS NULL OR d.IsDeleted = 0)
  AND (   (t.Code = 'OFFICIAL_LETTER' AND NOT EXISTS (SELECT 1 FROM dbo.COM_OFFICIAL_LETTERS l WHERE l.DocumentID = d.ID))
       OR (t.Code = 'COMP_ORDER'      AND NOT EXISTS (SELECT 1 FROM dbo.ADM_COMPANY_ORDERS o WHERE o.DocumentID = d.ID))
       OR (t.Code = 'COMPANY_STANDARD' AND NOT EXISTS (SELECT 1 FROM dbo.STD_COMPANY_STANDARDS s WHERE s.DocumentID = d.ID)));

/* 4. Bir hujjatda bir nechta IsMain=1 fayl (avval IsMain parallel/qulfsiz almashtirilardi). */
SELECT DocumentID, FileExtension, COUNT(*) AS MainCount
FROM dbo.DOC_FILES
WHERE IsMain = 1 AND (IsDeleted IS NULL OR IsDeleted = 0)
GROUP BY DocumentID, FileExtension
HAVING COUNT(*) > 1;

/* 5. Takroriy raqamlar (DOC-B06/B07, TSK-B05) — FAQAT ma'lumot uchun. Raqamni o'zgartirish
      biznes qarori (tashqariga yuborilgan xat raqami), shuning uchun fix skriptida yo'q. */
SELECT 'OUT letter' AS Kind, LetterNo AS Number, COUNT(*) AS Cnt
FROM dbo.COM_OFFICIAL_LETTERS
WHERE DirectionCode = 'OUT' AND (IsDeleted IS NULL OR IsDeleted = 0)
GROUP BY LetterNo HAVING COUNT(*) > 1
UNION ALL
SELECT 'Company order', OrderNo, COUNT(*)
FROM dbo.ADM_COMPANY_ORDERS
WHERE (IsDeleted IS NULL OR IsDeleted = 0)
GROUP BY OrderNo HAVING COUNT(*) > 1
UNION ALL
SELECT 'Task KeyCode', KeyCode, COUNT(*)
FROM dbo.APP_TASKS
WHERE IsDeleted = 0 AND KeyCode IS NOT NULL
GROUP BY KeyCode HAVING COUNT(*) > 1
UNION ALL
SELECT 'Document code', Code, COUNT(*)
FROM dbo.DOC_DOCUMENTS
WHERE Code LIKE 'STD-%' AND (IsDeleted IS NULL OR IsDeleted = 0)
GROUP BY Code HAVING COUNT(*) > 1;

/* 6. Topshiriq ref'lari: o'chirilgan status/prioritet/loyihaga bog'langan faol topshiriqlar (TSK-B04).
      Endi dropdownda ko'rinmaydi — Edit'da birinchi qiymat tanlanib qolishi mumkin. */
SELECT t.ID, t.KeyCode, 'Status' AS RefKind, s.Code
FROM dbo.APP_TASKS t JOIN dbo.APP_TASK_STATUS_REF s ON s.ID = t.StatusID
WHERE t.IsDeleted = 0 AND s.IsDeleted = 1
UNION ALL
SELECT t.ID, t.KeyCode, 'Priority', p.Code
FROM dbo.APP_TASKS t JOIN dbo.APP_TASK_PRIORITY_REF p ON p.ID = t.PriorityID
WHERE t.IsDeleted = 0 AND p.IsDeleted = 1
UNION ALL
SELECT t.ID, t.KeyCode, 'Project', pr.Code
FROM dbo.APP_TASKS t JOIN dbo.APP_TASK_PROJECTS pr ON pr.ID = t.ProjectID
WHERE t.IsDeleted = 0 AND pr.IsDeleted = 1;

/* Eslatma (TSK-B01): ChangeStatus orqali yozilgan izohlar EF tomonidan eski qiymat bilan
   ustidan yozilgan — bazada qolmagan, tiklab bo'lmaydi. USERLOGS'da faqat status o'zgarishi bor. */
