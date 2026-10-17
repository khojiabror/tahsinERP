/*
  Tannarx fakt xarajatlari — TUZATUVCHI skript (avtomatik ishga tushirilmaydi; avval
  Tannarx_2026_10_ActualCosts_Check.sql natijasini ko'rib chiqing va bazaning zaxira nusxasini oling).

  1-qism: shu oyda ishlab chiqarilmagan mahsulotlar qatorlaridagi eski fakt qiymatlarni tozalaydi
          (yangi engine ham aynan shunday qiladi — NULL = "fakt hisoblanmagan").
  Qolgan qatorlar (ActualProfit / EXW farqi) uchun SQL emas, ilovadagi "Fakt xarajatlarni yangilash" ni
  har oy uchun qayta bosing — hisob faqat C# engine'da.

  2-qism (ixtiyoriy): eski SP va V_TANNARX_ACTUAL_* view'lari endi kod tomonidan chaqirilmaydi.
          O'chirishdan oldin boshqa joyda (hisobot, job) ishlatilmasligini tekshiring.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

UPDATE t
SET ActualMaterialPrice = NULL,
    ActualMaterialTransportCost = NULL,
    ActualLaborCost = NULL,
    ActualTaxAndBank = NULL,
    ActualAdministrativeExpenses = NULL,
    ActualOtherExpenses = NULL,
    ActualProfit = NULL,
    ActualFinishedProductTransportCost = NULL,
    ActualSalesPriceEXWWithoutVAT = NULL,
    ActualSalesPriceEXWWithVAT = NULL,
    ActualSalesPriceCIPWithoutVAT = NULL,
    ActualSalesPriceCIPWithVAT = NULL,
    ModifiedDT = GETDATE()
FROM dbo.TANNARX t
WHERE t.IsDeleted = 0
  AND NOT EXISTS (
        SELECT 1 FROM dbo.PRODUCT_PRODUCTION pp
        WHERE pp.ProductID = t.ProductID
          AND ISNULL(pp.IsDeleted, 0) = 0
          AND pp.[Date] >= DATEFROMPARTS(t.Year, t.Month, 1)
          AND pp.[Date] <  DATEADD(MONTH, 1, DATEFROMPARTS(t.Year, t.Month, 1))
          AND pp.Total > 0);

SELECT @@ROWCOUNT AS ClearedRows;

-- Natijani tekshiring; to'g'ri bo'lsa COMMIT, aks holda ROLLBACK.
-- COMMIT;
ROLLBACK;

/* 2-qism (ixtiyoriy, alohida ishga tushiring):
IF OBJECT_ID(N'dbo.SP_TANNARX_UPDATE_ACTUAL_COSTS', N'P') IS NOT NULL DROP PROCEDURE dbo.SP_TANNARX_UPDATE_ACTUAL_COSTS;
IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_MATERIAL_COSTS', N'V') IS NOT NULL DROP VIEW dbo.V_TANNARX_ACTUAL_MATERIAL_COSTS;
IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_MATERIAL_TRANSPORT', N'V') IS NOT NULL DROP VIEW dbo.V_TANNARX_ACTUAL_MATERIAL_TRANSPORT;
IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_LABOR_COSTS', N'V') IS NOT NULL DROP VIEW dbo.V_TANNARX_ACTUAL_LABOR_COSTS;
IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT', N'V') IS NOT NULL DROP VIEW dbo.V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT;
IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_ADMIN_EXPENSES', N'V') IS NOT NULL DROP VIEW dbo.V_TANNARX_ACTUAL_ADMIN_EXPENSES;
*/
