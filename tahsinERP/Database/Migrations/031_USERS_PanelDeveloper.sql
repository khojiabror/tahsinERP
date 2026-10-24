/* =====================================================================
   031 — USERS.IsPanelDeveloper: boshqaruv panelidagi ERP dasturchisining (super-foydalanuvchi) firma
   bazasidagi nusxasi. Bunday foydalanuvchi paroli shu bazada saqlanmaydi — har kirishda panel tekshiradi
   (License API dev-login, Services/LicenseSync/PanelDeveloperLogin.cs); roli DEVELOPER.
   Firmaning o'z foydalanuvchisi (bayroq = 0) hech qachon panel orqali kirmaydi.
   ===================================================================== */
IF COL_LENGTH('dbo.USERS', 'IsPanelDeveloper') IS NULL
    ALTER TABLE dbo.USERS ADD IsPanelDeveloper bit NOT NULL
        CONSTRAINT DF_USERS_IsPanelDeveloper DEFAULT (0);
GO
