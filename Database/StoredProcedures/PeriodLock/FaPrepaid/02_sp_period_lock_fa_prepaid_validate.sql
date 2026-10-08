DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_period_lock_fa_prepaid_validate`$$

CREATE PROCEDURE `sp_period_lock_fa_prepaid_validate`(
    IN p_COMPANY_CD VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_PERIOD_YM CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_USER_ID VARCHAR(50) CHARACTER SET utf8 COLLATE utf8_unicode_ci
)
BEGIN
    DECLARE v_asset_id BIGINT;
    DECLARE v_asset_cd VARCHAR(50) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_start_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_end_ym CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_depre_type VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_depre_amt DECIMAL(18,2);
    DECLARE v_alloc_sum DECIMAL(18,2);
    DECLARE v_msg VARCHAR(255) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_done INT DEFAULT 0;

    DECLARE cur_assets CURSOR FOR
        SELECT
            a.ASSET_ID,
            a.ASSET_CD,
            a.DEPRE_START_YM,
            a.DEPRE_END_YM,
            CASE
                WHEN p_PERIOD_YM = a.DEPRE_START_YM THEN a.FIRST_DEPRE_AMT
                WHEN p_PERIOD_YM = a.DEPRE_END_YM THEN a.LAST_DEPRE_AMT
                ELSE a.NORMAL_DEPRE_AMT
            END AS DEPRE_AMT
        FROM fa_asset a
        WHERE a.COMPANY_CD = p_COMPANY_CD
          AND IFNULL(a.ISDEL, '0') = '0'
          AND a.STATUS = 'IN_USE'
          AND p_PERIOD_YM BETWEEN a.DEPRE_START_YM AND a.DEPRE_END_YM
          AND NOT EXISTS (
              SELECT 1
              FROM fa_asset_depre_posted p
              WHERE p.COMPANY_CD = a.COMPANY_CD
                AND p.ASSET_ID = a.ASSET_ID
                AND p.DEPRE_YM = p_PERIOD_YM
                AND IFNULL(p.CANCEL_YN, 'N') = 'N'
          );

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1;

    OPEN cur_assets;

    asset_loop: LOOP
        FETCH cur_assets
        INTO v_asset_id, v_asset_cd, v_start_ym, v_end_ym, v_depre_amt;

        IF v_done = 1 THEN
            LEAVE asset_loop;
        END IF;

        CALL sp_period_lock_fa_prepaid_resolve_depre_type(
            p_PERIOD_YM,
            v_start_ym,
            v_end_ym,
            v_depre_type
        );

        IF v_depre_amt IS NULL OR v_depre_amt < 0 THEN
            SET v_msg = LEFT(CONCAT('TSCD ', v_asset_cd, ': so khau hao thang khong hop le'), 128);
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_msg;
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM fa_asset_depre_alloc al
            WHERE al.COMPANY_CD = p_COMPANY_CD
              AND al.ASSET_ID = v_asset_id
              AND IFNULL(al.ISDEL, '0') = '0'
        ) THEN
            SET v_msg = LEFT(CONCAT('TSCD ', v_asset_cd, ': chua co dong phan bo khau hao'), 128);
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_msg;
        END IF;

        IF EXISTS (
            SELECT 1
            FROM fa_asset_depre_alloc al
            WHERE al.COMPANY_CD = p_COMPANY_CD
              AND al.ASSET_ID = v_asset_id
              AND IFNULL(al.ISDEL, '0') = '0'
              AND (
                  IFNULL(al.DEBIT_ACCT_CD, '') = ''
                  OR IFNULL(al.CREDIT_ACCT_CD, '') = ''
              )
        ) THEN
            SET v_msg = LEFT(CONCAT('TSCD ', v_asset_cd, ': thieu tai khoan No/Co phan bo'), 128);
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_msg;
        END IF;

        IF v_depre_type = 'FIRST' AND EXISTS (
            SELECT 1
            FROM fa_asset_depre_alloc al
            WHERE al.COMPANY_CD = p_COMPANY_CD
              AND al.ASSET_ID = v_asset_id
              AND IFNULL(al.ISDEL, '0') = '0'
              AND (al.FIRST_ALLOC_AMT IS NULL OR al.FIRST_ALLOC_AMT < 0)
        ) THEN
            SET v_msg = LEFT(CONCAT('TSCD ', v_asset_cd, ': thieu FIRST_ALLOC_AMT'), 128);
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_msg;
        END IF;

        IF v_depre_type = 'NORMAL' AND EXISTS (
            SELECT 1
            FROM fa_asset_depre_alloc al
            WHERE al.COMPANY_CD = p_COMPANY_CD
              AND al.ASSET_ID = v_asset_id
              AND IFNULL(al.ISDEL, '0') = '0'
              AND (al.NORMAL_ALLOC_AMT IS NULL OR al.NORMAL_ALLOC_AMT < 0)
        ) THEN
            SET v_msg = LEFT(CONCAT('TSCD ', v_asset_cd, ': thieu NORMAL_ALLOC_AMT'), 128);
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_msg;
        END IF;

        IF v_depre_type = 'LAST' AND EXISTS (
            SELECT 1
            FROM fa_asset_depre_alloc al
            WHERE al.COMPANY_CD = p_COMPANY_CD
              AND al.ASSET_ID = v_asset_id
              AND IFNULL(al.ISDEL, '0') = '0'
              AND (al.LAST_ALLOC_AMT IS NULL OR al.LAST_ALLOC_AMT < 0)
        ) THEN
            SET v_msg = LEFT(CONCAT('TSCD ', v_asset_cd, ': thieu LAST_ALLOC_AMT'), 128);
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_msg;
        END IF;

        SELECT
            SUM(
                CASE v_depre_type
                    WHEN 'FIRST' THEN IFNULL(al.FIRST_ALLOC_AMT, 0)
                    WHEN 'NORMAL' THEN IFNULL(al.NORMAL_ALLOC_AMT, 0)
                    WHEN 'LAST' THEN IFNULL(al.LAST_ALLOC_AMT, 0)
                    ELSE 0
                END
            )
        INTO v_alloc_sum
        FROM fa_asset_depre_alloc al
        WHERE al.COMPANY_CD = p_COMPANY_CD
          AND al.ASSET_ID = v_asset_id
          AND IFNULL(al.ISDEL, '0') = '0';

        IF ABS(IFNULL(v_alloc_sum, 0) - IFNULL(v_depre_amt, 0)) > 0.01 THEN
            SET v_msg = LEFT(
                CONCAT(
                    'TSCD ',
                    v_asset_cd,
                    ': tong phan bo ',
                    CAST(IFNULL(v_alloc_sum, 0) AS CHAR),
                    ' khac khau hao ',
                    CAST(IFNULL(v_depre_amt, 0) AS CHAR)
                ),
                128
            );

            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_msg;
        END IF;

    END LOOP;

    CLOSE cur_assets;
END$$

DELIMITER ;