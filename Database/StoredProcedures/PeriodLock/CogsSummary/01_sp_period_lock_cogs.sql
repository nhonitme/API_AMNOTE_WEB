-- PeriodLock COGS: EXP_TO_154_ONLY + 3 rule B-I (an toàn exclusive)
-- Đồng bộ nội dung logic với:
--   Database/ProductionCost/migrations/20261102_2b_04_sp_period_lock_cogs.sql
-- B3 G-K: nhận RUN.STATUS=ALLOC_CONFIRMED (sau B2) qua sp_pc_assert_allocation_ready_for_exp.
-- Ba rule B-I không đổi hành vi.
--
-- Cài từ PeriodLock/00_install_all.sql — KHÔNG SOURCE guard migration 2B
-- (guard chỉ dùng khi SOURCE file trong ProductionCost/migrations).
--
-- Transaction owner: caller (API/job). SP này không COMMIT.
-- validate/calculate = check only (không claim, không xóa CT).
-- create_voucher = claim → build → (chỉ khi build OK) xóa CT cũ + post.
-- Exclusive fail / validate fail: handler KHÔNG xóa chứng từ hiện có.
--
-- Yêu cầu (G-K EXP): đã cài sp_pc_assert_allocation_ready_for_exp (2c02_sp_alloc.sql).

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_build_xfer`$$
CREATE PROCEDURE `sp_period_lock_cogs_build_xfer`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_RULE_CODE VARCHAR(50)
)
proc: BEGIN
    DECLARE v_from_ymd CHAR(8);
    DECLARE v_to_ymd CHAR(8);
    DECLARE v_acc_154 VARCHAR(20);
    DECLARE v_acc_155 VARCHAR(20);
    DECLARE v_acc_632 VARCHAR(20);
    DECLARE v_has_source INT DEFAULT 0;
    DECLARE v_run_id BIGINT DEFAULT NULL;
    DECLARE v_run_ver INT DEFAULT NULL;
    DECLARE v_run_status VARCHAR(20) DEFAULT NULL;
    DECLARE v_alloc_sum DECIMAL(18,2) DEFAULT 0;
    DECLARE v_pool_sum DECIMAL(18,2) DEFAULT 0;
    DECLARE v_tbl_alloc INT DEFAULT 0;

    SET v_from_ymd = CONCAT(p_PERIOD_YM, '01');
    SET v_to_ymd = DATE_FORMAT(LAST_DAY(STR_TO_DATE(CONCAT(p_PERIOD_YM, '01'), '%Y%m%d')), '%Y%m%d');

    DROP TEMPORARY TABLE IF EXISTS tmp_xfer_lines;
    CREATE TEMPORARY TABLE tmp_xfer_lines
    (
        TRANSFER_CODE VARCHAR(30) NOT NULL DEFAULT '',
        DEBIT_ACC VARCHAR(20) NOT NULL DEFAULT '',
        CREDIT_ACC VARCHAR(20) NOT NULL DEFAULT '',
        AMOUNT DECIMAL(18,2) NOT NULL DEFAULT 0,
        DEPARTMENT_ID BIGINT NOT NULL DEFAULT 0,
        KEY idx_tmp_xfer_lines_01 (TRANSFER_CODE)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    DROP TEMPORARY TABLE IF EXISTS tmp_154_dept;
    CREATE TEMPORARY TABLE tmp_154_dept
    (
        DEPARTMENT_ID BIGINT NOT NULL DEFAULT 0,
        AMOUNT DECIMAL(18,2) NOT NULL DEFAULT 0,
        PRIMARY KEY (DEPARTMENT_ID)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    -- ========== G-K: EXP_TO_154_ONLY ==========
    IF p_RULE_CODE = 'EXP_TO_154_ONLY' THEN
        SELECT COUNT(*) INTO v_tbl_alloc
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_alloc_detail';

        IF IFNULL(v_tbl_alloc, 0) = 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'EXP_TO_154_ONLY can bang production_cost_alloc_detail';
        END IF;

        -- Preview được chạy trước claim: không yêu cầu COST_ENGINE=GK tại đây.
        -- B2 Confirm → ALLOC_CONFIRMED + COST_ALLOCATION=DONE (khác CONFIRMED toàn kỳ).
        -- Ba rule B-I không đi nhánh này — không đổi hành vi.
        CALL sp_pc_assert_allocation_ready_for_exp(p_COMPANY_CD, p_PERIOD_YM);

        SELECT p.CURRENT_RUN_ID, r.RUN_VERSION, r.STATUS
          INTO v_run_id, v_run_ver, v_run_status
        FROM production_cost_period p
        INNER JOIN production_cost_run r
            ON r.RUN_ID = p.CURRENT_RUN_ID
           AND r.COMPANY_CD = p.COMPANY_CD
           AND r.ISDEL = '0'
        WHERE p.COMPANY_CD = p_COMPANY_CD
          AND p.PERIOD_YM = p_PERIOD_YM
          AND p.ISDEL = '0'
        LIMIT 1;

        IF v_run_id IS NULL OR v_run_ver IS NULL THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'EXP_TO_154_ONLY can CURRENT_RUN_ID G-K';
        END IF;

        SELECT IFNULL(SUM(ad.ALLOCATED_AMOUNT), 0)
          INTO v_alloc_sum
        FROM production_cost_alloc_detail ad
        WHERE ad.COMPANY_CD = p_COMPANY_CD
          AND ad.RUN_ID = v_run_id
          AND ad.RUN_VERSION = v_run_ver
          AND ad.ISDEL = '0'
          AND (
                ad.ACCOUNT_CD LIKE '622%'
             OR ad.ACCOUNT_CD LIKE '627%'
          );

        IF IFNULL(v_alloc_sum, 0) <= 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Chua co ket qua phan bo 622/627 CONFIRMED cho ky G-K';
        END IF;

        -- Đối chiếu với pool sau REDUCTION (ALLOCATION_COSTS), không so GL Nợ thô
        SELECT IFNULL(SUM(ap.ALLOCATION_COSTS), 0)
          INTO v_pool_sum
        FROM production_cost_alloc_pool ap
        WHERE ap.COMPANY_CD = p_COMPANY_CD
          AND ap.RUN_ID = v_run_id
          AND ap.RUN_VERSION = v_run_ver
          AND ap.ISDEL = '0'
          AND (
                ap.ACCOUNT_CD LIKE '622%'
             OR ap.ACCOUNT_CD LIKE '627%'
          );

        IF ABS(IFNULL(v_pool_sum, 0) - IFNULL(v_alloc_sum, 0)) > 1 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Tong phan bo 622/627 khong khop pool ALLOCATION_COSTS (sau REDUCTION)';
        END IF;

        CALL sp_period_lock_resolve_posting_acc(p_COMPANY_CD, '154', v_acc_154);

        -- 621 (loại 6212) + 623 + 6212 từ GL nguồn — không lấy CT LOCK
        INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
        SELECT
            'EXP_TO_154',
            v_acc_154,
            TRIM(CD.DEBIT),
            SUM(CD.AMOUNT),
            IFNULL(ext.DEPARTMENT_ID, 0)
        FROM chitinfo C
        INNER JOIN chitdetailinfo CD
            ON CD.COMPANY_CD = C.COMPANY_CD
           AND CD.CHIT_ID = C.CHIT_ID
           AND CD.ISDEL = '0'
        LEFT JOIN chitdetailinfo_ext ext
            ON ext.COMPANY_CD = CD.COMPANY_CD
           AND ext.CHITDETAIL_ID = CD.CHITDETAIL_ID
           AND ext.ISDEL = '0'
        INNER JOIN acclist_info A
            ON A.COMPANY_CD = CD.COMPANY_CD
           AND A.ACC_CD = TRIM(CD.DEBIT)
           AND IFNULL(A.ISDEL, '0') = '0'
           AND IFNULL(A.ISABLEINPUT, '0') = '1'
        WHERE C.COMPANY_CD = p_COMPANY_CD
          AND C.ISDEL = '0'
          AND IFNULL(C.INPUT_TYPE, '') <> 'LOCK'
          AND C.CHIT_YMD >= v_from_ymd
          AND C.CHIT_YMD <= v_to_ymd
          AND (
                (TRIM(CD.DEBIT) LIKE '621%' AND TRIM(CD.DEBIT) NOT LIKE '6212%')
             OR TRIM(CD.DEBIT) LIKE '623%'
             OR TRIM(CD.DEBIT) LIKE '6212%'
          )
        GROUP BY TRIM(CD.DEBIT), IFNULL(ext.DEPARTMENT_ID, 0)
        HAVING SUM(CD.AMOUNT) > 0;

        -- 622/627 chỉ từ phân bổ CONFIRMED
        INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
        SELECT
            'EXP_TO_154',
            v_acc_154,
            TRIM(ad.ACCOUNT_CD),
            SUM(ad.ALLOCATED_AMOUNT),
            IFNULL(ad.DEPARTMENT_ID, 0)
        FROM production_cost_alloc_detail ad
        WHERE ad.COMPANY_CD = p_COMPANY_CD
          AND ad.RUN_ID = v_run_id
          AND ad.RUN_VERSION = v_run_ver
          AND ad.ISDEL = '0'
          AND (
                ad.ACCOUNT_CD LIKE '622%'
             OR ad.ACCOUNT_CD LIKE '627%'
          )
        GROUP BY TRIM(ad.ACCOUNT_CD), IFNULL(ad.DEPARTMENT_ID, 0)
        HAVING SUM(ad.ALLOCATED_AMOUNT) > 0;

        -- Không tạo 154→155 / 154→632
        LEAVE proc;
    END IF;

    -- ========== B-I: 3 rule hiện tại (không đổi hành vi nguồn) ==========
    SELECT COUNT(*)
      INTO v_has_source
    FROM chitinfo C
    INNER JOIN chitdetailinfo CD
        ON CD.COMPANY_CD = C.COMPANY_CD
       AND CD.CHIT_ID = C.CHIT_ID
       AND CD.ISDEL = '0'
    INNER JOIN acclist_info A
        ON A.COMPANY_CD = CD.COMPANY_CD
       AND A.ACC_CD = TRIM(CD.DEBIT)
       AND IFNULL(A.ISDEL, '0') = '0'
       AND IFNULL(A.ISABLEINPUT, '0') = '1'
    WHERE C.COMPANY_CD = p_COMPANY_CD
      AND C.ISDEL = '0'
      AND C.CHIT_YMD >= v_from_ymd
      AND C.CHIT_YMD <= v_to_ymd
      AND IFNULL(TRIM(CD.DEBIT), '') <> ''
      AND IFNULL(TRIM(CD.CREDIT), '') <> ''
      AND (
            TRIM(CD.DEBIT) LIKE '621%'
         OR TRIM(CD.DEBIT) LIKE '622%'
         OR TRIM(CD.DEBIT) LIKE '623%'
         OR TRIM(CD.DEBIT) LIKE '627%'
         OR TRIM(CD.DEBIT) LIKE '154%'
      );

    IF IFNULL(v_has_source, 0) = 0 THEN
        LEAVE proc;
    END IF;

    CALL sp_period_lock_resolve_posting_acc(p_COMPANY_CD, '154', v_acc_154);

    IF p_RULE_CODE IN ('154_TO_155', '154_TO_155_TO_632') THEN
        CALL sp_period_lock_resolve_posting_acc(p_COMPANY_CD, '155', v_acc_155);
    END IF;

    IF p_RULE_CODE IN ('154_TO_632', '154_TO_155_TO_632') THEN
        CALL sp_period_lock_resolve_posting_acc(p_COMPANY_CD, '632', v_acc_632);
    END IF;

    INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
    SELECT
        'EXP_TO_154',
        v_acc_154,
        TRIM(CD.DEBIT),
        SUM(CD.AMOUNT),
        IFNULL(ext.DEPARTMENT_ID, 0)
    FROM chitinfo C
    INNER JOIN chitdetailinfo CD
        ON CD.COMPANY_CD = C.COMPANY_CD
       AND CD.CHIT_ID = C.CHIT_ID
       AND CD.ISDEL = '0'
    LEFT JOIN chitdetailinfo_ext ext
        ON ext.COMPANY_CD = CD.COMPANY_CD
       AND ext.CHITDETAIL_ID = CD.CHITDETAIL_ID
       AND ext.ISDEL = '0'
    INNER JOIN acclist_info A
        ON A.COMPANY_CD = CD.COMPANY_CD
       AND A.ACC_CD = TRIM(CD.DEBIT)
       AND IFNULL(A.ISDEL, '0') = '0'
       AND IFNULL(A.ISABLEINPUT, '0') = '1'
    WHERE C.COMPANY_CD = p_COMPANY_CD
      AND C.ISDEL = '0'
      AND C.CHIT_YMD >= v_from_ymd
      AND C.CHIT_YMD <= v_to_ymd
      AND IFNULL(TRIM(CD.DEBIT), '') <> ''
      AND IFNULL(TRIM(CD.CREDIT), '') <> ''
      AND (
            TRIM(CD.DEBIT) LIKE '621%'
         OR TRIM(CD.DEBIT) LIKE '622%'
         OR TRIM(CD.DEBIT) LIKE '623%'
         OR TRIM(CD.DEBIT) LIKE '627%'
      )
    GROUP BY TRIM(CD.DEBIT), IFNULL(ext.DEPARTMENT_ID, 0)
    HAVING SUM(CD.AMOUNT) > 0;

    INSERT INTO tmp_154_dept (DEPARTMENT_ID, AMOUNT)
    SELECT DEPARTMENT_ID, SUM(AMOUNT)
    FROM tmp_xfer_lines
    WHERE TRANSFER_CODE = 'EXP_TO_154'
    GROUP BY DEPARTMENT_ID
    ON DUPLICATE KEY UPDATE AMOUNT = AMOUNT + VALUES(AMOUNT);

    INSERT INTO tmp_154_dept (DEPARTMENT_ID, AMOUNT)
    SELECT
        IFNULL(ext.DEPARTMENT_ID, 0),
        SUM(CD.AMOUNT)
    FROM chitinfo C
    INNER JOIN chitdetailinfo CD
        ON CD.COMPANY_CD = C.COMPANY_CD
       AND CD.CHIT_ID = C.CHIT_ID
       AND CD.ISDEL = '0'
    LEFT JOIN chitdetailinfo_ext ext
        ON ext.COMPANY_CD = CD.COMPANY_CD
       AND ext.CHITDETAIL_ID = CD.CHITDETAIL_ID
       AND ext.ISDEL = '0'
    INNER JOIN acclist_info A
        ON A.COMPANY_CD = CD.COMPANY_CD
       AND A.ACC_CD = TRIM(CD.DEBIT)
       AND IFNULL(A.ISDEL, '0') = '0'
       AND IFNULL(A.ISABLEINPUT, '0') = '1'
    WHERE C.COMPANY_CD = p_COMPANY_CD
      AND C.ISDEL = '0'
      AND C.CHIT_YMD >= v_from_ymd
      AND C.CHIT_YMD <= v_to_ymd
      AND TRIM(CD.DEBIT) LIKE '154%'
      AND IFNULL(TRIM(CD.CREDIT), '') <> ''
    GROUP BY IFNULL(ext.DEPARTMENT_ID, 0)
    HAVING SUM(CD.AMOUNT) > 0
    ON DUPLICATE KEY UPDATE AMOUNT = AMOUNT + VALUES(AMOUNT);

    IF p_RULE_CODE IN ('154_TO_155', '154_TO_155_TO_632') THEN
        INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
        SELECT
            '154_TO_155',
            v_acc_155,
            v_acc_154,
            AMOUNT,
            DEPARTMENT_ID
        FROM tmp_154_dept
        WHERE AMOUNT > 0;
    END IF;

    IF p_RULE_CODE = '154_TO_632' THEN
        INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
        SELECT
            '154_TO_632',
            v_acc_632,
            v_acc_154,
            AMOUNT,
            DEPARTMENT_ID
        FROM tmp_154_dept
        WHERE AMOUNT > 0;
    END IF;

    IF p_RULE_CODE = '154_TO_155_TO_632' THEN
        INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
        SELECT
            '155_TO_632',
            v_acc_632,
            v_acc_155,
            AMOUNT,
            DEPARTMENT_ID
        FROM tmp_154_dept
        WHERE AMOUNT > 0;
    END IF;
END proc$$

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_validate`$$
CREATE PROCEDURE `sp_period_lock_cogs_validate`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_RULE_CODE VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    CALL sp_period_lock_step_require_done(
        p_COMPANY_CD,
        p_PERIOD_YM,
        'FA_PREPAID_LOCK',
        'Khóa TSCĐ / CP trả trước'
    );

    CALL sp_period_lock_step_require_open(
        p_COMPANY_CD,
        p_PERIOD_YM,
        'COGS_SUMMARY'
    );

    IF p_RULE_CODE NOT IN ('154_TO_155', '154_TO_155_TO_632', '154_TO_632', 'EXP_TO_154_ONLY') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Rule ket chuyen gia von khong hop le';
    END IF;

    -- Preview/validate: CHỈ kiểm tra exclusive — không claim
    CALL sp_pc_engine_assert_for_cogs(p_COMPANY_CD, p_PERIOD_YM, p_RULE_CODE, p_USER_ID);
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_calculate`$$
CREATE PROCEDURE `sp_period_lock_cogs_calculate`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_RULE_CODE VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    -- Preview: validate (check) + build temp — không claim, không ghi CT
    CALL sp_period_lock_cogs_validate(p_COMPANY_CD, p_PERIOD_YM, p_RULE_CODE, p_USER_ID);
    CALL sp_period_lock_cogs_build_xfer(p_COMPANY_CD, p_PERIOD_YM, p_RULE_CODE);
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_rollback_period`$$
CREATE PROCEDURE `sp_period_lock_cogs_rollback_period`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6)
)
BEGIN
    CALL sp_period_lock_delete_lock_vouchers(
        p_COMPANY_CD,
        p_PERIOD_YM,
        'COGS_SUMMARY'
    );
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_assert_can_overwrite`$$
CREATE PROCEDURE `sp_period_lock_cogs_assert_can_overwrite`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_RULE_CODE VARCHAR(50)
)
proc: BEGIN
    DECLARE v_pl_status VARCHAR(20) DEFAULT NULL;
    DECLARE v_downstream INT DEFAULT 0;
    DECLARE v_tbl INT DEFAULT 0;

    -- Bước sau (PL) đã DONE → không cho ghi đè COGS
    SELECT STATUS
      INTO v_pl_status
    FROM acc_period_lock_step
    WHERE COMPANY_CD = p_COMPANY_CD
      AND PERIOD_YM = p_PERIOD_YM
      AND STEP_CODE = 'PROFIT_LOSS_REPORT'
    LIMIT 1;

    IF IFNULL(v_pl_status, '') = 'DONE' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Khong ghi de COGS: PROFIT_LOSS_REPORT da DONE';
    END IF;

    SELECT COUNT(*) INTO v_tbl
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_voucher_ref';

    IF IFNULL(v_tbl, 0) = 0 THEN
        LEAVE proc;
    END IF;

    -- Downstream G-K (COST_POST / VALUATION) đã gắn CT → không xóa EXP khi chưa có bản thay thế API
    SELECT COUNT(*)
      INTO v_downstream
    FROM production_cost_voucher_ref
    WHERE COMPANY_CD = p_COMPANY_CD
      AND ISDEL = '0'
      AND REF_ROLE IN ('COST_POST', 'VALUATION_COGS', 'VALUATION')
      AND (
            STABLE_KEY LIKE CONCAT('%:', p_COMPANY_CD, ':', p_PERIOD_YM)
         OR STABLE_KEY LIKE CONCAT('%:', p_PERIOD_YM)
      );

    IF IFNULL(v_downstream, 0) > 0 AND p_RULE_CODE = 'EXP_TO_154_ONLY' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Khong ghi de EXP_TO_154: CT buoc sau (COST_POST/VALUATION) dang dung';
    END IF;
END proc$$

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_create_voucher`$$
CREATE PROCEDURE `sp_period_lock_cogs_create_voucher`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_RULE_CODE VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
proc: BEGIN
    DECLARE v_description VARCHAR(500);
    DECLARE v_line_cnt INT DEFAULT 0;
    DECLARE v_fail_point VARCHAR(30) DEFAULT NULL;

    -- Không START/COMMIT ở đây: transaction owner = caller (CreateSessionAsync).
    -- Không xóa CT trong handler khi lỗi — để caller ROLLBACK khôi phục CT cũ.
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        RESIGNAL;
    END;

    SET v_fail_point = NULLIF(TRIM(IFNULL(@GK_2B_FAIL_POINT, '')), '');
    SET v_description = CONCAT(
        'Tự động kết chuyển giá vốn - ',
        p_RULE_CODE,
        ' - ',
        SUBSTRING(p_PERIOD_YM, 5, 2),
        '/',
        SUBSTRING(p_PERIOD_YM, 1, 4)
    );

    -- 1) Kiểm tra (không claim, không xóa)
    CALL sp_period_lock_cogs_validate(p_COMPANY_CD, p_PERIOD_YM, p_RULE_CODE, p_USER_ID);

    -- 2) Chiếm quyền chính thức
    CALL sp_pc_engine_claim_for_cogs(p_COMPANY_CD, p_PERIOD_YM, p_RULE_CODE, p_USER_ID);

    -- 3) Build bản thay thế vào temp (CT cũ vẫn nguyên)
    CALL sp_period_lock_cogs_build_xfer(p_COMPANY_CD, p_PERIOD_YM, p_RULE_CODE);

    SELECT IFNULL(COUNT(*), 0)
      INTO v_line_cnt
    FROM tmp_xfer_lines
    WHERE AMOUNT > 0;

    -- Không có bản thay thế hợp lệ → giữ nguyên CT cũ (không xóa)
    IF IFNULL(v_line_cnt, 0) = 0 THEN
        LEAVE proc;
    END IF;

    -- 4) Chặn ghi đè khi bước sau / downstream đang dùng
    CALL sp_period_lock_cogs_assert_can_overwrite(p_COMPANY_CD, p_PERIOD_YM, p_RULE_CODE);

    -- 5) Trong cùng TX caller: xóa CT cũ → post mới
    CALL sp_period_lock_cogs_rollback_period(p_COMPANY_CD, p_PERIOD_YM);

    -- Hook test DEV: SET @GK_2B_FAIL_POINT='AFTER_DELETE' trước CALL
    IF v_fail_point = 'AFTER_DELETE' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'GK2B TEST HOOK: FAIL AFTER_DELETE (expect TX rollback restore vouchers)';
    END IF;

    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'COGS_SUMMARY',
        'EXP_TO_154', v_description, p_USER_ID, 'CG1'
    );

    IF v_fail_point = 'DURING_POST' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'GK2B TEST HOOK: FAIL DURING_POST (expect TX rollback restore vouchers)';
    END IF;

    IF p_RULE_CODE IN ('154_TO_155', '154_TO_155_TO_632') THEN
        CALL sp_period_lock_post_xfer_group(
            p_COMPANY_CD, p_PERIOD_YM, 'COGS_SUMMARY',
            '154_TO_155', v_description, p_USER_ID, 'CG2'
        );
    END IF;

    IF p_RULE_CODE = '154_TO_632' THEN
        CALL sp_period_lock_post_xfer_group(
            p_COMPANY_CD, p_PERIOD_YM, 'COGS_SUMMARY',
            '154_TO_632', v_description, p_USER_ID, 'CG3'
        );
    END IF;

    IF p_RULE_CODE = '154_TO_155_TO_632' THEN
        CALL sp_period_lock_post_xfer_group(
            p_COMPANY_CD, p_PERIOD_YM, 'COGS_SUMMARY',
            '155_TO_632', v_description, p_USER_ID, 'CG4'
        );
    END IF;
END proc$$

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_unlock`$$
CREATE PROCEDURE `sp_period_lock_cogs_unlock`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_REASON VARCHAR(500),
    IN p_USER_ID VARCHAR(50),
    IN p_EXPECTED_ENGINE VARCHAR(10)
)
BEGIN
    DECLARE v_engine VARCHAR(10) DEFAULT 'NONE';
    DECLARE v_pl_status VARCHAR(20) DEFAULT NULL;
    DECLARE v_tbl INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        RESIGNAL;
    END;

    -- Không START/COMMIT: để caller (API) làm transaction owner nếu cần

    IF IFNULL(p_EXPECTED_ENGINE, '') NOT IN ('GK', 'BI') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Unlock COGS can p_EXPECTED_ENGINE = GK|BI';
    END IF;

    -- Phụ thuộc: không mở COGS khi PL đã DONE
    SELECT STATUS
      INTO v_pl_status
    FROM acc_period_lock_step
    WHERE COMPANY_CD = p_COMPANY_CD
      AND PERIOD_YM = p_PERIOD_YM
      AND STEP_CODE = 'PROFIT_LOSS_REPORT'
    LIMIT 1;

    IF IFNULL(v_pl_status, '') = 'DONE' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Khong mo COGS: PROFIT_LOSS_REPORT dang DONE';
    END IF;

    SELECT COUNT(*) INTO v_tbl
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_period';

    IF IFNULL(v_tbl, 0) > 0 THEN
        SELECT IFNULL(COST_ENGINE, 'NONE')
          INTO v_engine
        FROM production_cost_period
        WHERE COMPANY_CD = p_COMPANY_CD
          AND PERIOD_YM = p_PERIOD_YM
          AND ISDEL = '0'
        LIMIT 1;
        SET v_engine = IFNULL(v_engine, 'NONE');

        IF v_engine <> 'NONE' AND v_engine <> p_EXPECTED_ENGINE THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Unlock bi tu choi: engine khac (BI khong mo GK va nguoc lai)';
        END IF;
    END IF;

    CALL sp_period_lock_delete_lock_vouchers(
        p_COMPANY_CD,
        p_PERIOD_YM,
        'COGS_SUMMARY'
    );

    CALL sp_period_lock_step_upsert(
        p_COMPANY_CD,
        p_PERIOD_YM,
        'COGS_SUMMARY',
        'Báo cáo về tổng hợp giá vốn',
        2,
        'OPEN',
        'Đã mở',
        p_USER_ID
    );

    IF IFNULL(v_tbl, 0) > 0 AND IFNULL(v_engine, 'NONE') <> 'NONE' THEN
        CALL sp_pc_engine_release_cogs(p_COMPANY_CD, p_PERIOD_YM, p_USER_ID, p_EXPECTED_ENGINE);
    END IF;
END$$

DELIMITER ;
