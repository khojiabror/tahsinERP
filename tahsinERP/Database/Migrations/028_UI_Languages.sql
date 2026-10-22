/* =====================================================================
   028 — Ko'p tillilik: butun ERP interfeysi uchun til tanlovi.
     dst.USER_SETTINGS.Language   foydalanuvchi tili: 'uz' (lotin), 'oz' (o'zbek kirill), 'ru'
                                  (avval faqat distributsiya uchun edi — endi butun ERP va bot shu ustundan o'qiydi)
     dst.SETTINGS 'DefaultLanguage'  firma standart tili (qator bo'lmasa — 'uz'; Admin panelidan o'zgartiriladi)
   ===================================================================== */

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DST_USER_SETTINGS_Language'
           AND parent_object_id = OBJECT_ID('dst.USER_SETTINGS'))
    ALTER TABLE dst.USER_SETTINGS DROP CONSTRAINT CK_DST_USER_SETTINGS_Language;
GO

IF OBJECT_ID('dst.USER_SETTINGS', 'U') IS NOT NULL
    ALTER TABLE dst.USER_SETTINGS WITH CHECK
        ADD CONSTRAINT CK_DST_USER_SETTINGS_Language CHECK (Language IN ('uz', 'oz', 'ru'));
GO
