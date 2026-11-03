-- Kiểm tra chỉ đọc sau khi cài G-K final. Không UPDATE/DELETE dữ liệu nghiệp vụ.

SELECT TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN (
    'production_cost_period', 'production_cost_run', 'production_cost_step',
    'production_cost_object', 'production_cost_alloc_pool', 'production_cost_alloc_detail',
    'production_cost_nvl_basis', 'production_cost_source', 'production_cost_wip',
    'production_cost_receipt', 'production_cost_voucher_ref', 'production_cost_history',
    'production_cost_price_audit'
  )
ORDER BY TABLE_NAME;

SELECT ROUTINE_NAME
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME IN (
    'sp_pc_alloc_load_pool', 'sp_pc_alloc_load_basis_qty', 'sp_pc_nvl_basis_build',
    'sp_pc_alloc_confirm', 'sp_pc_valuation_apply_input_price', 'sp_pc_valuation_confirm'
  );

SELECT INDEX_NAME, NON_UNIQUE
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_history'
  AND INDEX_NAME IN ('uk_pc_hist_stable', 'uk_pc_hist_operation');

-- Kỳ vọng: production_cost_price_audit có mặt.
-- Kỳ vọng: sp_pc_valuation_apply_input_price và sp_pc_valuation_confirm KHÔNG còn
-- (writer là C# theo INPUT_ID). Hai SP phân bổ/NVL/confirm B2 vẫn còn.
