-- =============================================================================
-- G-K 2C-06B — Patch sp_pc_alloc_confirm: bỏ INSERT history (C# sở hữu)
-- BÀN GIAO — user tự SOURCE trên DEV sau 2c02_sp_alloc. Agent không chạy.
-- Không START/COMMIT. Không EnableWrites trong bước SOURCE này.
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

-- Áp dụng bằng cách SOURCE lại body từ 20261102_2c02_sp_alloc.sql (đã bỏ history)
-- hoặc chạy đoạn dưới nếu chỉ cần patch confirm.

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_pc_alloc_confirm`$$
CREATE PROCEDURE `sp_pc_alloc_confirm`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_USER_ID VARCHAR(50)
)
proc: BEGIN
    DECLARE v_period CHAR(6);
    DECLARE v_header_ver INT;
    DECLARE v_run_status VARCHAR(20);
    DECLARE v_step_status VARCHAR(20);
    DECLARE v_step_id BIGINT DEFAULT NULL;
    DECLARE v_detail_cnt INT DEFAULT 0;
    DECLARE v_pool_cnt INT DEFAULT 0;
    DECLARE v_stable VARCHAR(100);
    DECLARE v_ref_ver INT DEFAULT NULL;

    SELECT PERIOD_YM, RUN_VERSION, STATUS
      INTO v_period, v_header_ver, v_run_status
    FROM production_cost_run
    WHERE COMPANY_CD = p_COMPANY_CD AND RUN_ID = p_RUN_ID AND ISDEL = '0'
    LIMIT 1
    FOR UPDATE;

    IF v_period IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'RUN_ID khong thuoc COMPANY_CD';
    END IF;

    IF IFNULL(p_RUN_VERSION, 0) <= 0 OR p_RUN_VERSION <> v_header_ver THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'p_RUN_VERSION bat buoc khop production_cost_run.RUN_VERSION';
    END IF;

    SELECT COUNT(*) INTO v_pool_cnt
    FROM production_cost_alloc_pool
    WHERE COMPANY_CD = p_COMPANY_CD AND RUN_ID = p_RUN_ID AND RUN_VERSION = p_RUN_VERSION AND ISDEL = '0';

    SELECT COUNT(*) INTO v_detail_cnt
    FROM production_cost_alloc_detail
    WHERE COMPANY_CD = p_COMPANY_CD AND RUN_ID = p_RUN_ID AND RUN_VERSION = p_RUN_VERSION AND ISDEL = '0';

    IF IFNULL(v_pool_cnt, 0) = 0 OR IFNULL(v_detail_cnt, 0) = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Chua co pool/detail day du — khong CONFIRM phan bo';
    END IF;

    SELECT STEP_ID, STATUS INTO v_step_id, v_step_status
    FROM production_cost_step
    WHERE COMPANY_CD = p_COMPANY_CD AND RUN_ID = p_RUN_ID AND STEP_INDEX = 2 AND ISDEL = '0'
    LIMIT 1
    FOR UPDATE;

    SET v_stable = CONCAT('ALLOC:', p_COMPANY_CD, ':', v_period);

    SELECT RUN_VERSION INTO v_ref_ver
    FROM production_cost_voucher_ref
    WHERE COMPANY_CD = p_COMPANY_CD AND STABLE_KEY = v_stable AND ISDEL = '0'
    LIMIT 1
    FOR UPDATE;

    IF IFNULL(v_step_status, '') = 'DONE'
       AND IFNULL(v_run_status, '') IN ('ALLOC_CONFIRMED', 'CONFIRMED')
       AND IFNULL(v_ref_ver, -1) = p_RUN_VERSION THEN
        SELECT 'NOOP_ALREADY_CONFIRMED' AS RESULT, p_RUN_ID AS RUN_ID, p_RUN_VERSION AS RUN_VERSION;
        LEAVE proc;
    END IF;

    IF v_step_id IS NULL THEN
        INSERT INTO production_cost_step (
            RUN_ID, COMPANY_CD, STEP_INDEX, STEP_CODE, ENGINE, STATUS,
            RESULT_SUMMARY, COMPLETED_BY, COMPLETED_AT, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
        ) VALUES (
            p_RUN_ID, p_COMPANY_CD, 2, 'COST_ALLOCATION', 'GK', 'DONE',
            CONCAT('ALLOC confirm v', p_RUN_VERSION), p_USER_ID, NOW(), '0',
            p_USER_ID, NOW(), p_USER_ID, NOW()
        );
    ELSE
        UPDATE production_cost_step
        SET STATUS = 'DONE',
            STEP_CODE = 'COST_ALLOCATION',
            ENGINE = 'GK',
            RESULT_SUMMARY = CONCAT('ALLOC confirm v', p_RUN_VERSION),
            COMPLETED_BY = p_USER_ID,
            COMPLETED_AT = NOW(),
            UPDATE_BY = p_USER_ID,
            UPDATE_AT = NOW()
        WHERE STEP_ID = v_step_id AND COMPANY_CD = p_COMPANY_CD;
    END IF;

    UPDATE production_cost_run
    SET STATUS = CASE
            WHEN STATUS = 'CONFIRMED' THEN 'CONFIRMED'
            ELSE 'ALLOC_CONFIRMED'
        END,
        UPDATE_BY = p_USER_ID,
        UPDATE_AT = NOW()
    WHERE COMPANY_CD = p_COMPANY_CD AND RUN_ID = p_RUN_ID AND ISDEL = '0';

    INSERT INTO production_cost_voucher_ref (
        RUN_ID, COMPANY_CD, STEP_INDEX, STEP_CODE, REF_ROLE, STABLE_KEY,
        CHIT_ID, RUN_VERSION, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
    ) VALUES (
        p_RUN_ID, p_COMPANY_CD, 2, 'COST_ALLOCATION', 'ALLOC_MEMO', v_stable,
        NULL, p_RUN_VERSION, '0', p_USER_ID, NOW(), p_USER_ID, NOW()
    )
    ON DUPLICATE KEY UPDATE
        RUN_ID = VALUES(RUN_ID),
        RUN_VERSION = VALUES(RUN_VERSION),
        STEP_INDEX = 2,
        STEP_CODE = 'COST_ALLOCATION',
        REF_ROLE = 'ALLOC_MEMO',
        UPDATE_BY = p_USER_ID,
        UPDATE_AT = NOW(),
        ISDEL = '0';

    -- Không INSERT production_cost_history — C# HistoryPlan / TryInsertHistoryAsync.

    SELECT 'ALLOC_CONFIRMED' AS RESULT, p_RUN_ID AS RUN_ID, p_RUN_VERSION AS RUN_VERSION;
END proc$$

DELIMITER ;

SELECT '2C-06B sp_pc_alloc_confirm — no history insert; C# TX owner' AS NOTE;
