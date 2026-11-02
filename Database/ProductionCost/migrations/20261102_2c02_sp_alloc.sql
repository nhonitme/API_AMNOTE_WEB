-- =============================================================================
-- G-K 2C-02 — Stored procedure phân bổ 622/627 (Bước 2) — bản sửa logic
-- BÀN GIAO — KHÔNG tự chạy trong agent. User tự SOURCE trên DEV/TEST sau 2B.
-- MySQL 5.6 / 8.0 · CẤM production
--
-- Đồng bộ C#:
--   REDUCTION = CREDIT − DEBIT_154; company 3619 → REDUCTION = CREDIT
--   ALLOCATION_COSTS = TOTAL − REDUCTION
--   DEPARTMENT_ID lấy từ chitdetailinfo_ext (JOIN *_ID + COMPANY_CD)
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

DELIMITER $$

-- -----------------------------------------------------------------------------
-- 1) Load pool 622/627 (+6212 tùy chọn)
--    - 1 CHITDETAIL_ID chỉ cộng 1 lần (không nhân khi JOIN)
--    - DEPARTMENT từ chitdetailinfo_ext.DEPARTMENT_ID
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
        AMOUNT DECIMAL(18,2) NOT NULL DEFAULT 0
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    -- Nợ 622/627/(6212) — PK CHITDETAIL_ID chống nhân đôi
    INSERT INTO tmp_pc_pool_raw (CHITDETAIL_ID, ACCOUNT_CD, DEPARTMENT_ID, DEPARTMENT_CD, AMOUNT)
    SELECT
        d.CHITDETAIL_ID,
        TRIM(d.DEBIT) AS ACCOUNT_CD,
        CASE
            WHEN p_BY_DEPARTMENT = '1' THEN IFNULL(ext.DEPARTMENT_ID, 0)
            ELSE 0
        END AS DEPARTMENT_ID,
        CASE
            WHEN p_BY_DEPARTMENT = '1' THEN ext.DEPARTMENT_CD
            ELSE '(ALL)'
        END AS DEPARTMENT_CD,
        ROUND(IFNULL(d.AMOUNT, 0), 2)
    FROM chitdetailinfo d
    INNER JOIN chitinfo c
        ON c.COMPANY_CD = d.COMPANY_CD
       AND c.CHIT_ID = d.CHIT_ID
       AND c.ISDEL = '0'
    LEFT JOIN chitdetailinfo_ext ext
        ON ext.COMPANY_CD = d.COMPANY_CD
       AND ext.CHITDETAIL_ID = d.CHITDETAIL_ID
       AND ext.ISDEL = '0'
    WHERE d.COMPANY_CD = p_COMPANY_CD
      AND d.ISDEL = '0'
      AND c.CHIT_YMD >= p_FROM_YMD
      AND c.CHIT_YMD <= p_TO_YMD
      AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
      AND (
            TRIM(d.DEBIT) LIKE '622%'
         OR TRIM(d.DEBIT) LIKE '627%'
         OR (p_IS_6212 = '1' AND TRIM(d.DEBIT) LIKE '6212%')
      );

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_pool_credit;
    CREATE TEMPORARY TABLE tmp_pc_pool_credit (
        ACCOUNT_CD VARCHAR(20) NOT NULL,
        DEPARTMENT_ID BIGINT NOT NULL DEFAULT 0,
        CREDIT_AMOUNT DECIMAL(18,2) NOT NULL DEFAULT 0,
        DEBIT_AMOUNT_154 DECIMAL(18,2) NOT NULL DEFAULT 0,
        PRIMARY KEY (ACCOUNT_CD, DEPARTMENT_ID)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    -- CREDIT = A (cùng ACCOUNT_CD) trong kỳ — không nhân JOIN (group theo ID)
    INSERT INTO tmp_pc_pool_credit (ACCOUNT_CD, DEPARTMENT_ID, CREDIT_AMOUNT, DEBIT_AMOUNT_154)
    SELECT
        TRIM(d.CREDIT) AS ACCOUNT_CD,
        CASE WHEN p_BY_DEPARTMENT = '1' THEN IFNULL(ext.DEPARTMENT_ID, 0) ELSE 0 END AS DEPARTMENT_ID,
        ROUND(SUM(IFNULL(d.AMOUNT, 0)), 2) AS CREDIT_AMOUNT,
        ROUND(SUM(CASE WHEN TRIM(d.DEBIT) LIKE '154%' THEN IFNULL(d.AMOUNT, 0) ELSE 0 END), 2) AS DEBIT_AMOUNT_154
    FROM chitdetailinfo d
    INNER JOIN chitinfo c
        ON c.COMPANY_CD = d.COMPANY_CD AND c.CHIT_ID = d.CHIT_ID AND c.ISDEL = '0'
    LEFT JOIN chitdetailinfo_ext ext
        ON ext.COMPANY_CD = d.COMPANY_CD
       AND ext.CHITDETAIL_ID = d.CHITDETAIL_ID
       AND ext.ISDEL = '0'
    WHERE d.COMPANY_CD = p_COMPANY_CD
      AND d.ISDEL = '0'
      AND c.CHIT_YMD >= p_FROM_YMD AND c.CHIT_YMD <= p_TO_YMD
      AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
      AND (
            TRIM(d.CREDIT) LIKE '622%'
         OR TRIM(d.CREDIT) LIKE '627%'
         OR (p_IS_6212 = '1' AND TRIM(d.CREDIT) LIKE '6212%')
      )
    GROUP BY TRIM(d.CREDIT), CASE WHEN p_BY_DEPARTMENT = '1' THEN IFNULL(ext.DEPARTMENT_ID, 0) ELSE 0 END;

    INSERT INTO production_cost_alloc_pool (
        RUN_ID, RUN_VERSION, COMPANY_CD, ACCOUNT_CD, CREDIT_SAMPLE, DEPARTMENT_ID, DEPARTMENT_CD,
        TOTAL_AMOUNT, FC_AMOUNT, CREDIT_AMOUNT, DEBIT_AMOUNT_154, REDUCTION, ALLOCATION_COSTS,
        ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
    )
    SELECT
        p_RUN_ID,
        p_RUN_VERSION,
        p_COMPANY_CD,
        r.ACCOUNT_CD,
        NULL,
        r.DEPARTMENT_ID,
        MAX(r.DEPARTMENT_CD),
        ROUND(SUM(r.AMOUNT), 2) AS TOTAL_AMOUNT,
        0,
        ROUND(IFNULL(MAX(cr.CREDIT_AMOUNT), 0), 2) AS CREDIT_AMOUNT,
        ROUND(IFNULL(MAX(cr.DEBIT_AMOUNT_154), 0), 2) AS DEBIT_AMOUNT_154,
        -- Đồng bộ AllocationMath: 3619 → REDUCTION = CREDIT; còn lại CREDIT − DEBIT_154
        ROUND(
            CASE
                WHEN p_COMPANY_CD = '3619' THEN IFNULL(MAX(cr.CREDIT_AMOUNT), 0)
                ELSE IFNULL(MAX(cr.CREDIT_AMOUNT), 0) - IFNULL(MAX(cr.DEBIT_AMOUNT_154), 0)
            END
        , 0) AS REDUCTION,
        ROUND(
            SUM(r.AMOUNT) - CASE
                WHEN p_COMPANY_CD = '3619' THEN IFNULL(MAX(cr.CREDIT_AMOUNT), 0)
                ELSE IFNULL(MAX(cr.CREDIT_AMOUNT), 0) - IFNULL(MAX(cr.DEBIT_AMOUNT_154), 0)
            END
        , 0) AS ALLOCATION_COSTS,
        '0',
        p_USER_ID,
        NOW(),
        p_USER_ID,
        NOW()
    FROM tmp_pc_pool_raw r
    LEFT JOIN tmp_pc_pool_credit cr
        ON cr.ACCOUNT_CD = r.ACCOUNT_CD
       AND cr.DEPARTMENT_ID = r.DEPARTMENT_ID
    GROUP BY r.ACCOUNT_CD, r.DEPARTMENT_ID;

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_pool_raw;
    DROP TEMPORARY TABLE IF EXISTS tmp_pc_pool_credit;
END proc$$

-- -----------------------------------------------------------------------------
-- 2) Load basis type=1 (SL TP N155/C154) — không nhân dòng JOIN
--    type=0: dùng sp_pc_nvl_basis_build (2B)
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
    -- Object gộp theo PRODUCT_ID (tránh nhân dòng khi cùng TP chọn nhiều DEPARTMENT)
    -- INPUT không nhân: join CHITDETAIL_ID + COMPANY_CD
    SELECT
        i.PRODUCT_ID,
        o.DEPARTMENT_ID,
        ROUND(SUM(IFNULL(i.QUANTITY, 0)), 6) AS QUANTITY
    FROM chit_inventory_input i
    INNER JOIN chitinfo c
        ON c.COMPANY_CD = i.COMPANY_CD AND c.CHIT_ID = i.CHIT_ID AND c.ISDEL = '0'
    INNER JOIN chitdetailinfo d
        ON d.COMPANY_CD = i.COMPANY_CD
       AND d.CHITDETAIL_ID = i.CHITDETAIL_ID
       AND d.ISDEL = '0'
    INNER JOIN (
        SELECT COMPANY_CD, RUN_ID, PRODUCT_ID, MIN(DEPARTMENT_ID) AS DEPARTMENT_ID
        FROM production_cost_object
        WHERE RUN_ID = p_RUN_ID
          AND COMPANY_CD = p_COMPANY_CD
          AND IS_SELECTED = '1'
          AND ISDEL = '0'
        GROUP BY COMPANY_CD, RUN_ID, PRODUCT_ID
    ) o
        ON o.COMPANY_CD = i.COMPANY_CD
       AND o.PRODUCT_ID = i.PRODUCT_ID
    WHERE i.COMPANY_CD = p_COMPANY_CD
      AND i.ISDEL = '0'
      AND c.CHIT_YMD >= p_FROM_YMD AND c.CHIT_YMD <= p_TO_YMD
      AND TRIM(d.DEBIT) LIKE '155%'
      AND TRIM(d.CREDIT) LIKE '154%'
      AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
    GROUP BY i.PRODUCT_ID, o.DEPARTMENT_ID;
END proc$$

-- -----------------------------------------------------------------------------
-- 3) Calculate — skeleton (engine chính = C# AllocationEngine đến khi EnableWrites)
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
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'sp_pc_alloc_calculate chua implement body SQL — dung AllocationEngine C# (2C-02). Chi bat sau EnableWrites + bo sung body.';
END proc$$

-- -----------------------------------------------------------------------------
-- 4) Confirm phân bổ — idempotent theo RUN_ID + RUN_VERSION + step STATUS
--    Không dựa CHIT_ID IS NULL để kết luận chưa confirm.
-- -----------------------------------------------------------------------------
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
    DECLARE v_ref_exists INT DEFAULT 0;
    DECLARE v_changed INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    SELECT PERIOD_YM, RUN_VERSION, STATUS
      INTO v_period, v_header_ver, v_run_status
    FROM production_cost_run
    WHERE COMPANY_CD = p_COMPANY_CD
      AND RUN_ID = p_RUN_ID
      AND ISDEL = '0'
    LIMIT 1
    FOR UPDATE;

    IF v_period IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'RUN_ID khong thuoc COMPANY_CD hoac khong ton tai';
    END IF;

    IF p_RUN_VERSION IS NULL OR p_RUN_VERSION <= 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'p_RUN_VERSION bat buoc > 0';
    END IF;

    IF p_RUN_VERSION <> v_header_ver THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'p_RUN_VERSION khong khop production_cost_run.RUN_VERSION';
    END IF;

    SELECT COUNT(*) INTO v_pool_cnt
    FROM production_cost_alloc_pool
    WHERE COMPANY_CD = p_COMPANY_CD
      AND RUN_ID = p_RUN_ID
      AND RUN_VERSION = p_RUN_VERSION
      AND ISDEL = '0';

    SELECT COUNT(*) INTO v_detail_cnt
    FROM production_cost_alloc_detail
    WHERE COMPANY_CD = p_COMPANY_CD
      AND RUN_ID = p_RUN_ID
      AND RUN_VERSION = p_RUN_VERSION
      AND ISDEL = '0';

    IF IFNULL(v_pool_cnt, 0) = 0 OR IFNULL(v_detail_cnt, 0) = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Chua co pool/detail day du cho RUN_ID+RUN_VERSION — khong CONFIRM';
    END IF;

    SELECT STEP_ID, STATUS
      INTO v_step_id, v_step_status
    FROM production_cost_step
    WHERE COMPANY_CD = p_COMPANY_CD
      AND RUN_ID = p_RUN_ID
      AND STEP_INDEX = 2
      AND ISDEL = '0'
    LIMIT 1
    FOR UPDATE;

    SET v_stable = CONCAT('ALLOC:', p_COMPANY_CD, ':', v_period);

    -- Idempotent: step đã DONE cùng version → không thêm history / không đổi kết quả
    IF IFNULL(v_step_status, '') = 'DONE' THEN
        SELECT COUNT(*) INTO v_ref_exists
        FROM production_cost_voucher_ref
        WHERE COMPANY_CD = p_COMPANY_CD
          AND STABLE_KEY = v_stable
          AND RUN_ID = p_RUN_ID
          AND RUN_VERSION = p_RUN_VERSION
          AND ISDEL = '0';

        IF IFNULL(v_ref_exists, 0) > 0 THEN
            COMMIT;
            SELECT 'NOOP_ALREADY_CONFIRMED' AS RESULT, p_RUN_ID AS RUN_ID, p_RUN_VERSION AS RUN_VERSION;
            LEAVE proc;
        END IF;
    END IF;

    -- Upsert step COST_ALLOCATION nếu chưa có
    IF v_step_id IS NULL THEN
        INSERT INTO production_cost_step (
            RUN_ID, COMPANY_CD, STEP_INDEX, STEP_CODE, ENGINE, STATUS, BI_STEP_CODE,
            RESULT_SUMMARY, COMPLETED_BY, COMPLETED_AT, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
        ) VALUES (
            p_RUN_ID, p_COMPANY_CD, 2, 'COST_ALLOCATION', 'GK', 'DONE', NULL,
            CONCAT('ALLOC confirm v', p_RUN_VERSION), p_USER_ID, NOW(), '0',
            p_USER_ID, NOW(), p_USER_ID, NOW()
        );
        SET v_changed = 1;
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
        WHERE STEP_ID = v_step_id
          AND COMPANY_CD = p_COMPANY_CD
          AND (STATUS IS NULL OR STATUS <> 'DONE' OR IFNULL(RESULT_SUMMARY, '') <> CONCAT('ALLOC confirm v', p_RUN_VERSION));
        SET v_changed = ROW_COUNT();
    END IF;

    -- Đồng bộ RUN (preview → confirmed allocation stage; không đổi COST_ENGINE ở bước 2)
    UPDATE production_cost_run
    SET STATUS = CASE
            WHEN STATUS IN ('DRAFT', 'PREVIEWED', '') OR STATUS IS NULL THEN 'PREVIEWED'
            ELSE STATUS
        END,
        UPDATE_BY = p_USER_ID,
        UPDATE_AT = NOW()
    WHERE COMPANY_CD = p_COMPANY_CD
      AND RUN_ID = p_RUN_ID
      AND ISDEL = '0';

    -- voucher_ref theo STABLE_KEY (có thể CHIT_ID NULL) — không dùng CHIT_ID IS NULL làm điều kiện confirm
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

    -- History chỉ khi trạng thái thực sự đổi (tránh trùng confirm cùng version)
    IF v_changed > 0 OR IFNULL(v_step_status, '') <> 'DONE' THEN
        INSERT INTO production_cost_history (
            RUN_ID, COMPANY_CD, PERIOD_YM, STEP_INDEX, ACTION_CODE, STATUS_AFTER,
            RUN_VERSION, RESULT_AFTER, USER_NAME, ACTION_AT, ISDEL
        ) VALUES (
            p_RUN_ID, p_COMPANY_CD, v_period, 2, 'CONFIRM', 'DONE',
            p_RUN_VERSION,
            CONCAT('ALLOC confirm; stable=', v_stable, '; pool=', v_pool_cnt, '; detail=', v_detail_cnt),
            p_USER_ID, NOW(), '0'
        );
    END IF;

    COMMIT;
    SELECT 'CONFIRMED' AS RESULT, p_RUN_ID AS RUN_ID, p_RUN_VERSION AS RUN_VERSION;
END proc$$

DELIMITER ;

SELECT '2C-02 alloc SP (logic-fixed) — calculate body still deferred to C# AllocationEngine' AS NOTE;
