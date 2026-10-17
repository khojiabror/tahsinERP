-- =====================================================================
-- CRM-B04: Customer Excel importi mijozlarni SUPPLIERS jadvaliga yozardi.
-- Kod olib tashlandi. UploadWithExcel view'i yo'q edi, shuning uchun import faqat
-- /Customer/Save ga to'g'ridan-to'g'ri POST bilan ishlagan bo'lishi mumkin — yozuvlar
-- bo'lmasligi ehtimoli katta. Quyidagi SELECT'lar shuni tekshiradi.
--
-- Faqat SELECT. Topilsa: yozuvni mijozlarga ko'chirish yoki o'chirish biznes qarori —
-- yetkazib beruvchi sifatida hujjatlarda ishlatilganini (2-so'rov) albatta tekshiring.
-- =====================================================================
SET NOCOUNT ON;

-- 1) Audit jurnalida "Mijozni Excell orqali yaratdi" yozuvlari (LogHelper ActionName'ga matn yozardi)
SELECT ul.ID, ul.UserID, ul.[DateTime], ul.ActionName
FROM dbo.USERLOGS ul
WHERE ul.ControllerName = 'CustomerController'
  AND ul.ActionName LIKE N'%Excell orqali yaratdi%'
ORDER BY ul.[DateTime];

-- 2) Shu nomli yetkazib beruvchilar va ularning hujjatlarda ishlatilishi
SELECT s.ID, s.Name, s.Type, s.DUNS, s.IsDeleted,
       (SELECT COUNT(*) FROM dbo.P_CONTRACTS pc WHERE pc.SupplierID = s.ID) AS PContracts,
       (SELECT COUNT(*) FROM dbo.P_INVOICES  pi WHERE pi.SupplierID = s.ID) AS PInvoices,
       CASE WHEN EXISTS (SELECT 1 FROM dbo.CUSTOMERS c WHERE c.Name = s.Name AND c.IsDeleted = 0) THEN 1 ELSE 0 END AS AlsoCustomer
FROM dbo.SUPPLIERS s
WHERE EXISTS (
    SELECT 1 FROM dbo.USERLOGS ul
    WHERE ul.ControllerName = 'CustomerController'
      AND ul.ActionName LIKE N'%Excell orqali yaratdi%'
      AND ul.ActionName LIKE N'%' + s.Name + N' Mijozni Excell%');
