-- =============================================
-- Balans kategoriyalari: operatsion aktivlar Type=1 ga (bazada noto'g'ri Type=2 bo'lgan bo'lsa).
-- Ilovada BalanceCategoryDisplayHelper ham nom bo'yicha tuzatadi; bu skript ma'lumotni to'g'rilaydi.
-- =============================================

-- MISOL (o'z bazangizda nomlar boshqa bo'lsa, moslashtiring):
/*
UPDATE FIN_BALANCE_CATEGORIES SET Type = 1 WHERE Name IN (
    N'Naqd pul',
    N'Bank hisobi',
    N'Xaridorlardan haqlarimiz',
    N'Xomashyo',
    N'Tayyor maxsulot',
    N'Jihozlar',
    N'Bino'
)
AND IsActive = 1;

UPDATE FIN_BALANCE_CATEGORIES SET Type = 2 WHERE Name IN (
    N'Ta''minotchilardan qarzlar',
    N'Taminotchilardan qarzlar',
    N'Oylikdan qarzimiz',
    N'Oyliklardan qarzimiz',
    N'Logistikadan qarzimiz',
    N'Doimiy harajatlar qarzi'
)
AND IsActive = 1;
*/

PRINT 'Skriptni ishga tushirishdan oldin yuqoridagi UPDATE larni nomlarga qarab tekshiring.';
