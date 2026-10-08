-- Helper procedures for FA prepaid period lock (MySQL 5.6+)
-- Deploy to company database (e.g. am_web_001)

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_period_lock_fa_prepaid_last_day`$$
CREATE PROCEDURE `sp_period_lock_fa_prepaid_last_day`(
    IN p_PERIOD_YM CHAR(6),
    OUT p_LAST_DAY DATE
)
BEGIN
    SET p_LAST_DAY = LAST_DAY(STR_TO_DATE(CONCAT(p_PERIOD_YM, '01'), '%Y%m%d'));
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_fa_prepaid_resolve_depre_type`$$
CREATE PROCEDURE `sp_period_lock_fa_prepaid_resolve_depre_type`(
    IN p_PERIOD_YM CHAR(6),
    IN p_DEPRE_START_YM CHAR(6),
    IN p_DEPRE_END_YM CHAR(6),
    OUT p_DEPRE_TYPE VARCHAR(20)
)
BEGIN
    IF p_PERIOD_YM = p_DEPRE_START_YM THEN
        SET p_DEPRE_TYPE = 'FIRST';
    ELSEIF p_PERIOD_YM = p_DEPRE_END_YM THEN
        SET p_DEPRE_TYPE = 'LAST';
    ELSE
        SET p_DEPRE_TYPE = 'NORMAL';
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_fa_prepaid_next_chit_no`$$
CREATE PROCEDURE `sp_period_lock_fa_prepaid_next_chit_no`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_BASE_DATE DATETIME,
    OUT p_CHIT_NO VARCHAR(100)
)
BEGIN
    DECLARE v_id BIGINT DEFAULT NULL;
    DECLARE v_current_no BIGINT DEFAULT 0;
    DECLARE v_next_no BIGINT DEFAULT 0;
    DECLARE v_prefix VARCHAR(50) DEFAULT '';
    DECLARE v_suffix VARCHAR(50) DEFAULT '';
    DECLARE v_code_pattern VARCHAR(100) DEFAULT '{PREFIX}{NO}{SUFFIX}';
    DECLARE v_number_length INT DEFAULT 6;
    DECLARE v_reset_type VARCHAR(20) DEFAULT '';
    DECLARE v_reset_key VARCHAR(50) DEFAULT '';
    DECLARE v_year_key VARCHAR(4) DEFAULT '';
    DECLARE v_month_key VARCHAR(6) DEFAULT '';
    DECLARE v_no_text VARCHAR(50) DEFAULT '';

    SET p_CHIT_NO = NULL;
    SET v_year_key = DATE_FORMAT(p_BASE_DATE, '%Y');
    SET v_month_key = DATE_FORMAT(p_BASE_DATE, '%Y%m');

    START TRANSACTION;

    SELECT
        ID,
        CURRENT_NO,
        PREFIX,
        SUFFIX,
        IFNULL(NULLIF(CODE_PATTERN, ''), '{PREFIX}{NO}{SUFFIX}'),
        NUMBER_LENGTH,
        RESET_TYPE,
        RESET_KEY
    INTO
        v_id,
        v_current_no,
        v_prefix,
        v_suffix,
        v_code_pattern,
        v_number_length,
        v_reset_type,
        v_reset_key
    FROM sys_code_sequence
    WHERE COMPANY_CD = p_COMPANY_CD
      AND OBJECT_TYPE = 'OT'
      AND IFNULL(IS_USE, '1') IN ('1', 'Y')
    ORDER BY ID
    LIMIT 1
    FOR UPDATE;

    IF v_id IS NULL THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Chua cau hinh day so chung tu OT trong sys_code_sequence';
    END IF;

    IF v_reset_type = 'YEAR' AND IFNULL(v_reset_key, '') <> v_year_key THEN
        SET v_current_no = 0;
        SET v_reset_key = v_year_key;
    ELSEIF v_reset_type = 'MONTH' AND IFNULL(v_reset_key, '') <> v_month_key THEN
        SET v_current_no = 0;
        SET v_reset_key = v_month_key;
    END IF;

    SET v_next_no = v_current_no + 1;
    SET v_no_text = LPAD(CAST(v_next_no AS CHAR), IFNULL(v_number_length, 6), '0');

    SET p_CHIT_NO = v_code_pattern;
    SET p_CHIT_NO = REPLACE(p_CHIT_NO, '{PREFIX}', IFNULL(v_prefix, ''));
    SET p_CHIT_NO = REPLACE(p_CHIT_NO, '{SUFFIX}', IFNULL(v_suffix, ''));
    SET p_CHIT_NO = REPLACE(p_CHIT_NO, '{NO}', v_no_text);
    SET p_CHIT_NO = REPLACE(p_CHIT_NO, '{YYYY}', DATE_FORMAT(p_BASE_DATE, '%Y'));
    SET p_CHIT_NO = REPLACE(p_CHIT_NO, '{YY}', DATE_FORMAT(p_BASE_DATE, '%y'));
    SET p_CHIT_NO = REPLACE(p_CHIT_NO, '{MM}', DATE_FORMAT(p_BASE_DATE, '%m'));
    SET p_CHIT_NO = REPLACE(p_CHIT_NO, '{DD}', DATE_FORMAT(p_BASE_DATE, '%d'));

    UPDATE sys_code_sequence
    SET CURRENT_NO = v_next_no,
        RESET_KEY = v_reset_key,
        UPDATE_AT = NOW()
    WHERE ID = v_id;

    COMMIT;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_fa_prepaid_rollback_period`$$
CREATE PROCEDURE `sp_period_lock_fa_prepaid_rollback_period`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6)
)
BEGIN
    DECLARE v_last_day DATE;

    CALL sp_period_lock_fa_prepaid_last_day(p_PERIOD_YM, v_last_day);

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
      AND h.LOCK_STEP_CODE = 'FA_PREPAID_LOCK'
      AND h.CHIT_YMD = DATE_FORMAT(v_last_day, '%Y%m%d');

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
      AND h.LOCK_STEP_CODE = 'FA_PREPAID_LOCK'
      AND h.CHIT_YMD = DATE_FORMAT(v_last_day, '%Y%m%d');

    DELETE d
    FROM chitdetailinfo d
    INNER JOIN chitinfo h
        ON h.CHIT_ID = d.CHIT_ID
       AND h.COMPANY_CD = d.COMPANY_CD
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = 'FA_PREPAID_LOCK'
      AND h.CHIT_YMD = DATE_FORMAT(v_last_day, '%Y%m%d');

    DELETE desc_h
    FROM chitdescriptioninfo desc_h
    INNER JOIN chitinfo h
        ON h.CHIT_ID = desc_h.CHIT_ID
       AND h.COMPANY_CD = desc_h.COMPANY_CD
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = 'FA_PREPAID_LOCK'
      AND h.CHIT_YMD = DATE_FORMAT(v_last_day, '%Y%m%d');

    DELETE ext_h
    FROM chitinfo_ext ext_h
    INNER JOIN chitinfo h
        ON h.CHIT_ID = ext_h.CHIT_ID
       AND h.COMPANY_CD = ext_h.COMPANY_CD
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = 'FA_PREPAID_LOCK'
      AND h.CHIT_YMD = DATE_FORMAT(v_last_day, '%Y%m%d');

    DELETE h
    FROM chitinfo h
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.INPUT_TYPE = 'LOCK'
      AND h.LOCK_STEP_CODE = 'FA_PREPAID_LOCK'
      AND h.CHIT_YMD = DATE_FORMAT(v_last_day, '%Y%m%d');

    DELETE pa
    FROM fa_asset_depre_posted_alloc pa
    INNER JOIN fa_asset_depre_posted p
        ON p.POST_ID = pa.POST_ID
       AND p.COMPANY_CD = pa.COMPANY_CD
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM;

    DELETE
    FROM fa_asset_depre_posted
    WHERE COMPANY_CD = p_COMPANY_CD
      AND DEPRE_YM = p_PERIOD_YM;
END$$

DELIMITER ;
