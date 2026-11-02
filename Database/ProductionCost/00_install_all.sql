-- =============================================================================
-- Install G-K production_cost_* (chỉ chạy SAU KHI được duyệt Giai đoạn 2A)
-- MySQL 5.6 / 8.0
-- Thứ tự: tables → (sau này) SP implement
-- =============================================================================

SOURCE 01_production_cost_tables.sql;

-- 02_sp_specifications.sql chỉ là đặc tả — KHÔNG SOURCE khi cài DB.
-- Khi Giai đoạn 2B triển khai SP, thêm:
--   SOURCE 10_sp_pc_period.sql;
--   ...

SELECT 'production_cost tables install script ready — await approval before execute' AS MSG;
