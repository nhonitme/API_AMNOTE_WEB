-- --------------------------------------------------------
-- New FA report: depreciation by specific period
-- Replaces legacy: getfixedassetsSpecPeriod_info_All_V2025
-- Reference: rpt_fa_depreciation + new fa_* tables
-- MySQL 5.6 compatible
--
-- Example:
-- CALL rpt_fa_depreciation_period('0001', '20260930', 'VIET', 'ALL', 'ALL', 'ALL');
-- CALL rpt_fa_depreciation_period('0001', '20260930', 'VIET', '2111,2112', 'IN_USE', 'A001');
--
-- Notes:
--   * One row per asset, matching the legacy menu structure.
--   * Posted depreciation is preferred. Missing months are calculated from
--     fa_asset FIRST/NORMAL/LAST depreciation amounts.
--   * Legacy columns whose source no longer exists in the new FA schema are
--     kept as NULL/blank/0 for result-contract compatibility.
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;

DROP PROCEDURE IF EXISTS `rpt_fa_depreciation_period`;
DELIMITER //

CREATE PROCEDURE `rpt_fa_depreciation_period`(
    IN p_COMPANY_CD      VARCHAR(20)   CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_USE_START_YMD   VARCHAR(20)   CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_LANGUAGE        VARCHAR(20)   CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_ACC_CD          VARCHAR(2000) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_ASSET_STATUS    VARCHAR(500)  CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_DEPARTMENT_CD   VARCHAR(500)  CHARACTER SET utf8 COLLATE utf8_unicode_ci
)
BEGIN
    DECLARE v_period_end DATE;
    DECLARE v_period_ymd CHAR(8) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_period_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_year_start_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_lang VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    DECLARE v_done INT DEFAULT 0;
    DECLARE v_asset_id BIGINT;
    DECLARE v_start_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_end_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_cur_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_depre_type VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_first_amt DECIMAL(18,2);
    DECLARE v_normal_amt DECIMAL(18,2);
    DECLARE v_last_amt DECIMAL(18,2);
    DECLARE v_calc_amt DECIMAL(18,2);

    /*
       Keep the same business filter direction as the legacy report:
       - account: ALL/blank = all; comma list = exact codes; single value = prefix
       - status : ALL/blank/5 = all
       - dept   : ALL/blank/A001 = all (A001 was the old "all department" value)
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
                OR (
                    LOCATE(',', p_ACC_CD) > 0
                    AND FIND_IN_SET(a.ACC_CD, REPLACE(p_ACC_CD, ' ', '')) > 0
                )
                OR (
                    LOCATE(',', p_ACC_CD) = 0
                    AND a.ACC_CD LIKE CONCAT(TRIM(p_ACC_CD), '%')
                )
              )
          AND (
                IFNULL(TRIM(p_ASSET_STATUS), '') = ''
                OR UPPER(TRIM(p_ASSET_STATUS)) = 'ALL'
                OR TRIM(p_ASSET_STATUS) = '5'
                OR FIND_IN_SET(a.STATUS, REPLACE(p_ASSET_STATUS, ' ', '')) > 0
              )
          AND (
                IFNULL(TRIM(p_DEPARTMENT_CD), '') = ''
                OR UPPER(TRIM(p_DEPARTMENT_CD)) = 'ALL'
                OR UPPER(TRIM(p_DEPARTMENT_CD)) = 'A001'
                OR FIND_IN_SET(IFNULL(a.USE_DEPT_CD, ''), REPLACE(p_DEPARTMENT_CD, ' ', '')) > 0
              );

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1;

    /* ----------------------------------------------------
       1. Normalize input
       ---------------------------------------------------- */
    SET v_lang = UPPER(IFNULL(NULLIF(TRIM(p_LANGUAGE), ''), 'VIET'));

    IF v_lang = 'EN' THEN SET v_lang = 'ENG'; END IF;
    IF v_lang = 'KR' THEN SET v_lang = 'KOR'; END IF;
    IF v_lang IN ('CN', 'CHN') THEN SET v_lang = 'CHINA'; END IF;

    IF LENGTH(TRIM(IFNULL(p_USE_START_YMD, ''))) = 8
       AND TRIM(p_USE_START_YMD) REGEXP '^[0-9]{8}$' THEN
        SET v_period_ymd = TRIM(p_USE_START_YMD);
        SET v_period_end = STR_TO_DATE(v_period_ymd, '%Y%m%d');
    ELSEIF LOCATE('-', TRIM(IFNULL(p_USE_START_YMD, ''))) > 0 THEN
        SET v_period_end = STR_TO_DATE(TRIM(p_USE_START_YMD), '%Y-%m-%d');
        SET v_period_ymd = DATE_FORMAT(v_period_end, '%Y%m%d');
    ELSEIF LOCATE('/', TRIM(IFNULL(p_USE_START_YMD, ''))) > 0 THEN
        SET v_period_end = STR_TO_DATE(TRIM(p_USE_START_YMD), '%d/%m/%Y');
        SET v_period_ymd = DATE_FORMAT(v_period_end, '%Y%m%d');
    ELSE
        SET v_period_end = NULL;
        SET v_period_ymd = NULL;
    END IF;

    IF v_period_end IS NULL OR v_period_ymd IS NULL THEN
        SET v_period_end = CURDATE();
        SET v_period_ymd = DATE_FORMAT(v_period_end, '%Y%m%d');
    END IF;

    SET v_period_ym = DATE_FORMAT(v_period_end, '%Y%m');
    SET v_year_start_ym = CONCAT(YEAR(v_period_end), '01');

    /* ----------------------------------------------------
       2. Monthly depreciation at ASSET level
          - actual posted amount first
          - calculate only when that asset/month is not posted
       ---------------------------------------------------- */
    DROP TEMPORARY TABLE IF EXISTS tmp_fa_period_month;
    CREATE TEMPORARY TABLE tmp_fa_period_month (
        ASSET_ID BIGINT NOT NULL,
        DEPRE_YM CHAR(6) NOT NULL,
        DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        SOURCE_TYPE VARCHAR(10) NOT NULL DEFAULT 'POSTED',
        PRIMARY KEY (ASSET_ID, DEPRE_YM)
    ) ENGINE=MEMORY;

    INSERT INTO tmp_fa_period_month
    (
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
            OR (
                LOCATE(',', p_ACC_CD) > 0
                AND FIND_IN_SET(a.ACC_CD, REPLACE(p_ACC_CD, ' ', '')) > 0
            )
            OR (
                LOCATE(',', p_ACC_CD) = 0
                AND a.ACC_CD LIKE CONCAT(TRIM(p_ACC_CD), '%')
            )
          )
      AND (
            IFNULL(TRIM(p_ASSET_STATUS), '') = ''
            OR UPPER(TRIM(p_ASSET_STATUS)) = 'ALL'
            OR TRIM(p_ASSET_STATUS) = '5'
            OR FIND_IN_SET(a.STATUS, REPLACE(p_ASSET_STATUS, ' ', '')) > 0
          )
      AND (
            IFNULL(TRIM(p_DEPARTMENT_CD), '') = ''
            OR UPPER(TRIM(p_DEPARTMENT_CD)) = 'ALL'
            OR UPPER(TRIM(p_DEPARTMENT_CD)) = 'A001'
            OR FIND_IN_SET(IFNULL(a.USE_DEPT_CD, ''), REPLACE(p_DEPARTMENT_CD, ' ', '')) > 0
          );

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
                FROM tmp_fa_period_month t
                WHERE t.ASSET_ID = v_asset_id
                  AND t.DEPRE_YM = v_cur_ym
            ) THEN
                IF v_cur_ym = v_start_ym THEN
                    SET v_depre_type = 'FIRST';
                    SET v_calc_amt = IFNULL(v_first_amt, 0);
                ELSEIF v_cur_ym = v_end_ym THEN
                    SET v_depre_type = 'LAST';
                    SET v_calc_amt = IFNULL(v_last_amt, 0);
                ELSE
                    SET v_depre_type = 'NORMAL';
                    SET v_calc_amt = IFNULL(v_normal_amt, 0);
                END IF;

                IF IFNULL(v_calc_amt, 0) <> 0 THEN
                    INSERT INTO tmp_fa_period_month
                    (
                        ASSET_ID,
                        DEPRE_YM,
                        DEPRE_AMT,
                        SOURCE_TYPE
                    )
                    VALUES
                    (
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

    /* ----------------------------------------------------
       3. Depreciation summary for the selected period
       ---------------------------------------------------- */
    DROP TEMPORARY TABLE IF EXISTS tmp_fa_period_summary;
    CREATE TEMPORARY TABLE tmp_fa_period_summary (
        ASSET_ID BIGINT NOT NULL,
        BEFORE_YEAR_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        BEFORE_CURRENT_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        CURRENT_MONTH_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        PERIOD_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        TOTAL_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,
        POSTED_MONTH_COUNT INT NOT NULL DEFAULT 0,
        CALC_MONTH_COUNT INT NOT NULL DEFAULT 0,
        PRIMARY KEY (ASSET_ID)
    ) ENGINE=MEMORY;

    INSERT INTO tmp_fa_period_summary
    (
        ASSET_ID,
        BEFORE_YEAR_DEPRE_AMT,
        BEFORE_CURRENT_DEPRE_AMT,
        CURRENT_MONTH_DEPRE_AMT,
        PERIOD_DEPRE_AMT,
        TOTAL_DEPRE_AMT,
        POSTED_MONTH_COUNT,
        CALC_MONTH_COUNT
    )
    SELECT
        t.ASSET_ID,
        SUM(CASE
                WHEN t.DEPRE_YM < v_year_start_ym
                THEN t.DEPRE_AMT ELSE 0
            END),
        SUM(CASE
                WHEN t.DEPRE_YM < v_period_ym
                THEN t.DEPRE_AMT ELSE 0
            END),
        SUM(CASE
                WHEN t.DEPRE_YM = v_period_ym
                THEN t.DEPRE_AMT ELSE 0
            END),
        SUM(CASE
                WHEN t.DEPRE_YM >= v_year_start_ym
                 AND t.DEPRE_YM <= v_period_ym
                THEN t.DEPRE_AMT ELSE 0
            END),
        SUM(CASE
                WHEN t.DEPRE_YM <= v_period_ym
                THEN t.DEPRE_AMT ELSE 0
            END),
        SUM(CASE WHEN t.SOURCE_TYPE = 'POSTED' THEN 1 ELSE 0 END),
        SUM(CASE WHEN t.SOURCE_TYPE = 'CALC' THEN 1 ELSE 0 END)
    FROM tmp_fa_period_month t
    GROUP BY t.ASSET_ID;

    /* ----------------------------------------------------
       4. Aggregate depreciation accounts.
          New schema can have multiple allocation rows per asset, while the
          legacy report had one FADEP/FAEX account. GROUP_CONCAT preserves
          all configured accounts without duplicating asset rows.
       ---------------------------------------------------- */
    DROP TEMPORARY TABLE IF EXISTS tmp_fa_period_accounts;
    CREATE TEMPORARY TABLE tmp_fa_period_accounts (
        ASSET_ID BIGINT NOT NULL,
        FADEP_ACC_CD VARCHAR(500) NULL,
        FADEP_NM_VIET VARCHAR(1000) NULL,
        FADEP_NM_ENG VARCHAR(1000) NULL,
        FADEP_NM_KOR VARCHAR(1000) NULL,
        FADEP_NM_CHINA VARCHAR(1000) NULL,
        FAEX_ACC_CD VARCHAR(500) NULL,
        FAEX_NM_VIET VARCHAR(1000) NULL,
        FAEX_NM_ENG VARCHAR(1000) NULL,
        FAEX_NM_KOR VARCHAR(1000) NULL,
        FAEX_NM_CHINA VARCHAR(1000) NULL,
        PRIMARY KEY (ASSET_ID)
    ) ENGINE=MEMORY;

    INSERT INTO tmp_fa_period_accounts
    (
        ASSET_ID,
        FADEP_ACC_CD,
        FADEP_NM_VIET,
        FADEP_NM_ENG,
        FADEP_NM_KOR,
        FADEP_NM_CHINA,
        FAEX_ACC_CD,
        FAEX_NM_VIET,
        FAEX_NM_ENG,
        FAEX_NM_KOR,
        FAEX_NM_CHINA
    )
    SELECT
        al.ASSET_ID,
        GROUP_CONCAT(DISTINCT al.DEBIT_ACCT_CD ORDER BY al.ALLOC_SEQ SEPARATOR ','),
        GROUP_CONCAT(DISTINCT IFNULL(dr.ACCTITLE_NM_VIET, al.DEBIT_ACCT_CD) ORDER BY al.ALLOC_SEQ SEPARATOR ','),
        GROUP_CONCAT(DISTINCT IFNULL(dr.ACCTITLE_NM_ENG,  IFNULL(dr.ACCTITLE_NM_VIET, al.DEBIT_ACCT_CD)) ORDER BY al.ALLOC_SEQ SEPARATOR ','),
        GROUP_CONCAT(DISTINCT IFNULL(dr.ACCTITLE_NM_KOR,  IFNULL(dr.ACCTITLE_NM_VIET, al.DEBIT_ACCT_CD)) ORDER BY al.ALLOC_SEQ SEPARATOR ','),
        GROUP_CONCAT(DISTINCT IFNULL(dr.ACCTITLE_NM_CHINA,IFNULL(dr.ACCTITLE_NM_VIET, al.DEBIT_ACCT_CD)) ORDER BY al.ALLOC_SEQ SEPARATOR ','),
        GROUP_CONCAT(DISTINCT al.CREDIT_ACCT_CD ORDER BY al.ALLOC_SEQ SEPARATOR ','),
        GROUP_CONCAT(DISTINCT IFNULL(cr.ACCTITLE_NM_VIET, al.CREDIT_ACCT_CD) ORDER BY al.ALLOC_SEQ SEPARATOR ','),
        GROUP_CONCAT(DISTINCT IFNULL(cr.ACCTITLE_NM_ENG,  IFNULL(cr.ACCTITLE_NM_VIET, al.CREDIT_ACCT_CD)) ORDER BY al.ALLOC_SEQ SEPARATOR ','),
        GROUP_CONCAT(DISTINCT IFNULL(cr.ACCTITLE_NM_KOR,  IFNULL(cr.ACCTITLE_NM_VIET, al.CREDIT_ACCT_CD)) ORDER BY al.ALLOC_SEQ SEPARATOR ','),
        GROUP_CONCAT(DISTINCT IFNULL(cr.ACCTITLE_NM_CHINA,IFNULL(cr.ACCTITLE_NM_VIET, al.CREDIT_ACCT_CD)) ORDER BY al.ALLOC_SEQ SEPARATOR ',')
    FROM fa_asset_depre_alloc al
    LEFT JOIN acclist_info dr
        ON dr.COMPANY_CD = al.COMPANY_CD
       AND dr.ACC_CD = al.DEBIT_ACCT_CD
       AND IFNULL(dr.ISDEL, '0') <> '1'
    LEFT JOIN acclist_info cr
        ON cr.COMPANY_CD = al.COMPANY_CD
       AND cr.ACC_CD = al.CREDIT_ACCT_CD
       AND IFNULL(cr.ISDEL, '0') <> '1'
    WHERE al.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(al.ISDEL, '0') = '0'
    GROUP BY al.ASSET_ID;

    /* ----------------------------------------------------
       5. Final result
          Column aliases intentionally follow the old menu contract.
       ---------------------------------------------------- */
    SELECT
        /* Asset / account / department */
        a.ASSET_CD AS FA_CD,
        a.ACC_CD AS FA_ACC_CD,
        IFNULL(a.USE_DEPT_CD, '') AS DEPARTMENT_CD,

        IFNULL(di.DEP_NAME_VIET, '') AS DEP_NAME_VIET,
        IFNULL(di.DEP_NAME_ENG,  IFNULL(di.DEP_NAME_VIET, '')) AS DEP_NAME_ENG,
        IFNULL(di.DEP_NAME_KOR,  IFNULL(di.DEP_NAME_VIET, '')) AS DEP_NAME_KOR,
        IFNULL(di.DEP_NAME_CHINA,IFNULL(di.DEP_NAME_VIET, '')) AS DEP_NAME_CHINA,

        /* Legacy setting used to conditionally show this column.
           New API always returns it; UI can hide it when needed. */
        COALESCE(NULLIF(ci.CHIT_NO, ''), NULLIF(a.ACQ_CHIT_NO, ''), '') AS CHIT_NO,

        IFNULL(acc.ACCTITLE_NM_VIET, '') AS FA_ACC_NM_VIET,
        IFNULL(acc.ACCTITLE_NM_ENG,  IFNULL(acc.ACCTITLE_NM_VIET, '')) AS FA_ACC_NM_ENG,
        IFNULL(acc.ACCTITLE_NM_KOR,  IFNULL(acc.ACCTITLE_NM_VIET, '')) AS FA_ACC_NM_KOR,
        IFNULL(acc.ACCTITLE_NM_CHINA,IFNULL(acc.ACCTITLE_NM_VIET, '')) AS FA_ACC_NM_CHINA,

        IFNULL(aa.FADEP_NM_VIET, '') AS FADEP_NM_VIET,
        IFNULL(aa.FADEP_NM_ENG, '') AS FADEP_NM_ENG,
        IFNULL(aa.FADEP_NM_KOR, '') AS FADEP_NM_KOR,
        IFNULL(aa.FADEP_NM_CHINA, '') AS FADEP_NM_CHINA,
        IFNULL(aa.FADEP_ACC_CD, '') AS FADEP_ACC_CD,

        IFNULL(aa.FAEX_NM_VIET, '') AS FAEX_NM_VIET,
        IFNULL(aa.FAEX_NM_ENG, '') AS FAEX_NM_ENG,
        IFNULL(aa.FAEX_NM_KOR, '') AS FAEX_NM_KOR,
        IFNULL(aa.FAEX_NM_CHINA, '') AS FAEX_NM_CHINA,
        IFNULL(aa.FAEX_ACC_CD, '') AS FAEX_ACC_CD,

        /* Not present in new fa_asset schema */
        '' AS MANAGEMENT_CD,
        '' AS MANAGEMENT_NM_VIET,
        '' AS MANAGEMENT_NM_ENG,
        '' AS MANAGEMENT_NM_KOR,
        '' AS MANAGEMENT_NM_CHINA,
        '' AS MANAGEMENT_CD_2,
        '' AS MANAGEMENT_NM_2_VIET,
        '' AS MANAGEMENT_NM_2_ENG,
        '' AS MANAGEMENT_NM_2_KOR,
        '' AS MANAGEMENT_NM_2_CHINA,

        a.ASSET_NM AS FA_NM,
        NULL AS FA_NM_EN,
        NULL AS FA_NM_KR,
        NULL AS FA_NM_CN,
        '' AS FA_PRODUCT_CD,

        CASE
            WHEN IFNULL(a.RECEIVE_YMD, '') REGEXP '^[0-9]{8}$'
            THEN STR_TO_DATE(a.RECEIVE_YMD, '%Y%m%d')
            ELSE NULL
        END AS ACQ_YMD,

        CASE
            WHEN IFNULL(a.USE_START_YMD, '') REGEXP '^[0-9]{8}$'
            THEN STR_TO_DATE(a.USE_START_YMD, '%Y%m%d')
            ELSE NULL
        END AS USE_YMD,

        GREATEST(
            DATEDIFF(
                v_period_end,
                STR_TO_DATE(a.USE_START_YMD, '%Y%m%d')
            ),
            0
        ) AS LAST_DAYS,

        GREATEST(
            DATEDIFF(
                LAST_DAY(STR_TO_DATE(CONCAT(a.DEPRE_END_YM, '01'), '%Y%m%d')),
                v_period_end
            ) + 1,
            0
        ) AS REMAIN_DAYS,

        IFNULL(a.USEFUL_LIFE_MONTH, 0) AS DEP_MONTH,

        GREATEST(
            PERIOD_DIFF(a.DEPRE_END_YM, v_period_ym),
            0
        ) AS REMAIN_TERM,

        IFNULL(a.ORIGINAL_AMT, 0) AS ACQ_MONEY,

        /* VAT acquisition cost is not stored in current fa_asset/chitdetailinfo */
        0.0 AS VAT_COST,

        /* Current master balance before future planned depreciation */
        GREATEST(IFNULL(a.REMAIN_DEPRE_AMT, 0), 0) AS BALANCE,

        /* Opening accumulated depreciation migrated/stored on asset master */
        GREATEST(IFNULL(a.ACCUM_DEPRE_AMT, 0), 0) AS HANG_MONEY,

        /* No stop/sale date column in new schema */
        NULL AS STOP_YMD,
        a.STATUS AS STATE,
        0.0 AS SALE_MONEY,

        /* Accumulated depreciation before current fiscal year */
        LEAST(
            IFNULL(a.ORIGINAL_AMT, 0),
            GREATEST(IFNULL(a.ACCUM_DEPRE_AMT, 0), 0)
            + GREATEST(IFNULL(ds.BEFORE_YEAR_DEPRE_AMT, 0), 0)
        ) AS PRE_PERIOD_ACCUMONEY,

        /* Remaining book value at beginning of current fiscal year */
        GREATEST(
            IFNULL(a.ORIGINAL_AMT, 0)
            - LEAST(
                IFNULL(a.ORIGINAL_AMT, 0),
                GREATEST(IFNULL(a.ACCUM_DEPRE_AMT, 0), 0)
                + GREATEST(IFNULL(ds.BEFORE_YEAR_DEPRE_AMT, 0), 0)
              ),
            0
        ) AS PRIOR_REMAINMONEY,

        /* Depreciation of selected month, clipped to book value before month */
        LEAST(
            GREATEST(IFNULL(ds.CURRENT_MONTH_DEPRE_AMT, 0), 0),
            GREATEST(
                IFNULL(a.ORIGINAL_AMT, 0)
                - LEAST(
                    IFNULL(a.ORIGINAL_AMT, 0),
                    GREATEST(IFNULL(a.ACCUM_DEPRE_AMT, 0), 0)
                    + GREATEST(IFNULL(ds.BEFORE_CURRENT_DEPRE_AMT, 0), 0)
                  ),
                0
            )
        ) AS DEPMONTHMONEY,

        /* Depreciation in current fiscal year up to report month */
        GREATEST(
            LEAST(
                IFNULL(a.ORIGINAL_AMT, 0),
                GREATEST(IFNULL(a.ACCUM_DEPRE_AMT, 0), 0)
                + GREATEST(IFNULL(ds.TOTAL_DEPRE_AMT, 0), 0)
            )
            - LEAST(
                IFNULL(a.ORIGINAL_AMT, 0),
                GREATEST(IFNULL(a.ACCUM_DEPRE_AMT, 0), 0)
                + GREATEST(IFNULL(ds.BEFORE_YEAR_DEPRE_AMT, 0), 0)
              ),
            0
        ) AS PERIOD_ACCUMONEY,

        /* Total accumulated depreciation as of report date */
        LEAST(
            IFNULL(a.ORIGINAL_AMT, 0),
            GREATEST(IFNULL(a.ACCUM_DEPRE_AMT, 0), 0)
            + GREATEST(IFNULL(ds.TOTAL_DEPRE_AMT, 0), 0)
        ) AS ACCUMONEY,

        /* Remaining book value as of report date */
        GREATEST(
            IFNULL(a.ORIGINAL_AMT, 0)
            - LEAST(
                IFNULL(a.ORIGINAL_AMT, 0),
                GREATEST(IFNULL(a.ACCUM_DEPRE_AMT, 0), 0)
                + GREATEST(IFNULL(ds.TOTAL_DEPRE_AMT, 0), 0)
              ),
            0
        ) AS REMAINMONEY,

        /* Legacy fields without equivalent source in the new FA schema */
        '' AS STORE_CD,
        '' AS STORE_NM,
        0.0 AS QUANTITY,
        '' AS UNIT_NM,
        '' AS VAT_CHIT_NO,
        NULL AS VAT_YMD,
        0.0 AS LIQUIDATE,

        IFNULL(a.NORMAL_DEPRE_AMT, 0) AS DEP_MONEY_MONTH_DEFAULT,
        '' AS REASON_SELL,

        /* New helper fields for the web/API layer */
        a.ASSET_ID,
        a.DEPRE_START_YM,
        a.DEPRE_END_YM,
        IFNULL(a.NORMAL_MONTH_COUNT, 0) AS NORMAL_MONTH_COUNT,
        IFNULL(ds.POSTED_MONTH_COUNT, 0) AS POSTED_MONTH_COUNT,
        IFNULL(ds.CALC_MONTH_COUNT, 0) AS CALC_MONTH_COUNT,

        CASE v_lang
            WHEN 'ENG' THEN IFNULL(acc.ACCTITLE_NM_ENG, IFNULL(acc.ACCTITLE_NM_VIET, ''))
            WHEN 'KOR' THEN IFNULL(acc.ACCTITLE_NM_KOR, IFNULL(acc.ACCTITLE_NM_VIET, ''))
            WHEN 'CHINA' THEN IFNULL(acc.ACCTITLE_NM_CHINA, IFNULL(acc.ACCTITLE_NM_VIET, ''))
            ELSE IFNULL(acc.ACCTITLE_NM_VIET, '')
        END AS FA_ACC_NM,

        CASE v_lang
            WHEN 'ENG' THEN IFNULL(di.DEP_NAME_ENG, IFNULL(di.DEP_NAME_VIET, ''))
            WHEN 'KOR' THEN IFNULL(di.DEP_NAME_KOR, IFNULL(di.DEP_NAME_VIET, ''))
            WHEN 'CHINA' THEN IFNULL(di.DEP_NAME_CHINA, IFNULL(di.DEP_NAME_VIET, ''))
            ELSE IFNULL(di.DEP_NAME_VIET, '')
        END AS DEPARTMENT_NM

    FROM fa_asset a

    LEFT JOIN acclist_info acc
        ON acc.COMPANY_CD = a.COMPANY_CD
       AND acc.ACC_CD = a.ACC_CD
       AND IFNULL(acc.ISDEL, '0') <> '1'

    LEFT JOIN department_info di
        ON di.COMPANY_CD = a.COMPANY_CD
       AND di.DEPARTMENT_CD = a.USE_DEPT_CD
       AND IFNULL(di.ISDEL, '0') <> '1'

    LEFT JOIN chitdetailinfo acqd
        ON acqd.COMPANY_CD = a.COMPANY_CD
       AND acqd.CHITDETAIL_ID = a.ACQ_CHITDETAIL_ID
       AND IFNULL(acqd.ISDEL, '0') <> '1'

    LEFT JOIN chitinfo ci
        ON ci.COMPANY_CD = a.COMPANY_CD
       AND ci.CHIT_ID = IFNULL(a.ACQ_CHITINFO_ID, acqd.CHIT_ID)
       AND IFNULL(ci.ISDEL, '0') <> '1'

    LEFT JOIN tmp_fa_period_summary ds
        ON ds.ASSET_ID = a.ASSET_ID

    LEFT JOIN tmp_fa_period_accounts aa
        ON aa.ASSET_ID = a.ASSET_ID

    WHERE a.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(a.ISDEL, '0') = '0'
      AND a.USE_START_YMD <= v_period_ymd
      AND (
            IFNULL(TRIM(p_ACC_CD), '') = ''
            OR UPPER(TRIM(p_ACC_CD)) = 'ALL'
            OR (
                LOCATE(',', p_ACC_CD) > 0
                AND FIND_IN_SET(a.ACC_CD, REPLACE(p_ACC_CD, ' ', '')) > 0
            )
            OR (
                LOCATE(',', p_ACC_CD) = 0
                AND a.ACC_CD LIKE CONCAT(TRIM(p_ACC_CD), '%')
            )
          )
      AND (
            IFNULL(TRIM(p_ASSET_STATUS), '') = ''
            OR UPPER(TRIM(p_ASSET_STATUS)) = 'ALL'
            OR TRIM(p_ASSET_STATUS) = '5'
            OR FIND_IN_SET(a.STATUS, REPLACE(p_ASSET_STATUS, ' ', '')) > 0
          )
      AND (
            IFNULL(TRIM(p_DEPARTMENT_CD), '') = ''
            OR UPPER(TRIM(p_DEPARTMENT_CD)) = 'ALL'
            OR UPPER(TRIM(p_DEPARTMENT_CD)) = 'A001'
            OR FIND_IN_SET(IFNULL(a.USE_DEPT_CD, ''), REPLACE(p_DEPARTMENT_CD, ' ', '')) > 0
          )

    ORDER BY a.ACC_CD, a.ASSET_CD;

    DROP TEMPORARY TABLE IF EXISTS tmp_fa_period_accounts;
    DROP TEMPORARY TABLE IF EXISTS tmp_fa_period_summary;
    DROP TEMPORARY TABLE IF EXISTS tmp_fa_period_month;
END//
DELIMITER ;

/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
