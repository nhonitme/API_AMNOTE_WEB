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

    IF p_RULE_CODE NOT IN ('154_TO_155', '154_TO_155_TO_632', '154_TO_632') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Rule kết chuyển giá vốn không hợp lệ';
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_calculate`$$
CREATE PROCEDURE `sp_period_lock_cogs_calculate`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_RULE_CODE VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
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

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_create_voucher`$$
CREATE PROCEDURE `sp_period_lock_cogs_create_voucher`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_RULE_CODE VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    DECLARE v_description VARCHAR(500);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        CALL sp_period_lock_cogs_rollback_period(p_COMPANY_CD, p_PERIOD_YM);
        RESIGNAL;
    END;

    SET v_description = CONCAT(
        'Tự động kết chuyển giá vốn - ',
        p_RULE_CODE,
        ' - ',
        SUBSTRING(p_PERIOD_YM, 5, 2),
        '/',
        SUBSTRING(p_PERIOD_YM, 1, 4)
    );

    CALL sp_period_lock_cogs_validate(p_COMPANY_CD, p_PERIOD_YM, p_RULE_CODE, p_USER_ID);
    CALL sp_period_lock_cogs_build_xfer(p_COMPANY_CD, p_PERIOD_YM, p_RULE_CODE);

    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'COGS_SUMMARY',
        'EXP_TO_154', v_description, p_USER_ID, 'CG1'
    );

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
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_cogs_unlock`$$
CREATE PROCEDURE `sp_period_lock_cogs_unlock`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_REASON VARCHAR(500),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

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

    COMMIT;
END$$

DELIMITER ;
