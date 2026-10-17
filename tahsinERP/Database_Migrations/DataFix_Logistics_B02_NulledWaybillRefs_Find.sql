/*
  LOG-B02 — soft-delete paytida bog'lanishi NULL qilingan yo'l varaqalarini TOPISH (faqat o'qiydi).

  Eski SoftDeleteReferenceService yuk tashuvchi / F_CONTRACTS / transport turi / xarid buyurtmasi
  o'chirilganda barcha tarixiy F_WAYBILLS qatorlarida ForwarderID / ContractID / TransportTypeID /
  PurchaseOrderID ni NULL qilardi (core'da tuzatildi — endi NULL qilinmaydi). Bazada qaysi qiymat
  bo'lgani saqlanmagan, shuning uchun bu ma'lumotni ANIQ TIKLAB BO'LMAYDI — tuzatuvchi skript
  yozilmagan. Quyidagi so'rovlar faqat zarar ko'lamini va qo'lda tekshirish uchun ehtimoliy
  nomzodlarni ko'rsatadi (kim qachon o'chirganini USERLOGS'dan solishtiring).

  NULL qiymat har doim ham xato emas: yo'l varaqasi dastlab shartnomasiz/transport turisiz
  kiritilgan bo'lishi mumkin.
*/
SET NOCOUNT ON;

-- 1) Yuk tashuvchisi yo'q yo'l varaqalari + ehtimoliy yuk tashuvchi:
--    (a) shartnomadagi ForwarderID (shartnoma o'chirilmagan bo'lsa ham NULL qilinmagan),
--    (b) shu yo'l varaqasiga bog'langan to'lovlardagi ForwarderID (to'lovlarda NULL qilinmagan).
SELECT w.ID, w.WaybillNo, w.IssueDate, w.Amount, w.ContractID,
       c.ForwarderID AS ForwarderFromContract,
       pf.ForwarderIDs AS ForwarderFromPayments,
       CASE WHEN c.ForwarderID IS NOT NULL OR pf.ForwarderIDs IS NOT NULL THEN 1 ELSE 0 END AS HasCandidate
FROM dbo.F_WAYBILLS w
LEFT JOIN dbo.F_CONTRACTS c ON c.ID = w.ContractID
OUTER APPLY (
    SELECT STUFF((SELECT DISTINCT ',' + CAST(x.ForwarderID AS VARCHAR(20))
                    FROM (SELECT p.ForwarderID FROM dbo.F_WAYBILL_PAYMENTS p WHERE p.WaybillID = w.ID
                          UNION
                          SELECT p.ForwarderID FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
                          JOIN dbo.F_WAYBILL_PAYMENTS p ON p.ID = s.PayerID
                          WHERE s.PayerType = 99 AND s.WaybillID = w.ID) x
                   FOR XML PATH('')), 1, 1, '') AS ForwarderIDs
) pf
WHERE ISNULL(w.IsDeleted, 0) = 0
  AND w.ForwarderID IS NULL
ORDER BY w.IssueDate DESC;

-- 2) Shartnomasi yo'q yo'l varaqalari + shu yuk tashuvchining O'CHIRILGAN shartnomalari
--    (nomzod: IssueDate shartnoma muddati ichida bo'lganlari).
SELECT w.ID, w.WaybillNo, w.IssueDate, w.ForwarderID,
       c.ID AS DeletedContractID, c.ContractNo, c.IssueDate AS ContractFrom, c.DueDate AS ContractTo
FROM dbo.F_WAYBILLS w
LEFT JOIN dbo.F_CONTRACTS c
       ON c.IsDeleted = 1
      AND c.ForwarderID = w.ForwarderID
      AND w.IssueDate BETWEEN c.IssueDate AND c.DueDate
WHERE ISNULL(w.IsDeleted, 0) = 0
  AND w.ContractID IS NULL
ORDER BY w.IssueDate DESC, w.ID;

-- 3) Transport turi yo'q yo'l varaqalari + o'chirilgan transport turlari ro'yxati
--    (tariflarda ham TransportTypeID NULL qilingani uchun avtomatik moslash imkoni yo'q).
SELECT w.ID, w.WaybillNo, w.IssueDate, w.ContractID, w.FreightBasis, w.Rate
FROM dbo.F_WAYBILLS w
WHERE ISNULL(w.IsDeleted, 0) = 0
  AND w.TransportTypeID IS NULL
ORDER BY w.IssueDate DESC;

SELECT t.ID, t.TransportType FROM dbo.F_TRANSPORT_TYPES t WHERE t.IsDeleted = 1;

SELECT tr.ID AS TariffID, tr.ContractID, tr.FreightBasis, tr.Rate
FROM dbo.F_CONTRACT_TARIFFS tr
WHERE tr.TransportTypeID IS NULL;

-- 4) Xarid buyurtmasi bog'lanishi uzilgan bo'lishi mumkin bo'lgan yo'l varaqalari:
--    xarid invoysi bor, lekin PurchaseOrderID yo'q — invoysning buyurtmasi o'chirilgan bo'lsa nomzod.
SELECT w.ID, w.WaybillNo, w.IssueDate, w.InvoiceID, pi.InvoiceNo, pi.OrderID AS InvoiceOrderID, po.OrderNo,
       po.IsDeleted AS OrderIsDeleted
FROM dbo.F_WAYBILLS w
JOIN dbo.P_INVOICES pi ON pi.ID = w.InvoiceID
LEFT JOIN dbo.P_ORDERS po ON po.ID = pi.OrderID
WHERE ISNULL(w.IsDeleted, 0) = 0
  AND w.PurchaseOrderID IS NULL
  AND po.IsDeleted = 1
ORDER BY w.IssueDate DESC;

-- 5) Xulosa
SELECT
    SUM(CASE WHEN ForwarderID IS NULL THEN 1 ELSE 0 END)     AS NoForwarder,
    SUM(CASE WHEN ContractID IS NULL THEN 1 ELSE 0 END)      AS NoContract,
    SUM(CASE WHEN TransportTypeID IS NULL THEN 1 ELSE 0 END) AS NoTransportType,
    SUM(CASE WHEN ForwarderID IS NULL THEN ISNULL(Amount, 0) ELSE 0 END) AS AmountWithoutForwarder
FROM dbo.F_WAYBILLS
WHERE ISNULL(IsDeleted, 0) = 0;
