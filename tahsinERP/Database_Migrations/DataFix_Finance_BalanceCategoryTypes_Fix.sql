-- =====================================================================
-- Finance — TUZATISH: ..._Check.sql ro'yxatidagi kategoriyalar turini to'g'rilaydi.
-- Nomlar bazangizda boshqacha bo'lsa — avval moslashtiring.
-- Natija: balansda ombor/asosiy vositalar aktiv ustuniga o'tadi, DCF sof aktivi oshadi.
-- =====================================================================
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE dbo.FIN_BALANCE_CATEGORIES SET Type = 1
WHERE IsActive = 1 AND Type <> 1
  AND Name IN (N'Naqd pul', N'Bank hisobi', N'Xaridorlardan haqlarimiz', N'Xomashyo', N'Tayyor maxsulot', N'Jihozlar', N'Bino');
PRINT CONCAT(@@ROWCOUNT, ' ta kategoriya Type=1 (aktiv) qilindi');

UPDATE dbo.FIN_BALANCE_CATEGORIES SET Type = 2
WHERE IsActive = 1 AND Type <> 2
  AND Name IN (N'Ta''minotchilardan qarzlar', N'Oylikdan qarzimiz', N'Logistikadan qarzimiz', N'Doimiy harajatlar qarzi');
PRINT CONCAT(@@ROWCOUNT, ' ta kategoriya Type=2 (majburiyat) qilindi');

COMMIT;
