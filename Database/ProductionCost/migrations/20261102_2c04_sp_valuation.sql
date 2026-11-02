-- =============================================================================
-- G-K 2C-04 — Skeleton SP VALUATION (BÀN GIAO — user tự SOURCE)
-- Không START/COMMIT nội bộ. Không cập nhật kho thật trong 2C-04.
-- CẤM production.
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_pc_valuation_preview`$$
CREATE PROCEDURE `sp_pc_valuation_preview`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT
)
BEGIN
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'sp_pc_valuation_preview skeleton — dung ValuationAverageCost/ReceiptPricePrep C#';
END$$

DROP PROCEDURE IF EXISTS `sp_pc_valuation_confirm`$$
CREATE PROCEDURE `sp_pc_valuation_confirm`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    -- Khi mở (sau nghiệm thu): atomic
    --   apply giá INPUT_ID (COMPANY_CD) + tính xuất + UPSERT VALUATION_COGS STABLE_KEY
    -- VALUATION=DONE; KHONG set RUN.STATUS=CONFIRMED toan ky
    -- KHONG insert trung N632/C155; KHONG cong EXP_TO_154 vao 632
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'sp_pc_valuation_confirm skeleton — WriteGate OFF; khong cap nhat kho trong 2C-04';
END$$

DELIMITER ;

SELECT '2C-04 valuation SP skeleton' AS NOTE;
