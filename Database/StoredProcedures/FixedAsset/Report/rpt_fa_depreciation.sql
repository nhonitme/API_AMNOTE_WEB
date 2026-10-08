-- Bảng tính khấu hao TSCĐ (chi tiết theo dòng fa_asset_depre_alloc)

-- Deploy to company database (e.g. am_web_001)

-- Params align with ConfiguredReportService query: companyCd, useStartYmd (yyyyMMdd), language, accCd, assetStatus



DELIMITER $$



DROP PROCEDURE IF EXISTS `rpt_fa_depreciation`$$



CREATE PROCEDURE `rpt_fa_depreciation`(

    IN p_COMPANY_CD VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,

    IN p_USE_START_YMD VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,

    IN p_LANGUAGE VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,

    IN p_ACC_CD VARCHAR(2000) CHARACTER SET utf8 COLLATE utf8_unicode_ci,

    IN p_ASSET_STATUS VARCHAR(500) CHARACTER SET utf8 COLLATE utf8_unicode_ci

)

BEGIN

    DECLARE v_period_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    DECLARE v_year_start_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    DECLARE v_period_ymd CHAR(8) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    DECLARE v_period_end DATE;

    DECLARE v_lang VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci;



    DECLARE v_done INT DEFAULT 0;

    DECLARE v_asset_id BIGINT;

    DECLARE v_alloc_id BIGINT;

    DECLARE v_start_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    DECLARE v_end_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    DECLARE v_cur_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    DECLARE v_depre_type VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    DECLARE v_calc_amt DECIMAL(18,2);



    DECLARE cur_assets CURSOR FOR

        SELECT

            a.ASSET_ID,

            al.ALLOC_ID,

            a.DEPRE_START_YM,

            a.DEPRE_END_YM

        FROM fa_asset a

        INNER JOIN fa_asset_depre_alloc al

            ON al.COMPANY_CD = a.COMPANY_CD

           AND al.ASSET_ID = a.ASSET_ID

           AND IFNULL(al.ISDEL, '0') = '0'

        WHERE a.COMPANY_CD = p_COMPANY_CD

          AND IFNULL(a.ISDEL, '0') = '0'

          AND a.USE_START_YMD <= v_period_ymd

          AND (

                IFNULL(p_ACC_CD, '') = ''

                OR FIND_IN_SET(a.ACC_CD, REPLACE(p_ACC_CD, ' ', '')) > 0

              )

          AND (

                IFNULL(p_ASSET_STATUS, '') = ''

                OR FIND_IN_SET(a.STATUS, REPLACE(p_ASSET_STATUS, ' ', '')) > 0

              );



    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1;



    SET v_lang = UPPER(IFNULL(NULLIF(TRIM(p_LANGUAGE), ''), 'VIET'));

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

    SET v_year_start_ym = CONCAT(YEAR(v_period_end), '01');



    DROP TEMPORARY TABLE IF EXISTS tmp_fa_depre_alloc_month;

    CREATE TEMPORARY TABLE tmp_fa_depre_alloc_month (

        ASSET_ID BIGINT NOT NULL,

        ALLOC_ID BIGINT NOT NULL,

        DEPRE_YM CHAR(6) NOT NULL,

        DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,

        SOURCE_TYPE VARCHAR(10) NOT NULL DEFAULT 'POSTED',

        PRIMARY KEY (ASSET_ID, ALLOC_ID, DEPRE_YM)

    ) ENGINE=MEMORY;



    INSERT INTO tmp_fa_depre_alloc_month (ASSET_ID, ALLOC_ID, DEPRE_YM, DEPRE_AMT, SOURCE_TYPE)

    SELECT

        pa.ASSET_ID,

        pa.ALLOC_ID,

        pa.DEPRE_YM,

        IFNULL(pa.DEPRE_AMT, 0),

        'POSTED'

    FROM fa_asset_depre_posted_alloc pa

    INNER JOIN fa_asset_depre_posted p

        ON p.POST_ID = pa.POST_ID

       AND p.COMPANY_CD = pa.COMPANY_CD

    INNER JOIN fa_asset a

        ON a.COMPANY_CD = pa.COMPANY_CD

       AND a.ASSET_ID = pa.ASSET_ID

       AND IFNULL(a.ISDEL, '0') = '0'

    WHERE pa.COMPANY_CD = p_COMPANY_CD

      AND IFNULL(p.CANCEL_YN, 'N') = 'N'

      AND pa.ALLOC_ID IS NOT NULL

      AND a.USE_START_YMD <= v_period_ymd

      AND (

            IFNULL(p_ACC_CD, '') = ''

            OR FIND_IN_SET(a.ACC_CD, REPLACE(p_ACC_CD, ' ', '')) > 0

          )

      AND (

            IFNULL(p_ASSET_STATUS, '') = ''

            OR FIND_IN_SET(a.STATUS, REPLACE(p_ASSET_STATUS, ' ', '')) > 0

          );



    SET v_done = 0;

    OPEN cur_assets;

    asset_loop: LOOP

        FETCH cur_assets INTO v_asset_id, v_alloc_id, v_start_ym, v_end_ym;

        IF v_done = 1 THEN

            LEAVE asset_loop;

        END IF;



        SET v_cur_ym = v_start_ym;

        WHILE v_cur_ym <= v_end_ym DO

            IF NOT EXISTS (

                SELECT 1

                FROM tmp_fa_depre_alloc_month t

                WHERE t.ASSET_ID = v_asset_id

                  AND t.ALLOC_ID = v_alloc_id

                  AND t.DEPRE_YM = v_cur_ym

            ) THEN

                IF v_cur_ym = v_start_ym THEN

                    SET v_depre_type = 'FIRST';

                ELSEIF v_cur_ym = v_end_ym THEN

                    SET v_depre_type = 'LAST';

                ELSE

                    SET v_depre_type = 'NORMAL';

                END IF;



                SELECT

                    CASE v_depre_type

                        WHEN 'FIRST' THEN IFNULL(al.FIRST_ALLOC_AMT, 0)

                        WHEN 'LAST' THEN IFNULL(al.LAST_ALLOC_AMT, 0)

                        ELSE IFNULL(al.NORMAL_ALLOC_AMT, 0)

                    END

                INTO v_calc_amt

                FROM fa_asset_depre_alloc al

                WHERE al.COMPANY_CD = p_COMPANY_CD

                  AND al.ALLOC_ID = v_alloc_id

                  AND IFNULL(al.ISDEL, '0') = '0'

                LIMIT 1;



                IF IFNULL(v_calc_amt, 0) <> 0 THEN

                    INSERT INTO tmp_fa_depre_alloc_month (ASSET_ID, ALLOC_ID, DEPRE_YM, DEPRE_AMT, SOURCE_TYPE)

                    VALUES (v_asset_id, v_alloc_id, v_cur_ym, v_calc_amt, 'CALC');

                END IF;

            END IF;



            SET v_cur_ym = DATE_FORMAT(

                DATE_ADD(STR_TO_DATE(CONCAT(v_cur_ym, '01'), '%Y%m%d'), INTERVAL 1 MONTH),

                '%Y%m'

            );

        END WHILE;

    END LOOP;

    CLOSE cur_assets;



    DROP TEMPORARY TABLE IF EXISTS tmp_fa_depre_summary;

    CREATE TEMPORARY TABLE tmp_fa_depre_summary (

        ASSET_ID BIGINT NOT NULL,

        ALLOC_ID BIGINT NOT NULL,

        OPENING_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,

        PERIOD_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,

        ACCUM_DEPRE_AMT DECIMAL(18,2) NOT NULL DEFAULT 0,

        PRIMARY KEY (ASSET_ID, ALLOC_ID)

    ) ENGINE=MEMORY;



    INSERT INTO tmp_fa_depre_summary (ASSET_ID, ALLOC_ID, OPENING_DEPRE_AMT, PERIOD_DEPRE_AMT, ACCUM_DEPRE_AMT)

    SELECT

        src.ASSET_ID,

        src.ALLOC_ID,

        SUM(CASE WHEN src.DEPRE_YM < v_year_start_ym THEN src.DEPRE_AMT ELSE 0 END),

        SUM(CASE WHEN src.DEPRE_YM = v_period_ym THEN src.DEPRE_AMT ELSE 0 END),

        SUM(CASE WHEN src.DEPRE_YM <= v_period_ym THEN src.DEPRE_AMT ELSE 0 END)

    FROM tmp_fa_depre_alloc_month src

    GROUP BY src.ASSET_ID, src.ALLOC_ID;



    SELECT

        a.ACC_CD,

        CASE v_lang

            WHEN 'ENG' THEN IFNULL(acc.ACCTITLE_NM_ENG, acc.ACCTITLE_NM_VIET)

            WHEN 'KOR' THEN IFNULL(acc.ACCTITLE_NM_KOR, acc.ACCTITLE_NM_VIET)

            WHEN 'CHINA' THEN IFNULL(acc.ACCTITLE_NM_CHINA, acc.ACCTITLE_NM_VIET)

            ELSE IFNULL(acc.ACCTITLE_NM_VIET, '')

        END AS ACC_NM,

        al.CREDIT_ACCT_CD,

        CASE v_lang

            WHEN 'ENG' THEN IFNULL(cr_acc.ACCTITLE_NM_ENG, cr_acc.ACCTITLE_NM_VIET)

            WHEN 'KOR' THEN IFNULL(cr_acc.ACCTITLE_NM_KOR, cr_acc.ACCTITLE_NM_VIET)

            WHEN 'CHINA' THEN IFNULL(cr_acc.ACCTITLE_NM_CHINA, cr_acc.ACCTITLE_NM_VIET)

            ELSE IFNULL(cr_acc.ACCTITLE_NM_VIET, '')

        END AS CREDIT_ACCT_NM,

        al.DEBIT_ACCT_CD,

        CASE v_lang

            WHEN 'ENG' THEN IFNULL(dr_acc.ACCTITLE_NM_ENG, dr_acc.ACCTITLE_NM_VIET)

            WHEN 'KOR' THEN IFNULL(dr_acc.ACCTITLE_NM_KOR, dr_acc.ACCTITLE_NM_VIET)

            WHEN 'CHINA' THEN IFNULL(dr_acc.ACCTITLE_NM_CHINA, dr_acc.ACCTITLE_NM_VIET)

            ELSE IFNULL(dr_acc.ACCTITLE_NM_VIET, '')

        END AS DEBIT_ACCT_NM,

        a.ASSET_CD,

        a.ASSET_NM,

        a.RECEIVE_YMD,

        a.USE_START_YMD,

        LAST_DAY(STR_TO_DATE(CONCAT(a.DEPRE_END_YM, '01'), '%Y%m%d')) AS DEPRE_END_YMD,

        GREATEST(

            PERIOD_DIFF(a.DEPRE_END_YM, DATE_FORMAT(CURDATE(), '%Y%m')),

            0

        ) AS REMAIN_MONTHS_TODAY,

        GREATEST(

            PERIOD_DIFF(a.DEPRE_END_YM, v_period_ym),

            0

        ) AS REMAIN_MONTHS_PERIOD,

        IFNULL(a.ORIGINAL_AMT, 0) AS ORIGINAL_AMT,

        IFNULL(a.ORIGINAL_AMT, 0) - IFNULL(depre_sum.OPENING_DEPRE_AMT, 0) AS OPENING_BOOK_AMT,

        IFNULL(depre_sum.PERIOD_DEPRE_AMT, 0) AS PERIOD_DEPRE_AMT,

        IFNULL(depre_sum.PERIOD_DEPRE_AMT, 0) AS PERIOD_ACCUM_DEPRE_AMT,

        IFNULL(depre_sum.ACCUM_DEPRE_AMT, 0) AS ACCUM_DEPRE_AMT,

        IFNULL(a.ORIGINAL_AMT, 0) - IFNULL(depre_sum.ACCUM_DEPRE_AMT, 0) AS END_BOOK_AMT

    FROM fa_asset a

    INNER JOIN fa_asset_depre_alloc al

        ON al.COMPANY_CD = a.COMPANY_CD

       AND al.ASSET_ID = a.ASSET_ID

       AND IFNULL(al.ISDEL, '0') = '0'

    LEFT JOIN acclist_info acc

        ON acc.COMPANY_CD = a.COMPANY_CD

       AND acc.ACC_CD = a.ACC_CD

       AND IFNULL(acc.ISDEL, '0') <> '1'

    LEFT JOIN acclist_info cr_acc

        ON cr_acc.COMPANY_CD = al.COMPANY_CD

       AND cr_acc.ACC_CD = al.CREDIT_ACCT_CD

       AND IFNULL(cr_acc.ISDEL, '0') <> '1'

    LEFT JOIN acclist_info dr_acc

        ON dr_acc.COMPANY_CD = al.COMPANY_CD

       AND dr_acc.ACC_CD = al.DEBIT_ACCT_CD

       AND IFNULL(dr_acc.ISDEL, '0') <> '1'

    LEFT JOIN tmp_fa_depre_summary depre_sum

        ON depre_sum.ASSET_ID = a.ASSET_ID

       AND depre_sum.ALLOC_ID = al.ALLOC_ID

    WHERE a.COMPANY_CD = p_COMPANY_CD

      AND IFNULL(a.ISDEL, '0') = '0'

      AND a.USE_START_YMD <= v_period_ymd

      AND (

            IFNULL(p_ACC_CD, '') = ''

            OR FIND_IN_SET(a.ACC_CD, REPLACE(p_ACC_CD, ' ', '')) > 0

          )

      AND (

            IFNULL(p_ASSET_STATUS, '') = ''

            OR FIND_IN_SET(a.STATUS, REPLACE(p_ASSET_STATUS, ' ', '')) > 0

          )

    ORDER BY a.ACC_CD, a.ASSET_CD, al.ALLOC_SEQ;



    DROP TEMPORARY TABLE IF EXISTS tmp_fa_depre_summary;

    DROP TEMPORARY TABLE IF EXISTS tmp_fa_depre_alloc_month;

END$$



DELIMITER ;


