-- =====================================================================
-- Finance — TEKSHIRUV (faqat SELECT): balans kategoriyalarining turi.
-- Aktivlar (naqd, bank, debitor, ombor, asosiy vositalar) Type=1, majburiyatlar Type=2,
-- kapital Type=3 bo'lishi kerak. Lokal bazada Xomashyo, Tayyor maxsulot, Jihozlar, Bino
-- Type=3 (Kapital) — shu sabab balansda ular PASSIV ustunida chiqadi va DCF'da
-- aktivga kirmaydi (Type=3 ni BalanceCategoryDisplayHelper aktivga o'tkazmaydi).
-- =====================================================================
SELECT ID, Name, Type AS HozirgiType,
       CASE WHEN Name IN (N'Naqd pul', N'Bank hisobi', N'Xaridorlardan haqlarimiz', N'Xomashyo',
                          N'Tayyor maxsulot', N'Jihozlar', N'Bino') THEN 1
            WHEN Name IN (N'Ta''minotchilardan qarzlar', N'Oylikdan qarzimiz', N'Logistikadan qarzimiz',
                          N'Doimiy harajatlar qarzi') THEN 2 END AS KerakliType
FROM dbo.FIN_BALANCE_CATEGORIES
WHERE IsActive = 1
  AND ((Name IN (N'Naqd pul', N'Bank hisobi', N'Xaridorlardan haqlarimiz', N'Xomashyo', N'Tayyor maxsulot', N'Jihozlar', N'Bino') AND Type <> 1)
    OR (Name IN (N'Ta''minotchilardan qarzlar', N'Oylikdan qarzimiz', N'Logistikadan qarzimiz', N'Doimiy harajatlar qarzi') AND Type <> 2))
ORDER BY SortOrder;
