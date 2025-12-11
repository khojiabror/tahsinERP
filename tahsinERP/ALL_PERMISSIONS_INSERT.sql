-- ============================================================================
-- PERMISSIONS INSERT QUERY
-- Barcha rollar va barcha permission modullar uchun PERMISSIONS jadvaliga insert
-- ViewPermit = 0 (false)
-- ChangePermit = 0 (false)
-- DeletePermit = 0 (false)
-- ============================================================================

-- Barcha rollar va barcha permission modullar uchun CROSS JOIN qilish
INSERT INTO PERMISSIONS (RoleID, PermissionModuleID, ViewPermit, ChangePermit, DeletePermit)
SELECT 
    r.ID AS RoleID,
    pm.ID AS PermissionModuleID,
    0 AS ViewPermit,      -- Default: false
    0 AS ChangePermit,    -- Default: false
    0 AS DeletePermit     -- Default: false
FROM 
    ROLES r
CROSS JOIN 
    PERMISSIONMODULES pm
WHERE 
    r.IsDeleted = 0
    AND NOT EXISTS (
        -- Takrorlanishni oldini olish: agar bu rol va permission module uchun allaqachon yozuv bo'lsa, qo'shmasin
        SELECT 1 
        FROM PERMISSIONS p 
        WHERE p.RoleID = r.ID 
        AND p.PermissionModuleID = pm.ID
    );

-- ============================================================================
-- Eslatma:
-- Bu query barcha rollar va barcha permission modullar uchun 
-- default ruxsatlar (barcha false) bilan PERMISSIONS yozuvlarini yaratadi.
-- Admin keyin Admin Panel orqali kerakli ruxsatlarni yoqishi mumkin.
-- ============================================================================
