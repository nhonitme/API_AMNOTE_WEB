-- =============================================================================
-- G-K 2C-02 — Stored procedure phân bổ 622/627 (Bước 2)
-- BÀN GIAO — KHÔNG tự chạy trong agent. User tự SOURCE trên DEV/TEST sau 2B.
-- MySQL 5.6 / 8.0 · CẤM production
--
-- Trạng thái 2B hiện có:
--   ✅ sp_pc_engine_* , sp_pc_nvl_basis_build , PeriodLock EXP_TO_154_ONLY
--   ❌ sp_pc_alloc_load_pool / load_basis(type1) / calculate / confirm  (file này)
--
-- Yêu cầu: migration 2B tables + nvl_basis đã cài trên DB đích.
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

DELIMITER $$

-- -----------------------------------------------------------------------------
-- 1) Load pool 622/627 (+6212 tùy chọn) — một CHITDETAIL_ID chỉ cộng 1 lần
-- -----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS `sp_pc_alloc_load_pool`$$
CREATE PROCEDURE `sp_pc_alloc_load_pool`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_FROM_YMD CHAR(8),
    IN p_TO_YMD CHAR(8),
    IN p_IS_6212 CHAR(1),
    IN p_BY_DEPARTMENT CHAR(1),
    IN p_USER_ID VARCHAR(50)
)
proc: BEGIN
    DECLARE v_tbl INT DEFAULT 0;

    SELECT COUNT(*) INTO v_tbl
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_alloc_pool';
    IF IFNULL(v_tbl, 0) = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Thieu bang production_cost_alloc_pool';
    END IF;

    DELETE FROM production_cost_alloc_pool
    WHERE RUN_ID = p_RUN_ID AND RUN_VERSION = p_RUN_VERSION AND COMPANY_CD = p_COMPANY_CD;

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_pool_raw;
    CREATE TEMPORARY TABLE tmp_pc_pool_raw (
        CHITDETAIL_ID BIGINT NOT NULL PRIMARY KEY,
        ACCOUNT_CD VARCHAR(20) NOT NULL,
        DEPARTMENT_ID BIGINT NOT NULL DEFAULT 0,
        DEPARTMENT_CD VARCHAR(20) NULL,
        AMOUNT DECIMAL(18,2) NOT NULL DEFAULT 0,
        CREDIT_AMOUNT DECIMAL(18,2) NOT NULL DEFAULT 0,
        DEBIT_AMOUNT_154 DECIMAL(18,2) NOT NULL DEFAULT 0
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_pc_pool_raw (
        CHITDETAIL_ID, ACCOUNT_CD, DEPARTMENT_ID, DEPARTMENT_CD, AMOUNT, CREDIT_AMOUNT, DEBIT_AMOUNT_154
    )
    SELECT
        d.CHITDETAIL_ID,
        TRIM(d.DEBIT) AS ACCOUNT_CD,
        CASE WHEN p_BY_DEPARTMENT = '1' THEN IFNULL(d.DEPARTMENT_ID, 0) ELSE 0 END,
        CASE WHEN p_BY_DEPARTMENT = '1' THEN d.DEPARTMENT_CD ELSE '(ALL)' END,
        ROUND(IFNULL(d.AMOUNT, 0), 2),
        0,
        0
    FROM chitdetailinfo d
    INNER JOIN chitinfo c
        ON c.COMPANY_CD = d.COMPANY_CD
       AND c.CHIT_ID = d.CHIT_ID
       AND c.ISDEL = '0'
    WHERE d.COMPANY_CD = p_COMPANY_CD
      AND d.ISDEL = '0'
      AND c.CHIT_YMD >= p_FROM_YMD
      AND c.CHIT_YMD <= p_TO_YMD
      AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
      AND (
            TRIM(d.DEBIT) LIKE '622%'
         OR TRIM(d.DEBIT) LIKE '627%'
         OR (p_IS_6212 = '1' AND TRIM(d.DEBIT) LIKE '6212%')
      )
    ON DUPLICATE KEY UPDATE AMOUNT = AMOUNT; -- CHITDETAIL_ID PK → không nhân đôi

    -- CREDIT_AMOUNT(A) / DEBIT_AMOUNT_154(A) — cùng kỳ, CREDIT = A
    UPDATE tmp_pc_pool_raw r
    INNER JOIN (
        SELECT
            TRIM(d.CREDIT) AS ACCOUNT_CD,
            CASE WHEN p_BY_DEPARTMENT = '1' THEN IFNULL(d.DEPARTMENT_ID, 0) ELSE 0 END AS DEPARTMENT_ID,
            ROUND(SUM(IFNULL(d.AMOUNT, 0)), 2) AS CREDIT_AMOUNT,
            ROUND(SUM(CASE WHEN TRIM(d.DEBIT) LIKE '154%' THEN IFNULL(d.AMOUNT, 0) ELSE 0 END), 2) AS DEBIT_AMOUNT_154
        FROM chitdetailinfo d
        INNER JOIN chitinfo c
            ON c.COMPANY_CD = d.COMPANY_CD AND c.CHIT_ID = d.CHIT_ID AND c.ISDEL = '0'
        WHERE d.COMPANY_CD = p_COMPANY_CD
          AND d.ISDEL = '0'
          AND c.CHIT_YMD >= p_FROM_YMD AND c.CHIT_YMD <= p_TO_YMD
          AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
        GROUP BY TRIM(d.CREDIT), CASE WHEN p_BY_DEPARTMENT = '1' THEN IFNULL(d.DEPARTMENT_ID, 0) ELSE 0 END
    ) x ON x.ACCOUNT_CD = r.ACCOUNT_CD AND x.DEPARTMENT_ID = r.DEPARTMENT_ID
    SET r.CREDIT_AMOUNT = x.CREDIT_AMOUNT,
        r.DEBIT_AMOUNT_154 = x.DEBIT_AMOUNT_154;

    INSERT INTO production_cost_alloc_pool (
        RUN_ID, RUN_VERSION, COMPANY_CD, ACCOUNT_CD, CREDIT_SAMPLE, DEPARTMENT_ID, DEPARTMENT_CD,
        TOTAL_AMOUNT, FC_AMOUNT, CREDIT_AMOUNT, DEBIT_AMOUNT_154, REDUCTION, ALLOCATION_COSTS,
        ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
    )
    SELECT
        p_RUN_ID, p_RUN_VERSION, p_COMPANY_CD,
        ACCOUNT_CD, NULL, DEPARTMENT_ID, MAX(DEPARTMENT_CD),
        ROUND(SUM(AMOUNT), 2), 0,
        ROUND(MAX(CREDIT_AMOUNT), 2),
        ROUND(MAX(DEBIT_AMOUNT_154), 2),
        ROUND(MAX(CREDIT_AMOUNT) - MAX(DEBIT_AMOUNT_154), 0) AS REDUCTION,
        ROUND(SUM(AMOUNT) - (MAX(CREDIT_AMOUNT) - MAX(DEBIT_AMOUNT_154)), 0) AS ALLOCATION_COSTS,
        '0', p_USER_ID, NOW(), p_USER_ID, NOW()
    FROM tmp_pc_pool_raw
    GROUP BY ACCOUNT_CD, DEPARTMENT_ID;

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_pool_raw;
END proc$$

-- -----------------------------------------------------------------------------
-- 2) Load basis type=1 (SL TP N155/C154) — type=0 dùng sp_pc_nvl_basis_build (2B)
-- -----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS `sp_pc_alloc_load_basis_qty`$$
CREATE PROCEDURE `sp_pc_alloc_load_basis_qty`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_FROM_YMD CHAR(8),
    IN p_TO_YMD CHAR(8),
    IN p_USER_ID VARCHAR(50)
)
proc: BEGIN
    -- Ghi QUANTITY vào production_cost_alloc_detail staging qua bảng tạm / cập nhật detail sau calculate.
    -- 2C-02: lưu tiêu thức vào alloc_detail khi calculate; ở đây trả resultset để app/SP calculate dùng.
    SELECT
        o.PRODUCT_ID,
        IFNULL(o.DEPARTMENT_ID, 0) AS DEPARTMENT_ID,
        ROUND(SUM(IFNULL(i.QUANTITY, 0)), 6) AS QUANTITY
    FROM chit_inventory_input i
    INNER JOIN chitinfo c
        ON c.COMPANY_CD = i.COMPANY_CD AND c.CHIT_ID = i.CHIT_ID AND c.ISDEL = '0'
    INNER JOIN chitdetailinfo d
        ON d.COMPANY_CD = i.COMPANY_CD AND d.CHITDETAIL_ID = i.CHITDETAIL_ID AND d.ISDEL = '0'
    INNER JOIN production_cost_object o
        ON o.COMPANY_CD = i.COMPANY_CD
       AND o.RUN_ID = p_RUN_ID
       AND o.PRODUCT_ID = i.PRODUCT_ID
       AND o.IS_SELECTED = '1'
       AND o.ISDEL = '0'
    WHERE i.COMPANY_CD = p_COMPANY_CD
      AND i.ISDEL = '0'
      AND c.CHIT_YMD >= p_FROM_YMD AND c.CHIT_YMD <= p_TO_YMD
      AND TRIM(d.DEBIT) LIKE '155%'
      AND TRIM(d.CREDIT) LIKE '154%'
      AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
    GROUP BY o.PRODUCT_ID, IFNULL(o.DEPARTMENT_ID, 0);
END proc$$

-- -----------------------------------------------------------------------------
-- 3) Calculate phân bổ — Round(P_A * T_i / ΣT); dòng cuối = phần dư
-- -----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS `sp_pc_alloc_calculate`$$
CREATE PROCEDURE `sp_pc_alloc_calculate`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_ALLOCATE_TYPE TINYINT,
    IN p_BY_DEPARTMENT CHAR(1),
    IN p_NUMBER_DIGIT TINYINT,
    IN p_USER_ID VARCHAR(50)
)
proc: BEGIN
    -- Body đầy đủ nên port từ AllocationEngine C# / frmAllocateCosts.
    -- Bản 2C-02 giao skeleton + UNIQUE version; triển khai chi tiết khi bật EnableWrites.
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'sp_pc_alloc_calculate: dung AllocationEngine C# (2C-02) cho den khi bat ghi DB; bo sung body SQL sau nghiem thu';
END proc$$

-- -----------------------------------------------------------------------------
-- 4) Confirm phân bổ — idempotent STABLE_KEY ALLOC:{COMPANY}:{PERIOD}
-- -----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS `sp_pc_alloc_confirm`$$
CREATE PROCEDURE `sp_pc_alloc_confirm`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_USER_ID VARCHAR(50)
)
proc: BEGIN
    DECLARE v_period CHAR(6);
    DECLARE v_version INT;
    DECLARE v_stable VARCHAR(100);
    DECLARE v_chit BIGINT DEFAULT NULL;

    SELECT PERIOD_YM, RUN_VERSION INTO v_period, v_version
    FROM production_cost_run
    WHERE COMPANY_CD = p_COMPANY_CD AND RUN_ID = p_RUN_ID AND ISDEL = '0'
    LIMIT 1;

    IF v_period IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Khong tim thay RUN';
    END IF;

    SET v_stable = CONCAT('ALLOC:', p_COMPANY_CD, ':', v_period);

    SELECT CHIT_ID INTO v_chit
    FROM production_cost_voucher_ref
    WHERE COMPANY_CD = p_COMPANY_CD AND STABLE_KEY = v_stable AND ISDEL = '0'
    LIMIT 1;

    -- Idempotent: đã có CHIT_ID → UPDATE nội dung (không INSERT chứng từ mới).
    -- Memo phân bổ (nếu nghiệp vụ yêu cầu CT) — 2C-02 chưa tạo CT kế toán bắt buộc;
    -- chỉ đóng băng step + history.

    UPDATE production_cost_step
    SET STATUS = 'DONE',
        RESULT_SUMMARY = CONCAT('ALLOC confirm v', v_version),
        COMPLETED_BY = p_USER_ID,
        COMPLETED_AT = NOW(),
        UPDATE_BY = p_USER_ID,
        UPDATE_AT = NOW()
    WHERE COMPANY_CD = p_COMPANY_CD
      AND RUN_ID = p_RUN_ID
      AND STEP_INDEX = 2
      AND ISDEL = '0';

    INSERT INTO production_cost_history (
        RUN_ID, COMPANY_CD, PERIOD_YM, STEP_INDEX, ACTION_CODE, STATUS_AFTER,
        RUN_VERSION, RESULT_AFTER, USER_NAME, ACTION_AT, ISDEL
    ) VALUES (
        p_RUN_ID, p_COMPANY_CD, v_period, 2, 'CONFIRM', 'DONE',
        v_version, CONCAT('ALLOC confirm; stable=', v_stable, '; chit=', IFNULL(v_chit, 0)),
        p_USER_ID, NOW(), '0'
    );

    IF v_chit IS NULL THEN
        INSERT INTO production_cost_voucher_ref (
            RUN_ID, COMPANY_CD, STEP_INDEX, STEP_CODE, REF_ROLE, STABLE_KEY,
            CHIT_ID, RUN_VERSION, ISDEL, CREATE_BY, CREATE_AT
        ) VALUES (
            p_RUN_ID, p_COMPANY_CD, 2, 'COST_ALLOCATION', 'ALLOC_MEMO', v_stable,
            NULL, v_version, '0', p_USER_ID, NOW()
        )
        ON DUPLICATE KEY UPDATE
            RUN_VERSION = VALUES(RUN_VERSION),
            UPDATE_BY = p_USER_ID,
            UPDATE_AT = NOW();
    ELSE
        UPDATE production_cost_voucher_ref
        SET RUN_VERSION = v_version,
            UPDATE_BY = p_USER_ID,
            UPDATE_AT = NOW()
        WHERE COMPANY_CD = p_COMPANY_CD AND STABLE_KEY = v_stable;
    END IF;
END proc$$

DELIMITER ;

SELECT '2C-02 alloc SP script loaded (calculate body deferred to C# engine until EnableWrites)' AS NOTE;
