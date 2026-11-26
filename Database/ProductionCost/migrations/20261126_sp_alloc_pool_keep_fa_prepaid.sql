-- =============================================================================
-- G-K — sp_pc_alloc_load_pool: giữ CT FA_PREPAID_LOCK trong pool 622/627/6212
-- BÀN GIAO — user tự SOURCE trên DB đã có 2C-02 alloc.
--
-- Trước đây loại mọi INPUT_TYPE='LOCK' → bỏ luôn khấu hao TSCĐ Nợ 622/627.
-- Giờ: giữ nghiệp vụ + FA_PREPAID_LOCK; vẫn loại COGS_SUMMARY / PROFIT_LOSS_REPORT.
-- Transaction: KHÔNG START/COMMIT — caller C# sở hữu TX.
-- =============================================================================

DELIMITER $$

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
      -- Giữ PS nghiệp vụ + khấu hao TSCĐ (FA_PREPAID_LOCK); loại CT khóa kết chuyển B3/B6
      AND (
            IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
         OR IFNULL(c.LOCK_STEP_CODE, '') = 'FA_PREPAID_LOCK'
      )
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
      -- Giữ PS nghiệp vụ + khấu hao TSCĐ (FA_PREPAID_LOCK); loại CT khóa kết chuyển B3/B6
      AND (
            IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
         OR IFNULL(c.LOCK_STEP_CODE, '') = 'FA_PREPAID_LOCK'
      )
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

DELIMITER ;

SELECT 'sp_pc_alloc_load_pool — keep FA_PREPAID_LOCK in 622/627/6212 pool' AS NOTE;
