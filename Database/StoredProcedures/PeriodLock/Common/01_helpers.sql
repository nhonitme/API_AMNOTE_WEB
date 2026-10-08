-- Shared helpers for Period Lock (MySQL 5.6+)

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_period_lock_delete_lock_vouchers`$$
CREATE PROCEDURE `sp_period_lock_delete_lock_vouchers`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_LOCK_STEP_CODE VARCHAR(50)
)
BEGIN
    DECLARE v_last_day DATE;
    DECLARE v_chit_ymd CHAR(8);

    CALL sp_period_lock_fa_prepaid_last_day(p_PERIOD_YM, v_last_day);
    SET v_chit_ymd = DATE_FORMAT(v_last_day, '%Y%m%d');

    DELETE dd
    FROM chitdetaildescriptioninfo dd
    INNER JOIN chitdetailinfo d
        ON d.CHITDETAIL_ID = dd.CHITDETAIL_ID
       AND d.COMPANY_CD = dd.COMPANY_CD
    INNER JOIN chitinfo h
        ON h.CHIT_ID = d.CHIT_ID
       AND h.COMPANY_CD = d.COMPANY_CD
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = p_LOCK_STEP_CODE
      AND h.CHIT_YMD = v_chit_ymd;

    DELETE ext
    FROM chitdetailinfo_ext ext
    INNER JOIN chitdetailinfo d
        ON d.CHITDETAIL_ID = ext.CHITDETAIL_ID
       AND d.COMPANY_CD = ext.COMPANY_CD
    INNER JOIN chitinfo h
        ON h.CHIT_ID = d.CHIT_ID
       AND h.COMPANY_CD = d.COMPANY_CD
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = p_LOCK_STEP_CODE
      AND h.CHIT_YMD = v_chit_ymd;

    DELETE d
    FROM chitdetailinfo d
    INNER JOIN chitinfo h
        ON h.CHIT_ID = d.CHIT_ID
       AND h.COMPANY_CD = d.COMPANY_CD
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = p_LOCK_STEP_CODE
      AND h.CHIT_YMD = v_chit_ymd;

    DELETE desc_h
    FROM chitdescriptioninfo desc_h
    INNER JOIN chitinfo h
        ON h.CHIT_ID = desc_h.CHIT_ID
       AND h.COMPANY_CD = desc_h.COMPANY_CD
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = p_LOCK_STEP_CODE
      AND h.CHIT_YMD = v_chit_ymd;

    DELETE ext_h
    FROM chitinfo_ext ext_h
    INNER JOIN chitinfo h
        ON h.CHIT_ID = ext_h.CHIT_ID
       AND h.COMPANY_CD = ext_h.COMPANY_CD
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = p_LOCK_STEP_CODE
      AND h.CHIT_YMD = v_chit_ymd;

    DELETE h
    FROM chitinfo h
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = p_LOCK_STEP_CODE
      AND h.CHIT_YMD = v_chit_ymd;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_closingmonth_delete`$$
CREATE PROCEDURE `sp_period_lock_closingmonth_delete`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6)
)
BEGIN
    DECLARE v_last_day DATE;
    DECLARE v_close_ymd CHAR(8);

    CALL sp_period_lock_fa_prepaid_last_day(p_PERIOD_YM, v_last_day);
    SET v_close_ymd = DATE_FORMAT(v_last_day, '%Y%m%d');

    DELETE
    FROM closingmonth_detail
    WHERE COMPANY_CD = p_COMPANY_CD
      AND CLOSE_YMD = v_close_ymd;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_step_require_done`$$
CREATE PROCEDURE `sp_period_lock_step_require_done`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_STEP_CODE VARCHAR(50),
    IN p_STEP_LABEL VARCHAR(100)
)
BEGIN
    DECLARE v_status VARCHAR(20) DEFAULT NULL;

    SELECT STATUS
      INTO v_status
    FROM acc_period_lock_step
    WHERE COMPANY_CD = p_COMPANY_CD
      AND PERIOD_YM = p_PERIOD_YM
      AND STEP_CODE = p_STEP_CODE
    LIMIT 1;

    IF IFNULL(v_status, '') <> 'DONE' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Buoc khóa sổ trước chưa hoàn tất';
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_step_require_open`$$
CREATE PROCEDURE `sp_period_lock_step_require_open`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_STEP_CODE VARCHAR(50)
)
BEGIN
    DECLARE v_status VARCHAR(20) DEFAULT NULL;

    SELECT STATUS
      INTO v_status
    FROM acc_period_lock_step
    WHERE COMPANY_CD = p_COMPANY_CD
      AND PERIOD_YM = p_PERIOD_YM
      AND STEP_CODE = p_STEP_CODE
    LIMIT 1;

    IF IFNULL(v_status, '') = 'DONE' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Buoc khóa sổ đã hoàn tất trước đó';
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_closingmonth_snapshot`$$
CREATE PROCEDURE `sp_period_lock_closingmonth_snapshot`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_BALANCE_METHOD VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    DECLARE v_from_ymd CHAR(8);
    DECLARE v_to_ymd CHAR(8);
    DECLARE v_prev_from_ymd CHAR(8);
    DECLARE v_prev_close_ymd CHAR(8) DEFAULT NULL;
    DECLARE v_open_ymd CHAR(8) DEFAULT NULL;
    DECLARE v_begin_move_from_ymd CHAR(8) DEFAULT NULL;

    SET v_from_ymd = CONCAT(p_PERIOD_YM, '01');
    SET v_to_ymd = DATE_FORMAT(LAST_DAY(STR_TO_DATE(CONCAT(p_PERIOD_YM, '01'), '%Y%m%d')), '%Y%m%d');
    SET v_prev_from_ymd = DATE_FORMAT(DATE_SUB(STR_TO_DATE(v_from_ymd, '%Y%m%d'), INTERVAL 1 DAY), '%Y%m%d');

    DELETE
    FROM closingmonth_detail
    WHERE COMPANY_CD = p_COMPANY_CD
      AND CLOSE_YMD = v_to_ymd;

    SELECT MAX(CLOSE_YMD)
      INTO v_prev_close_ymd
    FROM closingmonth_detail
    WHERE COMPANY_CD = p_COMPANY_CD
      AND ISDEL = '0'
      AND CLOSE_YMD < v_from_ymd;

    DROP TEMPORARY TABLE IF EXISTS tmp_cm_begin_base;
    CREATE TEMPORARY TABLE tmp_cm_begin_base
    (
        ACCOUNT_CD VARCHAR(20) NOT NULL DEFAULT '',
        BEGIN_DEBIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        BEGIN_CREDIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        PRIMARY KEY (ACCOUNT_CD)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    DROP TEMPORARY TABLE IF EXISTS tmp_cm_begin_move;
    CREATE TEMPORARY TABLE tmp_cm_begin_move
    (
        ACCOUNT_CD VARCHAR(20) NOT NULL DEFAULT '',
        PERIOD_DEBIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        PERIOD_CREDIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        PRIMARY KEY (ACCOUNT_CD)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    DROP TEMPORARY TABLE IF EXISTS tmp_cm_period_amount;
    CREATE TEMPORARY TABLE tmp_cm_period_amount
    (
        ACCOUNT_CD VARCHAR(20) NOT NULL DEFAULT '',
        PERIOD_DEBIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        PERIOD_CREDIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        PRIMARY KEY (ACCOUNT_CD)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    IF v_prev_close_ymd IS NOT NULL THEN
        INSERT INTO tmp_cm_begin_base (ACCOUNT_CD, BEGIN_DEBIT, BEGIN_CREDIT)
        SELECT
            ACC_CD,
            SUM(DEBIT),
            SUM(CREDIT)
        FROM closingmonth_detail
        WHERE COMPANY_CD = p_COMPANY_CD
          AND ISDEL = '0'
          AND CLOSE_YMD = v_prev_close_ymd
          AND IFNULL(ACC_CD, '') <> ''
        GROUP BY ACC_CD;

        SET v_begin_move_from_ymd = DATE_FORMAT(
            DATE_ADD(STR_TO_DATE(v_prev_close_ymd, '%Y%m%d'), INTERVAL 1 DAY),
            '%Y%m%d'
        );
    ELSE
        SELECT MAX(OPEN_YMD)
          INTO v_open_ymd
        FROM (
            SELECT MAX(OPEN_YMD) AS OPEN_YMD
            FROM before_states
            WHERE COMPANY_CD = p_COMPANY_CD
              AND ISDEL = '0'
              AND OPEN_YMD <= v_from_ymd

            UNION ALL

            SELECT MAX(OPEN_YMD) AS OPEN_YMD
            FROM before_states_bank
            WHERE COMPANY_CD = p_COMPANY_CD
              AND ISDEL = '0'
              AND OPEN_YMD <= v_from_ymd

            UNION ALL

            SELECT MAX(OPEN_YMD) AS OPEN_YMD
            FROM before_states_customer
            WHERE COMPANY_CD = p_COMPANY_CD
              AND ISDEL = '0'
              AND OPEN_YMD <= v_from_ymd

            UNION ALL

            SELECT MAX(OPEN_YMD) AS OPEN_YMD
            FROM before_states_department
            WHERE COMPANY_CD = p_COMPANY_CD
              AND ISDEL = '0'
              AND OPEN_YMD <= v_from_ymd
        ) T
        WHERE OPEN_YMD IS NOT NULL;

        IF v_open_ymd IS NOT NULL THEN
            INSERT INTO tmp_cm_begin_base (ACCOUNT_CD, BEGIN_DEBIT, BEGIN_CREDIT)
            SELECT
                X.ACC_CD,
                SUM(X.DEBIT),
                SUM(X.CREDIT)
            FROM (
                SELECT ACC_CD, DEBIT, CREDIT
                FROM before_states
                WHERE COMPANY_CD = p_COMPANY_CD
                  AND ISDEL = '0'
                  AND OPEN_YMD = v_open_ymd
                  AND IFNULL(ACC_CD, '') <> ''

                UNION ALL

                SELECT ACC_CD, DEBIT, CREDIT
                FROM before_states_bank
                WHERE COMPANY_CD = p_COMPANY_CD
                  AND ISDEL = '0'
                  AND OPEN_YMD = v_open_ymd
                  AND IFNULL(ACC_CD, '') <> ''

                UNION ALL

                SELECT ACC_CD, DEBIT, CREDIT
                FROM before_states_customer
                WHERE COMPANY_CD = p_COMPANY_CD
                  AND ISDEL = '0'
                  AND OPEN_YMD = v_open_ymd
                  AND IFNULL(ACC_CD, '') <> ''

                UNION ALL

                SELECT ACC_CD, DEBIT, CREDIT
                FROM before_states_department
                WHERE COMPANY_CD = p_COMPANY_CD
                  AND ISDEL = '0'
                  AND OPEN_YMD = v_open_ymd
                  AND IFNULL(ACC_CD, '') <> ''
            ) X
            GROUP BY X.ACC_CD;

            SET v_begin_move_from_ymd = v_open_ymd;
        ELSE
            SET v_begin_move_from_ymd = '00000000';
        END IF;
    END IF;

    IF v_begin_move_from_ymd IS NOT NULL
       AND v_begin_move_from_ymd <= v_prev_from_ymd THEN
        INSERT INTO tmp_cm_begin_move (ACCOUNT_CD, PERIOD_DEBIT, PERIOD_CREDIT)
        SELECT TRIM(CD.DEBIT), SUM(CD.AMOUNT), 0
        FROM chitinfo C
        INNER JOIN chitdetailinfo CD
            ON CD.COMPANY_CD = C.COMPANY_CD
           AND CD.CHIT_ID = C.CHIT_ID
           AND CD.ISDEL = '0'
        WHERE C.COMPANY_CD = p_COMPANY_CD
          AND C.ISDEL = '0'
          AND C.CHIT_YMD >= v_begin_move_from_ymd
          AND C.CHIT_YMD <= v_prev_from_ymd
          AND IFNULL(TRIM(CD.DEBIT), '') <> ''
        GROUP BY TRIM(CD.DEBIT)
        ON DUPLICATE KEY UPDATE PERIOD_DEBIT = PERIOD_DEBIT + VALUES(PERIOD_DEBIT);

        INSERT INTO tmp_cm_begin_move (ACCOUNT_CD, PERIOD_DEBIT, PERIOD_CREDIT)
        SELECT TRIM(CD.CREDIT), 0, SUM(CD.AMOUNT)
        FROM chitinfo C
        INNER JOIN chitdetailinfo CD
            ON CD.COMPANY_CD = C.COMPANY_CD
           AND CD.CHIT_ID = C.CHIT_ID
           AND CD.ISDEL = '0'
        WHERE C.COMPANY_CD = p_COMPANY_CD
          AND C.ISDEL = '0'
          AND C.CHIT_YMD >= v_begin_move_from_ymd
          AND C.CHIT_YMD <= v_prev_from_ymd
          AND IFNULL(TRIM(CD.CREDIT), '') <> ''
        GROUP BY TRIM(CD.CREDIT)
        ON DUPLICATE KEY UPDATE PERIOD_CREDIT = PERIOD_CREDIT + VALUES(PERIOD_CREDIT);
    END IF;

    INSERT INTO tmp_cm_period_amount (ACCOUNT_CD, PERIOD_DEBIT, PERIOD_CREDIT)
    SELECT TRIM(CD.DEBIT), SUM(CD.AMOUNT), 0
    FROM chitinfo C
    INNER JOIN chitdetailinfo CD
        ON CD.COMPANY_CD = C.COMPANY_CD
       AND CD.CHIT_ID = C.CHIT_ID
       AND CD.ISDEL = '0'
    WHERE C.COMPANY_CD = p_COMPANY_CD
      AND C.ISDEL = '0'
      AND C.CHIT_YMD >= v_from_ymd
      AND C.CHIT_YMD <= v_to_ymd
      AND IFNULL(TRIM(CD.DEBIT), '') <> ''
    GROUP BY TRIM(CD.DEBIT)
    ON DUPLICATE KEY UPDATE PERIOD_DEBIT = PERIOD_DEBIT + VALUES(PERIOD_DEBIT);

    INSERT INTO tmp_cm_period_amount (ACCOUNT_CD, PERIOD_DEBIT, PERIOD_CREDIT)
    SELECT TRIM(CD.CREDIT), 0, SUM(CD.AMOUNT)
    FROM chitinfo C
    INNER JOIN chitdetailinfo CD
        ON CD.COMPANY_CD = C.COMPANY_CD
       AND CD.CHIT_ID = C.CHIT_ID
       AND CD.ISDEL = '0'
    WHERE C.COMPANY_CD = p_COMPANY_CD
      AND C.ISDEL = '0'
      AND C.CHIT_YMD >= v_from_ymd
      AND C.CHIT_YMD <= v_to_ymd
      AND IFNULL(TRIM(CD.CREDIT), '') <> ''
    GROUP BY TRIM(CD.CREDIT)
    ON DUPLICATE KEY UPDATE PERIOD_CREDIT = PERIOD_CREDIT + VALUES(PERIOD_CREDIT);

    DROP TEMPORARY TABLE IF EXISTS tmp_cm_merged;
    CREATE TEMPORARY TABLE tmp_cm_merged
    (
        ACCOUNT_CD VARCHAR(20) NOT NULL DEFAULT '',
        BEGIN_DEBIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        BEGIN_CREDIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        MOVE_DEBIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        MOVE_CREDIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        PERIOD_DEBIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        PERIOD_CREDIT DECIMAL(18,6) NOT NULL DEFAULT 0,
        END_NET DECIMAL(18,6) NOT NULL DEFAULT 0,
        PRIMARY KEY (ACCOUNT_CD)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_cm_merged (ACCOUNT_CD)
    SELECT ACCOUNT_CD
    FROM tmp_cm_begin_base
    ON DUPLICATE KEY UPDATE ACCOUNT_CD = VALUES(ACCOUNT_CD);

    INSERT INTO tmp_cm_merged (ACCOUNT_CD)
    SELECT ACCOUNT_CD
    FROM tmp_cm_begin_move
    ON DUPLICATE KEY UPDATE ACCOUNT_CD = VALUES(ACCOUNT_CD);

    INSERT INTO tmp_cm_merged (ACCOUNT_CD)
    SELECT ACCOUNT_CD
    FROM tmp_cm_period_amount
    ON DUPLICATE KEY UPDATE ACCOUNT_CD = VALUES(ACCOUNT_CD);

    UPDATE tmp_cm_merged M
    INNER JOIN tmp_cm_begin_base B
        ON B.ACCOUNT_CD = M.ACCOUNT_CD
    SET
        M.BEGIN_DEBIT = B.BEGIN_DEBIT,
        M.BEGIN_CREDIT = B.BEGIN_CREDIT;

    UPDATE tmp_cm_merged M
    INNER JOIN tmp_cm_begin_move BM
        ON BM.ACCOUNT_CD = M.ACCOUNT_CD
    SET
        M.MOVE_DEBIT = BM.PERIOD_DEBIT,
        M.MOVE_CREDIT = BM.PERIOD_CREDIT;

    UPDATE tmp_cm_merged M
    INNER JOIN tmp_cm_period_amount PA
        ON PA.ACCOUNT_CD = M.ACCOUNT_CD
    SET
        M.PERIOD_DEBIT = PA.PERIOD_DEBIT,
        M.PERIOD_CREDIT = PA.PERIOD_CREDIT;

    UPDATE tmp_cm_merged
    SET END_NET =
        BEGIN_DEBIT
        - BEGIN_CREDIT
        + MOVE_DEBIT
        - MOVE_CREDIT
        + PERIOD_DEBIT
        - PERIOD_CREDIT;

    INSERT INTO closingmonth_detail (
        COMPANY_CD,
        CLOSE_YMD,
        ACC_CD,
        FC_TYPE,
        DEBIT,
        CREDIT,
        DEBIT_FC,
        CREDIT_FC,
        NOTE,
        ISDEL,
        CREATE_BY,
        UPDATE_BY
    )
    SELECT
        p_COMPANY_CD,
        v_to_ymd,
        M.ACCOUNT_CD,
        'VND',
        CASE
            WHEN IFNULL(p_BALANCE_METHOD, 'BALANCE_ACCOUNT') = 'BALANCE_ACCOUNT_TWO_SIDE'
                 AND IFNULL(AI.ISABLETYPE, 0) = 2 THEN
                ROUND(
                    M.BEGIN_DEBIT + M.MOVE_DEBIT + M.PERIOD_DEBIT,
                    0
                )
            WHEN ROUND(M.END_NET, 0) > 0 THEN ROUND(M.END_NET, 0)
            ELSE 0
        END,
        CASE
            WHEN IFNULL(p_BALANCE_METHOD, 'BALANCE_ACCOUNT') = 'BALANCE_ACCOUNT_TWO_SIDE'
                 AND IFNULL(AI.ISABLETYPE, 0) = 2 THEN
                ROUND(
                    M.BEGIN_CREDIT + M.MOVE_CREDIT + M.PERIOD_CREDIT,
                    0
                )
            WHEN ROUND(M.END_NET, 0) < 0 THEN ABS(ROUND(M.END_NET, 0))
            ELSE 0
        END,
        0,
        0,
        '',
        '0',
        p_USER_ID,
        p_USER_ID
    FROM tmp_cm_merged M
    LEFT JOIN acclist_info AI
        ON AI.COMPANY_CD = p_COMPANY_CD
       AND AI.ACC_CD = M.ACCOUNT_CD
       AND IFNULL(AI.ISDEL, '0') = '0'
    WHERE
        CASE
            WHEN IFNULL(p_BALANCE_METHOD, 'BALANCE_ACCOUNT') = 'BALANCE_ACCOUNT_TWO_SIDE'
                 AND IFNULL(AI.ISABLETYPE, 0) = 2 THEN
                ROUND(M.BEGIN_DEBIT + M.MOVE_DEBIT + M.PERIOD_DEBIT, 0) <> 0
                OR ROUND(M.BEGIN_CREDIT + M.MOVE_CREDIT + M.PERIOD_CREDIT, 0) <> 0
            ELSE ROUND(ABS(M.END_NET), 0) <> 0
        END;
END$$

DELIMITER ;
