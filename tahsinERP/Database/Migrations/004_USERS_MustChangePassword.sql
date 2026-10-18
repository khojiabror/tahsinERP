/* =====================================================================
   004 — USERS.MustChangePassword: vaqtinchalik parol bilan yaratilgan foydalanuvchi
   (masalan New-TenantDb.ps1 dagi admin) birinchi kirishda parolni almashtirishi shart.
   Login muvaffaqiyatli bo'lsa va bayroq = 1 — Account/Settings sahifasiga yo'naltiriladi
   (Filters/MustChangePasswordAttribute); parol almashtirilgach bayroq 0 ga tushadi.
   ===================================================================== */
IF COL_LENGTH('dbo.USERS', 'MustChangePassword') IS NULL
    ALTER TABLE dbo.USERS ADD MustChangePassword bit NOT NULL
        CONSTRAINT DF_USERS_MustChangePassword DEFAULT (0);
GO
