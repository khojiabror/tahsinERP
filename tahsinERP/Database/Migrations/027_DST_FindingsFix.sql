/* =====================================================================
   027 — Distributsiya: qo'llanma yozilganda topilgan kamchiliklar tuzatishi (Docs/Distribution/05_FINDINGS.md).

     - K1-1: maxsus huquqlar (DstSeeCost, DstOverrideCredit, DstDiscountUnlimited, DstWriteOffApprove) endi
       Change ustuni bilan tekshiriladi (yangi rolga hamma modulda View avtomatik beriladi). Mavjud egalar saqlanadi:
       View bor qatorlarga Change ham qo'yiladi.
   Idempotent.
   ===================================================================== */

-- ---------- K1-1: maxsus huquqlar — View egalari Change oladi ----------
UPDATE p SET p.ChangePermit = 1
FROM dbo.PERMISSIONS p
JOIN dbo.PERMISSIONMODULES pm ON pm.ID = p.PermissionModuleID
WHERE pm.Module IN ('DstSeeCost', 'DstOverrideCredit', 'DstDiscountUnlimited', 'DstWriteOffApprove')
  AND p.ViewPermit = 1 AND p.ChangePermit = 0;
GO
