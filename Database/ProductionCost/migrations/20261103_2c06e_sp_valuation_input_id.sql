-- =============================================================================
-- SUPERSEDED bởi 20261103_gk_final_price_audit.sql.
-- Không SOURCE file này sau script final: nó tạo lại procedure SIGNAL.
-- Ghi giá INPUT_ID nằm ở ProductionCostWriteRepository.ValuationApply (C#).
-- =============================================================================
-- G-K 2C-06E — Skeleton SQL cập nhật giá nhập theo INPUT_ID + COMPANY_CD
-- BÀN GIAO — user tự SOURCE sau nghiệm thu. CẤM production trong 2C-06E.
-- Không START/COMMIT nội bộ. Caller C# sở hữu DapperSession / TX.
-- KHÔNG dùng apply_inventory_update_in_price (PRODUCT_CD bulk).
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_pc_valuation_apply_input_price`$$
CREATE PROCEDURE `sp_pc_valuation_apply_input_price`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_INPUT_ID BIGINT,
    IN p_NEW_UNIT_PRICE DECIMAL(18,6),
    IN p_NEW_AMOUNT DECIMAL(18,2),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    -- Khi mở (sau nghiệm thu + SupportsInputIdSafeInventoryUpdate=true):
    --   UPDATE chit_inventory_input
    --   SET UNIT_PRICE = p_NEW_UNIT_PRICE,
    --       AMOUNT = p_NEW_AMOUNT,
    --       UPDATE_BY = p_USER_ID,
    --       UPDATE_AT = UTC_TIMESTAMP()
    --   WHERE COMPANY_CD = p_COMPANY_CD
    --     AND INPUT_ID = p_INPUT_ID
    --     AND ISDEL = '0';
    --   -- bắt buộc 1 hàng; không cập nhật theo PRODUCT_CD
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'sp_pc_valuation_apply_input_price skeleton — INPUT_ID+COMPANY_CD only; WriteGate OFF';
END$$

DROP PROCEDURE IF EXISTS `sp_pc_valuation_confirm`$$
CREATE PROCEDURE `sp_pc_valuation_confirm`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    -- Confirm B5 orchestration thuộc C# (ProductionCostWriteService.ConfirmValuationAsync).
    -- SP này giữ SIGNAL — không START/COMMIT; không post 632 tại đây trong 2C-06E.
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'sp_pc_valuation_confirm skeleton — dung ConfirmValuationAsync C#; WriteGate OFF';
END$$

DELIMITER ;

SELECT '2C-06E valuation INPUT_ID SQL skeleton' AS NOTE;
