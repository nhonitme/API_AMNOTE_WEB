-- ============================================================
-- Procedure: rpt_fa_asset_book
-- Purpose  : Fixed asset book - 1 row per asset
-- Compatible: MySQL 5.6+
--
-- Example:
-- CALL rpt_fa_asset_book('0001', '20260930', 'VIET', '', '');
-- CALL rpt_fa_asset_book('0001', '20260930', 'ENG', '2111,2112', 'IN_USE');
--
-- Filter:
--   p_ACC_CD       : NULL / '' / 'ALL' = all; otherwise CSV, e.g. '2111,2112'
--   p_ASSET_STATUS : NULL / '' / 'ALL' = all; otherwise CSV, e.g. 'IN_USE,SUSPENDED'
--
-- Notes:
--   1. p_USE_START_YMD is used as report cutoff date, same convention as
--      rpt_fa_depreciation.
--   2. Actual posted depreciation has priority.
--   3. Missing months are calculated from fa_asset FIRST/NORMAL/LAST amounts.
--   4. Result is aggregated to one row per asset.
-- ============================================================

DROP PROCEDURE IF EXISTS `rpt_fa_asset_book`;
DELIMITER //

CREATE PROCEDURE `rpt_fa_asset_book`(
    IN p_COMPANY_CD VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_USE_START_YMD VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_LANGUAGE VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_ACC_CD VARCHAR(2000) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_ASSET_STATUS VARCHAR(500) CHARACTER SET utf8 COLLATE utf8_unicode_ci
)
BEGIN
    DECLARE v_period_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_period_ymd CHAR(8) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_period_end DATE;
    DECLARE v_lang VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    DECLARE v_done INT DEFAULT 0;
    DECLARE v_asset_id BIGINT;
    DECLARE v_start_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_end_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_cur_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_first_amt DECIMAL(18,2);
    DECLARE v_normal_amt DECIMAL(18,2);
    DECLARE v_last_amt DECIMAL(18,2);
    DECLARE v_calc_amt DECIMAL(18,2);

    /*
     * Asset-level cursor.
     * Unlike rpt_fa_depreciation, this report must return one row per asset,
     * therefore depreciation is built at ASSET_ID level instead of ALLOC_ID level.
     */
    DECLARE cur_assets CURSOR FOR
        SELECT
            a.ASSET_ID,
            a.DEPRE_START_YM,
            a.DEPRE_END_YM,
            IFNULL(a.FIRST_DEPRE_AMT, 0),
            IFNULL(a.NORMAL_DEPRE_AMT, 0),
            IFNULL(a.LAST_DEPRE_AMT, 0)
        FROM fa_asset a
        WHERE a.COMPANY_CD = p_COMPANY_CD
          AND IFNULL(a.ISDEL, '0') = '0'
          AND a.USE_START_YMD <= v_period_ymd
          AND (
                IFNULL(TRIM(p_ACC_CD), '') = ''
                OR UPPER(TRIM(p_ACC_CD)) = 'ALL'
                OR FIND_IN_SET(a.ACC_CD, REPLACE(p_ACC_CD, ' ', '')) > 0
              )
          AND (
                IFNULL(TRIM(p_ASSET_STATUS), '') = ''
                OR UPPER(TRIM(p_ASSET_STATUS)) = 'ALL'
                OR FIND_IN_SET(a.STATUS, REPLACE(p_ASSET_STATUS, ' ', '')) > 0
              );

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1;

    /* ------------------------------------------------------------
       1. Normalize language
       ------------------------------------------------------------ */
    SET v_lang = UPPER(IFNULL(NULLIF(TRIM(p_LANGUAGE), ''), 'VIET'));

    IF v_lang IN ('EN', 'ENG', 'ENGLISH') THEN
        SET v_lang = 'ENG';
    ELSEIF v_lang IN ('KR', 'KOR', 'KOREAN') THEN
        SET v_lang = 'KOR';
    ELSEIF v_lang IN ('CN', 'CHN', 'CHINA', 'CHINESE') THEN
        SET v_lang = 'CHINA';
    ELSE
        SET v_lang = 'VIET';
    END IF;

    /* ------------------------------------------------------------
       2. Normalize report date
          Supports:
            YYYYMMDD
            YYYY-MM-DD
            DD/MM/YYYY
       ------------------------------------------------------------ */
    IF LENGTH(TRIM(IFNULL(p_USE_START_YMD, ''))) = 8
       AND TRIM(p_USE_START_YMD) REGEXP '^[0-9]{8}$' THEN

        SET v_period_ymd = TRIM(p_USE_START_YMD);
        SET v_period_end = STR_TO_DATE(v_period_ymd, '%Y%m%d');

    ELSEIF LOCATE('-', TRIM(IFNULL(p_USE_START_YMD, ''))) > 0 THEN

        SET v_period_end = STR_TO_DATE(TRIM(p_USE_START_YMD), '%Y-%m-%d');
        SET v_period_ymd = DATE_FORMAT(v_period_end, '%Y%m%d');

    ELSE

        SET v_period_end = STR_TO_DATE(TRIM(p_USE_START_YMD), '%d/%m/%Y');
        SET v_period_ymd = DATE_FORMAT(v_period_end, '%Y%m%d');

    END IF;

    IF v_period_end IS NULL OR v_period_ymd IS NULL THEN
        SET v_period_end = CURDATE();
        SET v_period_ymd = DATE_FORMAT(v_period_end, '%Y%m%d');
    END IF;

    SET v_period_ym = DATE_FORMAT(v_period_end, '%Y%m');

    /* ------------------------------------------------------------
       3. Monthly depreciation source per asset.
          POSTED rows are inserted first.
          Missing months are then filled by CALC.
       ------------------------------------------------------------ */
    DROP TEMPORARY TABLE IF EXISTS tmp_fa_book_month;

    CREATE TEMPORARY TABLE tmp_fa_book_month (
        ASSET_ID BIGINT NOT NULL,
        DEPRE_YM CHAR(6) NOT NULL,
        DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        SOURCE_TYPE VARCHAR(10) NOT NULL DEFAULT 'POSTED',
        PRIMARY KEY (ASSET_ID, DEPRE_YM)
    ) ENGINE=MEMORY;

    INSERT INTO tmp_fa_book_month (
        ASSET_ID,
        DEPRE_YM,
        DEPRE_AMT,
        SOURCE_TYPE
    )
    SELECT
        p.ASSET_ID,
        p.DEPRE_YM,
        IFNULL(p.DEPRE_AMT, 0),
        'POSTED'
    FROM fa_asset_depre_posted p
    INNER JOIN fa_asset a
        ON a.COMPANY_CD = p.COMPANY_CD
       AND a.ASSET_ID = p.ASSET_ID
       AND IFNULL(a.ISDEL, '0') = '0'
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(p.CANCEL_YN, 'N') = 'N'
      AND p.DEPRE_YM <= v_period_ym
      AND a.USE_START_YMD <= v_period_ymd
      AND (
            IFNULL(TRIM(p_ACC_CD), '') = ''
            OR UPPER(TRIM(p_ACC_CD)) = 'ALL'
            OR FIND_IN_SET(a.ACC_CD, REPLACE(p_ACC_CD, ' ', '')) > 0
          )
      AND (
            IFNULL(TRIM(p_ASSET_STATUS), '') = ''
            OR UPPER(TRIM(p_ASSET_STATUS)) = 'ALL'
            OR FIND_IN_SET(a.STATUS, REPLACE(p_ASSET_STATUS, ' ', '')) > 0
          )
    ON DUPLICATE KEY UPDATE
        DEPRE_AMT = VALUES(DEPRE_AMT),
        SOURCE_TYPE = 'POSTED';

    /*
     * Fill months that have not yet been posted.
     * This follows the FIRST / NORMAL / LAST approach used by
     * rpt_fa_depreciation, but uses asset-level amounts so the asset book
     * is not duplicated by depreciation allocation lines.
     */
    SET v_done = 0;
    OPEN cur_assets;

    asset_loop: LOOP
        FETCH cur_assets
        INTO v_asset_id, v_start_ym, v_end_ym,
             v_first_amt, v_normal_amt, v_last_amt;

        IF v_done = 1 THEN
            LEAVE asset_loop;
        END IF;

        SET v_cur_ym = v_start_ym;

        WHILE v_cur_ym <= v_end_ym
          AND v_cur_ym <= v_period_ym DO

            IF NOT EXISTS (
                SELECT 1
                FROM tmp_fa_book_month t
                WHERE t.ASSET_ID = v_asset_id
                  AND t.DEPRE_YM = v_cur_ym
            ) THEN

                IF v_cur_ym = v_start_ym THEN
                    SET v_calc_amt = IFNULL(v_first_amt, 0);
                ELSEIF v_cur_ym = v_end_ym THEN
                    SET v_calc_amt = IFNULL(v_last_amt, 0);
                ELSE
                    SET v_calc_amt = IFNULL(v_normal_amt, 0);
                END IF;

                IF IFNULL(v_calc_amt, 0) <> 0 THEN
                    INSERT INTO tmp_fa_book_month (
                        ASSET_ID,
                        DEPRE_YM,
                        DEPRE_AMT,
                        SOURCE_TYPE
                    )
                    VALUES (
                        v_asset_id,
                        v_cur_ym,
                        v_calc_amt,
                        'CALC'
                    );
                END IF;

            END IF;

            SET v_cur_ym = DATE_FORMAT(
                DATE_ADD(
                    STR_TO_DATE(CONCAT(v_cur_ym, '01'), '%Y%m%d'),
                    INTERVAL 1 MONTH
                ),
                '%Y%m'
            );

        END WHILE;
    END LOOP;

    CLOSE cur_assets;

    /* ------------------------------------------------------------
       4. Summarize depreciation to one row per asset.
          OPENING_DEPRE_AMT = depreciation before report month.
          PERIOD_DEPRE_AMT  = depreciation of report month.
          ACCUM_DEPRE_AMT   = depreciation from schedule through report month.
       ------------------------------------------------------------ */
    DROP TEMPORARY TABLE IF EXISTS tmp_fa_book_summary;

    CREATE TEMPORARY TABLE tmp_fa_book_summary (
        ASSET_ID BIGINT NOT NULL,
        OPENING_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        PERIOD_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        ACCUM_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        POSTED_MONTH_COUNT INT NOT NULL DEFAULT 0,
        CALC_MONTH_COUNT INT NOT NULL DEFAULT 0,
        PRIMARY KEY (ASSET_ID)
    ) ENGINE=MEMORY;

    INSERT INTO tmp_fa_book_summary (
        ASSET_ID,
        OPENING_DEPRE_AMT,
        PERIOD_DEPRE_AMT,
        ACCUM_DEPRE_AMT,
        POSTED_MONTH_COUNT,
        CALC_MONTH_COUNT
    )
    SELECT
        t.ASSET_ID,
        SUM(CASE WHEN t.DEPRE_YM < v_period_ym
                 THEN t.DEPRE_AMT ELSE 0 END) AS OPENING_DEPRE_AMT,
        SUM(CASE WHEN t.DEPRE_YM = v_period_ym
                 THEN t.DEPRE_AMT ELSE 0 END) AS PERIOD_DEPRE_AMT,
        SUM(CASE WHEN t.DEPRE_YM <= v_period_ym
                 THEN t.DEPRE_AMT ELSE 0 END) AS ACCUM_DEPRE_AMT,
        SUM(CASE WHEN t.SOURCE_TYPE = 'POSTED' THEN 1 ELSE 0 END) AS POSTED_MONTH_COUNT,
        SUM(CASE WHEN t.SOURCE_TYPE = 'CALC' THEN 1 ELSE 0 END) AS CALC_MONTH_COUNT
    FROM tmp_fa_book_month t
    GROUP BY t.ASSET_ID;

    /* ------------------------------------------------------------
       5. Final asset book: ONE ROW / ASSET
       ------------------------------------------------------------ */
    SELECT
        a.ASSET_ID,
        a.ASSET_CD,
        a.ASSET_NM,

        a.ACC_CD,
        CASE v_lang
            WHEN 'ENG' THEN IFNULL(acc.ACCTITLE_NM_ENG, acc.ACCTITLE_NM_VIET)
            WHEN 'KOR' THEN IFNULL(acc.ACCTITLE_NM_KOR, acc.ACCTITLE_NM_VIET)
            WHEN 'CHINA' THEN IFNULL(acc.ACCTITLE_NM_CHINA, acc.ACCTITLE_NM_VIET)
            ELSE IFNULL(acc.ACCTITLE_NM_VIET, '')
        END AS ACC_NM,

        a.USE_DEPT_CD,
        CASE v_lang
            WHEN 'ENG' THEN IFNULL(dep.DEP_NAME_ENG, dep.DEP_NAME_VIET)
            WHEN 'KOR' THEN IFNULL(dep.DEP_NAME_KOR, dep.DEP_NAME_VIET)
            WHEN 'CHINA' THEN IFNULL(dep.DEP_NAME_CHINA, dep.DEP_NAME_VIET)
            ELSE IFNULL(dep.DEP_NAME_VIET, '')
        END AS USE_DEPT_NM,

        a.RECEIVE_YMD,
        a.USE_START_YMD,
        a.DEPRE_START_YM,
        a.DEPRE_END_YM,
        LAST_DAY(
            STR_TO_DATE(CONCAT(a.DEPRE_END_YM, '01'), '%Y%m%d')
        ) AS DEPRE_END_YMD,

        a.USEFUL_LIFE_MONTH,
        a.NORMAL_MONTH_COUNT,

        IFNULL(a.ORIGINAL_AMT, 0) AS ORIGINAL_AMT,

        /* Opening accumulated depreciation before the selected month:
           opening accumulation already stored in fa_asset
           + monthly depreciation before the report month. */
        LEAST(
            IFNULL(a.ORIGINAL_AMT, 0),
            IFNULL(a.ACCUM_DEPRE_AMT, 0)
            + IFNULL(s.OPENING_DEPRE_AMT, 0)
        ) AS OPENING_ACCUM_DEPRE_AMT,

        GREATEST(
            IFNULL(a.ORIGINAL_AMT, 0)
            - LEAST(
                IFNULL(a.ORIGINAL_AMT, 0),
                IFNULL(a.ACCUM_DEPRE_AMT, 0)
                + IFNULL(s.OPENING_DEPRE_AMT, 0)
              ),
            0
        ) AS OPENING_BOOK_AMT,

        /* Prevent current-period depreciation from exceeding
           the remaining book amount at beginning of month. */
        LEAST(
            IFNULL(s.PERIOD_DEPRE_AMT, 0),
            GREATEST(
                IFNULL(a.ORIGINAL_AMT, 0)
                - LEAST(
                    IFNULL(a.ORIGINAL_AMT, 0),
                    IFNULL(a.ACCUM_DEPRE_AMT, 0)
                    + IFNULL(s.OPENING_DEPRE_AMT, 0)
                  ),
                0
            )
        ) AS PERIOD_DEPRE_AMT,

        LEAST(
            IFNULL(a.ORIGINAL_AMT, 0),
            IFNULL(a.ACCUM_DEPRE_AMT, 0)
            + IFNULL(s.ACCUM_DEPRE_AMT, 0)
        ) AS ACCUM_DEPRE_AMT,

        GREATEST(
            IFNULL(a.ORIGINAL_AMT, 0)
            - LEAST(
                IFNULL(a.ORIGINAL_AMT, 0),
                IFNULL(a.ACCUM_DEPRE_AMT, 0)
                + IFNULL(s.ACCUM_DEPRE_AMT, 0)
              ),
            0
        ) AS END_BOOK_AMT,

        IFNULL(a.REMAIN_DEPRE_AMT, 0) AS REMAIN_DEPRE_AMT_MASTER,
        IFNULL(a.FIRST_DEPRE_AMT, 0) AS FIRST_DEPRE_AMT,
        IFNULL(a.NORMAL_DEPRE_AMT, 0) AS NORMAL_DEPRE_AMT,
        IFNULL(a.LAST_DEPRE_AMT, 0) AS LAST_DEPRE_AMT,

        GREATEST(
            PERIOD_DIFF(a.DEPRE_END_YM, v_period_ym),
            0
        ) AS REMAIN_MONTHS_PERIOD,

        a.ACQ_CHITINFO_ID,
        a.ACQ_CHITDETAIL_ID,
        ci_acq.CHIT_CD AS ACQ_CHIT_CD,
        COALESCE(NULLIF(ci_acq.CHIT_NO, ''), a.ACQ_CHIT_NO) AS ACQ_CHIT_NO,
        ci_acq.CHIT_YMD AS ACQ_CHIT_YMD,
        cd_acq.CHITDETAIL_CD AS ACQ_CHITDETAIL_CD,
        cd_acq.DEBIT AS ACQ_DEBIT,
        cd_acq.CREDIT AS ACQ_CREDIT,
        cd_acq.AMOUNT AS ACQ_CHITDETAIL_AMT,

        a.STATUS AS ASSET_STATUS,
        a.NOTE,

        IFNULL(s.POSTED_MONTH_COUNT, 0) AS POSTED_MONTH_COUNT,
        IFNULL(s.CALC_MONTH_COUNT, 0) AS CALC_MONTH_COUNT

    FROM fa_asset a

    LEFT JOIN acclist_info acc
        ON acc.COMPANY_CD = a.COMPANY_CD
       AND acc.ACC_CD = a.ACC_CD
       AND IFNULL(acc.ISDEL, '0') <> '1'

    LEFT JOIN department_info dep
        ON dep.COMPANY_CD = a.COMPANY_CD
       AND dep.DEPARTMENT_CD = a.USE_DEPT_CD
       AND IFNULL(dep.ISDEL, '0') <> '1'

    LEFT JOIN chitinfo ci_acq
        ON ci_acq.COMPANY_CD = a.COMPANY_CD
       AND ci_acq.CHIT_ID = a.ACQ_CHITINFO_ID
       AND IFNULL(ci_acq.ISDEL, '0') <> '1'

    LEFT JOIN chitdetailinfo cd_acq
        ON cd_acq.COMPANY_CD = a.COMPANY_CD
       AND cd_acq.CHITDETAIL_ID = a.ACQ_CHITDETAIL_ID
       AND IFNULL(cd_acq.ISDEL, '0') <> '1'

    LEFT JOIN tmp_fa_book_summary s
        ON s.ASSET_ID = a.ASSET_ID

    WHERE a.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(a.ISDEL, '0') = '0'
      AND a.USE_START_YMD <= v_period_ymd
      AND (
            IFNULL(TRIM(p_ACC_CD), '') = ''
            OR UPPER(TRIM(p_ACC_CD)) = 'ALL'
            OR FIND_IN_SET(a.ACC_CD, REPLACE(p_ACC_CD, ' ', '')) > 0
          )
      AND (
            IFNULL(TRIM(p_ASSET_STATUS), '') = ''
            OR UPPER(TRIM(p_ASSET_STATUS)) = 'ALL'
            OR FIND_IN_SET(a.STATUS, REPLACE(p_ASSET_STATUS, ' ', '')) > 0
          )

    ORDER BY a.ACC_CD, a.ASSET_CD;

    DROP TEMPORARY TABLE IF EXISTS tmp_fa_book_summary;
    DROP TEMPORARY TABLE IF EXISTS tmp_fa_book_month;

END//

DELIMITER ;
