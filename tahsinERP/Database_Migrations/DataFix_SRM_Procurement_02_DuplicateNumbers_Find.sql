/*
    SRM-B14 — takrorlangan hujjat raqamlarini TOPISH (faqat SELECT).

    Eski raqam generatorlari (MAX + 1, qulfsiz, o'chirilganlarni hisobga olmasdan) bir xil
    raqamni ikki marta berishi mumkin edi. Kod endi DocumentNumberService (sp_getapplock)
    ishlatadi va qo'lda kiritilgan raqam takrorlanishini tekshiradi. Bu skript eski
    takrorlarni ko'rsatadi — ular unique indeks (Proposal_SRM_Procurement_Indexes.sql)
    yaratishdan OLDIN qo'lda hal qilinishi kerak (masalan, ikkinchisiga "-2" qo'shish).

    Qoidalar (kod bilan bir xil):
      - P_ORDERS.OrderNo         — faol buyurtmalar orasida global unikal
      - P_REQUISITIONS.RequisitionNo — faol talabnomalar orasida global unikal
      - P_CONTRACTS.ContractNo   — bitta ta'minotchining faol shartnomalari orasida unikal
      - P_INVOICES.InvoiceNo     — bitta ta'minotchining faol invoyslari orasida unikal
*/
SET NOCOUNT ON;

SELECT 'P_ORDERS' AS TableName, OrderNo AS DocNo, NULL AS SupplierID, COUNT(*) AS Cnt,
       STRING_AGG(CAST(ID AS varchar(20)), ',') AS IDs
FROM dbo.P_ORDERS
WHERE ISNULL(IsDeleted, 0) = 0 AND OrderNo IS NOT NULL
GROUP BY OrderNo
HAVING COUNT(*) > 1

UNION ALL
SELECT 'P_REQUISITIONS', RequisitionNo, NULL, COUNT(*), STRING_AGG(CAST(ID AS varchar(20)), ',')
FROM dbo.P_REQUISITIONS
WHERE ISNULL(IsDeleted, 0) = 0 AND RequisitionNo IS NOT NULL
GROUP BY RequisitionNo
HAVING COUNT(*) > 1

UNION ALL
SELECT 'P_CONTRACTS', ContractNo, SupplierID, COUNT(*), STRING_AGG(CAST(ID AS varchar(20)), ',')
FROM dbo.P_CONTRACTS
WHERE ISNULL(IsDeleted, 0) = 0 AND ContractNo IS NOT NULL
GROUP BY SupplierID, ContractNo
HAVING COUNT(*) > 1

UNION ALL
SELECT 'P_INVOICES', InvoiceNo, SupplierID, COUNT(*), STRING_AGG(CAST(ID AS varchar(20)), ',')
FROM dbo.P_INVOICES
WHERE ISNULL(IsDeleted, 0) = 0 AND InvoiceNo IS NOT NULL
GROUP BY SupplierID, InvoiceNo
HAVING COUNT(*) > 1
ORDER BY TableName, DocNo;

-- Eslatma: STRING_AGG SQL Server 2017+ talab qiladi; eski versiyada IDs ustunini olib tashlang.
