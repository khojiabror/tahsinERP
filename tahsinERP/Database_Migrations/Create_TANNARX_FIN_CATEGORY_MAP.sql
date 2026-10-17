/*
  Tannarx: Finance xarajat kategoriyasi → tannarx guruhi (qo'lda belgilash).
  Ilova bu jadvalni "Tannarx → Oy xarajatlari manbalari → Kategoriya guruhlarini sozlash" sahifasida
  birinchi saqlashda o'zi yaratadi; DB foydalanuvchisida CREATE TABLE huquqi bo'lmasa, shu skriptni qo'lda ishga tushiring.
  EDMX ga qo'shish shart emas (xom SQL bilan o'qiladi).

  Bucket: 0 = tannarxga kirmaydi, 1 = Material (PATH3), 2 = B — Soliq va bank, 3 = D — Boshqa.
  Jadvalda yo'q kategoriya — "Avtomatik" (nom qoidasi).
*/
IF OBJECT_ID(N'dbo.TANNARX_FIN_CATEGORY_MAP', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TANNARX_FIN_CATEGORY_MAP (
        CategoryID      INT      NOT NULL CONSTRAINT PK_TANNARX_FIN_CATEGORY_MAP PRIMARY KEY,
        Bucket          TINYINT  NOT NULL,
        UpdatedAt       DATETIME NOT NULL CONSTRAINT DF_TANNARX_FIN_CATEGORY_MAP_UpdatedAt DEFAULT (GETDATE()),
        UpdatedByUserID INT      NULL,
        CONSTRAINT FK_TANNARX_FIN_CATEGORY_MAP_CATEGORY FOREIGN KEY (CategoryID) REFERENCES dbo.FIN_CATEGORIES (ID),
        CONSTRAINT CK_TANNARX_FIN_CATEGORY_MAP_Bucket CHECK (Bucket IN (0, 1, 2, 3))
    );
END
GO
