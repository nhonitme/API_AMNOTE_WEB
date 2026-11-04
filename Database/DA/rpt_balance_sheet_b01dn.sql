
DROP PROCEDURE IF EXISTS `rpt_balance_sheet_b01dn`;
DELIMITER //
CREATE DEFINER=`amnc9it`@`115.78.232.22` PROCEDURE `rpt_balance_sheet_b01dn`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_FROM_DATE DATE,
    IN p_TO_DATE DATE,
    IN p_REPORT_VERSION VARCHAR(20),
    IN p_UNIT_DIVISOR DECIMAL(18,6),
    IN p_LANGUAGE VARCHAR(20)
)
BEGIN
    DECLARE v_from_date DATE;
    DECLARE v_begin_date DATE;
    DECLARE v_to_date DATE;
    DECLARE v_begin_ymd CHAR(8);
    DECLARE v_to_ymd CHAR(8);
    DECLARE v_report_version VARCHAR(20);
    DECLARE v_template_company_cd VARCHAR(20);
    DECLARE v_unit_divisor DECIMAL(18,6);
    DECLARE v_iteration INT DEFAULT 0;
    DECLARE v_changed_rows INT DEFAULT 0;
    DECLARE v_language VARCHAR(20);

    /* Company-level baselines (same idea as rpt_trial_balance_s06dn). */
    DECLARE v_end_close_ymd CHAR(8) DEFAULT NULL;
    DECLARE v_end_open_ymd CHAR(8) DEFAULT NULL;
    DECLARE v_end_move_from_ymd CHAR(8) DEFAULT '00000000';
    DECLARE v_begin_close_ymd CHAR(8) DEFAULT NULL;
    DECLARE v_begin_open_ymd CHAR(8) DEFAULT NULL;
    DECLARE v_begin_move_from_ymd CHAR(8) DEFAULT '00000000';
    DECLARE v_scan_from_ymd CHAR(8) DEFAULT '00000000';

    IF p_TO_DATE IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'toDate is required';
    END IF;

    SET v_to_date = p_TO_DATE;
    SET v_from_date = IFNULL(p_FROM_DATE, STR_TO_DATE(CONCAT(YEAR(p_TO_DATE), '0101'), '%Y%m%d'));
    SET v_begin_date = DATE_SUB(v_from_date, INTERVAL 1 DAY);
    SET v_to_ymd = DATE_FORMAT(v_to_date, '%Y%m%d');
    SET v_begin_ymd = DATE_FORMAT(v_begin_date, '%Y%m%d');
    SET v_report_version = IFNULL(NULLIF(TRIM(IFNULL(p_REPORT_VERSION, '')), ''), '2025');
    SET v_unit_divisor = CASE WHEN IFNULL(p_UNIT_DIVISOR, 0) = 0 THEN 1 ELSE p_UNIT_DIVISOR END;
    SET v_language = UPPER(IFNULL(NULLIF(TRIM(p_LANGUAGE), ''), 'VIET'));

    SELECT COALESCE(
        (
            SELECT template.COMPANY_CD
            FROM report_B01DN template
            WHERE template.REPORT_CODE = 'B01_DN'
              AND template.REPORT_VERSION = v_report_version
              AND template.COMPANY_CD IN (p_COMPANY_CD, '')
              AND IFNULL(template.ISDEL, '0') = '0'
            ORDER BY CASE
                WHEN template.COMPANY_CD = p_COMPANY_CD THEN 0
                ELSE 1
            END
            LIMIT 1
        ),
        ''
    )
    INTO v_template_company_cd;

    /*
        END baseline as of v_to_ymd:
        1) latest closingmonth_detail.CLOSE_YMD <= to-date
        2) else latest before_states.OPEN_YMD <= to-date
        CLOSE is end-of-day => movements start next day.
        OPEN is opening => movements include OPEN_YMD (same as previous B01).
    */
    SELECT MAX(CLOSE_YMD)
      INTO v_end_close_ymd
    FROM closingmonth_detail
    WHERE COMPANY_CD = p_COMPANY_CD
      AND ISDEL = '0'
      AND CLOSE_YMD <= v_to_ymd;

    IF v_end_close_ymd IS NOT NULL THEN
        SET v_end_move_from_ymd = DATE_FORMAT(
            DATE_ADD(STR_TO_DATE(v_end_close_ymd, '%Y%m%d'), INTERVAL 1 DAY),
            '%Y%m%d'
        );
    ELSE
        SELECT MAX(OPEN_YMD)
          INTO v_end_open_ymd
        FROM before_states
        WHERE COMPANY_CD = p_COMPANY_CD
          AND ISDEL = '0'
          AND OPEN_YMD <= v_to_ymd;

        IF v_end_open_ymd IS NOT NULL THEN
            SET v_end_move_from_ymd = v_end_open_ymd;
        ELSE
            SET v_end_move_from_ymd = '00000000';
        END IF;
    END IF;

    /*
        BEGIN baseline as of day before from-date.
    */
    SELECT MAX(CLOSE_YMD)
      INTO v_begin_close_ymd
    FROM closingmonth_detail
    WHERE COMPANY_CD = p_COMPANY_CD
      AND ISDEL = '0'
      AND CLOSE_YMD <= v_begin_ymd;

    IF v_begin_close_ymd IS NOT NULL THEN
        SET v_begin_move_from_ymd = DATE_FORMAT(
            DATE_ADD(STR_TO_DATE(v_begin_close_ymd, '%Y%m%d'), INTERVAL 1 DAY),
            '%Y%m%d'
        );
    ELSE
        SELECT MAX(OPEN_YMD)
          INTO v_begin_open_ymd
        FROM before_states
        WHERE COMPANY_CD = p_COMPANY_CD
          AND ISDEL = '0'
          AND OPEN_YMD <= v_begin_ymd;

        IF v_begin_open_ymd IS NOT NULL THEN
            SET v_begin_move_from_ymd = v_begin_open_ymd;
        ELSE
            SET v_begin_move_from_ymd = '00000000';
        END IF;
    END IF;

    SET v_scan_from_ymd = LEAST(v_begin_move_from_ymd, v_end_move_from_ymd);

    DROP TEMPORARY TABLE IF EXISTS tmp_b01_base_end;
    CREATE TEMPORARY TABLE tmp_b01_base_end
    (
        ACC_CD VARCHAR(20) NOT NULL,
        DEBIT DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        CREDIT DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        PRIMARY KEY (ACC_CD)
    ) ENGINE=MEMORY DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    DROP TEMPORARY TABLE IF EXISTS tmp_b01_base_begin;
    CREATE TEMPORARY TABLE tmp_b01_base_begin
    (
        ACC_CD VARCHAR(20) NOT NULL,
        DEBIT DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        CREDIT DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        PRIMARY KEY (ACC_CD)
    ) ENGINE=MEMORY DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    IF v_end_close_ymd IS NOT NULL THEN
        INSERT INTO tmp_b01_base_end (ACC_CD, DEBIT, CREDIT)
        SELECT ACC_CD, SUM(DEBIT), SUM(CREDIT)
        FROM closingmonth_detail
        WHERE COMPANY_CD = p_COMPANY_CD
          AND ISDEL = '0'
          AND CLOSE_YMD = v_end_close_ymd
          AND IFNULL(ACC_CD, '') <> ''
        GROUP BY ACC_CD;
    ELSEIF v_end_open_ymd IS NOT NULL THEN
        INSERT INTO tmp_b01_base_end (ACC_CD, DEBIT, CREDIT)
        SELECT ACC_CD, SUM(DEBIT), SUM(CREDIT)
        FROM before_states
        WHERE COMPANY_CD = p_COMPANY_CD
          AND ISDEL = '0'
          AND OPEN_YMD = v_end_open_ymd
          AND IFNULL(ACC_CD, '') <> ''
        GROUP BY ACC_CD;
    END IF;

    IF v_begin_close_ymd IS NOT NULL THEN
        INSERT INTO tmp_b01_base_begin (ACC_CD, DEBIT, CREDIT)
        SELECT ACC_CD, SUM(DEBIT), SUM(CREDIT)
        FROM closingmonth_detail
        WHERE COMPANY_CD = p_COMPANY_CD
          AND ISDEL = '0'
          AND CLOSE_YMD = v_begin_close_ymd
          AND IFNULL(ACC_CD, '') <> ''
        GROUP BY ACC_CD;
    ELSEIF v_begin_open_ymd IS NOT NULL THEN
        INSERT INTO tmp_b01_base_begin (ACC_CD, DEBIT, CREDIT)
        SELECT ACC_CD, SUM(DEBIT), SUM(CREDIT)
        FROM before_states
        WHERE COMPANY_CD = p_COMPANY_CD
          AND ISDEL = '0'
          AND OPEN_YMD = v_begin_open_ymd
          AND IFNULL(ACC_CD, '') <> ''
        GROUP BY ACC_CD;
    END IF;

    /*
        Movements only in [scan_from, to_ymd] via IDX_CHITINFO_03.
        No per-row join to opening thresholds (that made the previous rewrite slower).
    */
    DROP TEMPORARY TABLE IF EXISTS tmp_b01_account_move_sums;
    CREATE TEMPORARY TABLE tmp_b01_account_move_sums
    (
        ACC_CD VARCHAR(20) NOT NULL,
        END_NET DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        BEGIN_NET DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        PRIMARY KEY (ACC_CD)
    ) ENGINE=MEMORY DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    IF v_scan_from_ymd <= v_to_ymd THEN
        /*
            Dá»¯ liá»‡u thá»±c táº¿ Ä‘Ã£ test ráº¥t nhanh.
            Giá»¯ aggregate trá»±c tiáº¿p Ä‘á»ƒ trÃ¡nh overhead materialize/CROSS JOIN.
        */
        INSERT INTO tmp_b01_account_move_sums (ACC_CD, END_NET, BEGIN_NET)
        SELECT
            TRIM(d.DEBIT) AS ACC_CD,
            SUM(
                CASE
                    WHEN h.CHIT_YMD >= v_end_move_from_ymd
                     AND h.CHIT_YMD <= v_to_ymd
                    THEN IFNULL(d.AMOUNT, 0)
                    ELSE 0
                END
            ) AS END_NET,
            SUM(
                CASE
                    WHEN h.CHIT_YMD >= v_begin_move_from_ymd
                     AND h.CHIT_YMD <= v_begin_ymd
                    THEN IFNULL(d.AMOUNT, 0)
                    ELSE 0
                END
            ) AS BEGIN_NET
        FROM chitinfo h
        INNER JOIN chitdetailinfo d
            ON d.COMPANY_CD = h.COMPANY_CD
           AND d.CHIT_ID = h.CHIT_ID
           AND d.ISDEL = '0'
        WHERE h.COMPANY_CD = p_COMPANY_CD
          AND h.ISDEL = '0'
          AND h.CHIT_YMD >= v_scan_from_ymd
          AND h.CHIT_YMD <= v_to_ymd
          AND d.DEBIT IS NOT NULL
          AND d.DEBIT <> ''
        GROUP BY TRIM(d.DEBIT);

        INSERT INTO tmp_b01_account_move_sums (ACC_CD, END_NET, BEGIN_NET)
        SELECT
            TRIM(d.CREDIT) AS ACC_CD,
            SUM(
                CASE
                    WHEN h.CHIT_YMD >= v_end_move_from_ymd
                     AND h.CHIT_YMD <= v_to_ymd
                    THEN -IFNULL(d.AMOUNT, 0)
                    ELSE 0
                END
            ) AS END_NET,
            SUM(
                CASE
                    WHEN h.CHIT_YMD >= v_begin_move_from_ymd
                     AND h.CHIT_YMD <= v_begin_ymd
                    THEN -IFNULL(d.AMOUNT, 0)
                    ELSE 0
                END
            ) AS BEGIN_NET
        FROM chitinfo h
        INNER JOIN chitdetailinfo d
            ON d.COMPANY_CD = h.COMPANY_CD
           AND d.CHIT_ID = h.CHIT_ID
           AND d.ISDEL = '0'
        WHERE h.COMPANY_CD = p_COMPANY_CD
          AND h.ISDEL = '0'
          AND h.CHIT_YMD >= v_scan_from_ymd
          AND h.CHIT_YMD <= v_to_ymd
          AND d.CREDIT IS NOT NULL
          AND d.CREDIT <> ''
        GROUP BY TRIM(d.CREDIT)
        ON DUPLICATE KEY UPDATE
            END_NET = END_NET + VALUES(END_NET),
            BEGIN_NET = BEGIN_NET + VALUES(BEGIN_NET);
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_b01_account_seed;
    CREATE TEMPORARY TABLE tmp_b01_account_seed
    (
        ACC_CD VARCHAR(20) NOT NULL,
        PRIMARY KEY (ACC_CD)
    ) ENGINE=MEMORY DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT IGNORE INTO tmp_b01_account_seed (ACC_CD)
    SELECT ACC_CD FROM tmp_b01_base_end WHERE IFNULL(ACC_CD, '') <> '';

    INSERT IGNORE INTO tmp_b01_account_seed (ACC_CD)
    SELECT ACC_CD FROM tmp_b01_base_begin WHERE IFNULL(ACC_CD, '') <> '';

    INSERT IGNORE INTO tmp_b01_account_seed (ACC_CD)
    SELECT ACC_CD FROM tmp_b01_account_move_sums WHERE IFNULL(ACC_CD, '') <> '';

    DROP TEMPORARY TABLE IF EXISTS tmp_b01_account_balance;
    CREATE TEMPORARY TABLE tmp_b01_account_balance
    (
        ACC_CD VARCHAR(20) NOT NULL,
        END_NET DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        BEGIN_NET DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        PRIMARY KEY (ACC_CD)
    ) ENGINE=MEMORY DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_b01_account_balance (ACC_CD, END_NET, BEGIN_NET)
    SELECT
        seed.ACC_CD,
        CAST(
            IFNULL(base_end.DEBIT, 0) - IFNULL(base_end.CREDIT, 0)
            + IFNULL(move.END_NET, 0) AS DECIMAL(18,2)
        ),
        CAST(
            IFNULL(base_begin.DEBIT, 0) - IFNULL(base_begin.CREDIT, 0)
            + IFNULL(move.BEGIN_NET, 0) AS DECIMAL(18,2)
        )
    FROM tmp_b01_account_seed seed
    LEFT JOIN tmp_b01_base_end base_end
        ON base_end.ACC_CD = seed.ACC_CD
    LEFT JOIN tmp_b01_base_begin base_begin
        ON base_begin.ACC_CD = seed.ACC_CD
    LEFT JOIN tmp_b01_account_move_sums move
        ON move.ACC_CD = seed.ACC_CD;

    DROP TEMPORARY TABLE IF EXISTS tmp_b01_rule;
    CREATE TEMPORARY TABLE tmp_b01_rule
    (
        ITEM_CODE VARCHAR(20) NOT NULL,
        ACC_PREFIX VARCHAR(20) NOT NULL,
        CALC_METHOD VARCHAR(30) NOT NULL,
        INDEX idx_tmp_b01_rule_01 (ITEM_CODE, ACC_PREFIX)
    ) ENGINE=MEMORY DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_b01_rule (ITEM_CODE, ACC_PREFIX, CALC_METHOD)
    SELECT
        template.ITEM_CODE,
        TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(IFNULL(template.ACCOUNT_RULE, ''), ',', numbers.n), ',', -1)),
        template.CALC_METHOD
    FROM report_B01DN template
    INNER JOIN (
        SELECT 1 AS n UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5
        UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9 UNION ALL SELECT 10
    ) numbers
        ON numbers.n <= 1 + LENGTH(IFNULL(template.ACCOUNT_RULE, '')) - LENGTH(REPLACE(IFNULL(template.ACCOUNT_RULE, ''), ',', ''))
    WHERE template.COMPANY_CD = v_template_company_cd
      AND template.REPORT_CODE = 'B01_DN'
      AND template.REPORT_VERSION = v_report_version
      AND template.SECTION_TYPE = 'DETAIL'
      AND IFNULL(template.DATA_SOURCE_TYPE, '') = 'ACCOUNT_RULE'
      AND IFNULL(template.ACCOUNT_RULE, '') <> ''
      AND TRIM(SUBSTRING_INDEX(SUBSTRING_INDEX(IFNULL(template.ACCOUNT_RULE, ''), ',', numbers.n), ',', -1)) <> '';

    DROP TEMPORARY TABLE IF EXISTS tmp_b01_report;
    CREATE TEMPORARY TABLE tmp_b01_report
    (
        ROW_KEY VARCHAR(80) NOT NULL DEFAULT '',
        COMPANY_CD VARCHAR(20) NOT NULL DEFAULT '',
        ROW_TYPE VARCHAR(30) NOT NULL DEFAULT '',
        SORT_ORDER INT NOT NULL DEFAULT 0,
        ITEM_CODE VARCHAR(50) NULL,
        ITEM_NAME VARCHAR(1000) NULL,
        CAPTION VARCHAR(1000) NULL,
        LABEL_TEXT VARCHAR(200) NULL,
        NOTE VARCHAR(100) NOT NULL DEFAULT '',
        END_YEAR DECIMAL(18,2) NULL,
        BEGIN_YEAR DECIMAL(18,2) NULL,
        LEVEL_NO INT NOT NULL DEFAULT 0,
        FONT_BOLD CHAR(1) NOT NULL DEFAULT '0',
        FONT_ITALIC CHAR(1) NOT NULL DEFAULT '0',
        DATA_SOURCE_TYPE VARCHAR(50) NULL,
        FORMULA_EXPR VARCHAR(1000) NULL,
        REPORT_YEAR_TEXT VARCHAR(100) NOT NULL DEFAULT '',
        REPORT_DATE_TEXT VARCHAR(100) NOT NULL DEFAULT '',
        AMOUNT_UNIT_TEXT VARCHAR(255) NOT NULL DEFAULT '',
        PRIMARY KEY (ROW_KEY),
        INDEX idx_tmp_b01_report_01 (ITEM_CODE),
        INDEX idx_tmp_b01_report_02 (SORT_ORDER)
    ) ENGINE=MEMORY DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_b01_report
    (
        ROW_KEY,
        COMPANY_CD,
        ROW_TYPE,
        SORT_ORDER,
        ITEM_CODE,
        ITEM_NAME,
        CAPTION,
        LABEL_TEXT,
        NOTE,
        LEVEL_NO,
        FONT_BOLD,
        FONT_ITALIC,
        DATA_SOURCE_TYPE,
        FORMULA_EXPR,
        REPORT_YEAR_TEXT,
        REPORT_DATE_TEXT,
        AMOUNT_UNIT_TEXT
    )
    SELECT
        template.ITEM_KEY,
        p_COMPANY_CD,
        template.ELEMENT_TYPE,
        template.SORT_ORDER,
        template.ITEM_CODE,
        -- ITEM_NAME: backward-compat display = indented CAPTION; LABEL_TEXT for future i18n via t_message_info.
        CONCAT(REPEAT('  ', GREATEST(IFNULL(template.LEVEL_NO, 1) - 1, 0)), IFNULL(template.CAPTION, '')),
        template.CAPTION,
        template.LABEL_TEXT,
        '',
        IFNULL(template.LEVEL_NO, 0),
        IFNULL(template.FONT_BOLD, '0'),
        IFNULL(template.FONT_ITALIC, '0'),
        IFNULL(template.DATA_SOURCE_TYPE, ''),
        template.FORMULA_EXPR,
        CASE v_language
            WHEN 'ENG' THEN CONCAT('Year ', YEAR(v_to_date))
            WHEN 'KOR' THEN CONCAT(YEAR(v_to_date), 'ë…„')
            WHEN 'CHN' THEN CONCAT(YEAR(v_to_date), 'å¹´')
            ELSE CONCAT('NÄƒm ', YEAR(v_to_date))
        END,
        CASE v_language
            WHEN 'ENG' THEN CONCAT('As at ', DATE_FORMAT(v_to_date, '%d/%m/%Y'))
            WHEN 'KOR' THEN CONCAT('ê¸°ì¤€ì¼: ', DATE_FORMAT(v_to_date, '%d/%m/%Y'))
            WHEN 'CHN' THEN CONCAT('æˆªè‡³æ—¥æœŸï¼š', DATE_FORMAT(v_to_date, '%d/%m/%Y'))
            ELSE CONCAT('Táº¡i ngÃ y ', DATE_FORMAT(v_to_date, '%d/%m/%Y'))
        END,
        CASE v_language
            WHEN 'ENG' THEN
                CASE
                    WHEN v_unit_divisor = 1000000 THEN 'Unit: million VND'
                    WHEN v_unit_divisor = 1000 THEN 'Unit: thousand VND'
                    ELSE 'Unit: VND'
                END
            WHEN 'KOR' THEN
                CASE
                    WHEN v_unit_divisor = 1000000 THEN 'ë‹¨ìœ„: ë°±ë§Œ VND'
                    WHEN v_unit_divisor = 1000 THEN 'ë‹¨ìœ„: ì²œ VND'
                    ELSE 'ë‹¨ìœ„: VND'
                END
            WHEN 'CHN' THEN
                CASE
                    WHEN v_unit_divisor = 1000000 THEN 'å•ä½ï¼šç™¾ä¸‡è¶Šå—ç›¾'
                    WHEN v_unit_divisor = 1000 THEN 'å•ä½ï¼šåƒè¶Šå—ç›¾'
                    ELSE 'å•ä½ï¼šè¶Šå—ç›¾'
                END
            ELSE
                CASE
                    WHEN v_unit_divisor = 1000000 THEN 'ÄÆ¡n vá»‹ tÃ­nh: triá»‡u Ä‘á»“ng'
                    WHEN v_unit_divisor = 1000 THEN 'ÄÆ¡n vá»‹ tÃ­nh: nghÃ¬n Ä‘á»“ng'
                    ELSE 'ÄÆ¡n vá»‹ tÃ­nh: Ä‘á»“ng'
                END
        END
    FROM report_B01DN template
    WHERE template.COMPANY_CD = v_template_company_cd
      AND template.REPORT_CODE = 'B01_DN'
      AND template.REPORT_VERSION = v_report_version
      AND template.SECTION_TYPE = 'DETAIL'
      AND IFNULL(template.ISDEL, '0') = '0'
      AND IFNULL(template.IS_VISIBLE, '1') = '1'
    ORDER BY template.SORT_ORDER;

    UPDATE tmp_b01_report report
    LEFT JOIN
    (
        SELECT
            rule.ITEM_CODE,
            SUM(CASE rule.CALC_METHOD
                WHEN 'CREDIT_MINUS_DEBIT' THEN -balance.END_NET
                WHEN 'DEBIT_ONLY' THEN GREATEST(balance.END_NET, 0)
                WHEN 'CREDIT_ONLY' THEN GREATEST(-balance.END_NET, 0)
                ELSE balance.END_NET
            END) AS END_YEAR,
            SUM(CASE rule.CALC_METHOD
                WHEN 'CREDIT_MINUS_DEBIT' THEN -balance.BEGIN_NET
                WHEN 'DEBIT_ONLY' THEN GREATEST(balance.BEGIN_NET, 0)
                WHEN 'CREDIT_ONLY' THEN GREATEST(-balance.BEGIN_NET, 0)
                ELSE balance.BEGIN_NET
            END) AS BEGIN_YEAR
        FROM tmp_b01_rule rule
        INNER JOIN tmp_b01_account_balance balance
            ON balance.ACC_CD LIKE CONCAT(rule.ACC_PREFIX, '%')
        GROUP BY rule.ITEM_CODE
    ) calc
        ON UPPER(calc.ITEM_CODE) = UPPER(report.ITEM_CODE)
    SET
        report.END_YEAR = NULLIF(ROUND(IFNULL(calc.END_YEAR, 0) / v_unit_divisor, 0), 0),
        report.BEGIN_YEAR = NULLIF(ROUND(IFNULL(calc.BEGIN_YEAR, 0) / v_unit_divisor, 0), 0)
    WHERE UPPER(IFNULL(report.DATA_SOURCE_TYPE, '')) = 'ACCOUNT_RULE';

    /*
        Tá»‘i Æ°u FORMULA cho MySQL 5.6:
        - KhÃ´ng DROP/CREATE temporary table trong WHILE.
        - tmp_b01_formula_source vÃ  tmp_b01_formula_calc chá»‰ táº¡o 1 láº§n.
        - Má»—i vÃ²ng chá»‰ TRUNCATE + INSERT + UPDATE trÃªn khoáº£ng 126 dÃ²ng.
        - Dá»«ng ngay khi káº¿t quáº£ khÃ´ng cÃ²n thay Ä‘á»•i.
    */
    DROP TEMPORARY TABLE IF EXISTS tmp_b01_formula_source;
    CREATE TEMPORARY TABLE tmp_b01_formula_source
    (
        ITEM_CODE VARCHAR(50) NULL,
        FORMULA_EXPR VARCHAR(1000) NULL,
        INDEX idx_tmp_b01_formula_source_item (ITEM_CODE)
    ) ENGINE=MEMORY DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_b01_formula_source (ITEM_CODE, FORMULA_EXPR)
    SELECT ITEM_CODE, FORMULA_EXPR
    FROM tmp_b01_report
    WHERE UPPER(IFNULL(DATA_SOURCE_TYPE, '')) = 'FORMULA';

    DROP TEMPORARY TABLE IF EXISTS tmp_b01_formula_calc;
    CREATE TEMPORARY TABLE tmp_b01_formula_calc
    (
        ITEM_CODE VARCHAR(50) NOT NULL,
        END_YEAR DECIMAL(18,2) NULL,
        BEGIN_YEAR DECIMAL(18,2) NULL,
        PRIMARY KEY (ITEM_CODE)
    ) ENGINE=MEMORY DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    SET v_iteration = 0;

    formula_loop: WHILE v_iteration < 8 DO
        TRUNCATE TABLE tmp_b01_formula_calc;

        INSERT INTO tmp_b01_formula_calc (ITEM_CODE, END_YEAR, BEGIN_YEAR)
        SELECT
            formula.ITEM_CODE,
            SUM(IFNULL(child.END_YEAR, 0)) AS END_YEAR,
            SUM(IFNULL(child.BEGIN_YEAR, 0)) AS BEGIN_YEAR
        FROM tmp_b01_formula_source formula
        INNER JOIN tmp_b01_report child
            ON FIND_IN_SET(
                UPPER(IFNULL(child.ITEM_CODE, '')),
                REPLACE(
                    REPLACE(
                        REPLACE(
                            UPPER(SUBSTRING_INDEX(IFNULL(formula.FORMULA_EXPR, ''), '=', -1)),
                            ' ',
                            ''
                        ),
                        '+',
                        ','
                    ),
                    ';',
                    ','
                )
            ) > 0
        GROUP BY formula.ITEM_CODE;

        UPDATE tmp_b01_report report
        INNER JOIN tmp_b01_formula_calc calc
            ON UPPER(calc.ITEM_CODE) = UPPER(report.ITEM_CODE)
        SET
            report.END_YEAR = NULLIF(calc.END_YEAR, 0),
            report.BEGIN_YEAR = NULLIF(calc.BEGIN_YEAR, 0)
        WHERE UPPER(IFNULL(report.DATA_SOURCE_TYPE, '')) = 'FORMULA';

        SET v_changed_rows = ROW_COUNT();
        SET v_iteration = v_iteration + 1;

        IF v_changed_rows = 0 THEN
            LEAVE formula_loop;
        END IF;
    END WHILE formula_loop;

    SELECT
        ROW_KEY,
        COMPANY_CD,
        ROW_TYPE,
        ITEM_NAME,
        CAPTION,
        LABEL_TEXT,
        ITEM_CODE,
        NOTE,
        END_YEAR,
        BEGIN_YEAR,
        LEVEL_NO,
        FONT_BOLD,
        FONT_ITALIC,
        REPORT_YEAR_TEXT,
        REPORT_DATE_TEXT,
        AMOUNT_UNIT_TEXT
    FROM tmp_b01_report
    ORDER BY SORT_ORDER;
END//
DELIMITER ;