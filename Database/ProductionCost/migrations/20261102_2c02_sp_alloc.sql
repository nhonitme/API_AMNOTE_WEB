-- =============================================================================
-- G-K 2C-02 — SP phân bổ 622/627 (tích hợp trạng thái / TX owner / bảo vệ CONFIRMED)
-- BÀN GIAO — user tự SOURCE sau 2B + 20261102_2c02_ddl_alloc_detail_key.sql
--
-- Transaction: KHÔNG START/COMMIT trong SP — caller C# (CreateSessionAsync) sở hữu TX.
-- RUN.STATUS sau B2 Confirm = ALLOC_CONFIRMED (khác CONFIRMED toàn kỳ).
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

DELIMITER $$

-- -----------------------------------------------------------------------------
-- 1) Load pool — chặn tuyệt đối DELETE khi RUN = ALLOC_CONFIRMED/CONFIRMED
--    (kể cả STEP thiếu / chưa DONE). Chỉ DRAFT|PREVIEWED|REOPENED + đúng VERSION.
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
    DECLARE v_header_ver INT;
    DECLARE v_run_status VARCHAR(20);

    SELECT COUNT(*) INTO v_tbl
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_alloc_pool';
    IF IFNULL(v_tbl, 0) = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Thieu bang production_cost_alloc_pool';
    END IF;

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
            SET MESSAGE_TEXT = 'Chi duoc load pool dung RUN_VERSION hien tai tren header';
    END IF;

    -- Chặn tuyệt đối theo RUN.STATUS — không phụ thuộc STEP (thiếu / chưa DONE)
    IF IFNULL(v_run_status, '') IN ('ALLOC_CONFIRMED', 'CONFIRMED') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Khong xoa pool khi RUN ALLOC_CONFIRMED/CONFIRMED — REOPEN truoc khi tinh lai';
    END IF;

    IF IFNULL(v_run_status, '') NOT IN ('DRAFT', 'PREVIEWED', 'REOPENED', '') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Trang thai RUN khong cho phep ghi de pool';
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

    INSERT INTO tmp_pc_pool_raw (CHITDETAIL_ID, ACCOUNT_CD, DEPARTMENT_ID, DEPARTMENT_CD, AMOUNT)
    SELECT
        d.CHITDETAIL_ID,
        TRIM(d.DEBIT),
        CASE WHEN p_BY_DEPARTMENT = '1' THEN IFNULL(ext.DEPARTMENT_ID, 0) ELSE 0 END,
        CASE WHEN p_BY_DEPARTMENT = '1' THEN ext.DEPARTMENT_CD ELSE '(ALL)' END,
        ROUND(IFNULL(d.AMOUNT, 0), 2)
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

    INSERT INTO tmp_pc_pool_credit (ACCOUNT_CD, DEPARTMENT_ID, CREDIT_AMOUNT, DEBIT_AMOUNT_154)
    SELECT
        TRIM(d.CREDIT),
        CASE WHEN p_BY_DEPARTMENT = '1' THEN IFNULL(ext.DEPARTMENT_ID, 0) ELSE 0 END,
        ROUND(SUM(IFNULL(d.AMOUNT, 0)), 2),
        ROUND(SUM(CASE WHEN TRIM(d.DEBIT) LIKE '154%' THEN IFNULL(d.AMOUNT, 0) ELSE 0 END), 2)
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
        p_RUN_ID, p_RUN_VERSION, p_COMPANY_CD,
        r.ACCOUNT_CD, NULL, r.DEPARTMENT_ID, MAX(r.DEPARTMENT_CD),
        ROUND(SUM(r.AMOUNT), 2), 0,
        ROUND(IFNULL(MAX(cr.CREDIT_AMOUNT), 0), 2),
        ROUND(IFNULL(MAX(cr.DEBIT_AMOUNT_154), 0), 2),
        ROUND(CASE
            WHEN p_COMPANY_CD = '3619' THEN IFNULL(MAX(cr.CREDIT_AMOUNT), 0)
            ELSE IFNULL(MAX(cr.CREDIT_AMOUNT), 0) - IFNULL(MAX(cr.DEBIT_AMOUNT_154), 0)
        END, 0),
        ROUND(SUM(r.AMOUNT) - CASE
            WHEN p_COMPANY_CD = '3619' THEN IFNULL(MAX(cr.CREDIT_AMOUNT), 0)
            ELSE IFNULL(MAX(cr.CREDIT_AMOUNT), 0) - IFNULL(MAX(cr.DEBIT_AMOUNT_154), 0)
        END, 0),
        '0', p_USER_ID, NOW(), p_USER_ID, NOW()
    FROM tmp_pc_pool_raw r
    LEFT JOIN tmp_pc_pool_credit cr
        ON cr.ACCOUNT_CD = r.ACCOUNT_CD AND cr.DEPARTMENT_ID = r.DEPARTMENT_ID
    GROUP BY r.ACCOUNT_CD, r.DEPARTMENT_ID;

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_pool_raw;
    DROP TEMPORARY TABLE IF EXISTS tmp_pc_pool_credit;
END proc$$

-- -----------------------------------------------------------------------------
-- 2) Basis type=1 — mọi object (PRODUCT_ID, DEPARTMENT_ID); không MIN(DEPARTMENT_ID)
--    QUANTITY theo PRODUCT từ nhập kho; gắn từng dòng object đã chọn.
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
    DROP TEMPORARY TABLE IF EXISTS tmp_pc_qty_by_product;
    CREATE TEMPORARY TABLE tmp_pc_qty_by_product (
        PRODUCT_ID BIGINT NOT NULL PRIMARY KEY,
        QUANTITY DECIMAL(18,6) NOT NULL DEFAULT 0
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_pc_qty_by_product (PRODUCT_ID, QUANTITY)
    SELECT
        i.PRODUCT_ID,
        ROUND(SUM(IFNULL(i.QUANTITY, 0)), 6)
    FROM chit_inventory_input i
    INNER JOIN chitinfo c
        ON c.COMPANY_CD = i.COMPANY_CD AND c.CHIT_ID = i.CHIT_ID AND c.ISDEL = '0'
    INNER JOIN chitdetailinfo d
        ON d.COMPANY_CD = i.COMPANY_CD
       AND d.CHITDETAIL_ID = i.CHITDETAIL_ID
       AND d.ISDEL = '0'
    WHERE i.COMPANY_CD = p_COMPANY_CD
      AND i.ISDEL = '0'
      AND c.CHIT_YMD >= p_FROM_YMD AND c.CHIT_YMD <= p_TO_YMD
      AND TRIM(d.DEBIT) LIKE '155%'
      AND TRIM(d.CREDIT) LIKE '154%'
      AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
    GROUP BY i.PRODUCT_ID;

    -- Một dòng / object đã chọn — không tự chọn 1 phòng bằng MIN()
    SELECT
        o.PRODUCT_ID,
        o.DEPARTMENT_ID,
        IFNULL(q.QUANTITY, 0) AS QUANTITY
    FROM production_cost_object o
    LEFT JOIN tmp_pc_qty_by_product q
        ON q.PRODUCT_ID = o.PRODUCT_ID
    WHERE o.COMPANY_CD = p_COMPANY_CD
      AND o.RUN_ID = p_RUN_ID
      AND o.IS_SELECTED = '1'
      AND o.ISDEL = '0'
    ORDER BY o.PRODUCT_ID, o.DEPARTMENT_ID;

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_qty_by_product;
END proc$$

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
        SET MESSAGE_TEXT = 'sp_pc_alloc_calculate skeleton — dung AllocationEngine C#; chua bat EnableWrites';
END proc$$

-- -----------------------------------------------------------------------------
-- 3) Confirm Bước 2 → step DONE + RUN.STATUS=ALLOC_CONFIRMED
--    Không START/COMMIT (TX của caller). Idempotent + FOR UPDATE.
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
    DECLARE v_ref_ver INT DEFAULT NULL;
    DECLARE v_changed INT DEFAULT 0;

    -- KHÔNG START TRANSACTION — caller C# giữ atomicity / rollback

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

    -- Idempotent đồng thời: đã DONE + ALLOC_CONFIRMED + cùng version → NOOP (không history)
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
        WHERE STEP_ID = v_step_id AND COMPANY_CD = p_COMPANY_CD;
        IF IFNULL(v_step_status, '') <> 'DONE' THEN
            SET v_changed = 1;
        END IF;
    END IF;

    -- Phân biệt B2 vs giá thành toàn kỳ: không set CONFIRMED ở đây
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

    -- History: KHÔNG INSERT ở đây (2C-06B) — C# TryInsertHistoryAsync sở hữu OperationId/fingerprint.
    -- Tránh ghi history hai lần (SP + C#).
    SELECT 'ALLOC_CONFIRMED' AS RESULT, p_RUN_ID AS RUN_ID, p_RUN_VERSION AS RUN_VERSION;
END proc$$

-- Helper Bước 3: assert phân bổ đã khóa theo RUN_ID+VERSION (không đụng 3 rule B-I)
DROP PROCEDURE IF EXISTS `sp_pc_assert_allocation_ready_for_exp`$$
CREATE PROCEDURE `sp_pc_assert_allocation_ready_for_exp`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6)
)
proc: BEGIN
    DECLARE v_run_id BIGINT;
    DECLARE v_run_ver INT;
    DECLARE v_run_status VARCHAR(20);
    DECLARE v_step_status VARCHAR(20);
    DECLARE v_alloc_sum DECIMAL(18,2);

    SELECT p.CURRENT_RUN_ID, r.RUN_VERSION, r.STATUS
      INTO v_run_id, v_run_ver, v_run_status
    FROM production_cost_period p
    INNER JOIN production_cost_run r
        ON r.RUN_ID = p.CURRENT_RUN_ID AND r.COMPANY_CD = p.COMPANY_CD AND r.ISDEL = '0'
    WHERE p.COMPANY_CD = p_COMPANY_CD AND p.PERIOD_YM = p_PERIOD_YM AND p.ISDEL = '0'
    LIMIT 1;

    IF v_run_id IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'EXP_TO_154_ONLY can CURRENT_RUN G-K';
    END IF;

    IF IFNULL(v_run_status, '') NOT IN ('ALLOC_CONFIRMED', 'CONFIRMED') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'EXP_TO_154_ONLY can RUN STATUS=ALLOC_CONFIRMED (B2) hoac CONFIRMED';
    END IF;

    SELECT STATUS INTO v_step_status
    FROM production_cost_step
    WHERE COMPANY_CD = p_COMPANY_CD AND RUN_ID = v_run_id AND STEP_INDEX = 2 AND ISDEL = '0'
    LIMIT 1;

    IF IFNULL(v_step_status, '') <> 'DONE' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'EXP_TO_154_ONLY can COST_ALLOCATION=DONE dung RUN_ID+VERSION';
    END IF;

    SELECT IFNULL(SUM(ALLOCATED_AMOUNT), 0) INTO v_alloc_sum
    FROM production_cost_alloc_detail
    WHERE COMPANY_CD = p_COMPANY_CD
      AND RUN_ID = v_run_id AND RUN_VERSION = v_run_ver AND ISDEL = '0'
      AND (ACCOUNT_CD LIKE '622%' OR ACCOUNT_CD LIKE '627%');

    IF IFNULL(v_alloc_sum, 0) <= 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Chua co ket qua phan bo 622/627 cho RUN_VERSION hien tai';
    END IF;
END proc$$

DELIMITER ;

SELECT '2C-02 alloc SP integrated — no internal TX; ALLOC_CONFIRMED; load_pool protected' AS NOTE;
