-- =============================================================================
-- G-K 2C-03 — Script SP tính dở dang / giá thành (BÀN GIAO — user tự SOURCE)
-- Không START/COMMIT nội bộ (TX owner = C#).
-- Không cập nhật giá nhập kho thật trong script này (chỉ skeleton confirm).
-- CẤM production. Agent không tự chạy.
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

DELIMITER $$

-- -----------------------------------------------------------------------------
-- 1) Rebuild nguồn chi phí — loại XFER_EXP_TO_154 (không cộng trùng B3)
--    Skeleton: SIGNAL — body SQL chờ nghiệm thu DEV / bật EnableWrites.
-- -----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS `sp_pc_source_rebuild`$$
CREATE PROCEDURE `sp_pc_source_rebuild`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'sp_pc_source_rebuild skeleton — dung CostSourceRules C#; chua bat EnableWrites';
END$$

-- -----------------------------------------------------------------------------
-- 2) Tính WIP — PERCENT_COMPLETED / CLOSING_WIP_QUANTITY + chế độ ngoại lệ
--    Không DELETE version CONFIRMED khác; chỉ ghi đúng RUN_VERSION hiện tại.
-- -----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS `sp_pc_wip_calculate`$$
CREATE PROCEDURE `sp_pc_wip_calculate`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_WIP_METHOD VARCHAR(40),
    IN p_USER_ID VARCHAR(50)
)
proc: BEGIN
    DECLARE v_header_ver INT;
    DECLARE v_run_status VARCHAR(20);

    SELECT RUN_VERSION, STATUS
      INTO v_header_ver, v_run_status
    FROM production_cost_run
    WHERE COMPANY_CD = p_COMPANY_CD AND RUN_ID = p_RUN_ID AND ISDEL = '0'
    LIMIT 1
    FOR UPDATE;

    IF v_header_ver IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'RUN_ID khong thuoc COMPANY_CD';
    END IF;

    IF IFNULL(p_RUN_VERSION, 0) <= 0 OR p_RUN_VERSION <> v_header_ver THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Chi tinh WIP dung RUN_VERSION hien tai tren header';
    END IF;

    IF IFNULL(v_run_status, '') = 'CONFIRMED' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Khong ghi de WIP khi RUN CONFIRMED — REOPEN / version moi';
    END IF;

    IF IFNULL(v_run_status, '') NOT IN ('DRAFT', 'PREVIEWED', 'ALLOC_CONFIRMED', 'REOPENED', '') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Trang thai RUN khong cho phep tinh WIP';
    END IF;

    -- Skeleton body: engine C# WipCostEngine đã implement; SP ghi DB chờ DEV.
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'sp_pc_wip_calculate skeleton — dung WipCostEngine C#; chua bat EnableWrites';
END proc$$

-- -----------------------------------------------------------------------------
-- 3) Confirm COST_CALC — skeleton (không apply giá kho trong 2C-03)
-- -----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS `sp_pc_cost_confirm`$$
CREATE PROCEDURE `sp_pc_cost_confirm`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    -- Khi mở: upsert production_cost_receipt theo INPUT_ID; COST_CALC=DONE;
    -- RUN.STATUS=CONFIRMED; KHÔNG gọi cập nhật kho cho đến giai đoạn sau.
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'sp_pc_cost_confirm skeleton — ReceiptPricePrep C#; chua bat EnableWrites / khong cap nhat kho';
END$$

DELIMITER ;

SELECT '2C-03 WIP SP skeleton — source_rebuild / wip_calculate / cost_confirm' AS NOTE;
