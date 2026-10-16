/* ============================================================================
   LITSENZIYA TIZIMI — jadval o'zgarishlari
   Ishga tushirilgandan keyin Visual Studio'da:
     DBTHSNModel.edmx -> o'ng tugma -> "Update Model from Database..."
     -> COMPANY_LICENSES va USER_LICENSES ni tanlab yangilang.
   ============================================================================ */

/* ---------- COMPANY_LICENSES: yangi ustunlar ---------- */
IF COL_LENGTH('dbo.COMPANY_LICENSES', 'MaxUsers') IS NULL
BEGIN
    ALTER TABLE dbo.COMPANY_LICENSES ADD
        MaxUsers   INT           NOT NULL CONSTRAINT DF_CL_MaxUsers  DEFAULT(0),
        StartDate  DATETIME      NULL,
        ExpiryDate DATETIME      NULL,
        Edition    NVARCHAR(50)  NULL,
        IsActive   BIT           NOT NULL CONSTRAINT DF_CL_IsActive  DEFAULT(1),
        CreatedAt  DATETIME      NOT NULL CONSTRAINT DF_CL_CreatedAt DEFAULT(GETDATE()),
        CreatedBy  NVARCHAR(100) NULL;
END
GO

/* ---------- USER_LICENSES: yangi ustunlar + bog'lanishlar ---------- */
IF COL_LENGTH('dbo.USER_LICENSES', 'UserID') IS NULL
BEGIN
    ALTER TABLE dbo.USER_LICENSES ADD
        CompanyLicenseID INT           NULL,
        UserID           INT           NULL,
        StartDate        DATETIME      NULL,
        ExpiryDate       DATETIME      NULL,
        IsActive         BIT           NOT NULL CONSTRAINT DF_UL_IsActive     DEFAULT(1),
        AssignedBy       NVARCHAR(100) NULL,
        AssignedDate     DATETIME      NOT NULL CONSTRAINT DF_UL_AssignedDate DEFAULT(GETDATE());
END
GO

/* ---------- Tashqi kalitlar (FK) ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UL_COMPANY_LICENSES')
BEGIN
    ALTER TABLE dbo.USER_LICENSES WITH CHECK
        ADD CONSTRAINT FK_UL_COMPANY_LICENSES
        FOREIGN KEY (CompanyLicenseID) REFERENCES dbo.COMPANY_LICENSES(ID);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UL_USERS')
BEGIN
    ALTER TABLE dbo.USER_LICENSES WITH CHECK
        ADD CONSTRAINT FK_UL_USERS
        FOREIGN KEY (UserID) REFERENCES dbo.USERS(ID);
END
GO

/* ---------- Bir xodimga bir litsenziyada bitta seat ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_UL_License_User')
BEGIN
    CREATE UNIQUE INDEX UX_UL_License_User
        ON dbo.USER_LICENSES(CompanyLicenseID, UserID)
        WHERE CompanyLicenseID IS NOT NULL AND UserID IS NOT NULL;
END
GO

/* ---------- LICENSE_STATE: o'rnatma ID va HMAC'li "oxirgi ko'rilgan vaqt" ----------
   EDMX'ga QO'SHILMAYDI — LicenseService xom SQL bilan ishlaydi va jadval
   bo'lmasa o'zi yaratadi. Bu blok DB foydalanuvchisida CREATE TABLE huquqi
   bo'lmagan o'rnatmalar uchun.
     InstallationId — birinchi ishga tushishda yaratiladigan GUID (v2 kalit shunga bog'lanadi)
     LastSeenUtc    — "ticks:hmac"; qo'lda o'zgartirilsa yoki o'chirilsa tizim bloklanadi
   ------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.LICENSE_STATE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LICENSE_STATE (
        Name      NVARCHAR(50)  NOT NULL CONSTRAINT PK_LICENSE_STATE PRIMARY KEY,
        Value     NVARCHAR(400) NOT NULL,
        UpdatedAt DATETIME      NOT NULL CONSTRAINT DF_LS_UpdatedAt DEFAULT(GETDATE())
    );
END
GO
