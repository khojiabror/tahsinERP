/* =====================================================================
   033 — Omborga qo'lda (yetkazib beruvchidan) kirim mumkinligi (WAREHOUSES.AllowManualIncome).
   NULL = standart, ombor turi bo'yicha: xomashyo (SUPPLY) — ha; savdo (SALES) — paketda ishlab chiqarish
   (MES) bo'lmasa ha; ishlab chiqarish (PRODUCTION) va turi berilmagan — yo'q.
   1/0 — Sozlamalar → Omborlar formasida aniq tanlangan qiymat. Mavjud qatorlar NULL qoladi.
   Idempotent.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.WAREHOUSES', 'AllowManualIncome') IS NULL
BEGIN
    ALTER TABLE dbo.WAREHOUSES ADD AllowManualIncome BIT NULL;
END
GO
