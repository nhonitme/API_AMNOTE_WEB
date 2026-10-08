DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_period_lock_fa_prepaid_create_voucher`$$

CREATE PROCEDURE `sp_period_lock_fa_prepaid_create_voucher`(
    IN p_COMPANY_CD VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_PERIOD_YM CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_USER_ID VARCHAR(50) CHARACTER SET utf8 COLLATE utf8_unicode_ci
)
BEGIN
    DECLARE v_not_found INT DEFAULT 0;
    DECLARE v_post_id BIGINT;
    DECLARE v_asset_id BIGINT;
    DECLARE v_asset_cd VARCHAR(50);
    DECLARE v_asset_nm VARCHAR(255);
    DECLARE v_depre_amt DECIMAL(18,2);
    DECLARE v_chit_id BIGINT;
    DECLARE v_chit_no VARCHAR(50);
    DECLARE v_chit_cd VARCHAR(30);
    DECLARE v_chit_ymd DATE;
    DECLARE v_chit_ymd_char CHAR(8);
    DECLARE v_description VARCHAR(500);
    DECLARE v_detail_cd VARCHAR(30);
    DECLARE v_detail_id BIGINT;
    DECLARE v_line_seq INT DEFAULT 0;
    DECLARE v_post_alloc_id BIGINT;
    DECLARE v_alloc_seq INT;
    DECLARE v_debit_acct VARCHAR(20);
    DECLARE v_credit_acct VARCHAR(20);
    DECLARE v_department_id BIGINT;
    DECLARE v_department_cd VARCHAR(20);
    DECLARE v_line_amt DECIMAL(18,2);

    DECLARE cur_posted CURSOR FOR
        SELECT
            p.POST_ID,
            p.ASSET_ID,
            a.ASSET_CD,
            a.ASSET_NM,
            p.DEPRE_AMT
        FROM fa_asset_depre_posted p
        INNER JOIN fa_asset a
            ON a.ASSET_ID = p.ASSET_ID
           AND a.COMPANY_CD = p.COMPANY_CD
        WHERE p.COMPANY_CD = p_COMPANY_CD
          AND p.DEPRE_YM = p_PERIOD_YM
          AND IFNULL(p.CANCEL_YN, 'N') = 'N'
          AND p.CHITINFO_ID IS NULL
        ORDER BY p.POST_ID;

    DECLARE cur_alloc CURSOR FOR
        SELECT
            pa.POST_ALLOC_ID,
            pa.ALLOC_SEQ,
            pa.DEBIT_ACCT_CD,
            pa.CREDIT_ACCT_CD,
            pa.DEPARTMENT_ID,
            pa.DEPRE_AMT
        FROM fa_asset_depre_posted_alloc pa
        WHERE pa.POST_ID = v_post_id
          AND pa.COMPANY_CD = p_COMPANY_CD
        ORDER BY pa.ALLOC_SEQ, pa.POST_ALLOC_ID;

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_not_found = 1;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        CALL sp_period_lock_fa_prepaid_rollback_period(p_COMPANY_CD, p_PERIOD_YM);
        RESIGNAL;
    END;

    CALL sp_period_lock_fa_prepaid_last_day(p_PERIOD_YM, v_chit_ymd);
    SET v_chit_ymd_char = DATE_FORMAT(v_chit_ymd, '%Y%m%d');

    OPEN cur_posted;

    posted_loop: LOOP
        SET v_not_found = 0;
        FETCH cur_posted INTO v_post_id, v_asset_id, v_asset_cd, v_asset_nm, v_depre_amt;
        IF v_not_found = 1 THEN
            LEAVE posted_loop;
        END IF;

        CALL sp_period_lock_fa_prepaid_next_chit_no(p_COMPANY_CD, v_chit_ymd, v_chit_no);

        SET v_chit_cd = LEFT(CONCAT('C', DATE_FORMAT(NOW(6), '%Y%m%d%H%i%s%f'), LPAD(v_post_id, 6, '0')), 30);
        SET v_description = CONCAT('Tự động kết chuyển - ', v_asset_cd, ' - ', v_asset_nm);

        INSERT INTO chitinfo (
            COMPANY_CD,
            CHIT_CD,
            CHIT_NO,
            CHIT_YMD,
            CHIT_TYPE,
            INPUT_TYPE,
            LOCK_STEP_CODE,
            AMOUNT,
            ISDEL,
            CREATE_BY,
            UPDATE_BY
        ) VALUES (
            p_COMPANY_CD,
            v_chit_cd,
            LEFT(v_chit_no, 50),
            v_chit_ymd_char,
            'OT',
            'LOCK',
            'FA_PREPAID_LOCK',
            v_depre_amt,
            '0',
            p_USER_ID,
            p_USER_ID
        );

        SET v_chit_id = LAST_INSERT_ID();

        INSERT INTO chitinfo_ext (
            COMPANY_CD,
            CHIT_ID,
            CHIT_CD,
            IS_LOCK,
            ISEXCEL,
            IS_CONFIRMED,
            IS_PAYMENT,
            ISDEL,
            CREATE_BY,
            UPDATE_BY
        ) VALUES (
            p_COMPANY_CD,
            v_chit_id,
            LEFT(v_chit_cd, 20),
            '0',
            '0',
            '0',
            '0',
            '0',
            p_USER_ID,
            p_USER_ID
        );

        INSERT INTO chitdescriptioninfo (
            COMPANY_CD,
            CHIT_ID,
            CHIT_CD,
            DESC_CD,
            LANG_TYPE,
            DESCRIPTION,
            ISDEL,
            CREATE_BY,
            UPDATE_BY
        ) VALUES (
            p_COMPANY_CD,
            v_chit_id,
            LEFT(v_chit_cd, 20),
            LEFT(v_chit_cd, 20),
            'VIET',
            v_description,
            '0',
            p_USER_ID,
            p_USER_ID
        );

        OPEN cur_alloc;

        alloc_loop: LOOP
            SET v_not_found = 0;
            FETCH cur_alloc INTO
                v_post_alloc_id,
                v_alloc_seq,
                v_debit_acct,
                v_credit_acct,
                v_department_id,
                v_line_amt;

            IF v_not_found = 1 THEN
                LEAVE alloc_loop;
            END IF;

            SET v_line_seq = v_line_seq + 1;
            SET v_detail_cd = LEFT(CONCAT('D', DATE_FORMAT(NOW(6), '%Y%m%d%H%i%s%f'), LPAD(v_line_seq, 6, '0')), 30);

            INSERT INTO chitdetailinfo (
                COMPANY_CD,
                CHIT_ID,
                CHIT_CD,
                CHITDETAIL_CD,
                CHIT_YMD,
                DEBIT,
                CREDIT,
                AMOUNT,
                FC_AMOUNT,
                FC_RATE,
                SORT,
                ISDEL,
                CREATE_BY,
                UPDATE_BY
            ) VALUES (
                p_COMPANY_CD,
                v_chit_id,
                v_chit_cd,
                v_detail_cd,
                v_chit_ymd_char,
                v_debit_acct,
                v_credit_acct,
                v_line_amt,
                0,
                0,
                v_alloc_seq,
                '0',
                p_USER_ID,
                p_USER_ID
            );

            SET v_detail_id = LAST_INSERT_ID();

            INSERT INTO chitdetaildescriptioninfo (
                COMPANY_CD,
                CHITDETAIL_ID,
                CHITDETAIL_CD,
                DESC_CD,
                LANG_TYPE,
                DESCRIPTION,
                ISDEL,
                CREATE_BY,
                UPDATE_BY
            ) VALUES (
                p_COMPANY_CD,
                v_detail_id,
                LEFT(v_detail_cd, 20),
                LEFT(v_detail_cd, 20),
                'VIET',
                v_description,
                '0',
                p_USER_ID,
                p_USER_ID
            );

            SET v_department_cd = NULL;
            IF v_department_id IS NOT NULL THEN
                SELECT dep.DEPARTMENT_CD
                  INTO v_department_cd
                  FROM department_info dep
                 WHERE dep.DEPARTMENT_ID = v_department_id
                   AND dep.COMPANY_CD = p_COMPANY_CD
                 LIMIT 1;
            END IF;

            INSERT INTO chitdetailinfo_ext (
                COMPANY_CD,
                CHITDETAIL_ID,
                CHITDETAIL_CD,
                DEPARTMENT_ID,
                DEPARTMENT_CD,
                ISDEL,
                CREATE_BY,
                UPDATE_BY
            ) VALUES (
                p_COMPANY_CD,
                v_detail_id,
                LEFT(v_detail_cd, 20),
                v_department_id,
                v_department_cd,
                '0',
                p_USER_ID,
                p_USER_ID
            );

            UPDATE fa_asset_depre_posted_alloc
            SET CHITDETAIL_ID = v_detail_id
            WHERE POST_ALLOC_ID = v_post_alloc_id;
        END LOOP;

        CLOSE cur_alloc;

        UPDATE fa_asset_depre_posted
        SET CHITINFO_ID = v_chit_id,
            CHIT_NO = LEFT(v_chit_no, 50)
        WHERE POST_ID = v_post_id;
    END LOOP;

    CLOSE cur_posted;
END$$

DELIMITER ;
