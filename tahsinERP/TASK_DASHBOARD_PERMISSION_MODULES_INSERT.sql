-- =============================================
-- Task Dashboard Controller Permission Modules
-- Module = Controller + Action
-- =============================================

-- TaskDashboard Controller
-- =============================================
INSERT INTO [dbo].[PERMISSIONMODULES] ([Module], [Controller], [Action], [Parameter])
VALUES ('TaskDashboardIndex', 'TaskDashboard', 'Index', NULL);

-- =============================================
-- Izohlar: 
-- - Module ustuni Controller + Action ni o'z ichiga oladi
-- - Controller ustuni 'TaskDashboard' ni o'z ichiga oladi
-- - Action ustuni 'Index' ni o'z ichiga oladi
-- - Parameter NULL
-- =============================================
