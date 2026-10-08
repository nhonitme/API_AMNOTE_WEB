DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_period_lock_pl_insert_debit_xfer`$$
CREATE PROCEDURE `sp_period_lock_pl_insert_debit_xfer`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_FROM_YMD CHAR(8),
    IN p_TO_YMD CHAR(8),
    IN p_PREFIX VARCHAR(20),
    IN p_TRANSFER_CODE VARCHAR(30),
    IN p_DEBIT_ACC VARCHAR(20),
    IN p_CREDIT_ACC VARCHAR(20)
)
BEGIN
    IF p_CREDIT_ACC IS NULL OR p_CREDIT_ACC = '' THEN
        INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
        SELECT
            p_TRANSFER_CODE,
            p_DEBIT_ACC,
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
          AND C.CHIT_YMD >= p_FROM_YMD
          AND C.CHIT_YMD <= p_TO_YMD
          AND TRIM(CD.DEBIT) LIKE CONCAT(p_PREFIX, '%')
          AND IFNULL(TRIM(CD.CREDIT), '') <> ''
        GROUP BY TRIM(CD.DEBIT), IFNULL(ext.DEPARTMENT_ID, 0)
        HAVING SUM(CD.AMOUNT) > 0;
    ELSE
        INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
        SELECT
            p_TRANSFER_CODE,
            p_DEBIT_ACC,
            p_CREDIT_ACC,
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
          AND C.CHIT_YMD >= p_FROM_YMD
          AND C.CHIT_YMD <= p_TO_YMD
          AND TRIM(CD.DEBIT) LIKE CONCAT(p_PREFIX, '%')
          AND IFNULL(TRIM(CD.CREDIT), '') <> ''
        GROUP BY IFNULL(ext.DEPARTMENT_ID, 0)
        HAVING SUM(CD.AMOUNT) > 0;
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_pl_insert_credit_xfer`$$
CREATE PROCEDURE `sp_period_lock_pl_insert_credit_xfer`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_FROM_YMD CHAR(8),
    IN p_TO_YMD CHAR(8),
    IN p_PREFIX VARCHAR(20),
    IN p_TRANSFER_CODE VARCHAR(30),
    IN p_DEBIT_ACC VARCHAR(20),
    IN p_CREDIT_ACC VARCHAR(20)
)
BEGIN
    IF p_DEBIT_ACC IS NULL OR p_DEBIT_ACC = '' THEN
        INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
        SELECT
            p_TRANSFER_CODE,
            TRIM(CD.CREDIT),
            p_CREDIT_ACC,
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
           AND A.ACC_CD = TRIM(CD.CREDIT)
           AND IFNULL(A.ISDEL, '0') = '0'
           AND IFNULL(A.ISABLEINPUT, '0') = '1'
        WHERE C.COMPANY_CD = p_COMPANY_CD
          AND C.ISDEL = '0'
          AND C.CHIT_YMD >= p_FROM_YMD
          AND C.CHIT_YMD <= p_TO_YMD
          AND TRIM(CD.CREDIT) LIKE CONCAT(p_PREFIX, '%')
          AND IFNULL(TRIM(CD.DEBIT), '') <> ''
        GROUP BY TRIM(CD.CREDIT), IFNULL(ext.DEPARTMENT_ID, 0)
        HAVING SUM(CD.AMOUNT) > 0;
    ELSE
        INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
        SELECT
            p_TRANSFER_CODE,
            p_DEBIT_ACC,
            p_CREDIT_ACC,
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
           AND A.ACC_CD = TRIM(CD.CREDIT)
           AND IFNULL(A.ISDEL, '0') = '0'
           AND IFNULL(A.ISABLEINPUT, '0') = '1'
        WHERE C.COMPANY_CD = p_COMPANY_CD
          AND C.ISDEL = '0'
          AND C.CHIT_YMD >= p_FROM_YMD
          AND C.CHIT_YMD <= p_TO_YMD
          AND TRIM(CD.CREDIT) LIKE CONCAT(p_PREFIX, '%')
          AND IFNULL(TRIM(CD.DEBIT), '') <> ''
        GROUP BY IFNULL(ext.DEPARTMENT_ID, 0)
        HAVING SUM(CD.AMOUNT) > 0;
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_pl_build_xfer`$$
CREATE PROCEDURE `sp_period_lock_pl_build_xfer`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6)
)
proc: BEGIN
    DECLARE v_from_ymd CHAR(8);
    DECLARE v_to_ymd CHAR(8);
    DECLARE v_acc_511 VARCHAR(20);
    DECLARE v_acc_911 VARCHAR(20);
    DECLARE v_acc_4212 VARCHAR(20);
    DECLARE v_acc_8212 VARCHAR(20);
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

    DROP TEMPORARY TABLE IF EXISTS tmp_911_dept;
    CREATE TEMPORARY TABLE tmp_911_dept
    (
        DEPARTMENT_ID BIGINT NOT NULL DEFAULT 0,
        NET_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        PRIMARY KEY (DEPARTMENT_ID)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    SELECT COUNT(*)
      INTO v_has_source
    FROM chitinfo C
    INNER JOIN chitdetailinfo CD
        ON CD.COMPANY_CD = C.COMPANY_CD
       AND CD.CHIT_ID = C.CHIT_ID
       AND CD.ISDEL = '0'
    LEFT JOIN acclist_info A_DEBIT
        ON A_DEBIT.COMPANY_CD = CD.COMPANY_CD
       AND A_DEBIT.ACC_CD = TRIM(CD.DEBIT)
       AND IFNULL(A_DEBIT.ISDEL, '0') = '0'
       AND IFNULL(A_DEBIT.ISABLEINPUT, '0') = '1'
    LEFT JOIN acclist_info A_CREDIT
        ON A_CREDIT.COMPANY_CD = CD.COMPANY_CD
       AND A_CREDIT.ACC_CD = TRIM(CD.CREDIT)
       AND IFNULL(A_CREDIT.ISDEL, '0') = '0'
       AND IFNULL(A_CREDIT.ISABLEINPUT, '0') = '1'
    WHERE C.COMPANY_CD = p_COMPANY_CD
      AND C.ISDEL = '0'
      AND C.CHIT_YMD >= v_from_ymd
      AND C.CHIT_YMD <= v_to_ymd
      AND (
            (
                IFNULL(TRIM(CD.DEBIT), '') <> ''
            AND IFNULL(TRIM(CD.CREDIT), '') <> ''
            AND A_DEBIT.ACC_CD IS NOT NULL
            AND (
                    TRIM(CD.DEBIT) LIKE '521%'
                 OR TRIM(CD.DEBIT) LIKE '632%'
                 OR TRIM(CD.DEBIT) LIKE '635%'
                 OR TRIM(CD.DEBIT) LIKE '641%'
                 OR TRIM(CD.DEBIT) LIKE '642%'
                 OR TRIM(CD.DEBIT) LIKE '811%'
                 OR TRIM(CD.DEBIT) LIKE '8211%'
                 OR TRIM(CD.DEBIT) LIKE '8212%'
                 OR TRIM(CD.DEBIT) LIKE '911%'
            )
        )
         OR (
                IFNULL(TRIM(CD.CREDIT), '') <> ''
            AND IFNULL(TRIM(CD.DEBIT), '') <> ''
            AND A_CREDIT.ACC_CD IS NOT NULL
            AND (
                    TRIM(CD.CREDIT) LIKE '511%'
                 OR TRIM(CD.CREDIT) LIKE '515%'
                 OR TRIM(CD.CREDIT) LIKE '711%'
                 OR TRIM(CD.CREDIT) LIKE '8212%'
                 OR TRIM(CD.CREDIT) LIKE '911%'
            )
        )
      );

    IF IFNULL(v_has_source, 0) = 0 THEN
        LEAVE proc;
    END IF;

    CALL sp_period_lock_resolve_posting_acc(p_COMPANY_CD, '511', v_acc_511);
    CALL sp_period_lock_resolve_posting_acc(p_COMPANY_CD, '911', v_acc_911);
    CALL sp_period_lock_resolve_posting_acc(p_COMPANY_CD, '4212', v_acc_4212);
    CALL sp_period_lock_resolve_posting_acc(p_COMPANY_CD, '8212', v_acc_8212);

    CALL sp_period_lock_pl_insert_debit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '521', '521_TO_511', v_acc_511, NULL
    );
    CALL sp_period_lock_pl_insert_credit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '511', '511_TO_911', NULL, v_acc_911
    );
    CALL sp_period_lock_pl_insert_credit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '515', '515_TO_911', NULL, v_acc_911
    );
    CALL sp_period_lock_pl_insert_debit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '632', '632_TO_911', v_acc_911, NULL
    );
    CALL sp_period_lock_pl_insert_debit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '635', '635_TO_911', v_acc_911, NULL
    );
    CALL sp_period_lock_pl_insert_debit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '641', '641_TO_911', v_acc_911, NULL
    );
    CALL sp_period_lock_pl_insert_debit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '642', '642_TO_911', v_acc_911, NULL
    );
    CALL sp_period_lock_pl_insert_credit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '711', '711_TO_911', NULL, v_acc_911
    );
    CALL sp_period_lock_pl_insert_debit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '811', '811_TO_911', v_acc_911, NULL
    );
    CALL sp_period_lock_pl_insert_debit_xfer(
        p_COMPANY_CD, v_from_ymd, v_to_ymd, '8211', '8211_TO_911', v_acc_911, NULL
    );

    DROP TEMPORARY TABLE IF EXISTS tmp_8212_dept;
    CREATE TEMPORARY TABLE tmp_8212_dept
    (
        DEPARTMENT_ID BIGINT NOT NULL DEFAULT 0,
        NET_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        PRIMARY KEY (DEPARTMENT_ID)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_8212_dept (DEPARTMENT_ID, NET_AMT)
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
      AND TRIM(CD.DEBIT) LIKE '8212%'
      AND IFNULL(TRIM(CD.CREDIT), '') <> ''
    GROUP BY IFNULL(ext.DEPARTMENT_ID, 0)
    HAVING SUM(CD.AMOUNT) <> 0;

    INSERT INTO tmp_8212_dept (DEPARTMENT_ID, NET_AMT)
    SELECT
        IFNULL(ext.DEPARTMENT_ID, 0),
        -SUM(CD.AMOUNT)
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
       AND A.ACC_CD = TRIM(CD.CREDIT)
       AND IFNULL(A.ISDEL, '0') = '0'
       AND IFNULL(A.ISABLEINPUT, '0') = '1'
    WHERE C.COMPANY_CD = p_COMPANY_CD
      AND C.ISDEL = '0'
      AND C.CHIT_YMD >= v_from_ymd
      AND C.CHIT_YMD <= v_to_ymd
      AND TRIM(CD.CREDIT) LIKE '8212%'
      AND IFNULL(TRIM(CD.DEBIT), '') <> ''
    GROUP BY IFNULL(ext.DEPARTMENT_ID, 0)
    HAVING SUM(CD.AMOUNT) <> 0
    ON DUPLICATE KEY UPDATE NET_AMT = NET_AMT + VALUES(NET_AMT);

    INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
    SELECT
        '8212_NET',
        v_acc_911,
        v_acc_8212,
        NET_AMT,
        DEPARTMENT_ID
    FROM tmp_8212_dept
    WHERE NET_AMT > 0;

    INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
    SELECT
        '8212_NET',
        v_acc_8212,
        v_acc_911,
        ABS(NET_AMT),
        DEPARTMENT_ID
    FROM tmp_8212_dept
    WHERE NET_AMT < 0;

    INSERT INTO tmp_911_dept (DEPARTMENT_ID, NET_AMT)
    SELECT
        IFNULL(ext.DEPARTMENT_ID, 0),
        SUM(
            CASE
                WHEN TRIM(CD.DEBIT) LIKE '911%' THEN CD.AMOUNT
                ELSE 0
            END
        ) - SUM(
            CASE
                WHEN TRIM(CD.CREDIT) LIKE '911%' THEN CD.AMOUNT
                ELSE 0
            END
        )
    FROM chitinfo C
    INNER JOIN chitdetailinfo CD
        ON CD.COMPANY_CD = C.COMPANY_CD
       AND CD.CHIT_ID = C.CHIT_ID
       AND CD.ISDEL = '0'
    LEFT JOIN chitdetailinfo_ext ext
        ON ext.COMPANY_CD = CD.COMPANY_CD
       AND ext.CHITDETAIL_ID = CD.CHITDETAIL_ID
       AND ext.ISDEL = '0'
    WHERE C.COMPANY_CD = p_COMPANY_CD
      AND C.ISDEL = '0'
      AND C.CHIT_YMD >= v_from_ymd
      AND C.CHIT_YMD <= v_to_ymd
      AND (
            TRIM(CD.DEBIT) LIKE '911%'
         OR TRIM(CD.CREDIT) LIKE '911%'
      )
    GROUP BY IFNULL(ext.DEPARTMENT_ID, 0)
    HAVING SUM(
            CASE
                WHEN TRIM(CD.DEBIT) LIKE '911%' THEN CD.AMOUNT
                ELSE 0
            END
        ) - SUM(
            CASE
                WHEN TRIM(CD.CREDIT) LIKE '911%' THEN CD.AMOUNT
                ELSE 0
            END
        ) <> 0
    ON DUPLICATE KEY UPDATE NET_AMT = NET_AMT + VALUES(NET_AMT);

    INSERT INTO tmp_911_dept (DEPARTMENT_ID, NET_AMT)
    SELECT
        DEPARTMENT_ID,
        SUM(
            CASE
                WHEN DEBIT_ACC = v_acc_911 THEN AMOUNT
                WHEN CREDIT_ACC = v_acc_911 THEN -AMOUNT
                ELSE 0
            END
        )
    FROM tmp_xfer_lines
    WHERE TRANSFER_CODE <> '911_TO_4212'
    GROUP BY DEPARTMENT_ID
    HAVING SUM(
        CASE
            WHEN DEBIT_ACC = v_acc_911 THEN AMOUNT
            WHEN CREDIT_ACC = v_acc_911 THEN -AMOUNT
            ELSE 0
        END
    ) <> 0
    ON DUPLICATE KEY UPDATE NET_AMT = NET_AMT + VALUES(NET_AMT);

    INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
    SELECT
        '911_TO_4212',
        v_acc_911,
        v_acc_4212,
        NET_AMT,
        DEPARTMENT_ID
    FROM tmp_911_dept
    WHERE NET_AMT > 0;

    INSERT INTO tmp_xfer_lines (TRANSFER_CODE, DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID)
    SELECT
        '911_TO_4212',
        v_acc_4212,
        v_acc_911,
        ABS(NET_AMT),
        DEPARTMENT_ID
    FROM tmp_911_dept
    WHERE NET_AMT < 0;
END proc$$

DROP PROCEDURE IF EXISTS `sp_period_lock_pl_validate`$$
CREATE PROCEDURE `sp_period_lock_pl_validate`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_BALANCE_METHOD VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    CALL sp_period_lock_step_require_done(p_COMPANY_CD, p_PERIOD_YM, 'FA_PREPAID_LOCK', 'FA');
    CALL sp_period_lock_step_require_done(p_COMPANY_CD, p_PERIOD_YM, 'COGS_SUMMARY', 'COGS');
    CALL sp_period_lock_step_require_open(p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT');

    IF p_BALANCE_METHOD NOT IN ('BALANCE_ACCOUNT', 'BALANCE_ACCOUNT_TWO_SIDE') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Phuong phap bao cao lai lo khong hop le';
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_pl_calculate`$$
CREATE PROCEDURE `sp_period_lock_pl_calculate`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_BALANCE_METHOD VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    CALL sp_period_lock_pl_validate(p_COMPANY_CD, p_PERIOD_YM, p_BALANCE_METHOD, p_USER_ID);
    CALL sp_period_lock_pl_build_xfer(p_COMPANY_CD, p_PERIOD_YM);
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_pl_rollback_period`$$
CREATE PROCEDURE `sp_period_lock_pl_rollback_period`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6)
)
BEGIN
    CALL sp_period_lock_delete_lock_vouchers(
        p_COMPANY_CD,
        p_PERIOD_YM,
        'PROFIT_LOSS_REPORT'
    );
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_pl_create_voucher`$$
CREATE PROCEDURE `sp_period_lock_pl_create_voucher`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_BALANCE_METHOD VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    DECLARE v_description VARCHAR(500);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        CALL sp_period_lock_pl_rollback_period(p_COMPANY_CD, p_PERIOD_YM);
        RESIGNAL;
    END;

    SET v_description = CONCAT(
        'Tự động kết chuyển lãi lỗ - ',
        SUBSTRING(p_PERIOD_YM, 5, 2),
        '/',
        SUBSTRING(p_PERIOD_YM, 1, 4)
    );

    CALL sp_period_lock_pl_validate(p_COMPANY_CD, p_PERIOD_YM, p_BALANCE_METHOD, p_USER_ID);
    CALL sp_period_lock_pl_build_xfer(p_COMPANY_CD, p_PERIOD_YM);

    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '521_TO_511', v_description, p_USER_ID, 'PL1'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '511_TO_911', v_description, p_USER_ID, 'PL2'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '515_TO_911', v_description, p_USER_ID, 'PL3'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '632_TO_911', v_description, p_USER_ID, 'PL4'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '635_TO_911', v_description, p_USER_ID, 'PL5'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '641_TO_911', v_description, p_USER_ID, 'PL6'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '642_TO_911', v_description, p_USER_ID, 'PL7'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '711_TO_911', v_description, p_USER_ID, 'PL8'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '811_TO_911', v_description, p_USER_ID, 'PL9'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '8211_TO_911', v_description, p_USER_ID, 'PLA'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '8212_NET', v_description, p_USER_ID, 'PLB'
    );
    CALL sp_period_lock_post_xfer_group(
        p_COMPANY_CD, p_PERIOD_YM, 'PROFIT_LOSS_REPORT', '911_TO_4212', v_description, p_USER_ID, 'PLC'
    );

    CALL sp_period_lock_closingmonth_snapshot(
        p_COMPANY_CD,
        p_PERIOD_YM,
        p_BALANCE_METHOD,
        p_USER_ID
    );
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_pl_unlock`$$
CREATE PROCEDURE `sp_period_lock_pl_unlock`(
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

    CALL sp_period_lock_closingmonth_delete(p_COMPANY_CD, p_PERIOD_YM);

    CALL sp_period_lock_delete_lock_vouchers(
        p_COMPANY_CD,
        p_PERIOD_YM,
        'PROFIT_LOSS_REPORT'
    );

    CALL sp_period_lock_step_upsert(
        p_COMPANY_CD,
        p_PERIOD_YM,
        'PROFIT_LOSS_REPORT',
        'Báo cáo lãi lỗ',
        3,
        'OPEN',
        'Đã mở',
        p_USER_ID
    );

    COMMIT;
END$$

DELIMITER ;
